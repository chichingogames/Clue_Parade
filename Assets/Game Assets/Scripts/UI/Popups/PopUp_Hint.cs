using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Need a little help?" (clues are always free): "Read it another way" explains the current clues,
// then the next-swap aid ("Show next swap · free" or · 30 coins; it only highlights the move, the
// player makes it), "Open free notebook", the coin balance and Back. Opening it costs no move.
public class PopUp_Hint : BasePopUp
{
    [SerializeField] private TMP_Text _explanation;
    [SerializeField] private Button _showButton;
    [SerializeField] private TMP_Text _showLabel;
    [SerializeField] private Button _notebookButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private TMP_Text _caption;

    public void Setup(string explanation, string showLabel, bool canShow, string caption,
                      Action onShow, Action onNotebook, Action onBack)
    {
        SetTexts("CLUES ARE ALWAYS FREE", "Need a little help?",
                 "A next-swap hint highlights one useful move. It does not make the move for you.");
        _explanation.text = explanation;
        Bind(_showButton, _showLabel, showLabel, onShow);
        _showButton.interactable = canShow;
        Bind(_notebookButton, onNotebook);
        Bind(_backButton, onBack);
        _caption.text = caption;
    }
}
