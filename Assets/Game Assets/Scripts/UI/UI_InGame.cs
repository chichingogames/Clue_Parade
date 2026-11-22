using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

// The in-game HUD on the Tilewrap pack's 1080x1920 layout (built by ShiftSort > Build UI, which wires
// every field): level and step, the step's goal, moves left, the step's clue notes, the characters
// above the board (their pose follows the confirmed steps), the wrap hint, and Undo / Notebook / the
// gold action button. Pure view: reads Manager_Game / Manager_Board (and FileRef for item and
// character art); its buttons call Manager_Game commands.
//
// The gold button changes with the state: Hint while playing (placeholder), Next level after a win,
// Retry when out of moves. Notebook is a placeholder.
public class UI_InGame : MonoBehaviour
{
    [Serializable]
    public struct ClueSlot
    {
        public GameObject Root;
        public UnityEngine.UI.Image Icon;
        public TMP_Text Text;
    }

    [Serializable]
    public struct CharacterSlot
    {
        public UnityEngine.UI.Image Portrait;    // under Characters, behind the counter ledge
        public RectTransform Plate;              // under Name Plates, in front of it
        public TMP_Text Name;
    }

    private const string WonGoal = "Everyone is ready!";
    private const string FailedGoal = "Out of moves";

    [Header("Title")]
    [SerializeField] private TMP_Text _levelStage;
    [SerializeField] private TMP_Text _goal;

    [Header("Moves")]
    [SerializeField] private TMP_Text _movesValue;
    [SerializeField] private TMP_Text _movesLabel;
    [SerializeField] private Color _movesColor = new Color32(0x42, 0x1B, 0x19, 0xFF);
    [SerializeField] private Color _movesLowColor = new Color32(0xD3, 0x2F, 0x2F, 0xFF);
    [Tooltip("The moves count turns red at this many moves left or fewer.")]
    [SerializeField, Min(0)] private int _lowMovesAt = 2;

    [Header("Clues")]
    [SerializeField] private GameObject _cluesHeading;
    [Tooltip("One per clue note, left to right. A step shows its first clues in these; extra clues are not shown.")]
    [SerializeField] private ClueSlot[] _clues = new ClueSlot[0];

    [Header("Characters")]
    [Tooltip("One per board column, left to right; each is moved over its column.")]
    [SerializeField] private CharacterSlot[] _characters = new CharacterSlot[0];

    [Header("Buttons")]
    [SerializeField] private UnityEngine.UI.Button _undoButton;
    [SerializeField] private UnityEngine.UI.Button _notebookButton;
    [SerializeField] private UnityEngine.UI.Button _actionButton;
    [SerializeField] private UnityEngine.UI.Image _actionIcon;
    [SerializeField] private TMP_Text _actionLabel;
    [SerializeField] private Sprite _hintIcon;
    [SerializeField] private Sprite _nextIcon;
    [SerializeField] private Sprite _retryIcon;
    [Tooltip("Next and Retry use the Shift & Sort icons, which are white: tinted this. The pack's hint icon is drawn as is.")]
    [SerializeField] private Color _legacyIconTint = new Color32(0x42, 0x1B, 0x19, 0xFF);

    [Header("Sound")]
    [SerializeField] private UnityEngine.UI.Button _soundButton;
    [SerializeField] private UnityEngine.UI.Image _soundIcon;
    [SerializeField] private Sprite _soundOnSprite;
    [SerializeField] private Sprite _soundOffSprite;

    [Header("Intro")]
    [Tooltip("Scaled up from 0 one after another when the game opens, in this order. Timing: GameTuning > UI intro.")]
    [SerializeField] private RectTransform[] _introItems = new RectTransform[0];

    private bool _introPlayed;
    private int _shownPose = -1;

    private void Awake()
    {
        // Hidden until the first level opens (after the loading screen), then PlayIntro scales them up.
        foreach (var item in _introItems)
            if (item != null) item.localScale = Vector3.zero;

        _soundButton.onClick.AddListener(ToggleSound);
        _undoButton.onClick.AddListener(() => Manager_Game.Instance.Undo());
        _notebookButton.onClick.AddListener(Tap);    // placeholder
        _actionButton.onClick.AddListener(OnAction);
    }

    private void OnEnable()
    {
        var game = Manager_Game.Instance;
        var board = Manager_Board.Instance;
        if (game == null || board == null) return;

        // Subscribe, then read the current values right away.
        game.OnLevelStarted += HandleLevelStarted;
        game.OnStateChanged += HandleStateChanged;
        game.OnMoveMade += Refresh;
        board.OnBoardSettled += Refresh;
        if (Manager_Audio.Instance != null)
        {
            Manager_Audio.Instance.OnSoundChanged += HandleSoundChanged;
            HandleSoundChanged(Manager_Audio.Instance.SoundOn);
        }
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
        if (Manager_Board.Instance != null) Manager_Board.Instance.OnBoardSettled -= Refresh;
        if (Manager_Audio.Instance != null) Manager_Audio.Instance.OnSoundChanged -= HandleSoundChanged;
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
        _shownPose = -1;
        PlaceCharacters();
        Refresh();
    }

    private void HandleStateChanged(GameState state) => Refresh();

