using System;
using System.Collections.Generic;
using UnityEngine;

// Central state machine for the any-row cases. Decides whether a swap is legal (DeliveryRules),
// keeps the model, the request in focus and the undo history, and ends the case. Moving on after a
// win or fail is the player's call (Manager_Flow's popups). Input is gated, never paused by a flag:
// it is accepted while Playing and no popup is up (ApplyInputGate re-runs on state changes and
// popup open / close).
//
// Controls (the playtest's): drag a tile towards a neighbour (it follows the pointer and commits at
// Tuning.DragCommitThreshold of a cell, or on a flick), or tap a tile and then a neighbour. Swapping
// with an empty cell is allowed; two empty cells are not. Edges do not wrap.
public class Manager_Game : Singleton<Manager_Game>
{
    public enum Rejection { Edge, NotNeighbour, Tutorial }

    public event Action<GameState> OnStateChanged = delegate { };
    public event Action<int> OnLevelStarted = delegate { };
    public event Action OnViewChanged = delegate { };              // View, the focus or the selection changed: refresh the HUD
    public event Action<int, int> OnSwapCommitted = delegate { };   // a counted swap (cells), before it animates
    public event Action OnMoveResolved = delegate { };             // the swap and its deliveries have finished playing
    public event Action<Rejection> OnSwapRejected = delegate { };
    public event Action OnUndone = delegate { };

    public GameState State { get; private set; } = GameState.Loading;
    public int CurrentLevel { get; private set; } = 1;
    public DeliveryCase Level { get; private set; }
    // The rules state after the last move (the board settles into it when the move has played).
    public DeliveryState Model { get; private set; }
    // The state on screen: the model, or a step of the move being played.
    public DeliveryState View { get; private set; }
    public int MovesUsed => Model?.Moves ?? 0;
    public int MovesLeft => Level != null && Level.HasMoveLimit ? Level.moveLimit - MovesUsed : -1;   // -1 = unlimited
    // The tap-selected cell, or -1.
    public int Selected { get; private set; } = -1;
    public bool Busy => _resolving || Manager_Board.Instance.IsAnimating;

    // The request in focus: the player's choice (order tabs) while it is eligible, else the first.
    public int FocusSet => Level == null || View == null ? -1 : DeliveryRules.CurrentSet(View, Level, _focus);
    public DeliverySet FocusedSet => FocusSet >= 0 ? Level.sets[FocusSet] : null;

    // Undo works while playing and after running out of moves, not after the win.
    public bool CanUndo => _history.Count > 0 && State != GameState.Loading && State != GameState.Won && !Busy;

    // Case 1's guided first swap while it applies (no move yet), else null.
    public DeliveryTutorial RequiredSwap =>
        State == GameState.Playing && Level != null && Level.hasTutorial && Model.Stage == 0 && Model.Moves == 0 ? Level.tutorial : null;

    private int _focus;
    private bool _resolving;
    private readonly Stack<(DeliveryState model, int focus)> _history = new Stack<(DeliveryState, int)>();

