using System;
using DG.Tweening;
using UnityEngine;

// Owns the BoardState and its views: builds the board from Cell / ItemTile prefabs, drags and
// swaps tiles, and animates. Board-local space: centered on _boardRoot, row 0 at the top, cells
// CellSize wide and high (the ItemTile prefab's size; 2.9 units = the pack's 290 px cells).
//
// One pair of neighbouring tiles at a time can be "held": dragging the pressed tile towards one of
// its four neighbours slides it there and the neighbour the other way, by a fraction (0..1) of the
// way. Nothing wraps.
//
// Matches: every tile in its home cell shows a check (the HUD's subject rigs show the fitted part).
// The selected category's row (SelectedRow: the clues on screen) has the highlight behind it. All
// derived from BoardState after each settle, so undo and broken matches need no special case. A
// played move that matches new cells flashes them pale green with a ring and a few stars; the
// guide (Board/Guide) shows a swap to make (the Hint).
public class Manager_Board : Singleton<Manager_Board>
{
    [SerializeField] private Transform _boardRoot;
    [SerializeField] private SpriteRenderer _frame;
    [SerializeField] private SpriteMask _mask;
    [SerializeField] private SpriteRenderer _activeRow;
    [Tooltip("Full-screen booth art behind everything; FitCamera scales it to cover the view.")]
    [SerializeField] private SpriteRenderer _background;
    [SerializeField] private Transform _cellsRoot;
    [SerializeField] private Transform _tilesRoot;
    [Tooltip("Board/Dividers: grid lines, the first half vertical, the rest horizontal (N - 1 each).")]
    [SerializeField] private SpriteRenderer[] _dividers = new SpriteRenderer[0];
    [Tooltip("Board/Guide: band, arrow and hand showing a swipe.")]
    [SerializeField] private BoardGuide _guide;

    // Raised when a line has finished animating and the indicators are up to date.
    public event Action OnBoardSettled = delegate { };

    // Raised when SelectedRow changes without a move (a category tab tap).
    public event Action OnSelectionChanged = delegate { };

    // Raised when a pair is picked up (a player's drag, or a swap from code: undo, ApplyMove).
    public event Action OnTilesPickedUp = delegate { };

    public BoardState State { get; private set; }
    public LevelDefinition Level { get; private set; }
    public bool IsDragging => _held && _animation == null;
    // True while a pair slides or the launch intro scales the board in; presses are refused meanwhile.
    public bool IsAnimating => _animation != null || _intro != null;
    public float DragOffset => _offset;
    // Cells the last settled move matched that were not matched when it was picked up.
    public int LastNewMatches { get; private set; }
    // Whether the last settle was a played move (not Undo): its new matches are celebrated.
    public bool LastSettleCelebrated { get; private set; }

    // The category whose clues are on screen: the player's choice (a category tab) until a move
    // completes that row, otherwise the top row that is not complete. -1 when solved.
    public int SelectedRow => State == null || State.IsSolved ? -1 : _chosenRow >= 0 ? _chosenRow : State.FirstIncompleteRow;

    private Cell[,] _cells;
    private ItemTile[,] _tiles;
    private int _chosenRow = -1;

    // Held pair: _heldA started in (_heldMove.row, col), _heldB in the neighbour's cell.
    private bool _held;
    private SwapMove _heldMove;
    private ItemTile _heldA;
    private ItemTile _heldB;
    private float _offset;
    private Tween _animation;

    // Launch intro: the whole board scales up once, on the first Build.
    private Tween _intro;
    private bool _introPlayed;

    // Which cells were matched when the pair was picked up, to find what a move newly matched.
    private bool[,] _homeAtPickUp;
    // False for a swap that takes a move back (Undo): its matches come back without celebration.
    private bool _celebrate;

    private int Size => State.Size;

    // One cell = one tile, no gap.
    public Vector2 CellSize => FileRef.Instance.ItemTilePrefab.Size;
    private Vector2 GridSize => CellSize * Size;

