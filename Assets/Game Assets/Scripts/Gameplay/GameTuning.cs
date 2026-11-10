using UnityEngine;

// Inspector overrides for Tuning. Lives on the Managers object; tweak live in Play mode.
[DisallowMultipleComponent, DefaultExecutionOrder(-1000), ExecuteAlways]
public class GameTuning : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private float _cellSize = Tuning.CellSizeDefault;
    [SerializeField, Range(0.5f, 1f)] private float _tileScale = Tuning.TileScaleDefault;
    [SerializeField] private float _shiftDuration = Tuning.ShiftDurationDefault;
    [SerializeField] private float _cameraMargin = Tuning.CameraMarginDefault;
    [SerializeField] private float _framePadding = Tuning.FramePaddingDefault;
    [SerializeField] private float _markerOffset = Tuning.MarkerOffsetDefault;

    [Header("Input")]
    [Tooltip("Fraction of the shorter screen side the pointer must move before the line starts following it.")]
    [SerializeField, Range(0.005f, 0.2f)] private float _dragStartThreshold = Tuning.DragStartThresholdDefault;
    [Tooltip("On release, the leftover part of a cell must be at least this fraction of a cell to count as one more step.")]
    [SerializeField, Range(0.05f, 0.95f)] private float _dragCommitThreshold = Tuning.DragCommitThresholdDefault;
    [Tooltip("A release within this many seconds moves at least one cell, even below the commit threshold. 0 disables flicks.")]
    [SerializeField, Min(0f)] private float _flickMaxDuration = Tuning.FlickMaxDurationDefault;

    [Header("Levels")]
    [SerializeField, Min(1)] private int _scrambleMovesPerSize = Tuning.ScrambleMovesPerSizeDefault;
    [SerializeField, Min(1)] private int _levelsPerSizeStep = Tuning.LevelsPerSizeStepDefault;
    [SerializeField, Range(2, 5)] private int _minBoardSize = Tuning.MinBoardSizeDefault;
    [SerializeField, Range(2, 5)] private int _maxBoardSize = Tuning.MaxBoardSizeDefault;

    [Header("Loading screen (on launch)")]
    [Tooltip("Seconds the loading bar takes to fill.")]
    [SerializeField, Min(0.01f)] private float _loadingDuration = Tuning.LoadingDurationDefault;
    [Tooltip("Loading screen fade-out. The level opens (board and HUD intros start) as it begins.")]
    [SerializeField, Min(0.01f)] private float _loadingFadeDuration = Tuning.LoadingFadeDurationDefault;

    [Header("Intro (after the loading screen): HUD, then board")]
    [Tooltip("Seconds before the first HUD element starts scaling up.")]
    [SerializeField, Min(0f)] private float _uiIntroDelay = Tuning.UiIntroDelayDefault;
    [Tooltip("Scale-up time of each HUD element (ease OutBack).")]
    [SerializeField, Min(0.01f)] private float _uiIntroDuration = Tuning.UiIntroDurationDefault;
    [Tooltip("Delay between one element starting and the next, top to bottom.")]
    [SerializeField, Min(0f)] private float _uiIntroStagger = Tuning.UiIntroStaggerDefault;

    [Tooltip("Seconds before the board starts scaling up. The default lands it between the top bar and the pills.")]
    [SerializeField, Min(0f)] private float _boardIntroDelay = Tuning.BoardIntroDelayDefault;
    [Tooltip("Board scale-up time (ease OutBack). Board input is ignored until it finishes.")]
    [SerializeField, Min(0.01f)] private float _boardIntroDuration = Tuning.BoardIntroDurationDefault;

    [Header("Tile spawn (every level)")]
    [Tooltip("Scale-up time of each tile when a level loads (ease OutBack).")]
    [SerializeField, Min(0.01f)] private float _tileSpawnDuration = Tuning.TileSpawnDurationDefault;
    [Tooltip("Extra delay per diagonal step (row + col) from the top-left tile.")]
    [SerializeField, Min(0f)] private float _tileSpawnStagger = Tuning.TileSpawnStaggerDefault;

    [Tooltip("Scale-up time of a matched check. On level load each check pops once its tile has spawned; in play, when a move matches it.")]
    [SerializeField, Min(0.01f)] private float _checkPopDuration = Tuning.CheckPopDurationDefault;

    [Header("Layout")]
    [Tooltip("Fraction of the safe-area height kept free above the board for the HUD (logo, level, pills, message panel). The camera fits the board into what is left.")]
    [SerializeField, Range(0f, 0.45f)] private float _hudTopReserve = Tuning.HudTopReserveDefault;
    [Tooltip("Fraction of the safe-area height kept free below the board (Next level, Undo, Restart).")]
    [SerializeField, Range(0f, 0.45f)] private float _hudBottomReserve = Tuning.HudBottomReserveDefault;

    [Header("HUD")]
    [Tooltip("Low-moves warning on the moves pill: seconds for one breath from white to red (the same again back).")]
    [SerializeField, Min(0.05f)] private float _movesWarningBreath = Tuning.MovesWarningBreathDefault;

    [Header("Tutorial (levels 1-4, Manager_Tutorial)")]
    [Tooltip("Seconds per loop of the hand that demonstrates the guided swipe.")]
    [SerializeField, Min(0.3f)] private float _tutorialHandLoop = Tuning.TutorialHandLoopDefault;
    [Tooltip("Guided levels: how long the \"matched!\" message shows after the board settles, before the win card.")]
    [SerializeField, Min(0f)] private float _tutorialConfirmation = Tuning.TutorialConfirmationDefault;
    [Tooltip("Seconds without a move before the target icons pulse as a hint.")]
    [SerializeField, Min(1f)] private float _tutorialIdleHint = Tuning.TutorialIdleHintDefault;
    [Tooltip("Length of the moves pill pulse on the first spent move.")]
    [SerializeField, Min(0.05f)] private float _tutorialMovesPulse = Tuning.TutorialMovesPulseDefault;

    public float CellSize => _cellSize;
    public float TileScale => _tileScale;
    public float ShiftDuration => _shiftDuration;
    public float CameraMargin => _cameraMargin;
    public float FramePadding => _framePadding;
    public float MarkerOffset => _markerOffset;
    public float DragStartThreshold => _dragStartThreshold;
    public float DragCommitThreshold => _dragCommitThreshold;
    public float FlickMaxDuration => _flickMaxDuration;
    public int ScrambleMovesPerSize => _scrambleMovesPerSize;
    public int LevelsPerSizeStep => _levelsPerSizeStep;
    public int MinBoardSize => _minBoardSize;
    public int MaxBoardSize => _maxBoardSize;
    public float UiIntroDelay => _uiIntroDelay;
    public float UiIntroDuration => _uiIntroDuration;
    public float UiIntroStagger => _uiIntroStagger;
    public float BoardIntroDelay => _boardIntroDelay;
    public float BoardIntroDuration => _boardIntroDuration;
    public float TileSpawnDuration => _tileSpawnDuration;
    public float TileSpawnStagger => _tileSpawnStagger;
    public float CheckPopDuration => _checkPopDuration;
    public float LoadingDuration => _loadingDuration;
    public float LoadingFadeDuration => _loadingFadeDuration;
    public float HudTopReserve => _hudTopReserve;
    public float HudBottomReserve => _hudBottomReserve;
    public float MovesWarningBreath => _movesWarningBreath;
    public float TutorialHandLoop => _tutorialHandLoop;
    public float TutorialConfirmation => _tutorialConfirmation;
    public float TutorialIdleHint => _tutorialIdleHint;
    public float TutorialMovesPulse => _tutorialMovesPulse;

    public static GameTuning Active { get; private set; }

    private void OnEnable() => Active = this;

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }
}
