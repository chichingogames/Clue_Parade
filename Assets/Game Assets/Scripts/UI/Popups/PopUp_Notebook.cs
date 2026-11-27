using System;
using UnityEngine;
using UnityEngine.UI;

// "Your outfit notebook": the goal (the fully dressed crew), all nine assignments grouped by
// category with what is matched now, and the rules in plain words. A reference: closing it keeps
// the board and moves. The clue text scrolls; the button stays reachable.
public class PopUp_Notebook : BasePopUp
{
    [SerializeField] private UI_CrewPortrait[] _crew = new UI_CrewPortrait[0];
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private Button _backButton;

    public void Setup(UI_CrewPortrait.Entry[] crew, string clues, Action onBack)
    {
        SetTexts("OUTFITS TO MAKE", "Your outfit notebook", clues);
        UI_CrewPortrait.ShowRow(_crew, crew, dressed: true);
        Bind(_backButton, onBack);
        if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
    }
}
