using DG.Tweening;
using TMPro;
using UnityEngine;

// A short message near the bottom of the HUD ("First, swipe the top row right."), shown for
// ToastSeconds then faded. A new message replaces the current one.
[RequireComponent(typeof(CanvasGroup))]
public class UI_Toast : MonoBehaviour
{
    private const float ToastSeconds = 2.7f;

    [SerializeField] private TMP_Text _text;

    private CanvasGroup _group;
    private Sequence _sequence;

    private void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }

    public void Show(string message)
    {
        _sequence?.Kill();
        _text.text = message;
        var rect = (RectTransform)transform;
        _sequence = DOTween.Sequence()
            .Append(_group.DOFade(1f, 0.2f))
            .Join(rect.DOScale(1f, 0.2f).From(0.92f))
            .AppendInterval(ToastSeconds)
            .Append(_group.DOFade(0f, 0.2f))
            .SetLink(gameObject);
    }
}
