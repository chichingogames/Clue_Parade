using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// The flow around the board (the ten-case plan and storyboards, the 30-case handoff; this build swaps
// neighbouring tiles and has no tutorial): toasts, Hint (explain the clue first, then the next swap
// of a shortest solve from the current board, SwapPuzzle), the Notebook, the win / out-of-moves popups, the case map, the menu,
// the first-clear reward, the end-of-campaign feedback and the local playtest log. It decides;
// popups and the HUD display (they get callbacks and listen to the events below). Board rules stay
// in Manager_Game / BoardState.
public class Manager_Flow : Singleton<Manager_Flow>
{
    public event Action<string> OnToast = delegate { };
    public event Action OnHintAttention = delegate { };                 // pulse the Hint button
    public event Action OnWinCelebration = delegate { };                // confetti

    private static Manager_Game Game => Manager_Game.Instance;
    private static Manager_Board Board => Manager_Board.Instance;
    private static Manager_PopUp PopUps => Manager_PopUp.Instance;
    private static OfflineData Save => OfflineData.Instance;
    private int LevelCount => Mathf.Max(1, Manager_Level.Instance.LevelCount);

    // Bumped on every level start.
    private int _session;
    private SwapMove _hint;
    private Tween _attention;
    private Tween _outcomeDelay;
    private bool _lastWinWasFirstClear;
    private int _celebratedSession = -1;   // so the win popup says the same when it comes back
    private bool _firstLevelOpened;
    private bool _firstAction;
    private float _levelStartTime;
    private float _activeSeconds;

    private int _matchedAtPickUp;
    private SwapMove _pendingMove;
    private bool _undoing;

    public override void Awake2()
    {
        Game.OnLevelStarted += HandleLevelStarted;
        Game.OnStateChanged += HandleStateChanged;
        Game.OnMoveCommitted += HandleMoveCommitted;
        Game.OnUndone += HandleUndone;
        Board.OnTilesPickedUp += HandleTilesPickedUp;
        Board.OnBoardSettled += HandleBoardSettled;
    }

    // Active play time for the log: playing, no popup, app focused.
    private void Update()
    {
        if (Game != null && Game.State == GameState.Playing && !Game.IsPaused && Application.isFocused)
            _activeSeconds += Time.unscaledDeltaTime;
    }

    // ---------- Level start ----------

    private void HandleLevelStarted(int levelNumber)
    {
        var level = Game.Level;
        _session++;
        _hint = null;
        _firstAction = false;
        _pendingMove = null;
        _undoing = false;
        _activeSeconds = 0f;
        _levelStartTime = Time.unscaledTime;
        _outcomeDelay?.Kill();

        Log("case_start", $"case={level.Title}; difficulty={level.Difficulty}; budget={level.MoveLimit}; minimum={level.MinimumMoves}");
        RefreshGuide();
        StartAttentionTimer();

        bool firstOpen = !_firstLevelOpened;
        _firstLevelOpened = true;
        if (firstOpen && Save.CompletedLevels.Count >= LevelCount) ShowMap();
    }

    // ---------- Moves ----------

    private void HandleTilesPickedUp() => _matchedAtPickUp = Board.State.MatchedCount;

    private void HandleMoveCommitted(SwapMove move)
    {
        if (!_firstAction)
        {
            _firstAction = true;
            Log("first_move", $"milliseconds={Mathf.RoundToInt((Time.unscaledTime - _levelStartTime) * 1000f)}");
        }
        _attention?.Kill();
        _hint = null;
        Board.HideGuide();
        _pendingMove = move;
    }

    private void HandleUndone()
    {
        _undoing = true;
        _hint = null;
        Board.HideGuide();
    }

    // After the slide: the move's log entry and the guide, from the settled board.
    private void HandleBoardSettled()
    {
        var level = Game.Level;
        var state = Board.State;
        if (level == null || state == null) return;

        if (_undoing)
        {
            _undoing = false;
            Log("undo", $"matched={state.MatchedCount}; remaining={Remaining()}");
            RefreshGuide();
            return;
        }
        if (_pendingMove == null) return;     // a drag that snapped back

        var move = _pendingMove;
        _pendingMove = null;
        Log("move", $"move={move.Describe(level.Size)}; matchedBefore={_matchedAtPickUp}; matchedAfter={state.MatchedCount}; remaining={Remaining()}");
        RefreshGuide();
    }

