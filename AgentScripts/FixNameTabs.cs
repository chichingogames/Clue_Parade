using UnityEditor.SceneManagement;
using UnityEngine;

// One-off: moves the name tabs' symbol and name apart (as SceneBuilder now builds them).
public static class FixNameTabs
{
    public static string Run()
    {
        var row = GameObject.Find("/GamePlay/Board/Characters");
        int n = 0;
        foreach (Transform child in row.transform)
        {
            if (!child.name.StartsWith("Name Tab")) continue;
            child.Find("Symbol").localPosition = new Vector3(-0.66f, 0f, 0f);
            var name = (RectTransform)child.Find("Name");
            name.anchoredPosition = new Vector2(0.30f, 0f);
            name.sizeDelta = ReferenceLayout.PlateNameSize;
            n++;
        }
        EditorSceneManager.MarkSceneDirty(row.scene);
        EditorSceneManager.SaveScene(row.scene);
        return $"fixed {n} tabs";
    }
}
