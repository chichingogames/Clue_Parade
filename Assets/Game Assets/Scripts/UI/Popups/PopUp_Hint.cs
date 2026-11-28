using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "A helpful next swap": the two tiles to swap (the item and its neighbour, or "Empty space"), the
// instruction ("Swipe the purple cap right."), why it helps, "Show me on the board" (closes it; the
// flow draws the guide) and "I'll try it" (just closes). Opening it costs no move.
public class PopUp_Hint : BasePopUp
{
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Image _otherIcon;
    [Tooltip("\"Empty space\", shown instead of _otherIcon when the neighbour cell is empty.")]
    [SerializeField] private GameObject _emptyLabel;
    [SerializeField] private TMP_Text _instruction;
    [SerializeField] private Button _showButton;
    [SerializeField] private Button _tryButton;

    // other null: the neighbour cell is empty.
    public void Setup(Sprite item, Sprite other, string instruction, string why, Action onShow, Action onTry)
    {
        SetTexts("HINT", "A helpful next swap", why);
        _itemIcon.sprite = item;
        _otherIcon.sprite = other;
        _otherIcon.gameObject.SetActive(other != null);
        _emptyLabel.SetActive(other == null);
        _instruction.text = instruction;
        Bind(_showButton, onShow);
        Bind(_tryButton, onTry);
    }
}
