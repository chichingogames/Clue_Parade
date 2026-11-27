using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Look at this clue": a large clue card (item icon + sentence) for the first unmatched assignment
// of the active category, where it goes, then "Show the next swipe" (Manager_Flow solves the
// current board and draws the guide) or "I'll try it" (closes, nothing changes).
public class PopUp_Hint : BasePopUp
{
    [SerializeField] private Image _clueIcon;
    [SerializeField] private TMP_Text _clueText;
    [SerializeField] private Button _showButton;
    [SerializeField] private Button _tryButton;

    public void Setup(string kicker, Sprite icon, string clue, string explanation, Action onShowSwipe, Action onTry)
    {
        SetTexts(kicker, "Look at this clue", explanation);
        _clueIcon.sprite = icon;
        _clueIcon.gameObject.SetActive(icon != null);
        _clueText.text = clue;
        Bind(_showButton, onShowSwipe);
        Bind(_tryButton, onTry);
    }
}
