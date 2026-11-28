using System;
using UnityEngine;

namespace Chichingo.TilewrapVaried
{
    [Serializable] public sealed class RigLayerBinding
    {
        public GameObject layer;
        public int goalIndex;
        public string expectedItem;
    }
    public sealed class TilewrapRigView : MonoBehaviour
    {
        public Vector2 nativeSize;
        public RigLayerBinding[] bindings;

        // Call after load, every committed swipe, Undo and retry. Wrong parts disappear.
        public void ApplyBoard(string[] board)
        {
            if (board == null || board.Length != 9) throw new ArgumentException("Expected nine board cells.");
            foreach (var binding in bindings)
            {
                bool show = binding.goalIndex < 0 || string.Equals(board[binding.goalIndex], binding.expectedItem, StringComparison.Ordinal);
                if (binding.layer.activeSelf != show) binding.layer.SetActive(show);
            }
        }
        // Completion uses these same layers, with all conditions satisfied.
        public void ShowCompleted()
        {
            foreach (var binding in bindings) binding.layer.SetActive(true);
        }
        public void FitInto(RectTransform frame)
        {
            if (frame == null || nativeSize.x <= 0 || nativeSize.y <= 0) throw new ArgumentException("Invalid rig frame.");
            Canvas.ForceUpdateCanvases();
            if (frame.rect.width <= 0 || frame.rect.height <= 0)
                throw new InvalidOperationException("Rig frame must have a positive laid-out size.");
            var rect = (RectTransform)transform;
            rect.SetParent(frame, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = nativeSize;
            float scale = Mathf.Min(frame.rect.width / nativeSize.x, frame.rect.height / nativeSize.y);
            rect.localScale = new Vector3(scale, scale, 1);
        }
    }
}
