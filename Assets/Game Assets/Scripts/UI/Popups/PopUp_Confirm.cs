using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A yes / no question ("Start a fresh test?").
public class PopUp_Confirm : BasePopUp
{
    [SerializeField] private Button _yesButton;
    [SerializeField] private TMP_Text _yesLabel;
    [SerializeField] private Button _noButton;
    [SerializeField] private TMP_Text _noLabel;

    public void Setup(string kicker, string title, string body, string yes, Action onYes, string no, Action onNo)
    {
        SetTexts(kicker, title, body);
        Bind(_yesButton, _yesLabel, yes, onYes);
        Bind(_noButton, _noLabel, no, onNo);
    }
}
