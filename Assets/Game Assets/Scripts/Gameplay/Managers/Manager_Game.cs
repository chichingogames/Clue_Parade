using System;
using System.Collections.Generic;
using UnityEngine;

// Central state machine. Decides whether a swipe is legal, counts moves, keeps the undo history,
// and ends the level. Moving on after a win or fail is the player's call (Manager_Flow's popups).
// Input is gated, never paused by a flag: it is accepted while Playing and no popup is up
// (ApplyInputGate re-runs on state changes and popup open / close).
public class Manager_Game : Singleton<Manager_Game>
{
    public event Action<GameState> OnStateChanged = delegate { };
    public event Action<int> OnLevelStarted = delegate { };
    public event Action OnMoveMade = delegate { };      // also raised by Undo: the board and MovesUsed changed
    public event Action<LevelMove> OnMoveCommitted = delegate { };   // a counted move (swipe or ApplyMove)
    public event Action<LevelMove> OnMoveRejected = delegate { };    // a swipe refused for the required first move (its argument)
    public event Action OnUndone = delegate { };

    public GameState State { get; private set; } = GameState.Loading;
    public int CurrentLevel { get; private set; } = 1;
    public LevelDefinition Level { get; private set; }
    public int MovesUsed { get; private set; }
    public int MovesLeft => Level != null && Level.HasMoveLimit ? Level.MoveLimit - MovesUsed : -1;   // -1 = unlimited

    // Undo works while playing and also after a win or fail ("Undo for one more move").
    public bool CanUndo => _history.Count > 0 && State != GameState.Loading;

    // The level's guided first swipe while it still applies: no move made and the board is the
    // start board (so Undo back to the start brings it back). Null otherwise.
    public LevelMove RequiredMove =>
        State == GameState.Playing && Level?.RequiredFirstMove != null && MovesUsed == 0
        && Manager_Board.Instance.State.SameCells(Level.StartCells) ? Level.RequiredFirstMove : null;

    // Current press: the cell it started on, and whether a line has been picked up.
    private bool _pressOnBoard;
    private int _pressRow;
    private int _pressCol;
    private bool _lineHeld;
    private bool _lineIsRow;

    // Cells a line can move in one swipe. Each swipe is one move.
    private const int MaxStepsPerMove = 1;

    private readonly Stack<Move> _history = new Stack<Move>();

    private struct Move
    {
        public bool IsRow;
        public int Index;
        public int Steps;
    }

    public override void Awake2()
    {
        var input = Manager_Input.Instance;
        input.OnPress += HandlePress;
        input.OnDrag += HandleDrag;
        input.OnRelease += HandleRelease;
        input.OnCancel += HandleCancel;

        var popUps = Manager_PopUp.Instance;
        if (popUps != null)
        {
            popUps.OnPopupShown += _ => ApplyInputGate();
            popUps.OnPopupCloseFinished += ApplyInputGate;
        }
    }

    // Paused = a popup is open or still closing. Derived, so it cannot get stuck.
    public bool IsPaused => Manager_PopUp.Instance != null && Manager_PopUp.Instance.IsPopUpActive;

    private void ApplyInputGate() => Manager_Input.Instance.AcceptInput = State == GameState.Playing && !IsPaused;

    protected override void OnDestroy()
    {
        var input = Manager_Input.Instance;
        if (input != null)
        {
            input.OnPress -= HandlePress;
            input.OnDrag -= HandleDrag;
            input.OnRelease -= HandleRelease;
            input.OnCancel -= HandleCancel;
        }
        base.OnDestroy();
    }

    // Launch: the first level opens once, on the saved level (OfflineData.CurrentLevel). Right away,
    // unless the loading screen (UI_Loading) held it in Awake; it then calls Launch() as it fades out.
    public bool HasLaunched { get; private set; }
    private bool _launchHeld;

    public void HoldLaunch() => _launchHeld = true;

    public void Launch()
    {
        if (HasLaunched) return;
        HasLaunched = true;
        // The Tester may have loaded one already. Saves from looping builds can be past the last level.
        if (Level == null) StartLevel(Mathf.Clamp(OfflineData.Instance.CurrentLevel, 1, Mathf.Max(1, Manager_Level.Instance.LevelCount)));
    }

