using System.Collections.Generic;
using Chichingo.TilewrapVaried;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One subject (a board column's person, pet, room, bag...) drawn from its registered rig: one base
// plus fitted parts, each shown while its board cell holds the right item (TilewrapRigView). The
// HUD shows the live board above each column; the win popup shows the same rig completed. A newly
// fitted part fades in (ui_layout.json slotFadeSeconds 0.16) and the card pops. Pure view: the HUD
// and Manager_Flow hand it the rig prefab, the name and the board.
public class UI_SubjectCard : MonoBehaviour
{
    [Tooltip("The rig is fitted into this rect (the card's art inset), keeping its registered aspect.")]
    [SerializeField] private RectTransform _frame;
    [SerializeField] private TMP_Text _name;
    [Tooltip("Scaled by the pop when a move fits a part; the card's own rect keeps its layout.")]
    [SerializeField] private RectTransform _body;

    private TilewrapRigView _rig;
    private TilewrapRigView _rigPrefab;
    private readonly List<bool> _shown = new List<bool>();

    public TilewrapRigView Rig => _rig;

    // A fresh instance of the prefab (the previous one is destroyed), or the same one kept.
    public void Show(TilewrapRigView prefab, string subjectName)
    {
        gameObject.SetActive(true);
        if (_name != null) _name.text = subjectName;
        if (prefab == _rigPrefab && _rig != null) return;

        if (_rig != null) Destroy(_rig.gameObject);
        _rig = null;
        _rigPrefab = prefab;
        if (prefab == null) return;

        _rig = Instantiate(prefab);
        _rig.FitInto(_frame);
        _rig.name = prefab.name;
        Remember();
    }

    public void Hide() => gameObject.SetActive(false);

    // board: the item id in each cell (row-major). animate: parts that appear fade in, and the card
    // pops if any did (a played move); otherwise they just switch (level load, Undo).
    public void ApplyBoard(string[] board, bool animate)
    {
        if (_rig == null) return;
        _rig.ApplyBoard(board);
        bool gained = false;
        var bindings = _rig.bindings;
        for (int i = 0; i < bindings.Length; i++)
        {
            var layer = bindings[i].layer;
            bool on = layer.activeSelf;
            var image = layer.GetComponent<Image>();
            image.DOKill();
            image.color = Color.white;
            if (on && !_shown[i] && animate)
            {
                gained = true;
                image.color = new Color(1f, 1f, 1f, 0f);
                image.DOFade(1f, Tuning.SlotFadeDuration).SetLink(layer);
            }
            _shown[i] = on;
        }
        if (gained) Pop();
    }

    // Every fitted part on: the finished scene (win popup).
    public void ShowCompleted()
    {
        if (_rig == null) return;
        _rig.ShowCompleted();
        Remember();
    }

    private void Remember()
    {
        _shown.Clear();
        foreach (var binding in _rig.bindings) _shown.Add(binding.layer.activeSelf);
    }

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
