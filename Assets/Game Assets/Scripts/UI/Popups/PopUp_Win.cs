using System;
using Chichingo.TilewrapVaried;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Case complete!": the completion line, the three subjects finished (the same rigs as the HUD,
// every part and extra delivered), the first-clear reward (or that it was collected before), the
// stats line (sets delivered, swaps used), a named preview of the next case ("UP NEXT"), Next case,
// and Choose a case. After the last case: Finish playtest. It has no close button: the player
// always leaves through an action.
public class PopUp_Win : BasePopUp
{
    public struct Subject
    {
        public TilewrapRigView Rig;
        public string Name;
        public UI_SubjectCard.Extra[] Extras;
    }

    [SerializeField] private UI_SubjectCard[] _subjects = new UI_SubjectCard[0];
    [SerializeField] private TMP_Text _reward;
    [SerializeField] private TMP_Text _rewardNote;
    [SerializeField] private GameObject _upNext;
    [SerializeField] private TMP_Text _upNextTitle;
    [SerializeField] private Button _nextButton;
    [SerializeField] private TMP_Text _nextLabel;
    [SerializeField] private Button _mapButton;

    // upNext null hides the preview card (the last case).
    public void Setup(string title, string completion, Subject[] subjects, string reward, string rewardNote,
                      string upNext, string nextLabel, Action onNext, Action onMap)
    {
        SetTexts("", title, completion);
        for (int i = 0; i < _subjects.Length; i++)
        {
            if (subjects == null || i >= subjects.Length) { _subjects[i].Hide(); continue; }
            _subjects[i].Show(subjects[i].Rig, subjects[i].Name, subjects[i].Extras);
            _subjects[i].ShowCompleted();
        }
        _reward.text = reward;
        _rewardNote.text = rewardNote;
        _upNext.SetActive(!string.IsNullOrEmpty(upNext));
        _upNextTitle.text = upNext ?? "";
        Bind(_nextButton, _nextLabel, nextLabel, onNext);
        Bind(_mapButton, onMap);
        _mapButton.gameObject.SetActive(onMap != null);
    }
}
