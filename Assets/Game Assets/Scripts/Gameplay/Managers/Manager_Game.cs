using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Central state machine for the deduction cases. Decides whether a swap is legal (DeliveryRules),
// keeps the model, the request in focus and the undo history, ends the case, and saves an
// unfinished case so the next launch resumes it (board, refills, deliveries, moves, continue, focus
// and undo history). Moving on after a win or fail is the player's call (Manager_Flow's popups).
// Input is gated, never paused by a flag: it is accepted while Playing and no popup is up
// (ApplyInputGate re-runs on state changes and popup open / close).
//
// Controls (the playtest's): drag a tile towards a neighbour (it follows the pointer and commits at
// Tuning.DragCommitThreshold of a cell, or on a flick), or tap a tile and then a neighbour. Swapping
// with an empty cell is allowed; two empty cells are not. Edges do not wrap.
public class Manager_Game : Singleton<Manager_Game>
{
    public enum Rejection { Edge, NotNeighbour, Tutorial }

    // The saved attempt (OfflineData.Resume).
    [Serializable]
    private class ResumeData
    {
        public string Version;
        public int Case;
        public int Focus;
        public DeliveryState Model;
        public List<DeliveryState> History = new List<DeliveryState>();   // oldest first
        public List<int> HistoryFocus = new List<int>();
    }

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
    // True when the case was opened from a saved attempt (no first-look settle, no fresh start).
    public bool Resumed { get; private set; }
    public int MovesUsed => Model?.Moves ?? 0;
    public int MovesLeft => Level != null && Level.HasMoveLimit ? DeliveryRules.MoveBudget(Model, Level) - MovesUsed : -1;   // -1 = unlimited
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
    private readonly List<(DeliveryState model, int focus)> _history = new List<(DeliveryState, int)>();

    // Current press: the cell it started on, the neighbour it picked and whether the pair is held.
    private int _pressCell = -1;
    private int _dragDRow, _dragDCol;
    private int _target = -1;
    private bool _pairHeld;
    private bool _pressMoved;

    private static string ContentVersion => FileRef.Instance.Cases?.version ?? "";

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

    // Launch: the first case opens once, on the saved one (OfflineData.CurrentLevel), resuming its
    // saved attempt if there is one. Right away, unless the loading screen (UI_Loading) held it in
    // Awake; it then calls Launch() as it fades out.
    public bool HasLaunched { get; private set; }
    private bool _launchHeld;

    public void HoldLaunch() => _launchHeld = true;

    public void Launch()
    {
        if (HasLaunched) return;
        HasLaunched = true;
        // The Tester may have loaded one already. Saves from older builds can be past the last case.
        if (Level == null) StartLevel(Mathf.Clamp(OfflineData.Instance.CurrentLevel, 1, Mathf.Max(1, Manager_Level.Instance.LevelCount)), resume: true);
    }

    private void Start()
    {
        if (!_launchHeld) Launch();
    }

    // A fresh attempt at the case, or (resume) its saved attempt when the save holds one for it.
    public void StartLevel(int levelNumber, bool resume = false)
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
        _focus = DeliveryRules.CurrentSet(Model, level, 0);
        Resumed = resume && TryResume();
        View = Model;
        Selected = -1;
        Manager_Board.Instance.Build(Level, Model.Board, CurrentLevel, Resumed);

        var data = OfflineData.Instance;
        data.CurrentLevel = CurrentLevel;
        if (!Resumed) data.Resume = "";
        data.Save();

