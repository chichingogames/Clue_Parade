// Pure board model: no Unity objects. Cells are [row, col]; row 0 is the top row.
// Each tile knows the cell it belongs in (TileData.Id = row * Size + col). A cell is "home" when it
// holds that tile.
//
// Steps: the level confirms one row per step, in StepRows order. Everything about progress is
// derived from the board, never stored: ConfirmedSteps counts the leading steps whose rows are
// complete, so a move that breaks an earlier row takes the level back to that step, and Undo
// repairs it. StepRows covers every row once, so all steps confirmed == solved.
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

    public bool IsRowComplete(int row)
    {
        for (int c = 0; c < Size; c++)
            if (!IsHome(row, c)) return false;
        return true;
    }

    public int StepCount => StepRows.Length;

    // Leading steps whose rows are complete.
    public int ConfirmedSteps
    {
        get
        {
            int confirmed = 0;
            while (confirmed < StepRows.Length && IsRowComplete(StepRows[confirmed])) confirmed++;
            return confirmed;
        }
    }

    // Index into StepRows of the step being played; StepCount when every step is confirmed.
    public int ActiveStep => ConfirmedSteps;

    // The row the active step asks for, or -1 when the board is solved.
    public int ActiveRow => ConfirmedSteps < StepRows.Length ? StepRows[ConfirmedSteps] : -1;

    public bool IsRowConfirmed(int row)
    {
        int confirmed = ConfirmedSteps;
        for (int i = 0; i < confirmed; i++)
            if (StepRows[i] == row) return true;
        return false;
    }

    public bool IsSolved => ConfirmedSteps == StepRows.Length;

    public TileData[,] CopyCells() => (TileData[,])_cells.Clone();

    public int Wrap(int index) => ((index % Size) + Size) % Size;
}
