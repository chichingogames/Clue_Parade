using System;
using System.Collections.Generic;

// Exact shortest-route solver for small wrapping boards (port of the prototype's bidirectional
// breadth-first search). Pure: no Unity objects, safe to run on a worker thread (Manager_Flow's Hint).
// A board is its tile ids row-major (tiles[i] = the id in cell i); solved = 0, 1, 2, ... A state is
// packed 4 bits per cell into a ulong, so boards up to 4x4 fit.
public static class BoardSolver
{
    public const int DefaultMaxNodes = 200000;

    // The shortest move list from tiles to solved; empty if already solved, null if unreachable.
    // Throws InvalidOperationException past maxNodes visited states.
    public static List<LevelMove> Solve(int[] tiles, int size, int maxNodes = DefaultMaxNodes)
    {
        if (size < 2 || size > 4 || tiles.Length != size * size) throw new ArgumentException("Unsupported board.");

        ulong start = Pack(tiles);
        var goalTiles = new int[tiles.Length];
        for (int i = 0; i < goalTiles.Length; i++) goalTiles[i] = i;
        ulong goal = Pack(goalTiles);
        if (start == goal) return new List<LevelMove>();

        var moves = AllMoves(size);
        // State -> (previous state, move index that led here); the roots map to (root, -1).
        var fromStart = new Dictionary<ulong, (ulong prev, int move)> { [start] = (start, -1) };
        var fromGoal = new Dictionary<ulong, (ulong prev, int move)> { [goal] = (goal, -1) };
        var frontStart = new List<ulong> { start };
        var frontGoal = new List<ulong> { goal };
        var buffer = new int[tiles.Length];

        while (frontStart.Count > 0 && frontGoal.Count > 0)
        {
            bool forward = frontStart.Count <= frontGoal.Count;
            var seen = forward ? fromStart : fromGoal;
            var other = forward ? fromGoal : fromStart;
            var next = new List<ulong>();

            foreach (ulong key in forward ? frontStart : frontGoal)
            {
                for (int m = 0; m < moves.Length; m++)
                {
                    Unpack(key, buffer);
                    Apply(buffer, size, moves[m]);
                    ulong candidate = Pack(buffer);
                    if (seen.ContainsKey(candidate)) continue;
                    seen[candidate] = (key, m);
                    next.Add(candidate);
                    if (other.ContainsKey(candidate)) return Join(candidate, fromStart, fromGoal, moves);
                    if (fromStart.Count + fromGoal.Count > maxNodes) throw new InvalidOperationException("Solver search limit.");
                }
            }
            if (forward) frontStart = next;
            else frontGoal = next;
        }
        return null;
    }

    private static List<LevelMove> Join(ulong meet, Dictionary<ulong, (ulong prev, int move)> fromStart,
                                        Dictionary<ulong, (ulong prev, int move)> fromGoal, LevelMove[] moves)
    {
        var route = new List<LevelMove>();
        for (ulong key = meet; fromStart[key].move >= 0; key = fromStart[key].prev) route.Add(moves[fromStart[key].move]);
        route.Reverse();
        // Walking back towards the goal means undoing each move the goal side made.
        for (ulong key = meet; fromGoal[key].move >= 0; key = fromGoal[key].prev) route.Add(Inverse(moves[fromGoal[key].move]));
        return route;
    }

    private static LevelMove[] AllMoves(int size)
    {
        var list = new List<LevelMove>();
        foreach (string axis in new[] { "row", "column" })
            for (int line = 0; line < size; line++)
                foreach (int dir in new[] { -1, 1 })
                    list.Add(new LevelMove { axis = axis, line = line, direction = dir });
        return list.ToArray();
    }

    private static LevelMove Inverse(LevelMove move) => new LevelMove { axis = move.axis, line = move.line, direction = -move.direction };

    // Same rule as BoardState.ShiftRow / ShiftColumn: direction + = right / down, wrapping.
    private static void Apply(int[] tiles, int size, LevelMove move)
    {
        var line = new int[size];
        for (int n = 0; n < size; n++)
        {
            int to = ((n + move.direction) % size + size) % size;
            line[to] = move.IsRow ? tiles[move.line * size + n] : tiles[n * size + move.line];
        }
        for (int n = 0; n < size; n++)
        {
            if (move.IsRow) tiles[move.line * size + n] = line[n];
            else tiles[n * size + move.line] = line[n];
        }
    }

    private static ulong Pack(int[] tiles)
    {
        ulong key = 0;
        for (int i = 0; i < tiles.Length; i++) key |= (ulong)tiles[i] << (4 * i);
        return key;
    }

    private static void Unpack(ulong key, int[] tiles)
    {
        for (int i = 0; i < tiles.Length; i++) tiles[i] = (int)((key >> (4 * i)) & 0xF);
    }
}
