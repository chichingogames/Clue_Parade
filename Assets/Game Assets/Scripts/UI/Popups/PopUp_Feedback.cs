using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "How did it feel?" after the last level: optional answers to three questions (goal clarity,
// wrapping, difficulty), one answer each, then Save feedback (and the report) or Back to the levels.
// Questions and answers are spawned from inactive templates inside the scrolling list.
public class PopUp_Feedback : BasePopUp
{
    public struct Question
    {
        public string Key;
        public string Title;
        public (string value, string label)[] Options;
        public string Selected;     // a value, or "" for none
    }

    [SerializeField] private TMP_Text _questionTemplate;
    [SerializeField] private UI_ChoiceRow _optionTemplate;
    [SerializeField] private Button _saveButton;
    [SerializeField] private Button _backButton;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly Dictionary<string, string> _answers = new Dictionary<string, string>();

    public void Setup(IReadOnlyList<Question> questions, Action<IReadOnlyDictionary<string, string>> onSave, Action onBack)
    {
        SetTexts("5/5 LEVELS COMPLETE", "How did it feel?",
                 "You’ve finished the five-level playtest. A little feedback helps us improve the onboarding. Optional.");
        foreach (var go in _spawned) Destroy(go);
        _spawned.Clear();
        _answers.Clear();
        _questionTemplate.gameObject.SetActive(false);
        _optionTemplate.gameObject.SetActive(false);

        var parent = _questionTemplate.transform.parent;
        foreach (var question in questions)
        {
            string key = question.Key;
            _answers[key] = question.Selected ?? "";
            var title = Instantiate(_questionTemplate, parent);
            title.gameObject.SetActive(true);
            title.text = question.Title;
            _spawned.Add(title.gameObject);

            var rows = new List<(UI_ChoiceRow row, string value)>();
            for (int i = 0; i < question.Options.Length; i++)
            {
                var (value, label) = question.Options[i];
                var row = Instantiate(_optionTemplate, parent);
                _spawned.Add(row.gameObject);
                rows.Add((row, value));
                row.Setup(((char)('A' + i)).ToString(), label, null, false, value == _answers[key], () =>
                {
                    _answers[key] = value;
                    foreach (var (r, v) in rows) r.SetSelected(v == value);
                });
            }
        }
        Bind(_saveButton, () => onSave(_answers));
        Bind(_backButton, onBack);
    }
}
