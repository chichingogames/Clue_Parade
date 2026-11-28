using System;
using System.Collections.Generic;
using Chichingo.TilewrapMystery;
using DG.Tweening;
using UnityEngine;

// Owns the board's views for the any-row rules (DeliveryRules): builds the 3-column board (3 or 4
// rows) from Cell / ItemTile prefabs, drags a pair of neighbours, and plays a move's steps in order:
// the swap, then each delivery (the row's tiles leave for the subjects: the HUD flies them,
// OnDelivering), the fall and the refill (new tiles drop in from above the top row). Board-local
// space: centered on _boardRoot, row 0 at the top. The board keeps its 870 x 870 px area: cells are
// 290 px wide and 870 / rows tall. Cell index i = row * 3 + col. An empty cell has no tile.
//
// Marks (all derived; RefreshMarks): the items a tapped clue names (Focus), a hinted pair (Hint),
// the tap-selected tile (Selected); case 1's first swap mutes every other tile. Nothing tints an
// item for standing under its right owner (no partial-correctness feedback). One row zone per row
// (copies of _activeRow) lights the hinted row. The guide (Board/Guide) shows a swap to make. The
// first time a case is seen its tiles settle in like slot reels (cosmetic: the start is authored).
public class Manager_Board : Singleton<Manager_Board>
{
    [SerializeField] private Transform _boardRoot;
    [SerializeField] private SpriteRenderer _frame;
    [SerializeField] private SpriteMask _mask;
    [Tooltip("Row zone template: cloned into one zone per row at Build.")]
    [SerializeField] private SpriteRenderer _activeRow;
    [Tooltip("Full-screen booth art behind everything; FitCamera scales it to cover the view.")]
    [SerializeField] private SpriteRenderer _background;
    [Tooltip("The pack's animated background (its own camera-space canvas behind the board). While set, the static _background is hidden.")]
    [SerializeField] private TilewrapAmbientBackground _ambient;
    [SerializeField] private Transform _cellsRoot;
    [SerializeField] private Transform _tilesRoot;
    [Tooltip("Board/Dividers: grid lines, the first half vertical, the rest horizontal (N - 1 each).")]
    [SerializeField] private SpriteRenderer[] _dividers = new SpriteRenderer[0];
    [Tooltip("Board/Guide: band, arrow and hand showing a swap.")]
    [SerializeField] private BoardGuide _guide;

    // Timings of a move's steps (the playtest's: swap 245 ms, flight 460 + 240, fall 310 / 460 with a refill).
    public const float FlightSeconds = 0.46f;
    private const float DeliverPause = 0.24f;
    private const float FallSeconds = 0.31f;
    private const float RefillSeconds = 0.46f;

    private static readonly Color ZoneHint = new Color32(0xFF, 0xCA, 0x35, 0xE0);
    private static readonly Color RefillFlash = new Color32(0x9E, 0xD8, 0xCF, 0xB0);

    // A delivery is about to leave the board: its event and each column's tile (sprite, world position).
    public event Action<DeliveryEvent, Sprite[], Vector3[]> OnDelivering = delegate { };
    // A delivery has landed on the subjects (after FlightSeconds).
    public event Action<DeliveryEvent> OnDelivered = delegate { };
    // A step of a move is on screen (its View is the state to show).
    public event Action<DeliveryEvent> OnStepShown = delegate { };
    // Raised when a move (or a snap-back) has finished animating and the marks are up to date.
    public event Action OnBoardSettled = delegate { };
    // Raised when a pair is picked up (a player's drag, or a swap from code).
    public event Action OnTilesPickedUp = delegate { };

    public DeliveryCase Level { get; private set; }
    // The board on screen (row-major item ids, null = empty): the model's board between moves.
    public string[] Board { get; private set; }
    public bool HasBoard => Board != null;
    public bool IsDragging => _held && _animation == null;
    // True while a move plays or the launch intro scales the board in; presses are refused meanwhile.
    public bool IsAnimating => _animation != null || _intro != null || _playing || _settling != null;
    public float DragOffset => _offset;

    private Cell[] _cells;
    private ItemTile[] _tiles = new ItemTile[9];
    private SpriteRenderer[] _zones = new SpriteRenderer[0];
    private readonly List<SpriteRenderer> _extraDividers = new List<SpriteRenderer>();

