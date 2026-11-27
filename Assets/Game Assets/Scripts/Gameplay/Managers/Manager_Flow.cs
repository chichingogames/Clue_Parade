using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

// The onboarding flow around the board (the approved clue prototype v2): lessons, the coach line,
// toasts, Hint (explain first, then solve the current board and guide the next swipe), the
// Notebook, the win / out-of-moves popups, the level selector, the menu, the first-clear reward,
// the end-of-playtest feedback and the local playtest log. It decides; popups and the HUD display
// (they get callbacks and listen to the events below). Board rules stay in Manager_Game / BoardState.
public class Manager_Flow : Singleton<Manager_Flow>
{
    public event Action<string, bool> OnCoachChanged = delegate { };   // text, success (green)
    public event Action<string> OnToast = delegate { };
    public event Action OnHintAttention = delegate { };                 // pulse the Hint button
    public event Action OnHintStateChanged = delegate { };              // HintPending changed
    public event Action OnWinCelebration = delegate { };                // confetti

    public string Coach { get; private set; } = "";
    public bool CoachSuccess { get; private set; }
    public bool HintPending { get; private set; }

    private static Manager_Game Game => Manager_Game.Instance;
    private static Manager_Board Board => Manager_Board.Instance;
    private static Manager_PopUp PopUps => Manager_PopUp.Instance;
    private static OfflineData Save => OfflineData.Instance;
    private int LevelCount => Mathf.Max(1, Manager_Level.Instance.LevelCount);

    // Bumped on every level start: a hint computed for an older level is dropped.
    private int _session;
    private LevelMove _hint;
    private Tween _attention;
    private Tween _outcomeDelay;
    private bool _lastWinWasFirstClear;
    private int _celebratedSession = -1;   // so the win popup says the same when it comes back
    private bool _firstLevelOpened;
    private bool _firstAction;
    private float _levelStartTime;
    private float _activeSeconds;

    // Snapshot when a line is picked up, to word the coach line after the move settles.
    private int _stepAtPickUp;
    private int _matchedAtPickUp;
    private LevelMove _pendingMove;
    private bool _undoing;

    public override void Awake2()
    {
        Game.OnLevelStarted += HandleLevelStarted;
        Game.OnStateChanged += HandleStateChanged;
        Game.OnMoveCommitted += HandleMoveCommitted;
        Game.OnMoveRejected += HandleMoveRejected;
        Game.OnUndone += HandleUndone;
        Board.OnLinePickedUp += HandleLinePickedUp;
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
        SetHintPending(false);
        _firstAction = false;
        _pendingMove = null;
        _undoing = false;
        _activeSeconds = 0f;
        _levelStartTime = Time.unscaledTime;
        _outcomeDelay?.Kill();

        SetCoach(level.Coach, false);
        Log("level_start", $"budget={level.MoveLimit}; lesson={level.Lesson?.id ?? "none"}");
        RefreshGuide();
        StartAttentionTimer();

        bool firstOpen = !_firstLevelOpened;
        _firstLevelOpened = true;
        if (level.Lesson != null && !Save.Lessons.Contains(level.Lesson.id)) ShowLesson(level);
        else if (firstOpen && Save.CompletedLevels.Count >= LevelCount) ShowLevelSelect();
    }

    private void ShowLesson(LevelDefinition level)
    {
        var lesson = level.Lesson;
        UI_CrewPortrait.Entry[] crew = null;
        int moves = 0;
        string kicker = "LESSON";
        switch (lesson.illustration)
        {
            case "neutralCrew":
                crew = CrewEntries(level);
                kicker = "LEARN THE GOAL";
                break;
            case "categoryOrder":
                crew = CategoryEntries(level);
                kicker = "LEARN COLUMNS";
                break;
            case "moveCounter":
                moves = level.MoveLimit;
                kicker = "LEARN MOVES";
                break;
        }

        Log("tutorial_open", $"lesson={lesson.id}");
        var popup = PopUps.GetPopup<PopUp_Lesson>(PopupName.Lesson);
        popup.Setup($"LEVEL {Game.CurrentLevel} · {kicker}", lesson, crew, moves, () =>
        {
            if (!Save.Lessons.Contains(lesson.id)) Save.Lessons.Add(lesson.id);
            Save.Save();
            Log("tutorial_acknowledged", $"lesson={lesson.id}");
            PopUps.ClosePopup(PopupName.Lesson);
        });
        PopUps.ShowPopup(PopupName.Lesson);
    }

