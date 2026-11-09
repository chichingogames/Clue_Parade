using UnityEngine;

// A movable piece. Shows the pre-rendered sprite it is handed; Manager_Board positions it.
public class Tile : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _body;

    public TileData Data { get; private set; }

    public void Setup(TileData data, Sprite sprite)
    {
        Data = data;
        name = $"Tile {data}";
        _body.sprite = sprite;
    }
}
