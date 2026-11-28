using DG.Tweening;
using UnityEngine;

// One board position. Instantiated N×N by Manager_Board; stays put while tiles slide under it.
// Shows the check while the cell holds its own item (about the position, not the tile).
public class Cell : MonoBehaviour
{
    [SerializeField] private GameObject _check;

    public int Row { get; private set; }
    public int Column { get; private set; }
    public bool IsMatched { get; private set; }

    private Vector3 _checkScale;
    private Tween _pop;

    private void Awake() => _checkScale = _check.transform.localScale;

    public void Setup(int row, int column)
    {
        Row = row;
        Column = column;
        name = $"Cell {row},{column}";
        IsMatched = false;
        _check.SetActive(false);
    }

    // Shows or hides the check. A check that appears pops in (scale 0 -> 1, OutBack) over
    // popDuration after delay; popDuration 0 shows it at once. Hiding is instant. Repeating the
    // current state does nothing, so a check that is already up never pops again.
    public void SetMatched(bool matched, float popDuration = 0f, float delay = 0f)
    {
        if (matched == IsMatched) return;
        IsMatched = matched;

        _pop?.Kill();
        _pop = null;
        _check.transform.localScale = _checkScale;
        _check.SetActive(matched);
        if (!matched || popDuration <= 0f) return;

        _check.transform.localScale = Vector3.zero;
        _pop = _check.transform.DOScale(_checkScale, popDuration)
            .SetDelay(delay)
            .SetEase(Ease.OutBack)
            .SetLink(gameObject);
    }
}