        var status = Model.Status;
        SetState(GameState.Playing);
        OnLevelStarted(CurrentLevel);
        RefreshBoardMarks();
        Debug.Log($"[Game] Case {CurrentLevel} {(Resumed ? "resumed" : "started")} (\"{Level.title}\", {Level.rows} rows, {Level.sets.Length} sets, " +
                  $"{(Level.HasMoveLimit ? $"{Level.moveLimit} moves" : "no move limit")}, {Level.orderMode}).");
        if (status == DeliveryStatus.Lost) SetState(GameState.Failed);
    }

    public void RestartLevel()
    {
        Manager_Audio.Instance?.PlaySound(SoundType.Restart);
        Haptics.Play(HapticType.Light);
        StartLevel(CurrentLevel);
    }

    // An order tab: put another eligible request in focus. Costs no move.
    public void SelectSet(int index)
    {
        if (State != GameState.Playing || Busy || !DeliveryRules.Eligible(View, Level).Contains(index) || index == FocusSet) return;
        _focus = index;
        Selected = -1;
        RefreshBoardMarks();
        SaveAttempt();
        OnViewChanged();
    }

    // Out of moves: +moves to this attempt, once. The flow charges for it first. Back to Playing.
    public bool Continue(int moves, int maxContinues)
    {
        if (State != GameState.Failed || Busy) return false;
        var model = Model.Clone();
        if (!DeliveryRules.Continue(model, moves, maxContinues)) return false;
        Model = model;
        View = model;
        // Undo cannot take back a used continue: the history keeps the extra moves.
        for (int i = 0; i < _history.Count; i++)
        {
            var (old, focus) = _history[i];
            var copy = old.Clone();
            copy.ExtraMoves = model.ExtraMoves;
            copy.ContinuesUsed = model.ContinuesUsed;
            _history[i] = (copy, focus);
        }
        SetState(GameState.Playing);
        SaveAttempt();
        RefreshBoardMarks();
        OnViewChanged();
        return true;
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
            int row = _pressCell / DeliveryRules.Columns + _dragDRow, col = _pressCell % DeliveryRules.Columns + _dragDCol;
            if (row < 0 || row >= Level.rows || col < 0 || col >= DeliveryRules.Columns)
            {
                _pressCell = -1;
                OnSwapRejected(Rejection.Edge);
                return;
            }
            _target = row * DeliveryRules.Columns + col;
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
        else if (DeliveryRules.Adjacent(Selected, cell, View.Board.Length))
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
        if (!DeliveryRules.Adjacent(a, b, View.Board.Length) || (View.Board[a] == null && View.Board[b] == null)) return false;
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

        _history.Add((Model, _focus));
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
        try { DeliveryRules.CheckInvariant(Model, Level); }
        catch (Exception e) { Debug.LogError($"[Game] Case {CurrentLevel}: {e.Message}"); }
        RefreshBoardMarks();
        SaveAttempt();
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
        var (model, focus) = _history[_history.Count - 1];
        _history.RemoveAt(_history.Count - 1);
        Model = model;
        View = model;
        _focus = focus;
        Selected = -1;
        Manager_Board.Instance.ShowBoard(Model.Board);
        Manager_Audio.Instance?.PlaySound(SoundType.Undo);
        Haptics.Play(HapticType.Light);
        SetState(GameState.Playing);
        RefreshBoardMarks();
        SaveAttempt();
        OnUndone();
        OnViewChanged();
        return true;
    }

    // The board's marks: the selection and case 1's muted tiles. No tint ever says whether an item
    // is under its right owner (the scope: no partial-correctness feedback).
    private void RefreshBoardMarks()
    {
        var required = RequiredSwap;
        Manager_Board.Instance.SetMarks(Selected, required?.from ?? -1, required?.to ?? -1);
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

    // ---------- Save / resume ----------

    // The attempt as it stands (cleared once won: the next launch opens the next case fresh).
    private void SaveAttempt()
    {
        var data = OfflineData.Instance;
        if (Model.Status == DeliveryStatus.Won) data.Resume = "";
        else
        {
            var resume = new ResumeData { Version = ContentVersion, Case = CurrentLevel, Focus = _focus, Model = Model };
            foreach (var (model, focus) in _history)
            {
                resume.History.Add(model);
                resume.HistoryFocus.Add(focus);
            }
            data.Resume = JsonUtility.ToJson(resume);
        }
        data.Save();
    }

    // Loads the saved attempt for this case into Model / history. False (and a fresh start) when
    // there is none, it is for another case or content, or it no longer fits the case.
    private bool TryResume()
    {
        var json = OfflineData.Instance.Resume;
        if (string.IsNullOrEmpty(json)) return false;
        try
        {
            var resume = JsonUtility.FromJson<ResumeData>(json);
            if (resume == null || resume.Case != CurrentLevel || resume.Version != ContentVersion || resume.Model == null) return false;
            if (resume.Model.Status == DeliveryStatus.Won) return false;
            var model = Restore(resume.Model);
            DeliveryRules.CheckInvariant(model, Level);
            var history = new List<(DeliveryState, int)>();
            for (int i = 0; i < resume.History.Count; i++)
                history.Add((Restore(resume.History[i]), i < resume.HistoryFocus.Count ? resume.HistoryFocus[i] : 0));
            Model = model;
            _history.AddRange(history);
            _focus = resume.Focus;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Game] The saved attempt for case {CurrentLevel} no longer fits; starting fresh. {e.Message}");
            return false;
        }
    }

    // JsonUtility writes empty cells as "": back to null.
    private static DeliveryState Restore(DeliveryState state)
    {
        state.Board = state.Board.Select(x => string.IsNullOrEmpty(x) ? null : x).ToArray();
        state.CompletedSets ??= new List<int>();
        state.Delivered ??= new List<string>();
        return state;
    }

#if UNITY_EDITOR
    // Tester only: end the case now, as if it had been won or lost.
    public void DebugEndLevel(bool won)
    {
        if (State == GameState.Playing) SetState(won ? GameState.Won : GameState.Failed);
    }
#endif

    // Saved on the win itself, so quitting before pressing Next keeps the progress: the next launch
    // opens the case after this one (case 1 after the last: the cases cycle).
    private void SaveProgress()
    {
        var data = OfflineData.Instance;
        data.PassedLevels = Mathf.Max(data.PassedLevels, CurrentLevel);
        data.CurrentLevel = CurrentLevel >= Manager_Level.Instance.LevelCount ? 1 : CurrentLevel + 1;
        data.Resume = "";
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
