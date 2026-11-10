using UnityEngine;

// Fits this RectTransform to Screen.safeArea (notches, rounded corners, home indicator) by
// setting its anchors. Put it on a full-screen child of a Screen Space – Overlay canvas.
// Applied once in Awake: the game is portrait-locked, so the safe area never changes at runtime.
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private void Awake()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;

        var area = Screen.safeArea;
        var min = area.position;
        var max = area.position + area.size;
        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        var rect = (RectTransform)transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
