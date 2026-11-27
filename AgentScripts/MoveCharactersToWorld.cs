using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off: removes the HUD's Character Row (now world space, Board/Characters) and its intro entry,
// then runs Build Scene to create Board/Characters.
public static class MoveCharactersToWorld
{
    public static string Run()
    {
        var log = "";
        var row = GameObject.Find("/Canvas/Container/InGame/Reference/Character Row");
        var ui = Object.FindFirstObjectByType<UI_InGame>(FindObjectsInactive.Include);
        if (ui != null)
        {
            var so = new SerializedObject(ui);
            var items = so.FindProperty("_introItems");
            for (int i = items.arraySize - 1; i >= 0; i--)
            {
                var item = items.GetArrayElementAtIndex(i);
                if (item.objectReferenceValue == null || (row != null && item.objectReferenceValue == row.transform))
                {
                    item.objectReferenceValue = null;
                    items.DeleteArrayElementAtIndex(i);
                    log += $"removed intro item {i}; ";
                }
            }
            so.ApplyModifiedProperties();
        }
        if (row != null) { Undo.DestroyObjectImmediate(row); log += "deleted Character Row; "; }

        SceneBuilder.Build();
        var built = GameObject.Find("/GamePlay/Board/Characters");
        log += built != null ? $"Board/Characters has {built.transform.childCount} children; " : "Board/Characters MISSING; ";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        return log + "saved";
    }
}
