using System;
using System.Collections;
using UnityEngine;

// Central state machine. Decides whether a swipe is legal, counts moves, and ends the level.
public class Manager_Game : Singleton<Manager_Game>
{
    public event Action<GameState> OnStateChanged = delegate { };
    public event Action<int> OnLevelStarted = delegate { };
    public event Action OnMoveMade = delegate { };

    public GameState State { get; private set; } = GameState.Loading;
    public int CurrentLevel { get; private set; } = 1;
    public LevelDefinition Level { get; private set; }
    public int MovesUsed { get; private set; }
    public int MovesLeft => Level != null && Level.HasMoveLimit ? Level.MoveLimit - MovesUsed : -1;   // -1 = unlimited

    // Current press: the cell it started on, and whether a line has been picked up.
    private bool _pressOnBoard;
    private int _pressRow;
    private int _pressCol;
    private bool _lineHeld;
    private bool _lineIsRow;

    public override void Awake2()
    {
        var input = Manager_Input.Instance;
        input.OnPress += HandlePress;
        input.OnDrag += HandleDrag;
        input.OnRelease += HandleRelease;
        input.OnCancel += HandleCancel;
    }

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

    private void Start() => StartLevel(CurrentLevel);

    public void StartLevel(int levelNumber)
    {
        StopAllCoroutines();
        SetState(GameState.Loading);

        CurrentLevel = levelNumber;
        Level = Manager_Level.Instance.Load(levelNumber);
        Manager_Board.Instance.Build(Level);
        MovesUsed = 0;

        SetState(GameState.Playing);
        OnLevelStarted(levelNumber);
        Debug.Log($"[Game] Level {levelNumber} started ({Level.Size}x{Level.Size}, " +
                  $"{Manager_Board.Instance.State.MatchedCount}/{Level.Size * Level.Size} matched).");
    }

    public void RestartLevel() => StartLevel(CurrentLevel);

    public void AdvanceLevel() => StartLevel(CurrentLevel + 1);

    // ---------- Hold-and-drag / swipe ----------
    // A press on the board picks up its row or column once the pointer has moved
    // Tuning.DragStartThreshold along one axis; the line then follows the pointer.
    // On release the line snaps to whole cells (see StepsForRelease), each step is one move.

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
        if (steps == 0)
        {
            board.CancelDrag();
            return;
        }

        board.CommitDrag(steps);
        MovesUsed += Mathf.Abs(steps);
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
    private float DragOffset(Vector2 screenStart, Vector2 screenNow)
    {
        Vector2 cells = Manager_Board.Instance.ScreenDeltaToCells(screenStart, screenNow);
        return _lineIsRow ? cells.x : -cells.y;
    }

    // Every whole cell dragged is a step; the leftover part counts as one more step once it
    // reaches Tuning.DragCommitThreshold of a cell. A quick flick moves at least one cell.
    private static int StepsForRelease(float offset, float secondsHeld)
    {
        float distance = Mathf.Abs(offset);
        int whole = Mathf.FloorToInt(distance);
        int steps = whole + (distance - whole >= Tuning.DragCommitThreshold ? 1 : 0);

        if (steps == 0 && secondsHeld <= Tuning.FlickMaxDuration) steps = 1;
        return steps * (offset < 0f ? -1 : 1);
    }

    private void EvaluateEndConditions()
    {
        if (Manager_Board.Instance.State.IsSolved) SetState(GameState.Won);
        else if (Level.HasMoveLimit && MovesUsed >= Level.MoveLimit) SetState(GameState.Failed);
    }

    private void SetState(GameState state)
    {
        if (State == state) return;
        State = state;
        Manager_Input.Instance.AcceptInput = State == GameState.Playing;
        OnStateChanged(state);

        // No UI yet: move on automatically.
        if (state == GameState.Won)
        {
            Debug.Log($"[Game] Level {CurrentLevel} complete in {MovesUsed} moves.");
            StartCoroutine(AfterDelay(Tuning.WinAdvanceDelay, AdvanceLevel));
        }
        else if (state == GameState.Failed)
        {
            Debug.Log($"[Game] Level {CurrentLevel} failed: out of moves.");
            StartCoroutine(AfterDelay(Tuning.FailRestartDelay, RestartLevel));
        }
    }

    private static IEnumerator AfterDelay(float seconds, Action action)
    {
        yield return new WaitForSeconds(seconds);
        action();
    }
}
