using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The in-game HUD on the Tilewrap handoff's 1080x1920 layout (built by ShiftSort > Build UI, which
// wires every field): the level pill (opens the level selector), moves (or "Practice"), the menu
// button, title and goal line, the active category's clues (heading, progress, "All 9", two clue
// cards that turn green while matched), the coach line, the 0-9 match meter, the control note and
// Undo / Notebook / Hint. The customers above the board are world space (CharacterRow).
// Pure view: reads Manager_Game / Manager_Board / Manager_Flow (and FileRef for item art); its
// buttons call Manager_Flow and Manager_Game.
public class UI_InGame : MonoBehaviour
{
    [Serializable]
    public struct ClueSlot
    {
        public GameObject Root;
        public Image Panel;
        public Image Icon;
        public TMP_Text Text;
        public GameObject Check;
    }

    [Header("Top bar")]
    [SerializeField] private Button _levelButton;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private Button _menuButton;
    [SerializeField] private Image _movesPanel;
    [SerializeField] private TMP_Text _movesValue;
    [SerializeField] private TMP_Text _movesLabel;
    [SerializeField] private Sprite _movesSprite;
    [SerializeField] private Sprite _practiceSprite;
    [SerializeField] private Color _movesColor = new Color32(0x40, 0x28, 0x1F, 0xFF);
    [SerializeField] private Color _movesLowColor = new Color32(0x9C, 0x29, 0x1D, 0xFF);
    [Tooltip("The moves panel turns to the low color at this many moves left or fewer.")]
    [SerializeField, Min(0)] private int _lowMovesAt = 3;

    [Header("Title")]
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _subtitle;

    [Header("Clues")]
    [SerializeField] private TMP_Text _clueHeading;
    [SerializeField] private TMP_Text _clueProgress;
    [SerializeField] private Button _allCluesButton;
    [Tooltip("One per clue card, left to right. BoardState.PickClues chooses which of the step's clues they show.")]
    [SerializeField] private ClueSlot[] _clues = new ClueSlot[0];
    [SerializeField] private Sprite _cluePanel;
    [SerializeField] private Sprite _cluePanelMatched;

    [Header("Coach, progress")]
    [SerializeField] private Image _coachPanel;
    [SerializeField] private TMP_Text _coachText;
    [SerializeField] private Color _coachColor = new Color32(0xFF, 0xF3, 0xD8, 0xFF);
    [SerializeField] private Color _coachSuccessColor = new Color32(0xE9, 0xF8, 0xDA, 0xFF);
    [SerializeField] private TMP_Text _progressText;
    [Tooltip("The meter's fill; its right anchor is the matched fraction.")]
    [SerializeField] private RectTransform _progressFill;

    [Header("Buttons")]
    [SerializeField] private Button _undoButton;
    [SerializeField] private Button _notebookButton;
    [SerializeField] private Button _hintButton;

    [Header("Toast")]
    [SerializeField] private UI_Toast _toast;

    [Header("Intro")]
    [Tooltip("Scaled up from 0 one after another when the game opens, in this order. Timing: GameTuning > UI intro.")]
    [SerializeField] private RectTransform[] _introItems = new RectTransform[0];

    private bool _introPlayed;
    private Tween _hintPulse;