    // Current press: the cell it started on, the neighbour it picked and whether the pair is held.
    private int _pressCell = -1;
    private int _dragDRow, _dragDCol;
    private int _target = -1;
    private bool _pairHeld;
    private bool _pressMoved;

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
        Manager_Board.Instance.OnStepShown += HandleStepShown;
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
        if (Manager_Board.Instance != null) Manager_Board.Instance.OnStepShown -= HandleStepShown;
        base.OnDestroy();
    }

    // Launch: the first case opens once, on the saved one (OfflineData.CurrentLevel). Right away,
    // unless the loading screen (UI_Loading) held it in Awake; it then calls Launch() as it fades out.
    public bool HasLaunched { get; private set; }
    private bool _launchHeld;

    public void HoldLaunch() => _launchHeld = true;

    public void Launch()
    {
        if (HasLaunched) return;
        HasLaunched = true;
        // The Tester may have loaded one already. Saves from older builds can be past the last case.
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
        _resolving = false;

        var level = Manager_Level.Instance.Load(levelNumber);
        if (level == null)
        {
            Debug.LogError($"[Game] Case {levelNumber} could not be loaded.");
            return;
        }
        CurrentLevel = Mathf.Clamp(levelNumber, 1, Manager_Level.Instance.LevelCount);
        Level = level;
        Model = DeliveryRules.Create(level);
        View = Model;
        _focus = DeliveryRules.CurrentSet(Model, level, 0);
        Selected = -1;
        Manager_Board.Instance.Build(Level, Model.Board);

        SetState(GameState.Playing);
        OnLevelStarted(CurrentLevel);
        RefreshBoardMarks();
        Debug.Log($"[Game] Case {CurrentLevel} started (\"{Level.title}\", {Level.sets.Length} sets, " +
                  $"{(Level.HasMoveLimit ? $"{Level.moveLimit} moves" : "no move limit")}, {Level.orderMode}).");
    }

    public void RestartLevel()
    {
        Manager_Audio.Instance?.PlaySound(SoundType.Restart);
        Haptics.Play(HapticType.Light);
        StartLevel(CurrentLevel);
    }

    public void AdvanceLevel() => StartLevel(CurrentLevel + 1);

    // An order tab: put another eligible request in focus. Costs no move.
    public void SelectSet(int index)
    {
        if (State != GameState.Playing || Busy || !DeliveryRules.Eligible(View, Level).Contains(index) || index == FocusSet) return;
        _focus = index;
        Selected = -1;
        RefreshBoardMarks();
        OnViewChanged();
    }

    // ---------- Drag / tap ----------

    private void HandlePress(Vector2 screenPosition)
    {
        var board = Manager_Board.Instance;
        _pairHeld = false;
        _pressMoved = false;
        _target = -1;
        _pressCell = State == GameState.Playing && !Busy ? board.CellAt(screenPosition) : -1;
    }

    private void HandleDrag(Vector2 screenStart, Vector2 screenNow)
    {
        if (_pressCell < 0) return;
        var board = Manager_Board.Instance;

        if (!_pressMoved)
        {
            float threshold = Tuning.DragStartThreshold * Mathf.Min(Screen.width, Screen.height);
            Vector2 delta = screenNow - screenStart;
            if (delta.magnitude < threshold) return;
            _pressMoved = true;

            // The dominant direction picks the neighbour, locked for the press. Screen up = row - 1.
            bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            _dragDRow = horizontal ? 0 : (delta.y > 0f ? -1 : 1);
            _dragDCol = horizontal ? (delta.x > 0f ? 1 : -1) : 0;
            int row = _pressCell / 3 + _dragDRow, col = _pressCell % 3 + _dragDCol;
            if (row < 0 || row > 2 || col < 0 || col > 2)
            {
                _pressCell = -1;
                OnSwapRejected(Rejection.Edge);
                return;
            }
            _target = row * 3 + col;
            if (View.Board[_pressCell] == null && View.Board[_target] == null) { _pressCell = -1; return; }
            if (Selected >= 0) { Selected = -1; RefreshBoardMarks(); }
            board.BeginDrag(_pressCell, _target);
            _pairHeld = true;
        }
        if (_pairHeld) board.SetDragOffset(DragOffset(screenStart, screenNow));
    }

    private void HandleRelease(Vector2 screenStart, Vector2 screenEnd, float secondsHeld)
    {
        int cell = _pressCell;
        _pressCell = -1;
        if (cell < 0) return;
        if (!_pressMoved) { Tap(cell); return; }
        if (!_pairHeld) return;
        _pairHeld = false;

        float offset = DragOffset(screenStart, screenEnd);
        bool commit = offset >= Tuning.DragCommitThreshold || (offset > 0f && secondsHeld <= Tuning.FlickMaxDuration);
        if (!commit || !TrySwap(cell, _target)) Manager_Board.Instance.CancelDrag();
    }

    // Tap a tile, then a neighbour (the playtest's second control).
    private void Tap(int cell)
    {
        if (State != GameState.Playing || Busy) return;
        if (Selected < 0)
        {
            if (View.Board[cell] == null) return;
            Selected = cell;
        }
        else if (Selected == cell) Selected = -1;
        else if (DeliveryRules.Adjacent(Selected, cell))
        {
            int a = Selected;
            Selected = -1;
            RefreshBoardMarks();
            ApplyMove(a, cell);
            return;
        }
        else
        {
            if (View.Board[cell] != null) Selected = cell;
            OnSwapRejected(Rejection.NotNeighbour);
        }
        RefreshBoardMarks();
        OnViewChanged();
    }

    // A swap made from code (taps, hints, the tester): the same rules and counting as a drag.
    // Ignored unless Playing and the board is idle.
    public bool ApplyMove(int a, int b)
    {
        var board = Manager_Board.Instance;
        if (State != GameState.Playing || Busy || board.IsDragging || _pairHeld) return false;
        if (!DeliveryRules.Adjacent(a, b) || (View.Board[a] == null && View.Board[b] == null)) return false;
        board.BeginDrag(a, b);
        if (TrySwap(a, b)) return true;
        board.CancelDrag();
        return false;
    }

    // The board is holding a and b. Resolves the swap and plays it; false = refused (snap back).
    private bool TrySwap(int a, int b)
    {
        var required = RequiredSwap;
        if (required != null && !((a == required.from && b == required.to) || (a == required.to && b == required.from)))
        {
            OnSwapRejected(Rejection.Tutorial);
            return false;
        }
        if (!DeliveryRules.Transact(Model, Level, a, b, out var result, out var events)) return false;

        _history.Push((Model, _focus));
        Model = result;
        _resolving = true;
        Selected = -1;
        OnSwapCommitted(a, b);
        Manager_Board.Instance.PlayMove(events, Resolve);
        return true;
    }

    private void HandleStepShown(DeliveryEvent e)
    {
        View = e.View;
        RefreshBoardMarks();
        OnViewChanged();
    }

    private void Resolve()
    {
        _resolving = false;
        View = Model;
        DeliveryRules.CheckInvariant(Model, Level);
        RefreshBoardMarks();
        OnViewChanged();
        OnMoveResolved();
        if (Model.Status == DeliveryStatus.Won) SetState(GameState.Won);
        else if (Model.Status == DeliveryStatus.Lost) SetState(GameState.Failed);
    }

    // Takes back the last swap with its deliveries and refills, refunding its move. From Failed this
    // returns to Playing. Refused while the board moves and after the win.
    public bool Undo()
    {
        if (!CanUndo) return false;
        var (model, focus) = _history.Pop();
        Model = model;
        View = model;
        _focus = focus;
        Selected = -1;
        Manager_Board.Instance.ShowBoard(Model.Board);
        Manager_Audio.Instance?.PlaySound(SoundType.Undo);
        Haptics.Play(HapticType.Light);
        SetState(GameState.Playing);
        RefreshBoardMarks();
        OnUndone();
        OnViewChanged();
        return true;
    }

    // The board's tints: the focused request's aligned items, the selection, case 1's muted tiles.
    private void RefreshBoardMarks()
    {
        var required = RequiredSwap;
        Manager_Board.Instance.SetMarks(FocusedSet, Selected, required?.from ?? -1, required?.to ?? -1);
    }

    private void HandleCancel()
    {
        if (_pairHeld) Manager_Board.Instance.CancelDrag();
        _pairHeld = false;
        _pressCell = -1;
    }

    // How far the pressed tile has been dragged towards its neighbour, in cells (0..1).
    private float DragOffset(Vector2 screenStart, Vector2 screenNow)
    {
        Vector2 cells = Manager_Board.Instance.ScreenDeltaToCells(screenStart, screenNow);
        float along = _dragDCol != 0 ? cells.x * _dragDCol : -cells.y * _dragDRow;    // cells.y is up
        return Mathf.Clamp01(along);
    }

#if UNITY_EDITOR
    // Tester only: end the case now, as if it had been won or lost.
    public void DebugEndLevel(bool won)
    {
        if (State == GameState.Playing) SetState(won ? GameState.Won : GameState.Failed);
    }
#endif

    // Saved on the win itself, so quitting before pressing Next keeps the progress.
    // Replaying an older case never moves CurrentLevel backwards.
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
            Debug.Log($"[Game] Case {CurrentLevel} complete in {MovesUsed} swaps.");
            SaveProgress();
        }
        else if (state == GameState.Failed)
        {
            Debug.Log($"[Game] Case {CurrentLevel} failed after {MovesUsed} swaps.");
            Manager_Audio.Instance?.PlaySound(SoundType.OutOfMoves);
            Haptics.Play(HapticType.Heavy);
        }
    }
}
