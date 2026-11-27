using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the HUD under Canvas/Container/InGame/Reference (see UI_InGame) on the Tilewrap handoff's
// 1080x1920 layout (game_config.json ui.layoutTopLeft: every x, y, w, h below is in its top-left-origin
// pixels; the groups are hud, title, subtitle, clueHeading, clueCards, coach, progress, controlNote,
// actions, brand), with the handoff's UI sprites and fonts, and the loading screen.
// Re-runnable: the HUD is built only when InGame has no UI_InGame yet. To rebuild it, delete
// InGame/Reference and the UI_InGame component, then run again. The helpers are shared with PopUpBuilder.
public static class UIBuilder
{
    public static readonly Color Ink = new Color32(0x40, 0x28, 0x1F, 0xFF);
    public static readonly Color Cream = new Color32(0xFF, 0xF1, 0xD8, 0xFF);
    public static readonly Color Teal = new Color32(0x36, 0x87, 0x79, 0xFF);
    private static readonly Color PaleCream = new Color32(0xEC, 0xF7, 0xE9, 0xFF);
    private static readonly Color NoteColor = new Color32(0xE6, 0xF2, 0xE6, 0xFF);
    private static readonly Color BrandColor = new Color32(0xB4, 0xDD, 0xD4, 0xFF);
    private static readonly Color CoachColor = new Color32(0xFF, 0xF3, 0xD8, 0xFF);

