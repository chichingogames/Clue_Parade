using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;

// The flow around the board (Tilewrap_Deduction_30_Level_Playtest, scope v5): the coach line under
// the board (a teaching line per opening case, then each case's reasoning tip), toasts, case 1's
// guided first swap, the clue cards (two at a time; More clues pages a shared pool; tapping one
// highlights only the items it names, and its profile for a column clue), Hint (an explanation first,
// then the next swap, DeliveryRules.Hint: free through the teaching cases, then coins), the
// Notebook (Clues / Case file), the case map, the menu, the milestone deduction question, the win
// (the case answered, evidence, the next lead) and out-of-moves popups (one +5 moves continue per
// attempt), the first-clear reward and the local playtest log. The cases cycle: after the last one,
// Next case opens case 1 again. It decides; popups and the HUD display (they get callbacks and listen
// to the events below). Board rules stay in Manager_Game / DeliveryRules.
public class Manager_Flow : Singleton<Manager_Flow>
{
    public event Action<string> OnToast = delegate { };
    public event Action<string, bool> OnCoach = delegate { };          // text, flash
    public event Action OnHintAttention = delegate { };                 // pulse the Hint button
    public event Action OnWinCelebration = delegate { };                // confetti
    public event Action<int, ICollection<string>> OnClueFocus = delegate { };   // profile column (-1 = none), items named (null = clear)
    public event Action OnHudChanged = delegate { };                    // the clue page or the coins changed
    public event Action<bool> OnCounterTeach = delegate { };            // case 4: spotlight the move counter

    private static Manager_Game Game => Manager_Game.Instance;
    private static Manager_Board Board => Manager_Board.Instance;
    private static Manager_PopUp PopUps => Manager_PopUp.Instance;
    private static OfflineData Save => OfflineData.Instance;
    private static DeliveryEconomy Economy => FileRef.Instance.Cases?.economy ?? new DeliveryEconomy();
    private int LevelCount => Mathf.Max(1, Manager_Level.Instance.LevelCount);

    private const float ClueFocusSeconds = 5.5f;

    private int _session;
    private Tween _attention;
    private Tween _outcomeDelay;
    private Tween _focusTimer;
    private bool _lastWinWasFirstClear;
    private bool _deductionPending;
    private int _celebratedSession = -1;   // so the win popup says the same when it comes back
    private bool _firstAction;
    private float _levelStartTime;
    private float _activeSeconds;
    private bool _refilledThisMove;
    private int _lastSelected = -1;
    private int _cluePage;
    private string _clueContext = "";

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

    // Help is free through the teaching cases (economy.freeHelpThroughLevel), then costs coins.
    private bool FreeHelp => Game.CurrentLevel <= Economy.freeHelpThroughLevel;

    // ---------- Case start ----------

    private void HandleLevelStarted(int levelNumber)
    {
        var level = Game.Level;
        _session++;
        _firstAction = false;
        _activeSeconds = 0f;
        _levelStartTime = Time.unscaledTime;
        _lastSelected = -1;
        _deductionPending = false;
        _outcomeDelay?.Kill();
        _focusTimer?.Kill();
        OnClueFocus(-1, null);
        SyncCluePage();

        Log("case_start", $"case={level.title}; items={level.items.Length}; sets={level.sets.Length}; rows={level.rows}; budget={level.moveLimit}; order={level.orderMode}; resumed={Game.Resumed}");
        DefaultCoach(false);
        StartAttentionTimer();
    }

    // ---------- Coach ----------

