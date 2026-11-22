using UnityEngine;

// Makes this RectTransform the art pack's 1080x1920 reference screen: that size, centred in its
// parent (the safe area) and scaled uniformly to fit it. Children are then placed in reference
// pixels (anchor and pivot top-left, position (x, -y) from Layout_1080x1920.csv). Manager_Board fits
// the camera to the same reference screen, so the HUD lines up with the world-space board.
[ExecuteAlways, RequireComponent(typeof(RectTransform))]
public class UI_ReferenceFit : MonoBehaviour
{
    public static readonly Vector2 ReferenceSize = new Vector2(1080f, 1920f);

    private RectTransform _rect;
    private Vector2 _fittedTo;

    private void OnEnable()
    {
        _rect = (RectTransform)transform;
        _fittedTo = Vector2.zero;
        Fit();
    }

    // The parent's size follows the canvas scaler and the safe area; refit when it changes.
    private void LateUpdate() => Fit();

    private void Fit()
    {
        if (!(_rect.parent is RectTransform parent)) return;
        Vector2 size = parent.rect.size;
        if (size == _fittedTo || size.x <= 0f || size.y <= 0f) return;
        _fittedTo = size;

        _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.anchoredPosition = Vector2.zero;
        _rect.sizeDelta = ReferenceSize;
        float scale = Mathf.Min(size.x / ReferenceSize.x, size.y / ReferenceSize.y);
        _rect.localScale = new Vector3(scale, scale, 1f);
    }
}
