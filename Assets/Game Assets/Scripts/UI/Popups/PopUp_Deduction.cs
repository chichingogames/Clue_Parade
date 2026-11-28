using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Final deduction" on the milestone cases (15, 20, 25, 30), between the last delivery and the win:
// the question, its evidence (all free), a feedback line and the answers. A wrong answer costs
// nothing: the flow disables it and says so; the right one closes the case.
public class PopUp_Deduction : BasePopUp
{
    [SerializeField] private TMP_Text _evidence;
    [SerializeField] private TMP_Text _feedback;
    [SerializeField] private Button[] _options = new Button[0];
    [SerializeField] private TMP_Text[] _optionLabels = new TMP_Text[0];

    // onAnswer gets the option index.
    public void Setup(string question, string evidence, string[] options, Action<int> onAnswer)
    {
        SetTexts("FINAL DEDUCTION · ALL EVIDENCE IS FREE", question, null);
        _evidence.text = evidence;
        SetFeedback("Use both facts to close this part of the case.");
        for (int i = 0; i < _options.Length; i++)
        {
            int index = i;
            bool has = options != null && i < options.Length;
            _options[i].interactable = true;
            Bind(_options[i], _optionLabels[i], has ? options[i] : null, has ? () => onAnswer(index) : (Action)null);
        }
    }

    public void SetFeedback(string text) => _feedback.text = text;

    public void Disable(int option)
    {
        if (option >= 0 && option < _options.Length) _options[option].interactable = false;
    }
}