    // ---------- Win / out of moves ----------

    private void HandleStateChanged(GameState state)
    {
        _outcomeDelay?.Kill();
        if (state == GameState.Won)
        {
            int coins = RecordClear(Game.CurrentLevel, Game.Level.FirstClearCoins);
            _lastWinWasFirstClear = coins > 0;
            Log("case_complete", $"moves={Game.MovesUsed}; activeSeconds={_activeSeconds:0.0}; firstClear={coins > 0}; coins={coins}");
            _outcomeDelay = DOVirtual.DelayedCall(Tuning.WinPopupDelay, () => ShowWin(_lastWinWasFirstClear)).SetLink(gameObject);
        }
        else if (state == GameState.Failed)
        {
            Log("case_failed", $"moves={Game.MovesUsed}; matched={Board.State.MatchedCount}; activeSeconds={_activeSeconds:0.0}");
            _outcomeDelay = DOVirtual.DelayedCall(Tuning.WinPopupDelay, ShowFailed).SetLink(gameObject);
        }
        if (state != GameState.Playing) Board.HideGuide();
    }

    // Saves the clear and its reward together, once per case, before the popup shows. Replays give 0.
    private int RecordClear(int level, int coins)
    {
        var save = Save;
        if (save.CompletedLevels.Contains(level)) return 0;
        save.CompletedLevels.Add(level);
        save.CompletedLevels.Sort();
        save.Coins += coins;
        save.Save();
        return coins;
    }

    private void ShowWin(bool firstClear)
    {
        if (Game.State != GameState.Won) return;
        var level = Game.Level;
        int next = level.NextLevelId;
        bool last = next <= 0 || next > LevelCount;
        string upNext = last ? null : TitleOf(next);

        var popup = PopUps.GetPopup<PopUp_Win>(PopupName.Win);
        popup.Setup(last ? "All 30 cases complete!" : "Case complete!",
                    last ? FileRef.Instance.Campaign?.endingText ?? level.Completion : level.Completion,
                    Subjects(level),
                    firstClear ? $"+{level.FirstClearCoins} coins" : "+0 coins",
                    firstClear ? "First completion reward" : "Reward already collected",
                    upNext,
                    last ? "Choose a case" : "Next case",
                    last ? (Action)Finish : () => StartLevel(next),
                    last ? null : (Action)ShowMap);
        PopUps.ShowPopup(PopupName.Win);
        if (_celebratedSession != _session)     // not again when the popup comes back from the map
        {
            _celebratedSession = _session;
            OnWinCelebration();
        }
    }

    private void ShowFailed()
    {
        if (Game.State != GameState.Failed) return;
        var state = Board.State;
        var popup = PopUps.GetPopup<PopUp_Failed>(PopupName.Failed);
        popup.Setup($"You matched <b>{state.MatchedCount}/{state.CellCount}</b> items. Try the same board again, or undo your last move.\n\nUndo is free and returns your move.",
                    () => { PopUps.ClosePopup(PopupName.Failed); Game.Undo(); },
                    () => StartLevel(Game.CurrentLevel),
                    ShowMap);
        PopUps.ShowPopup(PopupName.Failed);
    }

    // Closing the map or notebook over a finished case brings its outcome popup back, so the
    // player is never left on a finished board without a way on.
    private void ReturnToLevel()
    {
        PopUps.CloseAll();
        if (Game.State == GameState.Won) ShowWin(_lastWinWasFirstClear);
        else if (Game.State == GameState.Failed) ShowFailed();
    }

    public void StartLevel(int level)
    {
        PopUps.CloseAll();
        Game.StartLevel(Mathf.Clamp(level, 1, LevelCount));
    }

    // ---------- Hint ----------