    // The line under the board for the current situation (the playtest's defaultCoach). The first
    // five cases teach one idea each; 6-14 show their reasoning tip.
    private void DefaultCoach(bool flash)
    {
        var level = Game.Level;
        if (Game.State != GameState.Playing || level == null) return;
        int id = Game.CurrentLevel, focus = Game.FocusSet, moves = Game.MovesUsed;
        OnCounterTeach(id == 4 && moves == 0 && level.HasMoveLimit);
        var label = Game.FocusedSet?.label.ToLowerInvariant() ?? "the next set";
        string text;
        if (level.hasTutorial)
        {
            if (Game.RequiredSwap != null)
            {
                var t = level.tutorial;
                Board.ShowHint(t.from, t.to, t.from / DeliveryRules.Columns);
                text = t.text;
            }
            else text = $"Now {label}. Read the names; match each item to its profile.";
        }
        else if (id == 2) text = focus == 2 ? "“Goes with” means the same pet. Look for the teal scarf above." : "Use the names above. Each pet gets one item from this set.";
        else if (id == 3) text = focus == 2 ? "“Does not” rules out a pairing. Find the sugar star first."
                               : focus == 1 ? "Match the frosting to the cake bases already confirmed above."
                               : "Use the two names to find where the cake bases belong.";
        else if (id == 4) text = moves == 0 ? $"{level.moveLimit} moves left. Every swap uses 1. Undo is free." : "Each swap uses 1 move. Use the confirmed scene to link the next items.";
        else if (id == 5) text = Game.View.SpawnIndex > 0 ? "New supplies arrived; your completed sets stay earned." : "Use familiar clues. Finish the first set to make room.";
        else if (id <= 14 && !string.IsNullOrEmpty(level.reasoningTip)) text = level.reasoningTip;
        else if (focus >= 0 && focus < 3 && level.cluePool.Length > 0) text = "Combine the case clues. Two stay visible; More clues and Notebook hold the rest.";
        else if (DeliveryRules.Eligible(Game.View, level).Count > 1) text = "Choose either request. Use confirmed scene details as evidence.";
        else text = "Use confirmed items above to identify the next three owners.";
        Coach(text, flash);
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
        OnCounterTeach(false);
        Log("swap", $"a={a}; b={b}");
    }

