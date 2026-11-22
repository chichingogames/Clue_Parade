using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Builds the HUD under Canvas/Container/InGame/Reference (see UI_InGame) on the Tilewrap pack's
// 1080x1920 layout (Layout_1080x1920.csv: every x, y, w, h below is in its top-left-origin pixels),
// and the loading screen from Sprites/Sprites/branding. Fonts: TMP font assets made from Fonts/*.ttf.
// Re-runnable: sprite import settings are (re)applied, font assets are created only if missing, and
// the HUD is built only when InGame has no UI_InGame yet. To rebuild it, delete InGame/Reference and
// the UI_InGame component, then run again.
public static class UIBuilder
{
    private const string ArtFolder = "Assets/Game Assets/Sprites/Sprites";
    private const string FontFolder = "Assets/Game Assets/Fonts";

    // Text colors from the pack: cream on the booth wall, ink on cream surfaces.
    private static readonly Color Cream = new Color32(0xFF, 0xF3, 0xDD, 0xFF);
    private static readonly Color Ink = new Color32(0x42, 0x1B, 0x19, 0xFF);

    private const string Bold = "TilewrapNunito-ExtraBold";     // goal, labels, counters, clues
    private const string Semi = "TilewrapNunito-SemiBold";      // stage and helper copy

    [MenuItem("ShiftSort/Build UI")]
    public static void Build()
    {
        ImportUiArt();
        var bold = GetOrCreateFont(Bold);
        var semi = GetOrCreateFont(Semi);

        var inGameGo = GameObject.Find("/Canvas/Container/InGame");
        if (inGameGo == null)
        {
            Debug.LogError("[UIBuilder] Canvas/Container/InGame is missing. Run ShiftSort > Build Scene first.");
            return;
        }
        if (inGameGo.GetComponent<UI_InGame>() != null)
        {
            Debug.Log("[UIBuilder] InGame already has UI_InGame; HUD left as is.");
            return;
        }

        // The 1080x1920 reference screen, fitted into the safe area like the camera fits the board.
        var root = Rect(inGameGo.transform, "Reference", Center, Center, Center, Vector2.zero, UI_ReferenceFit.ReferenceSize);
        Undo.AddComponent<UI_ReferenceFit>(root.gameObject);

        var ui = Undo.AddComponent<UI_InGame>(inGameGo);
        var so = new SerializedObject(ui);
        var intro = new System.Collections.Generic.List<RectTransform>();

        BuildTitle(root, so, bold, semi, intro);
        BuildClues(root, so, bold, semi, intro);
        BuildCharacters(root, so, bold, intro);
        intro.Add(Label(root, "Wrap Instruction", 160, 1636, 850, 65, semi, "Swipe rows or columns. Tiles wrap.", 38, Cream, TextAlignmentOptions.Top));
        BuildButtons(root, so, bold, intro);

        var items = so.FindProperty("_introItems");
        items.arraySize = intro.Count;
        for (int i = 0; i < intro.Count; i++) items.GetArrayElementAtIndex(i).objectReferenceValue = intro[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(inGameGo.scene);
        Debug.Log("[UIBuilder] HUD built.");
    }

    // ---------- Loading screen ----------

    // Branding art is 1080x1920; the progress bar sits on the loading art between the board
    // illustration and "A GAME BY" (centre 1470 px from the top), at its native size.
    private const float BrandingAspect = 1080f / 1920f;
    private const float ProgressBarY = 1f - 1470f / 1920f;
    private static readonly Color LoadingLabelColor = new Color32(0xD4, 0xB9, 0xC5, 0xFF);   // the art's "A GAME BY" color

    // Fills Canvas/Loading Screen (SceneBuilder's placeholder): outside Container, so the art covers
    // the whole screen, not just the safe area; moved to the last sibling so it draws over everything.
    // Skipped once it has UI_Loading.
    [MenuItem("ShiftSort/Build Loading Screen")]
    public static void BuildLoading()
    {
        ImportUiArt();
        var rootGo = GameObject.Find("/Canvas/Loading Screen");
        if (rootGo == null)
        {
            Debug.LogError("[UIBuilder] Canvas/Loading Screen is missing. Run ShiftSort > Build Scene first.");
            return;
        }
        if (rootGo.GetComponent<UI_Loading>() != null)
        {
            Debug.Log("[UIBuilder] Loading Screen already has UI_Loading; left as is.");
            return;
        }

        var root = (RectTransform)rootGo.transform;
        Undo.RecordObject(root, "Build Loading Screen");
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();
        Undo.AddComponent<CanvasGroup>(root.gameObject);
        var ui = Undo.AddComponent<UI_Loading>(root.gameObject);

        // Full-screen art; also blocks taps on the HUD underneath.
        var art = FullScreenArt(root, "Art", "branding/Tilewrap_Loading");
        art.GetComponent<Image>().raycastTarget = true;

        // Bar 506x25 with a 488x12 fill inset 9 px / 6.5 px (both sprites 9-sliced, rounded ends).
        var anchor = new Vector2(0.5f, ProgressBarY);
        var bar = Rect(art, "Progress Bar", anchor, anchor, Center, Vector2.zero, new Vector2(506f, 25f));
        AddImage(bar, Art("branding/Progress Bar"), sliced: true, sourceHeight: 1f, height: 1f);
        var fillArea = Rect(bar, "Fill Area", Vector2.zero, Vector2.one, Center, Vector2.zero, new Vector2(-18f, -13f));
        var fill = Rect(fillArea, "Fill", Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        AddImage(fill, Art("branding/Tilewrap_Progress_Fill"), sliced: true, sourceHeight: 1f, height: 1f);

        // "LOADING" just above the bar, letter-spaced like the art's "A GAME BY".
        var label = AddText(Rect(bar, "Label", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                                 new Vector2(0f, 22f), new Vector2(506f, 44f)),
                            GetOrCreateFont("OpenSans-ExtraBold"), "LOADING", 30f, LoadingLabelColor);
        label.characterSpacing = 30f;

        var so = new SerializedObject(ui);
        so.FindProperty("_progressFill").objectReferenceValue = fill;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(rootGo.scene);
        Debug.Log("[UIBuilder] Loading screen built.");
    }

    // Fills the screen keeping the art's 9:16 aspect, cropping the overflow (like a cover background).
    private static RectTransform FullScreenArt(RectTransform parent, string name, string sprite)
    {
        var rect = Rect(parent, name, Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
        AddImage(rect, Art(sprite));
        var fitter = Undo.AddComponent<AspectRatioFitter>(rect.gameObject);
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = BrandingAspect;
        return rect;
    }

    // ---------- Sections ----------

    // Level / step, goal, moves panel, and the sound switch (not in the pack: a small cream button
    // between the goal and the moves panel, with the Shift & Sort sound icons tinted ink).
    private static void BuildTitle(RectTransform root, SerializedObject so, TMP_FontAsset bold, TMP_FontAsset semi,
                                   System.Collections.Generic.List<RectTransform> intro)
    {
        var stage = Label(root, "Level Stage", 90, 58, 590, 58, semi, "Level 1 · Step 1 of 3", 40, Cream, TextAlignmentOptions.TopLeft);
        var goal = Label(root, "Goal", 90, 115, 590, 82, bold, "Pack their bags", 64, Cream, TextAlignmentOptions.TopLeft);
        var goalText = goal.GetComponent<TMP_Text>();
        goalText.enableAutoSizing = true;          // long goals shrink to fit beside the moves panel
        goalText.fontSizeMin = 44f;
        goalText.fontSizeMax = 64f;
        Set(so, "_levelStage", stage.GetComponent<TMP_Text>());
        Set(so, "_goal", goalText);

        var sound = Place(root, "Sound Button", 694, 97, 72, 72);
        var soundSurface = AddImage(sound, AssetBuilder.LoadTilewrap("UI/button_cream"), sliced: true);
        soundSurface.pixelsPerUnitMultiplier = 2f;  // 48 px corners would not fit a 72 px button
        var soundIcon = AddImage(Place(sound, "Icon", 16, 14, 40, 40), Art("ui/icons/icon_sound_on"));
        soundIcon.color = Ink;
        soundIcon.preserveAspect = true;
        Set(so, "_soundButton", MakeButton(sound, soundSurface));
        Set(so, "_soundIcon", soundIcon);
        Set(so, "_soundOnSprite", Art("ui/icons/icon_sound_on"));
        Set(so, "_soundOffSprite", Art("ui/icons/icon_sound_off"));

        var moves = Place(root, "Moves Panel", 780, 58, 245, 150);
        AddImage(moves, AssetBuilder.LoadTilewrap("UI/panel_moves"), sliced: true);
        var value = Label(moves, "Value", 23, 15, 199, 83, bold, "8", 78, Ink, TextAlignmentOptions.Top);
        var label = Label(moves, "Label", 15, 97, 215, 41, bold, "moves left", 34, Ink, TextAlignmentOptions.Top);
        Set(so, "_movesValue", value.GetComponent<TMP_Text>());
        Set(so, "_movesLabel", label.GetComponent<TMP_Text>());

        intro.Add(stage);
        intro.Add(goal);
        intro.Add(sound);
        intro.Add(moves);
    }

    // "CLUES" and two clue notes: item icon on the left, the clue (rich text) on the right.
    private static void BuildClues(RectTransform root, SerializedObject so, TMP_FontAsset bold, TMP_FontAsset semi,
                                   System.Collections.Generic.List<RectTransform> intro)
    {
        var heading = Label(root, "Clues Heading", 90, 204, 280, 62, semi, "CLUES", 40, Cream, TextAlignmentOptions.TopLeft);
        Set(so, "_cluesHeading", heading.gameObject);
        intro.Add(heading);

        float[] xs = { 48, 546 };
        var clues = so.FindProperty("_clues");
        clues.arraySize = xs.Length;
        for (int i = 0; i < xs.Length; i++)
        {
            var note = Place(root, $"Clue {i + 1}", xs[i], 248, 486, 202);
            AddImage(note, AssetBuilder.LoadTilewrap("UI/clue_note"), sliced: true);
            var icon = AddImage(Place(note, "Icon", 34, 63, 108, 108), AssetBuilder.LoadTilewrap("Items/bag_red"));
            icon.preserveAspect = true;
            var text = Label(note, "Text", 157, 52, 310, 125, bold, "Mia carries the\n<color=#ED3937>red</color> bag.", 40, Ink,
                             TextAlignmentOptions.MidlineLeft).GetComponent<TMP_Text>();
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;          // longer clues shrink to stay inside the note
            text.fontSizeMin = 28f;
            text.fontSizeMax = 40f;

            var slot = clues.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("Root").objectReferenceValue = note.gameObject;
            slot.FindPropertyRelative("Icon").objectReferenceValue = icon;
            slot.FindPropertyRelative("Text").objectReferenceValue = text;
            intro.Add(note);
        }
    }

    // Character Row (x 0..1080, y 446..790): the characters, the counter ledge in front of them, and the
    // name plates on the ledge. UI_InGame centres each character and plate over its board column.
    private static void BuildCharacters(RectTransform root, SerializedObject so, TMP_FontAsset bold,
                                        System.Collections.Generic.List<RectTransform> intro)
    {
        const float top = 446f;
        var row = Place(root, "Character Row", 0, top, 1080, 344);
        row.pivot = new Vector2(0.5f, 0f);                      // the intro grows it up from the ledge
        row.anchoredPosition = new Vector2(540f, -(top + 344f));

        var portraits = Place(row, "Characters", 0, 0, 1080, 344);
        AddImage(Place(row, "Counter", 20, 726 - top, 1040, 64), AssetBuilder.LoadTilewrap("UI/counter_ledge"), sliced: true);
        var plates = Place(row, "Name Plates", 0, 0, 1080, 344);

        float[] xs = { 162, 458, 754 };      // the pack's 3 characters; plates are 30 px inside each
        string[] names = { "mia", "leo", "zoe" };
        var characters = so.FindProperty("_characters");
        characters.arraySize = xs.Length;
        for (int i = 0; i < xs.Length; i++)
        {
            var portrait = AddImage(Place(portraits, $"Character {i + 1}", xs[i], 0, 244, 305),
                                    AssetBuilder.LoadTilewrap($"Characters/character_{names[i]}_neutral"));
            portrait.preserveAspect = true;

            var plate = Place(plates, $"Name Plate {i + 1}", xs[i] + 30, 712 - top, 184, 64);
            AddImage(plate, AssetBuilder.LoadTilewrap("UI/nameplate"), sliced: true);
            var name = Label(plate, "Name", 14, 4, 156, 48, bold, Capitalize(names[i]), 40, Ink, TextAlignmentOptions.Top);

            var slot = characters.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("Portrait").objectReferenceValue = portrait;
            slot.FindPropertyRelative("Plate").objectReferenceValue = plate;
            slot.FindPropertyRelative("Name").objectReferenceValue = name.GetComponent<TMP_Text>();
        }
        intro.Add(row);
    }

    // Undo, Notebook (placeholder) and the gold action button (Hint / Next / Retry).
    private static void BuildButtons(RectTransform root, SerializedObject so, TMP_FontAsset bold,
                                     System.Collections.Generic.List<RectTransform> intro)
    {
        var undo = PackButton(root, "Undo Button", 40, 300, "button_cream", "icon_undo", new Rect(30, 30, 80, 90),
                              new Rect(112, 39, 175, 73), "Undo", 48, bold, out _, out _);
        var notebook = PackButton(root, "Notebook Button", 358, 352, "button_cream", "icon_notebook", new Rect(30, 30, 76, 90),
                                  new Rect(112, 39, 227, 73), "Notebook", 44, bold, out _, out _);
        var action = PackButton(root, "Action Button", 733, 307, "button_gold", "icon_hint", new Rect(39, 30, 74, 90),
                                new Rect(125, 39, 182, 73), "Hint", 48, bold, out var actionIcon, out var actionLabel);

        Set(so, "_undoButton", undo);
        Set(so, "_notebookButton", notebook);
        Set(so, "_actionButton", action);
        Set(so, "_actionIcon", actionIcon);
        Set(so, "_actionLabel", actionLabel);
        Set(so, "_hintIcon", AssetBuilder.LoadTilewrap("UI/icon_hint"));
        Set(so, "_nextIcon", Art("ui/icons/icon_next"));
        Set(so, "_retryIcon", Art("ui/icons/icon_restart"));

        intro.Add((RectTransform)undo.transform);
        intro.Add((RectTransform)notebook.transform);
        intro.Add((RectTransform)action.transform);
    }

    // ---------- Pieces ----------

    // A pack button at y 1738, 150 high: sliced surface, icon and left-aligned label (boxes relative
    // to the button's top-left).
    private static UnityEngine.UI.Button PackButton(RectTransform root, string name, float x, float width, string surface,
                                                    string icon, Rect iconBox, Rect labelBox, string text, float size,
                                                    TMP_FontAsset font, out UnityEngine.UI.Image iconImage, out TMP_Text label)
    {
        var rect = Place(root, name, x, 1738, width, 150);
        var image = AddImage(rect, AssetBuilder.LoadTilewrap($"UI/{surface}"), sliced: true);
        iconImage = AddImage(Place(rect, "Icon", iconBox.x, iconBox.y, iconBox.width, iconBox.height), AssetBuilder.LoadTilewrap($"UI/{icon}"));
        iconImage.preserveAspect = true;
        label = Label(rect, "Label", labelBox.x, labelBox.y, labelBox.width, labelBox.height, font, text, size, Ink,
                      TextAlignmentOptions.MidlineLeft).GetComponent<TMP_Text>();
        return MakeButton(rect, image);
    }

    // Color-tint button as in the pack (pressed 0.91 / 0.88 / 0.84), no keyboard navigation.
    private static UnityEngine.UI.Button MakeButton(RectTransform rect, UnityEngine.UI.Image surface)
    {
        surface.raycastTarget = true;
        var button = Undo.AddComponent<UnityEngine.UI.Button>(rect.gameObject);
        button.targetGraphic = surface;
        button.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.99f, 0.96f);
        colors.pressedColor = new Color(0.91f, 0.88f, 0.84f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.65f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        var nav = button.navigation;
        nav.mode = UnityEngine.UI.Navigation.Mode.None;
        button.navigation = nav;
        return button;
    }

    // A rect at (x, y, w, h) in layout pixels from the parent's top-left; pivot at its centre so the
    // intro scales it in place.
    private static RectTransform Place(Transform parent, string name, float x, float y, float w, float h) =>
        Rect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), Center, new Vector2(x + w * 0.5f, -(y + h * 0.5f)), new Vector2(w, h));

    private static RectTransform Label(RectTransform parent, string name, float x, float y, float w, float h,
                                       TMP_FontAsset font, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var rect = Place(parent, name, x, y, w, h);
        var tmp = AddText(rect, font, text, size, color);
        tmp.alignment = alignment;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return rect;
    }

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text.Substring(1);

    // ---------- Helpers ----------

    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    private static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                      Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Build UI");
        go.layer = LayerMask.NameToLayer("UI");
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Height(RectTransform rect, float height)
    {
        var element = Undo.AddComponent<LayoutElement>(rect.gameObject);
        element.preferredHeight = height;
        element.minHeight = height;
    }

    // sliced: 9-slice. With sourceHeight / height the corners are rescaled to keep the art's proportions
    // at that height (the loading screen's art); without, the sprite's own PPU sets them (the pack's art,
    // imported at its logical size).
    private static Image AddImage(RectTransform rect, Sprite sprite, bool sliced = false, float sourceHeight = 0f, float height = 0f)
    {
        var image = Undo.AddComponent<Image>(rect.gameObject);
        image.sprite = sprite;
        image.raycastTarget = false;
        if (sliced)
        {
            image.type = Image.Type.Sliced;
            if (sourceHeight > 0f && height > 0f) image.pixelsPerUnitMultiplier = sourceHeight / height;
        }
        return image;
    }

    private static TextMeshProUGUI AddText(RectTransform rect, TMP_FontAsset font, string text, float size, Color color)
    {
        var tmp = Undo.AddComponent<TextMeshProUGUI>(rect.gameObject);
        tmp.font = font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property == null) Debug.LogError($"[UIBuilder] UI_InGame has no field '{field}'.");
        else property.objectReferenceValue = value;
    }