    // The selected category's first unsatisfied fact, then the next swap of a shortest solve from the
    // current board (SwapPuzzle; one moving that fact's item when a shortest one does).
    public void OpenHint()
    {
        var level = Game.Level;
        var state = Board.State;
        if (Game.State != GameState.Playing || Board.IsAnimating || Board.IsDragging || state == null) return;
        int row = Board.SelectedRow;
        if (row < 0) return;

        int column = -1;
        for (int c = 0; c < state.Size; c++)
            if (!state.IsHome(row, c)) { column = c; break; }
        if (column < 0) column = 0;
        var item = level.Items[row * state.Size + column];

        SwapPuzzle.TryNextMove(state.TileIds(), out var move, row * state.Size + column);
        Log("clue_hint_open", $"item={item.Id}; move={move?.Describe(state.Size) ?? "none"}");
        string first = null, second = null;
        if (move != null)
        {
            first = level.ItemFor(state[move.row, move.col]).Label;
            second = level.ItemFor(state[move.OtherRow, move.OtherCol]).Label;
        }
        PopUps.GetPopup<PopUp_Hint>(PopupName.Hint).Setup(
            FileRef.Instance.Sprite(item.Sprite), item.Clue,
            move != null ? $"Swap the {first} and the {second}." : null,
            move != null ? $"Drag the {first} {(move.horizontal ? "right" : "down")} onto the {second}." : null,
            () => { PopUps.ClosePopup(PopupName.Hint); ShowHintMove(move); },
            () => PopUps.ClosePopup(PopupName.Hint));
        PopUps.ShowPopup(PopupName.Hint);
    }

    private void ShowHintMove(SwapMove move)
    {
        if (Game.State != GameState.Playing || move == null) return;
        _hint = move;
        Log("hint_shown", $"move={move.Describe(Game.Level.Size)}");
        RefreshGuide();
    }

    // The hint's swap while it is shown.
    private void RefreshGuide()
    {
        if (_hint != null && Game.State == GameState.Playing) Board.ShowGuide(_hint);
        else Board.HideGuide();
    }

    private void StartAttentionTimer()
    {
        _attention?.Kill();
        _attention = DOVirtual.DelayedCall(Tuning.HintAttentionDelay, () =>
        {
            if (Game.State == GameState.Playing && !Game.IsPaused && !_firstAction) OnHintAttention();
        }).SetLink(gameObject);
    }

    // ---------- Notebook ----------

    public void OpenNotebook()
    {
        var level = Game.Level;
        var state = Board.State;
        if (level == null || state == null || Board.IsAnimating) return;
        Log("notebook_open", $"matched={state.MatchedCount}");

        var names = new string[level.Subjects.Length];
        for (int c = 0; c < names.Length; c++) names[c] = level.Subjects[c].Name;
        var entries = new PopUp_Notebook.Entry[state.CellCount];
        for (int i = 0; i < entries.Length; i++)
        {
            var item = level.Items[i];
            entries[i] = new PopUp_Notebook.Entry
            {
                Icon = FileRef.Instance.Sprite(item.Sprite),
                Label = item.Label,
                Matched = state.IsTileHome(i),
            };
        }
        PopUps.GetPopup<PopUp_Notebook>(PopupName.Notebook).Setup(names, level.Categories, entries,
            $"Drag a tile onto a neighbour to swap them. All {state.CellCount} must match together.", ReturnToLevel);
        PopUps.ShowPopup(PopupName.Notebook);
    }

    // ---------- Case map, menu ----------

    // A case is open once the one before it is cleared (or it is cleared itself); level 1 always.
    private bool IsUnlocked(int level) => level <= 1 || Save.CompletedLevels.Contains(level) || Save.CompletedLevels.Contains(level - 1);

