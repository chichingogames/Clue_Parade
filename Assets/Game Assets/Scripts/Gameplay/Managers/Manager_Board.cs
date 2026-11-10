using System;
using DG.Tweening;
using UnityEngine;

// Owns the BoardState and its views: builds the board from Cell/Tile/TargetMarker prefabs,
// drags and shifts lines, and animates. Board-local space: centered on _boardRoot, row 0 at the top.
//
// One line (row or column) at a time can be "held": its tiles are drawn at a fractional offset
// (in cells; row + = right, column + = down) and wrap around the edge, with a ghost copy showing
// the part of the wrapping tile that is past the edge. _mask hides everything outside the board.
public class Manager_Board : Singleton<Manager_Board>
{
    [SerializeField] private Transform _boardRoot;
    [SerializeField] private SpriteRenderer _frame;
    [SerializeField] private SpriteMask _mask;
    [SerializeField] private Transform _cellsRoot;
    [SerializeField] private Transform _tilesRoot;
    [SerializeField] private Transform _markersRoot;

    // Raised when a line has finished animating and the indicators are up to date.
    public event Action OnBoardSettled = delegate { };

    // Raised after Build has placed the board and fitted the camera, i.e. whenever the board's
    // on-screen area may have changed. UI that lays out around the board listens to this.
    public event Action OnLayoutChanged = delegate { };

    // Raised when a line is picked up (a player's drag, or a shift from code: undo, ApplyMove).
    public event Action OnLinePickedUp = delegate { };

    public BoardState State { get; private set; }
    public bool IsDragging => _held && _animation == null;
    // True while a line slides or the launch intro scales the board in; presses are refused meanwhile.
    public bool IsAnimating => _animation != null || _intro != null;
    public float DragOffset => _offset;

    private Cell[,] _cells;
    private Tile[,] _tiles;
    private TargetMarker[] _rowMarkers;
    private TargetMarker[] _columnMarkers;

    // Held line
    private bool _held;
    private bool _heldIsRow;
    private int _heldIndex;
    private Tile[] _heldTiles;       // the line's tiles in their order before the drag
    private float _offset;
    private Tile _ghost;
    private Tween _animation;

    // Launch intro: the whole board scales up once, on the first Build.
    private Tween _intro;
    private bool _introPlayed;

    // Board as it was when the line was picked up, to tell which matches a move is new.
    private bool[,] _matchedAtPickUp;
    private int _linesAtPickUp;

    private int Size => State.Size;
    private static float CellSize => Tuning.CellSize;

