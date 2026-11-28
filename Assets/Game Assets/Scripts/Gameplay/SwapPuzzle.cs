using System;

// Shortest solves for the swap board (3x3): a breadth-first search from the solved board over
// every one of the 9! = 362,880 arrangements, where a move swaps two orthogonal neighbours (12
// pairs). Built once, on first use (a fraction of a second), and kept for the session. A board is
// given as its tile ids row-major (BoardState.TileIds: each cell's index into the goal board), so
// the solved board is 0..8.
public static class SwapPuzzle
{
    public const int Size = 3;
    private const int Cells = Size * Size;
    private const int States = 362880;
    private const byte Unknown = 255;

    private static readonly int[] Factorial = { 1, 1, 2, 6, 24, 120, 720, 5040, 40320 };
    private static byte[] _distances;

    // Every neighbour pair (a < b, cell indexes), in hint order: horizontal pairs first, top to bottom.
    private static readonly (int a, int b)[] Pairs = BuildPairs();

    private static (int, int)[] BuildPairs()
    {
        var pairs = new System.Collections.Generic.List<(int, int)>();
        for (int r = 0; r < Size; r++)
            for (int c = 0; c + 1 < Size; c++) pairs.Add((r * Size + c, r * Size + c + 1));
        for (int r = 0; r + 1 < Size; r++)
            for (int c = 0; c < Size; c++) pairs.Add((r * Size + c, (r + 1) * Size + c));
        return pairs.ToArray();
    }

    // Swaps of a shortest solve, or -1 when the board is not a 3x3 arrangement of 0..8.
    public static int Distance(int[] ids)
    {
        if (!IsBoard(ids)) return -1;
        return Table()[Rank(Pack(ids))];
    }

    // The first swap of a shortest solve from this board; false when it is solved (or not 3x3).
    // preferTile: among equally short swaps, one that moves this tile (the item a hint is about).
    public static bool TryNextMove(int[] ids, out SwapMove move, int preferTile = -1)
    {
        move = null;
        if (!IsBoard(ids)) return false;
        var table = Table();
        long state = Pack(ids);
        int d = table[Rank(state)];
        if (d == 0) return false;
        foreach (var (a, b) in Pairs)
        {
            if (table[Rank(SwapDigits(state, a, b))] != d - 1) continue;
            bool preferred = ids[a] == preferTile || ids[b] == preferTile;
            if (move != null && !preferred) continue;
            move = new SwapMove { row = a / Size, col = a % Size, horizontal = b == a + 1 };
            if (preferred || preferTile < 0) return true;
        }
        return move != null;
    }

    private static bool IsBoard(int[] ids)
    {
        if (ids == null || ids.Length != Cells) return false;
        int seen = 0;
        foreach (int id in ids)
        {
            if (id < 0 || id >= Cells || (seen & (1 << id)) != 0) return false;
            seen |= 1 << id;
        }
        return true;
    }

    private static byte[] Table()
    {
        if (_distances != null) return _distances;
        var distances = new byte[States];
        for (int i = 0; i < States; i++) distances[i] = Unknown;

        var queue = new long[States];
        long solved = 0;
        for (int i = 0; i < Cells; i++) solved |= (long)i << (4 * i);
        distances[Rank(solved)] = 0;
        queue[0] = solved;
        int head = 0, tail = 1;
        while (head < tail)
        {
            long state = queue[head++];
            byte next = (byte)(distances[Rank(state)] + 1);
            foreach (var (a, b) in Pairs)
            {
                long neighbour = SwapDigits(state, a, b);
                int rank = Rank(neighbour);
                if (distances[rank] != Unknown) continue;
                distances[rank] = next;
                queue[tail++] = neighbour;
            }
        }
        return _distances = distances;
    }

    // 4 bits per cell, cell 0 lowest.
    private static long Pack(int[] ids)
    {
        long state = 0;
        for (int i = 0; i < Cells; i++) state |= (long)ids[i] << (4 * i);
        return state;
    }

    private static long SwapDigits(long state, int a, int b)
    {
        long da = (state >> (4 * a)) & 0xF, db = (state >> (4 * b)) & 0xF;
        state &= ~((0xFL << (4 * a)) | (0xFL << (4 * b)));
        return state | (db << (4 * a)) | (da << (4 * b));
    }

    // Lehmer code: the arrangement's index among all 9! orders.
    private static int Rank(long state)
    {
        int rank = 0, used = 0;
        for (int i = 0; i < Cells; i++)
        {
            int digit = (int)((state >> (4 * i)) & 0xF);
            int smallerUnused = digit - BitCount(used & ((1 << digit) - 1));
            rank += smallerUnused * Factorial[Cells - 1 - i];
            used |= 1 << digit;
        }
        return rank;
    }

    private static int BitCount(int v)
    {
        int n = 0;
        for (; v != 0; v &= v - 1) n++;
        return n;
    }
}

// One swap: the tile in (row, col) trades places with its right neighbour (horizontal) or the one
// below it. A swipe either way between the two is the same swap.
[Serializable]
public class SwapMove
{
    public int row;
    public int col;
    public bool horizontal;

    public int OtherRow => horizontal ? row : row + 1;
    public int OtherCol => horizontal ? col + 1 : col;

    // The swap between (row, col) and its neighbour at (row + dRow, col + dCol), or null when that
    // is off a board of `size` or not a neighbour.
    public static SwapMove Between(int row, int col, int dRow, int dCol, int size)
    {
        if (Math.Abs(dRow) + Math.Abs(dCol) != 1) return null;
        int r2 = row + dRow, c2 = col + dCol;
        if (row < 0 || col < 0 || row >= size || col >= size || r2 < 0 || c2 < 0 || r2 >= size || c2 >= size) return null;
        return new SwapMove { row = Math.Min(row, r2), col = Math.Min(col, c2), horizontal = dRow == 0 };
    }

    // "top left <-> top middle" (3x3 names; other sizes use numbers).
    public string Describe(int size) => $"{Place(row, col, size)} <-> {Place(OtherRow, OtherCol, size)}";

    private static string Place(int r, int c, int size)
    {
        string[] rows = { "top", "middle", "bottom" };
        string[] cols = { "left", "middle", "right" };
        return size == 3 ? $"{rows[r]} {cols[c]}" : $"({r},{c})";
    }
}
