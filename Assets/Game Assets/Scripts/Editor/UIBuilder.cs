using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Builds the HUD under Canvas/Container/InGame (see UI_InGame) from the art in Sprites/Sprites/ui
// and branding, and the TMP font assets from Fonts/OpenSans-*.ttf.
// Re-runnable: sprite import settings are (re)applied, font assets are created only if missing, and
// the HUD is built only when InGame has no UI_InGame yet. To rebuild it, delete InGame's
// "Top Bar", "Board Area/Pills" and "Board Area/Bottom Stack" and the UI_InGame component, then run again.
public static class UIBuilder
{
    private const string ArtFolder = "Assets/Game Assets/Sprites/Sprites";
    private const string FontFolder = "Assets/Game Assets/Fonts";

    private static readonly Color Navy = new Color32(0x2B, 0x21, 0x44, 0xFF);
    private static readonly Color BodyColor = new Color32(0x5B, 0x53, 0x70, 0xFF);
    private static readonly Color MatchedColor = new Color32(0x3E, 0x6B, 0x4B, 0xFF);
    private static readonly Color MovesColor = new Color32(0x6E, 0x5B, 0x4A, 0xFF);

    // Sizes in canvas units (reference 1080x1920).
    private const float PillHeight = 64f;
    private const float ButtonHeight = 120f;
    private const float PanelHeight = 176f;
    private const float StackSpacing = 22f;
    private const float BottomStackMargin = 40f;         // Bottom Stack's gap above the safe area's bottom edge

