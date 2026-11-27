using DG.Tweening;
using UnityEngine;

// The swipe guide drawn over the board (Board/Guide): a gold band over the row or column, an arrow
// pointing the way, and a hand that slides along it on a loop (handoff: guideLoopSeconds 1.7). Used
// for a level's required first swipe and for the Hint's next swipe. Pure view: Manager_Board
// places it in board-local units.
public class BoardGuide : MonoBehaviour
{
    private const float LoopSeconds = 1.7f;

    [SerializeField] private SpriteRenderer _band;
    [SerializeField] private SpriteRenderer _arrow;
    [SerializeField] private SpriteRenderer _hand;

    private Sequence _loop;

    // center / bandSize in board-local units; direction: unit vector of the swipe (x right, y up).
    public void Show(Vector3 center, Vector2 bandSize, Vector2 direction, float travel)
    {
        gameObject.SetActive(true);
        _band.transform.localPosition = center;
        _band.size = bandSize;

        _arrow.transform.localPosition = center;
        _arrow.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        // The hand's pivot is its fingertip: it slides through the band's centre, a little off the arrow.
        Vector3 side = new Vector3(-direction.y, direction.x, 0f) * -0.35f;
        Vector3 from = center + side - (Vector3)(direction * travel * 0.5f);
        Vector3 to = center + side + (Vector3)(direction * travel * 0.5f);

        _loop?.Kill();
        var hand = _hand.transform;
        _loop = DOTween.Sequence()
            .AppendCallback(() => { hand.localPosition = from; SetHandAlpha(0f); })
            .AppendInterval(LoopSeconds * 0.12f)
            .Append(_hand.DOFade(1f, LoopSeconds * 0.13f))
            .Join(hand.DOLocalMove(Vector3.Lerp(from, to, 0.25f), LoopSeconds * 0.13f))
            .Append(hand.DOLocalMove(to, LoopSeconds * 0.5f).SetEase(Ease.InOutSine))
            .Append(_hand.DOFade(0f, LoopSeconds * 0.25f))
            .SetLoops(-1)
            .SetLink(gameObject);
    }

    public void Hide()
    {
        _loop?.Kill();
        _loop = null;
        gameObject.SetActive(false);
    }

    private void SetHandAlpha(float alpha)
    {
        var c = _hand.color;
        _hand.color = new Color(c.r, c.g, c.b, alpha);
    }
}
