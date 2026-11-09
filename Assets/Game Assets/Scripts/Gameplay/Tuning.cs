// Gameplay numbers: const defaults + read facade over the scene's GameTuning overrides.
// Code always reads Tuning.X. The GameTuning values in the scene are what ship.
public static class Tuning
{
    public const float CellSizeDefault = 1f;
    public const float TileScaleDefault = 0.95f;
    public const float ShiftDurationDefault = 0.15f;
    public const float DragStartThresholdDefault = 0.02f; // fraction of the shorter screen side before a line starts following
    public const float DragCommitThresholdDefault = 0.5f; // fraction of a cell the last partial step needs to count
    public const float FlickMaxDurationDefault = 0.25f;   // a release this quick moves at least one cell
    public const float CameraMarginDefault = 1.1f;
    public const float FramePaddingDefault = 0.15f;       // board frame border, in cells
    public const float MarkerOffsetDefault = 0.65f;       // target distance from the board edge, in cells
    public const int ScrambleMovesPerSizeDefault = 2;     // scramble swipes = board size * this
    public const int LevelsPerSizeStepDefault = 5;        // board grows by 1 every N levels
    public const int MinBoardSizeDefault = 3;
    public const int MaxBoardSizeDefault = 5;             // limited by the number of shapes/colors
    public const float WinAdvanceDelayDefault = 1f;
    public const float FailRestartDelayDefault = 1f;

    private static GameTuning Source => GameTuning.Active;

    public static float CellSize => Source != null ? Source.CellSize : CellSizeDefault;
    public static float TileScale => Source != null ? Source.TileScale : TileScaleDefault;
    public static float ShiftDuration => Source != null ? Source.ShiftDuration : ShiftDurationDefault;
    public static float DragStartThreshold => Source != null ? Source.DragStartThreshold : DragStartThresholdDefault;
    public static float DragCommitThreshold => Source != null ? Source.DragCommitThreshold : DragCommitThresholdDefault;
    public static float FlickMaxDuration => Source != null ? Source.FlickMaxDuration : FlickMaxDurationDefault;
    public static float CameraMargin => Source != null ? Source.CameraMargin : CameraMarginDefault;
    public static float FramePadding => Source != null ? Source.FramePadding : FramePaddingDefault;
    public static float MarkerOffset => Source != null ? Source.MarkerOffset : MarkerOffsetDefault;
    public static int ScrambleMovesPerSize => Source != null ? Source.ScrambleMovesPerSize : ScrambleMovesPerSizeDefault;
    public static int LevelsPerSizeStep => Source != null ? Source.LevelsPerSizeStep : LevelsPerSizeStepDefault;
    public static int MinBoardSize => Source != null ? Source.MinBoardSize : MinBoardSizeDefault;
    public static int MaxBoardSize => Source != null ? Source.MaxBoardSize : MaxBoardSizeDefault;
    public static float WinAdvanceDelay => Source != null ? Source.WinAdvanceDelay : WinAdvanceDelayDefault;
    public static float FailRestartDelay => Source != null ? Source.FailRestartDelay : FailRestartDelayDefault;
}
