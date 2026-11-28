using System;
using System.Collections.Generic;
using UnityEngine;

// Central state machine. Decides whether a swap is legal, counts moves, keeps the undo history,
// and ends the level. Moving on after a win or fail is the player's call (Manager_Flow's popups).
// Input is gated, never paused by a flag: it is accepted while Playing and no popup is up
// (ApplyInputGate re-runs on state changes and popup open / close).
public class Manager_Game : Singleton<Manager_Game>
{
    public event Action<GameState> OnStateChanged = delegate { };
    public event Action<int> OnLevelStarted = delegate { };
    public event Action OnMoveMade = delegate { };      // also raised by Undo: the board and MovesUsed changed
    public event Action<SwapMove> OnMoveCommitted = delegate { };    // a counted move (drag or ApplyMove)
    public event Action OnUndone = delegate { };

    public GameState State { get; private set; } = GameState.Loading;
    public int CurrentLevel { get; private set; } = 1;
    public LevelDefinition Level { get; private set; }
    public int MovesUsed { get; private set; }
    public int MovesLeft => Level != null && Level.HasMoveLimit ? Level.MoveLimit - MovesUsed : -1;   // -1 = unlimited

    // Undo works while playing and also after a win or fail ("Undo for one more move").
    public bool CanUndo => _history.Count > 0 && State != GameState.Loading;

    // Current press: the cell it started on, the direction it was dragged (one of dRow / dCol is
    // +-1, the other 0), the swap that picks, and whether the pair has been picked up.
    private bool _pressOnBoard;
    private int _pressRow;
    private int _pressCol;
    private int _dragRow;
    private int _dragCol;
    private SwapMove _pressMove;
    private bool _pairHeld;

    private readonly Stack<SwapMove> _history = new Stack<SwapMove>();

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

    // ---------- Drag to swap ----------
    // A press on a tile picks it up once the pointer has moved Tuning.DragStartThreshold; the
    // dominant direction (right, left, up or down) picks the neighbour it swaps with, locked for
    // the rest of the press. The pair then follows the pointer, up to a full swap. On release the
    // swap commits if it was dragged Tuning.DragCommitThreshold of a cell, or flicked; otherwise it
    // snaps back. A drag towards the board's edge (no neighbour) does nothing. Each swap is one move.

    private void HandlePress(Vector2 screenPosition)
    {
        var board = Manager_Board.Instance;
        _pairHeld = false;
        _pressOnBoard = State == GameState.Playing && !board.IsAnimating
                        && board.TryGetCell(screenPosition, out _pressRow, out _pressCol);
    }

    private void HandleDrag(Vector2 screenStart, Vector2 screenNow)
    {
        if (!_pressOnBoard) return;
        var board = Manager_Board.Instance;

        if (!_pairHeld)
        {
            float threshold = Tuning.DragStartThreshold * Mathf.Min(Screen.width, Screen.height);
            Vector2 delta = screenNow - screenStart;
            if (delta.magnitude < threshold) return;

            // Screen up = towards row 0.
            bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            _dragRow = horizontal ? 0 : (delta.y > 0f ? -1 : 1);
            _dragCol = horizontal ? (delta.x > 0f ? 1 : -1) : 0;
            _pressMove = SwapMove.Between(_pressRow, _pressCol, _dragRow, _dragCol, Level.Size);
            if (_pressMove == null)
            {
                _pressOnBoard = false;     // towards the edge: nothing to swap with
                return;
            }

            board.BeginDrag(_pressMove);
            _pairHeld = true;
        }

        board.SetDragOffset(DragOffset(screenStart, screenNow));
    }

    private void HandleRelease(Vector2 screenStart, Vector2 screenEnd, float secondsHeld)
    {
        if (!_pairHeld) return;      // a tap, or a press that never picked up a pair
        _pairHeld = false;
        _pressOnBoard = false;

        var board = Manager_Board.Instance;
        float offset = DragOffset(screenStart, screenEnd);
        bool commit = offset >= Tuning.DragCommitThreshold || (offset > 0f && secondsHeld <= Tuning.FlickMaxDuration);
        if (!commit)
        {
            board.CancelDrag();
            return;
        }

        board.CommitDrag();
        CountMove(_pressMove);
    }

    // A swap made from code (hints, the tester): same rules and counting as a drag.
    // Ignored unless Playing and the board is idle.
    public bool ApplyMove(SwapMove move)
    {
        var board = Manager_Board.Instance;
        if (State != GameState.Playing || board.IsAnimating || board.IsDragging || _pairHeld || move == null) return false;
        if (SwapMove.Between(move.row, move.col, move.OtherRow - move.row, move.OtherCol - move.col, Level.Size) == null) return false;

        board.Swap(move);
        CountMove(move);
        return true;
    }

    // Takes back the last swap (the same swap again), refunding its move. From Won or Failed this
    // returns to Playing. Refused while a pair is moving.
    public bool Undo()
    {
        var board = Manager_Board.Instance;
        if (!CanUndo || board.IsAnimating || board.IsDragging || _pairHeld) return false;

        board.Swap(_history.Pop(), celebrate: false);
        MovesUsed--;
        Manager_Audio.Instance?.PlaySound(SoundType.Undo);     // haptic comes from the slide's settle

        SetState(GameState.Playing);
        OnUndone();
        OnMoveMade();
        return true;
    }

    private void CountMove(SwapMove move)
    {
        _history.Push(move);
        MovesUsed++;
        OnMoveCommitted(move);
        OnMoveMade();
        EvaluateEndConditions();
    }

    private void HandleCancel()
    {
        if (_pairHeld) Manager_Board.Instance.CancelDrag();
        _pairHeld = false;
        _pressOnBoard = false;
    }

    // How far the pressed tile has been dragged towards its neighbour, in cells (0..1).
    private float DragOffset(Vector2 screenStart, Vector2 screenNow)
    {
        Vector2 cells = Manager_Board.Instance.ScreenDeltaToCells(screenStart, screenNow);
        float along = _dragCol != 0 ? cells.x * _dragCol : -cells.y * _dragRow;    // cells.y is up
        return Mathf.Clamp01(along);
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
