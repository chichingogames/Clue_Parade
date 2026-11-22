using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off migration for the Clue Parade redesign: removes what the old Shift & Sort mode left in the
// scene and assets before its scripts are deleted, so nothing ends up as a missing script.
//   unity command script run --file AgentScripts/RetireShiftSort.cs --entry RetireShiftSort.Run
public static class RetireShiftSort
{
    public static string Run()
    {
        var log = new System.Text.StringBuilder();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Game Assets/Scenes/Game.unity") return "Open Game.unity first.";

        // Manager_Tutorial: out of the boot list, then off the Managers object.
        var managers = GameObject.Find("/Managers");
        var tutorial = managers.GetComponent("Manager_Tutorial") as MonoBehaviour;
        if (tutorial != null)
        {
            var list = new SerializedObject(managers.GetComponent<SingletonManager>()).FindProperty("_singletonsToInitialize");
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue != tutorial) continue;
                list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
            }
            list.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(tutorial);
            log.Append("removed Manager_Tutorial; ");
        }

        var cues = GameObject.Find("/GamePlay/Board/Tutorial");
        if (cues != null) { Object.DestroyImmediate(cues); log.Append("removed Board/Tutorial; "); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // Old prefabs (Cell is rebuilt with the new check art).
        foreach (var name in new[] { "Tile", "TargetMarker", "Cell" })
        {
            string path = $"Assets/Game Assets/Prefabs/Gameobjects/{name}.prefab";
            if (AssetDatabase.DeleteAsset(path)) log.Append($"deleted {name}.prefab; ");
        }

        // Shift & Sort level files (schemaVersion 1).
        int levels = 0;
        foreach (string guid in AssetDatabase.FindAssets("level-", new[] { "Assets/Resources/Levels" }))
            if (AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid))) levels++;
        log.Append($"deleted {levels} level files.");

        return log.ToString();
    }
}
