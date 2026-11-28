using System;
using System.Collections.Generic;
using System.Linq;
using Chichingo.TilewrapVaried;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One subject (a board column's person, pet, cake, stall...) drawn from its registered rig: one base
// plus parts, each shown once its item has been delivered (TilewrapRigView bindings: expectedItem),
// plus the case's extra layers (refill items such as gifts or drinks, drawn on the rig at their
// rect). The HUD shows the scene so far above each column; the win popup shows it finished. A new
// part fades in (Tuning.SlotFadeDuration) and the card pops. A focused card (a tapped clue's owner)
// glows, and a scene clue's prop pulses. Pure view: the HUD and Manager_Flow hand it the rig prefab,
// the name, the extras and what has been delivered.
public class UI_SubjectCard : MonoBehaviour
{
    public struct Extra
    {
        public string Id;
        public Sprite Sprite;
        public Rect Rect;       // x, y, w, h in the rig's native pixels, top-left origin
        public int Order;
    }

    [Tooltip("The rig is fitted into this rect (the card's art inset), keeping its registered aspect.")]
    [SerializeField] private RectTransform _frame;
    [SerializeField] private TMP_Text _name;
    [Tooltip("Scaled by the pop when a part arrives; the card's own rect keeps its layout.")]
    [SerializeField] private RectTransform _body;

    private static readonly Color FocusColor = new Color32(0xFF, 0xE5, 0x87, 0xFF);

    private TilewrapRigView _rig;
    private TilewrapRigView _rigPrefab;
    // Every part that can appear: its item id and image (rig bindings, then extras).
    private readonly List<(string item, Image image)> _parts = new List<(string, Image)>();
    private readonly List<bool> _shown = new List<bool>();
    private Image _surface;
    private Color _surfaceColor;
    private Tween _propPulse;

    public TilewrapRigView Rig => _rig;

    // The case's extra layers for a column, with their sprites.
    public static Extra[] ExtrasFor(DeliveryCase level, int column) =>
        level.extraLayers.Where(x => x.column == column).OrderBy(x => x.order).Select(x => new Extra
        {
            Id = x.id,
            Sprite = FileRef.Instance.Sprite(level.Item(x.id)?.sprite ?? x.id),
            Rect = new Rect(x.rect[0], x.rect[1], x.rect[2], x.rect[3]),
            Order = x.order,
        }).ToArray();

    // A fresh instance of the prefab with the extras (the previous one is destroyed). Parts start hidden.
    public void Show(TilewrapRigView prefab, string subjectName, Extra[] extras)
    {
        gameObject.SetActive(true);
        if (_name != null) _name.text = subjectName;
        if (_surface == null && _body != null && _body.TryGetComponent(out _surface)) _surfaceColor = _surface.color;
        SetFocus(false, null);

        if (_rig != null) Destroy(_rig.gameObject);
        _rig = null;
        _rigPrefab = prefab;
        _parts.Clear();
        _shown.Clear();
        if (prefab == null) return;

        _rig = Instantiate(prefab);
        _rig.FitInto(_frame);
        _rig.name = prefab.name;
        foreach (var binding in _rig.bindings)
            if (binding.goalIndex >= 0) _parts.Add((binding.expectedItem, binding.layer.GetComponent<Image>()));
        foreach (var extra in extras ?? new Extra[0])
        {
            if (extra.Sprite == null) continue;
            var go = new GameObject(extra.Id, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_rig.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(extra.Rect.x, -extra.Rect.y);
            rect.sizeDelta = new Vector2(extra.Rect.width, extra.Rect.height);
            var image = go.GetComponent<Image>();
            image.sprite = extra.Sprite;
            image.raycastTarget = false;
            _parts.Add((extra.Id, image));
        }
        foreach (var part in _parts)
        {
            part.image.gameObject.SetActive(false);
            _shown.Add(false);
        }
    }

    public void Hide() => gameObject.SetActive(false);

    // Shows the parts whose items are delivered. animate: new parts fade in and the card pops
    // (a delivery); otherwise they just switch (case load, Undo).
    public void ApplyDelivered(ICollection<string> delivered, bool animate)
    {
        bool gained = false;
        for (int i = 0; i < _parts.Count; i++)
        {
            var (item, image) = _parts[i];
            bool on = delivered.Contains(item);
            image.gameObject.SetActive(on);
            image.DOKill();
            image.color = Color.white;
            if (on && !_shown[i] && animate)
            {
                gained = true;
                image.color = new Color(1f, 1f, 1f, 0f);
                image.DOFade(1f, Tuning.SlotFadeDuration).SetLink(image.gameObject);
            }
            _shown[i] = on;
        }
        if (gained) Pop();
    }

    // Every part on: the finished scene (win popup).
    public void ShowCompleted() => ApplyDelivered(_parts.Select(p => p.item).ToList(), false);

    // The tapped clue's owner glows; a scene clue's prop (if shown) pulses.
    public void SetFocus(bool focused, string propItem)
    {
        if (_surface != null) _surface.color = focused ? FocusColor : _surfaceColor;
        _propPulse?.Kill(true);
        _propPulse = null;
        if (!focused || propItem == null) return;
        foreach (var (item, image) in _parts)
        {
            if (item != propItem || !image.gameObject.activeSelf) continue;
            var t = image.transform;
            _propPulse = t.DOScale(1.12f, 0.45f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                          .SetLink(image.gameObject).OnKill(() => t.localScale = Vector3.one);
        }
    }

    // Where flown items land (world space): the middle of the art.
    public Vector3 LandingPoint => _frame != null ? _frame.TransformPoint(_frame.rect.center) : transform.position;

    private void Pop()
    {
        if (_body == null) return;
        _body.DOKill();
        _body.localScale = Vector3.one;
        _body.DOScale(Tuning.EquipPopScale, Tuning.EquipPopDuration * 0.45f).SetEase(Ease.OutQuad)
             .SetLoops(2, LoopType.Yoyo)
             .SetLink(gameObject);
    }
}
