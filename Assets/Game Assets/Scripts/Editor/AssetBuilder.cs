using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

// Imports the art (the Tilewrap pack with its manifest settings), creates the placeholder sprites still
// needed (the board mask), the Cell / ItemTile / CategoryTag prefabs, and fills Resources/FileRef.asset
// from the art by file name:
//   Sprites/Tilewrap/Items/{item id}.png, Characters/character_{id}_{neutral|gear|bag|complete}.png
//   Audio/Music/*.wav (first one = music), Audio/SFX/{sound_type}.wav or {sound_type}_NN.wav (variants)
// Re-runnable: existing prefabs are kept (so prefab edits survive) and FileRef tables are only filled when empty.
public static class AssetBuilder
{
    private const string SpriteFolder = "Assets/Game Assets/Sprites/Placeholder";
    private const string ArtFolder = "Assets/Game Assets/Sprites/Sprites";
    private const string PrefabFolder = "Assets/Game Assets/Prefabs/Gameobjects";
    private const string MusicFolder = "Assets/Game Assets/Audio/Music";
    private const string SfxFolder = "Assets/Game Assets/Audio/SFX";
    private const int Resolution = 128;

    // Rounded panels, drawn 9-sliced (ApplyPanel) so their corners stay sharp at any size: a stretched
    // 128 px sprite looked blurry on a big board. Both are 1 world unit wide at scale 1 (ppu = width).
    //   BoardFrame: the board frame only, 512 px, the most detail where the corner is largest on screen.
    //   Panel:      slots and target backgrounds, 256 px.
    public const string BoardFrameSprite = "BoardFrame";
    private const int BoardFrameResolution = 512;
    private const int BoardFrameCornerPixels = 128;
    public const float BoardFrameCornerRadius = (float)BoardFrameCornerPixels / BoardFrameResolution;   // 0.25 at scale 1

    public const string PanelSprite = "Panel";
    private const int PanelResolution = 256;
    private const int PanelCornerPixels = 64;
    public const float PanelCornerRadius = (float)PanelCornerPixels / PanelResolution;   // 0.25 at scale 1

    private const int PanelBorderMargin = 8;           // slice border = corner + this, for the anti-aliased edge
    public const string SolidSprite = "Solid";

    [MenuItem("ShiftSort/Build Assets")]
    public static void Build()
    {
        GenerateSprites();
        ImportArt();
        ImportTilewrapArt();
        ImportAudio();
        var cell = BuildCellPrefab();
        var itemTile = BuildItemTilePrefab();
        var tag = BuildCategoryTagPrefab();
        FillFileRef(cell, itemTile, tag);
        AssetDatabase.SaveAssets();
        Debug.Log("[AssetBuilder] Assets are up to date.");
    }

    public static Sprite LoadSprite(string spriteName) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{spriteName}.png");

    // ---------- Sprites ----------

    private static void GenerateSprites()
    {
        Directory.CreateDirectory(SpriteFolder);
        WritePanelSprite(BoardFrameSprite, BoardFrameResolution, BoardFrameCornerPixels);
        WritePanelSprite(PanelSprite, PanelResolution, PanelCornerPixels);
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

    // A white rounded box filling an n x n texture, imported as a 9-slice sprite with mipmaps.
    private static void WritePanelSprite(string spriteName, int n, int cornerPixels)
    {
        string path = $"{SpriteFolder}/{spriteName}.png";
        if (File.Exists(path)) return;

        float half = n * 0.5f;
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                // Pixel-space SDF of a rounded box 1 px inside the texture edge.
                var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float d = RoundedBox(p, half - 1f, cornerPixels);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
            }
        }

        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = n;              // full sprite = 1 world unit
        importer.spriteBorder = Vector4.one * (cornerPixels + PanelBorderMargin);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;                 // shown smaller than its pixels: mips keep the edge smooth
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;   // required for 9-slicing
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    // Shows the Panel sprite 9-sliced on r: size in r's parent space, cornerRadius in the same units.
    public static void ApplyPanel(SpriteRenderer r, Vector2 size, float cornerRadius)
    {
        float scale = cornerRadius / PanelCornerRadius;
        r.sprite = LoadSprite(PanelSprite);
        r.drawMode = SpriteDrawMode.Sliced;
        r.transform.localScale = Vector3.one * scale;
        r.size = size / scale;
    }

    private static float RoundedBox(Vector2 p, float halfSize, float radius)
    {
        var d = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - Vector2.one * (halfSize - radius);
        return Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
    }