    // Held pair: _heldA started in cell _a, _heldB in _b (either may be null: an empty cell).
    private bool _held;
    private int _a, _b;
    private ItemTile _heldA, _heldB;
    private float _offset;
    private Tween _animation;
    private bool _playing;
    private int _deliveriesThisMove;      // cascade depth: picks match_1 / 2 / 3

    // Marks set by the flow.
    private int _selected = -1;
    private readonly HashSet<string> _focusItems = new HashSet<string>();
    private int _hintA = -1, _hintB = -1, _hintRow = -1;
    private int _keepA = -1, _keepB = -1;     // case 1: every other tile is muted

    // Launch intro: the whole board scales up once, on the first Build.
    private Tween _intro;
    private bool _introPlayed;
    // First look at a case: the slot-style settle.
    private Tween _settling;

    private const int Size = 3;                 // columns
    private int _rows = 3;

    // The board area stays 3 x 3 tiles of the prefab's size; 4 rows make the cells shorter.
    private Vector2 GridSize => FileRef.Instance.ItemTilePrefab.Size * Size;
    public Vector2 CellSize => new Vector2(GridSize.x / Size, GridSize.y / _rows);
    public int Rows => _rows;

    // caseNumber / resumed: the slot settle plays the first time a case is opened fresh.
    public void Build(DeliveryCase level, string[] board, int caseNumber = 0, bool resumed = false)
    {
        Clear();
        Level = level;
        Board = (string[])board.Clone();
        _rows = level.rows;
        _tiles = new ItemTile[board.Length];
        var fileRef = FileRef.Instance;
        _boardRoot.localPosition = ReferenceLayout.BoardCenter;
        if (_ambient != null)
        {
            _ambient.SetChapter(level.chapterId);
            if (_background != null) _background.enabled = false;
        }

        // The frame is 9-sliced: sized, not scaled, so its corners stay sharp. The mask (a 1-unit
        // sprite) is scaled to the grid.
        _frame.drawMode = SpriteDrawMode.Sliced;
        _frame.transform.localScale = Vector3.one;
        _frame.size = GridSize + 2f * ReferenceLayout.FramePadding;
        _mask.transform.localScale = new Vector3(GridSize.x, GridSize.y, 1f);
        BuildZones();
        PlaceDividers();

        _cells = new Cell[board.Length];
        for (int i = 0; i < _cells.Length; i++)
        {
            var cell = Instantiate(fileRef.CellPrefab, _cellsRoot);
            cell.transform.localPosition = CellPosition(i / Size, i % Size);
            cell.Setup(i / Size, i % Size);
            _cells[i] = cell;
            if (Board[i] != null) _tiles[i] = SpawnTile(Board[i], CellPosition(i / Size, i % Size));
        }

        FitCamera();
        if (!_introPlayed) PlayIntro();
        var seen = OfflineData.Instance.SeenCases;
        if (!resumed && caseNumber > 0 && !seen.Contains(caseNumber))
        {
            seen.Add(caseNumber);
            OfflineData.Instance.Save();
            PlaySettle();
        }
        else AnimateTileSpawn();
        RefreshMarks();
    }

    // Puts the views on `board` at once (Undo): tiles kept by item, missing ones made, others removed.
    public void ShowBoard(string[] board)
    {
        _animation?.Kill();
        ReleaseHeld();
        _playing = false;
        var byId = new Dictionary<string, ItemTile>();
        foreach (var tile in _tiles) if (tile != null) byId[tile.Id] = tile;
        for (int i = 0; i < _tiles.Length; i++)
        {
            _tiles[i] = null;
            var id = board[i];
            if (id == null) continue;
            if (byId.TryGetValue(id, out var tile)) byId.Remove(id);
            else tile = SpawnTile(id, Vector3.zero);
            tile.transform.DOKill();
            tile.transform.localScale = Vector3.one;
            tile.transform.localPosition = CellPosition(i / Size, i % Size);
            _tiles[i] = tile;
        }
        foreach (var leftover in byId.Values) Destroy(leftover.gameObject);
        Board = (string[])board.Clone();
        RefreshMarks();
    }

    // ---------- Marks ----------

