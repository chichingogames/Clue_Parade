using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

// The flow around the board (Tilewrap_Any_Row_5_Case_Playtest): the coach line under the board,
// toasts, case 1's guided swap (and its hint after every move), Hint (the next swap towards the
// request in focus, DeliveryRules.Hint), tapping a clue (its item and owner light up; a scene clue
// lights the delivered prop it names), the Notebook (every set's facts), the case list, the menu,
// the win / out-of-moves popups, the first-clear reward, the closing feedback and the local
// playtest log. It decides; popups and the HUD display (they get callbacks and listen to the
// events below). Board rules stay in Manager_Game / DeliveryRules.
public class Manager_Flow : Singleton<Manager_Flow>
{
    public event Action<string> OnToast = delegate { };
    public event Action<string, bool> OnCoach = delegate { };          // text, flash
    public event Action OnHintAttention = delegate { };                 // pulse the Hint button
    public event Action OnWinCelebration = delegate { };                // confetti
    public event Action<int, string> OnClueFocus = delegate { };       // owner column (-1 = clear), scene-clue item or null

    private static Manager_Game Game => Manager_Game.Instance;
    private static Manager_Board Board => Manager_Board.Instance;
    private static Manager_PopUp PopUps => Manager_PopUp.Instance;
    private static OfflineData Save => OfflineData.Instance;
    private int LevelCount => Mathf.Max(1, Manager_Level.Instance.LevelCount);

    private const float ClueFocusSeconds = 5.5f;

    private int _session;
    private Tween _attention;
    private Tween _outcomeDelay;
    private Tween _focusTimer;
    private bool _lastWinWasFirstClear;
    private int _celebratedSession = -1;   // so the win popup says the same when it comes back
    private bool _firstAction;
    private float _levelStartTime;
    private float _activeSeconds;
    private bool _refilledThisMove;
    private int _lastSelected = -1;

    public override void Awake2()
    {
        Game.OnLevelStarted += HandleLevelStarted;
        Game.OnStateChanged += HandleStateChanged;
        Game.OnSwapCommitted += HandleSwapCommitted;
        Game.OnSwapRejected += HandleSwapRejected;
        Game.OnMoveResolved += HandleMoveResolved;
        Game.OnUndone += HandleUndone;
        Game.OnViewChanged += HandleViewChanged;
        Board.OnDelivering += HandleDelivering;
        Board.OnStepShown += HandleStepShown;
    }

    // Active play time for the log: playing, no popup, app focused.
    private void Update()
    {
        if (Game != null && Game.State == GameState.Playing && !Game.IsPaused && Application.isFocused)
            _activeSeconds += Time.unscaledDeltaTime;
    }

    // ---------- Case start ----------

    private void HandleLevelStarted(int levelNumber)
    {
        var level = Game.Level;
        _session++;
        _firstAction = false;
        _activeSeconds = 0f;
        _levelStartTime = Time.unscaledTime;
        _lastSelected = -1;
        _outcomeDelay?.Kill();
        _focusTimer?.Kill();
        OnClueFocus(-1, null);

        Log("case_start", $"case={level.title}; items={level.items.Length}; sets={level.sets.Length}; budget={level.moveLimit}; order={level.orderMode}");
        DefaultCoach(false);
        StartAttentionTimer();
    }

    // ---------- Coach ----------

    // The line under the board for the current situation. Case 1 also shows its next swap.
    private void DefaultCoach(bool flash)
    {
        var level = Game.Level;
        if (Game.State != GameState.Playing || level == null) return;
        if (level.hasTutorial)
        {
            var h = HintInfo();
            if (h == null) return;
            Board.ShowHint(h.From, h.To, h.TargetRow);
            Coach(Game.MovesUsed == 0 ? level.tutorial.text : $"{h.Text} Every row works; owners stay in their columns.", flash);
        }
        else if (Game.FocusedSet != null && Game.FocusedSet.visibleClues.Any(c => c.IsSceneClue))
            Coach("Look at the scene you have built. Tap a clue to highlight its object.", flash);
        else if (level.IsChoice && DeliveryRules.Eligible(Game.View, level).Count > 1)
            Coach("Choose either request. Both can clear in any row.", flash);
        else
            Coach("Line up the requested items under their owners in any row.", flash);
    }

