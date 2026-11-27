using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

// Builds the board prefabs on the Tilewrap handoff's art (Assets/Tilewrap, see HandoffBuilder) and
// fills Resources/FileRef.asset:
//   Sprites/Placeholder/Solid.png   generated white square (board mask, match sparks, dividers)
//   Prefabs/Gameobjects/Cell, ItemTile, CategoryTag, and (HandoffBuilder) Customer + one variant per person
//   FileRef: prefabs, spark sprite, items / characters / categories (HandoffBuilder, from game_config.json),
//   music (Audio/Music/*.wav, first one) and sounds (Audio/SFX/{sound_type}.wav or {sound_type}_NN.wav)
// Re-runnable: existing prefabs are kept (so prefab edits survive) and FileRef tables are only filled when empty.
public static class AssetBuilder
{
    private const string SpriteFolder = "Assets/Game Assets/Sprites/Placeholder";
    private const string PrefabFolder = "Assets/Game Assets/Prefabs/Gameobjects";
    private const string MusicFolder = "Assets/Game Assets/Audio/Music";
    private const string SfxFolder = "Assets/Game Assets/Audio/SFX";
    private const int Resolution = 128;

    public const string SolidSprite = "Solid";

    // One board cell: the handoff's 680 px inner board split in thirds (ReferenceLayout).
    public static readonly Vector2 CellSize = Vector2.one * 2.2667f;
    private static readonly Color InkColor = new Color32(0x40, 0x28, 0x1F, 0xFF);

    [MenuItem("ShiftSort/Build Assets")]
    public static void Build()
    {
        WriteSolidSprite();
        ImportAudio();
        var cell = BuildCellPrefab();
        var itemTile = BuildItemTilePrefab();
        var tag = BuildCategoryTagPrefab();
        var customers = HandoffBuilder.BuildCustomers();
        FillFileRef(cell, itemTile, tag, customers);
        AssetDatabase.SaveAssets();
        Debug.Log("[AssetBuilder] Assets are up to date.");
    }

    public static Sprite LoadSprite(string spriteName) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{spriteName}.png");

    // ---------- Sprites ----------

