using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Detective notebook" with two tabs: Clues (how the board works, the profile facts, the shared case
// clues, each set's clues, the confirmed scene facts) and Case file (the case question, its
// evidence and witness statements, the investigation and the evidence earned in earlier cases).
// Sections and their lines are spawned from inactive templates inside the scrolling list; a line has
// an optional icon and is ticked when confirmed. Opening it never costs a move.
public class PopUp_Notebook : BasePopUp
{
    public struct Line
    {
        public Sprite Icon;
        public string Text;
        public bool Confirmed;
    }

    public struct Section
    {
        public string Title;
        public string Status;
        public bool Current;
        public List<Line> Lines;
    }

    [SerializeField] private Button[] _tabs = new Button[0];
    [SerializeField] private Color _tabSelected = new Color32(0xFF, 0xDC, 0x61, 0xFF);
    [SerializeField] private Color _tabNormal = new Color32(0xF3, 0xE4, 0xC6, 0xFF);
    [SerializeField] private RectTransform _sectionTemplate;
    [SerializeField] private RectTransform _lineTemplate;
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private Color _normal = new Color32(0xFF, 0xF8, 0xE9, 0xFF);
    [SerializeField] private Color _current = new Color32(0xFF, 0xF1, 0xBE, 0xFF);
    [SerializeField] private Color _confirmed = new Color32(0x3D, 0x7D, 0x3A, 0xFF);
    [SerializeField] private Button _backButton;

    private readonly List<GameObject> _spawned = new List<GameObject>();

    // tab: 0 = Clues, 1 = Case file; onTab re-opens it on the other tab.
    public void Setup(int tab, string intro, IReadOnlyList<Section> sections, Action<int> onTab, Action onBack)
    {
        SetTexts("CLUES & CASE FILE", "Detective notebook", intro);
        for (int i = 0; i < _tabs.Length; i++)
        {
            int index = i;
            ((Image)_tabs[i].targetGraphic).color = i == tab ? _tabSelected : _tabNormal;
            Bind(_tabs[i], () => onTab(index));
        }

        foreach (var go in _spawned) Destroy(go);
        _spawned.Clear();
        _sectionTemplate.gameObject.SetActive(false);
        _lineTemplate.gameObject.SetActive(false);
        foreach (var section in sections)
        {
            var view = Instantiate(_sectionTemplate, _sectionTemplate.parent);
            view.gameObject.SetActive(true);
            _spawned.Add(view.gameObject);
            view.GetComponent<Image>().color = section.Current ? _current : _normal;
            var header = view.Find("Header");
            header.Find("Title").GetComponent<TMP_Text>().text = section.Title;
            var status = header.Find("Status").GetComponent<TMP_Text>();
            status.text = (section.Status ?? "").ToUpperInvariant();
            foreach (var line in section.Lines)
            {
                var row = Instantiate(_lineTemplate, view);
                row.gameObject.SetActive(true);
                var icon = row.Find("Icon").GetComponent<Image>();
                icon.sprite = line.Icon;
                icon.gameObject.SetActive(line.Icon != null);
                var text = row.Find("Text").GetComponent<TMP_Text>();
                text.text = line.Confirmed ? $"{line.Text} <size=70%><color=#{ColorUtility.ToHtmlStringRGB(_confirmed)}><b>CONFIRMED</b></color></size>" : line.Text;
            }
        }
        if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        Bind(_backButton, onBack);
    }
}
