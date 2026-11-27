using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;

// The Tilewrap Unity handoff (Assets/Tilewrap, copied as delivered; its sprite import settings come
// from its own helper, Tools > Chichingo > Tilewrap > Apply sprite import settings, which reads
// Data/game_config.json). This builds what the game needs from it:
//   Prefabs/Gameobjects/Customer.prefab          the base customer: five layer slots, accessory groups
//   Prefabs/Gameobjects/Customer_{Name}.prefab   one prefab variant per recipient, assigning its sprites
// fills FileRef's item, character and category tables from game_config.json (items: id, label, icon;
// characters: id, name, Customer variant, symbol; categories: label, icon), and makes the TMP font
// assets of the handoff's fonts (Fonts/TilewrapUI-*.ttf -> Game Assets/Fonts/TilewrapUI-* SDF.asset).
// Called by ShiftSort > Build Assets; the UI builders load sprites and fonts through it.
// Never overwrites: an existing prefab is kept, and FileRef tables are filled only when empty.
public static class HandoffBuilder
{
    public const string Root = "Assets/Tilewrap";
    public const string ConfigPath = Root + "/Data/game_config.json";
    private const string PrefabFolder = "Assets/Game Assets/Prefabs/Gameobjects";
    public const string CustomerPrefabPath = PrefabFolder + "/Customer.prefab";

    // The registered character canvas: 600 x 900 px at 100 px per unit, centre pivot.
    private const float LayerHeight = 9f;

    // Back to front, as game_config.json's characterRegistration.layerOrder. The suffix is the
    // sprite's file name after "{id}_"; the category is the board row that equips the layer
    // (null = always shown).
    private static readonly (string name, string suffix, string category)[] Layers =
    {
        ("Bag Rear", "bag_rear", "Bags"),
        ("Base", "base", null),
        ("Shirt", "shirt", "Shirts"),
        ("Bag Straps", "bag_straps", "Bags"),
        ("Hat", "hat", "Hats"),
    };

    [Serializable] private class Config { public Recipient[] recipients; public Item[] items; public Category[] categories; }
    [Serializable] private class Recipient { public string id; public string name; public string symbolSprite; }
    [Serializable] private class Category { public string id; public string label; public string sprite; }
    [Serializable] private class Item { public string id; public string label; public string sprite; }

    // A handoff sprite by its path under Assets/Tilewrap/Sprites, without ".png" ("UI/board_frame").
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

    // Dynamic SDF font asset made from Assets/Tilewrap/Fonts/{name}.ttf; glyphs are added as text needs them.
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

