using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Chichingo.TilewrapVaried;

namespace Chichingo.TilewrapVaried.Editor
{
    public static class TilewrapContentImporter
    {
        private const string Root = "Assets/TilewrapVaried";
        [MenuItem("Tools/Chichingo/Tilewrap/Build 30-level content")]
        public static void Build()
        {
            var manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/content.json");
            if (manifestAsset == null) throw new FileNotFoundException("Missing Tilewrap content.json");
            var manifest = JsonUtility.FromJson<ContentManifest>(manifestAsset.text);
            if (manifest == null || manifest.schemaVersion != 2) throw new FormatException("Unsupported manifest.");
            string generated = Root + "/Generated";
            Directory.CreateDirectory(generated + "/Rigs");
            AssetDatabase.Refresh();
            var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (var record in manifest.sprites)
            {
                var ti = AssetImporter.GetAtPath(record.path) as TextureImporter;
                if (ti == null) throw new FileNotFoundException(record.path);
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100;
                ti.alphaIsTransparency = true;
                ti.sRGBTexture = true;
                ti.mipmapEnabled = false;
                ti.isReadable = false;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.filterMode = FilterMode.Bilinear;
                ti.wrapMode = TextureWrapMode.Clamp;
                // Keep high-resolution masters in the handoff while limiting mobile memory.
                // Case art is displayed at about 240 px; shared UI includes the full backdrop.
                int maxSize = record.path.Contains("/UI/") ? 2048 : 512;
                ti.maxTextureSize = maxSize;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(record.pivot[0], record.pivot[1]);
                settings.spriteBorder = new Vector4(record.border[0], record.border[1], record.border[2], record.border[3]);
                ti.SetTextureSettings(settings);
                foreach (string platform in new[] { "Android", "iPhone" })
                {
                    var ps = ti.GetPlatformTextureSettings(platform);
                    ps.name = platform;
                    ps.overridden = true;
                    ps.maxTextureSize = maxSize;
                    ps.format = TextureImporterFormat.ASTC_4x4;
                    ps.compressionQuality = 100;
                    ti.SetPlatformTextureSettings(ps);
                }
                ti.SaveAndReimport();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(record.path);
                if (sprite == null) throw new InvalidOperationException("Sprite import failed: " + record.path);
                sprites.Add(record.key, sprite);
            }
            var rigEntries = new List<RigEntry>();
            foreach (var record in manifest.rigs)
            {
                var root = new GameObject(record.id, typeof(RectTransform));
                try
                {
                    var rt = (RectTransform)root.transform;
                    rt.sizeDelta = new Vector2(record.width, record.height);
                    var view = root.AddComponent<TilewrapRigView>();
                    view.nativeSize = rt.sizeDelta;
                    var bindings = new List<RigLayerBinding>();
                    Array.Sort(record.layers, (a, b) => a.order.CompareTo(b.order));
                    foreach (var layer in record.layers)
                    {
                        var go = new GameObject(layer.sprite, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        var child = (RectTransform)go.transform;
                        child.SetParent(rt, false);
                        child.anchorMin = child.anchorMax = child.pivot = new Vector2(0, 1);
                        child.anchoredPosition = new Vector2(layer.rect[0], -layer.rect[1]);
                        child.sizeDelta = new Vector2(layer.rect[2], layer.rect[3]);
                        var image = go.GetComponent<Image>();
                        image.sprite = sprites[layer.sprite];
                        image.color = Color.white;
                        image.preserveAspect = false;
                        image.raycastTarget = false;
                        bindings.Add(new RigLayerBinding { layer = go, goalIndex = layer.goalIndex, expectedItem = layer.itemId });
                        go.SetActive(layer.goalIndex < 0);
                    }
                    view.bindings = bindings.ToArray();
                    string path = generated + "/Rigs/" + record.id + ".prefab";
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                    if (saved == null) throw new InvalidOperationException("Could not create " + path);
                    rigEntries.Add(new RigEntry { id = record.id, prefab = saved.GetComponent<TilewrapRigView>() });
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            string libraryPath = generated + "/TilewrapContentLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<TilewrapContentLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<TilewrapContentLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
            }
            library.contentId = manifest.contentId;
            var entries = new List<SpriteEntry>();
            foreach (var pair in sprites) entries.Add(new SpriteEntry { id = pair.Key, sprite = pair.Value });
            library.sprites = entries.ToArray();
            library.rigs = rigEntries.ToArray();
            var levels = new List<TextAsset>();
            foreach (string path in manifest.levelPaths)
            {
                var source = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                TilewrapCaseData.Parse(source);
                levels.Add(source);
            }
            library.levels = levels.ToArray();
            library.uiLayout = AssetDatabase.LoadAssetAtPath<TextAsset>(manifest.uiLayoutPath);
            library.campaign = AssetDatabase.LoadAssetAtPath<TextAsset>(manifest.campaignPath);
            library.hintDistances = AssetDatabase.LoadAssetAtPath<TextAsset>(manifest.hintTablePath);
            if (library.uiLayout == null || library.campaign == null || library.hintDistances == null || library.hintDistances.bytes.Length != 362880)
                throw new InvalidOperationException("Missing UI data or invalid hint table.");
            foreach (var record in manifest.fonts)
            {
                var loaded = AssetDatabase.LoadAssetAtPath<Font>(record.path);
                if (loaded == null) throw new FileNotFoundException(record.path);
                if (record.key == "regular") library.regularFont = loaded;
                else if (record.key == "bold") library.boldFont = loaded;
            }
            var audio = new List<AudioEntry>();
            foreach (var record in manifest.audio)
            {
                var loaded = AssetDatabase.LoadAssetAtPath<AudioClip>(record.path);
                if (loaded == null) throw new FileNotFoundException(record.path);
                audio.Add(new AudioEntry { id = record.key, clip = loaded, volume = record.volume });
            }
            library.audio = audio.ToArray();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Selection.activeObject = library;
            Debug.Log("Tilewrap: imported " + library.levels.Length + " cases and " + library.rigs.Length + " registered subject prefabs.");
        }
    }
}
