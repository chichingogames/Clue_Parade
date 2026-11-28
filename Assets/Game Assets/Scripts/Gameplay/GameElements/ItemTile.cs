using DG.Tweening;
using TMPro;
using UnityEngine;

// How a tile's background is tinted (Manager_Board picks one; highest wins). Aligned is unused:
// nothing shows that an item stands under its right owner.
public enum TileLook { None = 0, Aligned = 1, Focus = 2, Hint = 3, Selected = 4 }

// A board piece: the item's icon (no name) on the board panel (no surface of its own, as in the
// handoff), a background tint for its look (an item a tapped clue names, a hinted swap, the
// tap-selected tile) and short flashes (a refill arriving). SetCellSize fits it to a shorter cell
// (4-row boards). Pure view:
// Manager_Board hands it the sprite and name (kept for logs). 1 unit = 100 px of the 1080x1920
// reference. The icon is clipped by the board mask.
public class ItemTile : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _item;
    [Tooltip("Optional item name; the any-row tiles show none.")]
    [SerializeField] private TextMeshPro _label;
    [Tooltip("Rounded highlight behind the item: the look's tint and flashes.")]
    [SerializeField] private SpriteRenderer _flash;
    [Tooltip("Tile size in board units. Manager_Board uses it as the cell size.")]
    [SerializeField] private Vector2 _size = new Vector2(2.9f, 2.9f);
    [Tooltip("The item art is scaled to fit inside this box (keeping its aspect), centered on the Item object.")]
    [SerializeField] private Vector2 _itemBox = new Vector2(2.4f, 2.05f);

    private static readonly Color FocusTint = new Color32(0x48, 0xAA, 0xA1, 0x60);
    private static readonly Color HintTint = new Color32(0xFF, 0xB3, 0x00, 0x70);
    private static readonly Color SelectedTint = new Color32(0xFF, 0xA8, 0x00, 0x90);

    public string Id { get; private set; }
    public string ItemName { get; private set; }
    public Vector2 Size => _size;
    public Sprite ItemSprite => _item.sprite;

    private TileLook _look;
    private Vector2 _box;
    private Vector2 _flashSize;

    public void Setup(string id, Sprite item, string itemName)
    {
        Id = id;
        ItemName = itemName;
        name = $"ItemTile {itemName}";

        _item.sprite = item;
        if (_box == Vector2.zero) _box = _itemBox;
        _item.transform.localScale = Vector3.one * FitScale(item);
        if (_label != null) _label.text = itemName;
        _look = TileLook.None;
        SetMuted(false);
        ApplyLook();
    }

    // The cell this tile fills (board units): the art box and the tint shrink with a shorter cell.
    public void SetCellSize(Vector2 cell)
    {
        if (_flashSize == Vector2.zero) _flashSize = _flash.size;
        Vector2 ratio = new Vector2(cell.x / _size.x, cell.y / _size.y);
        _box = new Vector2(_itemBox.x * Mathf.Min(1f, ratio.x), _itemBox.y * Mathf.Min(1f, ratio.y));
        _flash.size = new Vector2(_flashSize.x * ratio.x, _flashSize.y * ratio.y);
        _item.transform.localScale = Vector3.one * FitScale(_item.sprite);
    }

    public void SetLook(TileLook look)
    {
        if (look == _look) return;
        _look = look;
        ApplyLook();
    }

    // Faded (case 1's first swap: every tile but the two to swap).
    public void SetMuted(bool muted)
    {
        float alpha = muted ? 0.45f : 1f;
        _item.color = new Color(1f, 1f, 1f, alpha);
        if (_label != null) _label.alpha = alpha;
    }

    // Up to color (its alpha is the peak) and back to the look's tint over duration.
    public void Flash(Color color, float duration)
    {
        _flash.DOKill();
        _flash.color = new Color(color.r, color.g, color.b, 0f);
        _flash.DOFade(color.a, duration * 0.35f).SetLoops(2, LoopType.Yoyo).SetLink(gameObject).OnComplete(ApplyLook);
    }

    private void ApplyLook()
    {
        _flash.DOKill();
        var tint = _look switch
        {
            TileLook.Focus => FocusTint,
            TileLook.Hint => HintTint,
            TileLook.Selected => SelectedTint,
            _ => new Color(1f, 1f, 1f, 0f),
        };
        _flash.color = tint;
        if (_look == TileLook.Hint)     // a gentle pulse while the hint is up
            _flash.DOFade(tint.a * 0.35f, 0.65f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
    }

    private float FitScale(Sprite sprite)
    {
        if (sprite == null) return 1f;
        Vector2 size = sprite.bounds.size;
        Vector2 box = _box == Vector2.zero ? _itemBox : _box;
        return Mathf.Min(box.x / size.x, box.y / size.y);
    }
}
