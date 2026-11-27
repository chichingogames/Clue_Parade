using DG.Tweening;
using UnityEngine;

// A board piece: the item's icon on the board panel (no surface of its own, as in the handoff),
// and a pale green flash when it becomes matched. Pure view: Manager_Board hands it the sprite.
// 1 unit = 100 px of the 1080x1920 reference.
public class ItemTile : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _item;
    [Tooltip("Rounded highlight behind the item, transparent until Flash.")]
    [SerializeField] private SpriteRenderer _flash;
    [Tooltip("Tile size in board units. Manager_Board uses it as the cell size.")]
    [SerializeField] private Vector2 _size = new Vector2(2.2667f, 2.2667f);
    [Tooltip("The item art is scaled to fit inside this box (keeping its aspect), centered on the Item object.")]
    [SerializeField] private Vector2 _itemBox = new Vector2(2.0f, 2.0f);

    public TileData Data { get; private set; }
    public string ItemName { get; private set; }
    public Vector2 Size => _size;

    public void Setup(TileData data, Sprite item, string itemName)
    {
        Data = data;
        ItemName = itemName;
        name = $"ItemTile {itemName}";

        _item.sprite = item;
        _item.transform.localScale = Vector3.one * FitScale(item);
        SetFlashAlpha(0f);
    }

    // Up to color (its alpha is the peak) and back to clear over duration.
    public void Flash(Color color, float duration)
    {
        _flash.DOKill();
        _flash.color = new Color(color.r, color.g, color.b, 0f);
        _flash.DOFade(color.a, duration * 0.35f).SetLoops(2, LoopType.Yoyo).SetLink(gameObject);
    }

    private void SetFlashAlpha(float alpha)
    {
        _flash.DOKill();
        var c = _flash.color;
        _flash.color = new Color(c.r, c.g, c.b, alpha);
    }

    private float FitScale(Sprite sprite)
    {
        if (sprite == null) return 1f;
        Vector2 size = sprite.bounds.size;
        return Mathf.Min(_itemBox.x / size.x, _itemBox.y / size.y);
    }
}
