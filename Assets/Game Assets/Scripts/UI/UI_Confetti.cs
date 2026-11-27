using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// The win confetti: Count tinted quads (plain uGUI Images, no texture) falling across the screen
// over FallSeconds with a drift and a spin, as in the prototype. Lives over the popups and under the
// loading screen; never blocks taps.
public class UI_Confetti : MonoBehaviour
{
    private const int Count = 26;
    private const float FallSeconds = 2.2f;
    private static readonly Color[] Colors =
    {
        new Color32(0xF3, 0x65, 0x55, 0xFF), new Color32(0x35, 0x8E, 0xE9, 0xFF), new Color32(0xA4, 0x5D, 0xE2, 0xFF),
        new Color32(0xFF, 0xD3, 0x3D, 0xFF), new Color32(0xE9, 0xF5, 0xCF, 0xFF),
    };

    public void Play()
    {
        var area = (RectTransform)transform;
        Vector2 size = area.rect.size;
        for (int i = 0; i < Count; i++)
        {
            var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(area, false);
            rect.sizeDelta = new Vector2(18f, 32f);
            var image = go.GetComponent<Image>();
            image.color = Colors[i % Colors.Length];
            image.raycastTarget = false;

            float x = (0.07f + i * 0.035f - 0.5f) * size.x;
            float drift = (i % 2 == 1 ? 1f : -1f) * (40f + i * 4f);
            rect.anchoredPosition = new Vector2(x, size.y * 0.5f + 40f);

            float delay = i % 5 * 0.06f;
            DOTween.Sequence()
                .AppendInterval(delay)
                .Append(rect.DOAnchorPos(new Vector2(x + drift, -size.y * 0.55f), FallSeconds).SetEase(Ease.InQuad))
                .Join(rect.DOLocalRotate(new Vector3(0f, 0f, 650f), FallSeconds, RotateMode.FastBeyond360))
                .Join(image.DOFade(0.2f, FallSeconds).SetEase(Ease.InQuad))
                .OnComplete(() => Destroy(go))
                .SetLink(go);
        }
    }
}
