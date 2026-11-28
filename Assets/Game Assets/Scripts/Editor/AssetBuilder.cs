using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using Chichingo.TilewrapMystery;
using UnityEngine;

// Builds the board prefabs on the content pack's art (Assets/TilewrapMystery, see HandoffBuilder) and
// fills Resources/FileRef.asset:
//   Sprites/Placeholder/Solid.png   generated white square (board mask, dividers)
//   Prefabs/Gameobjects/Cell, ItemTile
//   FileRef: prefabs, the content library (TilewrapMystery/Generated, made by the pack's importer),
//   music (the pack's mystery_loop) and sounds: the pack's clips at their content.json gains where
//   PackKeys names one (TileMatch = match_1..3), else Audio/SFX/{sound_type}.wav or {sound_type}_NN.wav
// Re-runnable: existing prefabs are kept (so prefab edits survive) and FileRef tables are only filled when empty.
public static class AssetBuilder
{
    private const string SpriteFolder = "Assets/Game Assets/Sprites/Placeholder";
    private const string PrefabFolder = "Assets/Game Assets/Prefabs/Gameobjects";
    private const string MusicFolder = "Assets/Game Assets/Audio/Music";
    private const string SfxFolder = "Assets/Game Assets/Audio/SFX";
    private const int Resolution = 128;

    public const string SolidSprite = "Solid";

    // One board cell: the pack's 290 px cell (ReferenceLayout.CellSize).
    public static readonly Vector2 CellSize = Vector2.one * ReferenceLayout.CellSize;
    private static readonly Color InkColor = new Color32(0x40, 0x28, 0x1F, 0xFF);

    [MenuItem("ShiftSort/Build Assets")]
    public static void Build()
    {
        WriteSolidSprite();
        ImportAudio();
        var cell = BuildCellPrefab();
        var itemTile = BuildItemTilePrefab();
        FillFileRef(cell, itemTile);
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

    // Music streams (long, compressed); our effects decompress on load (short, played often, no
    // latency). The pack's own audio keeps the import settings its importer gives it.
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

    // Cell: a board position with the check badge a matched cell shows (top-right of the cell).
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

    // ItemTile: one cell (CellSize), pivot at its centre: the item icon (no name) centred in the
    // cell, and a rounded flash behind it. Masked by the board, so a tile dropping in from above is
    // clipped at the edge.
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

        var item = AddRenderer(root.transform, "Item", HandoffBuilder.LoadSprite("Items/crew_00"), Color.white, 1f,
            ReferenceLayout.ItemArtOffset, SortOrder.TileItem, masked: true);

        var tile = root.AddComponent<ItemTile>();
        var so = new SerializedObject(tile);
        so.FindProperty("_item").objectReferenceValue = item;
        so.FindProperty("_flash").objectReferenceValue = flash;
        so.FindProperty("_size").vector2Value = CellSize;
        so.FindProperty("_itemBox").vector2Value = ReferenceLayout.ItemArtBox;
        so.ApplyModifiedPropertiesWithoutUndo();
        tile.Setup("crew_00", item.sprite, "Red cap");
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

    private static void FillFileRef(Cell cell, ItemTile itemTile)
    {
        var fileRef = FileRef.Instance;      // creates Resources/FileRef.asset if missing
        var so = new SerializedObject(fileRef);

        SetIfEmpty(so.FindProperty("_cellPrefab"), cell);
        SetIfEmpty(so.FindProperty("_itemTilePrefab"), itemTile);
        var library = AssetDatabase.LoadAssetAtPath<TilewrapContentLibrary>(HandoffBuilder.LibraryPath);
        if (library == null) Debug.LogWarning($"[AssetBuilder] {HandoffBuilder.LibraryPath} is missing: run Tools > Chichingo > Tilewrap > Build current 30-level content.");
        SetIfEmpty(so.FindProperty("_content"), library);

        if (so.FindProperty("_music").objectReferenceValue == null)
        {
            var loop = PackClip(library, "mystery_loop", out _);
            if (loop == null)
            {
                var music = AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder });
                if (music.Length > 0) loop = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(music[0]));
            }
            so.FindProperty("_music").objectReferenceValue = loop;
        }

        var sounds = so.FindProperty("_sounds");
        if (sounds.arraySize == 0)
        {
            foreach (SoundType type in Enum.GetValues(typeof(SoundType)))
            {
                float[] gains = null;
                var clips = PackSounds(type, library, out gains) ?? FindSoundClips(type);
                if (clips.Length == 0) continue;
                var entry = AppendElement(sounds);
                entry.FindPropertyRelative("Type").intValue = (int)type;
                entry.FindPropertyRelative("Volume").floatValue = 1f;
                var clipArray = entry.FindPropertyRelative("Clips");
                clipArray.arraySize = clips.Length;
                for (int i = 0; i < clips.Length; i++) clipArray.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                var gainArray = entry.FindPropertyRelative("ClipVolumes");
                gainArray.arraySize = gains != null ? gains.Length : 0;
                for (int i = 0; i < gainArray.arraySize; i++) gainArray.GetArrayElementAtIndex(i).floatValue = gains[i];
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(fileRef);
    }

    // The pack's audio key(s) for each sound (Integration.md's event table); null = our own SFX file.
    private static string[] PackKeys(SoundType type) => type switch
    {
        SoundType.SwipeSettle => new[] { "swap" },
        SoundType.TileMatch => new[] { "match_1", "match_2", "match_3" },   // 1 set, double, triple+ cascade
        SoundType.LevelComplete => new[] { "level_win" },
        SoundType.OutOfMoves => new[] { "soft_fail" },
        SoundType.Undo => new[] { "undo" },
        SoundType.Invalid => new[] { "invalid" },
        SoundType.Clue => new[] { "clue" },
        SoundType.Notebook => new[] { "notebook" },
        SoundType.Hint => new[] { "hint" },
        SoundType.Refill => new[] { "refill" },
        SoundType.Coins => new[] { "coins" },
        SoundType.CaseClosed => new[] { "case_closed" },
        _ => null,
    };

    // The pack's clips for a sound at their content.json gains, or null.
    private static AudioClip[] PackSounds(SoundType type, TilewrapContentLibrary library, out float[] gains)
    {
        gains = null;
        var keys = PackKeys(type);
        if (keys == null) return null;
        var clips = new List<AudioClip>();
        var volumes = new List<float>();
        foreach (string key in keys)
        {
            var clip = PackClip(library, key, out float volume);
            if (clip == null) return null;
            clips.Add(clip);
            volumes.Add(volume);
        }
        gains = volumes.ToArray();
        return clips.ToArray();
    }

    private static AudioClip PackClip(TilewrapContentLibrary library, string key, out float volume)
    {
        volume = 1f;
        if (library == null || library.contentJson == null) return null;
        foreach (var record in library.Content.audio)
        {
            if (record.key != key) continue;
            volume = record.volume;
            return AssetDatabase.LoadAssetAtPath<AudioClip>(record.path);
        }
        return null;
    }

    // SoundType.Undo -> SFX/undo.wav, or undo_01.wav, undo_02.wav... in name order.
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
