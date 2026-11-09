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

    [Header("Flow")]
    [SerializeField] private float _winAdvanceDelay = Tuning.WinAdvanceDelayDefault;
    [SerializeField] private float _failRestartDelay = Tuning.FailRestartDelayDefault;

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
    public float WinAdvanceDelay => _winAdvanceDelay;
    public float FailRestartDelay => _failRestartDelay;

    public static GameTuning Active { get; private set; }

    private void OnEnable() => Active = this;

    private void OnDisable()
    {
        if (Active == this) Active = null;
    }
}
