using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chichingo.TilewrapMystery
{
    [Serializable] public sealed class RigBinding { public GameObject layer; public string itemId; public bool alwaysVisible; }
    public sealed class TilewrapRigView : MonoBehaviour
    {
        public Vector2 nativeSize;
        public RigBinding[] bindings;
        public void Fit(Vector2 frameSize)
        {
            if (frameSize.x <= 0 || frameSize.y <= 0) return;
            float scale = Mathf.Min(frameSize.x / nativeSize.x, frameSize.y / nativeSize.y);
            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f);
            rt.anchoredPosition = Vector2.zero; rt.localScale = Vector3.one * scale;
        }
        public void ApplyDelivered(IEnumerable<string> delivered)
        {
            var confirmed = new HashSet<string>(delivered, StringComparer.Ordinal);
            foreach (var binding in bindings) binding.layer.SetActive(binding.alwaysVisible || confirmed.Contains(binding.itemId));
        }
        public void ShowCompleted() { foreach (var binding in bindings) binding.layer.SetActive(true); }
    }
}
