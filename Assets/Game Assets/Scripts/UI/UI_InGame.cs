using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The in-game HUD on the 1080x1920 reference layout (built by ShiftSort > Build UI, which wires every
// field), following Tilewrap_Any_Row_5_Case_Playtest: the case pill ("Case 1 / 5", opens the case
// list), the menu, the coin wallet, moves (or "Practice"); the case title, "0 of 3 sets delivered"
// with a pip per set; the request line ("CLUES · HATS") with "SET 1 / 3", or the order tabs when two
// requests are open; the two clue cards (tap: highlight the item and its owner; a scene clue is
// tagged); the three subjects above the columns (their delivered parts); the board note, the coach
// line, Undo / Notebook / Hint; the "Hats delivered!" banner over the delivered row, and the items
// flying from the board to their owners.
// Pure view: reads Manager_Game / Manager_Flow (and FileRef for art); its buttons call Manager_Flow
// and Manager_Game.
public class UI_InGame : MonoBehaviour
{
    [Serializable]
    public struct ClueSlot
    {
        public Button Button;
        public Image Icon;
        public TMP_Text Text;
        public GameObject SceneTag;
    }

    [Header("Top bar")]
    [SerializeField] private Button _levelButton;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private Button _menuButton;
    [SerializeField] private TMP_Text _coinsText;
    [SerializeField] private Image _movesPanel;
    [SerializeField] private TMP_Text _movesValue;
    [SerializeField] private TMP_Text _movesLabel;
    [SerializeField] private Sprite _movesSprite;
    [SerializeField] private Sprite _practiceSprite;
    [SerializeField] private Color _movesColor = new Color32(0x40, 0x28, 0x1F, 0xFF);
    [SerializeField] private Color _movesLowColor = new Color32(0xB2, 0x53, 0x36, 0xFF);
    [Tooltip("The moves panel turns to the low color at this many moves left or fewer.")]
    [SerializeField, Min(0)] private int _lowMovesAt = 5;

    [Header("Title and progress")]
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _progressText;
    [Tooltip("One per set, left to right; unused ones hide.")]
    [SerializeField] private Image[] _pips = new Image[0];
    [SerializeField] private Color _pipDone = new Color32(0xFB, 0xD4, 0x5B, 0xFF);
    [SerializeField] private Color _pipActive = new Color32(0x4F, 0x9E, 0x93, 0xFF);
    [SerializeField] private Color _pipWaiting = new Color32(0x0B, 0x5D, 0x5F, 0xC0);

    [Header("Request")]
    [SerializeField] private TMP_Text _requestText;
    [SerializeField] private TMP_Text _setPosition;
    [Tooltip("Order tabs (choice cases): one per open request.")]
    [SerializeField] private Button[] _orderTabs = new Button[0];
    [SerializeField] private Color _tabSelected = new Color32(0xFF, 0xDC, 0x61, 0xFF);
    [SerializeField] private Color _tabNormal = new Color32(0x17, 0x6F, 0x70, 0xFF);
    [SerializeField] private ClueSlot[] _clues = new ClueSlot[0];

    [Header("Subjects")]
    [Tooltip("One per column, left to right.")]
    [SerializeField] private UI_SubjectCard[] _subjects = new UI_SubjectCard[0];

    [Header("Under the board")]
    [SerializeField] private TMP_Text _coachText;
    [SerializeField] private RectTransform _coach;

    [Header("Buttons")]
    [SerializeField] private Button _undoButton;
    [SerializeField] private Button _notebookButton;
    [SerializeField] private Button _hintButton;

    [Header("Deliveries")]
    [Tooltip("\"Hats delivered!\" over the delivered row (y set per row).")]
    [SerializeField] private RectTransform _banner;
    [SerializeField] private TMP_Text _bannerText;
    [Tooltip("Flown item pictures are made under this rect (the reference root).")]
    [SerializeField] private RectTransform _flightLayer;

    [Header("Toast")]
    [SerializeField] private UI_Toast _toast;

    [Header("Intro")]
    [Tooltip("Scaled up from 0 one after another when the game opens, in this order. Timing: GameTuning > UI intro.")]
    [SerializeField] private RectTransform[] _introItems = new RectTransform[0];

    private static readonly Color TabTextSelected = new Color32(0x49, 0x31, 0x20, 0xFF);
    private static readonly Color TabTextNormal = new Color32(0xFF, 0xF1, 0xD7, 0xFF);

    private bool _introPlayed;
    private Tween _hintPulse;
    private Tween _bannerTween;
    private readonly List<string> _shownDelivered = new List<string>();

    private void Awake()
    {
        // Hidden until the first case opens (after the loading screen), then PlayIntro scales them up.
        foreach (var item in _introItems)
            if (item != null) item.localScale = Vector3.zero;
        if (_banner != null) _banner.gameObject.SetActive(false);

        _levelButton.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.ShowCases(); });
        _menuButton.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.OpenMenu(); });
        _notebookButton.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.OpenNotebook(); });
        _hintButton.onClick.AddListener(() => { Tap(); StopHintPulse(); Manager_Flow.Instance.OpenHint(); });
        _undoButton.onClick.AddListener(() => Manager_Game.Instance.Undo());
        for (int i = 0; i < _clues.Length; i++)
        {
            int index = i;
            _clues[i].Button.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.FocusClue(index); });
        }
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
        game.OnViewChanged += Refresh;
        game.OnUndone += HandleUndone;
        board.OnDelivering += HandleDelivering;
        board.OnDelivered += HandleDelivered;
        flow.OnToast += _toast.Show;
        flow.OnCoach += ShowCoach;
        flow.OnHintAttention += PulseHint;
        flow.OnClueFocus += HandleClueFocus;
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
            game.OnViewChanged -= Refresh;
            game.OnUndone -= HandleUndone;
        }
        var board = Manager_Board.Instance;
        if (board != null)
        {
            board.OnDelivering -= HandleDelivering;
            board.OnDelivered -= HandleDelivered;
        }
        var flow = Manager_Flow.Instance;
        if (flow != null)
        {
            flow.OnToast -= _toast.Show;
            flow.OnCoach -= ShowCoach;
            flow.OnHintAttention -= PulseHint;
            flow.OnClueFocus -= HandleClueFocus;
        }
    }

    // Runs once per launch, on the first case start (as the loading screen fades), together with the
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
        HideBanner();
        ShowLevel();
        Refresh();
    }

    private void HandleStateChanged(GameState state) => Refresh();

    // The case's subjects (a fresh rig each, nothing delivered yet).
    private void ShowLevel()
    {
        var level = Manager_Game.Instance.Level;
        if (level == null) return;
        for (int c = 0; c < _subjects.Length; c++)
        {
            var subject = level.subjects.FirstOrDefault(s => s.column == c);
            if (subject == null) { _subjects[c].Hide(); continue; }
            _subjects[c].Show(FileRef.Instance.Rig(subject.rigId), subject.name, UI_SubjectCard.ExtrasFor(level, c));
        }
        ApplyDelivered(Manager_Game.Instance.View.Delivered, animate: false);
    }

    private void HandleUndone()
    {
        HideBanner();
        ApplyDelivered(Manager_Game.Instance.Model.Delivered, animate: false);
    }

    private void ApplyDelivered(List<string> delivered, bool animate)
    {
        _shownDelivered.Clear();
        _shownDelivered.AddRange(delivered);
        foreach (var subject in _subjects)
            if (subject.isActiveAndEnabled) subject.ApplyDelivered(_shownDelivered, animate);
    }

    private void Refresh()
    {
        var game = Manager_Game.Instance;
        var level = game.Level;
        var view = game.View;
        if (level == null || view == null) return;

        int count = Mathf.Max(1, Manager_Level.Instance.LevelCount);
        _levelText.text = $"Case {game.CurrentLevel} <size=70%><alpha=#A0>/ {count}</size>";
        _title.text = level.title;
        _coinsText.text = OfflineData.Instance.Coins.ToString();
        RefreshMoves(game, level);
        RefreshProgress(level, view);
        RefreshRequest(game, level, view);

        bool playing = game.State == GameState.Playing;
        _undoButton.interactable = game.CanUndo;
        _hintButton.interactable = playing && !game.Busy;
        _notebookButton.interactable = !game.Busy;
    }

    private void RefreshMoves(Manager_Game game, DeliveryCase level)
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

    private void RefreshProgress(DeliveryCase level, DeliveryState view)
    {
        _progressText.text = $"{view.Stage} of {level.sets.Length} sets delivered";
        var active = DeliveryRules.Eligible(view, level);
        for (int i = 0; i < _pips.Length; i++)
        {
            _pips[i].gameObject.SetActive(i < level.sets.Length);
            _pips[i].color = view.CompletedSets.Contains(i) ? _pipDone : active.Contains(i) ? _pipActive : _pipWaiting;
        }
    }

    // "CLUES · HATS" with "SET 1 / 3", or "Choose a set" with a tab per open request; the two clues.
    private void RefreshRequest(Manager_Game game, DeliveryCase level, DeliveryState view)
    {
        int focus = game.FocusSet;
        var set = focus >= 0 ? level.sets[focus] : null;
        var active = DeliveryRules.Eligible(view, level);
        bool choice = level.IsChoice && active.Count > 1;

        _requestText.text = set == null ? "Every request delivered!" : choice ? "Choose a set" : $"CLUES · {set.label.ToUpperInvariant()}";
        _setPosition.gameObject.SetActive(!choice);
        _setPosition.text = set != null ? $"SET {view.Stage + 1} / {level.sets.Length}" : "COMPLETE";
        for (int i = 0; i < _orderTabs.Length; i++)
        {
            var tab = _orderTabs[i];
            bool shown = choice && i < active.Count;
            tab.gameObject.SetActive(shown);
            if (!shown) continue;
            int index = active[i];
            var label = tab.GetComponentInChildren<TMP_Text>();
            label.text = level.sets[index].label;
            label.color = index == focus ? TabTextSelected : TabTextNormal;
            ((Image)tab.targetGraphic).color = index == focus ? _tabSelected : _tabNormal;
            tab.onClick.RemoveAllListeners();
            tab.onClick.AddListener(() => { Tap(); Manager_Flow.Instance.SelectSet(index); });
        }

        for (int i = 0; i < _clues.Length; i++)
        {
            var slot = _clues[i];
            bool has = set != null && i < set.visibleClues.Length;
            slot.Button.gameObject.SetActive(has);
            if (!has) continue;
            var clue = set.visibleClues[i];
            slot.Text.text = clue.text;
            var icon = FileRef.Instance.Sprite(level.Item(clue.icon)?.sprite);
            slot.Icon.sprite = icon;
            slot.Icon.gameObject.SetActive(icon != null);
            slot.SceneTag.SetActive(clue.IsSceneClue);
        }
    }

    private void HandleClueFocus(int column, string propItem)
    {
        for (int c = 0; c < _subjects.Length; c++) _subjects[c].SetFocus(c == column, c == column ? propItem : null);
    }

    // ---------- Coach ----------

    private void ShowCoach(string text, bool flash)
    {
        _coachText.text = text;
        if (!flash || _coach == null) return;
        _coach.DOKill(true);
        _coach.localScale = Vector3.one;
        _coach.DOScale(1.03f, 0.3f).SetEase(Ease.OutQuad).SetLoops(2, LoopType.Yoyo).SetLink(_coach.gameObject);
    }

    // ---------- Deliveries ----------

    // The banner over the row and one picture per column flying from its cell to its owner.
    private void HandleDelivering(DeliveryEvent e, Sprite[] sprites, Vector3[] worldPositions)
    {
        var level = Manager_Game.Instance.Level;
        ShowBanner($"{level.sets[e.SetIndex].label} delivered!", e.Row);
        var cam = Camera.main;
        var canvas = _flightLayer.GetComponentInParent<Canvas>().rootCanvas;
        var uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        for (int c = 0; c < sprites.Length && c < _subjects.Length; c++)
        {
            if (sprites[c] == null || cam == null) continue;
            Vector2 from = cam.WorldToScreenPoint(worldPositions[c]);
            Vector2 to = RectTransformUtility.WorldToScreenPoint(uiCam, _subjects[c].LandingPoint);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_flightLayer, from, uiCam, out var localFrom);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_flightLayer, to, uiCam, out var localTo);

            var go = new GameObject("Flying Item", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_flightLayer, false);
            rect.SetAsLastSibling();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 180f);
            rect.anchoredPosition = localFrom;
            var image = go.GetComponent<Image>();
            image.sprite = sprites[c];
            image.preserveAspect = true;
            image.raycastTarget = false;
            float t = Manager_Board.FlightSeconds;
            Vector2 mid = new Vector2(Mathf.Lerp(localFrom.x, localTo.x, 0.45f), Mathf.Lerp(localFrom.y, localTo.y, 0.65f) + 25f);
            DOTween.Sequence()
                .Append(rect.DOAnchorPos(mid, t * 0.6f).SetEase(Ease.OutQuad))
                .Join(rect.DOScale(0.8f, t * 0.6f))
                .Append(rect.DOAnchorPos(localTo, t * 0.4f).SetEase(Ease.InQuad))
                .Join(rect.DOScale(0.32f, t * 0.4f))
                .Join(image.DOFade(0.2f, t * 0.4f))
                .OnComplete(() => Destroy(go))
                .SetLink(go);
        }
    }

    private void HandleDelivered(DeliveryEvent e)
    {
        ApplyDelivered(e.View.Delivered, animate: true);
        _bannerTween?.Kill();
        _bannerTween = DOVirtual.DelayedCall(0.24f, HideBanner).SetLink(gameObject);
    }

    // Over the delivered row: rows are 290 px tall from y 800 of the reference layout.
    private void ShowBanner(string text, int row)
    {
        if (_banner == null) return;
        _bannerTween?.Kill();
        _bannerText.text = text;
        _banner.anchoredPosition = new Vector2(_banner.anchoredPosition.x, -(800f + 290f * (row + 0.5f)));
        _banner.gameObject.SetActive(true);
        _banner.DOKill();
        _banner.localScale = Vector3.one * 0.85f;
        _banner.DOScale(1f, 0.15f).SetEase(Ease.OutBack).SetLink(_banner.gameObject);
    }

    private void HideBanner()
    {
        if (_banner != null) _banner.gameObject.SetActive(false);
    }

    // ---------- Hint pulse ----------

    // Twice, gently. A tap or a case change stops it.
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