    [MenuItem("ShiftSort/Build UI")]
    public static void Build()
    {
        ImportUiArt();
        var heading = GetOrCreateFont("OpenSans-ExtraBold");
        var body = GetOrCreateFont("OpenSans-Bold");

        var inGameGo = GameObject.Find("/Canvas/Container/InGame");
        var boardAreaGo = GameObject.Find("/Canvas/Container/InGame/Board Area");
        if (inGameGo == null || boardAreaGo == null)
        {
            Debug.LogError("[UIBuilder] Canvas/Container/InGame/Board Area is missing. Run ShiftSort > Build Scene first.");
            return;
        }

        if (inGameGo.GetComponent<UI_InGame>() != null)
        {
            Debug.Log("[UIBuilder] InGame already has UI_InGame; HUD left as is.");
            return;
        }

        var inGame = (RectTransform)inGameGo.transform;
        var boardArea = (RectTransform)boardAreaGo.transform;
        var ui = Undo.AddComponent<UI_InGame>(inGameGo);
        var so = new SerializedObject(ui);

        BuildTopBar(inGame, so, heading);
        BuildPills(boardArea, so, body);
        BuildBottomStack(boardArea, so, heading, body);

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

    private static void BuildTopBar(RectTransform inGame, SerializedObject so, TMP_FontAsset heading)
    {
        var bar = Rect(inGame, "Top Bar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 340f));

        var logo = AddImage(Rect(bar, "Logo", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                 new Vector2(0f, -28f), new Vector2(600f, 165f)), Art("branding/logo_shift_and_sort"));
        logo.preserveAspect = true;

        var level = AddText(Rect(bar, "Level", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                                 new Vector2(0f, -205f), new Vector2(0f, 100f)), heading, "LEVEL 01", 70f, Navy);
        level.characterSpacing = 8f;
        Set(so, "_levelText", level);

        var soundRect = Rect(bar, "Sound Button", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                             new Vector2(-96f, -255f), new Vector2(116f, 116f));
        var soundBase = AddImage(soundRect, Art("ui/buttons/button_sound_base"));
        soundBase.raycastTarget = true;
        var sound = Undo.AddComponent<Button>(soundRect.gameObject);
        sound.targetGraphic = soundBase;
        var icon = AddImage(Rect(soundRect, "Icon", Center, Center, Center, new Vector2(0f, 4f), new Vector2(56f, 56f)), Art("ui/icons/icon_sound_on"));
        icon.color = Navy;

        Set(so, "_soundButton", sound);
        Set(so, "_soundIcon", icon);
        Set(so, "_soundOnSprite", Art("ui/icons/icon_sound_on"));
        Set(so, "_soundOffSprite", Art("ui/icons/icon_sound_off"));
    }

    // Matched (left) and moves (right), just above the column targets.
    private static void BuildPills(RectTransform boardArea, SerializedObject so, TMP_FontAsset body)
    {
        var pills = Rect(boardArea, "Pills", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f),
                         new Vector2(0f, 14f), new Vector2(0f, PillHeight));

        var matched = Pill(pills, "Matched Pill", new Vector2(0f, 0.5f), new Vector2(0f, 0f), 270f, "ui/hud/pill_matches", body, "0 / 9 matched", MatchedColor);
        var moves = Pill(pills, "Moves Pill", new Vector2(1f, 0.5f), new Vector2(0f, 0f), 290f, "ui/hud/pill_moves", body, "Unlimited moves", MovesColor);

        Set(so, "_matchedText", matched.GetComponentInChildren<TMP_Text>());
        Set(so, "_movesPill", moves.GetComponent<Image>());
        Set(so, "_movesText", moves.GetComponentInChildren<TMP_Text>());
        Set(so, "_movesSprite", Art("ui/hud/pill_moves"));
        Set(so, "_movesLowSprite", Art("ui/hud/pill_moves_low"));
    }

    // Bottom Stack: message panel, Next level (only shown after a win), then Undo + Restart. A child of
    // InGame pinned to the bottom of the safe area; UI_BoardArea (horizontal only) gives it the board's
    // width. It grows upward when Next level appears.
    private static void BuildBottomStack(RectTransform boardArea, SerializedObject so, TMP_FontAsset heading, TMP_FontAsset body)
    {
        var stack = Rect(boardArea.parent, "Bottom Stack", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                         new Vector2(0f, BottomStackMargin), new Vector2(0f, 0f));
        var layout = Undo.AddComponent<VerticalLayoutGroup>(stack.gameObject);
        layout.spacing = StackSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = Undo.AddComponent<ContentSizeFitter>(stack.gameObject);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var follow = new SerializedObject(Undo.AddComponent<UI_BoardArea>(stack.gameObject));
        follow.FindProperty("_horizontalOnly").boolValue = true;
        follow.ApplyModifiedPropertiesWithoutUndo();

        // Message panel
        var panelRect = Rect(stack, "Message Panel", Center, Center, Center, Vector2.zero, Vector2.zero);
        Height(panelRect, PanelHeight);
        var panel = AddImage(panelRect, Art("ui/panels/panel_neutral"), sliced: true, sourceHeight: 240f, height: PanelHeight * 1.2f);
        var title = AddText(Rect(panelRect, "Title", new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f),
                                 new Vector2(0f, -8f), new Vector2(-48f, -24f)), heading, "Match shapes across and colors down", 44f, Navy);
        title.enableAutoSizing = true;
        title.fontSizeMin = 28f;
        title.fontSizeMax = 44f;
        var bodyText = AddText(Rect(panelRect, "Body", new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(0f, 16f), new Vector2(-48f, -28f)), body, "Swipe rows or columns to line them up.", 28f, BodyColor);
        bodyText.enableAutoSizing = true;
        bodyText.fontSizeMin = 20f;
        bodyText.fontSizeMax = 28f;
        bodyText.verticalAlignment = VerticalAlignmentOptions.Top;

        Set(so, "_messagePanel", panel);
        Set(so, "_messageTitle", title);
        Set(so, "_messageBody", bodyText);
        Set(so, "_panelNeutralSprite", Art("ui/panels/panel_neutral"));
        Set(so, "_panelWinSprite", Art("ui/panels/panel_win"));
        Set(so, "_panelFailSprite", Art("ui/panels/panel_fail"));

        // Next level (primary)
        var next = StackButton(stack, "Next Button", primary: true, heading, "Next level", "ui/icons/icon_next", iconFirst: false, Navy);
        next.gameObject.SetActive(false);
        Set(so, "_nextButton", next);

        // Undo + Restart row
        var rowRect = Rect(stack, "Button Row", Center, Center, Center, Vector2.zero, Vector2.zero);
        Height(rowRect, ButtonHeight);
        var row = Undo.AddComponent<HorizontalLayoutGroup>(rowRect.gameObject);
        row.spacing = 24f;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = true;
        row.childForceExpandHeight = true;

        var undo = StackButton(rowRect, "Undo Button", primary: false, heading, "Undo", "ui/icons/icon_undo", iconFirst: true, Color.white);
        var restart = StackButton(rowRect, "Restart Button", primary: false, heading, "Restart", "ui/icons/icon_restart", iconFirst: true, Color.white);
        Set(so, "_undoButton", undo);
        Set(so, "_restartButton", restart);

        // Launch intro order: top to bottom.
        var intro = so.FindProperty("_introItems");
        RectTransform[] order =
        {
            (RectTransform)boardArea.parent.Find("Top Bar/Logo"),
            (RectTransform)boardArea.parent.Find("Top Bar/Level"),
            (RectTransform)boardArea.parent.Find("Top Bar/Sound Button"),
            (RectTransform)boardArea.Find("Pills/Matched Pill"),
            (RectTransform)boardArea.Find("Pills/Moves Pill"),
            panelRect,
            (RectTransform)undo.transform,
            (RectTransform)restart.transform,
        };
        intro.arraySize = order.Length;
        for (int i = 0; i < order.Length; i++) intro.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
    }

