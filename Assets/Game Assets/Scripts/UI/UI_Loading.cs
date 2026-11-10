using DG.Tweening;
using UnityEngine;

// The loading screen, once per app start, over everything (last child of Canvas, outside the
// safe area so the art covers the whole screen). The studio splash comes before it, outside this script.
//   1. The progress bar fills over Tuning.LoadingDuration.
//   2. Manager_Game.Launch() opens the saved level, and the screen fades out over
//      Tuning.LoadingFadeDuration while the board and HUD intros play. Then this object deactivates.
//
// The bar is timed, not tied to real work: everything lives in one scene and a level builds in a
// frame. Holds the launch in Awake (managers are initialized by then, SingletonManager runs at -900),
// so without an active loading screen the game opens straight away.
// Built by ShiftSort > Build Loading Screen (UIBuilder.BuildLoading). Timing: GameTuning > Loading screen.
[RequireComponent(typeof(CanvasGroup))]
public class UI_Loading : MonoBehaviour
{
    [Tooltip("The bar's fill; its right anchor is the progress. Starts as a dot (as wide as it is tall).")]
    [SerializeField] private RectTransform _progressFill;

    [Tooltip("Editor only: skip the loading screen, so Play opens the level straight away.")]
    [SerializeField] private bool _skipInEditor;

    private CanvasGroup _root;
    private float _progress;
    private float _minFill;

    private void Awake()
    {
#if UNITY_EDITOR
        if (_skipInEditor)
        {
            gameObject.SetActive(false);
            return;
        }
#endif
        _root = GetComponent<CanvasGroup>();
        Manager_Game.Instance.HoldLaunch();
    }

    private void Start()
    {
        _root.alpha = 1f;
        _root.blocksRaycasts = true;       // the HUD stays untouchable until the game opens

        var area = (RectTransform)_progressFill.parent;
        Canvas.ForceUpdateCanvases();
        _minFill = area.rect.width > 0f ? Mathf.Clamp01(area.rect.height / area.rect.width) : 0f;
        SetProgress(0f);

        DOTween.Sequence()
            .Append(DOTween.To(() => _progress, SetProgress, 1f, Tuning.LoadingDuration).SetEase(Ease.InOutSine))
            .AppendCallback(() =>
            {
                _root.blocksRaycasts = false;
                Manager_Game.Instance.Launch();
            })
            .Append(_root.DOFade(0f, Tuning.LoadingFadeDuration))
            .OnComplete(() => gameObject.SetActive(false))
            .SetLink(gameObject);
    }

    private void SetProgress(float progress)
    {
        _progress = progress;
        var max = _progressFill.anchorMax;
        max.x = Mathf.Lerp(_minFill, 1f, progress);
        _progressFill.anchorMax = max;
    }
}
