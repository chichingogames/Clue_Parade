// Append-only: these enums are saved / serialized by ordinal. Never renumber or reuse a value.
public enum GameState { Loading = 0, Playing = 1, Won = 2, Failed = 3 }

// One per popup (Manager_PopUp registry). Serialized on each popup by ordinal: append only.
// Lesson, Confirm and Feedback are retired (no popup); LevelSelect is the case map (PopUp_Cases);
// Deduction is the milestone cases' final question (PopUp_Deduction).
public enum PopupName { Lesson = 0, Win = 1, Failed = 2, Hint = 3, Notebook = 4, LevelSelect = 5, Menu = 6, Confirm = 7, Feedback = 8, Deduction = 9 }

// Strength of a vibration pulse (Haptics.Play). Not serialized.
public enum HapticType { Light = 0, Medium = 1, Heavy = 2 }

// Stored by ordinal in FileRef's sound table. Audio file name = snake_case of the name
// (Undo -> undo.wav, or undo_01.wav, undo_02.wav... for variants), except TileMatch and
// LevelComplete, which are the content pack's match_1..3 and level_win (AssetBuilder).
// LineComplete is not played; it keeps its ordinal.
public enum SoundType { UiTap = 0, SwipeSettle = 1, TileMatch = 2, LineComplete = 3, LevelComplete = 4, OutOfMoves = 5, Restart = 6, Undo = 7 }

// Sorting orders on the Default layer (the handoff board: a cream panel the tiles sit on).
public static class SortOrder
{
    public const int Background = -10;  // teal backdrop
    public const int Board = 0;         // board panel (board_frame)
    public const int Divider = 1;       // grid lines between cells
    public const int ActiveRow = 2;     // row zones (a row two thirds / fully done, the hinted row)
    public const int TileFlash = 3;     // a tile's tint (aligned, focus, hint, selected) and flashes
    public const int TileItem = 5;      // ItemTile item art
    public const int TileCheck = 9;     // check badge on a cell (unused by the any-row rules)
    public const int Guide = 20;        // guided / hinted swap: band, arrow, hand
    public const int Particles = 25;    // delivery ring and stars
}
