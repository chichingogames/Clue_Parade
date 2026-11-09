using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Imports the art, creates the few placeholder sprites still needed (cell slot and mask),
// the Cell / Tile / TargetMarker prefabs, and fills Resources/FileRef.asset from the art by file name:
//   Sprites/Sprites/tiles/tile_{shape}_{color}.png, targets/target_row_{shape}.png, targets/target_column_{color}.png
// Re-runnable: existing prefabs are kept (so prefab edits survive) and FileRef tables are only filled when empty.
public static class AssetBuilder
{
    private const string SpriteFolder = "Assets/Game Assets/Sprites/Placeholder";
    private const string ArtFolder = "Assets/Game Assets/Sprites/Sprites";
    private const string PrefabFolder = "Assets/Game Assets/Prefabs/Gameobjects";
    private const int Resolution = 128;

    public const string SlotSprite = "Slot";
    public const string SolidSprite = "Solid";
    private const string MatchedBadge = "fx/fx_matched_badge";

    [MenuItem("ShiftSort/Build Assets")]
    public static void Build()
    {
        GenerateSprites();
        ImportArt();
        var cell = BuildCellPrefab();
        var tile = BuildTilePrefab();
        var marker = BuildTargetMarkerPrefab();
        FillFileRef(cell, tile, marker);
        AssetDatabase.SaveAssets();
        Debug.Log("[AssetBuilder] Assets are up to date.");
    }

    public static Sprite LoadSprite(string spriteName) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{spriteName}.png");

    // ---------- Sprites ----------

    private static void GenerateSprites()
    {
        Directory.CreateDirectory(SpriteFolder);
        WriteSprite(SlotSprite, p => RoundedBox(p, 0.98f, 0.16f));
        WriteSprite(SolidSprite, p => -1f);
    }

    // sdf: signed distance in [-1, 1] sprite space, negative inside.
    private static void WriteSprite(string spriteName, Func<Vector2, float> sdf)
    {
        string path = $"{SpriteFolder}/{spriteName}.png";
        if (File.Exists(path)) return;

        var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
        float pixel = 2f / Resolution;
        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                var p = new Vector2((x + 0.5f) * pixel - 1f, (y + 0.5f) * pixel - 1f);
                float alpha = Mathf.Clamp01(0.5f - sdf(p) / pixel);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Resolution;     // 1 sprite = 1 world unit
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static float RoundedBox(Vector2 p, float halfSize, float radius)
    {
        var d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - Vector2.one * (halfSize - radius);
        return Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
    }

    // ---------- Art ----------

    // Every art sprite is imported so that its full texture is 1 world unit wide (ppu = width);
    // Manager_Board then scales tiles and markers in cell units.
    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { $"{ArtFolder}/tiles", $"{ArtFolder}/targets", $"{ArtFolder}/fx" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out _);

            bool changed = importer.textureType != TextureImporterType.Sprite
                           || importer.spriteImportMode != SpriteImportMode.Single
                           || !Mathf.Approximately(importer.spritePixelsPerUnit, width)
                           || importer.mipmapEnabled;
            if (!changed) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = width;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    private static Sprite LoadArt(string relativePath) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{relativePath}.png");

    private static string ArtName(Enum value) => value.ToString().ToLowerInvariant();

    // ---------- Prefabs ----------

    private static Cell BuildCellPrefab()
    {
        string path = $"{PrefabFolder}/Cell.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<Cell>(path);
        if (existing != null) return existing;

        var root = new GameObject("Cell");
        var background = AddRenderer(root.transform, "Background", LoadSprite(SlotSprite),
            new Color(0.14f, 0.11f, 0.22f), 0.94f, Vector3.zero, SortOrder.Slot, masked: false);
        var check = AddRenderer(root.transform, "Check", LoadArt(MatchedBadge),
            Color.white, 0.2f, new Vector3(0.33f, 0.33f), SortOrder.TileCheck, masked: false);

        var cell = root.AddComponent<Cell>();
        SetRefs(cell, ("_background", background), ("_check", check.gameObject));
        return SavePrefab<Cell>(root, path);
    }

    private static Tile BuildTilePrefab()
    {
        string path = $"{PrefabFolder}/Tile.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<Tile>(path);
        if (existing != null) return existing;

        var root = new GameObject("Tile");
        var body = AddRenderer(root.transform, "Body", null, Color.white, 1f, Vector3.zero,
            SortOrder.TileBody, masked: true);

        var tile = root.AddComponent<Tile>();
        SetRefs(tile, ("_body", body));
        return SavePrefab<Tile>(root, path);
    }

    private static TargetMarker BuildTargetMarkerPrefab()
    {
        string path = $"{PrefabFolder}/TargetMarker.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<TargetMarker>(path);
        if (existing != null) return existing;

        var root = new GameObject("TargetMarker");
        var background = AddRenderer(root.transform, "Background", LoadSprite(SlotSprite), Color.white, 0.8f,
            Vector3.zero, SortOrder.Marker, masked: false);
        var icon = AddRenderer(root.transform, "Icon", null, Color.white, 0.7f, Vector3.zero,
            SortOrder.MarkerIcon, masked: false);

        var marker = root.AddComponent<TargetMarker>();
        SetRefs(marker, ("_background", background), ("_icon", icon));
        return SavePrefab<TargetMarker>(root, path);
    }

    private static SpriteRenderer AddRenderer(Transform parent, string childName, Sprite sprite, Color color,
                                              float scale, Vector3 position, int order, bool masked)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = Vector3.one * scale;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        renderer.maskInteraction = masked ? SpriteMaskInteraction.VisibleInsideMask : SpriteMaskInteraction.None;
        return renderer;
    }

