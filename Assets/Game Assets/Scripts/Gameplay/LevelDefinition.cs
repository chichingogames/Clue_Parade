public interface ILevelSource
{
    // Returns null when this source has no such level.
    LevelDefinition Load(int levelNumber);
}

// One Clue Parade level: an N x N board whose columns are characters and rows are item categories.
// Each tile is an item; solved, every character has their item in every category. The level is
// played in steps, one row each, with clues that tell which character gets which item.
public class LevelDefinition
{
    public int Number { get; }
    public int Size { get; }
    public TileData[,] StartCells { get; }
    public int MoveLimit { get; }          // 0 = unlimited

    public string Name { get; }
    public string[] Characters { get; }    // column c's character id (FileRef.GetCharacter)
    public string[] Categories { get; }    // row r's category label ("Hats")
    public string[] Items { get; }         // item id of the tile whose home is cell id (row-major), FileRef.GetItem
    public LevelStep[] Steps { get; }
    public LevelMove[] Solution { get; }   // a known solve from the start board

    // Onboarding flow (optional in the file; empty / null when absent).
    public string Subtitle { get; set; } = "";
    public string Coach { get; set; } = "";
    public string Completion { get; set; } = "";
    public LevelMove RequiredFirstMove { get; set; }
    public LevelLesson Lesson { get; set; }

    public bool HasMoveLimit => MoveLimit > 0;

    public int[] StepRows
    {
        get
        {
            var rows = new int[Steps.Length];
            for (int i = 0; i < Steps.Length; i++) rows[i] = Steps[i].row;
            return rows;
        }
    }

    public string ItemFor(TileData tile) => Items[tile.Id];

    // The tile id (= home cell) of an item, or -1 if it is not on this board.
    public int TileOf(string itemId) => System.Array.IndexOf(Items, itemId);

    public LevelDefinition(int number, int size, TileData[,] startCells, int moveLimit, string name,
                           string[] characters, string[] categories, string[] items, LevelStep[] steps,
                           LevelMove[] solution = null)
    {
        Number = number;
        Size = size;
        StartCells = startCells;
        MoveLimit = moveLimit;
        Name = name ?? "";
        Characters = characters;
        Categories = categories;
        Items = items;
        Steps = steps;
        Solution = solution ?? new LevelMove[0];
    }
}
