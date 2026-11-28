using UnityEngine;

// Moves a HUD element down into the spare room a tall screen leaves below the 1080x1920 reference
// (UI_ReferenceFit centres the reference in the safe area, so a phone taller than 9:16 gets empty
// strips above and below it). _share is how much of the lower strip the element moves by: 1 = the
// bottom buttons sit on the safe area's bottom edge, smaller values spread the elements above them.
// On a 9:16 or wider screen there is no spare room and nothing moves. Runtime only: the scene
// keeps the built positions.
[RequireComponent(typeof(RectTransform))]
public class UI_SafeDock : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float _share = 1f;

    private RectTransform _rect;
    private Vector2 _home;
    private bool _hasHome;
    private float _applied = -1f;

    private void OnEnable()
    {
        _rect = (RectTransform)transform;
        if (!_hasHome) { _home = _rect.anchoredPosition; _hasHome = true; }
        _applied = -1f;
        Dock();
    }

    // Puts the element back where it was built, so the scene never saves a docked position.
    private void OnDisable()
    {
        if (_hasHome && _rect != null) _rect.anchoredPosition = _home;
        _applied = -1f;
    }

    private void LateUpdate() => Dock();

    private void Dock()
    {
        var reference = _rect.parent as RectTransform;
        var safeArea = reference != null ? reference.parent as RectTransform : null;
        if (safeArea == null) return;
        Vector2 size = safeArea.rect.size;
        if (size.x <= 0f || size.y <= 0f) return;
        float scale = Mathf.Min(size.x / UI_ReferenceFit.ReferenceSize.x, size.y / UI_ReferenceFit.ReferenceSize.y);
        float below = Mathf.Max(0f, (size.y / scale - UI_ReferenceFit.ReferenceSize.y) * 0.5f);   // reference px
        float offset = below * _share;
        if (Mathf.Approximately(offset, _applied)) return;
        _applied = offset;
        _rect.anchoredPosition = _home + Vector2.down * offset;
    }
}