    // A white square, 1 world unit at scale 1.
    private static void WriteSolidSprite()
    {
        Directory.CreateDirectory(SpriteFolder);
        string path = $"{SpriteFolder}/{SolidSprite}.png";
        if (File.Exists(path)) return;

        var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
        var pixels = new Color[Resolution * Resolution];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Resolution;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    // Music streams (long, compressed); effects decompress on load (short, played often, no latency).
    private static void ImportAudio()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder, SfxFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            bool music = path.StartsWith(MusicFolder);

            var settings = importer.defaultSampleSettings;
            var wantLoad = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            var wantFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            bool changed = settings.loadType != wantLoad || settings.compressionFormat != wantFormat
                           || importer.forceToMono == music || importer.loadInBackground != music;
            if (!changed) continue;

            settings.loadType = wantLoad;
            settings.compressionFormat = wantFormat;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !music;
            importer.loadInBackground = music;
            importer.SaveAndReimport();
        }
    }

    // ---------- Prefabs ----------

    // Cell: a board position with the check badge a matched cell shows (bottom-right of the cell).
    private static Cell BuildCellPrefab()
    {
        string path = $"{PrefabFolder}/Cell.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<Cell>(path);
        if (existing != null) return existing;

        var root = new GameObject("Cell");
        var checkSprite = HandoffBuilder.LoadSprite("UI/check_badge");
        float scale = ReferenceLayout.CheckSize / checkSprite.bounds.size.x;
        var check = AddRenderer(root.transform, "Check", checkSprite, Color.white, scale,
            ReferenceLayout.CheckOffset, SortOrder.TileCheck, masked: false);

        var cell = root.AddComponent<Cell>();
        SetRefs(cell, ("_check", check.gameObject));
        return SavePrefab<Cell>(root, path);
    }

    // CategoryTag: tab_category (sliced, rounded on the left, flush against the board) with the
    // category icon and label; the active step's row swaps in tab_category_active.
    private static CategoryTag BuildCategoryTagPrefab()
    {
        string path = $"{PrefabFolder}/CategoryTag.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<CategoryTag>(path);
        if (existing != null) return existing;

        var root = new GameObject("CategoryTag");
        var normal = HandoffBuilder.LoadSprite("UI/tab_category");
        var tab = AddRenderer(root.transform, "Tab", normal, Color.white, 1f, Vector3.zero, SortOrder.Tag, masked: false);
        tab.drawMode = SpriteDrawMode.Sliced;
        tab.size = ReferenceLayout.TagSize;

        var iconSprite = HandoffBuilder.LoadSprite("UI/category_hats");
        var icon = AddRenderer(root.transform, "Icon", iconSprite, Color.white, 0.62f / iconSprite.bounds.size.x,
            new Vector3(0f, 0.16f, 0f), SortOrder.TagIcon, masked: false);

        var label = AddLabel(root.transform, "Hats", new Vector2(0f, -0.38f), new Vector2(1.0f, 0.34f), 2.6f, SortOrder.TagIcon);

        var tag = root.AddComponent<CategoryTag>();
        SetRefs(tag, ("_tab", tab), ("_icon", icon), ("_label", label), ("_normal", normal),
                ("_active", HandoffBuilder.LoadSprite("UI/tab_category_active")));
        return SavePrefab<CategoryTag>(root, path);
    }

    // World TextMeshPro in ink, the handoff's ExtraBold, auto-sized down to 65% so long words fit.
    public static TextMeshPro AddLabel(Transform parent, string text, Vector2 position, Vector2 size, float fontSize, int order)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = go.AddComponent<TextMeshPro>();
        label.font = HandoffBuilder.BoldFont();
        label.text = text;
        label.fontSize = fontSize;
        label.color = InkColor;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize * 0.65f;
        label.fontSizeMax = fontSize;
        label.sortingOrder = order;
        return label;
    }

    // ItemTile: one cell (CellSize), pivot at its centre: the item icon in 88% of the cell (as the
    // prototype) and a rounded flash behind it. Both masked by the board, so a wrapping tile is clipped.
    private static ItemTile BuildItemTilePrefab()
    {
        string path = $"{PrefabFolder}/ItemTile.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<ItemTile>(path);
        if (existing != null) return existing;

        var root = new GameObject("ItemTile");
        var flash = AddRenderer(root.transform, "Flash", HandoffBuilder.LoadSprite("UI/row_highlight"),
            new Color(0.87f, 0.95f, 0.64f, 0f), 1f, Vector3.zero, SortOrder.TileFlash, masked: true);
        flash.drawMode = SpriteDrawMode.Sliced;
        flash.size = CellSize - Vector2.one * 0.12f;

        var item = AddRenderer(root.transform, "Item", HandoffBuilder.LoadSprite("Items/red_cap"), Color.white, 1f,
            Vector3.zero, SortOrder.TileItem, masked: true);

        var tile = root.AddComponent<ItemTile>();
        var so = new SerializedObject(tile);
        so.FindProperty("_item").objectReferenceValue = item;
        so.FindProperty("_flash").objectReferenceValue = flash;
        so.FindProperty("_size").vector2Value = CellSize;
        so.FindProperty("_itemBox").vector2Value = CellSize * 0.88f;
        so.ApplyModifiedPropertiesWithoutUndo();
        tile.Setup(new TileData(0), item.sprite, "Red cap");
        tile.name = "ItemTile";
        return SavePrefab<ItemTile>(root, path);
    }

    public static SpriteRenderer AddRenderer(Transform parent, string childName, Sprite sprite, Color color,
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

    private static void FillFileRef(Cell cell, ItemTile itemTile, CategoryTag tag, Dictionary<string, Customer> customers)
    {
        var fileRef = FileRef.Instance;      // creates Resources/FileRef.asset if missing
        var so = new SerializedObject(fileRef);

        SetIfEmpty(so.FindProperty("_cellPrefab"), cell);
        SetIfEmpty(so.FindProperty("_itemTilePrefab"), itemTile);
        SetIfEmpty(so.FindProperty("_categoryTagPrefab"), tag);
        SetIfEmpty(so.FindProperty("_spark"), LoadSprite(SolidSprite));

        HandoffBuilder.FillCatalog(so, customers);

        if (so.FindProperty("_music").objectReferenceValue == null)
        {
            var music = AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder });
            if (music.Length > 0)
                so.FindProperty("_music").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(music[0]));
        }

        var sounds = so.FindProperty("_sounds");
        if (sounds.arraySize == 0)
        {
            foreach (SoundType type in Enum.GetValues(typeof(SoundType)))
            {
                var clips = FindSoundClips(type);
                if (clips.Length == 0) continue;
                var entry = AppendElement(sounds);
                entry.FindPropertyRelative("Type").intValue = (int)type;
                entry.FindPropertyRelative("Volume").floatValue = 1f;
                var clipArray = entry.FindPropertyRelative("Clips");
                clipArray.arraySize = clips.Length;
                for (int i = 0; i < clips.Length; i++) clipArray.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(fileRef);
    }

    // SoundType.TileMatch -> SFX/tile_match.wav, or tile_match_01.wav, tile_match_02.wav... in name order.
    private static AudioClip[] FindSoundClips(SoundType type)
    {
        string baseName = Regex.Replace(type.ToString(), "(?<=[a-z0-9])([A-Z])", "_$1").ToLowerInvariant();
        var pattern = new Regex($"^{baseName}(_\\d+)?$");
        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (pattern.IsMatch(Path.GetFileNameWithoutExtension(path))) paths.Add(path);
        }
        paths.Sort(StringComparer.Ordinal);
        return paths.ConvertAll(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
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
