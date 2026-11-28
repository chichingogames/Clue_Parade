using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Hint": first the plain clue fact (bulb + the item's picture + the sentence) for the selected
// category's first unsatisfied item, then the next swipe from the CURRENT board in words ("Swipe the
// top row left." and which item wraps where). "Show the next swipe" closes it and draws the guide
// on the board; "I'll try it" just closes. Opening it costs no move.
public class PopUp_Hint : BasePopUp
{
    [SerializeField] private Image _clueIcon;
    [SerializeField] private TMP_Text _clueText;
    [SerializeField] private TMP_Text _swipeText;
    [SerializeField] private TMP_Text _wrapText;
    [SerializeField] private Button _showButton;
    [SerializeField] private Button _tryButton;

    // swipe null: nothing to guide (hides the swipe lines and the show button).
    public void Setup(Sprite icon, string clue, string swipe, string wrap, Action onShowSwipe, Action onTry)
    {
        SetTexts("", "Hint", "");
        _clueIcon.sprite = icon;
        _clueIcon.gameObject.SetActive(icon != null);
        _clueText.text = clue;
        bool hasSwipe = !string.IsNullOrEmpty(swipe);
        _swipeText.gameObject.SetActive(hasSwipe);
        _wrapText.gameObject.SetActive(hasSwipe && !string.IsNullOrEmpty(wrap));
        _swipeText.text = swipe ?? "";
        _wrapText.text = wrap ?? "";
        Bind(_showButton, null, null, hasSwipe ? onShowSwipe : null);
        Bind(_tryButton, onTry);
    }
}
