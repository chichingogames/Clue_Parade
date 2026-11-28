// Pure board model: no Unity objects. Cells are [row, col]; row 0 is the top row.
// Each tile knows the cell it belongs in (TileData.Id = row * Size + col). A cell is "home" (a
// matched slot) when it holds that tile. Matches are shown: the tile gets a check and the column's
// subject shows the fitted part, for as long as it stays home. Correct tiles stay movable: nothing
// is locked or banked, everything is derived from the board, so Undo needs no special case.
// Solved = every slot matched at the same time.
public class BoardState
{
    private readonly TileData[,] _cells;

    public int Size { get; }

    public TileData this[int row, int col] => _cells[row, col];

    public BoardState(int size, TileData[,] cells)
    {
        Size = size;
        _cells = (TileData[,])cells.Clone();
    }

    public BoardState(LevelDefinition level) : this(level.Size, level.StartCells) { }

    // dir +1 = right, -1 = left. The tile pushed off one end wraps to the other.
    public void ShiftRow(int row, int dir)
    {
        var line = new TileData[Size];
        for (int c = 0; c < Size; c++) line[Wrap(c + dir)] = _cells[row, c];
        for (int c = 0; c < Size; c++) _cells[row, c] = line[c];
    }

    // dir +1 = down (towards higher row index), -1 = up.
    public void ShiftColumn(int col, int dir)
    {
        var line = new TileData[Size];
        for (int r = 0; r < Size; r++) line[Wrap(r + dir)] = _cells[r, col];
        for (int r = 0; r < Size; r++) _cells[r, col] = line[r];
    }

    public bool IsHome(int row, int col) => _cells[row, col].Id == row * Size + col;

    // Whether the tile that belongs in cell `tileId` is there now (false for -1: no tile).
    public bool IsTileHome(int tileId) => tileId >= 0 && tileId < Size * Size && IsHome(tileId / Size, tileId % Size);

    public bool IsRowComplete(int row)
    {
        for (int c = 0; c < Size; c++)
            if (!IsHome(row, c)) return false;
        return true;
    }

    public int MatchedCount
    {
        get
        {
            int matched = 0;
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    if (IsHome(r, c)) matched++;
            return matched;
        }
    }

    public int CellCount => Size * Size;

    public bool IsSolved => MatchedCount == CellCount;

    // The top row that still has an unmatched slot, or -1 when the board is solved.
    public int FirstIncompleteRow
    {
        get
        {
            for (int r = 0; r < Size; r++)
                if (!IsRowComplete(r)) return r;
            return -1;
        }
    }

    // Which `count` of a row's clues to show, as column indexes: the unmatched ones first (left to
    // right), then matched ones to fill up. The cards point at what is still wrong.
    public int[] PickClues(int row, int count)
    {
        count = System.Math.Min(count, Size);
        var picked = new int[count];
        int n = 0;
        for (int pass = 0; pass < 2 && n < count; pass++)
            for (int c = 0; c < Size && n < count; c++)
                if (IsHome(row, c) == (pass == 1)) picked[n++] = c;
        return picked;
    }

    public TileData[,] CopyCells() => (TileData[,])_cells.Clone();

    // Tile ids row-major (= each cell's item as an index into the goal board).
    public int[] TileIds()
    {
        var ids = new int[Size * Size];
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                ids[r * Size + c] = _cells[r, c].Id;
        return ids;
    }

    public bool SameCells(TileData[,] cells)
    {
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                if (!_cells[r, c].Equals(cells[r, c])) return false;
        return true;
    }

    public int Wrap(int index) => ((index % Size) + Size) % Size;
}
