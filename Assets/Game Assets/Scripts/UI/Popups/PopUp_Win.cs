using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Everyone's ready!": the payoff line, the fully dressed crew, 9/9 matched, the first-clear reward
// (or that it was already collected), the move stats, and Next (or Finish) plus Choose a level.
// It has no close button: the player always leaves through one of its actions.
public class PopUp_Win : BasePopUp
{
    [SerializeField] private UI_CrewPortrait[] _crew = new UI_CrewPortrait[0];
    [SerializeField] private TMP_Text _matched;
    [SerializeField] private TMP_Text _reward;
    [SerializeField] private TMP_Text _stats;
    [SerializeField] private Button _nextButton;
    [SerializeField] private TMP_Text _nextLabel;
    [SerializeField] private Button _levelsButton;

    public void Setup(string kicker, string payoff, UI_CrewPortrait.Entry[] crew, string matched, string reward, string stats,
                      string nextLabel, Action onNext, Action onLevels)
    {
        SetTexts(kicker, "Everyone’s ready!", payoff);
        UI_CrewPortrait.ShowRow(_crew, crew, dressed: true);
        _matched.text = matched;
        _reward.text = reward;
        _stats.text = stats;
        Bind(_nextButton, _nextLabel, nextLabel, onNext);
        Bind(_levelsButton, onLevels);
    }
}
