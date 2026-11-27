using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared popup content (skeleton.md §9.2): the kicker / title / body texts every dialog has, a
// staggered pop-in of _gameObjectToAnimate after the panel lands, an optional close button, and
// Bind (clears old listeners, plays the tap). Subclasses fill their own widgets in a Setup method
// called by Manager_Flow before ShowPopup, with callbacks for their buttons: popups display and
// report taps, the manager decides.
public abstract class BasePopUp : Popup
{
    [SerializeField] protected TMP_Text _kicker;
    [SerializeField] protected TMP_Text _title;
    [SerializeField] protected TMP_Text _body;
    [Tooltip("Scaled in one after another once the panel has landed.")]
    [SerializeField] private RectTransform[] _gameObjectToAnimate = new RectTransform[0];
    [Tooltip("Optional. Closes the popup (Manager_PopUp.ClosePopup) when tapped.")]
    [SerializeField] private Button _closeButton;

    protected override void OnShown()
    {
        if (_closeButton != null) Bind(_closeButton, Dismiss);
        foreach (var item in _gameObjectToAnimate)
            if (item != null) item.localScale = Vector3.zero;
    }

    protected override void OnShowComplete()
    {
        for (int i = 0; i < _gameObjectToAnimate.Length; i++)
        {
            var item = _gameObjectToAnimate[i];
            if (item == null) continue;
            item.DOKill();
            item.DOScale(1f, 0.22f).SetDelay(i * 0.05f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(item.gameObject);
        }
    }

    protected void SetTexts(string kicker, string title, string body)
    {
        if (_kicker != null) _kicker.text = kicker;
        if (_title != null) _title.text = title;
        if (_body != null)
        {
            _body.text = body ?? "";
            _body.gameObject.SetActive(!string.IsNullOrEmpty(body));
        }
    }

    protected static void Bind(Button button, Action onClick)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            Manager_Audio.Instance?.PlaySound(SoundType.UiTap);
            Haptics.Play(HapticType.Light);
            onClick?.Invoke();
        });
    }

    // Binds a button and sets its label; hidden when action is null.
    protected static void Bind(Button button, TMP_Text label, string text, Action onClick)
    {
        button.gameObject.SetActive(onClick != null);
        if (onClick == null) return;
        if (label != null) label.text = text;
        Bind(button, onClick);
    }

    protected void Dismiss() => Manager_PopUp.Instance.ClosePopup(Name);
}
