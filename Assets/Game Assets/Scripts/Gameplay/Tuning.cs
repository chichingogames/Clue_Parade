// Gameplay numbers: const defaults + read facade over the scene's GameTuning overrides.
// Code always reads Tuning.X. The GameTuning values in the scene are what ship.
public static class Tuning
{
    public const float ShiftDurationDefault = 0.15f;
    public const float DragStartThresholdDefault = 0.02f; // fraction of the shorter screen side before a line starts following
    public const float DragCommitThresholdDefault = 0.5f; // fraction of a cell the last partial step needs to count
    public const float FlickMaxDurationDefault = 0.25f;   // a release this quick moves at least one cell
    public const float UiIntroDelayDefault = 0.1f;        // seconds before the first HUD element pops in on launch
    public const float UiIntroDurationDefault = 0.35f;    // scale-up time per element
    public const float UiIntroStaggerDefault = 0.06f;     // gap between one element starting and the next
    public const float BoardIntroDelayDefault = 0.22f;    // seconds before the board starts scaling up on launch
    public const float BoardIntroDurationDefault = 0.45f; // board scale-up time
    public const float TileSpawnDurationDefault = 0.28f;  // each tile's scale-up when a level loads
    public const float TileSpawnStaggerDefault = 0.045f;  // extra delay per diagonal step (row + col) from the top-left
    public const float CheckPopDurationDefault = 0.22f;   // a matched check's scale-up (after its tile spawns, or when a move matches it)
    public const float EquipPopDurationDefault = 0.4f;    // a customer's pop when a move puts an item on them (handoff: equipmentPopSeconds)
    public const float EquipPopScaleDefault = 1.06f;      // peak scale of that pop
    public const float WinPopupDelayDefault = 0.38f;      // seconds from the winning move to the win popup (handoff: winDelaySeconds)
    public const float HintAttentionDelayDefault = 16f;   // idle seconds after a level opens before the Hint button pulses
    public const int FirstClearCoinsDefault = 30;         // reward for a level's first clear (replays: 0)
    public const float LoadingDurationDefault = 1.6f;     // loading bar fill time
    public const float LoadingFadeDurationDefault = 0.4f; // loading screen fade-out, while the board and HUD intros start

    private static GameTuning Source => GameTuning.Active;

    public static float ShiftDuration => Source != null ? Source.ShiftDuration : ShiftDurationDefault;
    public static float DragStartThreshold => Source != null ? Source.DragStartThreshold : DragStartThresholdDefault;
    public static float DragCommitThreshold => Source != null ? Source.DragCommitThreshold : DragCommitThresholdDefault;
    public static float FlickMaxDuration => Source != null ? Source.FlickMaxDuration : FlickMaxDurationDefault;
    public static float UiIntroDelay => Source != null ? Source.UiIntroDelay : UiIntroDelayDefault;
    public static float UiIntroDuration => Source != null ? Source.UiIntroDuration : UiIntroDurationDefault;
    public static float UiIntroStagger => Source != null ? Source.UiIntroStagger : UiIntroStaggerDefault;
    public static float BoardIntroDelay => Source != null ? Source.BoardIntroDelay : BoardIntroDelayDefault;
    public static float BoardIntroDuration => Source != null ? Source.BoardIntroDuration : BoardIntroDurationDefault;
    public static float TileSpawnDuration => Source != null ? Source.TileSpawnDuration : TileSpawnDurationDefault;
    public static float TileSpawnStagger => Source != null ? Source.TileSpawnStagger : TileSpawnStaggerDefault;
    public static float CheckPopDuration => Source != null ? Source.CheckPopDuration : CheckPopDurationDefault;
    public static float EquipPopDuration => Source != null ? Source.EquipPopDuration : EquipPopDurationDefault;
    public static float EquipPopScale => Source != null ? Source.EquipPopScale : EquipPopScaleDefault;
    public static float WinPopupDelay => Source != null ? Source.WinPopupDelay : WinPopupDelayDefault;
    public static float HintAttentionDelay => Source != null ? Source.HintAttentionDelay : HintAttentionDelayDefault;
    public static int FirstClearCoins => Source != null ? Source.FirstClearCoins : FirstClearCoinsDefault;
    public static float LoadingDuration => Source != null ? Source.LoadingDuration : LoadingDurationDefault;
    public static float LoadingFadeDuration => Source != null ? Source.LoadingFadeDuration : LoadingFadeDurationDefault;
}
