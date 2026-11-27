using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

// One customer above the board, built from registered 600 x 900 layers that all share one rect
// (same position, scale and centre pivot; never trimmed), back to front: Bag Rear, Base, Shirt,
// Bag Straps, Hat. The base prefab (Prefabs/Gameobjects/Customer) holds the layers and the
// accessory groups; each person is a prefab variant of it (Customer_Mia, ...) that only assigns the
// sprites and the id. Its pivot is the art's bottom centre.
//
// An accessory is the layers one board category puts on the customer (Bags = rear + straps). They
// are switched on and off with SetActive. Pure view: Manager_Board says what is equipped.
[RequireComponent(typeof(SortingGroup))]
public class Customer : MonoBehaviour
{
    [Serializable]
    public struct Accessory
    {
        [Tooltip("The board category (row label in the level file) this accessory belongs to, e.g. \"Hats\".")]
        public string Category;
        public GameObject[] Layers;
    }

    [Tooltip("FileRef character id (\"mia\").")]
    [SerializeField] private string _id;
    [Tooltip("Scaled for the equip pop; the root keeps the scale it was placed with.")]
    [SerializeField] private Transform _body;
    [SerializeField] private Accessory[] _accessories = new Accessory[0];

    public string Id => _id;

    // Returns true when the accessory was off and is now on.
    public bool SetEquipped(string category, bool equipped)
    {
        bool gained = false;
        foreach (var accessory in _accessories)
        {
            if (!string.Equals(accessory.Category, category, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var layer in accessory.Layers)
            {
                if (layer == null || layer.activeSelf == equipped) continue;
                if (equipped) gained = true;
                layer.SetActive(equipped);
            }
        }
        return gained;
    }

    public void SetAllEquipped(bool equipped)
    {
        foreach (var accessory in _accessories)
            foreach (var layer in accessory.Layers)
                if (layer != null) layer.SetActive(equipped);
    }

    public bool IsEquipped(string category)
    {
        foreach (var accessory in _accessories)
            if (string.Equals(accessory.Category, category, StringComparison.OrdinalIgnoreCase))
                return accessory.Layers.Length > 0 && accessory.Layers[0] != null && accessory.Layers[0].activeSelf;
        return false;
    }

    // The layers back to front with their sprite and whether they are an accessory (false = always
    // shown). UI_CrewPortrait draws the same prefab in uGUI from this.
    public void GetLayers(List<(Sprite sprite, bool accessory)> into)
    {
        into.Clear();
        for (int i = 0; i < _body.childCount; i++)
        {
            var layer = _body.GetChild(i);
            if (!layer.TryGetComponent<SpriteRenderer>(out var renderer)) continue;
            into.Add((renderer.sprite, IsAccessoryLayer(layer.gameObject)));
        }
    }

    private bool IsAccessoryLayer(GameObject layer)
    {
        foreach (var accessory in _accessories)
            foreach (var l in accessory.Layers)
                if (l == layer) return true;
        return false;
    }

    // Up to `scale` and back over `duration`, from the feet (the body's pivot).
    public void Pop(float scale, float duration)
    {
        _body.DOKill();
        _body.localScale = Vector3.one;
        _body.DOScale(scale, duration * 0.45f).SetEase(Ease.OutQuad)
             .SetLoops(2, LoopType.Yoyo)
             .SetLink(gameObject);
    }
}