    // The tap-selected cell, and case 1's two cells to swap (others muted; -1 = none).
    public void SetMarks(int selected, int keepA = -1, int keepB = -1)
    {
        _selected = selected;
        _keepA = keepA;
        _keepB = keepB;
        RefreshMarks();
    }

    // The items a tapped clue names (empty = none).
    public void FocusItems(IEnumerable<string> itemIds)
    {
        _focusItems.Clear();
        if (itemIds != null) foreach (var id in itemIds) _focusItems.Add(id);
        RefreshMarks();
    }

    // A hinted swap: both cells pulse, the target row glows and the guide shows the swipe.
    public void ShowHint(int from, int to, int targetRow)
    {
        _hintA = from;
        _hintB = to;
        _hintRow = targetRow;
        ShowGuide(from, to);
        RefreshMarks();
    }

    public void ClearHint()
    {
        _hintA = _hintB = _hintRow = -1;
        _focusItems.Clear();
        HideGuide();
        RefreshMarks();
    }

    private void RefreshMarks()
    {
        if (Board == null) return;
        for (int i = 0; i < _tiles.Length; i++)
        {
            var tile = _tiles[i];
            if (tile == null) continue;
            var look = TileLook.None;
            if (_focusItems.Contains(tile.Id)) look = TileLook.Focus;
            if (i == _hintA || i == _hintB) look = TileLook.Hint;
            if (i == _selected) look = TileLook.Selected;
            tile.SetLook(look);
            tile.SetMuted(_keepA >= 0 && i != _keepA && i != _keepB);
        }
        for (int r = 0; r < _zones.Length; r++)
        {
            _zones[r].color = ZoneHint;
            _zones[r].gameObject.SetActive(r == _hintRow);
        }
    }

    // ---------- Dragging ----------

    // Picks up cells a and b (neighbours): dragging slides a's tile towards b and b's the other way.
    public void BeginDrag(int a, int b)
    {
        if (_held || Board == null) return;
        _held = true;
        _a = a;
        _b = b;
        _offset = 0f;
        _heldA = _tiles[a];
        _heldB = _tiles[b];
        RenderHeld();
        OnTilesPickedUp();
    }

    // How far the two cells have traded places: 0 = where they started, 1 = swapped.
    public void SetDragOffset(float offset)
    {
        if (!IsDragging) return;
        _offset = Mathf.Clamp01(offset);
        RenderHeld();
    }

    // Slide the pair back to where it started; the board is unchanged.
    public void CancelDrag()
    {
        if (!IsDragging) return;
        _animation = DOTween.To(() => _offset, x => { _offset = x; RenderHeld(); }, 0f, Tuning.ShiftDuration)
            .SetEase(Ease.OutCubic).SetLink(gameObject)
            .OnComplete(() =>
            {
                ReleaseHeld();
                RefreshMarks();
                OnBoardSettled();
            });
    }

    // Plays a move: the held pair finishes its swap, then the rest of `events` (deliveries, falls).
    // onDone runs once the board is still again.
    public void PlayMove(List<DeliveryEvent> events, Action onDone)
    {
        if (!_held || events.Count == 0 || events[0].Type != DeliveryEventType.Swap) { onDone?.Invoke(); return; }
        _playing = true;
        _deliveriesThisMove = 0;
        float duration = Tuning.ShiftDuration * Mathf.Clamp((1f - _offset) * 1.5f, 0.35f, 1.5f);
        _animation = DOTween.To(() => _offset, x => { _offset = x; RenderHeld(); }, 1f, duration)
            .SetEase(Ease.OutCubic).SetLink(gameObject)
            .OnComplete(() =>
            {
                _tiles[_a] = _heldB;
                _tiles[_b] = _heldA;
                SnapTile(_a);
                SnapTile(_b);
                ReleaseHeld();
                _animation = null;
                Board = (string[])events[0].Board.Clone();
                Manager_Audio.Instance?.PlaySound(SoundType.SwipeSettle);
                Haptics.Play(HapticType.Light);
                OnStepShown(events[0]);
                RefreshMarks();
                PlayStep(events, 1, onDone);
            });
    }

