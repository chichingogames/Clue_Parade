using System;
using System.Collections.Generic;
using System.Linq;

// Pure rules of the any-row board (no Unity objects), a port of the playtest's DeliveryCore: a board
// of 3 columns x level.rows (3 or 4) item ids (null = empty), cell i = row * 3 + col. A move swaps two
// orthogonal neighbours (at least one holding an item). After it, any row whose three cells hold a
// pending set's items under their owners (item c in column c) is delivered, whether or not that set
// is the request on screen: the items leave the board for the subjects, the items above fall down
// their columns, and the next refill triple (if any) drops into the top empty cell of each column.
// Deliveries repeat until no row is ready. All sets delivered = won (a win on the last move beats
// running out); reaching the move limit (plus any continue moves) first = lost.
public enum DeliveryStatus { Playing = 0, Won = 1, Lost = 2 }

[Serializable]
public class DeliveryState
{
    public string[] Board = new string[9];
    public List<int> CompletedSets = new List<int>();
    public List<string> Delivered = new List<string>();
    public int SpawnIndex;
    public int Moves;
    public int ExtraMoves;                 // moves added by a continue
    public int ContinuesUsed;
    public DeliveryStatus Status;

    public int Stage => CompletedSets.Count;

    public DeliveryState Clone() => new DeliveryState
    {
        Board = (string[])Board.Clone(),
        CompletedSets = new List<int>(CompletedSets),
        Delivered = new List<string>(Delivered),
        SpawnIndex = SpawnIndex,
        Moves = Moves,
        ExtraMoves = ExtraMoves,
        ContinuesUsed = ContinuesUsed,
        Status = Status,
    };
}

public enum DeliveryEventType { Swap, Deliver, Fall }

// One step of a move, for the board to animate in order. Board is the board after the step.
public class DeliveryEvent
{
    public DeliveryEventType Type;
    public int A, B;                       // Swap
    public int SetIndex = -1, Row = -1;    // Deliver
    public string[] Items;                 // Deliver: by column
    public string[] Before;                // Fall: the board before items fell
    public string[] Spawned = new string[0];   // Fall: refill items that dropped in
    public string[] Board;
    public DeliveryState View;             // the whole state after the step
}

// A suggested swap: move the item at From to To (a neighbour), towards set SetIndex in TargetRow.
public class DeliveryHint
{
    public int From, To;
    public int SetIndex, TargetRow, Distance;
}

public static class DeliveryRules
{
    public const int Columns = 3;

    public static int RowsOf(string[] board) => board.Length / Columns;

    // The neighbour pairs (a < b) of a board with `rows` rows: horizontal, then vertical.
    public static (int a, int b)[] Edges(int rows)
    {
        var edges = new List<(int, int)>();
        for (int r = 0; r < rows; r++) for (int c = 0; c + 1 < Columns; c++) edges.Add((r * Columns + c, r * Columns + c + 1));
        for (int r = 0; r + 1 < rows; r++) for (int c = 0; c < Columns; c++) edges.Add((r * Columns + c, (r + 1) * Columns + c));
        return edges.ToArray();
    }

    public static bool Adjacent(int a, int b, int cells) =>
        a >= 0 && a < cells && b >= 0 && b < cells && Math.Abs(a % Columns - b % Columns) + Math.Abs(a / Columns - b / Columns) == 1;

    public static DeliveryState Create(DeliveryCase level) => new DeliveryState { Board = (string[])level.start.Clone() };

    public static int MoveBudget(DeliveryState state, DeliveryCase level) => level.HasMoveLimit ? level.moveLimit + state.ExtraMoves : 0;

    // The sets that can be delivered now: staged = the next one; choice = up to two pending sets
    // whose items are all on the board; hybrid = staged while one of the first three is pending.
    public static List<int> Eligible(DeliveryState state, DeliveryCase level)
    {
        var pending = Enumerable.Range(0, level.sets.Length).Where(i => !state.CompletedSets.Contains(i)).ToList();
        if (level.orderMode == "hybrid" && pending.Any(i => i < 3)) return pending.Where(i => i < 3).Take(1).ToList();
        if (level.orderMode != "choice" && level.orderMode != "hybrid") return pending.Take(1).ToList();
        return pending.Where(i => level.sets[i].items.All(id => Array.IndexOf(state.Board, id) >= 0)).Take(2).ToList();
    }

    // The preferred set if it is eligible, else the first eligible one (-1 when none).
    public static int CurrentSet(DeliveryState state, DeliveryCase level, int preferred)
    {
        var active = Eligible(state, level);
        return active.Contains(preferred) ? preferred : active.Count > 0 ? active[0] : -1;
    }

    // True (the game's rule): any pending set clears from any row, even when its clues are not on
    // screen. False: only the open requests clear (the playtest HTML's rule; case 17 parks its third
    // set complete in a row at the start, which this rule delivers on the first swap).
    public static bool AnyPendingSetClears = true;