    private static Config LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            Debug.LogWarning($"[HandoffBuilder] {ConfigPath} is missing: copy the handoff's Assets/Tilewrap folder into the project.");
            return null;
        }
        return JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath));
    }

    [MenuItem("ShiftSort/Build Customers")]
    private static void BuildFromMenu()
    {
        BuildCustomers();
        AssetDatabase.SaveAssets();
    }

    // Returns each recipient id's Customer variant.
    public static Dictionary<string, Customer> BuildCustomers()
    {
        var customers = new Dictionary<string, Customer>();
        var config = LoadConfig();
        if (config?.recipients == null) return customers;

        var basePrefab = AssetDatabase.LoadAssetAtPath<Customer>(CustomerPrefabPath) ?? BuildBasePrefab();
        foreach (var recipient in config.recipients)
            customers[recipient.id] = BuildVariant(basePrefab, recipient);
        return customers;
    }

    // Customer (SortingGroup at SortOrder.Character, pivot = the art's bottom centre)
    //   Body (scaled by the equip pop)
    //     Bag Rear, Base, Shirt, Bag Straps, Hat: SpriteRenderers, orders 0..4, all at (0, 4.5)
    //     so the 9-unit-tall centre-pivot layers stand on the root. Sprites are left empty.
    private static Customer BuildBasePrefab()
    {
        var root = new GameObject("Customer");
        var group = root.AddComponent<SortingGroup>();
        group.sortingOrder = SortOrder.Character;
        var customer = root.AddComponent<Customer>();

        var body = new GameObject("Body").transform;
        body.SetParent(root.transform, false);

        var groups = new Dictionary<string, List<GameObject>>();
        var order = new List<string>();
        for (int i = 0; i < Layers.Length; i++)
        {
            var layer = new GameObject(Layers[i].name);
            layer.transform.SetParent(body, false);
            layer.transform.localPosition = new Vector3(0f, LayerHeight * 0.5f, 0f);
            layer.AddComponent<SpriteRenderer>().sortingOrder = i;

            string category = Layers[i].category;
            if (category == null) continue;
            if (!groups.TryGetValue(category, out var list))
            {
                groups[category] = list = new List<GameObject>();
                order.Add(category);
            }
            list.Add(layer);
        }

        var so = new SerializedObject(customer);
        so.FindProperty("_body").objectReferenceValue = body;
        var accessories = so.FindProperty("_accessories");
        accessories.arraySize = order.Count;
        for (int i = 0; i < order.Count; i++)
        {
            var entry = accessories.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("Category").stringValue = order[i];
            var layers = entry.FindPropertyRelative("Layers");
            layers.arraySize = groups[order[i]].Count;
            for (int j = 0; j < groups[order[i]].Count; j++)
                layers.GetArrayElementAtIndex(j).objectReferenceValue = groups[order[i]][j];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(PrefabFolder);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, CustomerPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        Debug.Log($"[HandoffBuilder] Built {CustomerPrefabPath}.");
        return prefab.GetComponent<Customer>();
    }

    // Customer_{Name}: a variant of the base that sets the id and the five layer sprites
    // (Assets/Tilewrap/Sprites/Characters/{id}/{id}_{suffix}.png).
    private static Customer BuildVariant(Customer basePrefab, Recipient recipient)
    {
        string path = $"{PrefabFolder}/Customer_{recipient.name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<Customer>(path);
        if (existing != null) return existing;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab.gameObject);
        instance.name = $"Customer_{recipient.name}";
        var so = new SerializedObject(instance.GetComponent<Customer>());
        so.FindProperty("_id").stringValue = recipient.id;
        so.ApplyModifiedPropertiesWithoutUndo();

        var body = instance.transform.Find("Body");
        foreach (var layer in Layers)
        {
            string spritePath = $"{Root}/Sprites/Characters/{recipient.id}/{recipient.id}_{layer.suffix}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null) Debug.LogWarning($"[HandoffBuilder] Missing layer {spritePath}.");
            body.Find(layer.name).GetComponent<SpriteRenderer>().sprite = sprite;
        }

        var variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
        UnityEngine.Object.DestroyImmediate(instance);
        Debug.Log($"[HandoffBuilder] Built {path}.");
        return variant.GetComponent<Customer>();
    }

    // FileRef._items from the config's items (id, label, icon at Sprites/Items/{sprite}.png) and
    // FileRef._characters from its recipients (id, name, Customer variant). Each only when empty.
    public static void FillCatalog(SerializedObject fileRef, Dictionary<string, Customer> customers)
    {
        var config = LoadConfig();
        if (config == null) return;

        var items = fileRef.FindProperty("_items");
        if (items.arraySize == 0 && config.items != null)
        {
            foreach (var item in config.items)
            {
                var entry = Append(items);
                entry.FindPropertyRelative("Id").stringValue = item.id;
                entry.FindPropertyRelative("Name").stringValue = item.label;
                entry.FindPropertyRelative("Sprite").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Sprites/Items/{item.sprite}.png");
            }
        }

        var characters = fileRef.FindProperty("_characters");
        if (characters.arraySize == 0 && config.recipients != null)
        {
            foreach (var recipient in config.recipients)
            {
                var entry = Append(characters);
                entry.FindPropertyRelative("Id").stringValue = recipient.id;
                entry.FindPropertyRelative("Name").stringValue = recipient.name;
                customers.TryGetValue(recipient.id, out var customer);
                entry.FindPropertyRelative("Customer").objectReferenceValue = customer;
                entry.FindPropertyRelative("Symbol").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Sprites/UI/{recipient.symbolSprite}.png");
            }
        }

        var categories = fileRef.FindProperty("_categories");
        if (categories.arraySize == 0 && config.categories != null)
        {
            foreach (var category in config.categories)
            {
                var entry = Append(categories);
                entry.FindPropertyRelative("Label").stringValue = category.label;
                entry.FindPropertyRelative("Icon").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{Root}/Sprites/UI/{category.sprite}.png");
            }
        }
    }

    private static SerializedProperty Append(SerializedProperty array)
    {
        array.arraySize++;
        return array.GetArrayElementAtIndex(array.arraySize - 1);
    }
}