    private void PlayStep(List<DeliveryEvent> events, int index, Action onDone)
    {
        if (index >= events.Count)
        {
            _playing = false;
            RefreshMarks();
            OnBoardSettled();
            onDone?.Invoke();
            return;
        }
        var e = events[index];
        if (e.Type == DeliveryEventType.Deliver) PlayDelivery(e, () => PlayStep(events, index + 1, onDone));
        else if (e.Type == DeliveryEventType.Fall) PlayFall(e, () => PlayStep(events, index + 1, onDone));
        else PlayStep(events, index + 1, onDone);
    }

    // The row's tiles leave the board (the HUD flies their pictures to the subjects).
    private void PlayDelivery(DeliveryEvent e, Action next)
    {
        var sprites = new Sprite[Size];
        var positions = new Vector3[Size];
        for (int c = 0; c < Size; c++)
        {
            int i = e.Row * Size + c;
            var tile = _tiles[i];
            positions[c] = _boardRoot.TransformPoint(CellPosition(e.Row, c) + (Vector3)ReferenceLayout.ItemArtOffset);
            sprites[c] = tile != null ? tile.ItemSprite : null;
            Celebrate(e.Row, c);
            if (tile != null) Destroy(tile.gameObject);
            _tiles[i] = null;
        }
        Board = (string[])e.Board.Clone();
        bool won = e.View.Stage == Level.sets.Length;     // the last set (the snapshot is taken before the status turns Won)
        var audio = Manager_Audio.Instance;
        _deliveriesThisMove++;
        if (won) audio?.PlaySound(Level.IsFinale ? SoundType.CaseClosed : SoundType.LevelComplete);
        else audio?.PlaySound(SoundType.TileMatch, Mathf.Min(_deliveriesThisMove, 3) - 1);
        Haptics.Play(won ? HapticType.Heavy : HapticType.Medium);
        OnDelivering(e, sprites, positions);
        OnStepShown(e);
        RefreshMarks();
        _animation = DOVirtual.DelayedCall(FlightSeconds, () =>
        {
            OnDelivered(e);
            _animation = DOVirtual.DelayedCall(DeliverPause, () => { _animation = null; next(); }).SetLink(gameObject);
        }).SetLink(gameObject);
    }

    // Survivors slide down their columns; refill tiles drop in from above the top row.
    private void PlayFall(DeliveryEvent e, Action next)
    {
        var byId = new Dictionary<string, ItemTile>();
        foreach (var tile in _tiles) if (tile != null) byId[tile.Id] = tile;
        float duration = e.Spawned.Length > 0 ? RefillSeconds : FallSeconds;
        if (e.Spawned.Length > 0) Manager_Audio.Instance?.PlaySound(SoundType.Refill);
        var spawned = new HashSet<string>(e.Spawned);
        for (int i = 0; i < _tiles.Length; i++)
        {
            _tiles[i] = null;
            var id = e.Board[i];
            if (id == null) continue;
            if (!byId.TryGetValue(id, out var tile))
            {
                tile = SpawnTile(id, CellPosition(-1, i % Size));
                if (spawned.Contains(id)) tile.Flash(RefillFlash, 0.9f);
            }
            _tiles[i] = tile;
            tile.transform.DOKill();
            tile.transform.DOLocalMove(CellPosition(i / Size, i % Size), duration).SetEase(Ease.OutBounce).SetLink(tile.gameObject);
        }
        Board = (string[])e.Board.Clone();
        OnStepShown(e);
        RefreshMarks();
        _animation = DOVirtual.DelayedCall(duration, () => { _animation = null; next(); }).SetLink(gameObject);
    }

    private void RenderHeld()
    {
        Vector3 a = CellPosition(_a / Size, _a % Size) + Vector3.back * 0.01f;
        Vector3 b = CellPosition(_b / Size, _b % Size) + Vector3.back * 0.01f;
        if (_heldA != null) _heldA.transform.localPosition = Vector3.Lerp(a, b, _offset);
        if (_heldB != null) _heldB.transform.localPosition = Vector3.Lerp(b, a, _offset);
    }

    private void SnapTile(int i)
    {
        if (_tiles[i] != null) _tiles[i].transform.localPosition = CellPosition(i / Size, i % Size);
    }

    private void ReleaseHeld()
    {
        if (_held)
        {
            // A snap-back or an interrupted drag: back into their own cells.
            if (_heldA != null && _tiles[_a] == _heldA) SnapTile(_a);
            if (_heldB != null && _tiles[_b] == _heldB) SnapTile(_b);
        }
        _held = false;
        _heldA = _heldB = null;
        _offset = 0f;
        _animation = null;
    }