    private void Awake()
    {
        // Hidden until the first level opens (after the loading screen), then PlayIntro scales them up.
        foreach (var item in _introItems)
            if (item != null) item.localScale = Vector3.zero;

        _levelButton.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.ShowLevelSelect(); });
        _menuButton.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.OpenMenu(); });
        _allCluesButton.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.OpenNotebook(); });
        _notebookButton.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.OpenNotebook(); });
        _hintButton.onClick.AddListener(() => { Tap(); StopHintPulse(); Manager_Flow.Instance.OpenHint(); });
        _undoButton.onClick.AddListener(() => Manager_Game.Instance.Undo());
    }

    private void OnEnable()
    {
        var game = Manager_Game.Instance;
        var board = Manager_Board.Instance;
        var flow = Manager_Flow.Instance;
        if (game == null || board == null || flow == null) return;

        // Subscribe, then read the current values right away.
        game.OnLevelStarted += HandleLevelStarted;
        game.OnStateChanged += HandleStateChanged;
        game.OnMoveMade += Refresh;
        board.OnBoardSettled += Refresh;
        board.OnLinePickedUp += Refresh;
        flow.OnCoachChanged += HandleCoach;
        flow.OnToast += _toast.Show;
        flow.OnHintAttention += PulseHint;
        flow.OnHintStateChanged += Refresh;
        HandleCoach(flow.Coach, flow.CoachSuccess);
        Refresh();
    }

    private void OnDisable()
    {
        var game = Manager_Game.Instance;
        if (game != null)
        {
            game.OnLevelStarted -= HandleLevelStarted;
            game.OnStateChanged -= HandleStateChanged;
            game.OnMoveMade -= Refresh;
        }
        var board = Manager_Board.Instance;
        if (board != null)
        {
            board.OnBoardSettled -= Refresh;
            board.OnLinePickedUp -= Refresh;
        }
        var flow = Manager_Flow.Instance;
        if (flow != null)
        {
            flow.OnCoachChanged -= HandleCoach;
            flow.OnToast -= _toast.Show;
            flow.OnHintAttention -= PulseHint;
            flow.OnHintStateChanged -= Refresh;
        }
    }

    // Runs once per launch, on the first level start (as the loading screen fades), together with the
    // board intro. UI_InGame lives in the only scene and is never reloaded.
    private void PlayIntro()
    {
        _introPlayed = true;
        for (int i = 0; i < _introItems.Length; i++)
        {
            var item = _introItems[i];
            if (item == null) continue;
            item.localScale = Vector3.zero;
            item.DOScale(1f, Tuning.UiIntroDuration)
                .SetDelay(Tuning.UiIntroDelay + i * Tuning.UiIntroStagger)
                .SetEase(Ease.OutBack)
                .SetLink(item.gameObject);
        }
    }

    private void HandleLevelStarted(int levelNumber)
    {
        if (!_introPlayed) PlayIntro();
        StopHintPulse();
        Refresh();
    }

    private void HandleStateChanged(GameState state) => Refresh();

    private void HandleCoach(string text, bool success)
    {
        _coachText.text = text;
        _coachPanel.color = success ? _coachSuccessColor : _coachColor;
        _coachPanel.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private void Refresh()
    {
        var game = Manager_Game.Instance;
        var level = game.Level;
        var board = Manager_Board.Instance.State;
        if (level == null || board == null) return;

        bool playing = game.State == GameState.Playing;
        int levels = Mathf.Max(1, Manager_Level.Instance.LevelCount);
        _levelText.text = $"LEVEL {game.CurrentLevel} <size=70%>of {levels}</size>";
        _title.text = level.Name;
        _subtitle.text = level.Subtitle;

        RefreshMoves(game, level);
        int step = Mathf.Min(board.ActiveStep, level.Steps.Length - 1);
        RefreshClues(level.Steps[step], level, board);

        int matched = board.MatchedCount;
        _progressText.text = $"<size=130%><b>{matched}</b></size>/{board.CellCount} matched";
        var max = _progressFill.anchorMax;
        max.x = board.CellCount > 0 ? (float)matched / board.CellCount : 0f;
        _progressFill.anchorMax = max;

        // A tap during a slide is refused by Manager_Game.Undo / Manager_Flow.OpenHint themselves.
        _undoButton.interactable = game.CanUndo;
        _hintButton.interactable = playing && !Manager_Flow.Instance.HintPending;
    }

    private void RefreshMoves(Manager_Game game, LevelDefinition level)
    {
        if (!level.HasMoveLimit)
        {
            _movesPanel.sprite = _practiceSprite;
            _movesValue.text = "Practice";
            _movesValue.fontSize = 44f;
            _movesLabel.text = "no move limit";
            _movesValue.color = _movesColor;
            return;
        }

        int left = Mathf.Max(0, game.MovesLeft);
        _movesPanel.sprite = _movesSprite;
        _movesValue.text = left.ToString();
        _movesValue.fontSize = 84f;
        _movesLabel.text = left == 1 ? "move left" : "moves left";
        _movesValue.color = left <= _lowMovesAt ? _movesLowColor : _movesColor;
    }

    // The heading names the step's category ("CLUES · HATS") and how many of its slots are matched.
    private void RefreshClues(LevelStep step, LevelDefinition level, BoardState board)
    {
        var clues = step?.clues ?? new LevelClue[0];
        var tiles = new int[clues.Length];
        for (int i = 0; i < clues.Length; i++) tiles[i] = string.IsNullOrEmpty(clues[i].item) ? -1 : level.TileOf(clues[i].item);
        var shown = board.PickClues(tiles, _clues.Length);

        int matched = 0;
        if (step != null)
            for (int c = 0; c < board.Size; c++) if (board.IsHome(step.row, c)) matched++;
        _clueHeading.text = step != null ? $"CLUES <color=#D3EECE>· {level.Categories[step.row].ToUpperInvariant()}</color>" : "CLUES";
        _clueProgress.text = step != null ? $"{matched}/{board.Size} matched" : "";

        for (int i = 0; i < _clues.Length; i++)
        {
            var slot = _clues[i];
            slot.Root.SetActive(i < shown.Length);
            if (i >= shown.Length) continue;

            var clue = clues[shown[i]];
            bool isMatched = tiles[shown[i]] >= 0 && board.IsTileHome(tiles[shown[i]]);
            slot.Text.text = clue.text;
            bool hasItem = !string.IsNullOrEmpty(clue.item) && FileRef.Instance.TryGetItem(clue.item, out var item);
            slot.Icon.gameObject.SetActive(hasItem);
            if (hasItem) slot.Icon.sprite = FileRef.Instance.GetItem(clue.item).Sprite;
            slot.Panel.sprite = isMatched ? _cluePanelMatched : _cluePanel;
            slot.Check.SetActive(isMatched);
        }
    }

    // Twice, gently (handoff: hintPulseSeconds 1.3, hintPulseRepeats 2). A tap or a level change stops it.
    private void PulseHint()
    {
        StopHintPulse();
        var t = _hintButton.transform;
        _hintPulse = t.DOScale(1.08f, 0.65f).SetEase(Ease.InOutSine).SetLoops(4, LoopType.Yoyo).SetLink(gameObject)
                      .OnKill(() => t.localScale = Vector3.one);
    }

    private void StopHintPulse()
    {
        _hintPulse?.Kill();
        _hintPulse = null;
    }

    private static void Tap()
    {
        Manager_Audio.Instance?.PlaySound(SoundType.UiTap);
        Haptics.Play(HapticType.Light);
    }
}