    public void Build(LevelDefinition level)
    {
        Clear();
        Level = level;
        State = new BoardState(level);
        var fileRef = FileRef.Instance;

        _chosenRow = -1;
        _boardRoot.localPosition = ReferenceLayout.BoardCenter;

        // The frame is 9-sliced: sized, not scaled, so its corners stay sharp. The mask (a 1-unit
        // sprite) is scaled to the grid.
        _frame.drawMode = SpriteDrawMode.Sliced;
        _frame.transform.localScale = Vector3.one;
        _frame.size = GridSize + 2f * ReferenceLayout.FramePadding;
        _mask.transform.localScale = new Vector3(GridSize.x, GridSize.y, 1f);
        _activeRow.drawMode = SpriteDrawMode.Sliced;
        _activeRow.size = new Vector2(GridSize.x, CellSize.y) - Vector2.one * 0.08f;
        PlaceDividers();

        _cells = new Cell[Size, Size];
        _tiles = new ItemTile[Size, Size];
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                var cell = Instantiate(fileRef.CellPrefab, _cellsRoot);
                cell.transform.localPosition = CellPosition(r, c);
                cell.Setup(r, c);
                _cells[r, c] = cell;

                _tiles[r, c] = SpawnTile(State[r, c], CellPosition(r, c));
            }
        }

        FitCamera();
        if (!_introPlayed) PlayIntro();
        float spawnStart = AnimateTileSpawn();

        // Each check pops in once its own tile has finished spawning.
        RefreshIndicators((r, c) => spawnStart + (r + c) * Tuning.TileSpawnStagger + Tuning.TileSpawnDuration);
    }

    // ---------- Category selection ----------

    // A category tab tap: show that row's clues (and its highlight). Costs no move.
    public void SelectRow(int row)
    {
        if (State == null || State.IsSolved || row < 0 || row >= Size || row == SelectedRow) return;
        _chosenRow = row;
        RefreshHighlight();
        OnSelectionChanged();
    }

    // ---------- Dragging ----------

    // Picks up the two neighbours of `move`: dragging slides the tile in (move.row, move.col)
    // towards the other cell and the other tile the opposite way.
    public void BeginDrag(SwapMove move)
    {
        if (_held) return;

        _homeAtPickUp = HomeCells();
        _celebrate = true;
        _held = true;
        _heldMove = move;
        _offset = 0f;
        _heldA = _tiles[move.row, move.col];
        _heldB = _tiles[move.OtherRow, move.OtherCol];
        _cells[move.row, move.col].SetMatched(false);     // checks are stale while moving
        _cells[move.OtherRow, move.OtherCol].SetMatched(false);
        RenderHeldPair();
        OnTilesPickedUp();
    }

    // How far the two tiles have traded places: 0 = where they started, 1 = swapped.
    public void SetDragOffset(float offset)
    {
        if (!IsDragging) return;
        _offset = Mathf.Clamp01(offset);
        RenderHeldPair();
    }

    // Slide the pair back to where it started; the board is unchanged.
    public void CancelDrag()
    {
        if (!IsDragging) return;
        AnimateHeldPair(0f, false);
    }

    // Commit the swap: the model changes now, the views finish sliding from the current offset.
    public void CommitDrag()
    {
        if (!IsDragging) return;
        State.Swap(_heldMove);
        AnimateHeldPair(1f, true);
    }

    // A swap made from code (ApplyMove, Undo). celebrate false (Undo): new matches appear without
    // the match sound or effects.
    public void Swap(SwapMove move, bool celebrate = true)
    {
        BeginDrag(move);
        _celebrate = celebrate;
        CommitDrag();
    }

    private void AnimateHeldPair(float targetOffset, bool committed)
    {
        float duration = Tuning.ShiftDuration * Mathf.Clamp(Mathf.Abs(targetOffset - _offset) * 1.5f, 0.35f, 1.5f);

        _animation = DOTween.To(() => _offset, x => { _offset = x; RenderHeldPair(); }, targetOffset, duration)
            .SetEase(Ease.OutCubic)
            .SetLink(gameObject)
            .OnComplete(() => SettleHeldPair(committed));
    }

    // Reorder the views to match the model and snap them into their cells.
    private void SettleHeldPair(bool committed)
    {
        var m = _heldMove;
        _tiles[m.row, m.col] = committed ? _heldB : _heldA;
        _tiles[m.OtherRow, m.OtherCol] = committed ? _heldA : _heldB;
        _tiles[m.row, m.col].transform.localPosition = CellPosition(m.row, m.col);
        _tiles[m.OtherRow, m.OtherCol].transform.localPosition = CellPosition(m.OtherRow, m.OtherCol);

        int gained = CountNewMatches();
        LastNewMatches = committed ? gained : 0;
        bool celebrate = _celebrate && committed;
        LastSettleCelebrated = celebrate;
        if (celebrate && gained > 0) CelebrateNewMatches();
        // A chosen category stays on screen until a move completes it.
        if (committed && _chosenRow >= 0 && State.IsRowComplete(_chosenRow)) _chosenRow = -1;
        ReleaseHeldPair();
        RefreshIndicators();
        if (committed) PlaySettleFeedback(celebrate ? gained : 0);
        OnBoardSettled();
    }

    // One layered cue per settled move: the slide sound, then the best thing it achieved, and a
    // single haptic pulse (Heavy = solved, Medium = new matches, Light = anything else). New
    // matches play one of match_1..3 (TileMatch variants) by their count, never all three; the
    // solve plays level_win (LevelComplete) once.
    private void PlaySettleFeedback(int newMatches)
    {
        var audio = Manager_Audio.Instance;
        audio?.PlaySound(SoundType.SwipeSettle);

        if (State.IsSolved)
        {
            Haptics.Play(HapticType.Heavy);
            audio?.PlaySound(SoundType.LevelComplete);
        }
        else if (newMatches > 0)
        {
            Haptics.Play(HapticType.Medium);
            audio?.PlaySound(SoundType.TileMatch, Mathf.Min(newMatches, 3) - 1);
        }
        else Haptics.Play(HapticType.Light);
    }

    private bool[,] HomeCells()
    {
        var home = new bool[Size, Size];
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                home[r, c] = State.IsHome(r, c);
        return home;
    }

    // Each newly matched cell: a pale green flash on its tile, the shared ring growing and fading,
    // and 2-4 stars rising from it (ui_layout.json timings: newMatch 0.4, ring 0.5, stars 0.65 s).
    private static readonly Color MatchFlash = new Color32(0xDF, 0xF2, 0xA2, 0x99);
    private const float NewMatchSeconds = 0.4f;
    private const float RingSeconds = 0.5f;
    private const float StarSeconds = 0.65f;

    private void CelebrateNewMatches()
    {
        var ring = FileRef.Instance.Sprite("fx_ring");
        var star = FileRef.Instance.Sprite("fx_star");
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                if (!State.IsHome(r, c) || _homeAtPickUp[r, c]) continue;
                _tiles[r, c].Flash(MatchFlash, NewMatchSeconds);
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
                if (star == null) continue;
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

    private int CountNewMatches()
    {
        if (_homeAtPickUp == null) return 0;
        int gained = 0;
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                if (State.IsHome(r, c) && !_homeAtPickUp[r, c]) gained++;
        return gained;
    }

    // The pair drawn a little in front of the other tiles, traded `_offset` of the way.
    private void RenderHeldPair()
    {
        var m = _heldMove;
        Vector3 a = CellPosition(m.row, m.col) + Vector3.back * 0.01f;
        Vector3 b = CellPosition(m.OtherRow, m.OtherCol) + Vector3.back * 0.01f;
        _heldA.transform.localPosition = Vector3.Lerp(a, b, _offset);
        _heldB.transform.localPosition = Vector3.Lerp(b, a, _offset);
    }

    private void ReleaseHeldPair()
    {
        _held = false;
        _heldMove = null;
        _heldA = _heldB = null;
        _homeAtPickUp = null;
        _offset = 0f;
        _animation = null;
    }

    // ---------- Queries ----------

    public bool TryGetCell(Vector2 screenPosition, out int row, out int col)
    {
        row = col = -1;
        if (State == null || !TryScreenToLocal(screenPosition, out var local)) return false;

        Vector2 half = GridSize * 0.5f;
        col = Mathf.FloorToInt((local.x + half.x) / CellSize.x);
        row = Mathf.FloorToInt((half.y - local.y) / CellSize.y);
        return row >= 0 && row < Size && col >= 0 && col < Size;
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

    // Accepts fractional and out-of-board indices (used while lines move). Board-local.
    // Inverse: TryGetCell.
    public Vector3 CellPosition(float row, float col)
    {
        float offset = (Size - 1) * 0.5f;
        return new Vector3((col - offset) * CellSize.x, (offset - row) * CellSize.y, 0f);
    }

    // ---------- Views ----------

    private ItemTile SpawnTile(TileData data, Vector3 localPosition)
    {
        var tile = Instantiate(FileRef.Instance.ItemTilePrefab, _tilesRoot);
        tile.transform.localPosition = localPosition;
        SetupTile(tile, data);
        return tile;
    }

    private void SetupTile(ItemTile tile, TileData data)
    {
        var item = Level.ItemFor(data);
        tile.Setup(data, FileRef.Instance.Sprite(item.Sprite), item.Label);
    }

    // A check on every matched cell (popping in when it appears: after checkDelay(row, col) if
    // given, otherwise right away) and the highlight behind the selected category's row.
    private void RefreshIndicators(Func<int, int, float> checkDelay = null)
    {
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                _cells[r, c].SetMatched(State.IsHome(r, c), Tuning.CheckPopDuration, checkDelay?.Invoke(r, c) ?? 0f);
        RefreshHighlight();
    }

    private void RefreshHighlight()
    {
        int row = SelectedRow;
        _activeRow.gameObject.SetActive(row >= 0);
        if (row >= 0) _activeRow.transform.localPosition = CellPosition(row, (Size - 1) * 0.5f);
    }

    // ---------- Guide ----------

    // Shows the swap `move` on the board (band over its two cells, arrow, sliding hand) until HideGuide.
    public void ShowGuide(SwapMove move)
    {
        if (_guide == null || State == null || move == null) return;
        Vector3 center = (CellPosition(move.row, move.col) + CellPosition(move.OtherRow, move.OtherCol)) * 0.5f;
        Vector2 band = move.horizontal ? new Vector2(CellSize.x * 2f, CellSize.y) : new Vector2(CellSize.x, CellSize.y * 2f);
        Vector2 direction = move.horizontal ? Vector2.right : Vector2.down;
        _guide.Show(center, band - Vector2.one * 0.08f, direction, (move.horizontal ? CellSize.x : CellSize.y) * 1.2f);
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

    // Every Build: the level's tiles scale up from 0 in a diagonal wave from the top-left
    // (row + col steps of TileSpawnStagger). On launch the wave waits until the board is half grown.
    // Moves are allowed meanwhile
    // (dragging only touches position). The tweens die with the tiles (SetLink).
    // Returns when the wave starts (seconds from now), so the checks can follow it.
    private float AnimateTileSpawn()
    {
        float start = _intro != null ? Tuning.BoardIntroDelay + Tuning.BoardIntroDuration * 0.5f : 0f;
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                var tile = _tiles[r, c].transform;
                var target = tile.localScale;
                tile.localScale = Vector3.zero;
                tile.DOScale(target, Tuning.TileSpawnDuration)
                    .SetDelay(start + (r + c) * Tuning.TileSpawnStagger)
                    .SetEase(Ease.OutBack)
                    .SetLink(tile.gameObject);
            }
        }
        return start;
    }

    // ---------- Layout ----------

    public bool HasBoard => State != null;

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
        ReleaseHeldPair();
        HideGuide();
        DestroyChildren(_cellsRoot);
        DestroyChildren(_tilesRoot);
    }

    private static void DestroyChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
    }
}
