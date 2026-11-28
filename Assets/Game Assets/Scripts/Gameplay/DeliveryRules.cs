using System;
using System.Collections.Generic;
using System.Linq;

// Pure rules of the any-row board (no Unity objects): a 3x3 board of item ids (null = empty).
// A move swaps two orthogonal neighbours (at least one holding an item). After it, any row whose
// three cells hold an eligible set's items under their owners (item c in column c) is delivered:
// the items leave the board for the subjects, the items above fall down their columns, and the
// next refill triple (if any) drops into the top empty cell of each column. Deliveries repeat
// until no row is ready. All sets delivered = won; reaching a move limit first = lost.
public enum DeliveryStatus { Playing = 0, Won = 1, Lost = 2 }

public class DeliveryState
{
    public string[] Board = new string[9];
    public List<int> CompletedSets = new List<int>();
    public List<string> Delivered = new List<string>();
    public int SpawnIndex;
    public int Moves;
    public DeliveryStatus Status;

    public int Stage => CompletedSets.Count;

    public DeliveryState Clone() => new DeliveryState
    {
        Board = (string[])Board.Clone(),
        CompletedSets = new List<int>(CompletedSets),
        Delivered = new List<string>(Delivered),
        SpawnIndex = SpawnIndex,
        Moves = Moves,
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
    public const int Size = 3;
    public const int Cells = 9;

    // The 12 neighbour pairs (a < b): horizontal, then vertical.
    public static readonly (int a, int b)[] Edges = BuildEdges();

    private static (int, int)[] BuildEdges()
    {
        var edges = new List<(int, int)>();
        for (int r = 0; r < Size; r++) for (int c = 0; c + 1 < Size; c++) edges.Add((r * Size + c, r * Size + c + 1));
        for (int r = 0; r + 1 < Size; r++) for (int c = 0; c < Size; c++) edges.Add((r * Size + c, (r + 1) * Size + c));
        return edges.ToArray();
    }

    public static bool Adjacent(int a, int b) =>
        a >= 0 && a < Cells && b >= 0 && b < Cells && Math.Abs(a % Size - b % Size) + Math.Abs(a / Size - b / Size) == 1;

    public static DeliveryState Create(DeliveryCase level) => new DeliveryState { Board = (string[])level.start.Clone() };

    // The sets that can be delivered now: staged = the next one; choice = up to two pending sets
    // whose items are all on the board.
    public static List<int> Eligible(DeliveryState state, DeliveryCase level)
    {
        var pending = Enumerable.Range(0, level.sets.Length).Where(i => !state.CompletedSets.Contains(i)).ToList();
        if (!level.IsChoice) return pending.Take(1).ToList();
        return pending.Where(i => level.sets[i].items.All(id => Array.IndexOf(state.Board, id) >= 0)).Take(2).ToList();
    }

    // The preferred set if it is eligible, else the first eligible one (-1 when none).
    public static int CurrentSet(DeliveryState state, DeliveryCase level, int preferred)
    {
        var active = Eligible(state, level);
        return active.Contains(preferred) ? preferred : active.Count > 0 ? active[0] : -1;
    }

    // A row holding an eligible set under its owners, or null.
    public static (int row, int index)? Ready(DeliveryState state, DeliveryCase level)
    {
        var eligible = Eligible(state, level);
        for (int row = 0; row < Size; row++)
            foreach (int index in eligible)
                if (RowHolds(state.Board, row, level.sets[index])) return (row, index);
        return null;
    }

    public static bool RowHolds(string[] board, int row, DeliverySet set)
    {
        for (int c = 0; c < Size; c++) if (board[row * Size + c] != set.items[c]) return false;
        return true;
    }

    // How many of a set's items are under their owner in this row.
    public static int AlignedInRow(string[] board, int row, DeliverySet set)
    {
        int n = 0;
        for (int c = 0; c < Size; c++) if (board[row * Size + c] == set.items[c]) n++;
        return n;
    }

    // Swaps a and b and resolves deliveries, falls and refills. Not accepted (state unchanged):
    // not playing, not neighbours, or both cells empty.
    public static bool Transact(DeliveryState state, DeliveryCase level, int a, int b,
                                out DeliveryState result, out List<DeliveryEvent> events)
    {
        events = new List<DeliveryEvent>();
        result = state.Clone();
        if (state.Status != DeliveryStatus.Playing || !Adjacent(a, b) || (state.Board[a] == null && state.Board[b] == null)) return false;

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
            for (int c = 0; c < Size; c++) s.Board[row * Size + c] = null;
            events.Add(new DeliveryEvent { Type = DeliveryEventType.Deliver, SetIndex = index, Row = row, Items = items, Board = Copy(s.Board), View = s.Clone() });
            if (s.Stage == level.sets.Length)
            {
                if (s.Board.Any(x => x != null)) throw new InvalidOperationException("Unused items remain at completion");
                s.Status = DeliveryStatus.Won;
                break;
            }

            var before = Copy(s.Board);
            for (int c = 0; c < Size; c++)
            {
                var survivors = new List<string>();
                for (int r = 0; r < Size; r++) if (s.Board[r * Size + c] != null) survivors.Add(s.Board[r * Size + c]);
                for (int r = 0; r < Size; r++) s.Board[r * Size + c] = null;
                for (int k = 0; k < survivors.Count; k++) s.Board[(Size - survivors.Count + k) * Size + c] = survivors[k];
            }
            var spawned = new List<string>();
            if (s.SpawnIndex < level.refills.Length)
            {
                var refill = level.refills[s.SpawnIndex++].items;
                for (int c = 0; c < Size; c++)
                {
                    int target = -1;
                    for (int r = 0; r < Size; r++) if (s.Board[r * Size + c] == null) { target = r * Size + c; break; }
                    if (target < 0) throw new InvalidOperationException("No room for refill");
                    s.Board[target] = refill[c];
                    spawned.Add(refill[c]);
                }
            }
            events.Add(new DeliveryEvent { Type = DeliveryEventType.Fall, Before = before, Board = Copy(s.Board), Spawned = spawned.ToArray(), View = s.Clone() });
        }
        if (s.Status != DeliveryStatus.Won && level.HasMoveLimit && s.Moves >= level.moveLimit) s.Status = DeliveryStatus.Lost;
        return true;
    }

