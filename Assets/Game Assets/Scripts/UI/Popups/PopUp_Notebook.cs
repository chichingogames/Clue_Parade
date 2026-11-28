using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Your notebook": every set of the case in order, each with its status (Delivered / Active /
// Upcoming; the request in focus highlighted) and all three facts (the two shown clues first,
// then the third). Built with as many sections and facts as the largest case needs; unused ones
// hide. Opening it never costs a move; closing keeps the board.
public class PopUp_Notebook : BasePopUp
{
    public struct Section
    {
        public string Label;
        public string Status;
        public bool Current;
        public (Sprite icon, string text)[] Facts;
    }

    [Serializable]
    public struct SectionView
    {
        public GameObject Root;
        public Image Surface;
        public TMP_Text Label;
        public TMP_Text Status;
        public Image[] Icons;
        public TMP_Text[] Texts;
    }

    [SerializeField] private SectionView[] _sections = new SectionView[0];
    [SerializeField] private Color _normal = new Color32(0xFF, 0xF8, 0xE9, 0xFF);
    [SerializeField] private Color _current = new Color32(0xFF, 0xF1, 0xBE, 0xFF);
    [SerializeField] private Button _backButton;

    public void Setup(string rules, Section[] sections, Action onBack)
    {
        SetTexts("EVERY CLUE, IN ONE PLACE", "Your notebook", rules);
        for (int i = 0; i < _sections.Length; i++)
        {
            var view = _sections[i];
            bool has = i < sections.Length;
            view.Root.SetActive(has);
            if (!has) continue;
            var section = sections[i];
            view.Label.text = section.Label;
            view.Status.text = section.Status.ToUpperInvariant();
            view.Surface.color = section.Current ? _current : _normal;
            for (int f = 0; f < view.Texts.Length; f++)
            {
                bool fact = f < section.Facts.Length;
                view.Texts[f].transform.parent.gameObject.SetActive(fact);
                if (!fact) continue;
                view.Icons[f].sprite = section.Facts[f].icon;
                view.Icons[f].gameObject.SetActive(section.Facts[f].icon != null);
                view.Texts[f].text = section.Facts[f].text;
            }
        }
        Bind(_backButton, onBack);
    }
}
