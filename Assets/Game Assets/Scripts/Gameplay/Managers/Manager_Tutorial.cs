using System;
using DG.Tweening;
using UnityEngine;

// The tutorial levels (1-4, TutorialLessons): lesson text for the HUD, the on-board cues of a guided
// swipe, and the rule that a guided level only accepts the swipe it demonstrates.
//
// A level is guided when its file has "tutorial": "row" / "column"; the demonstrated swipe is the
// first move of its "solution". Cues (board-local, under BoardRoot/Tutorial, rebuilt every level):
// an outline on the line and on the tile that wraps, a dashed outline where it lands, an arrow
// past the edge, outlines on the two targets that tile will match, and a looping hand.
public class Manager_Tutorial : Singleton<Manager_Tutorial>
{
    // The lesson text or the confirmation changed; UI_InGame refreshes.
    public event Action OnChanged = delegate { };
    // A lesson with PulseMoves just spent its first move; UI_InGame pulses the moves pill.
    public event Action OnPulseMoves = delegate { };

    public TutorialLesson Lesson { get; private set; }
    public bool IsGuided => _guided;
    // Guided level just won: the confirmation message shows instead of the win card for a moment.
    public bool IsConfirming { get; private set; }

    // Hand sprite: where the fingertip is, from the bottom-left, as a fraction of the sprite (art manifest).
    private static readonly Vector2 HandContact = new Vector2(0.35f, 0.767f);
    private const float HandSize = 0.8f;          // cells
    private const float ArrowSize = 0.36f;        // cells
    private const float OutlinePad = 0.06f;       // cells added around an outlined line or tile
    private const float MarkerOutlineSize = 0.92f;

    private bool _guided;
    private bool _guideIsRow;
    private int _guideLine;
    private int _guideDirection;                  // +1 = right / down
    private int _lastMovesUsed;

    private Transform _root;
    private SpriteRenderer _hand;
    private Sequence _handLoop;
    private Tween _targetPulse;
    private Tween _idleHint;
    private Tween _confirmation;

    public override void Awake2()
    {
        var game = Manager_Game.Instance;
        game.OnLevelStarted += HandleLevelStarted;
        game.OnStateChanged += HandleStateChanged;
        game.OnMoveMade += HandleMoveMade;
        var board = Manager_Board.Instance;
        board.OnLinePickedUp += HandleLinePickedUp;
        board.OnBoardSettled += HandleBoardSettled;
    }

    protected override void OnDestroy()
    {
        var game = Manager_Game.Instance;
        if (game != null)
        {
            game.OnLevelStarted -= HandleLevelStarted;
            game.OnStateChanged -= HandleStateChanged;
            game.OnMoveMade -= HandleMoveMade;
        }
        var board = Manager_Board.Instance;
        if (board != null)
        {
            board.OnLinePickedUp -= HandleLinePickedUp;
            board.OnBoardSettled -= HandleBoardSettled;
        }
        base.OnDestroy();
    }

    // ---------- Rules (asked by Manager_Game for player swipes) ----------

    // A guided level that has not had its swipe yet only lets the guided line be picked up...
    public bool AllowsLine(bool isRow, int index) =>
        !GuideActive() || (isRow == _guideIsRow && index == _guideLine);

    // ...and only commits it in the shown direction (anything else snaps back).
    public bool AllowsMove(bool isRow, int index, int steps) =>
        AllowsLine(isRow, index) && (!GuideActive() || Math.Sign(steps) == _guideDirection);

    private bool GuideActive() => _guided && Manager_Game.Instance.MovesUsed == 0;

    // ---------- HUD text ----------

    // False when the HUD should show its own text (no lesson, a win card, out of moves).
    public bool TryGetMessage(out string title, out string body)
    {
        title = body = null;
        var game = Manager_Game.Instance;
        if (Lesson == null) return false;

        bool moved = game.MovesUsed > 0 && Lesson.MovedTitle != null;
        if (game.State == GameState.Won && !IsConfirming) return false;
        if (game.State == GameState.Failed) return false;

        title = moved ? Format(Lesson.MovedTitle) : Lesson.Title;
        body = moved ? Format(Lesson.MovedBody) : Lesson.Body;
        return true;
    }

    private static string Format(string text)
    {
        var game = Manager_Game.Instance;
        return text.Replace("{used}", game.MovesUsed == 1 ? "1 move" : $"{game.MovesUsed} moves")
                   .Replace("{left}", game.MovesLeft.ToString());
    }

