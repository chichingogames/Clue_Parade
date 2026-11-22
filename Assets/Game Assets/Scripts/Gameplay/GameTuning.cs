using UnityEngine;

// Inspector overrides for Tuning. Lives on the Managers object; tweak live in Play mode.
[DisallowMultipleComponent, DefaultExecutionOrder(-1000), ExecuteAlways]
public class GameTuning : MonoBehaviour
{
    [Header("Board")]
    [SerializeField] private float _shiftDuration = Tuning.ShiftDurationDefault;

    [Header("Input")]
    [Tooltip("Fraction of the shorter screen side the pointer must move before the line starts following it.")]
    [SerializeField, Range(0.005f, 0.2f)] private float _dragStartThreshold = Tuning.DragStartThresholdDefault;
    [Tooltip("On release, the leftover part of a cell must be at least this fraction of a cell to count as one more step.")]
    [SerializeField, Range(0.05f, 0.95f)] private float _dragCommitThreshold = Tuning.DragCommitThresholdDefault;
    [Tooltip("A release within this many seconds moves at least one cell, even below the commit threshold. 0 disables flicks.")]
    [SerializeField, Min(0f)] private float _flickMaxDuration = Tuning.FlickMaxDurationDefault;


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


    [Header("HUD")]
    [Tooltip("Low-moves warning on the moves pill: seconds for one breath from white to red (the same again back).")]
    [SerializeField, Min(0.05f)] private float _movesWarningBreath = Tuning.MovesWarningBreathDefault;


    public float ShiftDuration => _shiftDuration;
    public float DragStartThreshold => _dragStartThreshold;
    public float DragCommitThreshold => _dragCommitThreshold;
    public float FlickMaxDuration => _flickMaxDuration;
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
    public float MovesWarningBreath => _movesWarningBreath;

    public static GameTuning Active { get; private set; }

    private void OnEnable() => Active = this;

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }
}
