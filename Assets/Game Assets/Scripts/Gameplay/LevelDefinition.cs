public interface ILevelSource
{
    // Returns null when this source has no such level.
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

    // Authored text; empty for generated levels.
    public string Name { get; }
    public string Instruction { get; }
    public string Tutorial { get; }        // "", "row" or "column"
    public LevelMove[] Solution { get; }   // a known solve from the start board; empty for generated levels

    public bool HasMoveLimit => MoveLimit > 0;

    public LevelDefinition(int number, int size, TileShape[] rowTargets, TileColor[] columnTargets,
                           TileData[,] startCells, int moveLimit,
                           string name = "", string instruction = "", string tutorial = "",
                           LevelMove[] solution = null)
    {
        Number = number;
        Size = size;
        RowTargets = rowTargets;
        ColumnTargets = columnTargets;
        StartCells = startCells;
        MoveLimit = moveLimit;
        Name = name ?? "";
        Instruction = instruction ?? "";
        Tutorial = tutorial ?? "";
        Solution = solution ?? new LevelMove[0];
    }
}