    public void Build(LevelDefinition level)
    {
        Clear();
        State = new BoardState(level);

        var fileRef = FileRef.Instance;
        float boardSize = Size * CellSize;
        // The frame is a 9-sliced panel: sized, not scaled, so its rounded corners stay sharp.
        _frame.drawMode = SpriteDrawMode.Sliced;
        _frame.transform.localScale = Vector3.one;
        _frame.size = Vector2.one * (boardSize + 2f * Tuning.FramePadding * CellSize);
        _mask.transform.localScale = Vector3.one * boardSize;

        _cells = new Cell[Size, Size];
        _tiles = new Tile[Size, Size];
        for (int r = 0; r < Size; r++)
        {
            for (int c = 0; c < Size; c++)
            {
                var cell = Instantiate(fileRef.CellPrefab, _cellsRoot);
                cell.transform.localPosition = CellPosition(r, c);
                cell.transform.localScale = Vector3.one * CellSize;
                cell.Setup(r, c);
                _cells[r, c] = cell;

                _tiles[r, c] = SpawnTile(State[r, c], CellPosition(r, c));
            }
        }

        float markerDistance = boardSize * 0.5f + Tuning.MarkerOffset * CellSize;

        _rowMarkers = new TargetMarker[Size];
        for (int r = 0; r < Size; r++)
        {
            var position = new Vector3(-markerDistance, CellPosition(r, 0).y);
            _rowMarkers[r] = SpawnMarker($"Row Target {r}", position, fileRef.GetRowTargetSprite(State.RowTargets[r]));
        }

        _columnMarkers = new TargetMarker[Size];
        for (int c = 0; c < Size; c++)
        {
            var position = new Vector3(CellPosition(0, c).x, markerDistance);
            _columnMarkers[c] = SpawnMarker($"Column Target {c}", position, fileRef.GetColumnTargetSprite(State.ColumnTargets[c]));
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

        SnapshotMatches();
        _held = true;
        _heldIsRow = isRow;
        _heldIndex = index;
        _offset = 0f;
        _heldTiles = new Tile[Size];
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

    private void SnapshotMatches()
    {
        _matchedAtPickUp = new bool[Size, Size];
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                _matchedAtPickUp[r, c] = State.IsCellMatched(r, c);
        _linesAtPickUp = CompleteLineCount();
    }

    private int CompleteLineCount()
    {
        int lines = 0;
        for (int i = 0; i < Size; i++)
        {
            if (State.IsRowComplete(i)) lines++;
            if (State.IsColumnComplete(i)) lines++;
        }
        return lines;
    }

    // One layered cue per settled move: the slide sound, then the best thing it achieved, and a
    // single haptic pulse for the best thing (Heavy = solved, Medium = line, Light = anything else).
    // TileMatch variants climb with the number of cells newly matched (01 = one, 04 = four or more).
    private void PlaySettleFeedback()
    {
        var audio = Manager_Audio.Instance;
        audio?.PlaySound(SoundType.SwipeSettle);

        if (State.IsSolved)
        {
            Haptics.Play(HapticType.Heavy);
            audio?.PlaySound(SoundType.LevelComplete);
            return;
        }

        if (CompleteLineCount() > _linesAtPickUp)
        {
            Haptics.Play(HapticType.Medium);
            audio?.PlaySound(SoundType.LineComplete);
            return;
        }

        Haptics.Play(HapticType.Light);
        int newMatches = 0;
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                if (State.IsCellMatched(r, c) && !_matchedAtPickUp[r, c]) newMatches++;
        if (newMatches > 0) audio?.PlaySound(SoundType.TileMatch, Mathf.Min(newMatches, 4) - 1);
    }

    private void RenderHeldLine()
    {
        Tile crossing = null;
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

        float half = Size * CellSize * 0.5f;
        col = Mathf.FloorToInt((local.x + half) / CellSize);
        row = Mathf.FloorToInt((half - local.y) / CellSize);
        return row >= 0 && row < Size && col >= 0 && col < Size;
    }

    // Screen-space drag converted to board cells (x right, y up).
    public Vector2 ScreenDeltaToCells(Vector2 screenFrom, Vector2 screenTo)
    {
        if (!TryScreenToLocal(screenFrom, out var from) || !TryScreenToLocal(screenTo, out var to)) return Vector2.zero;
        return (to - from) / CellSize;
    }

    private bool TryScreenToLocal(Vector2 screenPosition, out Vector2 local)
    {
        local = default;
        var cam = Camera.main;
        if (cam == null) return false;
        local = _boardRoot.InverseTransformPoint(cam.ScreenToWorldPoint(screenPosition));
        return true;
    }

    // Overlays that sit on the board (tutorial cues) parent here, so they follow its layout and intro.
    public Transform BoardRoot => _boardRoot;

    public Transform GetRowMarker(int row) => _rowMarkers[row].transform;
    public Transform GetColumnMarker(int col) => _columnMarkers[col].transform;

    // Accepts fractional and out-of-board indices (used while lines move). Board-local.
    public Vector3 CellPosition(float row, float col)
    {
        float offset = (Size - 1) * 0.5f;
        return new Vector3((col - offset) * CellSize, (offset - row) * CellSize, 0f);
    }

    // ---------- Views ----------

    private Tile SpawnTile(TileData data, Vector3 localPosition)
    {
        var tile = Instantiate(FileRef.Instance.TilePrefab, _tilesRoot);
        tile.transform.localPosition = localPosition;
        tile.transform.localScale = Vector3.one * (CellSize * Tuning.TileScale);
        SetupTile(tile, data);
        return tile;
    }

    private static void SetupTile(Tile tile, TileData data)
    {
        tile.Setup(data, FileRef.Instance.GetTileSprite(data));
    }

    private TargetMarker SpawnMarker(string markerName, Vector3 localPosition, Sprite icon)
    {
        var marker = Instantiate(FileRef.Instance.TargetMarkerPrefab, _markersRoot);
        marker.name = markerName;
        marker.transform.localPosition = localPosition;
        marker.transform.localScale = Vector3.one * CellSize;
        marker.Setup(icon);
        return marker;
    }

    // Checks that appear pop in (Tuning.CheckPopDuration), after checkDelay(row, col) if given,
    // otherwise right away; checks already up stay as they are.
    private void RefreshIndicators(Func<int, int, float> checkDelay = null)
    {
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                _cells[r, c].SetMatched(State.IsCellMatched(r, c), Tuning.CheckPopDuration, checkDelay?.Invoke(r, c) ?? 0f);

        for (int r = 0; r < Size; r++) _rowMarkers[r].SetComplete(State.IsRowComplete(r));
        for (int c = 0; c < Size; c++) _columnMarkers[c].SetComplete(State.IsColumnComplete(c));
    }

    // A hint: every target icon gives a short scale punch (Manager_Tutorial's idle hint).
    public void PulseTargets()
    {
        foreach (var marker in _rowMarkers) Punch(marker.transform);
        foreach (var marker in _columnMarkers) Punch(marker.transform);

        void Punch(Transform t)
        {
            t.DOComplete();
            t.DOPunchScale(Vector3.one * 0.18f * CellSize, 0.5f, 6, 0.6f).SetLink(t.gameObject);
        }
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

    // World-space area of the board. withTargets false: the frame only (grid + FramePadding).
    // withTargets true: the frame plus the row targets on the left and column targets on top,
    // which is the area the camera fits. Board rotation is not supported (axis-aligned bounds).
    public Bounds GetWorldBounds(bool withTargets)
    {
        GetLocalBounds(withTargets, out var min, out var max);
        var a = _boardRoot.TransformPoint(min);
        var b = _boardRoot.TransformPoint(max);
        var bounds = new Bounds((a + b) * 0.5f, Vector3.zero);
        bounds.Encapsulate(a);
        bounds.Encapsulate(b);
        return bounds;
    }

    // The same area in screen pixels (origin bottom-left), through Camera.main.
    public Rect GetScreenRect(bool withTargets)
    {
        var cam = Camera.main;
        if (cam == null || !HasBoard) return default;

        var bounds = GetWorldBounds(withTargets);
        Vector2 a = cam.WorldToScreenPoint(bounds.min);
        Vector2 b = cam.WorldToScreenPoint(bounds.max);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    private void GetLocalBounds(bool withTargets, out Vector2 min, out Vector2 max)
    {
        float half = (HasBoard ? Size : 0) * CellSize * 0.5f;
        float frame = half + Tuning.FramePadding * CellSize;
        min = new Vector2(-frame, -frame);
        max = new Vector2(frame, frame);
        if (!withTargets) return;

        // Targets: rows on the left, columns on top.
        float markers = half + (Tuning.MarkerOffset + 0.5f) * CellSize;
        min.x = Mathf.Min(min.x, -markers);
        max.y = Mathf.Max(max.y, markers);
    }

    private void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        GetLocalBounds(withTargets: true, out var min, out var max);

        // Fit the board (with targets) into the safe area minus the HUD bands above and below it.
        Rect safe = Screen.safeArea;
        var region = Rect.MinMaxRect(safe.xMin, safe.yMin + safe.height * Tuning.HudBottomReserve,
                                     safe.xMax, safe.yMax - safe.height * Tuning.HudTopReserve);
        if (region.width <= 0f || region.height <= 0f) region = safe;

        Vector2 extent = (max - min) * Tuning.CameraMargin;
        float unitsPerPixel = Mathf.Max(extent.x / region.width, extent.y / region.height);
        cam.orthographicSize = unitsPerPixel * Screen.height * 0.5f;

        // Shift the camera so the board's center lands on the region's center.
        Vector3 center = _boardRoot.TransformPoint((min + max) * 0.5f);
        Vector2 offset = (region.center - new Vector2(Screen.width, Screen.height) * 0.5f) * unitsPerPixel;
        cam.transform.position = new Vector3(center.x - offset.x, center.y - offset.y, cam.transform.position.z);
    }

#if UNITY_EDITOR
    // Scene view: green = frame, yellow = frame + targets (the camera-fit area).
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
        DestroyChildren(_markersRoot);
    }

    private static void DestroyChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
    }
}
