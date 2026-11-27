using System;
using DG.Tweening;
using UnityEngine;

// A popup's identity and its show / close animation (skeleton.md §9.2). The root is full screen;
// _dim (a raycast-blocking backdrop) fades, _holder (the panel) scales. Manager_PopUp collects every
// Popup under Canvas/Container/PopUps and is the only caller of Show / Close.
public class Popup : MonoBehaviour
{
    [SerializeField] private PopupName _popupName;
    [Tooltip("The panel that scales in and out (the dim backdrop does not scale).")]
    [SerializeField] private RectTransform _holder;
    [Tooltip("Full-screen backdrop; blocks taps on the HUD while open.")]
    [SerializeField] private CanvasGroup _dim;
    [SerializeField] private bool _isAnimated = true;
    [SerializeField, Min(0.01f)] private float _showDuration = 0.28f;
    [SerializeField, Min(0.01f)] private float _closeDuration = 0.16f;

    public PopupName Name => _popupName;
    public bool IsOpen { get; private set; }
    public bool IsClosing { get; private set; }

    private Action _onClosed;
    private Sequence _animation;

    public void Show(Action onClosed)
    {
        _animation?.Kill();
        _onClosed = onClosed;
        IsOpen = true;
        IsClosing = false;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        OnShown();

        if (!_isAnimated)
        {
            _holder.localScale = Vector3.one;
            if (_dim != null) _dim.alpha = 1f;
            OnShowComplete();
            return;
        }

        _holder.localScale = Vector3.one * 0.6f;
        if (_dim != null) _dim.alpha = 0f;
        _animation = DOTween.Sequence()
            .Append(_holder.DOScale(1f, _showDuration).SetEase(Ease.OutBack))
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(OnShowComplete);
        if (_dim != null) _animation.Join(_dim.DOFade(1f, _showDuration * 0.7f));
    }

    // done: after the panel is gone (and after the onClosed passed to Show, exactly once).
    public void Close(Action done)
    {
        if (!IsOpen) { done?.Invoke(); return; }
        IsOpen = false;
        IsClosing = true;
        _animation?.Kill();

        void Finish()
        {
            IsClosing = false;
            gameObject.SetActive(false);
            var closed = _onClosed;
            _onClosed = null;
            closed?.Invoke();
            done?.Invoke();
        }

        if (!_isAnimated || !gameObject.activeInHierarchy) { Finish(); return; }
        _animation = DOTween.Sequence()
            .Append(_holder.DOScale(0.7f, _closeDuration).SetEase(Ease.InBack))
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(Finish);
        if (_dim != null) _animation.Join(_dim.DOFade(0f, _closeDuration));
    }

    protected virtual void OnShown() { }
    protected virtual void OnShowComplete() { }
}
