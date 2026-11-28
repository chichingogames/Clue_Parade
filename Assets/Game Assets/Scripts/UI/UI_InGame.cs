using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The in-game HUD on the content pack's 1080x1920 layout (ui_layout.json regions; built by
// ShiftSort > Build UI, which wires every field): the level pill (opens the case map), the menu
// button, the Hard badge, moves (or "Practice"), the case title and goal line, "CLUES" with "All
// facts in Notebook", two clue cards for the selected category (unsatisfied facts first, green with
// a check while satisfied), the three subjects above the board (registered rigs showing the fitted
// parts of the current board) with their names, the category tabs beside the rows (a tap selects
// the category), the control note, the live "x/9 matched" count and Undo / Notebook / Hint.
// Pure view: reads Manager_Game / Manager_Board / Manager_Flow (and FileRef for art); its buttons
// call Manager_Flow, Manager_Game and Manager_Board.SelectRow.
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
    [SerializeField] private GameObject _hardBadge;
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
    [SerializeField] private TMP_Text _goal;

    [Header("Clues")]
    [SerializeField] private Button _allFactsButton;
    [Tooltip("One per clue card, left to right. BoardState.PickClues chooses which of the row's facts they show.")]
    [SerializeField] private ClueSlot[] _clues = new ClueSlot[0];
    [SerializeField] private Sprite _cluePanel;
    [SerializeField] private Sprite _cluePanelMatched;

    [Header("Board surroundings")]
    [Tooltip("One per column, left to right.")]
    [SerializeField] private UI_SubjectCard[] _subjects = new UI_SubjectCard[0];
    [Tooltip("One per row, top to bottom.")]
    [SerializeField] private UI_CategoryTab[] _categories = new UI_CategoryTab[0];
    [SerializeField] private TMP_Text _progressText;

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

        _levelButton.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.ShowMap(); });
        _menuButton.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.OpenMenu(); });
        _allFactsButton.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.OpenNotebook(); });
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
        board.OnBoardSettled += HandleBoardSettled;
        board.OnTilesPickedUp += Refresh;
        board.OnSelectionChanged += Refresh;
        flow.OnToast += _toast.Show;
        flow.OnHintAttention += PulseHint;
        if (game.Level != null) ShowLevel();
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
            board.OnBoardSettled -= HandleBoardSettled;
            board.OnTilesPickedUp -= Refresh;
            board.OnSelectionChanged -= Refresh;
        }
        var flow = Manager_Flow.Instance;
        if (flow != null)
        {
            flow.OnToast -= _toast.Show;
            flow.OnHintAttention -= PulseHint;
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
        ShowLevel();
        Refresh();
    }

    private void HandleStateChanged(GameState state) => Refresh();

    private void HandleBoardSettled()
    {
        ApplyBoard(Manager_Board.Instance.LastSettleCelebrated);
        Refresh();
    }

    // The case's subjects (a fresh rig each) and category tabs.
    private void ShowLevel()
    {
        var level = Manager_Game.Instance.Level;
        if (level == null) return;
        for (int c = 0; c < _subjects.Length; c++)
        {
            if (c >= level.Subjects.Length) { _subjects[c].Hide(); continue; }
            var subject = level.Subjects[c];
            _subjects[c].Show(FileRef.Instance.Rig(subject.RigId), subject.Name);
        }
        for (int r = 0; r < _categories.Length; r++)
        {
            if (r >= level.Categories.Length) { _categories[r].gameObject.SetActive(false); continue; }
            int row = r;
            _categories[r].Setup(level.Categories[r], () => { Tap(); Manager_Board.Instance.SelectRow(row); });
        }
        ApplyBoard(animate: false);
    }

    // Every rig shows the parts its cells hold the right items for.
    private void ApplyBoard(bool animate)
    {
        var level = Manager_Game.Instance.Level;
        var state = Manager_Board.Instance.State;
        if (level == null || state == null) return;
        var ids = new string[state.CellCount];
        for (int r = 0; r < state.Size; r++)
            for (int c = 0; c < state.Size; c++)
                ids[r * state.Size + c] = level.ItemFor(state[r, c]).Id;
        foreach (var subject in _subjects)
            if (subject.isActiveAndEnabled) subject.ApplyBoard(ids, animate);
    }

    private void Refresh()
    {
        var game = Manager_Game.Instance;
        var level = game.Level;
        var boardManager = Manager_Board.Instance;
        var board = boardManager.State;
        if (level == null || board == null) return;

        bool playing = game.State == GameState.Playing;
        _levelText.text = $"Level {game.CurrentLevel}";
        _title.text = level.Title;
        _goal.text = level.Goal;
        _hardBadge.SetActive(level.IsHard);

        RefreshMoves(game, level);
        int row = boardManager.SelectedRow;
        RefreshClues(row, level, board);
        for (int r = 0; r < _categories.Length; r++) _categories[r].SetActive(r == row);

        _progressText.text = $"{board.MatchedCount}/{board.CellCount} matched";

        // A tap during a slide is refused by Manager_Game.Undo / Manager_Flow.OpenHint themselves.
        _undoButton.interactable = game.CanUndo;
        _hintButton.interactable = playing;
    }

    private void RefreshMoves(Manager_Game game, LevelDefinition level)
    {
        if (!level.HasMoveLimit)
        {
            _movesPanel.sprite = _practiceSprite;
            _movesValue.text = "Practice";
            _movesValue.fontSize = 36f;
            _movesLabel.text = "no move limit";
            _movesValue.color = _movesColor;
            return;
        }

        int left = Mathf.Max(0, game.MovesLeft);
        _movesPanel.sprite = _movesSprite;
        _movesValue.text = left.ToString();
        _movesValue.fontSize = 50f;
        _movesLabel.text = left == 1 ? "move left" : "moves left";
        _movesValue.color = left <= _lowMovesAt ? _movesLowColor : _movesColor;
    }

    // Two facts of the selected row: unsatisfied ones first. A satisfied one turns green with a check.
    private void RefreshClues(int row, LevelDefinition level, BoardState board)
    {
        var shown = row >= 0 ? board.PickClues(row, _clues.Length) : new int[0];
        for (int i = 0; i < _clues.Length; i++)
        {
            var slot = _clues[i];
            slot.Root.SetActive(i < shown.Length);
            if (i >= shown.Length) continue;

            int tile = row * board.Size + shown[i];
            var item = level.Items[tile];
            bool matched = board.IsTileHome(tile);
            slot.Text.text = item.Clue;
            var icon = FileRef.Instance.Sprite(item.Sprite);
            slot.Icon.gameObject.SetActive(icon != null);
            slot.Icon.sprite = icon;
            slot.Panel.sprite = matched ? _cluePanelMatched : _cluePanel;
            slot.Check.SetActive(matched);
        }
    }

    // Twice, gently (hintPulseSeconds 1.3). A tap or a level change stops it.
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