    private static Sprite Art(string relativePath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{relativePath}.png");
        if (sprite == null) Debug.LogError($"[UIBuilder] Missing sprite {ArtFolder}/{relativePath}.png");
        return sprite;
    }

    // ---------- Import ----------

    // 9-slice borders (left, bottom, right, top) in source pixels; everything else imports with none.
    private static Vector4 BorderFor(string path)
    {
        if (path.Contains("/buttons/button_sound_base")) return Vector4.zero;
        if (path.Contains("/buttons/")) return new Vector4(64f, 40f, 64f, 40f);
        if (path.Contains("/hud/pill_")) return new Vector4(44f, 43f, 44f, 43f);
        if (path.Contains("/panels/")) return new Vector4(52f, 52f, 52f, 52f);
        if (path.EndsWith("/branding/Progress Bar.png")) return new Vector4(12f, 12f, 12f, 12f);
        if (path.EndsWith("/branding/Tilewrap_Progress_Fill.png")) return new Vector4(6f, 6f, 6f, 6f);
        return Vector4.zero;
    }

    private static void ImportUiArt()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { $"{ArtFolder}/ui", $"{ArtFolder}/branding" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var border = BorderFor(path);

            bool changed = importer.textureType != TextureImporterType.Sprite
                           || importer.spriteImportMode != SpriteImportMode.Single
                           || importer.mipmapEnabled
                           || importer.spriteBorder != border;
            if (!changed) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }

    // Dynamic SDF font asset next to the TTF; glyphs are added as text needs them.
    public static TMP_FontAsset GetOrCreateFont(string fontName)
    {
        string path = $"{FontFolder}/{fontName} SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;

        var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontFolder}/{fontName}.ttf");
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