    private void Start()
    {
        if (!_launchHeld) Launch();
    }

    public void StartLevel(int levelNumber)
    {
        SetState(GameState.Loading);
        _history.Clear();

        CurrentLevel = levelNumber;
        var level = Manager_Level.Instance.Load(levelNumber);
        if (level == null)
        {
            // CaseLevelSource has logged why; the previous level (if any) stays on screen, input off.
            Debug.LogError($"[Game] Level {levelNumber} could not be loaded.");
            return;
        }
        Level = level;
        Manager_Board.Instance.Build(Level);
        MovesUsed = 0;

        SetState(GameState.Playing);
        OnLevelStarted(levelNumber);
        Debug.Log($"[Game] Level {levelNumber} started (\"{Level.Title}\", {Level.Difficulty}, " +
                  $"{(Level.HasMoveLimit ? $"{Level.MoveLimit} moves" : "no move limit")}, minimum {Level.MinimumMoves}).");
    }

    public void RestartLevel()
    {
        Manager_Audio.Instance?.PlaySound(SoundType.Restart);
        Haptics.Play(HapticType.Light);
        StartLevel(CurrentLevel);
    }

    public void AdvanceLevel() => StartLevel(CurrentLevel + 1);

    // ---------- Hold-and-drag / swipe ----------
    // A press on the board picks up its row or column once the pointer has moved
    // Tuning.DragStartThreshold along one axis; the line then follows the pointer.
    // A move is exactly one cell: the line follows the pointer but no further than MaxStepsPerMove
    // cells, and on release snaps to 0 or 1 cell (see StepsForRelease).

    private void HandlePress(Vector2 screenPosition)
    {
        var board = Manager_Board.Instance;
        _lineHeld = false;
        _pressOnBoard = State == GameState.Playing && !board.IsAnimating
                        && board.TryGetCell(screenPosition, out _pressRow, out _pressCol);
    }

    private void HandleDrag(Vector2 screenStart, Vector2 screenNow)
    {
        if (!_pressOnBoard) return;
        var board = Manager_Board.Instance;

        if (!_lineHeld)
        {
            float threshold = Tuning.DragStartThreshold * Mathf.Min(Screen.width, Screen.height);
            Vector2 delta = screenNow - screenStart;
            if (delta.magnitude < threshold) return;

            // Lock to the dominant axis for the rest of this press.
            _lineIsRow = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);

            board.BeginDrag(_lineIsRow, _lineIsRow ? _pressRow : _pressCol);
            _lineHeld = true;
        }