    private void Coach(string text, bool flash = true) => OnCoach(text, flash);

    // ---------- Moves ----------

    private void HandleSwapCommitted(int a, int b)
    {
        if (!_firstAction)
        {
            _firstAction = true;
            Log("first_move", $"milliseconds={Mathf.RoundToInt((Time.unscaledTime - _levelStartTime) * 1000f)}");
        }
        _attention?.Kill();
        _focusTimer?.Kill();
        _refilledThisMove = false;
        Board.ClearHint();
        OnClueFocus(-1, null);
        Log("swap", $"a={a}; b={b}");
    }

    private void HandleSwapRejected(Manager_Game.Rejection reason)
    {
        switch (reason)
        {
            case Manager_Game.Rejection.Tutorial:
                var t = Game.Level.tutorial;
                Coach(t.text);
                Board.ShowHint(t.from, t.to, t.from / 3);
                Log("tutorial_redirect");
                break;
            case Manager_Game.Rejection.Edge:
                Toast("Tiles swap with neighbours. Edges do not wrap.");
                Log("edge_swipe");
                break;
            case Manager_Game.Rejection.NotNeighbour:
                Coach("Choose a neighbour, directly beside or above/below.");
                Log("non_adjacent_tap");
                break;
        }
    }

    private void HandleViewChanged()
    {
        int selected = Game.Selected;
        if (selected >= 0 && _lastSelected < 0 && Game.State == GameState.Playing) Coach("Swipe, or tap a neighbouring tile to swap.", false);
        _lastSelected = selected;
    }

    private void HandleDelivering(DeliveryEvent e, Sprite[] sprites, Vector3[] positions)
    {
        var set = Game.Level.sets[e.SetIndex];
        Coach("A complete set! Those items stay delivered.");
        Log("set_delivered", $"setIndex={e.SetIndex}; label={set.label}; row={e.Row}; stage={e.View.Stage}; moves={e.View.Moves}");
    }

    private void HandleStepShown(DeliveryEvent e)
    {
        if (e.Type != DeliveryEventType.Fall) return;
        bool refill = e.Spawned.Length > 0;
        _refilledThisMove |= refill;
        Coach(refill ? "New items arrive at the top. Your deliveries stay earned." : "The remaining items fall into place.");
        if (refill) Log("refill", $"items={string.Join(",", e.Spawned)}");
    }

    private void HandleMoveResolved()
    {
        if (Game.Model.Status != DeliveryStatus.Playing) return;
        if (_refilledThisMove && !Game.Level.hasTutorial)
            Coach($"New items arrived. Next: {Game.FocusedSet?.label.ToLowerInvariant() ?? "the next set"}.");
        else DefaultCoach(false);
        StartAttentionTimer();
    }

    private void HandleUndone()
    {
        _focusTimer?.Kill();
        Board.ClearHint();
        OnClueFocus(-1, null);
        DefaultCoach(false);
        Coach("Last swap undone. Moves, refills and deliveries restored.");
        Log("undo");
        StartAttentionTimer();
    }

    // An order tab (choice cases).
    public void SelectSet(int index)
    {
        if (Game.Busy || index == Game.FocusSet) return;
        Board.ClearHint();
        OnClueFocus(-1, null);
        Game.SelectSet(index);
        Coach("Either active request can clear in any row.");
        Log("request_selected", $"setIndex={index}");
    }

    // ---------- Clues ----------