    private void Refresh()
    {
        var game = Manager_Game.Instance;
        var level = game.Level;
        var board = Manager_Board.Instance.State;
        if (level == null || board == null) return;

        bool won = game.State == GameState.Won;
        bool failed = game.State == GameState.Failed;
        int step = Mathf.Min(board.ActiveStep, level.Steps.Length - 1);

        _levelStage.text = won ? $"Level {game.CurrentLevel} · Complete"
                         : $"Level {game.CurrentLevel} · Step {step + 1} of {level.Steps.Length}";
        _goal.text = won ? WonGoal : failed ? FailedGoal : level.Steps[step].goal;

        RefreshMoves(game, level);
        RefreshClues(won ? null : level.Steps[step].clues);
        RefreshCharacters(level, board.ConfirmedSteps);

        _undoButton.interactable = game.CanUndo;
        _actionIcon.sprite = won ? _nextIcon : failed ? _retryIcon : _hintIcon;
        _actionIcon.color = won || failed ? _legacyIconTint : Color.white;
        _actionLabel.text = won ? "Next" : failed ? "Retry" : "Hint";
    }

    private void RefreshMoves(Manager_Game game, LevelDefinition level)
    {
        if (!level.HasMoveLimit)
        {
            _movesValue.text = "–";
            _movesLabel.text = "no limit";
            _movesValue.color = _movesColor;
            return;
        }

        int left = game.MovesLeft;
        _movesValue.text = left.ToString();
        _movesLabel.text = left == 1 ? "move left" : "moves left";
        _movesValue.color = left <= _lowMovesAt ? _movesLowColor : _movesColor;
    }

    // clues null: none shown (after a win).
    private void RefreshClues(LevelClue[] clues)
    {
        int count = clues == null ? 0 : Mathf.Min(clues.Length, _clues.Length);
        if (clues != null && clues.Length > _clues.Length)
            Debug.LogWarning($"[UI_InGame] The step has {clues.Length} clues; only {_clues.Length} fit the HUD.");

        _cluesHeading.SetActive(count > 0);
        for (int i = 0; i < _clues.Length; i++)
        {
            var slot = _clues[i];
            slot.Root.SetActive(i < count);
            if (i >= count) continue;

            slot.Text.text = clues[i].text;
            bool hasItem = !string.IsNullOrEmpty(clues[i].item) && FileRef.Instance.TryGetItem(clues[i].item, out _);
            slot.Icon.gameObject.SetActive(hasItem);
            if (hasItem) slot.Icon.sprite = FileRef.Instance.GetItem(clues[i].item).Sprite;
        }
    }

    // Pose k after k confirmed steps (the last pose once they run out). A new pose gets a little hop.
    private void RefreshCharacters(LevelDefinition level, int confirmedSteps)
    {
        bool changed = confirmedSteps != _shownPose;
        bool hop = changed && _shownPose >= 0;
        _shownPose = confirmedSteps;
        if (!changed) return;

        for (int c = 0; c < _characters.Length && c < level.Characters.Length; c++)
        {
            if (!FileRef.Instance.TryGetCharacter(level.Characters[c], out var character)) continue;
            var slot = _characters[c];
            slot.Name.text = character.Name;
            slot.Portrait.sprite = character.Poses[Mathf.Min(confirmedSteps, character.Poses.Length - 1)];
            if (!hop) continue;

            var t = slot.Portrait.rectTransform;
            t.DOComplete();
            t.DOPunchScale(new Vector3(0.08f, 0.12f, 0f), 0.35f, 5, 0.6f).SetLink(t.gameObject);
        }
    }

    // Each character (portrait and name plate) is centred over its board column; columns without a
    // character, and characters beyond the board, are hidden.
    private void PlaceCharacters()
    {
        var board = Manager_Board.Instance;
        int columns = Manager_Game.Instance.Level.Size;
        for (int c = 0; c < _characters.Length; c++)
        {
            var slot = _characters[c];
            bool shown = c < columns;
            slot.Portrait.gameObject.SetActive(shown);
            slot.Plate.gameObject.SetActive(shown);
            if (!shown) continue;

            // Board column centre in reference px (x from the left of the 1080 px screen).
            float world = ReferenceLayout.BoardCenter.x + (c - (columns - 1) * 0.5f) * board.CellSize.x;
            float x = UI_ReferenceFit.ReferenceSize.x * 0.5f + world * 100f;
            SetCenterX(slot.Portrait.rectTransform, x);
            SetCenterX(slot.Plate, x);
        }
    }

    // Children of the layout are anchored top-left; keep the element's width and move its centre.
    private static void SetCenterX(RectTransform rect, float x) =>
        rect.anchoredPosition = new Vector2(x + rect.rect.width * (rect.pivot.x - 0.5f), rect.anchoredPosition.y);

    private void OnAction()
    {
        var game = Manager_Game.Instance;
        switch (game.State)
        {
            case GameState.Won:
                Tap();
                game.AdvanceLevel();
                break;
            case GameState.Failed:
                game.RestartLevel();
                break;
            default:
                Tap();       // Hint: placeholder
                break;
        }
    }

    private static void Tap()
    {
        Manager_Audio.Instance?.PlaySound(SoundType.UiTap);
        Haptics.Play(HapticType.Light);
    }

    // The tap plays after switching, so it is heard when turning sound on and not when turning it off.
    private void ToggleSound()
    {
        var audio = Manager_Audio.Instance;
        if (audio == null) return;
        audio.ToggleSound();
        Tap();
    }

    private void HandleSoundChanged(bool on) => _soundIcon.sprite = on ? _soundOnSprite : _soundOffSprite;
}