    [MenuItem("ShiftSort/Build UI")]
    public static void Build()
    {
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

        var bold = HandoffBuilder.BoldFont();
        var regular = HandoffBuilder.RegularFont();

        // The 1080x1920 reference screen, fitted into the safe area like the camera fits the board.
        var root = Rect(inGameGo.transform, "Reference", Center, Center, Center, Vector2.zero, UI_ReferenceFit.ReferenceSize);
        Undo.AddComponent<UI_ReferenceFit>(root.gameObject);

        var ui = Undo.AddComponent<UI_InGame>(inGameGo);
        var so = new SerializedObject(ui);
        var intro = new System.Collections.Generic.List<RectTransform>();

        BuildTopBar(root, so, bold, regular, intro);
        BuildTitle(root, so, bold, regular, intro);
        BuildClues(root, so, bold, regular, intro);
        BuildCoachAndProgress(root, so, bold, regular, intro);
        BuildButtons(root, so, bold, intro);
        BuildToast(root, so, regular);

        var items = so.FindProperty("_introItems");
        items.arraySize = intro.Count;
        for (int i = 0; i < intro.Count; i++) items.GetArrayElementAtIndex(i).objectReferenceValue = intro[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(inGameGo.scene);
        Debug.Log("[UIBuilder] HUD built.");
    }

    // ---------- HUD sections ----------

    // hud (42, 28, 996, 112): the level pill, the moves panel, the menu button.
    private static void BuildTopBar(RectTransform root, SerializedObject so, TMP_FontAsset bold, TMP_FontAsset regular,
                                    System.Collections.Generic.List<RectTransform> intro)
    {
        var level = Place(root, "Level Button", 42, 40, 300, 88);
        var levelSurface = AddImage(level, Sprite("UI/capsule_white"), sliced: true);
        levelSurface.color = Cream;
        var levelText = Label(level, "Label", 0, 0, 300, 88, bold, "LEVEL 1 <size=70%>of 5</size>", 40, Ink, TextAlignmentOptions.Center);
        Set(so, "_levelButton", MakeButton(level, levelSurface));
        Set(so, "_levelText", levelText.GetComponent<TMP_Text>());

        var moves = Place(root, "Moves Panel", 700, 28, 220, 112);
        var movesImage = AddImage(moves, Sprite("UI/panel_moves"), sliced: true);
        var value = Label(moves, "Value", 0, 6, 220, 70, bold, "12", 84, Ink, TextAlignmentOptions.Center);
        var valueText = value.GetComponent<TMP_Text>();
        valueText.enableAutoSizing = true;
        valueText.fontSizeMin = 36f;
        valueText.fontSizeMax = 84f;
        var label = Label(moves, "Label", 0, 74, 220, 32, regular, "moves left", 28, Ink, TextAlignmentOptions.Center);
        Set(so, "_movesPanel", movesImage);
        Set(so, "_movesValue", valueText);
        Set(so, "_movesLabel", label.GetComponent<TMP_Text>());
        Set(so, "_movesSprite", Sprite("UI/panel_moves"));
        Set(so, "_practiceSprite", Sprite("UI/panel_practice"));

        var menu = Place(root, "Menu Button", 942, 40, 96, 88);
        var menuSurface = AddImage(menu, Sprite("UI/button_cream"), sliced: true);
        var menuIcon = AddImage(Place(menu, "Icon", 22, 18, 52, 52), Sprite("UI/icon_menu"));
        menuIcon.preserveAspect = true;
        Set(so, "_menuButton", MakeButton(menu, menuSurface));

        intro.Add(level);
        intro.Add(moves);
        intro.Add(menu);
    }

    // title (42, 153, 996, 84) and subtitle (42, 240, 996, 42).
    private static void BuildTitle(RectTransform root, SerializedObject so, TMP_FontAsset bold, TMP_FontAsset regular,
                                   System.Collections.Generic.List<RectTransform> intro)
    {
        var title = Label(root, "Title", 42, 153, 996, 84, bold, "Hats on!", 80, Cream, TextAlignmentOptions.Center);
        var titleText = title.GetComponent<TMP_Text>();
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 54f;
        titleText.fontSizeMax = 80f;
        var subtitle = Label(root, "Subtitle", 42, 240, 996, 42, regular, "Match all 9 items using the clues.", 36, PaleCream, TextAlignmentOptions.Center);
        Set(so, "_title", titleText);
        Set(so, "_subtitle", subtitle.GetComponent<TMP_Text>());
        intro.Add(title);
        intro.Add(subtitle);
    }

    // clueHeading (42, 306, 996, 38) and clueCards (42, 354, 996, 168): two cards, 486 wide, 24 apart.
    private static void BuildClues(RectTransform root, SerializedObject so, TMP_FontAsset bold, TMP_FontAsset regular,
                                   System.Collections.Generic.List<RectTransform> intro)
    {
        var heading = Label(root, "Clue Heading", 42, 302, 500, 46, bold, "CLUES · HATS", 34, Cream, TextAlignmentOptions.MidlineLeft);
        var progress = Label(root, "Clue Progress", 560, 302, 280, 46, regular, "0/3 matched", 30, Cream, TextAlignmentOptions.MidlineRight);
        var all = Place(root, "All Clues Button", 868, 298, 170, 54);
        var allSurface = AddImage(all, Sprite("UI/capsule_white"), sliced: true);
        allSurface.color = Cream;
        Label(all, "Label", 0, 0, 170, 54, bold, "All 9", 30, Ink, TextAlignmentOptions.Center);
        Set(so, "_clueHeading", heading.GetComponent<TMP_Text>());
        Set(so, "_clueProgress", progress.GetComponent<TMP_Text>());
        Set(so, "_allCluesButton", MakeButton(all, allSurface));
        Set(so, "_cluePanel", Sprite("UI/panel_clue"));
        Set(so, "_cluePanelMatched", Sprite("UI/panel_clue_matched"));
        intro.Add(heading);
        intro.Add(progress);
        intro.Add(all);

        float[] xs = { 42, 552 };
        var clues = so.FindProperty("_clues");
        clues.arraySize = xs.Length;
        for (int i = 0; i < xs.Length; i++)
        {
            var card = Place(root, $"Clue {i + 1}", xs[i], 362, 486, 160);
            var panel = AddImage(card, Sprite("UI/panel_clue"), sliced: true);
            var pin = AddImage(Place(card, "Pin", 231, -18, 24, 48), Sprite("UI/clue_pin"));
            pin.preserveAspect = true;
            var icon = AddImage(Place(card, "Icon", 22, 20, 120, 120), Sprite("Items/red_cap"));
            icon.preserveAspect = true;
            var text = Label(card, "Text", 152, 14, 306, 132, bold, "<b>Mia</b> wears the <color=#f36555>red</color> cap.", 40, Ink,
                             TextAlignmentOptions.MidlineLeft).GetComponent<TMP_Text>();
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;          // longer clues shrink to stay inside the card
            text.fontSizeMin = 28f;
            text.fontSizeMax = 42f;
            var check = AddImage(Place(card, "Check", 440, -12, 56, 56), Sprite("UI/check_badge"));

            var slot = clues.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("Root").objectReferenceValue = card.gameObject;
            slot.FindPropertyRelative("Panel").objectReferenceValue = panel;
            slot.FindPropertyRelative("Icon").objectReferenceValue = icon;
            slot.FindPropertyRelative("Text").objectReferenceValue = text;
            slot.FindPropertyRelative("Check").objectReferenceValue = check.gameObject;
            intro.Add(card);
        }
    }

    // coach (42, 1548, 996, 66), progress (210, 1622, 720, 48), controlNote (42, 1678, 996, 32).
    private static void BuildCoachAndProgress(RectTransform root, SerializedObject so, TMP_FontAsset bold, TMP_FontAsset regular,
                                              System.Collections.Generic.List<RectTransform> intro)
    {
        var coach = Place(root, "Coach", 72, 1548, 936, 66);
        var coachPanel = AddImage(coach, Sprite("UI/capsule_white"), sliced: true);
        coachPanel.color = CoachColor;
        var coachText = Label(coach, "Text", 36, 4, 864, 58, bold, "Swipe the hats right. Red wraps back to Mia.", 32, Ink,
                              TextAlignmentOptions.Center).GetComponent<TMP_Text>();
        coachText.enableAutoSizing = true;
        coachText.fontSizeMin = 22f;
        coachText.fontSizeMax = 32f;
        coachText.textWrappingMode = TextWrappingModes.Normal;
        Set(so, "_coachPanel", coachPanel);
        Set(so, "_coachText", coachText);

        var progress = Place(root, "Progress", 210, 1622, 720, 48);
        var progressText = Label(progress, "Text", 0, 0, 340, 48, regular, "<size=130%><b>6</b></size>/9 matched", 30, Cream,
                                 TextAlignmentOptions.MidlineRight);
        var track = Place(progress, "Track", 360, 17, 300, 14);
        var trackImage = AddImage(track, Sprite("UI/capsule_white"), sliced: true);
        trackImage.color = new Color32(0x12, 0x66, 0x6E, 0x88);
        trackImage.pixelsPerUnitMultiplier = 4f;    // 30 px corners would not fit a 14 px bar
        var fill = Rect(track, "Fill", Vector2.zero, new Vector2(0.6f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        var fillImage = AddImage(fill, Sprite("UI/capsule_white"), sliced: true);
        fillImage.color = new Color32(0xDD, 0xED, 0xAB, 0xFF);
        fillImage.pixelsPerUnitMultiplier = 4f;
        Set(so, "_progressText", progressText.GetComponent<TMP_Text>());
        Set(so, "_progressFill", fill);

        var note = Label(root, "Control Note", 42, 1678, 996, 32, regular, "Swipe rows or columns. Tiles wrap.", 30, NoteColor,
                         TextAlignmentOptions.Center);

        intro.Add(coach);
        intro.Add(progress);
        intro.Add(note);
    }

    // actions (42, 1732, 996, 144): Undo (1 fr), Notebook (1.22 fr, with the "9" badge), Hint (1 fr, gold), 20 px apart.
    private static void BuildButtons(RectTransform root, SerializedObject so, TMP_FontAsset bold,
                                     System.Collections.Generic.List<RectTransform> intro)
    {
        var undo = ActionButton(root, "Undo Button", 42, 297, "UI/button_cream", "UI/icon_undo", "Undo", bold);
        var notebook = ActionButton(root, "Notebook Button", 359, 362, "UI/button_cream", "UI/icon_notebook", "Notebook", bold);
        var hint = ActionButton(root, "Hint Button", 741, 297, "UI/button_gold", "UI/icon_hint", "Hint", bold);

        var badge = Place(notebook.transform, "Badge", 316, -16, 54, 54);
        var badgeImage = AddImage(badge, Sprite("UI/capsule_white"), sliced: true);
        badgeImage.color = new Color32(0xA2, 0x63, 0x83, 0xFF);
        Label(badge, "Count", 0, 0, 54, 54, bold, "9", 30, Color.white, TextAlignmentOptions.Center);

        Set(so, "_undoButton", undo);
        Set(so, "_notebookButton", notebook);
        Set(so, "_hintButton", hint);
        intro.Add((RectTransform)undo.transform);
        intro.Add((RectTransform)notebook.transform);
        intro.Add((RectTransform)hint.transform);
    }

    private static Button ActionButton(RectTransform root, string name, float x, float width, string surface, string icon,
                                       string text, TMP_FontAsset bold)
    {
        var rect = Place(root, name, x, 1744, width, 120);
        var image = AddImage(rect, Sprite(surface), sliced: true);
        var iconImage = AddImage(Place(rect, "Icon", 26, 28, 64, 64), Sprite(icon));
        iconImage.preserveAspect = true;
        var label = Label(rect, "Label", 98, 20, width - 112, 80, bold, text, 42, Ink, TextAlignmentOptions.MidlineLeft).GetComponent<TMP_Text>();
        label.enableAutoSizing = true;
        label.fontSizeMin = 30f;
        label.fontSizeMax = 42f;
        return MakeButton(rect, image);
    }

    // A dark capsule above the coach line, hidden until UI_Toast.Show. It lives in Container/Overlay
    // (a reference-fitted rect after PopUps), so it shows over an open popup too.
    private static void BuildToast(RectTransform root, SerializedObject so, TMP_FontAsset regular)
    {
        var container = root.parent.parent;      // InGame/Reference -> InGame -> Container
        var overlayTransform = container.Find("Overlay");
        RectTransform overlay;
        if (overlayTransform != null) overlay = (RectTransform)overlayTransform;
        else
        {
            overlay = Rect(container, "Overlay", Center, Center, Center, Vector2.zero, UI_ReferenceFit.ReferenceSize);
            Undo.AddComponent<UI_ReferenceFit>(overlay.gameObject);
        }
        overlay.SetAsLastSibling();
        var toast = Place(overlay, "Toast", 140, 1430, 800, 96);
        var image = AddImage(toast, Sprite("UI/capsule_white"), sliced: true);
        image.color = new Color32(0x20, 0x3C, 0x43, 0xF2);
        Undo.AddComponent<CanvasGroup>(toast.gameObject);
        var text = Label(toast, "Text", 32, 6, 736, 84, regular, "First, swipe the top row right.", 32, Color.white,
                         TextAlignmentOptions.Center).GetComponent<TMP_Text>();
        text.textWrappingMode = TextWrappingModes.Normal;
        var ui = Undo.AddComponent<UI_Toast>(toast.gameObject);
        var toastSo = new SerializedObject(ui);
        toastSo.FindProperty("_text").objectReferenceValue = text;
        toastSo.ApplyModifiedPropertiesWithoutUndo();
        Set(so, "_toast", ui);
    }

    // ---------- Loading screen ----------

    // Fills Canvas/Loading Screen (SceneBuilder's placeholder): outside Container, so it covers the
    // whole screen; the last sibling, so it draws over everything. The handoff's quiet loading screen:
    // the teal backdrop, "Tilewrap", a line, a progress bar and CHICHINGO. Skipped once it has UI_Loading.
    [MenuItem("ShiftSort/Build Loading Screen")]
    public static void BuildLoading()
    {
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

        var bold = HandoffBuilder.BoldFont();
        var regular = HandoffBuilder.RegularFont();
        var root = (RectTransform)rootGo.transform;
        Undo.RecordObject(root, "Build Loading Screen");
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsLastSibling();
        Undo.AddComponent<CanvasGroup>(root.gameObject);
        var ui = Undo.AddComponent<UI_Loading>(root.gameObject);

        // Full-screen art keeping its 9:16 aspect, cropping the overflow; blocks taps on the HUD.
        var art = Rect(root, "Art", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
        AddImage(art, Sprite("UI/background_teal")).raycastTarget = true;
        var fitter = Undo.AddComponent<AspectRatioFitter>(art.gameObject);
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 1080f / 1920f;

        // Content in a 1080x1920 reference rect, fitted into the screen.
        var content = Rect(root, "Content", Center, Center, Center, Vector2.zero, UI_ReferenceFit.ReferenceSize);
        Undo.AddComponent<UI_ReferenceFit>(content.gameObject);
        Label(content, "Title", 40, 760, 1000, 150, bold, "Tilewrap", 130, Cream, TextAlignmentOptions.Center);
        Label(content, "Line", 40, 920, 1000, 60, regular, "Getting the crew ready…", 40, PaleCream, TextAlignmentOptions.Center);

        var bar = Place(content, "Progress Bar", 287, 1040, 506, 24);
        var barImage = AddImage(bar, Sprite("UI/capsule_white"), sliced: true);
        barImage.color = new Color32(0x14, 0x5F, 0x66, 0xFF);
        barImage.pixelsPerUnitMultiplier = 2.5f;
        var fillArea = Rect(bar, "Fill Area", Vector2.zero, Vector2.one, Center, Vector2.zero, new Vector2(-8f, -8f));
        var fill = Rect(fillArea, "Fill", Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        var fillImage = AddImage(fill, Sprite("UI/capsule_white"), sliced: true);
        fillImage.color = new Color32(0xFF, 0xC8, 0x2E, 0xFF);
        fillImage.pixelsPerUnitMultiplier = 4f;

        var brand = Label(content, "Brand", 40, 1780, 1000, 50, bold, "CHICHINGO", 34, BrandColor, TextAlignmentOptions.Center);
        brand.GetComponent<TMP_Text>().characterSpacing = 30f;

        var so = new SerializedObject(ui);
        so.FindProperty("_progressFill").objectReferenceValue = fill;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(rootGo.scene);
        Debug.Log("[UIBuilder] Loading screen built.");
    }

    // ---------- Pieces (shared with PopUpBuilder) ----------

    // Color-tint button (pressed 0.91 / 0.88 / 0.84), no keyboard navigation.
    public static Button MakeButton(RectTransform rect, Image surface)
    {
        surface.raycastTarget = true;
        var button = Undo.AddComponent<Button>(rect.gameObject);
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.ColorTint;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.99f, 0.96f);
        colors.pressedColor = new Color(0.91f, 0.88f, 0.84f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.55f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        var nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
        return button;
    }

    // A rect at (x, y, w, h) in layout pixels from the parent's top-left; pivot at its centre so the
    // intro scales it in place.
    public static RectTransform Place(Transform parent, string name, float x, float y, float w, float h) =>
        Rect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), Center, new Vector2(x + w * 0.5f, -(y + h * 0.5f)), new Vector2(w, h));

    public static RectTransform Label(RectTransform parent, string name, float x, float y, float w, float h,
                                      TMP_FontAsset font, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var rect = Place(parent, name, x, y, w, h);
        var tmp = AddText(rect, font, text, size, color);
        tmp.alignment = alignment;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return rect;
    }

    public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
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

    // sliced: 9-slice with the sprite's own border (set by the handoff's import helper).
    public static Image AddImage(RectTransform rect, Sprite sprite, bool sliced = false)
    {
        var image = Undo.AddComponent<Image>(rect.gameObject);
        image.sprite = sprite;
        image.raycastTarget = false;
        if (sliced) image.type = Image.Type.Sliced;
        return image;
    }

    public static TextMeshProUGUI AddText(RectTransform rect, TMP_FontAsset font, string text, float size, Color color)
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

    public static Sprite Sprite(string relativePath) => HandoffBuilder.LoadSprite(relativePath);

    private static void Set(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property == null) Debug.LogError($"[UIBuilder] UI_InGame has no field '{field}'.");
        else property.objectReferenceValue = value;
    }
}
