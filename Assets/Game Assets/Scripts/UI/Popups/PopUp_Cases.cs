using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Choose a case": one row per case (number, or a check once cleared; title; what it introduces),
// the current case highlighted, and Back. All cases are open (the playtest has no locks). Rows are
// spawned from the inactive template inside the scrolling list. Registered as PopupName.LevelSelect.
public class PopUp_Cases : BasePopUp
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

    private readonly List<GameObject> _spawned = new List<GameObject>();

    public void Setup(string progress, IReadOnlyList<Row> rows, Action<int> onPick, Action onBack)
    {
        SetTexts("ANY ROW · TWO CLUES", "Choose a case", progress);
        foreach (var go in _spawned) Destroy(go);
        _spawned.Clear();
        _rowTemplate.gameObject.SetActive(false);
        foreach (var row in rows)
        {
            int level = row.Level;
            var view = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
            _spawned.Add(view.gameObject);
            view.Setup(level.ToString(), row.Title, row.Subtitle, row.Done, row.Current, () => onPick(level));
        }
        Bind(_backButton, onBack);
    }
}