    private static T SavePrefab<T>(GameObject root, string path) where T : Component
    {
        Directory.CreateDirectory(PrefabFolder);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab.GetComponent<T>();
    }

    private static void SetRefs(UnityEngine.Object target, params (string field, UnityEngine.Object value)[] refs)
    {
        var so = new SerializedObject(target);
        foreach (var (field, value) in refs) so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- FileRef ----------

    private static void FillFileRef(Cell cell, Tile tile, TargetMarker marker)
    {
        var fileRef = FileRef.Instance;      // creates Resources/FileRef.asset if missing
        var so = new SerializedObject(fileRef);

        SetIfEmpty(so.FindProperty("_cellPrefab"), cell);
        SetIfEmpty(so.FindProperty("_tilePrefab"), tile);
        SetIfEmpty(so.FindProperty("_targetMarkerPrefab"), marker);

        var shapes = (TileShape[])Enum.GetValues(typeof(TileShape));
        var colors = (TileColor[])Enum.GetValues(typeof(TileColor));

        var tiles = so.FindProperty("_tileSprites");
        if (tiles.arraySize == 0)
        {
            foreach (var shape in shapes)
            {
                foreach (var color in colors)
                {
                    var sprite = LoadArt($"tiles/tile_{ArtName(shape)}_{ArtName(color)}");
                    if (sprite == null) continue;
                    var entry = AppendElement(tiles);
                    entry.FindPropertyRelative("Shape").intValue = (int)shape;
                    entry.FindPropertyRelative("Color").intValue = (int)color;
                    entry.FindPropertyRelative("Sprite").objectReferenceValue = sprite;
                }
            }
        }

        var rowTargets = so.FindProperty("_rowTargetSprites");
        if (rowTargets.arraySize == 0)
        {
            foreach (var shape in shapes)
            {
                var sprite = LoadArt($"targets/target_row_{ArtName(shape)}");
                if (sprite == null) continue;
                var entry = AppendElement(rowTargets);
                entry.FindPropertyRelative("Shape").intValue = (int)shape;
                entry.FindPropertyRelative("Sprite").objectReferenceValue = sprite;
            }
        }

        var columnTargets = so.FindProperty("_columnTargetSprites");
        if (columnTargets.arraySize == 0)
        {
            foreach (var color in colors)
            {
                var sprite = LoadArt($"targets/target_column_{ArtName(color)}");
                if (sprite == null) continue;
                var entry = AppendElement(columnTargets);
                entry.FindPropertyRelative("Color").intValue = (int)color;
                entry.FindPropertyRelative("Sprite").objectReferenceValue = sprite;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(fileRef);
    }

    private static SerializedProperty AppendElement(SerializedProperty array)
    {
        array.arraySize++;
        return array.GetArrayElementAtIndex(array.arraySize - 1);
    }

    private static void SetIfEmpty(SerializedProperty property, UnityEngine.Object value)
    {
        if (property.objectReferenceValue == null) property.objectReferenceValue = value;
    }
}
