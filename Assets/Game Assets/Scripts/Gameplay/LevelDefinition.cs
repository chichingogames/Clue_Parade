using System;

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
// pack's case file by CaseLevelSource.
public class LevelDefinition
{
    public int Number { get; }
    public int Size { get; }
    public TileData[,] StartCells { get; }
    public int MoveLimit { get; }          // 0 = unlimited
    public int MinimumMoves { get; }       // verified shortest solve

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
    public LevelMove[] Solution { get; }      // a known solve from the start board

    // Onboarding (optional in the file).
    public LevelMove RequiredFirstMove { get; set; }
    public string TutorialText { get; set; } = "";

    public bool HasMoveLimit => MoveLimit > 0;
    public bool IsHard => Difficulty == "hard";

    public LevelItem ItemFor(TileData tile) => Items[tile.Id];

    public LevelDefinition(int number, int size, TileData[,] startCells, int moveLimit, int minimumMoves,
                           string title, string goal, string setup, string completion, string difficulty,
                           int chapterId, int nextLevelId, int firstClearCoins,
                           LevelSubject[] subjects, string[] categories, LevelItem[] items, LevelMove[] solution)
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
        Solution = solution ?? new LevelMove[0];
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

[Serializable]
public class LevelMove
{
    public string axis;        // "row" or "column"
    public int line;
    public int direction;      // +1 = right / down, -1 = left / up

    public bool IsRow => axis == "row";

    public bool Is(bool isRow, int index, int dir) => IsRow == isRow && line == index && direction == Math.Sign(dir);

    // "top row right", "middle column down" (3x3 names; other sizes use numbers).
    public string Describe(int size)
    {
        string dir = IsRow ? (direction > 0 ? "right" : "left") : (direction > 0 ? "down" : "up");
        return $"{Place(size)} {(IsRow ? "row" : "column")} {dir}";
    }

    // "top", "middle", "left"... for a 3x3 board.
    public string Place(int size)
    {
        string[] rows = { "top", "middle", "bottom" };
        string[] cols = { "left", "middle", "right" };
        return size == 3 ? (IsRow ? rows[line] : cols[line]) : $"#{line + 1}";
    }

    // Which item wraps, and where to: "The last item wraps to the first space."
    public string DescribeWrap()
    {
        string leaving = IsRow ? (direction > 0 ? "last" : "first") : (direction > 0 ? "bottom" : "top");
        string entering = IsRow ? (direction > 0 ? "first" : "last") : (direction > 0 ? "top" : "bottom");
        return $"The {leaving} item wraps to the {entering} space.";
    }
}
