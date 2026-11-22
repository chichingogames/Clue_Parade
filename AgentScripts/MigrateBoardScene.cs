using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off scene migration for the Clue Parade board (SceneBuilder never changes existing objects):
// Board/Frame gets the pack's wooden frame over the tiles, Board/Markers becomes Board/Tags, then
// SceneBuilder adds what is missing (Board/Active Row) and wires it. Saves Game.unity.
//   unity command script run --file AgentScripts/MigrateBoardScene.cs --entry MigrateBoardScene.Run
public static class MigrateBoardScene
{
    public static string Run()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Game Assets/Scenes/Game.unity") return "Open Game.unity first.";
        var log = new System.Text.StringBuilder();

        var frame = GameObject.Find("/GamePlay/Board/Frame").GetComponent<SpriteRenderer>();
        frame.sprite = AssetBuilder.LoadTilewrap("UI/board_frame");
        frame.drawMode = SpriteDrawMode.Sliced;
        frame.color = Color.white;
        frame.sortingOrder = SortOrder.Frame;
        log.Append("frame -> board_frame; ");

        var markers = GameObject.Find("/GamePlay/Board/Markers");
        if (markers != null) { markers.name = "Tags"; log.Append("Markers -> Tags; "); }

        var cam = Camera.main;
        cam.backgroundColor = new Color32(0x6E, 0x41, 0x55, 0xFF);

        SceneBuilder.Build();

        var board = new SerializedObject(Object.FindFirstObjectByType<Manager_Board>());
        log.Append($"_activeRow={board.FindProperty("_activeRow").objectReferenceValue?.name ?? "null"}, " +
                   $"_tagsRoot={board.FindProperty("_tagsRoot").objectReferenceValue?.name ?? "null"}; ");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Append("saved.");
        return log.ToString();
    }
}