    // ---------- Events ----------

    private void HandleLevelStarted(int levelNumber)
    {
        ClearCues();
        _idleHint?.Kill();
        _confirmation?.Kill();
        IsConfirming = false;
        _lastMovesUsed = 0;

        var level = Manager_Game.Instance.Level;
        Lesson = TutorialLessons.For(levelNumber);
        _guided = Lesson != null && level.Solution.Length > 0
                  && (level.Tutorial == "row" || level.Tutorial == "column");
        if (_guided)
        {
            var move = level.Solution[0];
            _guideIsRow = level.Tutorial == "row";
            _guideLine = move.line;
            _guideDirection = Math.Sign(move.direction);
            BuildCues(level.Size);
        }

        RestartIdleHint();
        UpdateCues();
        OnChanged();
    }

    private void HandleStateChanged(GameState state)
    {
        // Guided level: the confirmation replaces the win card until the board settles + Tuning.TutorialConfirmation.
        if (state == GameState.Won && _guided)
        {
            IsConfirming = true;
            _confirmation?.Kill();
        }
        else if (state != GameState.Won && IsConfirming)
        {
            IsConfirming = false;
            _confirmation?.Kill();
        }
        if (state != GameState.Playing) _idleHint?.Kill();
        UpdateCues();
        OnChanged();
    }

    private void HandleMoveMade()
    {
        var game = Manager_Game.Instance;
        if (Lesson != null && Lesson.PulseMoves && _lastMovesUsed == 0 && game.MovesUsed == 1) OnPulseMoves();
        _lastMovesUsed = game.MovesUsed;
        RestartIdleHint();
        UpdateCues();
        OnChanged();
    }

    private void HandleLinePickedUp() => StopHand();

    private void HandleBoardSettled()
    {
        if (IsConfirming && _confirmation == null)
        {
            _confirmation = DOVirtual.DelayedCall(Tuning.TutorialConfirmation, () =>
            {
                IsConfirming = false;
                _confirmation = null;
                OnChanged();
            }).SetLink(gameObject);
        }
        UpdateCues();
    }

    // ---------- Cues ----------

    // Cues show while the guided swipe has not been made; the hand waits while a line is held
    // (it stops on pick-up and restarts when the line settles back).
    private void UpdateCues()
    {
        if (_root == null) return;
        bool show = GuideActive() && Manager_Game.Instance.State == GameState.Playing;
        _root.gameObject.SetActive(show);
        if (!show || Manager_Board.Instance.IsDragging) StopHand();
        else if (_handLoop == null) StartHand();
    }

    private void BuildCues(int size)
    {
        var board = Manager_Board.Instance;
        var fileRef = FileRef.Instance;
        float cell = Tuning.CellSize;
        float center = (size - 1) * 0.5f;

        _root = new GameObject("Tutorial").transform;
        _root.SetParent(board.BoardRoot, false);

        // The line, the tile that wraps (at the far end in the swipe direction) and where it lands.
        int source = _guideDirection > 0 ? size - 1 : 0;
        int destination = _guideDirection > 0 ? 0 : size - 1;
        int sourceRow = _guideIsRow ? _guideLine : source, sourceCol = _guideIsRow ? source : _guideLine;
        int destRow = _guideIsRow ? _guideLine : destination, destCol = _guideIsRow ? destination : _guideLine;

        var lineCenter = _guideIsRow ? board.CellPosition(_guideLine, center) : board.CellPosition(center, _guideLine);
        var lineSize = _guideIsRow ? new Vector2(size, 1f) : new Vector2(1f, size);
        Sliced("Line Outline", fileRef.TutorialLineOutline, lineCenter, lineSize * cell + Vector2.one * OutlinePad * cell);
        Sliced("Source Outline", fileRef.TutorialLineOutline, board.CellPosition(sourceRow, sourceCol), Vector2.one * (1f - OutlinePad * 2f) * cell);
        Simple("Destination", fileRef.TutorialDestination, board.CellPosition(destRow, destCol), 1f - OutlinePad * 2f, 0f);

        // Arrow just past the board edge, pointing the swipe direction.
        float edge = (size * 0.5f + Tuning.FramePadding) * cell + ArrowSize * 0.5f * cell;
        Vector3 arrowPosition = _guideIsRow
            ? new Vector3(_guideDirection * edge, lineCenter.y)
            : new Vector3(lineCenter.x, -_guideDirection * edge);
        float arrowAngle = _guideIsRow ? (_guideDirection > 0 ? 0f : 180f) : (_guideDirection > 0 ? -90f : 90f);
        Simple("Arrow", fileRef.TutorialArrow, arrowPosition, ArrowSize, arrowAngle);

        // The two targets the wrapped tile will match.
        var rowTarget = Sliced("Row Target Outline", fileRef.TutorialLineOutline,
                               board.BoardRoot.InverseTransformPoint(board.GetRowMarker(destRow).position), Vector2.one * MarkerOutlineSize * cell);
        var columnTarget = Sliced("Column Target Outline", fileRef.TutorialLineOutline,
                                  board.BoardRoot.InverseTransformPoint(board.GetColumnMarker(destCol).position), Vector2.one * MarkerOutlineSize * cell);
        _targetPulse = DOTween.Sequence()
            .Join(rowTarget.DOFade(0.35f, 0.6f))
            .Join(columnTarget.DOFade(0.35f, 0.6f))
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetLink(_root.gameObject);

        _hand = Simple("Hand", fileRef.TutorialHand, Vector3.zero, HandSize, 0f);
        _hand.sortingOrder = SortOrder.TutorialHand;
        _hand.color = Clear(_hand.color);
    }

