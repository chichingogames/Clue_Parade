using System;

// Append-only: these enums are saved / serialized by ordinal. Never renumber or reuse a value.
public enum GameState { Loading = 0, Playing = 1, Won = 2, Failed = 3 }

// One per popup (Manager_PopUp registry). Serialized on each popup by ordinal: append only.
// Lesson and Confirm are retired (no popup); LevelSelect is the case map.
public enum PopupName { Lesson = 0, Win = 1, Failed = 2, Hint = 3, Notebook = 4, LevelSelect = 5, Menu = 6, Confirm = 7, Feedback = 8 }

// Strength of a vibration pulse (Haptics.Play). Not serialized.
public enum HapticType { Light = 0, Medium = 1, Heavy = 2 }

// Stored by ordinal in FileRef's sound table. Audio file name = snake_case of the name
// (Undo -> undo.wav, or undo_01.wav, undo_02.wav... for variants), except TileMatch and
// LevelComplete, which are the content pack's match_1..3 and level_win (AssetBuilder).
// LineComplete is not played; it keeps its ordinal.
public enum SoundType { UiTap = 0, SwipeSettle = 1, TileMatch = 2, LineComplete = 3, LevelComplete = 4, OutOfMoves = 5, Restart = 6, Undo = 7 }

// A tile is known by the cell it belongs in when the board is solved: Id = row * size + col
// (its index in the case's goalCells). What it shows comes from LevelDefinition.ItemFor(tile).
[Serializable]
public struct TileData : IEquatable<TileData>
{
    public int Id;

    public TileData(int id) => Id = id;

    public int HomeRow(int size) => Id / size;
    public int HomeColumn(int size) => Id % size;

    public bool Equals(TileData other) => Id == other.Id;
    public override bool Equals(object obj) => obj is TileData other && Equals(other);
    public override int GetHashCode() => Id;
    public override string ToString() => $"#{Id}";
}

// Sorting orders on the Default layer (the handoff board: a cream panel the tiles sit on).
public static class SortOrder
{
    public const int Background = -10;  // teal backdrop
    public const int Board = 0;         // board panel (board_frame)
    public const int Divider = 1;       // grid lines between cells
    public const int ActiveRow = 2;     // highlight behind the selected category's row
    public const int TileFlash = 3;     // a newly matched tile's pale green flash
    public const int TileItem = 5;      // ItemTile item art
    public const int TileLabel = 6;     // ItemTile item name
    public const int TileCheck = 9;     // check badge on a matched cell
    public const int Guide = 20;        // first-swipe / hint guide band, arrow, hand
    public const int Particles = 25;    // match ring and stars
}
