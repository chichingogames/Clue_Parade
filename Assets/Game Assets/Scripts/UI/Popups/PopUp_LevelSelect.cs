using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// "The big parade": one row per level (number, or a check when cleared; title; "Learn · no move
// limit" / "Practice · 12 moves"), the current level highlighted. Every level is selectable.
public class PopUp_LevelSelect : BasePopUp
{
    public struct Row
    {
        public int Level;
        public string Title;
        public string Subtitle;
        public bool Done;
        public bool Current;
    }

    [SerializeField] private UI_ChoiceRow _rowTemplate;
    [SerializeField] private Button _backButton;

    private readonly List<UI_ChoiceRow> _rows = new List<UI_ChoiceRow>();

    public void Setup(string body, IReadOnlyList<Row> rows, Action<int> onPick, Action onBack)
    {
        SetTexts("5-LEVEL ONBOARDING PLAYTEST", "The big parade", body);
        _rowTemplate.gameObject.SetActive(false);
        while (_rows.Count < rows.Count)
            _rows.Add(Instantiate(_rowTemplate, _rowTemplate.transform.parent));
        for (int i = 0; i < _rows.Count; i++)
        {
            if (i >= rows.Count) { _rows[i].gameObject.SetActive(false); continue; }
            var row = rows[i];
            _rows[i].name = $"Level {row.Level}";
            _rows[i].Setup(row.Level.ToString(), row.Title, row.Subtitle, row.Done, row.Current, () => onPick(row.Level));
        }
        Bind(_backButton, onBack);
    }
}