    // A row holding a set that can clear under its owners, or null: the open requests are checked
    // first, then (AnyPendingSetClears) every other pending set.
    public static (int row, int index)? Ready(DeliveryState state, DeliveryCase level)
    {
        var eligible = Eligible(state, level);
        var order = !AnyPendingSetClears ? eligible : eligible.Concat(Enumerable.Range(0, level.sets.Length)
                                              .Where(i => !state.CompletedSets.Contains(i) && !eligible.Contains(i)));
        int rows = RowsOf(state.Board);
        foreach (int index in order)
            for (int row = 0; row < rows; row++)
                if (RowHolds(state.Board, row, level.sets[index])) return (row, index);
        return null;
    }

    public static bool RowHolds(string[] board, int row, DeliverySet set)
    {
        for (int c = 0; c < Columns; c++) if (board[row * Columns + c] != set.items[c]) return false;
        return true;
    }

    // Swaps a and b and resolves deliveries, falls and refills. Not accepted (state unchanged):
    // not playing, not neighbours, or both cells empty.
    public static bool Transact(DeliveryState state, DeliveryCase level, int a, int b,
                                out DeliveryState result, out List<DeliveryEvent> events)
    {
        events = new List<DeliveryEvent>();
        result = state.Clone();
        int cells = state.Board.Length, rows = RowsOf(state.Board);
        if (state.Status != DeliveryStatus.Playing || !Adjacent(a, b, cells) || (state.Board[a] == null && state.Board[b] == null)) return false;

        var s = result;
        (s.Board[a], s.Board[b]) = (s.Board[b], s.Board[a]);
        s.Moves++;
        events.Add(new DeliveryEvent { Type = DeliveryEventType.Swap, A = a, B = b, Board = Copy(s.Board), View = s.Clone() });

        for (var hit = Ready(s, level); hit.HasValue; hit = Ready(s, level))
        {
            var (row, index) = hit.Value;
            var items = (string[])level.sets[index].items.Clone();
            s.Delivered.AddRange(items);
            s.CompletedSets.Add(index);
            for (int c = 0; c < Columns; c++) s.Board[row * Columns + c] = null;
            events.Add(new DeliveryEvent { Type = DeliveryEventType.Deliver, SetIndex = index, Row = row, Items = items, Board = Copy(s.Board), View = s.Clone() });
            if (s.Stage == level.sets.Length)
            {
                if (s.Board.Any(x => x != null)) throw new InvalidOperationException("Unused items remain at completion");
                s.Status = DeliveryStatus.Won;
                break;
            }

            var before = Copy(s.Board);
            for (int c = 0; c < Columns; c++)
            {
                var survivors = new List<string>();
                for (int r = 0; r < rows; r++) if (s.Board[r * Columns + c] != null) survivors.Add(s.Board[r * Columns + c]);
                for (int r = 0; r < rows; r++) s.Board[r * Columns + c] = null;
                for (int k = 0; k < survivors.Count; k++) s.Board[(rows - survivors.Count + k) * Columns + c] = survivors[k];
            }
            var spawned = new List<string>();
            if (s.SpawnIndex < level.refills.Length)
            {
                var refill = level.refills[s.SpawnIndex++].items;
                for (int c = 0; c < Columns; c++)
                {
                    int target = -1;
                    for (int r = 0; r < rows; r++) if (s.Board[r * Columns + c] == null) { target = r * Columns + c; break; }
                    if (target < 0) throw new InvalidOperationException("No room for refill");
                    s.Board[target] = refill[c];
                    spawned.Add(refill[c]);
                }
            }
            events.Add(new DeliveryEvent { Type = DeliveryEventType.Fall, Before = before, Board = Copy(s.Board), Spawned = spawned.ToArray(), View = s.Clone() });
        }
        if (s.Status != DeliveryStatus.Won && level.HasMoveLimit && s.Moves >= MoveBudget(s, level)) s.Status = DeliveryStatus.Lost;
        return true;
    }

    // A continue after running out: +moves to this attempt, once (economy.maxContinues). Back to Playing.
    public static bool Continue(DeliveryState state, int moves, int maxContinues)
    {
        if (state.Status != DeliveryStatus.Lost || state.ContinuesUsed >= maxContinues) return false;
        state.ExtraMoves += moves;
        state.ContinuesUsed++;
        state.Status = DeliveryStatus.Playing;
        return true;
    }

    // ---------- Hint: an exact planner for three items into any row ----------
    // States are the three requested items' cells (n^3 codes, n = cells); BFS from every finished row
    // gives each arrangement's distance and the row it is nearest to. One table per board height.

    private class Planner
    {
        public int N;
        public short[] Distance;
        public sbyte[] Row;
        public (int a, int b)[] Edges;
        public int Code(int p0, int p1, int p2) => p0 + N * p1 + N * N * p2;
    }

    private static readonly Dictionary<int, Planner> Planners = new Dictionary<int, Planner>();

