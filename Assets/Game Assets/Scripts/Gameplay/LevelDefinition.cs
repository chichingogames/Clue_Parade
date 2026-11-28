public interface ILevelSource
{
    // Number of levels, numbered 1..Count without gaps.
    int Count { get; }

    // Returns null when this source has no such level.
    LevelDefinition Load(int levelNumber);
}

// One Tilewrap case: an N x N board whose columns are subjects (Mia, the Trail bag, the Kitchen) and
// rows are categories (Hats, Gear, Walls). Each tile is an item with one target cell; the case is
// solved when all N*N items are in their target cells at the same time. Each item carries the
// clue fact that says where it goes ("Trail bag gets the raincoat."). Converted from the content
// pack's case file by CaseLevelSource. A move swaps two neighbouring tiles (SwapMove).
public class LevelDefinition
{
    public int Number { get; }
    public int Size { get; }
    public TileData[,] StartCells { get; }
    public int MoveLimit { get; }          // swaps; 0 = unlimited
    public int MinimumMoves { get; }       // swaps of a shortest solve (SwapPuzzle)

    public string Title { get; }
    public string Goal { get; }            // "Match all 9 items using the clues."
    public string Setup { get; }           // "Pack three bags for three different trips."
    public string Completion { get; }      // the win popup's line
    public string Difficulty { get; }      // "tutorial", "practice", "hard"
    public int ChapterId { get; }
    public int NextLevelId { get; }        // 0 = the last case (the map follows)
    public int FirstClearCoins { get; }

    public LevelSubject[] Subjects { get; }   // column c's subject
    public string[] Categories { get; }       // row r's label ("Hats")
    public LevelItem[] Items { get; }         // indexed by tile id (= target cell, row-major)

    public bool HasMoveLimit => MoveLimit > 0;
    public bool IsHard => Difficulty == "hard";

    public LevelItem ItemFor(TileData tile) => Items[tile.Id];

    public LevelDefinition(int number, int size, TileData[,] startCells, int moveLimit, int minimumMoves,
                           string title, string goal, string setup, string completion, string difficulty,
                           int chapterId, int nextLevelId, int firstClearCoins,
                           LevelSubject[] subjects, string[] categories, LevelItem[] items)
    {
        Number = number;
        Size = size;
        StartCells = startCells;
        MoveLimit = moveLimit;
        MinimumMoves = minimumMoves;
        Title = title ?? "";
        Goal = goal ?? "";
        Setup = setup ?? "";
        Completion = completion ?? "";
        Difficulty = difficulty ?? "";
        ChapterId = chapterId;
        NextLevelId = nextLevelId;
        FirstClearCoins = firstClearCoins;
        Subjects = subjects;
        Categories = categories;
        Items = items;
    }
}

// A board column's subject: its name and the registered rig (FileRef.Rig) drawn above the board.
public class LevelSubject
{
    public string Id;
    public string Name;
    public string RigId;
}

// A board item: its name (shown on the tile), its icon (FileRef.Sprite) and its clue fact.
public class LevelItem
{
    public string Id;
    public string Label;
    public string Sprite;
    public string Clue;
}
