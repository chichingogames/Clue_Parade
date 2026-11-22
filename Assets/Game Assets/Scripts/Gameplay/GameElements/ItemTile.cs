using TMPro;
using UnityEngine;

// A Clue Parade board piece: a tile surface, the item's art and its name ("Sunhat").
// Pure view: Manager_Board hands it the sprite and the name. Laid out in board units, where
// 1 unit = 100 px of the 1080x1920 reference (a 296x256 px tile is 2.96 x 2.56).
public class ItemTile : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _surface;
    [SerializeField] private SpriteRenderer _item;
    [SerializeField] private TextMeshPro _label;
    [Tooltip("Tile size in board units. Manager_Board uses it as the cell size; the surface is drawn sliced at this size.")]
    [SerializeField] private Vector2 _size = new Vector2(2.96f, 2.56f);
    [Tooltip("The item art is scaled to fit inside this box (keeping its aspect), centered on the Item object.")]
    [SerializeField] private Vector2 _itemBox = new Vector2(2.52f, 1.84f);

    public TileData Data { get; private set; }
    public string ItemName { get; private set; }
    public Vector2 Size => _size;
    public Sprite Surface => _surface.sprite;

    public void Setup(TileData data, Sprite item, string itemName)
    {
        Data = data;
        ItemName = itemName;
        name = $"ItemTile {itemName}";

        _item.sprite = item;
        _item.transform.localScale = Vector3.one * FitScale(item);
        _label.text = itemName;
    }

    // Cream for most rows, honey for the active step's row (FileRef surfaces).
    public void SetSurface(Sprite surface)
    {
        if (surface == null || _surface.sprite == surface) return;
        _surface.sprite = surface;
        _surface.size = _size;
    }

    private float FitScale(Sprite sprite)
    {
        if (sprite == null) return 1f;
        Vector2 size = sprite.bounds.size;
        return Mathf.Min(_itemBox.x / size.x, _itemBox.y / size.y);
    }
}
