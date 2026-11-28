using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// The Tilewrap content pack (Assets/TilewrapVaried, copied as delivered). Its own importer,
// Tools > Chichingo > Tilewrap > Build 30-level content, applies the sprite import settings from
// Data/content.json, builds the 90 registered rig prefabs and Generated/TilewrapContentLibrary.asset.
// This gives the game's builders what they need from it: its sprites by path, the TMP font assets of
// its fonts (Fonts/TilewrapUI-*.ttf -> Game Assets/Fonts/TilewrapUI-* SDF.asset) and the masked
// material the board's tile names use. Never overwrites an existing font asset or material.
public static class HandoffBuilder
{
    public const string Root = "Assets/TilewrapVaried";
    public const string LibraryPath = Root + "/Generated/TilewrapContentLibrary.asset";

    // A pack sprite by its path under Assets/TilewrapVaried/Sprites, without ".png" ("UI/board_frame").
    public static Sprite LoadSprite(string relativePath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Sprites/{relativePath}.png");
        if (sprite == null) Debug.LogError($"[HandoffBuilder] Missing sprite {Root}/Sprites/{relativePath}.png");
        return sprite;
    }

    // ---------- Fonts ----------

    private const string FontAssetFolder = "Assets/Game Assets/Fonts";

    // ExtraBold for goals, names and buttons; Regular for body copy (the handoff's rule).
    public static TMP_FontAsset BoldFont() => GetOrCreateFont("TilewrapUI-ExtraBold");
    public static TMP_FontAsset RegularFont() => GetOrCreateFont("TilewrapUI-Regular");

    // ExtraBold drawn only inside the board's SpriteMask (stencil ref 1, Equal), so a tile's name is
    // clipped with its icon while a line wraps.
    public static Material MaskedBoldMaterial()
    {
        string path = $"{FontAssetFolder}/TilewrapUI-ExtraBold SDF Masked.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var material = new Material(BoldFont().material) { name = "TilewrapUI-ExtraBold SDF Masked" };
        material.SetFloat("_Stencil", 1f);
        material.SetFloat("_StencilComp", 4f);      // Equal
        material.SetFloat("_StencilOp", 0f);        // Keep
        material.SetFloat("_StencilReadMask", 255f);
        material.SetFloat("_StencilWriteMask", 255f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Dynamic SDF font asset made from Assets/TilewrapVaried/Fonts/{name}.ttf; glyphs are added as text needs them.
    private static TMP_FontAsset GetOrCreateFont(string fontName)
    {
        string path = $"{FontAssetFolder}/{fontName} SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>($"{Root}/Fonts/{fontName}.ttf");
        if (font == null)
        {
            Debug.LogError($"[HandoffBuilder] Missing font {Root}/Fonts/{fontName}.ttf");
            return null;
        }
        Directory.CreateDirectory(FontAssetFolder);
        var asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        asset.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(asset, path);
        asset.atlasTextures[0].name = $"{asset.name} Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        asset.material.name = $"{asset.name} Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        AssetDatabase.SaveAssets();
        return asset;
    }
}