    private void HandleSwapRejected(Manager_Game.Rejection reason)
    {
        switch (reason)
        {
            case Manager_Game.Rejection.Tutorial:
                var t = Game.Level.tutorial;
                Coach(t.text);
                Board.ShowHint(t.from, t.to, t.from / DeliveryRules.Columns);
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
        SyncCluePage();
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
            Coach(Game.CurrentLevel == 5 ? "New supplies arrive at the top. Your completed sets stay earned."
                                         : $"New items arrived. Next: {Game.FocusedSet?.label.ToLowerInvariant() ?? "the next set"}.");
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

    // The clues of the request in focus (the shared pool for the first three sets of a pooled case).
    public DeliveryClue[] CurrentClues => Game.Level == null ? new DeliveryClue[0] : Game.Level.CluesFor(Game.FocusSet);
    public int CluePages => Mathf.Max(1, (CurrentClues.Length + 1) / 2);
    public int CluePage => _cluePage;

    // The two cards on show: indices into CurrentClues (wrapping on an odd last page).
    public int[] VisibleClueIndices
    {
        get
        {
            int n = CurrentClues.Length;
            if (n == 0) return new int[0];
            return n == 1 ? new[] { 0 } : new[] { _cluePage * 2 % n, (_cluePage * 2 + 1) % n };
        }
    }

    // A clue whose named items have all been delivered is confirmed.
    public static bool IsConfirmed(DeliveryClue clue, DeliveryState view) =>
        clue.refs != null && clue.refs.Length > 0 && clue.refs.All(view.Delivered.Contains);

    // The page resets when the case or the request in focus changes.
    private void SyncCluePage()
    {
        var context = $"{Game.CurrentLevel}:{Game.FocusSet}";
        if (context == _clueContext) return;
        _clueContext = context;
        _cluePage = 0;
        OnHudChanged();
    }

    // "More clues": the next two cards of the shared pool.
    public void NextCluePage()
    {
        if (Game.Busy || CluePages < 2) return;
        _cluePage = (_cluePage + 1) % CluePages;
        Board.ClearHint();
        OnClueFocus(-1, null);
        OnHudChanged();
        Coach("Combine the case clues. Two cards stay visible at a time.");
        Log("clue_page", $"page={_cluePage}");
    }

    // A clue card (slot 0 / 1): the items it names light up on the board and in the scene, and its
    // profile for a column clue, for a few seconds.
    public void FocusClue(int slot)
    {
        var indices = VisibleClueIndices;
        if (Game.State != GameState.Playing || Game.Busy || slot < 0 || slot >= indices.Length) return;
        var clue = CurrentClues[indices[slot]];
        var refs = clue.refs ?? new string[0];
        Board.ClearHint();
        Board.FocusItems(refs);
        int column = clue.rule != null && clue.rule.kind == "at" ? clue.rule.column : -1;
        OnClueFocus(column, refs);
        Coach(clue.text);
        _focusTimer?.Kill();
        _focusTimer = DOVirtual.DelayedCall(ClueFocusSeconds, () =>
        {
            Board.ClearHint();
            OnClueFocus(-1, null);
            if (Game.Level != null && Game.RequiredSwap != null) DefaultCoach(false);
        }).SetLink(gameObject);
        Log("clue_focus", $"setIndex={Game.FocusSet}; index={indices[slot]}");
    }

    // ---------- Hint ----------

    private class HintText
    {
        public int From, To, TargetRow;
        public string Text;
    }

    private static HintText HintInfo()
    {
        var level = Game.Level;
        var view = Game.View;
        DeliveryHint h;
        try { h = DeliveryRules.Hint(view, level, Game.FocusSet); }
        catch (Exception e) { Debug.LogError($"[Flow] Hint: {e.Message}"); return null; }
        if (h == null) return null;
        int from = h.From, to = h.To;
        var required = Game.RequiredSwap;
        if (required != null) { from = required.from; to = required.to; }
        string id = view.Board[from];
        string dir = to == from + 1 ? "right" : to == from - 1 ? "left" : to > from ? "down" : "up";
        return new HintText
        {
            From = from, To = to, TargetRow = h.TargetRow,
            Text = id != null ? $"Swipe the {level.Item(id).label.ToLowerInvariant()} {dir}." : "Swap into the empty space.",
        };
    }

    // An explanation first (free); the next-swap aid after it (free through the teaching cases).
    public void OpenHint()
    {
        if (Game.State != GameState.Playing || Game.Busy) return;
        var h = HintInfo();
        if (h == null) return;
        Log("hint_open", $"a={h.From}; b={h.To}");
        if (Game.RequiredSwap != null) { ShowHint(h); return; }

        int cost = FreeHelp ? 0 : Economy.hintCoins;
        PopUps.GetPopup<PopUp_Hint>(PopupName.Hint).Setup(
            Explain(),
            $"Show next swap · {(cost > 0 ? $"{cost} coins" : "free")}",
            Save.Coins >= cost,
            $"Balance: {Save.Coins} coins.",
            () =>
            {
                if (Save.Coins < cost) return;
                if (cost > 0)
                {
                    Save.Coins -= cost;
                    Save.Save();
                    Log("hint_coins", $"cost={cost}");
                }
                PopUps.ClosePopup(PopupName.Hint);
                ShowHint(h);
            },
            () => OpenNotebook(0),
            () => PopUps.ClosePopup(PopupName.Hint));
        PopUps.ShowPopup(PopupName.Hint);
    }

    private void ShowHint(HintText h)
    {
        Board.ShowHint(h.From, h.To, h.TargetRow);
        Coach(h.Text);
        Log("hint_shown", $"a={h.From}; b={h.To}");
        OnHudChanged();         // the coins may have changed
    }

    // "Read it another way" (the playtest's explainSet): the current clues in plain words.
    private static string Explain()
    {
        var level = Game.Level;
        var facts = Manager_Flow.Instance.CurrentClues;
        if (facts.Length == 0) return "Each profile receives one item from the set.";
        DeliveryClue Find(params string[] kinds) => facts.FirstOrDefault(f => f.rule != null && kinds.Contains(f.rule.kind));
        if (Game.CurrentLevel <= 5)
        {
            var exclusion = Find("different");
            if (exclusion != null) return $"{exclusion.text} That pairing is ruled out. Place the sugar star using the other clue, then use the remaining two choices.";
            var same = Find("same");
            if (same != null) return $"{same.text} Find the confirmed item above. Both items belong to that same profile. Each profile receives one item from the set.";
            return $"{facts[0].text} Match that item to the named column, then use the second clue. The last item belongs to the remaining profile.";
        }
        var anchor = Find("lowest", "highest");
        if (anchor != null && level.hasDossier && level.dossier.values.Length == 3)
        {
            var values = level.dossier.values;
            float value = anchor.rule.kind == "lowest" ? values.Min() : values.Max();
            int c = Array.IndexOf(values, value);
            return $"{anchor.text} Compare the {level.dossier.label.ToLowerInvariant()} badges: {level.subjects[c].name} has {level.dossier.labels[c]}. " +
                   $"That anchors the {level.Item(anchor.rule.a)?.label.ToLowerInvariant() ?? "item"}. Now follow the clues that connect it to another item.";
        }
        var link = Find("same");
        if (link != null) return $"{link.text} These two items share one profile, even when they belong to different sets. Use another clue to identify that profile. Keep confirmed items in the scene as evidence.";
        var spatial = Find("left", "next", "notNext");
        if (spatial != null) return $"{spatial.text} These positions refer to the fixed profiles above. First and last are not neighbours. Combine this with the other case clues.";
        return "Use all available clues together. An exclusion removes one possibility; each profile must receive one item from the set.";
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

    public void OpenNotebook() => OpenNotebook(0);

    // tab 0: the clues; tab 1: the case file.
    public void OpenNotebook(int tab)
    {
        var level = Game.Level;
        if (level == null || Game.Busy) return;
        Log("notebook_open", $"tab={(tab == 0 ? "clues" : "case")}");
        var view = Game.View;
        var sections = new List<PopUp_Notebook.Section>();
        string intro;
        PopUp_Notebook.Line Fact(DeliveryClue clue) => new PopUp_Notebook.Line
        {
            Icon = FileRef.Instance.Sprite(level.SpriteKey(clue.icon)), Text = clue.text, Confirmed = IsConfirmed(clue, view),
        };
        PopUp_Notebook.Line Text(string text, Sprite icon = null) => new PopUp_Notebook.Line { Text = text, Icon = icon };

        if (tab == 1)
        {
            intro = level.question;
            var res = level.caseResolution;
            var lines = res.evidence.Select(x => Text(x)).ToList();
            if (res.statements.Length > 0)
            {
                sections.Add(new PopUp_Notebook.Section { Title = "Case evidence", Lines = lines });
                sections.Add(new PopUp_Notebook.Section { Title = "Witness statements", Lines = res.statements.Select(x => Text(x)).ToList() });
            }
            else sections.Add(new PopUp_Notebook.Section { Title = "Case evidence", Lines = lines });
            if (level.hasDeduction)
                sections.Add(new PopUp_Notebook.Section { Title = "Festival investigation", Lines = level.deduction.evidence.Select(x => Text(x)).ToList() });
            for (int n = 1; n <= LevelCount; n++)
            {
                var earlier = Manager_Level.Instance.Load(n);
                if (string.IsNullOrEmpty(earlier.evidenceReveal) || !Save.CompletedLevels.Contains(n)) continue;
                sections.Add(new PopUp_Notebook.Section
                {
                    Title = $"Case {n} · {earlier.title}",
                    Lines = new List<PopUp_Notebook.Line> { Text(earlier.evidenceReveal, FileRef.Instance.Sprite(earlier.SpriteKey(earlier.evidenceIcon))) },
                });
            }
        }
        else
        {
            intro = "One item per profile in each set. Different sets can describe the same owner. Read all available clues; only two cards are pinned on the board.";
            if (level.hasDossier)
                sections.Add(new PopUp_Notebook.Section
                {
                    Title = $"Known {level.dossier.label.ToLowerInvariant()} facts",
                    Lines = level.subjects.OrderBy(s => s.column).Select(s => Text($"<b>{s.name}</b> · {level.dossier.labels[s.column]}")).ToList(),
                });
            var active = DeliveryRules.Eligible(view, level);
            if (level.cluePool.Length > 0)
                sections.Add(new PopUp_Notebook.Section
                {
                    Title = "Linked case clues · read together",
                    Current = Game.FocusSet >= 0 && Game.FocusSet < 3,
                    Lines = level.cluePool.Select(Fact).ToList(),
                });
            for (int i = 0; i < level.sets.Length; i++)
            {
                if (i < 3 && level.cluePool.Length > 0) continue;
                sections.Add(new PopUp_Notebook.Section
                {
                    Title = level.sets[i].label,
                    Status = view.CompletedSets.Contains(i) ? "Delivered" : active.Contains(i) ? "Active" : "Upcoming",
                    Current = i == Game.FocusSet,
                    Lines = level.sets[i].visibleClues.Select(Fact).ToList(),
                });
            }
            if (view.Delivered.Count > 0)
                sections.Add(new PopUp_Notebook.Section
                {
                    Title = "Confirmed scene facts",
                    Lines = level.subjects.OrderBy(s => s.column).Select(s => Text($"<b>{s.name}</b>: " + string.Join(", ",
                        level.sets.Select(g => g.items[s.column]).Where(view.Delivered.Contains).Select(id => level.Item(id).label)))).ToList(),
                });
        }
        PopUps.GetPopup<PopUp_Notebook>(PopupName.Notebook).Setup(tab, intro, sections, OpenNotebook, ReturnToLevel);
        PopUps.ShowPopup(PopupName.Notebook);
    }

    // ---------- Case map, menu ----------

    public void ShowCases()
    {
        Log("level_selector_open");
        var content = FileRef.Instance.Cases;
        var rows = new List<PopUp_Cases.Row>();
        var chapters = content?.chapters ?? new DeliveryChapter[0];
        for (int n = 1; n <= LevelCount; n++)
        {
            var chapter = chapters.FirstOrDefault(c => c.firstLevel == n);
            if (chapter != null) rows.Add(new PopUp_Cases.Row { Heading = $"{chapter.id}. {chapter.title}" });
            var level = Manager_Level.Instance.Load(n);
            rows.Add(new PopUp_Cases.Row
            {
                Level = n,
                Title = level.IsExpert ? $"{level.title} · <color=#B25336>EXPERT</color>" : level.title,
                Subtitle = $"{level.rows} rows · {level.items.Length} items · {level.sets.Length} sets · " +
                           (level.HasMoveLimit ? $"{level.moveLimit} moves" : "untimed practice"),
                Done = Save.CompletedLevels.Contains(n),
                Current = n == Game.CurrentLevel,
            });
        }
        int done = Save.CompletedLevels.Count(n => n <= LevelCount);
        PopUps.GetPopup<PopUp_Cases>(PopupName.LevelSelect).Setup(
            $"{LevelCount}-CASE PLAYTEST", "The missing festival star",
            $"Prepare the town, follow the evidence and return the missing star. {done}/{LevelCount} cases closed · {Save.Coins} coins.",
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
        if (Game.State == GameState.Won)
        {
            if (_deductionPending) ShowDeduction();
            else ShowWin(_lastWinWasFirstClear);
        }
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
            // A milestone case asks its question first (once: a replay goes straight to the win).
            _deductionPending = Game.Level.hasDeduction && !Save.CompletedLevels.Contains(Game.CurrentLevel);
            _outcomeDelay = DOVirtual.DelayedCall(Tuning.WinPopupDelay, () =>
            {
                if (_deductionPending) ShowDeduction();
                else AwardWin();
            }).SetLink(gameObject);
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
            OnCounterTeach(false);
        }
    }

    private void ShowDeduction()
    {
        if (Game.State != GameState.Won) return;
        var d = Game.Level.deduction;
        var popup = PopUps.GetPopup<PopUp_Deduction>(PopupName.Deduction);
        popup.Setup(d.question, string.Join("\n\n", d.evidence), d.options, answer =>
        {
            bool right = answer == d.answer;
            Log("deduction_answer", $"answer={answer}; correct={right}");
            if (!right)
            {
                popup.SetFeedback("That does not fit both pieces of evidence. Read them together and try again. No penalty.");
                popup.Disable(answer);
                return;
            }
            _deductionPending = false;
            PopUps.ClosePopup(PopupName.Deduction);
            AwardWin();
        });
        PopUps.ShowPopup(PopupName.Deduction);
    }

    private void AwardWin()
    {
        int coins = RecordClear(Game.CurrentLevel, Game.Level.firstClearCoins);
        _lastWinWasFirstClear = coins > 0;
        OnHudChanged();
        Log("case_won", $"firstClear={coins > 0}; seconds={_activeSeconds:0}; swaps={Game.MovesUsed}");
        ShowWin(_lastWinWasFirstClear);
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
        int id = Game.CurrentLevel, next = NextCase(id);
        bool last = id >= LevelCount;
        var nextCase = Manager_Level.Instance.Load(next);
        var res = level.caseResolution;
        var content = new PopUp_Win.Content
        {
            Kicker = level.IsExpert ? "EXPERT CASE SOLVED" : "EVERY REQUEST DELIVERED",
            Title = last ? "The festival is saved!" : "Case complete!",
            Completion = level.completionText,
            Question = res.question,
            AnswerIcon = string.IsNullOrEmpty(res.icon) ? null : FileRef.Instance.Sprite(level.SpriteKey(res.icon)),
            Answer = res.answer,
            Subjects = last ? null : Enumerable.Range(0, 3).Select(c => UI_SubjectCard.For(level, c)).ToArray(),
            Star = FileRef.Instance.Sprite("festival_star"),
            StoryTitle = id == 5 ? "Something is missing…" : null,
            StoryText = id == 5 ? "The festival star has not arrived. Its delivery tag has been changed. Follow the parcels to bring it home." : null,
            EvidenceIcon = string.IsNullOrEmpty(level.evidenceIcon) ? null : FileRef.Instance.Sprite(level.SpriteKey(level.evidenceIcon)),
            Evidence = level.evidenceReveal,
            Reward = firstClear ? $"+{level.firstClearCoins} coins" : "Already collected",
            Stats = $"{level.sets.Length} / {level.sets.Length} sets delivered · {Game.MovesUsed} swaps used",
            UpNext = nextCase.title,
            UpNextLine = !last && !string.IsNullOrEmpty(level.nextLead) ? $"Next lead: {level.nextLead}" : nextCase.subtitle,
            NextLabel = "Next case",
        };
        PopUps.GetPopup<PopUp_Win>(PopupName.Win).Setup(content, () => StartLevel(next), ShowCases);
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
        var level = Game.Level;
        var economy = Economy;
        bool canContinue = Game.Model.ContinuesUsed < economy.maxContinues;
        int cost = FreeHelp ? 0 : economy.continueCoins;
        string body = $"You have delivered {Game.Model.Stage} of {level.sets.Length} sets. Retry and Undo are free." +
                      (canContinue ? "" : " This attempt has used its extra moves.");
        PopUps.GetPopup<PopUp_Failed>(PopupName.Failed).Setup(
            body,
            $"+{economy.continueMoves} moves · {(cost > 0 ? $"{cost} coins" : "free")}",
            Save.Coins >= cost,
            canContinue ? $"One continue per attempt. Balance: {Save.Coins} coins." : null,
            canContinue ? () => Continue(cost) : (Action)null,
            Game.CanUndo,
            () => { PopUps.ClosePopup(PopupName.Failed); Game.Undo(); },
            () => StartLevel(Game.CurrentLevel),
            ShowCases);
        PopUps.ShowPopup(PopupName.Failed);
    }

    private void Continue(int cost)
    {
        if (Save.Coins < cost) return;
        var economy = Economy;
        if (!Game.Continue(economy.continueMoves, economy.maxContinues)) return;
        if (cost > 0)
        {
            Save.Coins -= cost;
            Save.Save();
        }
        PopUps.ClosePopup(PopupName.Failed);
        OnHudChanged();
        Log("continue_granted", $"cost={cost}");
        DefaultCoach(false);
        Coach($"+{economy.continueMoves} moves. Your deliveries stay earned.");
        StartAttentionTimer();
    }

    // ---------- Helpers ----------

    // The case after `level`; after the last one the cases start again from case 1.
    private int NextCase(int level) => level >= LevelCount ? 1 : level + 1;

    private void Toast(string text) => OnToast(text);

    private void Log(string type, string data = "") => PlaytestLog.Log(type, Game != null ? Game.CurrentLevel : 0, data);
}
