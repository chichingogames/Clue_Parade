// Pure board model: no Unity objects. Cells are [row, col]; row 0 is the top row.
// A cell is matched when its shape equals its row target and its color equals its column target.
public class BoardState
{
    private readonly TileData[,] _cells;

    public int Size { get; }
    public TileShape[] RowTargets { get; }
    public TileColor[] ColumnTargets { get; }

    public TileData this[int row, int col] => _cells[row, col];

    public BoardState(int size, TileShape[] rowTargets, TileColor[] columnTargets, TileData[,] cells)
    {
        Size = size;
        RowTargets = (TileShape[])rowTargets.Clone();
        ColumnTargets = (TileColor[])columnTargets.Clone();
        _cells = (TileData[,])cells.Clone();
    }

    public BoardState(LevelDefinition level)
        : this(level.Size, level.RowTargets, level.ColumnTargets, level.StartCells) { }

    public static BoardState Solved(int size, TileShape[] rowTargets, TileColor[] columnTargets)
    {
        var cells = new TileData[size, size];
        for (int r = 0; r < size; r++)
            for (int c = 0; c < size; c++)
                cells[r, c] = new TileData(rowTargets[r], columnTargets[c]);
        return new BoardState(size, rowTargets, columnTargets, cells);
    }

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

    public bool IsCellMatched(int row, int col) =>
        _cells[row, col].Shape == RowTargets[row] && _cells[row, col].Color == ColumnTargets[col];

    public bool IsRowComplete(int row)
    {
        for (int c = 0; c < Size; c++)
            if (_cells[row, c].Shape != RowTargets[row]) return false;
        return true;
    }

    public bool IsColumnComplete(int col)
    {
        for (int r = 0; r < Size; r++)
            if (_cells[r, col].Color != ColumnTargets[col]) return false;
        return true;
    }

    public int MatchedCount
    {
        get
        {
            int count = 0;
            for (int r = 0; r < Size; r++)
                for (int c = 0; c < Size; c++)
                    if (IsCellMatched(r, c)) count++;
            return count;
        }
    }

    public bool IsSolved => MatchedCount == Size * Size;

    public TileData[,] CopyCells() => (TileData[,])_cells.Clone();

    public int Wrap(int index) => ((index % Size) + Size) % Size;
}
