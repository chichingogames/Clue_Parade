using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One subject (a board column's person, pet, cake, vehicle...) drawn from its case's rig: layers
// stacked by order on the rig's canvas, the base always, each fitted part once its item has been
// delivered, plus the case's extra layers. Found objects (items that are not part of the rig) are
// packed in a small badge: the last two delivered and a count. An optional profile fact (the case's
// dossier: "8 yrs", "40 km/h") sits under the name. The HUD shows the scene so far above each column;
// the win popup shows it finished. A new part fades in (Tuning.SlotFadeDuration) and the card pops. A
// focused card (a tapped clue names its profile) glows, and delivered parts a clue names pulse.
// Pure view: the HUD and Manager_Flow hand it the rig, the name, the sprites and what has been
// delivered.
public class UI_SubjectCard : MonoBehaviour
{
    public struct Extra
    {
        public string Id;
        public Sprite Sprite;
        public Rect Rect;       // x, y, w, h in the rig's native pixels, top-left origin
        public int Order;
    }

    // What to draw: the rig and every sprite it needs.
    public struct Setup
    {
        public DeliveryRig Rig;
        public string Name;
        public string Fact;                    // the dossier badge, or null
        public Func<string, Sprite> Sprite;    // sprite key -> sprite
        public Extra[] Extras;
        public Dictionary<string, Sprite> Packable;   // found objects of this column: item id -> icon
    }

    [Tooltip("The rig is fitted into this rect (the card's art inset), keeping its aspect.")]
    [SerializeField] private RectTransform _frame;
    [SerializeField] private TMP_Text _name;
    [Tooltip("Optional: the profile fact under the name.")]
    [SerializeField] private TMP_Text _fact;
    [Tooltip("Scaled by the pop when a part arrives; the card's own rect keeps its layout.")]
    [SerializeField] private RectTransform _body;
    [Tooltip("Optional: the found-objects badge (two icons and a count).")]
    [SerializeField] private GameObject _packed;
    [SerializeField] private Image[] _packedIcons = new Image[0];
    [SerializeField] private TMP_Text _packedCount;

    private static readonly Color FocusColor = new Color32(0xFF, 0xE5, 0x87, 0xFF);

    private RectTransform _rig;
    // Every part that can appear: its item id ("" = base, always on) and image.
    private readonly List<(string item, Image image)> _parts = new List<(string, Image)>();
    private readonly List<bool> _shown = new List<bool>();
    private Dictionary<string, Sprite> _packable = new Dictionary<string, Sprite>();
    private int _packedShown;
    private Image _surface;
    private Color _surfaceColor;
    private readonly List<Tween> _pulses = new List<Tween>();

    // The case's extra layers for a column, with their sprites.
    public static Extra[] ExtrasFor(DeliveryCase level, int column) =>
        level.extraLayers.Where(x => x.column == column).OrderBy(x => x.order).Select(x => new Extra
        {
            Id = x.id,
            Sprite = FileRef.Instance.Sprite(level.SpriteKey(x.id)),
            Rect = new Rect(x.rect[0], x.rect[1], x.rect[2], x.rect[3]),
            Order = x.order,
        }).ToArray();

    // Everything the card needs for a case's column (the HUD and the win popup use the same).
    public static Setup For(DeliveryCase level, int column)
    {
        var subject = level.subjects.FirstOrDefault(s => s.column == column);
        var rig = level.rigs.FirstOrDefault(r => subject != null && r.id == subject.rigId)
                  ?? (column < level.rigs.Length ? level.rigs[column] : null);
        var rigItems = new HashSet<string>(rig?.layers.Select(l => l.itemId) ?? Enumerable.Empty<string>());
        var packable = level.sets.Select(s => s.items[column])
                            .Where(id => !rigItems.Contains(id) && level.Item(id) != null && !level.Item(id).native)
                            .Distinct()
                            .ToDictionary(id => id, id => FileRef.Instance.Sprite(level.SpriteKey(id)));
        return new Setup
        {
            Rig = rig,
            Name = subject?.name ?? "",
            Fact = level.hasDossier && column < level.dossier.labels.Length ? level.dossier.labels[column] : null,
            Sprite = FileRef.Instance.Sprite,
            Extras = ExtrasFor(level, column),
            Packable = packable,
        };
    }

    // A fresh drawing of the rig with the extras (the previous one is destroyed). Parts start hidden.
    public void Show(Setup setup)
    {
        gameObject.SetActive(true);
        if (_name != null) _name.text = setup.Name;
        if (_fact != null)
        {
            _fact.text = setup.Fact ?? "";
            // The badge (the label's parent) shows only when there is a fact.
            _fact.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(setup.Fact));
        }
        if (_surface == null && _body != null && _body.TryGetComponent(out _surface)) _surfaceColor = _surface.color;
        SetFocus(false, null);

        if (_rig != null) Destroy(_rig.gameObject);
        _rig = null;
        _parts.Clear();
        _shown.Clear();
        _packable = setup.Packable ?? new Dictionary<string, Sprite>();
        _packedShown = 0;
        if (_packed != null) _packed.SetActive(false);
        var rig = setup.Rig;
        if (rig == null || _frame == null) return;

        _rig = (RectTransform)new GameObject("Rig", typeof(RectTransform)).transform;
        _rig.SetParent(_frame, false);
        _rig.anchorMin = _rig.anchorMax = _rig.pivot = new Vector2(0.5f, 0.5f);
        _rig.sizeDelta = new Vector2(rig.width, rig.height);
        Vector2 room = _frame.rect.size;
        float scale = Mathf.Min(room.x / rig.width, room.y / rig.height);
        _rig.localScale = Vector3.one * scale;

        var layers = rig.layers.Select(l => (item: string.IsNullOrEmpty(l.itemId) || l.goalIndex < 0 ? "" : l.itemId,
                                             sprite: setup.Sprite(l.sprite), rect: l.rect, order: l.order, name: l.sprite))
                        .Concat((setup.Extras ?? new Extra[0]).Select(x => (item: x.Id, sprite: x.Sprite,
                                rect: new[] { x.Rect.x, x.Rect.y, x.Rect.width, x.Rect.height }, order: x.Order, name: x.Id)))
                        .OrderBy(l => l.order);
        foreach (var layer in layers)
        {
            if (layer.sprite == null || layer.rect == null || layer.rect.Length < 4) continue;
            var go = new GameObject(layer.name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_rig, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(layer.rect[0], -layer.rect[1]);
            rect.sizeDelta = new Vector2(layer.rect[2], layer.rect[3]);
            var image = go.GetComponent<Image>();
            image.sprite = layer.sprite;
            image.raycastTarget = false;
            _parts.Add((layer.item, image));
        }
        foreach (var part in _parts)
        {
            part.image.gameObject.SetActive(part.item == "");
            _shown.Add(part.item == "");
        }
    }

    public void Hide() => gameObject.SetActive(false);

    // Shows the parts whose items are delivered, and the found objects. animate: new parts fade in
    // and the card pops (a delivery); otherwise they just switch (case load, Undo).
    public void ApplyDelivered(ICollection<string> delivered, bool animate)
    {
        bool gained = false;
        for (int i = 0; i < _parts.Count; i++)
        {
            var (item, image) = _parts[i];
            bool on = item == "" || delivered.Contains(item);
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

        if (_packed != null)
        {
            var found = delivered.Where(_packable.ContainsKey).ToList();
            _packed.SetActive(found.Count > 0);
            var last = found.Skip(Mathf.Max(0, found.Count - _packedIcons.Length)).ToList();
            for (int i = 0; i < _packedIcons.Length; i++)
            {
                bool has = i < last.Count;
                _packedIcons[i].gameObject.SetActive(has);
                if (has) _packedIcons[i].sprite = _packable[last[i]];
            }
            if (_packedCount != null) _packedCount.text = $"+{found.Count}";
            if (animate && found.Count > _packedShown) gained = true;
            _packedShown = found.Count;
        }
        if (gained) Pop();
    }

    // Every part on, no found-objects badge: the finished scene (win popup).
    public void ShowCompleted()
    {
        ApplyDelivered(_parts.Select(p => p.item).ToList(), false);
        if (_packed != null) _packed.SetActive(false);
    }

    // The card glows (a tapped clue names this profile) and the delivered parts of propItems pulse.
    public void SetFocus(bool focused, ICollection<string> propItems)
    {
        if (_surface != null) _surface.color = focused ? FocusColor : _surfaceColor;
        foreach (var pulse in _pulses) pulse?.Kill(true);
        _pulses.Clear();
        if (propItems == null) return;
        foreach (var (item, image) in _parts)
        {
            if (item == "" || !propItems.Contains(item) || !image.gameObject.activeSelf) continue;
            var t = image.transform;
            _pulses.Add(t.DOScale(1.1f, 0.45f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                         .SetLink(image.gameObject).OnKill(() => t.localScale = Vector3.one));
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