    // A clue card: its item on the board and its owner light up (a scene clue also lights the
    // delivered prop it names) for a few seconds.
    public void FocusClue(int index)
    {
        var set = Game.FocusedSet;
        if (Game.State != GameState.Playing || Game.Busy || set == null || index < 0 || index >= set.visibleClues.Length) return;
        var clue = set.visibleClues[index];
        Board.ClearHint();
        Board.FocusItem(set.items[clue.column]);
        OnClueFocus(clue.column, clue.IsSceneClue ? clue.referenceItem : null);
        Coach(clue.IsSceneClue ? "Look at the highlighted scene. Use it to identify the owner."
                               : $"{clue.text} Complete a whole row to deliver the set.");
        _focusTimer?.Kill();
        _focusTimer = DOVirtual.DelayedCall(ClueFocusSeconds, () =>
        {
            Board.ClearHint();
            OnClueFocus(-1, null);
            if (Game.Level != null && Game.Level.hasTutorial) DefaultCoach(false);
        }).SetLink(gameObject);
        Log("clue_focus", $"setIndex={Game.FocusSet}; column={clue.column}; reference={(clue.IsSceneClue ? clue.referenceItem : "none")}");
    }

    // ---------- Hint ----------

    private class HintText
    {
        public int From, To, TargetRow, Column;
        public string Id, Other, Text;
    }

    private static HintText HintInfo()
    {
        var level = Game.Level;
        var view = Game.View;
        DeliveryHint h;
        try { h = DeliveryRules.Hint(view, level, Game.FocusSet); }
        catch (Exception e) { Debug.LogError($"[Flow] Hint: {e.Message}"); return null; }
        if (h == null) return null;
        string id = view.Board[h.From];
        int to = h.To, from = h.From;
        string dir = to == from + 1 ? "right" : to == from - 1 ? "left" : to > from ? "down" : "up";
        return new HintText
        {
            From = from, To = to, TargetRow = h.TargetRow, Id = id, Other = view.Board[to],
            Column = Array.IndexOf(level.sets[h.SetIndex].items, id),
            Text = $"Swipe the {level.Item(id).label.ToLowerInvariant()} {dir}.",
        };
    }

    public void OpenHint()
    {
        if (Game.State != GameState.Playing || Game.Busy) return;
        var h = HintInfo();
        if (h == null) return;
        var level = Game.Level;
        Log("hint_open", $"a={h.From}; b={h.To}");
        string owner = h.Column >= 0 ? level.subjects[h.Column].name : "its owner";
        PopUps.GetPopup<PopUp_Hint>(PopupName.Hint).Setup(
            FileRef.Instance.Sprite(level.Item(h.Id)?.sprite),
            h.Other != null ? FileRef.Instance.Sprite(level.Item(h.Other)?.sprite) : null,
            h.Text,
            $"This brings {owner}’s {level.Item(h.Id).label.ToLowerInvariant()} closer to a complete row. Top, middle and bottom rows all work; each item must be under its owner.",
            () =>
            {
                PopUps.ClosePopup(PopupName.Hint);
                Board.ShowHint(h.From, h.To, h.TargetRow);
                Coach(h.Text);
                Log("hint_shown", $"a={h.From}; b={h.To}");
            },
            () => PopUps.ClosePopup(PopupName.Hint));
        PopUps.ShowPopup(PopupName.Hint);
    }

    private void StartAttentionTimer()
    {
        _attention?.Kill();
        _attention = DOVirtual.DelayedCall(Tuning.HintAttentionDelay, () =>
        {
            if (Game.State == GameState.Playing && !Game.IsPaused) OnHintAttention();
        }).SetLink(gameObject);
    }

    // ---------- Notebook ----------

