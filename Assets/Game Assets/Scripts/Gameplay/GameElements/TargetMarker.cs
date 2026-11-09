using UnityEngine;

// A row (shape) or column (color) target beside the board. Lights up when its line is satisfied.
public class TargetMarker : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _background;
    [SerializeField] private SpriteRenderer _icon;
    [SerializeField] private Color _idleColor = new Color(0.92f, 0.87f, 0.80f);
    [SerializeField] private Color _completeColor = new Color(0.75f, 0.90f, 0.71f);

    public void Setup(Sprite icon)
    {
        _icon.sprite = icon;
        SetComplete(false);
    }

    public void SetComplete(bool complete) => _background.color = complete ? _completeColor : _idleColor;
}
