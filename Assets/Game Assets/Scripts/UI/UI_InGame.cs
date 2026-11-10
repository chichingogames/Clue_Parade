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
    // Wording from the Tilewrap tutorial pack (Tutorial_Unity_Handoff.md). Tutorial levels (1-4)
    // replace the playing text with their lesson (Manager_Tutorial).
    private const string PlayingTitle = "Match every tile to both targets.";
    private const string PlayingBody = "Shapes match across each row. Colors match down each column.";
    private const string WonTitle = "Level complete!";
    private const string WonBody = "All {0} tiles match both targets.";
    private const string FailedTitle = "Out of moves";
    private const string FailedBody = "Undo to get a move back, or restart.";

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
    [SerializeField, Min(0)] private int _lowMovesAt = 2;
    [Tooltip("White pill-shaped overlay between the moves pill and its text, tinted for the warning. " +
             "Low on moves: breathes white <-> red (text red <-> white). Out of moves: solid red, white text.")]
    [SerializeField] private Image _movesWarning;
    [SerializeField] private Color _movesWarningTextColor = Color.white;

    [Header("Message")]
    [SerializeField] private Image _messagePanel;
    [SerializeField] private TMP_Text _messageTitle;
    [SerializeField] private TMP_Text _messageBody;
    [SerializeField] private Sprite _panelNeutralSprite;
    [SerializeField] private Sprite _panelWinSprite;
    [SerializeField] private Sprite _panelFailSprite;

    [Header("Buttons")]
    [SerializeField] private Button _nextButton;
    [SerializeField] private TMP_Text _nextLabel;
    [SerializeField] private Button _undoButton;
    [SerializeField] private Button _restartButton;

    [Header("Intro")]
    [Tooltip("Scaled up from 0 one after another when the game opens, in this order. Timing: GameTuning > UI intro.")]
    [SerializeField] private RectTransform[] _introItems = new RectTransform[0];

    private bool _introPlayed;

    private enum MovesWarning { None, Breathing, OutOfMoves }
    private MovesWarning _warning;
    private Tween _breath;

    private void Awake()
    {
        if (_movesWarning != null) _movesWarning.gameObject.SetActive(false);
        // Hidden until the first level opens (after the loading screen), then PlayIntro scales them up.
        foreach (var item in _introItems)
            if (item != null) item.localScale = Vector3.zero;

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

    // Runs once per launch, on the first level start (Manager_Game.Launch, as the loading screen
    // fades), together with the board intro. UI_InGame lives in the only scene and is never
    // reloaded, so moving between levels does not replay it.
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
        if (Manager_Tutorial.Instance != null)
        {
            Manager_Tutorial.Instance.OnChanged += Refresh;
            Manager_Tutorial.Instance.OnPulseMoves += PulseMoves;
        }
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
        if (Manager_Tutorial.Instance != null)
        {
            Manager_Tutorial.Instance.OnChanged -= Refresh;
            Manager_Tutorial.Instance.OnPulseMoves -= PulseMoves;
        }
        if (Manager_Audio.Instance != null) Manager_Audio.Instance.OnSoundChanged -= HandleSoundChanged;
    }

    private void HandleLevelStarted(int levelNumber)
    {
        if (!_introPlayed) PlayIntro();
        Refresh();
    }

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
        _movesText.text = !level.HasMoveLimit ? "No move limit"
                        : game.MovesLeft == 1 ? "1 move left"
                        : $"{game.MovesLeft} moves left";
        _movesPill.sprite = low ? _movesLowSprite : _movesSprite;
        SetMovesWarning(game.State == GameState.Failed ? MovesWarning.OutOfMoves
                      : game.State == GameState.Playing && low ? MovesWarning.Breathing
                      : MovesWarning.None);
        // While the warning shows, it owns the text color.
        if (_warning == MovesWarning.None) _movesText.color = low ? _movesLowColor : _movesColor;

        var tutorial = Manager_Tutorial.Instance;
        bool confirming = tutorial != null && tutorial.IsConfirming;
        if (tutorial != null && tutorial.TryGetMessage(out string lessonTitle, out string lessonBody))
            SetMessage(confirming ? _panelWinSprite : _panelNeutralSprite, lessonTitle, lessonBody);
        else switch (game.State)
        {
            case GameState.Won:
                SetMessage(_panelWinSprite, WonTitle, string.Format(WonBody, board.Size * board.Size));
                break;
            case GameState.Failed:
                SetMessage(_panelFailSprite, FailedTitle, FailedBody);
                break;
            default:
                SetMessage(_panelNeutralSprite, PlayingTitle,
                           string.IsNullOrEmpty(level.Instruction) ? PlayingBody : level.Instruction);
                break;
        }

        // Next level only after a win (and after a guided level's confirmation). Hiding it
        // collapses the bottom stack (VerticalLayoutGroup).
        _nextButton.gameObject.SetActive(game.State == GameState.Won && !confirming);
        if (_nextLabel != null) _nextLabel.text = $"Next level {game.CurrentLevel + 1:00}";
        _undoButton.interactable = game.CanUndo;
    }

    // Low on moves (Playing, MovesLeft <= _lowMovesAt): the warning overlay breathes white <-> red,
    // the text the other way so it stays readable. Out of moves (Failed): the breathing stops on
    // solid red with white text. A win, a restart or more moves left (Undo) hides it.
    private void SetMovesWarning(MovesWarning warning)
    {
        if (_movesWarning == null || warning == _warning) return;
        _warning = warning;
        _breath?.Kill();
        _breath = null;
        _movesWarning.gameObject.SetActive(warning != MovesWarning.None);

        if (warning == MovesWarning.OutOfMoves)
        {
            ApplyMovesWarning(1f);
        }
        else if (warning == MovesWarning.Breathing)
        {
            float t = 0f;
            ApplyMovesWarning(t);
            _breath = DOTween.To(() => t, x => { t = x; ApplyMovesWarning(x); }, 1f, Tuning.MovesWarningBreath)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(_movesWarning.gameObject);
        }
    }

    // t 0 = white fill, red text; t 1 = red fill, white text.
    private void ApplyMovesWarning(float t)
    {
        _movesWarning.color = Color.Lerp(Color.white, _movesLowColor, t);
        _movesText.color = Color.Lerp(_movesLowColor, _movesWarningTextColor, t);
    }

    // First spent move of the move-budget lesson: the moves pill pulses so the count is noticed.
    private void PulseMoves()
    {
        var pill = _movesPill.rectTransform;
        pill.DOComplete();
        pill.DOPunchScale(Vector3.one * 0.12f, Tuning.TutorialMovesPulse, 4, 0.5f).SetLink(pill.gameObject);
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