    // A delivered cell: the shared ring growing and fading, and 2-4 stars rising from it
    // (ui_layout.json timings: ring 0.5, stars 0.65 s).
    private const float RingSeconds = 0.5f;
    private const float StarSeconds = 0.65f;

    private void Celebrate(int r, int c)
    {
        var ring = FileRef.Instance.Sprite("fx_ring");
        var star = FileRef.Instance.Sprite("fx_star");
        Vector3 origin = CellPosition(r, c);
        if (ring != null)
        {
            var fx = Effect("Ring", ring, origin, CellSize.x * 0.55f);
            var target = fx.transform.localScale * 1.7f;
            DOTween.Sequence()
                .Append(fx.transform.DOScale(target, RingSeconds).SetEase(Ease.OutCubic))
                .Join(fx.DOFade(0f, RingSeconds).SetEase(Ease.InQuad))
                .OnComplete(() => Destroy(fx.gameObject))
                .SetLink(fx.gameObject);
        }
        if (star == null) return;
        int stars = 2 + (r * Size + c) % 3;      // 2..4, varied but the same every time
        for (int i = 0; i < stars; i++)
        {
            float angle = (90f + (i - (stars - 1) * 0.5f) * 38f) * Mathf.Deg2Rad;
            var fx = Effect("Star", star, origin, CellSize.x * 0.16f);
            var target = origin + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * CellSize.x * 0.42f;
            DOTween.Sequence()
                .Append(fx.transform.DOLocalMove(target, StarSeconds).SetEase(Ease.OutQuad))
                .Join(fx.transform.DOLocalRotate(new Vector3(0f, 0f, 120f), StarSeconds))
                .Join(fx.DOFade(0f, StarSeconds).SetEase(Ease.InQuad))
                .OnComplete(() => Destroy(fx.gameObject))
                .SetLink(fx.gameObject);
        }
    }

    // A one-shot effect sprite `size` units wide over the cells (destroyed by its own tween).
    private SpriteRenderer Effect(string effectName, Sprite sprite, Vector3 localPosition, float size)
    {
        var fx = new GameObject(effectName).AddComponent<SpriteRenderer>();
        fx.transform.SetParent(_cellsRoot, false);
        fx.transform.localPosition = localPosition;
        fx.transform.localScale = Vector3.one * (size / sprite.bounds.size.x);
        fx.sprite = sprite;
        fx.sortingOrder = SortOrder.Particles;
        return fx;
    }

    // ---------- Queries ----------

    // The cell under a screen point (index row * 3 + col), or -1 off the board.
    public int CellAt(Vector2 screenPosition)
    {
        if (Board == null || !TryScreenToLocal(screenPosition, out var local)) return -1;
        Vector2 half = GridSize * 0.5f;
        int col = Mathf.FloorToInt((local.x + half.x) / CellSize.x);
        int row = Mathf.FloorToInt((half.y - local.y) / CellSize.y);
        return row >= 0 && row < _rows && col >= 0 && col < Size ? row * Size + col : -1;
    }

    // Screen-space drag converted to board cells (x right, y up).
    public Vector2 ScreenDeltaToCells(Vector2 screenFrom, Vector2 screenTo)
    {
        if (!TryScreenToLocal(screenFrom, out var from) || !TryScreenToLocal(screenTo, out var to)) return Vector2.zero;
        Vector2 delta = to - from;
        return new Vector2(delta.x / CellSize.x, delta.y / CellSize.y);
    }

    private bool TryScreenToLocal(Vector2 screenPosition, out Vector2 local)
    {
        local = default;
        var cam = Camera.main;
        if (cam == null) return false;
        local = _boardRoot.InverseTransformPoint(cam.ScreenToWorldPoint(screenPosition));
        return true;
    }

    // Overlays that sit on the board parent here, so they follow its layout and intro.
    public Transform BoardRoot => _boardRoot;

    // Accepts fractional and out-of-board indices (used while tiles move). Board-local.
    public Vector3 CellPosition(float row, float col) =>
        new Vector3((col - (Size - 1) * 0.5f) * CellSize.x, ((_rows - 1) * 0.5f - row) * CellSize.y, 0f);

