using TMPro;
using UnityEngine;

// The tab beside a board row: the category's icon and label ("Hats"). The active step's row gets
// the gold tab. Stays put while the row's tiles move.
public class CategoryTag : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _tab;
    [SerializeField] private SpriteRenderer _icon;
    [SerializeField] private TextMeshPro _label;
    [SerializeField] private Sprite _normal;
    [SerializeField] private Sprite _active;

    public void Setup(string category, Sprite icon)
    {
        name = $"Tag {category}";
        _label.text = category;
        _icon.sprite = icon;
        _icon.gameObject.SetActive(icon != null);
    }

    public void SetActive(bool active)
    {
        var sprite = active ? _active : _normal;
        if (_tab.sprite == sprite) return;
        var size = _tab.size;
        _tab.sprite = sprite;
        _tab.size = size;
    }
}
