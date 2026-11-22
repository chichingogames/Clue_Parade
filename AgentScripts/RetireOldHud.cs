using UnityEditor.SceneManagement;
using UnityEngine;

// One-off migration for the HUD rebuild: removes the Shift & Sort HUD under Canvas/Container/InGame
// (Top Bar, Board Area, Bottom Stack) and its UI_InGame component, so UIBuilder can build the
// Tilewrap HUD there. Saves Game.unity.
//   unity command script run --file AgentScripts/RetireOldHud.cs --entry RetireOldHud.Run
public static class RetireOldHud
{
    public static string Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Game Assets/Scenes/Game.unity") return "Open Game.unity first.";

        var inGame = GameObject.Find("/Canvas/Container/InGame");
        if (inGame == null) return "Canvas/Container/InGame not found.";

        var log = new System.Text.StringBuilder();
        foreach (var name in new[] { "Top Bar", "Board Area", "Bottom Stack" })
        {
            var child = inGame.transform.Find(name);
            if (child == null) continue;
            Object.DestroyImmediate(child.gameObject);
            log.Append($"removed {name}; ");
        }

        var ui = inGame.GetComponent("UI_InGame");
        if (ui != null) { Object.DestroyImmediate(ui); log.Append("removed UI_InGame; "); }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return log.Append("saved.").ToString();
    }
}
