using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

// Owns the BoardState and its views: builds the board from Cell / ItemTile / CategoryTag prefabs,
// drags and shifts lines, and animates. Board-local space: centered on _boardRoot, row 0 at the top,
// cells CellSize wide and high (the ItemTile prefab's size; 2.96 x 2.56 units for the pack's tiles).
//
// One line (row or column) at a time can be "held": its tiles are drawn at a fractional offset
// (in cells; row + = right, column + = down) and wrap around the edge, with a ghost copy showing
// the part of the wrapping tile that is past the edge. _mask hides everything outside the board
// (the tile labels too, through their stencil material).
//
// Steps: the active step's row has the honey surface and the outline; confirmed rows show checks.
// Both are derived from BoardState after each settle, so undo and broken rows need no special case.
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
    [FormerlySerializedAs("_markersRoot")]
    [SerializeField] private Transform _tagsRoot;

    // Raised when a line has finished animating and the indicators are up to date.
    public event Action OnBoardSettled = delegate { };

    // Raised after Build has placed the board and fitted the camera, i.e. whenever the board's
    // on-screen area may have changed. UI that lays out around the board listens to this.
    public event Action OnLayoutChanged = delegate { };

    // Raised when a line is picked up (a player's drag, or a shift from code: undo, ApplyMove).
    public event Action OnLinePickedUp = delegate { };

    public BoardState State { get; private set; }
    public LevelDefinition Level { get; private set; }
    public bool IsDragging => _held && _animation == null;
    // True while a line slides or the launch intro scales the board in; presses are refused meanwhile.
    public bool IsAnimating => _animation != null || _intro != null;
    public float DragOffset => _offset;

    private Cell[,] _cells;
    private ItemTile[,] _tiles;
    private CategoryTag[] _tags;

    // Held line
    private bool _held;
    private bool _heldIsRow;
    private int _heldIndex;
    private ItemTile[] _heldTiles;   // the line's tiles in their order before the drag
    private float _offset;
    private ItemTile _ghost;
    private Tween _animation;

    // Launch intro: the whole board scales up once, on the first Build.
    private Tween _intro;
    private bool _introPlayed;

    // Steps confirmed when the line was picked up, to tell whether a move confirmed one.
    private int _stepsAtPickUp;

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

        _boardRoot.localPosition = ReferenceLayout.BoardCenter;

        // The frame is 9-sliced: sized, not scaled, so its corners stay sharp. The mask (a 1-unit
        // sprite) is scaled to the grid.
        _frame.drawMode = SpriteDrawMode.Sliced;
        _frame.transform.localScale = Vector3.one;
        _frame.size = GridSize + Vector2.one * (2f * ReferenceLayout.FramePadding);
        _mask.transform.localScale = new Vector3(GridSize.x, GridSize.y, 1f);
        _activeRow.drawMode = SpriteDrawMode.Sliced;
        _activeRow.size = new Vector2(GridSize.x, CellSize.y);

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

        _tags = new CategoryTag[Size];
        float tagX = -GridSize.x * 0.5f - ReferenceLayout.TagOffset;
        for (int r = 0; r < Size; r++)
        {
            var tag = Instantiate(fileRef.CategoryTagPrefab, _tagsRoot);
            tag.transform.localPosition = new Vector3(tagX, CellPosition(r, 0).y, 0f);
            tag.Setup(level.Categories[r]);
            _tags[r] = tag;
        }

        FitCamera();
        OnLayoutChanged();
        if (!_introPlayed) PlayIntro();
        float spawnStart = AnimateTileSpawn();

        // Each check pops in once its own tile has finished spawning.
        RefreshIndicators((r, c) => spawnStart + (r + c) * Tuning.TileSpawnStagger + Tuning.TileSpawnDuration);
    }

    // ---------- Dragging ----------

    public void BeginDrag(bool isRow, int index)
    {
        if (_held) return;

        _stepsAtPickUp = State.ConfirmedSteps;
        _held = true;
        _heldIsRow = isRow;
        _heldIndex = index;
        _offset = 0f;
        _heldTiles = new ItemTile[Size];
        for (int i = 0; i < Size; i++)
        {
            _heldTiles[i] = isRow ? _tiles[index, i] : _tiles[i, index];
            (isRow ? _cells[index, i] : _cells[i, index]).SetMatched(false);   // checks are stale while moving
        }

        _ghost = SpawnTile(_heldTiles[0].Data, Vector3.zero);
        _ghost.name = "Ghost";
        _ghost.gameObject.SetActive(false);
        OnLinePickedUp();
    }

    // offset in cells: row + = right, column + = down.
    public void SetDragOffset(float offset)
    {
        if (!IsDragging) return;
        _offset = offset;
        RenderHeldLine();
    }

    // Slide the held line back to where it started; the board is unchanged.
    public void CancelDrag()
    {
        if (!IsDragging) return;
        AnimateHeldLine(0f, 0);
    }

    // Commit the held line by whole steps (sign = direction), updating the model now
    // and sliding the views from the current drag offset to the final place.
    public void CommitDrag(int steps)
    {
        if (!IsDragging) return;

        if (_heldIsRow) State.ShiftRow(_heldIndex, steps);
        else State.ShiftColumn(_heldIndex, steps);

        AnimateHeldLine(steps, steps);
    }

    // Programmatic one-shot shifts (dir +1 = right / down).
    public void ShiftRow(int row, int steps)
    {
        BeginDrag(true, row);
        CommitDrag(steps);
    }

    public void ShiftColumn(int col, int steps)
    {
        BeginDrag(false, col);
        CommitDrag(steps);
    }

    private void AnimateHeldLine(float targetOffset, int committedSteps)
    {
        float duration = Tuning.ShiftDuration * Mathf.Clamp(Mathf.Abs(targetOffset - _offset), 0.35f, 1.5f);

        _animation = DOTween.To(() => _offset, x => { _offset = x; RenderHeldLine(); }, targetOffset, duration)
            .SetEase(Ease.OutCubic)
            .SetLink(gameObject)
            .OnComplete(() => SettleHeldLine(committedSteps));
    }

    // Reorder the views to match the model and snap them into their cells.
    private void SettleHeldLine(int committedSteps)
    {
        for (int i = 0; i < Size; i++)
        {
            var tile = _heldTiles[State.Wrap(i - committedSteps)];
            int r = _heldIsRow ? _heldIndex : i;
            int c = _heldIsRow ? i : _heldIndex;
            _tiles[r, c] = tile;
            tile.transform.localPosition = CellPosition(r, c);
        }

        ReleaseHeldLine();
        RefreshIndicators();
        if (committedSteps != 0) PlaySettleFeedback();
        OnBoardSettled();
    }

    // One layered cue per settled move: the slide sound, then the best thing it achieved, and a
    // single haptic pulse (Heavy = solved, Medium = a step confirmed, Light = anything else).
    // Nothing plays for a single tile reaching its cell: that would give the answer away.
    private void PlaySettleFeedback()
    {
        var audio = Manager_Audio.Instance;
        audio?.PlaySound(SoundType.SwipeSettle);

        if (State.IsSolved)
        {
            Haptics.Play(HapticType.Heavy);
            audio?.PlaySound(SoundType.LevelComplete);
        }
        else if (State.ConfirmedSteps > _stepsAtPickUp)
        {
            Haptics.Play(HapticType.Medium);
            audio?.PlaySound(SoundType.LineComplete);
        }
        else Haptics.Play(HapticType.Light);
    }

    private void RenderHeldLine()
    {
        ItemTile crossing = null;
        float ghostPosition = 0f;

        for (int i = 0; i < Size; i++)
        {
            // Wrapped position along the line, in [-0.5, Size - 0.5).
            float p = Mod(i + _offset + 0.5f, Size) - 0.5f;
            _heldTiles[i].transform.localPosition = LinePosition(p);

            // A tile half past one edge shows its other half entering on the opposite edge.
            if (p > Size - 1) { crossing = _heldTiles[i]; ghostPosition = p - Size; }
            else if (p < 0f) { crossing = _heldTiles[i]; ghostPosition = p + Size; }
        }

        _ghost.gameObject.SetActive(crossing != null);
        if (crossing == null) return;

        if (!_ghost.Data.Equals(crossing.Data)) SetupTile(_ghost, crossing.Data);
        _ghost.SetSurface(crossing.Surface);     // it stands in for that tile, so it wears its surface
        _ghost.transform.localPosition = LinePosition(ghostPosition);
    }

    private void ReleaseHeldLine()
    {
        if (_ghost != null) Destroy(_ghost.gameObject);
        _ghost = null;
        _held = false;
        _heldTiles = null;
        _offset = 0f;
        _animation = null;
    }

    private Vector3 LinePosition(float p) =>
        _heldIsRow ? CellPosition(_heldIndex, p) : CellPosition(p, _heldIndex);

    private static float Mod(float x, float m) => x - m * Mathf.Floor(x / m);

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
        var item = FileRef.Instance.GetItem(Level.ItemFor(data));
        tile.Setup(data, item.Sprite, item.Name);
    }

    private Sprite SurfaceFor(int row) =>
        row == State.ActiveRow ? FileRef.Instance.TileSurfaceActive : FileRef.Instance.TileSurface;

    // Checks on confirmed rows (popping in when they appear: after checkDelay(row, col) if given,
    // otherwise right away), the honey surface and outline on the active step's row.
    private void RefreshIndicators(Func<int, int, float> checkDelay = null)
    {
        int activeRow = State.ActiveRow;
        for (int r = 0; r < Size; r++)
        {
            bool confirmed = State.IsRowConfirmed(r);
            for (int c = 0; c < Size; c++)
            {
                _cells[r, c].SetMatched(confirmed, Tuning.CheckPopDuration, checkDelay?.Invoke(r, c) ?? 0f);
                _tiles[r, c].SetSurface(SurfaceFor(r));
            }
        }

        _activeRow.gameObject.SetActive(activeRow >= 0);
        if (activeRow >= 0) _activeRow.transform.localPosition = CellPosition(activeRow, (Size - 1) * 0.5f);
    }

    // ---------- Intro ----------

    // Scales _boardRoot from 0 to its scene scale (DOTween, OutBack), in step with UI_InGame's HUD
    // intro. Runs after FitCamera / OnLayoutChanged, which measure the board at full size.
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
    // Only the level's own tiles: the drag ghost appears instantly. Moves are allowed meanwhile
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

    // World-space area of the board. withTags false: the frame only (grid + FramePadding).
    // withTags true: also the category tags on the left. Board rotation is not supported.
    public Bounds GetWorldBounds(bool withTags)
    {
        GetLocalBounds(withTags, out var min, out var max);
        var a = _boardRoot.TransformPoint(min);
        var b = _boardRoot.TransformPoint(max);
        var bounds = new Bounds((a + b) * 0.5f, Vector3.zero);
        bounds.Encapsulate(a);
        bounds.Encapsulate(b);
        return bounds;
    }

    // The same area in screen pixels (origin bottom-left), through Camera.main.
    public Rect GetScreenRect(bool withTags)
    {
        var cam = Camera.main;
        if (cam == null || !HasBoard) return default;

        var bounds = GetWorldBounds(withTags);
        Vector2 a = cam.WorldToScreenPoint(bounds.min);
        Vector2 b = cam.WorldToScreenPoint(bounds.max);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    private void GetLocalBounds(bool withTags, out Vector2 min, out Vector2 max)
    {
        Vector2 half = (HasBoard ? GridSize : Vector2.zero) * 0.5f + Vector2.one * ReferenceLayout.FramePadding;
        min = -half;
        max = half;
        if (withTags && HasBoard)
            min.x = Mathf.Min(min.x, -GridSize.x * 0.5f - ReferenceLayout.TagOffset - ReferenceLayout.TagSize.x * 0.5f);
    }

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

#if UNITY_EDITOR
    // Scene view: green = frame, yellow = frame + category tags.
    private void OnDrawGizmos()
    {
        if (!HasBoard || _boardRoot == null) return;
        var frame = GetWorldBounds(false);
        var all = GetWorldBounds(true);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(frame.center, frame.size);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(all.center, all.size);
    }
#endif

    private void Clear()
    {
        _animation?.Kill();
        _intro?.Complete();     // a level change mid-intro jumps to full size
        ReleaseHeldLine();
        DestroyChildren(_cellsRoot);
        DestroyChildren(_tilesRoot);
        DestroyChildren(_tagsRoot);
    }

    private static void DestroyChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
    }
}
