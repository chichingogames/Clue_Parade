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
    public const float UiIntroDelayDefault = 0.1f;        // seconds before the first HUD element pops in on launch
    public const float UiIntroDurationDefault = 0.35f;    // scale-up time per element
    public const float UiIntroStaggerDefault = 0.06f;     // gap between one element starting and the next
    public const float BoardIntroDelayDefault = 0.22f;    // seconds before the board starts scaling up on launch
    public const float BoardIntroDurationDefault = 0.45f; // board scale-up time
    public const float TileSpawnDurationDefault = 0.28f;  // each tile's scale-up when a level loads
    public const float TileSpawnStaggerDefault = 0.045f;  // extra delay per diagonal step (row + col) from the top-left
    public const float CheckPopDurationDefault = 0.22f;   // a matched check's scale-up (after its tile spawns, or when a move matches it)
    public const float LoadingDurationDefault = 1.6f;     // loading bar fill time
    public const float LoadingFadeDurationDefault = 0.4f; // loading screen fade-out, while the board and HUD intros start
    public const float HudTopReserveDefault = 0.33f;      // fraction of the safe area kept free above the board (logo, level, pills, message panel)
    public const float HudBottomReserveDefault = 0.18f;   // fraction kept free below it (Next level, Undo, Restart)
    public const float MovesWarningBreathDefault = 0.6f;  // low-moves pill: seconds from white to red (and the same back)
    public const float TutorialHandLoopDefault = 1.7f;    // one loop of the guided-swipe hand cue (levels 1-2)
    public const float TutorialConfirmationDefault = 0.45f; // guided levels: the "matched!" message before the win card
    public const float TutorialIdleHintDefault = 8f;      // idle seconds before the target icons pulse (lesson with IdleHint)
    public const float TutorialMovesPulseDefault = 0.55f; // moves pill pulse on the first spent move (lesson with PulseMoves)

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
    public static float UiIntroDelay => Source != null ? Source.UiIntroDelay : UiIntroDelayDefault;
    public static float UiIntroDuration => Source != null ? Source.UiIntroDuration : UiIntroDurationDefault;
    public static float UiIntroStagger => Source != null ? Source.UiIntroStagger : UiIntroStaggerDefault;
    public static float BoardIntroDelay => Source != null ? Source.BoardIntroDelay : BoardIntroDelayDefault;
    public static float BoardIntroDuration => Source != null ? Source.BoardIntroDuration : BoardIntroDurationDefault;
    public static float TileSpawnDuration => Source != null ? Source.TileSpawnDuration : TileSpawnDurationDefault;
    public static float TileSpawnStagger => Source != null ? Source.TileSpawnStagger : TileSpawnStaggerDefault;
    public static float CheckPopDuration => Source != null ? Source.CheckPopDuration : CheckPopDurationDefault;
    public static float LoadingDuration => Source != null ? Source.LoadingDuration : LoadingDurationDefault;
    public static float LoadingFadeDuration => Source != null ? Source.LoadingFadeDuration : LoadingFadeDurationDefault;
    public static float HudTopReserve => Source != null ? Source.HudTopReserve : HudTopReserveDefault;
    public static float HudBottomReserve => Source != null ? Source.HudBottomReserve : HudBottomReserveDefault;
    public static float MovesWarningBreath => Source != null ? Source.MovesWarningBreath : MovesWarningBreathDefault;
    public static float TutorialHandLoop => Source != null ? Source.TutorialHandLoop : TutorialHandLoopDefault;
    public static float TutorialConfirmation => Source != null ? Source.TutorialConfirmation : TutorialConfirmationDefault;
    public static float TutorialIdleHint => Source != null ? Source.TutorialIdleHint : TutorialIdleHintDefault;
    public static float TutorialMovesPulse => Source != null ? Source.TutorialMovesPulse : TutorialMovesPulseDefault;
}
