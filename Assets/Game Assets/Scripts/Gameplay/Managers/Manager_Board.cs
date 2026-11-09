using System;
using System.Collections;
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

    public BoardState State { get; private set; }
    public bool IsDragging => _held && _animation == null;
    public bool IsAnimating => _animation != null;
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
    private Coroutine _animation;

    private int Size => State.Size;
    private static float CellSize => Tuning.CellSize;

    public void Build(LevelDefinition level)
    {
        Clear();
        State = new BoardState(level);

        var fileRef = FileRef.Instance;
        float boardSize = Size * CellSize;
        _frame.transform.localScale = Vector3.one * (boardSize + 2f * Tuning.FramePadding * CellSize);
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

        RefreshIndicators();
        FitCamera();
    }

    // ---------- Dragging ----------

    public void BeginDrag(bool isRow, int index)
    {
        if (_held) return;

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
        _animation = StartCoroutine(AnimateHeldLine(0f, 0));
    }

    // Commit the held line by whole steps (sign = direction), updating the model now
    // and sliding the views from the current drag offset to the final place.
    public void CommitDrag(int steps)
    {
        if (!IsDragging) return;

        if (_heldIsRow) State.ShiftRow(_heldIndex, steps);
        else State.ShiftColumn(_heldIndex, steps);

        _animation = StartCoroutine(AnimateHeldLine(steps, steps));
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

    private IEnumerator AnimateHeldLine(float targetOffset, int committedSteps)
    {
        float from = _offset;
        float duration = Tuning.ShiftDuration * Mathf.Clamp(Mathf.Abs(targetOffset - from), 0.35f, 1.5f);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = 1f - Mathf.Pow(1f - t / duration, 3f);   // ease-out cubic
            _offset = Mathf.LerpUnclamped(from, targetOffset, k);
            RenderHeldLine();
            yield return null;
        }

        // Reorder the views to match the model and snap them into their cells.
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
        OnBoardSettled();
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

    // Accepts fractional and out-of-board indices (used while lines move).
    private Vector3 CellPosition(float row, float col)
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

    private void RefreshIndicators()
    {
        for (int r = 0; r < Size; r++)
            for (int c = 0; c < Size; c++)
                _cells[r, c].SetMatched(State.IsCellMatched(r, c));

        for (int r = 0; r < Size; r++) _rowMarkers[r].SetComplete(State.IsRowComplete(r));
        for (int c = 0; c < Size; c++) _columnMarkers[c].SetComplete(State.IsColumnComplete(c));
    }

    private void FitCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // Board plus the row targets on the left and column targets on top.
        float half = Size * CellSize * 0.5f;
        float frame = half + Tuning.FramePadding * CellSize;
        float markers = half + (Tuning.MarkerOffset + 0.5f) * CellSize;
        var min = new Vector2(-markers, -frame);
        var max = new Vector2(frame, markers);

        Vector3 center = _boardRoot.TransformPoint((min + max) * 0.5f);
        cam.transform.position = new Vector3(center.x, center.y, cam.transform.position.z);

        Vector2 extent = max - min;
        cam.orthographicSize = Mathf.Max(extent.y * 0.5f, extent.x * 0.5f / cam.aspect) * Tuning.CameraMargin;
    }

    private void Clear()
    {
        if (_animation != null) StopCoroutine(_animation);
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