    private static Planner PlannerFor(int rows)
    {
        if (Planners.TryGetValue(rows, out var cached)) return cached;
        int n = rows * Columns;
        var p = new Planner { N = n, Distance = new short[n * n * n], Row = new sbyte[n * n * n], Edges = Edges(rows) };
        for (int i = 0; i < p.Distance.Length; i++) { p.Distance[i] = -1; p.Row[i] = -1; }
        var queue = new List<int[]>();
        for (int row = 0; row < rows; row++)
        {
            var start = new[] { row * Columns, row * Columns + 1, row * Columns + 2 };
            queue.Add(start);
            int k = p.Code(start[0], start[1], start[2]);
            p.Distance[k] = 0;
            p.Row[k] = (sbyte)row;
        }
        for (int q = 0; q < queue.Count; q++)
        {
            var cur = queue[q];
            int prev = p.Code(cur[0], cur[1], cur[2]);
            foreach (var (a, b) in p.Edges)
            {
                var next = SwapPositions(cur, a, b);
                int k = p.Code(next[0], next[1], next[2]);
                if (p.Distance[k] >= 0) continue;
                p.Distance[k] = (short)(p.Distance[prev] + 1);
                p.Row[k] = p.Row[prev];
                queue.Add(next);
            }
        }
        Planners[rows] = p;
        return p;
    }

    private static int[] SwapPositions(int[] p, int a, int b) => p.Select(x => x == a ? b : x == b ? a : x).ToArray();

    // The next swap towards delivering the preferred set (or the nearest eligible one), oriented
    // so From holds one of the set's items. Null when not playing or nothing to suggest.
    public static DeliveryHint Hint(DeliveryState state, DeliveryCase level, int preferred)
    {
        if (state.Status != DeliveryStatus.Playing) return null;
        var planner = PlannerFor(RowsOf(state.Board));
        var candidates = Eligible(state, level);
        if (candidates.Contains(preferred)) candidates = new List<int> { preferred };
        DeliveryHint best = null;
        foreach (int setIndex in candidates)
        {
            var set = level.sets[setIndex];
            var pos = set.items.Select(id => Array.IndexOf(state.Board, id)).ToArray();
            if (pos.Any(x => x < 0)) throw new InvalidOperationException("A requested item is missing from the board");
            int k = planner.Code(pos[0], pos[1], pos[2]), d = planner.Distance[k];
            if (d == 0) return null;
            foreach (var (a, b) in planner.Edges)
            {
                var n = SwapPositions(pos, a, b);
                int nk = planner.Code(n[0], n[1], n[2]);
                if (planner.Distance[nk] != d - 1 || planner.Row[nk] != planner.Row[k]) continue;
                if (best == null || d < best.Distance)
                {
                    bool aIsItem = Array.IndexOf(set.items, state.Board[a]) >= 0;
                    best = new DeliveryHint { From = aIsItem ? a : b, To = aIsItem ? b : a, SetIndex = setIndex, TargetRow = planner.Row[k], Distance = d };
                }
                break;
            }
        }
        if (best == null && candidates.Count > 0) throw new InvalidOperationException("No reducing adjacent swap");
        return best;
    }

    // Follows hints from the state (or the start) to the win, ignoring the move limit: the
    // validator's proof that a case can be finished. Returns the swaps.
    public static List<(int a, int b)> Solve(DeliveryCase level, DeliveryState from = null)
    {
        var s = (from ?? Create(level)).Clone();
        s.Status = DeliveryStatus.Playing;
        int limit = level.moveLimit;
        level.moveLimit = 0;
        try
        {
            var route = new List<(int, int)>();
            for (int n = 0; n < 500 && s.Status != DeliveryStatus.Won; n++)
            {
                var h = Hint(s, level, -1) ?? throw new InvalidOperationException("Unresolved level has no hint");
                Transact(s, level, h.From, h.To, out s, out _);
                route.Add((h.From, h.To));
            }
            if (s.Status != DeliveryStatus.Won) throw new InvalidOperationException("Hint route did not complete");
            return route;
        }
        finally { level.moveLimit = limit; }
    }

    // Every item is on the board, delivered or still to come, exactly once.
    public static void CheckInvariant(DeliveryState state, DeliveryCase level)
    {
        if (state.Board.Length != level.Cells || state.Delivered.Count != state.Stage * 3 || state.CompletedSets.Distinct().Count() != state.Stage)
            throw new InvalidOperationException("Invalid progress state");
        var all = state.Board.Where(x => x != null).Concat(state.Delivered)
                       .Concat(level.refills.Skip(state.SpawnIndex).SelectMany(r => r.items)).ToList();
        var expected = level.items.Select(i => i.id).ToList();
        if (all.Count != expected.Count || all.Distinct().Count() != expected.Count || expected.Any(x => !all.Contains(x)))
            throw new InvalidOperationException("Item inventory mismatch");
        foreach (int i in state.CompletedSets)
            if (i < 0 || i >= level.sets.Length || level.sets[i].items.Any(id => !state.Delivered.Contains(id)))
                throw new InvalidOperationException("Delivered set mismatch");
        if (state.Status == DeliveryStatus.Playing)
        {
            var active = Eligible(state, level);
            if (active.Count == 0) throw new InvalidOperationException("No eligible request");
            foreach (int i in active)
                if (level.sets[i].items.Any(x => Array.IndexOf(state.Board, x) < 0)) throw new InvalidOperationException("Request is not available");
        }
    }

    private static string[] Copy(string[] board) => (string[])board.Clone();
}