    // ---------- Moves ----------

    private void HandleLinePickedUp()
    {
        var state = Board.State;
        _stepAtPickUp = state.ActiveStep;
        _matchedAtPickUp = state.MatchedCount;
    }

    private void HandleMoveCommitted(LevelMove move)
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

    private void HandleMoveRejected(LevelMove expected)
    {
        string text = $"First, swipe the {expected.Describe(Game.Level.Size)}.";
        Log("tutorial_other_move", $"expected={expected.Describe(Game.Level.Size)}");
        Toast(text);
    }

    private void HandleUndone()
    {
        _undoing = true;
        _hint = null;
        Board.HideGuide();
    }

    // After the slide: the move's log entry, the coach line and the guide, from the settled board.
    private void HandleBoardSettled()
    {
        var level = Game.Level;
        var state = Board.State;
        if (level == null || state == null) return;

        if (_undoing)
        {
            _undoing = false;
            Log("undo", $"matched={state.MatchedCount}; remaining={Remaining()}");
            SetCoach(Game.MovesUsed == 0 ? level.Coach : "Move restored. Take your time.", false);
            RefreshGuide();
            return;
        }
        if (_pendingMove == null) return;     // a drag that snapped back

        var move = _pendingMove;
        _pendingMove = null;
        int matched = state.MatchedCount;
        Log("move", $"move={move.Describe(level.Size)}; matchedBefore={_matchedAtPickUp}; matchedAfter={matched}; remaining={Remaining()}");

        int step = state.ActiveStep;
        if (Game.State == GameState.Won) SetCoach($"{matched}/{state.CellCount} matched. Everyone is ready!", true);
        else if (Game.State == GameState.Failed) SetCoach("Out of moves. Undo is free.", false);
        else if (step > _stepAtPickUp && _stepAtPickUp < state.StepCount)
            SetCoach($"{level.Categories[state.StepRows[_stepAtPickUp]]} matched. Now read the {CategoryOf(state)} clues.", true);
        else if (Board.LastNewMatches > 0)
            SetCoach($"{matched}/{state.CellCount} matched. Read the {CategoryOf(state)} clues above.", true);
        else SetCoach("You can move a matched item again. Undo is always free.", false);

        RefreshGuide();
    }

    private string CategoryOf(BoardState state) =>
        state.ActiveRow >= 0 ? Game.Level.Categories[state.ActiveRow] : "";

    // ---------- Win / out of moves ----------

    private void HandleStateChanged(GameState state)
    {
        _outcomeDelay?.Kill();
        if (state == GameState.Won)
        {
            bool first = RecordClear(Game.CurrentLevel);
            _lastWinWasFirstClear = first;
            Log("level_complete", $"moves={Game.MovesUsed}; activeSeconds={_activeSeconds:0.0}; firstClear={first}; coins={(first ? Tuning.FirstClearCoins : 0)}");
            _outcomeDelay = DOVirtual.DelayedCall(Tuning.WinPopupDelay, () => ShowWin(first)).SetLink(gameObject);
        }
        else if (state == GameState.Failed)
        {
            Log("level_failed", $"moves={Game.MovesUsed}; matched={Board.State.MatchedCount}; activeSeconds={_activeSeconds:0.0}");
            _outcomeDelay = DOVirtual.DelayedCall(Tuning.WinPopupDelay, ShowFailed).SetLink(gameObject);
        }
        if (state != GameState.Playing) Board.HideGuide();
    }

    // Saves the clear and its reward together, once per level, before the popup shows.
    private bool RecordClear(int level)
    {
        var save = Save;
        if (save.CompletedLevels.Contains(level)) return false;
        save.CompletedLevels.Add(level);
        save.CompletedLevels.Sort();
        save.Coins += Tuning.FirstClearCoins;
        save.Save();
        return true;
    }

