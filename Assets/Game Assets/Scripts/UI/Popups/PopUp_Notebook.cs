using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Notebook": all nine facts as a grid, the subject names over the columns and each category with
// its three items (the same picture and name as the board) under the subject they belong to. A
// small check marks an item that is in place now; a fact stays readable after a mismatch. Opening
// it never costs a move; closing keeps the board.
public class PopUp_Notebook : BasePopUp
{
    public struct Entry
    {
        public Sprite Icon;
        public string Label;
        public bool Matched;
    }

    [Tooltip("Subject names over the columns, left to right.")]
    [SerializeField] private TMP_Text[] _names = new TMP_Text[0];
    [Tooltip("Category headings, top to bottom.")]
    [SerializeField] private TMP_Text[] _headings = new TMP_Text[0];
    [Tooltip("Row-major, one per board cell.")]
    [SerializeField] private Image[] _icons = new Image[0];
    [SerializeField] private TMP_Text[] _labels = new TMP_Text[0];
    [SerializeField] private GameObject[] _checks = new GameObject[0];
    [SerializeField] private Button _backButton;

    public void Setup(string[] names, string[] categories, Entry[] entries, string rules, Action onBack)
    {
        SetTexts("", "Notebook", rules);
        for (int i = 0; i < _names.Length; i++) _names[i].text = i < names.Length ? names[i] : "";
        for (int i = 0; i < _headings.Length; i++) _headings[i].text = i < categories.Length ? categories[i].ToUpperInvariant() : "";
        for (int i = 0; i < _icons.Length; i++)
        {
            bool has = i < entries.Length;
            _icons[i].gameObject.SetActive(has && entries[i].Icon != null);
            if (has) _icons[i].sprite = entries[i].Icon;
            _labels[i].text = has ? entries[i].Label : "";
            _checks[i].SetActive(has && entries[i].Matched);
        }
        Bind(_backButton, onBack);
    }
}