    // A row's centre in reference pixels below the top of the board (the HUD's banner).
    public float RowCenterPixels(int row) => (row + 0.5f) * CellSize.y * 100f;

    // ---------- Views ----------

    private ItemTile SpawnTile(string id, Vector3 localPosition)
    {
        var tile = Instantiate(FileRef.Instance.ItemTilePrefab, _tilesRoot);
        tile.transform.localPosition = localPosition;
        var item = Level.Item(id);
        tile.Setup(id, FileRef.Instance.Sprite(item?.sprite), item?.label ?? id);
        tile.SetCellSize(CellSize);
        return tile;
    }

    private void BuildZones()
    {
        foreach (var zone in _zones) if (zone != null && zone != _activeRow) Destroy(zone.gameObject);
        _activeRow.drawMode = SpriteDrawMode.Sliced;
        _activeRow.size = new Vector2(GridSize.x, CellSize.y) - Vector2.one * 0.08f;
        _zones = new SpriteRenderer[_rows];
        for (int r = 0; r < _rows; r++)
        {
            var zone = r == 0 ? _activeRow : Instantiate(_activeRow, _activeRow.transform.parent);
            zone.name = r == 0 ? _activeRow.name : $"Row Zone {r + 1}";
            zone.transform.localPosition = CellPosition(r, (Size - 1) * 0.5f);
            zone.gameObject.SetActive(false);
            _zones[r] = zone;
        }
    }

    // ---------- Guide ----------

    // Shows the swap from -> to on the board (band over its two cells, arrow, sliding hand) until HideGuide.
    public void ShowGuide(int from, int to)
    {
        if (_guide == null || Board == null) return;
        Vector3 a = CellPosition(from / Size, from % Size), b = CellPosition(to / Size, to % Size);
        bool horizontal = from / Size == to / Size;
        Vector2 band = horizontal ? new Vector2(CellSize.x * 2f, CellSize.y) : new Vector2(CellSize.x, CellSize.y * 2f);
        Vector2 direction = ((Vector2)(b - a)).normalized;
        _guide.Show((a + b) * 0.5f, band - Vector2.one * 0.08f, direction, (horizontal ? CellSize.x : CellSize.y) * 1.2f);
    }

    public void HideGuide()
    {
        if (_guide != null) _guide.Hide();
    }

    // Columns - 1 vertical lines and rows - 1 horizontal ones; the scene's dividers are reused and
    // copied when a 4-row board needs more.
    private void PlaceDividers()
    {
        foreach (var extra in _extraDividers) if (extra != null) Destroy(extra.gameObject);
        _extraDividers.Clear();
        if (_dividers.Length == 0) return;
        var lines = new List<(bool vertical, int k)>();
        for (int k = 0; k < Size - 1; k++) lines.Add((true, k));
        for (int k = 0; k < _rows - 1; k++) lines.Add((false, k));
        for (int i = 0; i < Mathf.Max(lines.Count, _dividers.Length); i++)
        {
            SpriteRenderer divider;
            if (i < _dividers.Length) divider = _dividers[i];
            else
            {
                divider = Instantiate(_dividers[0], _dividers[0].transform.parent);
                divider.name = $"Divider {i + 1}";
                _extraDividers.Add(divider);
            }
            divider.gameObject.SetActive(i < lines.Count);
            if (i >= lines.Count) continue;
            var (vertical, k) = lines[i];
            divider.transform.localPosition = vertical
                ? new Vector3((k + 1 - Size * 0.5f) * CellSize.x, 0f, 0f)
                : new Vector3(0f, (_rows * 0.5f - (k + 1)) * CellSize.y, 0f);
            divider.transform.localScale = vertical
                ? new Vector3(ReferenceLayout.DividerWidth, GridSize.y, 1f)
                : new Vector3(GridSize.x, ReferenceLayout.DividerWidth, 1f);
        }
    }

    // ---------- Intro ----------

    // Scales _boardRoot from 0 to its scene scale (DOTween, OutBack), in step with UI_InGame's HUD
    // intro. Runs after FitCamera.
    private void PlayIntro()
    {
        _introPlayed = true;
        var target = _boardRoot.localScale;
        _boardRoot.localScale = Vector3.zero;
        _intro = _boardRoot.DOScale(target, Tuning.BoardIntroDuration)
            .SetDelay(Tuning.BoardIntroDelay)
            .SetEase(Ease.OutBack)
            .SetLink(gameObject)
            .OnComplete(() => { _intro = null; OnBoardSettled(); });
    }