    // ---------- Art ----------

    // 9-slice borders (px, all four sides) of the art drawn Sliced, from the art's Sprite_Manifest.json.
    private static readonly Dictionary<string, float> SliceBorders = new Dictionary<string, float>
    {
        { "fx_guide_outline", 34f },
        { "fx_line_pulse", 34f },
    };

    // Every art sprite is imported so that its full texture is 1 world unit wide (ppu = width);
    // Manager_Board then scales tiles and markers in cell units.
    private static void ImportArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { $"{ArtFolder}/tiles", $"{ArtFolder}/targets", $"{ArtFolder}/fx" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.GetSourceTextureWidthAndHeight(out int width, out _);
            SliceBorders.TryGetValue(Path.GetFileNameWithoutExtension(path), out float border);
            var borderLTRB = Vector4.one * border;

            bool changed = importer.textureType != TextureImporterType.Sprite
                           || importer.spriteImportMode != SpriteImportMode.Single
                           || !Mathf.Approximately(importer.spritePixelsPerUnit, width)
                           || importer.mipmapEnabled
                           || importer.spriteBorder != borderLTRB;
            if (!changed) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = width;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spriteBorder = borderLTRB;
            if (border > 0f)
            {
                // Drawn Sliced (SpriteRenderer.size), which needs a FullRect mesh.
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
            }
            importer.SaveAndReimport();
        }
    }

    // ---------- Tilewrap art (Clue Parade) ----------

    // Art from the Tilewrap fair-booth pack, imported with the settings in its art_manifest.json:
    // per-asset PPU (200 for most UI, which is drawn at 2x its logical size; 100 for the rest), pivot
    // and 9-slice border (left, bottom, right, top, in source pixels). Uncompressed until signed off.
    public const string TilewrapFolder = "Assets/Game Assets/Sprites/Tilewrap";

    [Serializable]
    private class TilewrapManifest
    {
        public TilewrapAsset[] assets;
    }

    [Serializable]
    private class TilewrapAsset
    {
        public string id;
        public string path;          // relative to the pack root; its "Sprites/" level is dropped in this project
        public float[] border;
        public float[] pivot;
        public float pixelsPerUnit;
    }

    private static string TilewrapPath(string manifestPath) =>
        $"{TilewrapFolder}/{(manifestPath.StartsWith("Sprites/") ? manifestPath.Substring("Sprites/".Length) : manifestPath)}";

    private static void ImportTilewrapArt()
    {
        string manifestPath = $"{TilewrapFolder}/art_manifest.json";
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning($"[AssetBuilder] {manifestPath} is missing; Tilewrap art not imported.");
            return;
        }

        var manifest = JsonUtility.FromJson<TilewrapManifest>(File.ReadAllText(manifestPath));
        foreach (var asset in manifest.assets)
        {
            string path = TilewrapPath(asset.path);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                Debug.LogWarning($"[AssetBuilder] Tilewrap asset {asset.id} not found at {path}.");
                continue;
            }

            var border = new Vector4(asset.border[0], asset.border[1], asset.border[2], asset.border[3]);
            var pivot = new Vector2(asset.pivot[0], asset.pivot[1]);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);

            bool changed = importer.textureType != TextureImporterType.Sprite
                           || importer.spriteImportMode != SpriteImportMode.Single
                           || !Mathf.Approximately(importer.spritePixelsPerUnit, asset.pixelsPerUnit)
                           || importer.spriteBorder != border
                           || importer.mipmapEnabled
                           || settings.spriteMeshType != SpriteMeshType.FullRect
                           || settings.spritePivot != pivot;
            if (!changed) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = asset.pixelsPerUnit;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;     // required for 9-slicing
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
    }

    public static Sprite LoadTilewrap(string relativePath) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{TilewrapFolder}/{relativePath}.png");

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

    private static Sprite LoadArt(string relativePath) =>
        AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{relativePath}.png");


    // ---------- Prefabs ----------

    // Cell: a board position with the check a confirmed row shows (check_complete, top-right of the tile).
    private static Cell BuildCellPrefab()
    {
        string path = $"{PrefabFolder}/Cell.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<Cell>(path);
        if (existing != null) return existing;

        var root = new GameObject("Cell");
        var checkSprite = LoadTilewrap("UI/check_complete");
        float scale = ReferenceLayout.CheckSize / checkSprite.bounds.size.x;
        var check = AddRenderer(root.transform, "Check", checkSprite, Color.white, scale,
            ReferenceLayout.CheckOffset, SortOrder.TileCheck, masked: false);

        var cell = root.AddComponent<Cell>();
        SetRefs(cell, ("_check", check.gameObject));
        return SavePrefab<Cell>(root, path);
    }

    // CategoryTag: the pack's category_tag (122 x 88 px, sliced) with the row's label, Nunito ExtraBold 40.
    private static CategoryTag BuildCategoryTagPrefab()
    {
        string path = $"{PrefabFolder}/CategoryTag.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<CategoryTag>(path);
        if (existing != null) return existing;

        var root = new GameObject("CategoryTag");
        var background = AddRenderer(root.transform, "Background", LoadTilewrap("UI/category_tag"), Color.white, 1f,
            Vector3.zero, SortOrder.Tag, masked: false);
        background.drawMode = SpriteDrawMode.Sliced;
        background.size = ReferenceLayout.TagSize;

        // Label box 105 x 62 px, 3 px right of and 3 px above the tag's centre (shadow lip at the bottom).
        var label = AddLabel(root.transform, "Hats", new Vector2(0.03f, 0.03f), new Vector2(1.05f, 0.62f), 4.0f, SortOrder.TagLabel);
        label.font = UIBuilder.GetOrCreateFont(ItemTileFont);

        var tag = root.AddComponent<CategoryTag>();
        SetRefs(tag, ("_label", label));
        return SavePrefab<CategoryTag>(root, path);
    }

    // World TextMeshPro in the pack's text color, auto-sized down to 65% so long words fit.
    private static TextMeshPro AddLabel(Transform parent, string text, Vector2 position, Vector2 size, float fontSize, int order)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var label = go.AddComponent<TextMeshPro>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = ItemLabelColor;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;
        label.fontSizeMin = fontSize * 0.65f;
        label.fontSizeMax = fontSize;
        label.sortingOrder = order;
        return label;
    }

    // ItemTile: the pack's 296x256 tile in board units (1 unit = 100 reference px), pivot at its centre.
    // From the pack layout (top-left origin, px): item box 252x184 at (22, 10); name 282x50 at (7, 191),
    // Nunito ExtraBold 38, #421B19, centred. The pack puts the name at y 199 on the top rows and 191 on
    // the bottom row, where the frame's thicker bottom edge would cover it; 191 is used on every row.
    public const string ItemTileFont = "TilewrapNunito-ExtraBold";
    public static readonly Vector2 ItemTileSize = new Vector2(2.96f, 2.56f);
    private static readonly Color ItemLabelColor = new Color32(0x42, 0x1B, 0x19, 0xFF);

    private static ItemTile BuildItemTilePrefab()
    {
        string path = $"{PrefabFolder}/ItemTile.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<ItemTile>(path);
        if (existing != null) return existing;

        var root = new GameObject("ItemTile");

        // Sliced so the surface keeps its 12 px edge at any tile size.
        var surface = AddRenderer(root.transform, "Surface", LoadTilewrap("UI/tile_cream"), Color.white, 1f,
            Vector3.zero, SortOrder.TileBody, masked: true);
        surface.drawMode = SpriteDrawMode.Sliced;
        surface.size = ItemTileSize;

        // Box centre (148, 102) px from the tile's top-left = 0.26 above the tile centre.
        var item = AddRenderer(root.transform, "Item", LoadTilewrap("Items/hat_sunhat_purple"), Color.white, 1f,
            new Vector3(0f, 0.26f, 0f), SortOrder.TileItem, masked: true);

        // Label centre (148, 216) px = 0.88 below the tile centre. World TMP: font size 10 ~ 1 unit.
        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(root.transform, false);
        var labelRect = (RectTransform)labelGo.transform;
        labelRect.anchoredPosition = new Vector2(0f, -0.88f);
        labelRect.sizeDelta = new Vector2(2.82f, 0.5f);
        var label = labelGo.AddComponent<TextMeshPro>();
        label.font = UIBuilder.GetOrCreateFont(ItemTileFont);
        label.fontSharedMaterial = GetOrCreateMaskedFontMaterial(label.font);
        label.text = "Sunhat";
        label.fontSize = 3.8f;
        label.color = ItemLabelColor;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;          // long names shrink instead of spilling over the tile
        label.fontSizeMin = 2.4f;
        label.fontSizeMax = 3.8f;
        label.sortingOrder = SortOrder.TileLabel;

        var tile = root.AddComponent<ItemTile>();
        SetRefs(tile, ("_surface", surface), ("_item", item), ("_label", label));
        tile.Setup(new TileData(0), item.sprite, label.text);
        tile.name = "ItemTile";
        return SavePrefab<ItemTile>(root, path);
    }

    // World TextMeshPro ignores SpriteMask, so a tile label would show past the board edge while its
    // line wraps. SpriteMask (VisibleInsideMask) clips through the stencil buffer: this copy of the
    // font material draws only where the stencil is >= 1, i.e. inside the board's mask.
    private static Material GetOrCreateMaskedFontMaterial(TMP_FontAsset font)
    {
        string path = $"Assets/Game Assets/Fonts/{font.name} Masked.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var material = new Material(font.material) { name = $"{font.name} Masked" };
        material.SetFloat("_Stencil", 1f);
        material.SetFloat("_StencilComp", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        material.SetFloat("_StencilReadMask", 255f);
        AssetDatabase.CreateAsset(material, path);
        return material;
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

    // Item names the art's file names can't give ("hat_sunhat_purple" is shown as "Sunhat").
    // Anything else gets "{color} {thing}" from its file name (bag_blue -> "Blue bag").
    private static readonly Dictionary<string, string> ItemNames = new Dictionary<string, string>
    {
        { "hat_sunhat_purple", "Sunhat" },
        { "hat_cap_red", "Red cap" },
        { "hat_beanie_teal", "Teal beanie" },
        { "gear_skateboard_yellow", "Skateboard" },
        { "gear_basketball_blue", "Basketball" },
        { "gear_guitar_brown", "Guitar" },
    };

    // Character pose order: Poses[k] shows after k confirmed steps.
    private static readonly string[] CharacterPoses = { "neutral", "gear", "bag", "complete" };

    private static string ItemName(string id)
    {
        if (ItemNames.TryGetValue(id, out string name)) return name;
        var parts = id.Split('_');
        string thing = parts.Length >= 3 ? parts[1] : parts[0];
        string color = parts[parts.Length - 1];
        return Capitalize(parts.Length >= 2 ? $"{color} {thing}" : thing);
    }

    private static string Capitalize(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

    private static void FillFileRef(Cell cell, ItemTile itemTile, CategoryTag tag)
    {
        var fileRef = FileRef.Instance;      // creates Resources/FileRef.asset if missing
        var so = new SerializedObject(fileRef);

        SetIfEmpty(so.FindProperty("_cellPrefab"), cell);
        SetIfEmpty(so.FindProperty("_itemTilePrefab"), itemTile);
        SetIfEmpty(so.FindProperty("_categoryTagPrefab"), tag);
        SetIfEmpty(so.FindProperty("_tileSurface"), LoadTilewrap("UI/tile_cream"));
        SetIfEmpty(so.FindProperty("_tileSurfaceActive"), LoadTilewrap("UI/tile_honey"));

        var items = so.FindProperty("_items");
        if (items.arraySize == 0)
        {
            var paths = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { $"{TilewrapFolder}/Items" }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                string id = Path.GetFileNameWithoutExtension(path);
                var entry = AppendElement(items);
                entry.FindPropertyRelative("Id").stringValue = id;
                entry.FindPropertyRelative("Name").stringValue = ItemName(id);
                entry.FindPropertyRelative("Sprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }

        var characters = so.FindProperty("_characters");
        if (characters.arraySize == 0)
        {
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            var pattern = new Regex(@"^character_([a-z0-9]+)_");
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { $"{TilewrapFolder}/Characters" }))
            {
                var match = pattern.Match(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
                if (match.Success) ids.Add(match.Groups[1].Value);
            }
            foreach (string id in ids)
            {
                var entry = AppendElement(characters);
                entry.FindPropertyRelative("Id").stringValue = id;
                entry.FindPropertyRelative("Name").stringValue = Capitalize(id);
                var poses = entry.FindPropertyRelative("Poses");
                poses.arraySize = 0;
                foreach (string pose in CharacterPoses)
                {
                    var sprite = LoadTilewrap($"Characters/character_{id}_{pose}");
                    if (sprite == null) break;      // poses must be consecutive
                    AppendElement(poses).objectReferenceValue = sprite;
                }
            }
        }

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
        var paths = new System.Collections.Generic.List<string>();
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
