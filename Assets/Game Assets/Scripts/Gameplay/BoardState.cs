// Pure board model: no Unity objects. Cells are [row, col]; row 0 is the top row.
// Each tile knows the cell it belongs in (TileData.Id = row * Size + col). A cell is "home" (a
// matched slot) when it holds that tile. Matches are shown: the tile gets a check and the column's
// customer wears the item, for as long as it stays home.
//
// Steps: StepRows is the level's clue order (one row per category, e.g. Hats, Shirts, Bags). The
// active step is the first one whose row is not complete, so finishing a row moves the clues on,
// and breaking one brings them back. Everything is derived from the board, never stored, so Undo
// needs no special case. Solved = every slot matched at the same time.
public class BoardState
{
    private readonly TileData[,] _cells;

    public int Size { get; }
    public int[] StepRows { get; }

    public TileData this[int row, int col] => _cells[row, col];

    public BoardState(int size, int[] stepRows, TileData[,] cells)
    {
        Size = size;
        StepRows = (int[])stepRows.Clone();
        _cells = (TileData[,])cells.Clone();
    }

    public BoardState(LevelDefinition level) : this(level.Size, level.StepRows, level.StartCells) { }

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

    public int StepCount => StepRows.Length;

    // Index into StepRows of the first step whose row is not complete; StepCount when all are.
    public int ActiveStep
    {
        get
        {
            for (int i = 0; i < StepRows.Length; i++)
                if (!IsRowComplete(StepRows[i])) return i;
            return StepRows.Length;
        }
    }

    // The row the clues are about, or -1 when the board is solved.
    public int ActiveRow
    {
        get
        {
            int step = ActiveStep;
            return step < StepRows.Length ? StepRows[step] : -1;
        }
    }

    public bool IsSolved => MatchedCount == CellCount;

    // Which of a step's clues to show, as indexes into clueTiles (each clue's tile id, in the level's
    // preferred order). The first `count` are shown, except that when all of them are matched while
    // another clue of the step is not, the first is swapped for that one: the cards should point at
    // what is still wrong.
    public int[] PickClues(int[] clueTiles, int count)
    {
        count = System.Math.Min(count, clueTiles.Length);
        var picked = new int[count];
        bool allMatched = true;
        for (int i = 0; i < count; i++)
        {
            picked[i] = i;
            if (!IsTileHome(clueTiles[i])) allMatched = false;
        }
        if (!allMatched || count == 0) return picked;

        for (int i = count; i < clueTiles.Length; i++)
        {
            if (IsTileHome(clueTiles[i])) continue;
            picked[0] = i;
            break;
        }
        return picked;
    }

    public TileData[,] CopyCells() => (TileData[,])_cells.Clone();

    // Tile ids row-major (BoardSolver's input).
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
