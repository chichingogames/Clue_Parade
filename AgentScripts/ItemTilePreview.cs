using System.IO;
using UnityEditor;
using UnityEngine;

// Agent check for the ItemTile prefab (Clue Parade redesign). Run from the CLI:
//   unity command script run --file AgentScripts/ItemTilePreview.cs --entry ItemTilePreview.Render --args '["<out.png>"]'
// Instantiates four tiles far from the scene content, puts a SpriteMask over the first three and half
// of the fourth (a wrapping line), renders them with a temporary camera to a PNG, and removes
// everything again. The scene is not modified. Returns a short report for the CLI.
public static class ItemTilePreview
{
    private static readonly string[] Items = { "hat_sunhat_purple", "gear_basketball_blue", "bag_red", "gear_guitar_brown" };
    private static readonly string[] Names = { "Sunhat", "Basketball", "Red bag", "Guitar" };

    public static string Render(string outputPath)
    {
        var prefab = FileRef.Instance.ItemTilePrefab;
        if (prefab == null) return "FileRef has no ItemTile prefab: run ShiftSort > Build Assets.";

        var root = new GameObject("_ItemTilePreview");
        root.hideFlags = HideFlags.DontSave;
        root.transform.position = new Vector3(1000f, 0f, 0f);
        float w = AssetBuilder.ItemTileSize.x, h = AssetBuilder.ItemTileSize.y;
        try
        {
            for (int i = 0; i < Items.Length; i++)
            {
                var tile = (ItemTile)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                tile.transform.localPosition = new Vector3((i - 1.5f) * w, 0f, 0f);
                tile.Setup(AssetBuilder.LoadTilewrap("Items/" + Items[i]), Names[i],
                           i == 2 ? AssetBuilder.LoadTilewrap("UI/tile_honey") : null);
            }

            // Mask from the left edge of tile 0 to the middle of tile 3.
            var mask = new GameObject("Mask").AddComponent<SpriteMask>();
            mask.transform.SetParent(root.transform, false);
            mask.sprite = AssetBuilder.LoadSprite(AssetBuilder.SolidSprite);
            float left = -2f * w, right = 1.5f * w;
            mask.transform.localPosition = new Vector3((left + right) * 0.5f, 0f, 0f);
            mask.transform.localScale = new Vector3(right - left, h, 1f);

            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.transform.SetParent(root.transform, false);
            cam.transform.localPosition = new Vector3(0f, 0f, -10f);
            cam.orthographic = true;
            cam.orthographicSize = 1.85f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x6E, 0x41, 0x55, 0xFF);   // the booth wall

            const int width = 1300, height = 400;
            var rt = new RenderTexture(width, height, 24);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cam.targetTexture = rt;
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(texture);

            var label = prefab.GetComponentInChildren<TMPro.TextMeshPro>();
            return $"Wrote {outputPath}. Label material: {label.fontSharedMaterial.name} " +
                   $"(stencil {label.fontSharedMaterial.GetFloat("_Stencil")}).";
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
