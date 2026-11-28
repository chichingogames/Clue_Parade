using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// Owns the board's views for the any-row rules (DeliveryRules): builds the 3x3 board from Cell /
// ItemTile prefabs, drags a pair of neighbours, and plays a move's steps in order: the swap, then
// each delivery (the row's tiles leave for the subjects: the HUD flies them, OnDelivering), the
// fall and the refill (new tiles drop in from above the top row). Board-local space: centered on
// _boardRoot, row 0 at the top, cells CellSize wide and high (2.9 units = the pack's 290 px cells).
// Cell index i = row * 3 + col. An empty cell has no tile.
//
// Marks (all derived; RefreshMarks): tiles of the current set that stand under their owner are
// tinted (Aligned), the focused clue's item (Focus), a hinted pair (Hint), the tap-selected tile
// (Selected); case 1's first swap mutes every other tile. Three row zones (copies of _activeRow)
// show a row two thirds done (gold), complete (green) or hinted (strong gold). The guide
// (Board/Guide) shows a swap to make.
public class Manager_Board : Singleton<Manager_Board>
{
    [SerializeField] private Transform _boardRoot;
    [SerializeField] private SpriteRenderer _frame;
    [SerializeField] private SpriteMask _mask;
    [Tooltip("Row zone template: cloned into one zone per row at Build.")]
    [SerializeField] private SpriteRenderer _activeRow;
    [Tooltip("Full-screen booth art behind everything; FitCamera scales it to cover the view.")]
    [SerializeField] private SpriteRenderer _background;
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

    private static readonly Color ZoneNearly = new Color32(0xEA, 0xC4, 0x60, 0x70);
    private static readonly Color ZoneReady = new Color32(0x9C, 0xC7, 0x5A, 0xC0);
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
    public bool IsAnimating => _animation != null || _intro != null || _playing;
    public float DragOffset => _offset;

    private Cell[] _cells;
    private readonly ItemTile[] _tiles = new ItemTile[9];
    private SpriteRenderer[] _zones = new SpriteRenderer[0];

    // Held pair: _heldA started in cell _a, _heldB in _b (either may be null: an empty cell).
    private bool _held;
    private int _a, _b;
    private ItemTile _heldA, _heldB;
    private float _offset;
    private Tween _animation;
    private bool _playing;

    // Marks set by the flow.
    private DeliverySet _alignSet;
    private int _selected = -1;
    private string _focusItem;
    private int _hintA = -1, _hintB = -1, _hintRow = -1;
    private int _keepA = -1, _keepB = -1;     // case 1: every other tile is muted

    // Launch intro: the whole board scales up once, on the first Build.
    private Tween _intro;
    private bool _introPlayed;

    private const int Size = 3;

    // One cell = one tile, no gap.
    public Vector2 CellSize => FileRef.Instance.ItemTilePrefab.Size;
    private Vector2 GridSize => CellSize * Size;

    public void Build(DeliveryCase level, string[] board)
    {
        Clear();
        Level = level;
        Board = (string[])board.Clone();
        var fileRef = FileRef.Instance;
        _boardRoot.localPosition = ReferenceLayout.BoardCenter;

        // The frame is 9-sliced: sized, not scaled, so its corners stay sharp. The mask (a 1-unit
        // sprite) is scaled to the grid.
        _frame.drawMode = SpriteDrawMode.Sliced;
        _frame.transform.localScale = Vector3.one;
        _frame.size = GridSize + 2f * ReferenceLayout.FramePadding;
        _mask.transform.localScale = new Vector3(GridSize.x, GridSize.y, 1f);
        BuildZones();
        PlaceDividers();

        _cells = new Cell[Size * Size];
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
        AnimateTileSpawn();
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

    // The set whose items are tinted when under their owner (the current request), the
    // tap-selected cell, and case 1's two cells to swap (others muted; -1 = none).
    public void SetMarks(DeliverySet alignSet, int selected, int keepA = -1, int keepB = -1)
    {
        _alignSet = alignSet;
        _selected = selected;
        _keepA = keepA;
        _keepB = keepB;
        RefreshMarks();
    }

    public void FocusItem(string itemId)
    {
        _focusItem = itemId;
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
        _focusItem = null;
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
            if (_alignSet != null && _alignSet.items[i % Size] == tile.Id) look = TileLook.Aligned;
            if (_focusItem != null && tile.Id == _focusItem) look = TileLook.Focus;
            if (i == _hintA || i == _hintB) look = TileLook.Hint;
            if (i == _selected) look = TileLook.Selected;
            tile.SetLook(look);
            tile.SetMuted(_keepA >= 0 && i != _keepA && i != _keepB);
        }
        for (int r = 0; r < _zones.Length; r++)
        {
            int aligned = _alignSet != null ? DeliveryRules.AlignedInRow(Board, r, _alignSet) : 0;
            var color = r == _hintRow ? ZoneHint : aligned == 3 ? ZoneReady : aligned == 2 ? ZoneNearly : Color.clear;
            _zones[r].color = color;
            _zones[r].gameObject.SetActive(color.a > 0f);
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
        bool won = e.View.Status == DeliveryStatus.Won;
        var audio = Manager_Audio.Instance;
        audio?.PlaySound(won ? SoundType.LevelComplete : SoundType.TileMatch, won ? -1 : Mathf.Min(e.View.Stage, 3) - 1);
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
        return row >= 0 && row < Size && col >= 0 && col < Size ? row * Size + col : -1;
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
    public Vector3 CellPosition(float row, float col)
    {
        float offset = (Size - 1) * 0.5f;
        return new Vector3((col - offset) * CellSize.x, (offset - row) * CellSize.y, 0f);
    }

    // ---------- Views ----------

    private ItemTile SpawnTile(string id, Vector3 localPosition)
    {
        var tile = Instantiate(FileRef.Instance.ItemTilePrefab, _tilesRoot);
        tile.transform.localPosition = localPosition;
        var item = Level.Item(id);
        tile.Setup(id, FileRef.Instance.Sprite(item?.sprite), item?.label ?? id);
        return tile;
    }

    private void BuildZones()
    {
        foreach (var zone in _zones) if (zone != null && zone != _activeRow) Destroy(zone.gameObject);
        _activeRow.drawMode = SpriteDrawMode.Sliced;
        _activeRow.size = new Vector2(GridSize.x, CellSize.y) - Vector2.one * 0.08f;
        _zones = new SpriteRenderer[Size];
        for (int r = 0; r < Size; r++)
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

    private void PlaceDividers()
    {
        int perAxis = Size - 1;
        for (int i = 0; i < _dividers.Length; i++)
        {
            var divider = _dividers[i];
            bool vertical = i < _dividers.Length / 2;
            int k = vertical ? i : i - _dividers.Length / 2;
            divider.gameObject.SetActive(k < perAxis);
            if (k >= perAxis) continue;
            float along = (k + 1) - Size * 0.5f;    // cell edges, in cells from the centre
            divider.transform.localPosition = vertical ? new Vector3(along * CellSize.x, 0f, 0f) : new Vector3(0f, -along * CellSize.y, 0f);
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
            .OnComplete(() => _intro = null);
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
        _alignSet = null;
        _selected = _hintA = _hintB = _hintRow = _keepA = _keepB = -1;
        _focusItem = null;
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
