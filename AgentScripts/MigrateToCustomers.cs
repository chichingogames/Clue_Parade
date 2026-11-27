using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-off: the handoff's modular customers replace the pose portraits.
// Clears FileRef's item and character tables (old fair-booth items and pose characters), runs Build
// Assets (builds Customer.prefab + Customer_{Name} variants and refills both tables from
// Assets/Tilewrap/Data/game_config.json), then turns Board/Characters' "Character i" portraits
// into empty "Customer Stand i" transforms wired into CharacterRow._slots[i].Stand.
public static class MigrateToCustomers
{
    public static string Run()
    {
        var log = "";
        var fileRef = new SerializedObject(FileRef.Instance);
        fileRef.FindProperty("_items").arraySize = 0;
        fileRef.FindProperty("_characters").arraySize = 0;
        fileRef.ApplyModifiedPropertiesWithoutUndo();

        AssetBuilder.Build();
        fileRef.Update();
        log += $"items {fileRef.FindProperty("_items").arraySize}, characters {fileRef.FindProperty("_characters").arraySize}; ";
        for (int i = 0; i < fileRef.FindProperty("_characters").arraySize; i++)
        {
            var c = fileRef.FindProperty("_characters").GetArrayElementAtIndex(i);
            log += $"{c.FindPropertyRelative("Id").stringValue}->{(c.FindPropertyRelative("Customer").objectReferenceValue != null ? c.FindPropertyRelative("Customer").objectReferenceValue.name : "NULL")}; ";
        }

        var rowGo = GameObject.Find("/GamePlay/Board/Characters");
        if (rowGo == null) return log + "Board/Characters missing";
        var row = rowGo.GetComponent<CharacterRow>();
        var slots = new SerializedObject(row).FindProperty("_slots");
        for (int i = 0; i < slots.arraySize; i++)
        {
            var old = rowGo.transform.Find($"Character {i + 1}");
            var stand = old != null ? old : rowGo.transform.Find($"Customer Stand {i + 1}");
            if (stand == null) { log += $"slot {i} has no portrait; "; continue; }

            var renderer = stand.GetComponent<SpriteRenderer>();
            if (renderer != null) Object.DestroyImmediate(renderer);
            stand.name = $"Customer Stand {i + 1}";
            stand.localScale = Vector3.one * (ReferenceLayout.PortraitSize.y / ReferenceLayout.CustomerArtHeight);
            slots.GetArrayElementAtIndex(i).FindPropertyRelative("Stand").objectReferenceValue = stand;
            log += $"slot {i} -> {stand.name} at {stand.localPosition}; ";
        }
        slots.serializedObject.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(rowGo.scene);
        EditorSceneManager.SaveScene(rowGo.scene);
        return log + "saved";
    }
}
