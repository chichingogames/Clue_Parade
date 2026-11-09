using System;
using UnityEngine;

// Where a ScriptableSingleton lives, relative to Assets/Resources, without extension.
[AttributeUsage(AttributeTargets.Class)]
public class AssetPathAttribute : Attribute
{
    public string Path { get; }

    public AssetPathAttribute(string path) => Path = path;
}

// ScriptableObject with a global Instance, loaded from Resources by its [AssetPath].
// In the Editor a missing asset is created on first access; in a build it is null.
public abstract class ScriptableSingleton<T> : ScriptableObject where T : ScriptableSingleton<T>
{
    private static T _instance;

    public static T Instance
    {
        get
        {
            if (_instance == null) _instance = Load();
            return _instance;
        }
    }

    private static T Load()
    {
        var attribute = (AssetPathAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(AssetPathAttribute));
        if (attribute == null)
        {
            Debug.LogError($"[ScriptableSingleton] {typeof(T).Name} has no [AssetPath].");
            return null;
        }

        var asset = Resources.Load<T>(attribute.Path);

#if UNITY_EDITOR
        if (asset == null)
        {
            string assetPath = $"Assets/Resources/{attribute.Path}.asset";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(assetPath));
            asset = CreateInstance<T>();
            UnityEditor.AssetDatabase.CreateAsset(asset, assetPath);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[ScriptableSingleton] Created {assetPath}.");
        }
#endif

        if (asset == null) Debug.LogError($"[ScriptableSingleton] Resources/{attribute.Path} not found.");
        return asset;
    }
}