    public void OpenNotebook()
    {
        var level = Game.Level;
        if (level == null || Game.Busy) return;
        Log("notebook_open");
        var active = DeliveryRules.Eligible(Game.View, level);
        var sections = new PopUp_Notebook.Section[level.sets.Length];
        for (int i = 0; i < sections.Length; i++)
        {
            var set = level.sets[i];
            // The two shown clues first (as on the cards), then the third fact.
            var shown = new HashSet<int>(set.visibleClues.Select(c => c.column));
            var facts = set.visibleClues.Select(c => (FileRef.Instance.Sprite(level.Item(c.icon)?.sprite), c.text))
                .Concat(Enumerable.Range(0, 3).Where(c => !shown.Contains(c))
                    .Select(c => (FileRef.Instance.Sprite(level.Item(set.items[c])?.sprite), set.clues[c])))
                .ToArray();
            sections[i] = new PopUp_Notebook.Section
            {
                Label = set.label,
                Status = Game.View.CompletedSets.Contains(i) ? "Delivered" : active.Contains(i) ? "Active" : "Upcoming",
                Current = i == Game.FocusSet,
                Facts = facts,
            };
        }
        PopUps.GetPopup<PopUp_Notebook>(PopupName.Notebook).Setup(
            "Each subject keeps their column. Complete a requested set in any row. The third owner can be found by elimination, or checked here.",
            sections, ReturnToLevel);
        PopUps.ShowPopup(PopupName.Notebook);
    }

    // ---------- Cases, menu ----------

    public void ShowCases()
    {
        Log("level_selector_open");
        var rows = new List<PopUp_Cases.Row>();
        for (int n = 1; n <= LevelCount; n++)
        {
            var level = Manager_Level.Instance.Load(n);
            var notes = new List<string> { $"{level.items.Length} items", $"{level.sets.Length} delivery sets" };
            if (level.hasTutorial) notes.Add("guided first swap");
            if (level.refills.Length > 0) notes.Add("refills");
            if (level.sets.Any(s => s.visibleClues.Any(c => c.IsSceneClue))) notes.Add("scene clues");
            if (level.IsChoice) notes.Add("choose an order");
            rows.Add(new PopUp_Cases.Row
            {
                Level = n,
                Title = level.title,
                Subtitle = string.Join(" · ", notes),
                Done = Save.CompletedLevels.Contains(n),
                Current = n == Game.CurrentLevel,
            });
        }
        PopUps.GetPopup<PopUp_Cases>(PopupName.LevelSelect).Setup(
            $"{Save.CompletedLevels.Count(n => n <= LevelCount)}/{LevelCount} cases complete · {Save.Coins} coins",
            rows, StartLevel, ReturnToLevel);
        PopUps.ShowPopup(PopupName.LevelSelect);
    }

    public void OpenMenu()
    {
        var audio = Manager_Audio.Instance;
        var popup = PopUps.GetPopup<PopUp_Menu>(PopupName.Menu);
        popup.Setup(audio == null || audio.SoundOn,
                     () => { audio?.ToggleSound(); popup.SetSound(audio == null || audio.SoundOn); },
                     () => { Log("restart"); StartLevel(Game.CurrentLevel); },
                     ShowCases,
                     ReturnToLevel);
        PopUps.ShowPopup(PopupName.Menu);
    }

    // Closing a list over a finished case brings its outcome popup back, so the player is never
    // left on a finished board without a way on.
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

    // ---------- Win / out of moves ----------

    private void HandleStateChanged(GameState state)
    {
        _outcomeDelay?.Kill();
        if (state == GameState.Won)
        {
            int coins = RecordClear(Game.CurrentLevel, Game.Level.firstClearCoins);
            _lastWinWasFirstClear = coins > 0;
            Log("case_won", $"firstClear={coins > 0}; seconds={_activeSeconds:0}; swaps={Game.MovesUsed}");
            _outcomeDelay = DOVirtual.DelayedCall(Tuning.WinPopupDelay, () => ShowWin(_lastWinWasFirstClear)).SetLink(gameObject);
        }
        else if (state == GameState.Failed)
        {
            Log("out_of_moves", $"swaps={Game.MovesUsed}; stage={Game.Model.Stage}");
            _outcomeDelay = DOVirtual.DelayedCall(Tuning.WinPopupDelay, ShowFailed).SetLink(gameObject);
        }
        if (state != GameState.Playing)
        {
            _attention?.Kill();
            Board.ClearHint();
        }
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
        bool last = Game.CurrentLevel >= LevelCount;
        int sets = level.sets.Length;
        var popup = PopUps.GetPopup<PopUp_Win>(PopupName.Win);
        popup.Setup("Case complete!", level.completionText, Subjects(level),
                    firstClear ? $"+{level.firstClearCoins} coins" : "Already collected",
                    $"{sets} / {sets} sets delivered · {Game.MovesUsed} swaps used",
                    last ? null : Manager_Level.Instance.Load(Game.CurrentLevel + 1).title,
                    last ? "Finish playtest" : "Next case",
                    last ? (Action)Finish : () => StartLevel(Game.CurrentLevel + 1),
                    ShowCases);
        PopUps.ShowPopup(PopupName.Win);
        if (_celebratedSession != _session)     // not again when the popup comes back from the list
        {
            _celebratedSession = _session;
            OnWinCelebration();
        }
    }

