using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The case map, "The missing festival star": a heading per chapter, then one row per case (number,
// or a check once cleared; title with an EXPERT marker; rows, items, sets and moves), the current
// case highlighted, and Back. All cases are open (the playtest's test mode). Rows and headings are
// spawned from inactive templates inside the scrolling list. Registered as PopupName.LevelSelect.
public class PopUp_Cases : BasePopUp
{
    public struct Row
    {
        public string Heading;     // non-null: a chapter heading instead of a case
        public int Level;
        public string Title;
        public string Subtitle;
        public bool Done;
        public bool Current;
    }

    [SerializeField] private UI_ChoiceRow _rowTemplate;
    [SerializeField] private TMP_Text _headingTemplate;
    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private Button _backButton;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private RectTransform _current;

    public void Setup(string kicker, string title, string body, IReadOnlyList<Row> rows, Action<int> onPick, Action onBack)
    {
        SetTexts(kicker, title, body);
        foreach (var go in _spawned) Destroy(go);
        _spawned.Clear();
        _rowTemplate.gameObject.SetActive(false);
        _headingTemplate.gameObject.SetActive(false);
        RectTransform current = null;
        foreach (var row in rows)
        {
            if (row.Heading != null)
            {
                var heading = Instantiate(_headingTemplate, _headingTemplate.transform.parent);
                heading.gameObject.SetActive(true);
                heading.text = row.Heading;
                _spawned.Add(heading.gameObject);
                continue;
            }
            int level = row.Level;
            var view = Instantiate(_rowTemplate, _rowTemplate.transform.parent);
            _spawned.Add(view.gameObject);
            view.Setup(level.ToString(), row.Title, row.Subtitle, row.Done, row.Current, () => onPick(level));
            if (row.Current) current = (RectTransform)view.transform;
        }
        Bind(_backButton, onBack);
        _current = current;
    }

    // Once the panel is up (its layout is live), bring the current case into view.
    protected override void OnShowComplete()
    {
        base.OnShowComplete();
        if (_scroll != null) ScrollTo(_current);
    }

    // Brings the current case into view (after the layout has placed the rows).
    private void ScrollTo(RectTransform row)
    {
        Canvas.ForceUpdateCanvases();
        var content = _scroll.content;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        float height = content.rect.height - _scroll.viewport.rect.height;
        if (row == null || height <= 0f) { _scroll.verticalNormalizedPosition = 1f; return; }
        float y = -row.anchoredPosition.y - row.rect.height * 2f;
        _scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(y / height);
    }
}