    private void ShowWin(bool firstClear)
    {
        if (Game.State != GameState.Won) return;
        var level = Game.Level;
        int n = Game.CurrentLevel;
        int cells = level.Size * level.Size;
        string stats = $"{Game.MovesUsed} {(Game.MovesUsed == 1 ? "swipe" : "swipes")}" +
                       (level.HasMoveLimit ? $" · {Game.MovesLeft} moves left" : " · relaxed practice");
        bool last = n >= LevelCount;

        var popup = PopUps.GetPopup<PopUp_Win>(PopupName.Win);
        popup.Setup($"LEVEL {n} COMPLETE · {Mathf.Min(n, LevelCount)}/{LevelCount}", level.Completion, CrewEntries(level),
                    $"{cells}/{cells} items matched",
                    firstClear ? $"+{Tuning.FirstClearCoins} coins" : "First-clear reward already collected",
                    stats,
                    last ? "Finish the five-level playtest" : $"Next: {TitleOf(n + 1)}",
                    last ? (Action)Finish : () => StartLevel(n + 1),
                    ShowLevelSelect);
        PopUps.ShowPopup(PopupName.Win);
        if (_celebratedSession != _session)     // not again when the popup comes back from the level selector
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
        popup.Setup($"You matched <b>{state.MatchedCount}/{state.CellCount}</b> items. You can undo your last move or try the same board again.\n\nUndo is free and returns your move.",
                    () => { PopUps.ClosePopup(PopupName.Failed); Game.Undo(); },
                    () => StartLevel(Game.CurrentLevel),
                    ShowLevelSelect);
        PopUps.ShowPopup(PopupName.Failed);
    }

    // Closing the level selector or notebook over a finished level brings its outcome popup back,
    // so the player is never left on a finished board without a way on.
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

    // Step one: explain the first unmatched assignment of the active category.
    public void OpenHint()
    {
        var level = Game.Level;
        var state = Board.State;
        if (HintPending || Game.State != GameState.Playing || Board.IsAnimating || Board.IsDragging || state == null) return;
        int row = state.ActiveRow;
        if (row < 0) return;

        int column = -1;
        for (int c = 0; c < state.Size; c++)
            if (!state.IsHome(row, c)) { column = c; break; }
        if (column < 0) return;

        string itemId = level.Items[row * state.Size + column];
        var item = FileRef.Instance.GetItem(itemId);
        string character = FileRef.Instance.TryGetCharacter(level.Characters[column], out var data) ? data.Name : level.Characters[column];
        string clue = ClueText(level, itemId) ?? $"{character} gets the {item.Name.ToLowerInvariant()}.";
        string category = level.Categories[row];

        Log("clue_hint_open", $"item={itemId}");
        PopUps.GetPopup<PopUp_Hint>(PopupName.Hint).Setup(
            $"HINT · {category.ToUpperInvariant()}", item.Sprite, clue,
            $"Put the <b>{item.Name.ToLowerInvariant()}</b> in the <b>{category}</b> row, under <b>{character}</b>.\n\n" +
            $"You can move any row or column. Match <b>all {state.CellCount} items together</b> to finish.",
            () => { PopUps.ClosePopup(PopupName.Hint); RequestHintMove(); },
            () => PopUps.ClosePopup(PopupName.Hint));
        PopUps.ShowPopup(PopupName.Hint);
    }