    // ---------- Hint: an exact planner for three items into any row ----------
    // States are the three requested items' cells (9^3 = 729 codes); BFS from every finished row
    // gives each arrangement's distance and the row it is nearest to.

    private static short[] _distance;
    private static sbyte[] _row;

    private static int Code(int p0, int p1, int p2) => p0 + 9 * p1 + 81 * p2;

    private static void BuildTable()
    {
        if (_distance != null) return;
        var distance = new short[729];
        var targetRow = new sbyte[729];
        for (int i = 0; i < 729; i++) { distance[i] = -1; targetRow[i] = -1; }
        var queue = new List<int[]>();
        for (int row = 0; row < Size; row++)
        {
            var p = new[] { row * 3, row * 3 + 1, row * 3 + 2 };
            queue.Add(p);
            distance[Code(p[0], p[1], p[2])] = 0;
            targetRow[Code(p[0], p[1], p[2])] = (sbyte)row;
        }
        for (int q = 0; q < queue.Count; q++)
        {
            var cur = queue[q];
            int prev = Code(cur[0], cur[1], cur[2]);
            foreach (var (a, b) in Edges)
            {
                var p = SwapPositions(cur, a, b);
                int k = Code(p[0], p[1], p[2]);
                if (distance[k] >= 0) continue;
                distance[k] = (short)(distance[prev] + 1);
                targetRow[k] = targetRow[prev];
                queue.Add(p);
            }
        }
        _distance = distance;
        _row = targetRow;
    }

    private static int[] SwapPositions(int[] p, int a, int b) => p.Select(x => x == a ? b : x == b ? a : x).ToArray();

    // The next swap towards delivering the preferred set (or the nearest eligible one), oriented
    // so From holds one of the set's items. Null when not playing or nothing to suggest.
    public static DeliveryHint Hint(DeliveryState state, DeliveryCase level, int preferred)
    {
        if (state.Status != DeliveryStatus.Playing) return null;
        BuildTable();
        var candidates = Eligible(state, level);
        if (candidates.Contains(preferred)) candidates = new List<int> { preferred };
        DeliveryHint best = null;
        foreach (int setIndex in candidates)
        {
            var set = level.sets[setIndex];
            var pos = set.items.Select(id => Array.IndexOf(state.Board, id)).ToArray();
            if (pos.Any(x => x < 0)) throw new InvalidOperationException("A requested item is missing from the board");
            int k = Code(pos[0], pos[1], pos[2]), d = _distance[k];
            if (d == 0) return null;
            foreach (var (a, b) in Edges)
            {
                var n = SwapPositions(pos, a, b);
                int nk = Code(n[0], n[1], n[2]);
                if (_distance[nk] != d - 1 || _row[nk] != _row[k]) continue;
                if (best == null || d < best.Distance)
                {
                    bool aIsItem = Array.IndexOf(set.items, state.Board[a]) >= 0;
                    best = new DeliveryHint { From = aIsItem ? a : b, To = aIsItem ? b : a, SetIndex = setIndex, TargetRow = _row[k], Distance = d };
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
            for (int n = 0; n < 150 && s.Status != DeliveryStatus.Won; n++)
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
        if (state.Board.Length != Cells || state.Delivered.Count != state.Stage * 3 || state.CompletedSets.Distinct().Count() != state.Stage)
            throw new InvalidOperationException("Invalid progress state");
        var all = state.Board.Where(x => x != null).Concat(state.Delivered)
                       .Concat(level.refills.Skip(state.SpawnIndex).SelectMany(r => r.items)).ToList();
        var expected = level.items.Select(i => i.id).ToList();
        if (all.Count != expected.Count || all.Distinct().Count() != expected.Count || expected.Any(x => !all.Contains(x)))
            throw new InvalidOperationException("Item inventory mismatch");
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
