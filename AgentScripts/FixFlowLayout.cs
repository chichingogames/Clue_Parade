using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off: as PopUpBuilder / UIBuilder now build them: the win payoff centred, and the toast moved
// into Container/Overlay (after PopUps) so it shows over popups.
public static class FixFlowLayout
{
    public static string Run()
    {
        var container = GameObject.Find("/Canvas/Container").transform;
        var log = "";
        foreach (Transform popup in container.Find("PopUps"))
        {
            if (popup.name != "PopUp_Win") continue;
            popup.Find("Holder/Body").GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Top;
            log += "win body centred; ";
        }

        var overlay = container.Find("Overlay");
        if (overlay == null)
        {
            var go = new GameObject("Overlay", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            overlay = go.transform;
            overlay.SetParent(container, false);
            var rect = (RectTransform)overlay;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = UI_ReferenceFit.ReferenceSize;
            go.AddComponent<UI_ReferenceFit>();
        }
        overlay.SetAsLastSibling();
        var toast = container.Find("InGame/Reference/Toast");
        if (toast != null) { toast.SetParent(overlay, false); log += "toast moved; "; }

        var scene = container.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return log + "saved";
    }
}
