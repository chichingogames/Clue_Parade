using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off: removes the HUD's "CHICHINGO · PARADE PREP · PLAYTEST" footer and its intro entry.
public static class RemoveBrandFooter
{
    public static string Run()
    {
        var brand = GameObject.Find("/Canvas/Container/InGame/Reference/Brand");
        var ui = Object.FindFirstObjectByType<UI_InGame>(FindObjectsInactive.Include);
        var so = new SerializedObject(ui);
        var items = so.FindProperty("_introItems");
        for (int i = items.arraySize - 1; i >= 0; i--)
        {
            var item = items.GetArrayElementAtIndex(i);
            if (item.objectReferenceValue == null || (brand != null && item.objectReferenceValue == brand.transform))
            {
                item.objectReferenceValue = null;
                items.DeleteArrayElementAtIndex(i);
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        if (brand != null) Object.DestroyImmediate(brand);
        var scene = ui.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"brand {(brand != null ? "removed" : "missing")}; intro items {items.arraySize}";
    }
}