        board.SetDragOffset(DragOffset(screenStart, screenNow));
    }

    private void HandleRelease(Vector2 screenStart, Vector2 screenEnd, float secondsHeld)
    {
        if (!_lineHeld) return;      // a tap, or a press that never picked up a line
        _lineHeld = false;
        _pressOnBoard = false;

        var board = Manager_Board.Instance;
        int steps = StepsForRelease(DragOffset(screenStart, screenEnd), secondsHeld);
        int index = _lineIsRow ? _pressRow : _pressCol;
        if (steps == 0)
        {
            board.CancelDrag();
            return;
        }

        // A guided first swipe: anything else snaps back and costs nothing.
        var required = RequiredMove;
        if (required != null && !required.Is(_lineIsRow, index, steps))
        {
            board.CancelDrag();
            OnMoveRejected(required);
            return;
        }

        board.CommitDrag(steps);
        CountMove(_lineIsRow, index, steps);
    }

    // A move made from code (tutorials, hints, the tester): same rules and counting as a swipe.
    // steps: only the sign is used (row + = right, column + = down): a move is one cell.
    // Ignored unless Playing and the board is idle.
    public bool ApplyMove(bool isRow, int index, int steps)
    {
        var board = Manager_Board.Instance;
        if (State != GameState.Playing || board.IsAnimating || board.IsDragging || _lineHeld || steps == 0) return false;
        steps = Mathf.Clamp(steps, -MaxStepsPerMove, MaxStepsPerMove);

        if (isRow) board.ShiftRow(index, steps);
        else board.ShiftColumn(index, steps);
        CountMove(isRow, index, steps);
        return true;
    }

    // Takes back the last swipe, refunding its move. From Won or Failed this
    // returns to Playing. Refused while a line is moving.
    public bool Undo()
    {
        var board = Manager_Board.Instance;
        if (!CanUndo || board.IsAnimating || board.IsDragging || _lineHeld) return false;

        var move = _history.Pop();
        if (move.IsRow) board.ShiftRow(move.Index, -move.Steps, celebrate: false);
        else board.ShiftColumn(move.Index, -move.Steps, celebrate: false);
        MovesUsed -= Mathf.Abs(move.Steps);
        Manager_Audio.Instance?.PlaySound(SoundType.Undo);     // haptic comes from the slide's settle

        SetState(GameState.Playing);
        OnUndone();
        OnMoveMade();
        return true;
    }

    private void CountMove(bool isRow, int index, int steps)
    {
        _history.Push(new Move { IsRow = isRow, Index = index, Steps = steps });
        MovesUsed += Mathf.Abs(steps);
        OnMoveCommitted(new LevelMove { axis = isRow ? "row" : "column", line = index, direction = Math.Sign(steps) });
        OnMoveMade();
        EvaluateEndConditions();
    }

    private void HandleCancel()
    {
        if (_lineHeld) Manager_Board.Instance.CancelDrag();
        _lineHeld = false;
        _pressOnBoard = false;
    }

    // Line offset in cells: row + = right, column + = down (screen up = towards row 0).
    // Clamped to one move's reach, so the line stops following past one cell.
    private float DragOffset(Vector2 screenStart, Vector2 screenNow)
    {
        Vector2 cells = Manager_Board.Instance.ScreenDeltaToCells(screenStart, screenNow);
        return Mathf.Clamp(_lineIsRow ? cells.x : -cells.y, -MaxStepsPerMove, MaxStepsPerMove);
    }

    // Every whole cell dragged is a step; the leftover part counts as one more step once it
    // reaches Tuning.DragCommitThreshold of a cell. A quick flick moves at least one cell.
    // Never more than MaxStepsPerMove (the offset is already clamped; this is the guard).
    private static int StepsForRelease(float offset, float secondsHeld)
    {
        float distance = Mathf.Abs(offset);
        int whole = Mathf.FloorToInt(distance);
        int steps = whole + (distance - whole >= Tuning.DragCommitThreshold ? 1 : 0);

        if (steps == 0 && secondsHeld <= Tuning.FlickMaxDuration) steps = 1;
        steps = Mathf.Min(steps, MaxStepsPerMove);
        return steps * (offset < 0f ? -1 : 1);
    }

    private void EvaluateEndConditions()
    {
        if (Manager_Board.Instance.State.IsSolved) SetState(GameState.Won);
        else if (Level.HasMoveLimit && MovesUsed >= Level.MoveLimit) SetState(GameState.Failed);
    }

#if UNITY_EDITOR
    // Tester only: end the level now, as if it had been won or lost.
    public void DebugEndLevel(bool won)
    {
        if (State == GameState.Playing) SetState(won ? GameState.Won : GameState.Failed);
    }
#endif

    // Saved on the win itself, so quitting before pressing Next level keeps the progress.
    // Replaying an older level never moves CurrentLevel backwards.
    private void SaveProgress()
    {
        var data = OfflineData.Instance;
        data.PassedLevels = Mathf.Max(data.PassedLevels, CurrentLevel);
        data.CurrentLevel = Mathf.Min(Mathf.Max(data.CurrentLevel, CurrentLevel + 1), Mathf.Max(1, Manager_Level.Instance.LevelCount));
        data.Save();
    }

    private void SetState(GameState state)
    {
        if (State == state) return;
        State = state;
        ApplyInputGate();
        OnStateChanged(state);

        if (state == GameState.Won)
        {
            Debug.Log($"[Game] Level {CurrentLevel} complete in {MovesUsed} moves.");
            SaveProgress();
        }
        else if (state == GameState.Failed)
        {
            Debug.Log($"[Game] Level {CurrentLevel} failed after {MovesUsed} moves.");
            Manager_Audio.Instance?.PlaySound(SoundType.OutOfMoves);
            Haptics.Play(HapticType.Heavy);
        }
    }
}
