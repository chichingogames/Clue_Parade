using UnityEngine;

// One board slot. Instantiated N×N by Manager_Board; stays put while tiles slide over it.
// Matching is positional, so the "matched" check lives here, not on the tile.
public class Cell : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _background;
    [SerializeField] private GameObject _check;

    public int Row { get; private set; }
    public int Column { get; private set; }

    public void Setup(int row, int column)
    {
        Row = row;
        Column = column;
        name = $"Cell {row},{column}";
        SetMatched(false);
    }

    public void SetMatched(bool matched) => _check.SetActive(matched);
}
