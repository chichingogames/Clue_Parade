using UnityEngine;

// Sizes this RectTransform to cover the board on screen, so HUD elements can be laid out around
// the board: make them children and anchor them to its edges (e.g. anchor top-center, pivot
// bottom-center to sit just above it). Updated whenever Manager_Board rebuilds the board.
//
// Lives under Canvas/Container/InGame (Screen Space – Overlay). Anchors are set as fractions of
// the parent rect, so the area keeps up with the canvas scaler and the safe area.
[RequireComponent(typeof(RectTransform))]
public class UI_BoardArea : MonoBehaviour
{
    [Tooltip("Off: the board frame only. On: also the row targets (left) and column targets (top).")]
    [SerializeField] private bool _includeTargets = true;

    [Tooltip("Extra space around the area, in canvas units (+ grows, - shrinks).")]
    [SerializeField] private float _padding;

    [Tooltip("On: only match the board's left and right edges; the vertical anchors and offsets set in the " +
             "Inspector are kept (e.g. Bottom Stack: board width, pinned to the bottom of the screen). " +
             "Needs the same parent as Board Area.")]
    [SerializeField] private bool _horizontalOnly;

    private RectTransform _rect;

    private void Awake() => _rect = (RectTransform)transform;

    private void OnEnable()
    {
        var board = Manager_Board.Instance;
        if (board == null) return;

        // Subscribe, then read the current value: the board may already be built.
        board.OnLayoutChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (Manager_Board.Instance != null) Manager_Board.Instance.OnLayoutChanged -= Refresh;
    }

    public void Refresh()
    {
        var board = Manager_Board.Instance;
        var parent = _rect.parent as RectTransform;
        if (board == null || !board.HasBoard || parent == null) return;

        // The safe area and canvas scaler may not have laid out yet on the first frame.
        Canvas.ForceUpdateCanvases();

        Rect screen = board.GetScreenRect(_includeTargets);
        Rect parentRect = parent.rect;
        if (parentRect.width <= 0f || parentRect.height <= 0f) return;

        // Overlay canvas: no camera for the screen → local conversion.
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen.min, null, out var min);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen.max, null, out var max);

        // Unclamped (unlike Rect.PointToNormalized): the board may reach past the safe area.
        Vector2 anchorMin = (min - parentRect.min) / parentRect.size;
        Vector2 anchorMax = (max - parentRect.min) / parentRect.size;

        if (_horizontalOnly)
        {
            _rect.anchorMin = new Vector2(anchorMin.x, _rect.anchorMin.y);
            _rect.anchorMax = new Vector2(anchorMax.x, _rect.anchorMax.y);
            _rect.offsetMin = new Vector2(-_padding, _rect.offsetMin.y);
            _rect.offsetMax = new Vector2(_padding, _rect.offsetMax.y);
            return;
        }

        _rect.anchorMin = anchorMin;
        _rect.anchorMax = anchorMax;
        _rect.offsetMin = new Vector2(-_padding, -_padding);
        _rect.offsetMax = new Vector2(_padding, _padding);
    }
}