    public void ShowMap()
    {
        var campaign = FileRef.Instance.Campaign;
        if (campaign?.map == null)
        {
            Debug.LogError("[Flow] The campaign has no map. Check FileRef's content library.");
            return;
        }
        Log("map_open");
        int latest = 1;
        var nodes = new List<PopUp_Map.Node>();
        for (int n = 1; n <= LevelCount; n++)
        {
            bool open = IsUnlocked(n);
            if (open) latest = n;
            nodes.Add(new PopUp_Map.Node
            {
                Level = n,
                State = n == Game.CurrentLevel ? PopUp_Map.NodeState.Current : open ? PopUp_Map.NodeState.Open : PopUp_Map.NodeState.Locked,
                Done = Save.CompletedLevels.Contains(n),
            });
        }
        PopUps.GetPopup<PopUp_Map>(PopupName.LevelSelect).Setup(
            $"{Save.CompletedLevels.Count}/{LevelCount} cases complete · {Save.Coins} coins",
            campaign, nodes, latest, StartLevel, ReturnToLevel);
        PopUps.ShowPopup(PopupName.LevelSelect);
    }

    public void OpenMenu()
    {
        var audio = Manager_Audio.Instance;
        var popup = PopUps.GetPopup<PopUp_Menu>(PopupName.Menu);
        popup.Setup(audio == null || audio.SoundOn,
                     () => { audio?.ToggleSound(); popup.SetSound(audio == null || audio.SoundOn); },
                     () => StartLevel(Game.CurrentLevel),
                     ShowMap,
                     ReturnToLevel);
        PopUps.ShowPopup(PopupName.Menu);
    }

    private void ExportReport()
    {
        Log("report_export");
        try
        {
            string path = PlaytestLog.Export();
            Debug.Log($"[Flow] Playtest report saved to {path}");
            Toast("Results saved on this device.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Flow] Report export failed: {e.Message}");
            Toast("The results could not be saved.");
        }
    }

    // ---------- End of the campaign ----------

    private void Finish()
    {
        Log("playtest_finished");
        var save = Save;
        var questions = new[]
        {
            new PopUp_Feedback.Question { Key = "goal", Title = "Was the goal clear?", Selected = save.FeedbackGoal,
                Options = new[] { ("clear", "Yes, I knew what to match"), ("unclear", "I wasn’t sure what to match") } },
            new PopUp_Feedback.Question { Key = "wrap", Title = "Did swapping make sense?", Selected = save.FeedbackWrap,
                Options = new[] { ("clear", "Yes, after trying it"), ("unclear", "I still found it confusing") } },
            new PopUp_Feedback.Question { Key = "difficulty", Title = "How were the cases?", Selected = save.FeedbackDifficulty,
                Options = new[] { ("easy", "Too easy"), ("right", "About right"), ("hard", "Too difficult") } },
        };
        PopUps.GetPopup<PopUp_Feedback>(PopupName.Feedback).Setup($"{LevelCount}/{LevelCount} CASES COMPLETE", questions, answers =>
        {
            save.FeedbackGoal = answers.TryGetValue("goal", out var goal) ? goal : "";
            save.FeedbackWrap = answers.TryGetValue("wrap", out var wrap) ? wrap : "";
            save.FeedbackDifficulty = answers.TryGetValue("difficulty", out var difficulty) ? difficulty : "";
            save.Save();
            Log("feedback_saved", $"goal={save.FeedbackGoal}; wrap={save.FeedbackWrap}; difficulty={save.FeedbackDifficulty}");
            ExportReport();
            ShowMap();
        }, ShowMap);
        PopUps.ShowPopup(PopupName.Feedback);
    }

    // ---------- Helpers ----------

    private void Toast(string text) => OnToast(text);

    private int Remaining() => Game.Level != null && Game.Level.HasMoveLimit ? Game.MovesLeft : -1;

    private void Log(string type, string data = "") => PlaytestLog.Log(type, Game != null ? Game.CurrentLevel : 0, data);

    private static string TitleOf(int levelNumber) => Manager_Level.Instance.Load(levelNumber)?.Title ?? $"Case {levelNumber}";

    private static PopUp_Win.Subject[] Subjects(LevelDefinition level)
    {
        var subjects = new PopUp_Win.Subject[level.Subjects.Length];
        for (int i = 0; i < subjects.Length; i++)
            subjects[i] = new PopUp_Win.Subject { Rig = FileRef.Instance.Rig(level.Subjects[i].RigId), Name = level.Subjects[i].Name };
        return subjects;
    }
}