    // First look at a case: every tile drops in from above the board like a slot reel settling,
    // column by column, overshooting a little. Cosmetic: the start board is authored, no move is
    // used. Presses wait until it has settled.
    private void PlaySettle()
    {
        float start = _intro != null ? Tuning.BoardIntroDelay + Tuning.BoardIntroDuration * 0.5f : 0f;
        var sequence = DOTween.Sequence().SetLink(gameObject);
        for (int i = 0; i < _tiles.Length; i++)
        {
            if (_tiles[i] == null) continue;
            var tile = _tiles[i].transform;
            int row = i / Size, col = i % Size;
            Vector3 target = CellPosition(row, col);
            tile.localPosition = CellPosition(row - _rows - 0.4f, col);
            sequence.Insert(start + col * 0.12f + i * 0.008f, tile.DOLocalMove(target, 0.68f).SetEase(Ease.OutBack, 1.1f));
        }
        _settling = sequence.OnComplete(() => { _settling = null; OnBoardSettled(); }).OnKill(() => _settling = null);
    }

    // Every Build: the tiles scale up from 0 in a diagonal wave from the top-left (row + col steps of
    // TileSpawnStagger). On launch the wave waits until the board is half grown.
    private void AnimateTileSpawn()
    {
        float start = _intro != null ? Tuning.BoardIntroDelay + Tuning.BoardIntroDuration * 0.5f : 0f;
        for (int i = 0; i < _tiles.Length; i++)
        {
            if (_tiles[i] == null) continue;
            var tile = _tiles[i].transform;
            var target = tile.localScale;
            tile.localScale = Vector3.zero;
            tile.DOScale(target, Tuning.TileSpawnDuration)
                .SetDelay(start + (i / Size + i % Size) * Tuning.TileSpawnStagger)
                .SetEase(Ease.OutBack)
                .SetLink(tile.gameObject);
        }
    }

    // ---------- Layout ----------

    // Shows the 10.8 x 19.2 reference screen, centred on the world origin, as large as it fits in
    // the safe area (uniform scale), with the board at its reference place inside it.
    private void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Rect safe = Screen.safeArea;
        if (safe.width <= 0f || safe.height <= 0f) safe = new Rect(0f, 0f, Screen.width, Screen.height);

        float unitsPerPixel = Mathf.Max(ReferenceLayout.Width / safe.width, ReferenceLayout.Height / safe.height);
        cam.orthographicSize = unitsPerPixel * Screen.height * 0.5f;

        // Put the world origin (the reference screen's centre) at the safe area's centre.
        Vector2 offset = (safe.center - new Vector2(Screen.width, Screen.height) * 0.5f) * unitsPerPixel;
        cam.transform.position = new Vector3(-offset.x, -offset.y, cam.transform.position.z);

        // The background stays centred on the reference screen and grows to cover the whole view,
        // notch and letterbox areas included.
        if (_background != null && _background.sprite != null)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector2 reach = new Vector2(Mathf.Abs(cam.transform.position.x) + halfWidth, Mathf.Abs(cam.transform.position.y) + halfHeight);
            Vector2 art = _background.sprite.bounds.size * 0.5f;
            float scale = Mathf.Max(1f, Mathf.Max(reach.x / art.x, reach.y / art.y));
            _background.transform.position = Vector3.zero;
            _background.transform.localScale = new Vector3(scale, scale, 1f);
        }
    }

    private void Clear()
    {
        _animation?.Kill();
        _intro?.Complete();     // a level change mid-intro jumps to full size
        ReleaseHeld();
        _playing = false;
        _settling?.Kill();
        _settling = null;
        _selected = _hintA = _hintB = _hintRow = _keepA = _keepB = -1;
        _focusItems.Clear();
        HideGuide();
        for (int i = 0; i < _tiles.Length; i++) _tiles[i] = null;
        DestroyChildren(_cellsRoot);
        DestroyChildren(_tilesRoot);
    }

    private static void DestroyChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
    }
}