    // The hand presses the middle of the line half a cell behind its center, drags one cell in
    // the swipe direction, lifts, and waits for the rest of Tuning.TutorialHandLoop.
    private void StartHand()
    {
        var board = Manager_Board.Instance;
        int size = board.State.Size;
        float center = (size - 1) * 0.5f;
        float from = center - 0.5f * _guideDirection, to = center + 0.5f * _guideDirection;
        Vector3 start = _guideIsRow ? board.CellPosition(_guideLine, from) : board.CellPosition(from, _guideLine);
        Vector3 end = _guideIsRow ? board.CellPosition(_guideLine, to) : board.CellPosition(to, _guideLine);

        // The transform is the sprite's center; offset it so the fingertip is on the path.
        Vector3 tip = (Vector3)((new Vector2(0.5f, 0.5f) - HandContact) * HandSize * Tuning.CellSize);
        var t = _hand.transform;
        float loop = Tuning.TutorialHandLoop;

        _handLoop = DOTween.Sequence()
            .AppendCallback(() => { t.localPosition = start + tip; _hand.color = Clear(_hand.color); })
            .Append(_hand.DOFade(1f, loop * 0.15f))
            .Append(t.DOLocalMove(end + tip, loop * 0.45f).SetEase(Ease.InOutCubic))
            .Append(_hand.DOFade(0f, loop * 0.15f))
            .AppendInterval(loop * 0.25f)
            .SetLoops(-1)
            .SetLink(_hand.gameObject);
    }

    private void StopHand()
    {
        _handLoop?.Kill();
        _handLoop = null;
        if (_hand != null) _hand.color = Clear(_hand.color);
    }

    private void ClearCues()
    {
        StopHand();
        _targetPulse?.Kill();
        if (_root != null) Destroy(_root.gameObject);
        _root = null;
        _hand = null;
    }

    // ---------- Hints ----------

    // Lessons with IdleHint: every Tuning.TutorialIdleHint seconds without a move, the target icons pulse.
    private void RestartIdleHint()
    {
        _idleHint?.Kill();
        _idleHint = null;
        if (Lesson == null || !Lesson.IdleHint || Manager_Game.Instance.State != GameState.Playing) return;
        _idleHint = DOVirtual.DelayedCall(Tuning.TutorialIdleHint, () =>
        {
            Manager_Board.Instance.PulseTargets();
            RestartIdleHint();
        }).SetLink(gameObject);
    }

    // ---------- Helpers ----------

    private SpriteRenderer Sliced(string name, Sprite sprite, Vector3 localPosition, Vector2 size)
    {
        var r = Renderer(name, sprite, localPosition);
        r.drawMode = SpriteDrawMode.Sliced;
        r.size = size;
        return r;
    }

    private SpriteRenderer Simple(string name, Sprite sprite, Vector3 localPosition, float cells, float angle)
    {
        var r = Renderer(name, sprite, localPosition);
        r.transform.localScale = Vector3.one * cells * Tuning.CellSize;
        r.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        return r;
    }

    private SpriteRenderer Renderer(string name, Sprite sprite, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        go.transform.localPosition = localPosition;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = SortOrder.TutorialCue;
        return r;
    }

    private static Color Clear(Color c) => new Color(c.r, c.g, c.b, 0f);
}
