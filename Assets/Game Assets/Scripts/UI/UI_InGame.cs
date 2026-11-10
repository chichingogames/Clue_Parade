using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The HUD: level title, sound toggle, matched / moves pills, the message panel and the
// Next level / Undo / Restart buttons. Pure view: reads Manager_Game, and its buttons call
// Manager_Game commands. Built by ShiftSort > Build UI (UIBuilder), which wires every field.
//
// Layout: the top bar is anchored to the top of the safe area and the bottom stack to its bottom
// (board width); the pills are children of Board Area (UI_BoardArea), so they follow the board. The camera keeps the board inside the
// Tuning.HudTop/BottomReserve bands, which is the room this HUD has.
public class UI_InGame : MonoBehaviour
{
    private const string PlayingTitle = "Match shapes across and colors down";
    private const string PlayingBody = "Swipe rows or columns to line them up.";
    private const string WonTitle = "Board complete";
    private const string WonBody = "Every tile matches its shape and color.";
    private const string FailedTitle = "Out of moves";
    private const string FailedBody = "Undo for one more move, or restart the board.";

    [Header("Top bar")]
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private Button _soundButton;
    [SerializeField] private Image _soundIcon;
    [SerializeField] private Sprite _soundOnSprite;
    [SerializeField] private Sprite _soundOffSprite;

    [Header("Pills")]
    [SerializeField] private TMP_Text _matchedText;
    [SerializeField] private Image _movesPill;
    [SerializeField] private TMP_Text _movesText;
    [SerializeField] private Sprite _movesSprite;
    [SerializeField] private Sprite _movesLowSprite;
    [SerializeField] private Color _movesColor = new Color32(0x6E, 0x5B, 0x4A, 0xFF);
    [SerializeField] private Color _movesLowColor = new Color32(0xB1, 0x4A, 0x3F, 0xFF);
    [Tooltip("The moves pill turns red at this many moves left or fewer.")]
    [SerializeField, Min(0)] private int _lowMovesAt = 1;

    [Header("Message")]
    [SerializeField] private Image _messagePanel;
    [SerializeField] private TMP_Text _messageTitle;
    [SerializeField] private TMP_Text _messageBody;
    [SerializeField] private Sprite _panelNeutralSprite;
    [SerializeField] private Sprite _panelWinSprite;
    [SerializeField] private Sprite _panelFailSprite;

    [Header("Buttons")]
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _undoButton;
    [SerializeField] private Button _restartButton;

    [Header("Intro")]
    [Tooltip("Scaled up from 0 one after another when the game opens, in this order. Timing: GameTuning > UI intro.")]
    [SerializeField] private RectTransform[] _introItems = new RectTransform[0];

    private void Awake()
    {
        _soundButton.onClick.AddListener(ToggleSound);
        _nextButton.onClick.AddListener(() =>
        {
            Manager_Audio.Instance?.PlaySound(SoundType.UiTap);
            Haptics.Play(HapticType.Light);
            Manager_Game.Instance.AdvanceLevel();
        });
        _undoButton.onClick.AddListener(() => Manager_Game.Instance.Undo());
        _restartButton.onClick.AddListener(() => Manager_Game.Instance.RestartLevel());
    }

    // Runs once per launch: UI_InGame lives in the only scene and is never reloaded, so moving
    // between levels does not replay it. Start runs before the first frame renders, so nothing
    // flashes at full size first.
    private void Start() => PlayIntro();

    private void PlayIntro()
    {
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

    private void HandleLevelStarted(int levelNumber) => Refresh();

    private void HandleStateChanged(GameState state) => Refresh();

    private void Refresh()
    {
        var game = Manager_Game.Instance;
        var level = game.Level;
        var board = Manager_Board.Instance.State;
        if (level == null || board == null) return;

        _levelText.text = $"LEVEL {game.CurrentLevel:00}";
        _matchedText.text = $"{board.MatchedCount} / {board.Size * board.Size} matched";

        bool low = level.HasMoveLimit && game.MovesLeft <= _lowMovesAt;
        _movesText.text = !level.HasMoveLimit ? "Unlimited moves"
                        : game.MovesLeft == 1 ? "1 move left"
                        : $"{game.MovesLeft} moves left";
        _movesPill.sprite = low ? _movesLowSprite : _movesSprite;
        _movesText.color = low ? _movesLowColor : _movesColor;

        switch (game.State)
        {
            case GameState.Won:
                SetMessage(_panelWinSprite, WonTitle, WonBody);
                break;
            case GameState.Failed:
                SetMessage(_panelFailSprite, FailedTitle, FailedBody);
                break;
            default:
                SetMessage(_panelNeutralSprite, PlayingTitle,
                           string.IsNullOrEmpty(level.Instruction) ? PlayingBody : level.Instruction);
                break;
        }

        // Next level only after a win. Hiding it collapses the bottom stack (VerticalLayoutGroup).
        _nextButton.gameObject.SetActive(game.State == GameState.Won);
        _undoButton.interactable = game.CanUndo;
    }

    private void SetMessage(Sprite panel, string title, string body)
    {
        _messagePanel.sprite = panel;
        _messageTitle.text = title;
        _messageBody.text = body;
    }

    // The tap plays after switching, so it is heard when turning sound on and not when turning it off.
    private void ToggleSound()
    {
        var audio = Manager_Audio.Instance;
        if (audio == null) return;
        audio.ToggleSound();
        audio.PlaySound(SoundType.UiTap);
        Haptics.Play(HapticType.Light);
    }

    private void HandleSoundChanged(bool on) => _soundIcon.sprite = on ? _soundOnSprite : _soundOffSprite;
}
