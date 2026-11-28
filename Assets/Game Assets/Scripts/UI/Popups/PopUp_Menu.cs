using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Menu": the sound switch, Restart this case, Choose a case, Back to the game (resumes the exact board).
public class PopUp_Menu : BasePopUp
{
    [SerializeField] private Button _soundButton;
    [SerializeField] private TMP_Text _soundLabel;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _levelsButton;
    [SerializeField] private Button _backButton;

    public void Setup(bool soundOn, Action onSound, Action onRestart, Action onLevels, Action onBack)
    {
        SetTexts("CHICHINGO", "Menu", "");
        SetSound(soundOn);
        Bind(_soundButton, onSound);
        Bind(_restartButton, onRestart);
        Bind(_levelsButton, onLevels);
        Bind(_backButton, onBack);
    }

    public void SetSound(bool on) => _soundLabel.text = $"Sound effects: {(on ? "On" : "Off")}";
}