    // ---------- Pieces ----------

    private static RectTransform Pill(RectTransform parent, string name, Vector2 anchor, Vector2 position, float width,
                                      string sprite, TMP_FontAsset font, string text, Color color)
    {
        var rect = Rect(parent, name, anchor, anchor, anchor, position, new Vector2(width, PillHeight));
        AddImage(rect, Art(sprite), sliced: true, sourceHeight: 88f, height: PillHeight);
        AddText(Rect(rect, "Text", Vector2.zero, Vector2.one, Center, Vector2.zero, new Vector2(-24f, 0f)), font, text, 26f, color);
        return rect;
    }

    private static Button StackButton(RectTransform parent, string name, bool primary, TMP_FontAsset font,
                                      string label, string iconSprite, bool iconFirst, Color contentColor)
    {
        var rect = Rect(parent, name, Center, Center, Center, Vector2.zero, Vector2.zero);
        Height(rect, ButtonHeight);

        string kind = primary ? "primary" : "secondary";
        var image = AddImage(rect, Art($"ui/buttons/button_{kind}_normal"), sliced: true, sourceHeight: 136f, height: ButtonHeight);
        image.raycastTarget = true;

        var button = Undo.AddComponent<Button>(rect.gameObject);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            pressedSprite = Art($"ui/buttons/button_{kind}_pressed"),
            highlightedSprite = Art($"ui/buttons/button_{kind}_normal"),
            selectedSprite = Art($"ui/buttons/button_{kind}_normal"),
            disabledSprite = Art("ui/buttons/button_disabled"),
        };
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;

        // Icon + label, centered together (lifted a little: the art has a shadow lip at the bottom).
        var content = Rect(rect, "Content", Vector2.zero, Vector2.one, Center, new Vector2(0f, 5f), Vector2.zero);
        var h = Undo.AddComponent<HorizontalLayoutGroup>(content.gameObject);
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        if (iconFirst) ContentIcon(content, iconSprite, contentColor);
        var text = AddText(Rect(content, "Label", Center, Center, Center, Vector2.zero, Vector2.zero), font, label, 36f, contentColor);
        var fit = Undo.AddComponent<LayoutElement>(text.gameObject);
        fit.preferredHeight = 60f;
        if (!iconFirst) ContentIcon(content, iconSprite, contentColor);

        return button;
    }

    private static Image ContentIcon(RectTransform parent, string sprite, Color color)
    {
        var icon = AddImage(Rect(parent, "Icon", Center, Center, Center, Vector2.zero, Vector2.zero), Art(sprite));
        icon.color = color;
        icon.preserveAspect = true;
        var element = Undo.AddComponent<LayoutElement>(icon.gameObject);
        element.preferredWidth = 40f;
        element.preferredHeight = 40f;
        return icon;
    }

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

    // sliced: 9-slice with the corner size scaled so the art's corners keep their proportions at this height.
    private static Image AddImage(RectTransform rect, Sprite sprite, bool sliced = false, float sourceHeight = 0f, float height = 0f)
    {
        var image = Undo.AddComponent<Image>(rect.gameObject);
        image.sprite = sprite;
        image.raycastTarget = false;
        if (sliced)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = sourceHeight / height;
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
    private static TMP_FontAsset GetOrCreateFont(string fontName)
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
