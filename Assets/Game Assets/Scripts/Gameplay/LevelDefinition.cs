public interface ILevelSource
{
    LevelDefinition Load(int levelNumber);
}

public class LevelDefinition
{
    public int Number { get; }
    public int Size { get; }
    public TileShape[] RowTargets { get; }
    public TileColor[] ColumnTargets { get; }
    public TileData[,] StartCells { get; }
    public int MoveLimit { get; }          // 0 = unlimited

    public bool HasMoveLimit => MoveLimit > 0;

    public LevelDefinition(int number, int size, TileShape[] rowTargets, TileColor[] columnTargets,
                           TileData[,] startCells, int moveLimit)
    {
        Number = number;
        Size = size;
        RowTargets = rowTargets;
        ColumnTargets = columnTargets;
        StartCells = startCells;
        MoveLimit = moveLimit;
    }
}
