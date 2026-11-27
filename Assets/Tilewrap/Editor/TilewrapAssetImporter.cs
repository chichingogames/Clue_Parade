using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Chichingo.Tilewrap.Editor
{
    // Editor-only import helper. No runtime dependencies or generated scene content.
    public static class TilewrapAssetImporter
    {
        private const string ConfigPath = "Assets/Tilewrap/Data/game_config.json";

        [Serializable] private sealed class Config { public SpriteEntry[] sprites; }
        [Serializable] private sealed class SpriteEntry
        {
            public string key;
            public string path;
            public int width;
            public int height;
            public float[] pivot;
            public float[] border;
        }

        [MenuItem("Tools/Chichingo/Tilewrap/Apply sprite import settings")]
        public static void Apply()
        {
            if (!File.Exists(ConfigPath))
                throw new FileNotFoundException("Copy the supplied Assets/Tilewrap folder into this project first.", ConfigPath);
            var config = JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath));
            if (config == null || config.sprites == null)
                throw new InvalidDataException("game_config.json has no sprites array.");

            // Validate the complete list before changing any importer.
            foreach (var sprite in config.sprites)
            {
                if (sprite == null || string.IsNullOrEmpty(sprite.path) ||
                    !sprite.path.StartsWith("Assets/Tilewrap/Sprites/", StringComparison.Ordinal) ||
                    sprite.path.Contains("..") || !File.Exists(sprite.path) ||
                    sprite.pivot == null || sprite.pivot.Length != 2 ||
                    sprite.border == null || sprite.border.Length != 4 ||
                    !(AssetImporter.GetAtPath(sprite.path) is TextureImporter))
                    throw new InvalidDataException("Missing or invalid sprite entry: " + sprite?.key);
            }

            int updated = 0;
            try
            {
                foreach (var sprite in config.sprites)
                {
                    EditorUtility.DisplayProgressBar("Tilewrap sprites", sprite.key, (float)updated / config.sprites.Length);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(sprite.path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 100;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.alphaIsTransparency = true;
                    importer.sRGBTexture = true;
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.crunchedCompression = false;
                    importer.maxTextureSize = 2048;
                    importer.isReadable = false;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteAlignment = (int)SpriteAlignment.Custom;
                    settings.spritePivot = new Vector2(sprite.pivot[0], sprite.pivot[1]);
                    settings.spriteBorder = new Vector4(sprite.border[0], sprite.border[1], sprite.border[2], sprite.border[3]);
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                    updated++;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            Debug.Log("Tilewrap: applied source import settings to " + updated + " sprites. Use Sliced Image mode for entries with nonzero borders.");
        }
    }
}