    private void ShowFailed()
    {
        if (Game.State != GameState.Failed) return;
        PopUps.GetPopup<PopUp_Failed>(PopupName.Failed).Setup(
            "Your next try is free. You can also undo your last swap and keep going.",
            () => { PopUps.ClosePopup(PopupName.Failed); Game.Undo(); },
            () => StartLevel(Game.CurrentLevel),
            ShowCases);
        PopUps.ShowPopup(PopupName.Failed);
    }

    // ---------- End of the playtest ----------

    private void Finish()
    {
        Log("playtest_finished");
        var save = Save;
        var scenes = Enumerable.Range(1, LevelCount).Select(n => (n.ToString(), Manager_Level.Instance.Load(n).title)).ToArray();
        var questions = new[]
        {
            new PopUp_Feedback.Question { Key = "goal", Title = "Was the any-row rule clear?", Selected = save.FeedbackGoal,
                Options = new[] { ("clear", "Yes, any row works"), ("unclear", "I wasn’t sure which row counted") } },
            new PopUp_Feedback.Question { Key = "wrap", Title = "Did the two clues help you find the third owner?", Selected = save.FeedbackWrap,
                Options = new[] { ("clear", "Yes, by elimination"), ("unclear", "I needed the Notebook") } },
            new PopUp_Feedback.Question { Key = "difficulty", Title = "Which scene would you play again?", Selected = save.FeedbackDifficulty,
                Options = scenes },
        };
        PopUps.GetPopup<PopUp_Feedback>(PopupName.Feedback).Setup("PLAYTEST COMPLETE", questions, answers =>
        {
            save.FeedbackGoal = answers.TryGetValue("goal", out var goal) ? goal : "";
            save.FeedbackWrap = answers.TryGetValue("wrap", out var clues) ? clues : "";
            save.FeedbackDifficulty = answers.TryGetValue("difficulty", out var scene) ? scene : "";
            save.Save();
            Log("feedback_saved", $"anyRow={save.FeedbackGoal}; clues={save.FeedbackWrap}; replay={save.FeedbackDifficulty}");
            ExportReport();
            ShowCases();
        }, ShowCases);
        PopUps.ShowPopup(PopupName.Feedback);
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

    // ---------- Helpers ----------

    private void Toast(string text) => OnToast(text);

    private void Log(string type, string data = "") => PlaytestLog.Log(type, Game != null ? Game.CurrentLevel : 0, data);

    // Every subject with all its parts and delivered extras (the win popup's finished scene).
    private static PopUp_Win.Subject[] Subjects(DeliveryCase level)
    {
        var subjects = new PopUp_Win.Subject[level.subjects.Length];
        for (int i = 0; i < subjects.Length; i++)
        {
            var subject = level.subjects.First(s => s.column == i);
            subjects[i] = new PopUp_Win.Subject { Rig = FileRef.Instance.Rig(subject.rigId), Name = subject.name, Extras = UI_SubjectCard.ExtrasFor(level, i) };
        }
        return subjects;
    }
}
