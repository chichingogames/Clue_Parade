using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off: moves the scene to the Tilewrap handoff's UI and adds the onboarding flow.
//   Rebuild: deletes the old board prefabs (Cell, ItemTile, CategoryTag), clears their FileRef slots
//   and the character table (refilled with symbols), removes the old board objects (Background,
//   Frame, Active Row, Characters), the HUD (InGame/Reference + UI_InGame) and the loading screen's
//   content, then runs Build Assets + Build Scene (which also builds the popups). Saves the scene.
//   Unused: lists files under the old art folders that the scene, prefabs or FileRef still depend on.
public static class MigrateToHandoffUI
{
    private const string Scene = "Assets/Game Assets/Scenes/Game.unity";
    private const string Prefabs = "Assets/Game Assets/Prefabs/Gameobjects";
    public static readonly string[] OldArt =
    {
        "Assets/Game Assets/Sprites/Sprites",
        "Assets/Game Assets/Sprites/Tilewrap",
    };

    public static string Rebuild()
    {
        var log = new List<string>();
        foreach (var name in new[] { "Cell", "ItemTile", "CategoryTag" })
            if (AssetDatabase.DeleteAsset($"{Prefabs}/{name}.prefab")) log.Add($"deleted {name}.prefab");

        var fileRef = new SerializedObject(FileRef.Instance);
        foreach (var field in new[] { "_cellPrefab", "_itemTilePrefab", "_categoryTagPrefab", "_spark" })
            fileRef.FindProperty(field).objectReferenceValue = null;
        fileRef.FindProperty("_characters").arraySize = 0;
        fileRef.FindProperty("_categories").arraySize = 0;
        fileRef.ApplyModifiedPropertiesWithoutUndo();

        AssetBuilder.Build();

        var board = Object.FindFirstObjectByType<Manager_Board>();
        var boardSo = new SerializedObject(board);
        foreach (var path in new[] { "/GamePlay/Background", "/GamePlay/Board/Frame", "/GamePlay/Board/Active Row", "/GamePlay/Board/Characters" })
        {
            var go = GameObject.Find(path);
            if (go == null) continue;
            Object.DestroyImmediate(go);
            log.Add($"removed {path}");
        }
        foreach (var field in new[] { "_frame", "_activeRow", "_background", "_characters", "_guide" })
            boardSo.FindProperty(field).objectReferenceValue = null;
        boardSo.FindProperty("_dividers").arraySize = 0;
        boardSo.ApplyModifiedPropertiesWithoutUndo();

        var inGame = GameObject.Find("/Canvas/Container/InGame");
        var reference = inGame.transform.Find("Reference");
        if (reference != null) Object.DestroyImmediate(reference.gameObject);
        var hud = inGame.GetComponent<UI_InGame>();
        if (hud != null) Object.DestroyImmediate(hud);
        log.Add("removed HUD");

        var loading = FindIncludingInactive("Canvas/Loading Screen");
        if (loading != null)
        {
            var ui = loading.GetComponent<UI_Loading>();
            if (ui != null) Object.DestroyImmediate(ui);
            var group = loading.GetComponent<CanvasGroup>();
            if (group != null) Object.DestroyImmediate(group);
            for (int i = loading.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(loading.transform.GetChild(i).gameObject);
            loading.SetActive(true);
            log.Add("cleared loading screen");
        }

        SceneBuilder.Build();

        // Confetti draws over the popups but under the loading screen.
        var canvas = GameObject.Find("/Canvas").transform;
        var confetti = canvas.Find("Confetti");
        var loadingScreen = canvas.Find("Loading Screen");
        if (confetti != null && loadingScreen != null) confetti.SetSiblingIndex(loadingScreen.GetSiblingIndex());

        var list = new SerializedObject(Object.FindFirstObjectByType<SingletonManager>()).FindProperty("_singletonsToInitialize");
        var order = new List<string>();
        for (int i = 0; i < list.arraySize; i++) order.Add(list.GetArrayElementAtIndex(i).objectReferenceValue?.GetType().Name ?? "null");
        log.Add("init order: " + string.Join(", ", order));

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return string.Join("; ", log) + "; saved";
    }

    // Files under the old art folders that the scene, the prefabs or FileRef still use.
    public static string Unused()
    {
        var roots = new List<string> { Scene, "Assets/Resources/FileRef.asset" };
        roots.AddRange(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath));
        var used = new HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(), true));
        var stillUsed = used.Where(p => OldArt.Any(folder => p.StartsWith(folder + "/"))).ToList();
        int total = OldArt.Sum(folder => AssetDatabase.FindAssets("", new[] { folder }).Length);
        return $"old art assets: {total}; still used: {stillUsed.Count} {string.Join(", ", stillUsed)}";
    }

    private static GameObject FindIncludingInactive(string path)
    {
        var parts = path.Split('/');
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != parts[0]) continue;
            var t = root.transform.Find(string.Join("/", parts.Skip(1)));
            if (t != null) return t.gameObject;
        }
        return null;
    }
}