    // Step two: solve the current board off the main thread and guide its first move. The result
    // is dropped if the level, the board or the state changed meanwhile.
    private async void RequestHintMove()
    {
        if (HintPending || Game.State != GameState.Playing) return;
        var state = Board.State;
        int[] tiles = state.TileIds();
        int size = state.Size;
        int session = _session;
        SetHintPending(true);
        Log("hint_requested");

        try
        {
            var route = await Task.Run(() => BoardSolver.Solve(tiles, size));
            if (session != _session || Game.State != GameState.Playing || !SameTiles(Board.State.TileIds(), tiles)) return;
            if (route == null || route.Count == 0)
            {
                Toast("Everything is already matched.");
                return;
            }
            _hint = route[0];
            SetCoach($"Hint: swipe the {_hint.Describe(size)}.", false);
            Log("hint_shown", $"move={_hint.Describe(size)}; routeLength={route.Count}");
            RefreshGuide();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Flow] Hint failed: {e.Message}");
            if (session == _session) Toast("The hint could not load. Please try again.");
        }
        finally
        {
            if (session == _session) SetHintPending(false);
        }
    }

    private static bool SameTiles(int[] a, int[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // The hint's move if one is shown, else the level's required first swipe while it applies.
    private void RefreshGuide()
    {
        var move = _hint ?? Game.RequiredMove;
        if (move != null && Game.State == GameState.Playing) Board.ShowGuide(move);
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

    private void SetHintPending(bool pending)
    {
        if (HintPending == pending) return;
        HintPending = pending;
        OnHintStateChanged();
    }

    // ---------- Notebook ----------

    public void OpenNotebook()
    {
        var level = Game.Level;
        var state = Board.State;
        if (level == null || state == null || Board.IsAnimating) return;
        Log("notebook_open", $"matched={state.MatchedCount}");

        var text = new StringBuilder();
        text.Append("<b>This is the goal.</b> Follow all nine item assignments below. The clue cards on screen focus on the highlighted row.\n");
        for (int r = 0; r < state.Size; r++)
        {
            string category = level.Categories[r];
            text.Append($"\n<b>{category.ToUpperInvariant()}</b>{(r == state.ActiveRow ? "  <color=#368779>· clues on screen</color>" : "")}\n");
            for (int c = 0; c < state.Size; c++)
            {
                string itemId = level.Items[r * state.Size + c];
                string clue = ClueText(level, itemId) ?? FileRef.Instance.GetItem(itemId).Name;
                text.Append(state.IsHome(r, c) ? $"{clue}  <color=#4A9A24><b>matched</b></color>\n" : $"{clue}\n");
            }
        }
        string[] places = { "Top", "Middle", "Bottom" };
        var order = new StringBuilder();
        for (int r = 0; r < state.Size; r++)
            order.Append($"{(state.Size == 3 ? places[r] : $"Row {r + 1}")} row: {level.Categories[r].ToLowerInvariant()}. ");
        text.Append($"\n<b>{order.ToString().TrimEnd()}</b>\n");
        text.Append("Swipe a whole row or column. Tiles leaving an edge wrap around to the opposite edge.\n");
        text.Append($"A check means an item is in the right place <b>now</b>. All {state.CellCount} must be right together to win.");

        PopUps.GetPopup<PopUp_Notebook>(PopupName.Notebook).Setup(CrewEntries(level), text.ToString(), ReturnToLevel);
        PopUps.ShowPopup(PopupName.Notebook);
    }

    // ---------- Level selector, menu ----------

    public void ShowLevelSelect()
    {
        Log("level_selector_open");
        var rows = new List<PopUp_LevelSelect.Row>();
        for (int n = 1; n <= LevelCount; n++)
        {
            var level = Manager_Level.Instance.Load(n);
            rows.Add(new PopUp_LevelSelect.Row
            {
                Level = n,
                Title = level?.Name ?? $"Level {n}",
                Subtitle = level != null && level.HasMoveLimit ? $"Practice · {level.MoveLimit} moves" : "Learn · no move limit",
                Done = Save.CompletedLevels.Contains(n),
                Current = n == Game.CurrentLevel,
            });
        }
        PopUps.GetPopup<PopUp_LevelSelect>(PopupName.LevelSelect).Setup(
            $"{Save.CompletedLevels.Count}/{LevelCount} levels complete. Choose any level for this playtest.",
            rows, StartLevel, ReturnToLevel);
        PopUps.ShowPopup(PopupName.LevelSelect);
    }

    public void OpenMenu()
    {
        var audio = Manager_Audio.Instance;
        var popup = PopUps.GetPopup<PopUp_Menu>(PopupName.Menu);
        popup.Setup(audio == null || audio.SoundOn,
                     () => { audio?.ToggleSound(); popup.SetSound(audio == null || audio.SoundOn); },
                     () => StartLevel(Game.CurrentLevel),
                     ShowLevelSelect,
                     ExportReport,
                     ConfirmFreshTest,
                     ReturnToLevel);
        PopUps.ShowPopup(PopupName.Menu);
    }

    private void ConfirmFreshTest()
    {
        PopUps.GetPopup<PopUp_Confirm>(PopupName.Confirm).Setup(
            "NEW PLAYTEST", "Start a fresh test?",
            "This clears the saved level progress, coins and feedback <b>on this device</b>. Save the results first if you need them.",
            "Start from level 1", () =>
            {
                OfflineData.Clear();
                PlaytestLog.Clear();
                Manager_Audio.Instance?.SetSoundOn(Manager_Audio.Instance.SoundOn);   // keep the current switch
                StartLevel(1);
            },
            "Keep this test", OpenMenu);
        PopUps.ShowPopup(PopupName.Confirm);
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

    // ---------- End of the playtest ----------

    private void Finish()
    {
        Log("playtest_finished");
        var save = Save;
        var questions = new[]
        {
            new PopUp_Feedback.Question { Key = "goal", Title = "Was the goal clear?", Selected = save.FeedbackGoal,
                Options = new[] { ("clear", "Yes, I knew what to match"), ("unclear", "I wasn’t sure what to match") } },
            new PopUp_Feedback.Question { Key = "wrap", Title = "Did wrapping make sense?", Selected = save.FeedbackWrap,
                Options = new[] { ("clear", "Yes, after trying it"), ("unclear", "I still found it confusing") } },
            new PopUp_Feedback.Question { Key = "difficulty", Title = "How were the first five levels?", Selected = save.FeedbackDifficulty,
                Options = new[] { ("easy", "Too easy"), ("right", "About right"), ("hard", "Too difficult") } },
        };
        PopUps.GetPopup<PopUp_Feedback>(PopupName.Feedback).Setup(questions, answers =>
        {
            save.FeedbackGoal = answers.TryGetValue("goal", out var goal) ? goal : "";
            save.FeedbackWrap = answers.TryGetValue("wrap", out var wrap) ? wrap : "";
            save.FeedbackDifficulty = answers.TryGetValue("difficulty", out var difficulty) ? difficulty : "";
            save.Save();
            Log("feedback_saved", $"goal={save.FeedbackGoal}; wrap={save.FeedbackWrap}; difficulty={save.FeedbackDifficulty}");
            ExportReport();
            ShowLevelSelect();
        }, ShowLevelSelect);
        PopUps.ShowPopup(PopupName.Feedback);
    }

    // ---------- Helpers ----------

    private void SetCoach(string text, bool success)
    {
        Coach = text ?? "";
        CoachSuccess = success;
        OnCoachChanged(Coach, success);
    }

    private void Toast(string text) => OnToast(text);

    private int Remaining() => Game.Level != null && Game.Level.HasMoveLimit ? Game.MovesLeft : -1;

    private void Log(string type, string data = "") => PlaytestLog.Log(type, Game != null ? Game.CurrentLevel : 0, data);

    private static string TitleOf(int levelNumber) => Manager_Level.Instance.Load(levelNumber)?.Name ?? $"Level {levelNumber}";

    // The clue sentence the level gives for an item (any step), or null.
    private static string ClueText(LevelDefinition level, string itemId)
    {
        foreach (var step in level.Steps)
            foreach (var clue in step.clues)
                if (clue.item == itemId) return clue.text;
        return null;
    }

    private static UI_CrewPortrait.Entry[] CrewEntries(LevelDefinition level)
    {
        var entries = new UI_CrewPortrait.Entry[level.Characters.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            FileRef.Instance.TryGetCharacter(level.Characters[i], out var character);
            entries[i] = new UI_CrewPortrait.Entry { Customer = character.Customer, Symbol = character.Symbol, Caption = character.Name ?? level.Characters[i] };
        }
        return entries;
    }

    private static UI_CrewPortrait.Entry[] CategoryEntries(LevelDefinition level)
    {
        string[] places = { "Top", "Middle", "Bottom" };
        var entries = new UI_CrewPortrait.Entry[level.Categories.Length];
        for (int r = 0; r < entries.Length; r++)
        {
            string place = level.Categories.Length == 3 ? places[r] : $"Row {r + 1}";
            entries[r] = new UI_CrewPortrait.Entry
            {
                Icon = FileRef.Instance.CategoryIcon(level.Categories[r]),
                Caption = $"{place}: {level.Categories[r].ToLowerInvariant()}",
            };
        }
        return entries;
    }
}
