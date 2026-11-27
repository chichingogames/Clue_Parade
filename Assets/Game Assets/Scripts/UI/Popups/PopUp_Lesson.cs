using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A level's lesson (levels 1, 3, 4): kicker, title, the lesson's paragraphs, an illustration
// (the neutral crew, the category icons, or the move counter) and one button. Shown once per
// lesson id; Manager_Flow saves the acknowledgement when the button is tapped.
public class PopUp_Lesson : BasePopUp
{
    [SerializeField] private UI_CrewPortrait[] _crew = new UI_CrewPortrait[0];
    [SerializeField] private GameObject _crewRow;
    [Tooltip("The \"12 moves left\" counter illustration.")]
    [SerializeField] private GameObject _movesExample;
    [SerializeField] private TMP_Text _movesValue;
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _buttonLabel;

    // crew: null for no portraits; moves > 0 shows the counter illustration.
    public void Setup(string kicker, LevelLesson lesson, UI_CrewPortrait.Entry[] crew, int moves, Action onDone)
    {
        var paragraphs = lesson.paragraphs ?? new string[0];
        var body = new System.Text.StringBuilder();
        for (int i = 0; i < paragraphs.Length; i++)
        {
            if (i > 0) body.Append("\n\n");
            body.Append(i == 0 ? $"<b>{paragraphs[i]}</b>" : paragraphs[i]);
        }
        SetTexts(kicker, lesson.title, body.ToString());

        _crewRow.SetActive(crew != null);
        UI_CrewPortrait.ShowRow(_crew, crew, dressed: false);
        _movesExample.SetActive(moves > 0);
        _movesValue.text = moves.ToString();
        Bind(_button, _buttonLabel, string.IsNullOrEmpty(lesson.button) ? "Got it" : lesson.button, onDone);
    }
}
