using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Case complete!": the case question answered (icon + answer), the finished scene (the three
// subjects, every part delivered; the festival star after the last case), an optional story card
// (case 5: something is missing), the evidence added to the case file, the first-clear reward (or
// that it was collected before), the stats line, the next case with its lead ("UP NEXT"), Next case
// and Choose a case. After the last case, Next case opens case 1. It has no close button: the player
// always leaves through an action.
public class PopUp_Win : BasePopUp
{
    public struct Content
    {
        public string Kicker;
        public string Title;
        public string Completion;               // shown when there is no evidence card
        public string Question;
        public Sprite AnswerIcon;
        public string Answer;
        public UI_SubjectCard.Setup[] Subjects; // null: show the star instead
        public Sprite Star;
        public string StoryTitle;               // null: no story card
        public string StoryText;
        public Sprite EvidenceIcon;
        public string Evidence;                 // null: no evidence card
        public string Reward;
        public string Stats;
        public string UpNext;
        public string UpNextLine;
        public string NextLabel;
    }

    [SerializeField] private TMP_Text _question;
    [SerializeField] private Image _answerIcon;
    [SerializeField] private TMP_Text _answer;
    [SerializeField] private GameObject _sceneRoot;
    [SerializeField] private UI_SubjectCard[] _subjects = new UI_SubjectCard[0];
    [SerializeField] private Image _star;
    [SerializeField] private GameObject _story;
    [SerializeField] private TMP_Text _storyTitle;
    [SerializeField] private TMP_Text _storyText;
    [SerializeField] private GameObject _evidence;
    [SerializeField] private Image _evidenceIcon;
    [SerializeField] private TMP_Text _evidenceText;
    [SerializeField] private TMP_Text _reward;
    [SerializeField] private TMP_Text _rewardNote;
    [SerializeField] private GameObject _upNext;
    [SerializeField] private TMP_Text _upNextTitle;
    [SerializeField] private TMP_Text _upNextLine;
    [SerializeField] private Button _nextButton;
    [SerializeField] private TMP_Text _nextLabel;
    [SerializeField] private Button _mapButton;

    public void Setup(Content content, Action onNext, Action onMap)
    {
        SetTexts(content.Kicker, content.Title, string.IsNullOrEmpty(content.Evidence) ? content.Completion : null);
        _question.text = content.Question;
        _answer.text = content.Answer;
        _answerIcon.sprite = content.AnswerIcon;
        _answerIcon.gameObject.SetActive(content.AnswerIcon != null);

        bool star = content.Subjects == null;
        _star.gameObject.SetActive(star);
        if (star) _star.sprite = content.Star;
        for (int i = 0; i < _subjects.Length; i++)
        {
            if (star || i >= content.Subjects.Length) { _subjects[i].Hide(); continue; }
            _subjects[i].Show(content.Subjects[i]);
            _subjects[i].ShowCompleted();
        }

        _story.SetActive(!string.IsNullOrEmpty(content.StoryTitle));
        _storyTitle.text = content.StoryTitle ?? "";
        _storyText.text = content.StoryText ?? "";
        _evidence.SetActive(!string.IsNullOrEmpty(content.Evidence));
        _evidenceIcon.sprite = content.EvidenceIcon;
        _evidenceIcon.gameObject.SetActive(content.EvidenceIcon != null);
        _evidenceText.text = content.Evidence ?? "";

        _reward.text = content.Reward;
        _rewardNote.text = content.Stats;
        _upNext.SetActive(!string.IsNullOrEmpty(content.UpNext));
        _upNextTitle.text = content.UpNext ?? "";
        _upNextLine.text = content.UpNextLine ?? "";
        Bind(_nextButton, _nextLabel, content.NextLabel, onNext);
        Bind(_mapButton, onMap);
        _mapButton.gameObject.SetActive(onMap != null);
    }
}
