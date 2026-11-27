using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Playtest menu": the sound switch, Restart this level, Choose a level, Save results (JSON report),
// Start a fresh test (asks first), Back to the game (resumes the exact board).
public class PopUp_Menu : BasePopUp
{
    [SerializeField] private Button _soundButton;
    [SerializeField] private TMP_Text _soundLabel;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _levelsButton;
    [SerializeField] private Button _exportButton;
    [SerializeField] private Button _freshButton;
    [SerializeField] private Button _backButton;

    public void Setup(bool soundOn, Action onSound, Action onRestart, Action onLevels, Action onExport, Action onFresh, Action onBack)
    {
        SetTexts("CHICHINGO", "Playtest menu",
                 "Progress and feedback stay on this device. No ads or purchases are active in these five onboarding levels.");
        SetSound(soundOn);
        Bind(_soundButton, onSound);
        Bind(_restartButton, onRestart);
        Bind(_levelsButton, onLevels);
        Bind(_exportButton, onExport);
        Bind(_freshButton, onFresh);
        Bind(_backButton, onBack);
    }

    public void SetSound(bool on) => _soundLabel.text = $"Sound effects: {(on ? "On" : "Off")}";
}
