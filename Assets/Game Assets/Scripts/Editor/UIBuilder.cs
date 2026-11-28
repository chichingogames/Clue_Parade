using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Builds the HUD under Canvas/Container/InGame/Reference (see UI_InGame) on the 1080x1920 reference
// layout, following Tilewrap_Any_Row_5_Case_Playtest: case pill, menu, coins, moves; title,
// progress and pips; the request line, order tabs and two clue cards; the subjects over the board's
// columns; the board note and the coach line; Undo / Notebook / Hint; the delivery banner. Every
// x, y, w, h below is in top-left-origin reference pixels. The pack's UI sprites and fonts; the
// loading screen too. The board itself is world space (SceneBuilder).
// Re-runnable: the HUD is built only when InGame has no UI_InGame yet. To rebuild it, delete
// InGame/Reference and the UI_InGame component, then run again. The helpers are shared with PopUpBuilder.
public static class UIBuilder
{
    public static readonly Color Ink = new Color32(0x40, 0x28, 0x1F, 0xFF);
    public static readonly Color Cream = new Color32(0xFF, 0xF1, 0xD8, 0xFF);
    public static readonly Color Teal = new Color32(0x36, 0x87, 0x79, 0xFF);
    public static readonly Color Gold = new Color32(0xFF, 0xC8, 0x2E, 0xFF);
    private static readonly Color PaleCream = new Color32(0xEC, 0xF7, 0xE9, 0xFF);
    private static readonly Color NoteColor = new Color32(0xE6, 0xF2, 0xE6, 0xFF);
    private static readonly Color BrandColor = new Color32(0xB4, 0xDD, 0xD4, 0xFF);
    private static readonly Color WalletColor = new Color32(0x11, 0x65, 0x65, 0xFF);

    // The board is centred: x 105..975, y 800..1670 (ReferenceLayout.BoardCenter); subject cards 280
    // wide over its 290 px columns, art inset [17, 5, 246, 200].
    private const float BoardLeft = 105f;
    private const float Cell = 290f;
    private const int MaxSets = 5;

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
        var intro = new List<RectTransform>();

        BuildTopBar(root, so, bold, regular, intro);
        BuildTitle(root, so, bold, regular, intro);
        BuildRequest(root, so, bold, regular, intro);
        BuildSubjects(root, so, bold, intro);
        BuildUnderBoard(root, so, bold, regular, intro);
        BuildButtons(root, so, bold, intro);
        BuildBanner(root, so, bold);
        BuildToast(root, so, regular);
        Set(so, "_flightLayer", root);

        var items = so.FindProperty("_introItems");
        items.arraySize = intro.Count;
        for (int i = 0; i < intro.Count; i++) items.GetArrayElementAtIndex(i).objectReferenceValue = intro[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(inGameGo.scene);
        Debug.Log("[UIBuilder] HUD built.");
    }

    // ---------- HUD sections ----------

    // case pill (40, 30, 280, 85), menu (340, 45, 64, 64), coins (470, 35, 210, 80), moves (760, 25, 280, 107).
    private static void BuildTopBar(RectTransform root, SerializedObject so, TMP_FontAsset bold, TMP_FontAsset regular,
                                    List<RectTransform> intro)
    {
        var level = Place(root, "Level Button", 40, 30, 280, 85);
        var levelSurface = AddImage(level, Sprite("UI/capsule_white"), sliced: true);
        levelSurface.color = Cream;
        var levelText = Label(level, "Label", 0, 0, 280, 85, bold, "Case 1 / 5", 40, Ink, TextAlignmentOptions.Center);
        Set(so, "_levelButton", MakeButton(level, levelSurface));
        Set(so, "_levelText", levelText.GetComponent<TMP_Text>());

        var menu = Place(root, "Menu Button", 340, 45, 64, 64);
        var menuSurface = AddImage(menu, Sprite("UI/capsule_white"), sliced: true);
        menuSurface.color = new Color(1f, 1f, 1f, 0f);       // the icon alone; the rect still catches taps
        var menuIcon = AddImage(Place(menu, "Icon", 4, 4, 56, 56), Sprite("UI/icon_menu"));
        menuIcon.preserveAspect = true;
        Set(so, "_menuButton", MakeButton(menu, menuSurface));

        var wallet = Place(root, "Wallet", 470, 35, 210, 80);
        AddImage(wallet, Sprite("UI/capsule_white"), sliced: true).color = WalletColor;
        AddImage(Place(wallet, "Coin", 26, 13, 50, 54), Sprite("UI/icon_coin")).preserveAspect = true;
        var coins = Label(wallet, "Coins", 84, 0, 110, 80, bold, "0", 44, Cream, TextAlignmentOptions.MidlineLeft);
        Set(so, "_coinsText", coins.GetComponent<TMP_Text>());

        var moves = Place(root, "Moves Panel", 760, 25, 280, 107);
        var movesImage = AddImage(moves, Sprite("UI/panel_moves"), sliced: true);
        var value = Label(moves, "Value", 0, 6, 280, 62, bold, "12", 50, Ink, TextAlignmentOptions.Center);
        var valueText = value.GetComponent<TMP_Text>();
        valueText.enableAutoSizing = true;
        valueText.fontSizeMin = 28f;
        valueText.fontSizeMax = 50f;
        var label = Label(moves, "Label", 0, 66, 280, 34, regular, "moves left", 25, Ink, TextAlignmentOptions.Center);
        Set(so, "_movesPanel", movesImage);
        Set(so, "_movesValue", valueText);
        Set(so, "_movesLabel", label.GetComponent<TMP_Text>());
        Set(so, "_movesSprite", Sprite("UI/panel_moves"));
        Set(so, "_practiceSprite", Sprite("UI/panel_practice"));

        intro.Add(level);
        intro.Add(menu);
        intro.Add(wallet);
        intro.Add(moves);
    }

    // title (40, 150, 1000, 74); progress (40, 232, 600, 48) with a pip per set on the right (69 x 17, 12 apart).
    private static void BuildTitle(RectTransform root, SerializedObject so, TMP_FontAsset bold, TMP_FontAsset regular,
                                   List<RectTransform> intro)
    {
        var title = Label(root, "Title", 40, 150, 1000, 74, bold, "Parade prep", 60, Cream, TextAlignmentOptions.Center);
        var titleText = title.GetComponent<TMP_Text>();
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 40f;
        titleText.fontSizeMax = 60f;
        var progress = Label(root, "Progress", 40, 232, 600, 48, regular, "0 of 3 sets delivered", 32, PaleCream, TextAlignmentOptions.MidlineLeft);
        Set(so, "_title", titleText);
        Set(so, "_progressText", progress.GetComponent<TMP_Text>());

        var pipsRect = Place(root, "Pips", 1040 - (MaxSets * 69 + (MaxSets - 1) * 12), 247, MaxSets * 69 + (MaxSets - 1) * 12, 17);
        var pips = so.FindProperty("_pips");
        pips.arraySize = MaxSets;
        for (int i = 0; i < MaxSets; i++)
        {
            var pip = AddImage(Place(pipsRect, $"Pip {i + 1}", i * 81, 0, 69, 17), Sprite("UI/capsule_white"), sliced: true);
            pip.pixelsPerUnitMultiplier = 6f;
            pips.GetArrayElementAtIndex(i).objectReferenceValue = pip;
        }
        intro.Add(title);
        intro.Add(progress);
        intro.Add(pipsRect);
    }

    // request (40, 290, 560, 46) and "SET 1 / 3" (640, 290, 400, 46), or two order tabs
    // (600 / 825, 284, 215, 58); two clue cards (40 / 550, 345, 490, 150): icon [14, 20, 100, 110],
    // text [126, 12, 350, 126], the "SCENE CLUE" tag bottom right.
    private static void BuildRequest(RectTransform root, SerializedObject so, TMP_FontAsset bold, TMP_FontAsset regular,
                                     List<RectTransform> intro)
    {
        var request = Label(root, "Request", 40, 290, 560, 46, bold, "CLUES · HATS", 36, Cream, TextAlignmentOptions.MidlineLeft);
        var requestText = request.GetComponent<TMP_Text>();
        requestText.enableAutoSizing = true;
        requestText.fontSizeMin = 26f;
        requestText.fontSizeMax = 36f;
        var position = Label(root, "Set Position", 640, 290, 400, 46, regular, "SET 1 / 3", 26, Cream, TextAlignmentOptions.MidlineRight);
        position.GetComponent<TMP_Text>().characterSpacing = 6f;
        Set(so, "_requestText", requestText);
        Set(so, "_setPosition", position.GetComponent<TMP_Text>());
        intro.Add(request);
        intro.Add(position);

        var tabs = so.FindProperty("_orderTabs");
        tabs.arraySize = 2;
        for (int i = 0; i < 2; i++)
        {
            var tab = Place(root, $"Order Tab {i + 1}", 600 + i * 225, 284, 215, 58);
            var surface = AddImage(tab, Sprite("UI/capsule_white"), sliced: true);
            var text = Label(tab, "Label", 10, 0, 195, 58, bold, "Mains", 30, Cream, TextAlignmentOptions.Center).GetComponent<TMP_Text>();
            text.enableAutoSizing = true;
            text.fontSizeMin = 20f;
            text.fontSizeMax = 30f;
            tabs.GetArrayElementAtIndex(i).objectReferenceValue = MakeButton(tab, surface);
            tab.gameObject.SetActive(false);
        }

        float[] xs = { 40, 550 };
        var clues = so.FindProperty("_clues");
        clues.arraySize = xs.Length;
        for (int i = 0; i < xs.Length; i++)
        {
            var card = Place(root, $"Clue {i + 1}", xs[i], 345, 490, 150);
            var panel = AddImage(card, Sprite("UI/panel_clue"), sliced: true);
            var icon = AddImage(Place(card, "Icon", 14, 20, 100, 110), Sprite("Items/crew_00"));
            icon.preserveAspect = true;
            var text = Label(card, "Text", 126, 12, 350, 126, bold, "Mia gets the red cap.", 32, Ink,
                             TextAlignmentOptions.MidlineLeft).GetComponent<TMP_Text>();
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true;          // longer facts shrink to stay inside the card
            text.fontSizeMin = 22f;
            text.fontSizeMax = 32f;
            var tag = Label(card, "Scene Tag", 280, 120, 196, 26, bold, "SCENE CLUE", 18, Teal, TextAlignmentOptions.MidlineRight);
            tag.GetComponent<TMP_Text>().characterSpacing = 4f;

            var slot = clues.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("Button").objectReferenceValue = MakeButton(card, panel);
            slot.FindPropertyRelative("Icon").objectReferenceValue = icon;
            slot.FindPropertyRelative("Text").objectReferenceValue = text;
            slot.FindPropertyRelative("SceneTag").objectReferenceValue = tag.gameObject;
            intro.Add(card);
        }
    }

    // subjects (105, 515, 870, 267): one white card per column (280 wide, centred on its 290 px
    // column) holding the subject's rig in the art inset, and its name tab under it.
    private static void BuildSubjects(RectTransform root, SerializedObject so, TMP_FontAsset bold, List<RectTransform> intro)
    {
        var subjects = so.FindProperty("_subjects");
        subjects.arraySize = 3;
        for (int c = 0; c < 3; c++)
        {
            float x = BoardLeft + c * Cell + 5f;
            var subject = Place(root, $"Subject {c + 1}", x, 515, 280, 267);
            var card = SubjectCard(subject, bold, 280, 213, 54, new[] { 17f, 5f, 246f, 200f }, Color.white, 32);
            subjects.GetArrayElementAtIndex(c).objectReferenceValue = card;
            intro.Add(subject);
        }
    }

    // A UI_SubjectCard on `parent`: the card (capsule_white in `surface`, the pop scales it) with the
    // rig's frame at `inset` (x, y, w, h inside the card), and a name tab of tabHeight under it.
    public static UI_SubjectCard SubjectCard(RectTransform parent, TMP_FontAsset bold, float width, float cardHeight,
                                             float tabHeight, float[] inset, Color surface, float nameSize)
    {
        var body = Place(parent, "Card", 0, 0, width, cardHeight);
        var image = AddImage(body, Sprite("UI/capsule_white"), sliced: true);
        image.color = surface;
        image.pixelsPerUnitMultiplier = 1.4f;
        var frame = Place(body, "Frame", inset[0], inset[1], inset[2], inset[3]);

        var tab = Place(parent, "Name Tab", 0, cardHeight, width, tabHeight);
        AddImage(tab, Sprite("UI/tab_name"), sliced: true);
        var name = Label(tab, "Name", 10, 0, width - 20, tabHeight, bold, "Mia", nameSize, Ink, TextAlignmentOptions.Center)
                       .GetComponent<TMP_Text>();
        name.enableAutoSizing = true;              // nameMin 22 .. nameMax 32: long names shrink, never clip
        name.fontSizeMin = 22f;
        name.fontSizeMax = nameSize;

        var card = Undo.AddComponent<UI_SubjectCard>(parent.gameObject);
        var cardSo = new SerializedObject(card);
        cardSo.FindProperty("_frame").objectReferenceValue = frame;
        cardSo.FindProperty("_name").objectReferenceValue = name;
        cardSo.FindProperty("_body").objectReferenceValue = body;
        cardSo.ApplyModifiedPropertiesWithoutUndo();
        return card;
    }

    // board note (40, 1680, 1000, 40) and the coach line (40, 1722, 1000, 62).
    private static void BuildUnderBoard(RectTransform root, SerializedObject so, TMP_FontAsset bold, TMP_FontAsset regular,
                                        List<RectTransform> intro)
    {
        var note = Label(root, "Board Note", 40, 1680, 1000, 40, regular,
                         "Each item under its owner. <color=#FFE083><b>Any row clears.</b></color>", 28, NoteColor, TextAlignmentOptions.Center);
        var coach = Label(root, "Coach", 40, 1722, 1000, 62, regular, "Line up the requested items under their owners in any row.", 30,
                          Cream, TextAlignmentOptions.Center);
        var coachText = coach.GetComponent<TMP_Text>();
        coachText.textWrappingMode = TextWrappingModes.Normal;
        coachText.enableAutoSizing = true;
        coachText.fontSizeMin = 22f;
        coachText.fontSizeMax = 30f;
        Set(so, "_coachText", coachText);
        Set(so, "_coach", coach);
        intro.Add(note);
        intro.Add(coach);
    }

    // actions (40, 1790, 1000, 110): Undo, Notebook (wider), Hint (gold), 20 px apart.
    private static void BuildButtons(RectTransform root, SerializedObject so, TMP_FontAsset bold, List<RectTransform> intro)
    {
        var undo = ActionButton(root, "Undo Button", 40, 300, "UI/button_cream", "UI/icon_undo", "Undo", bold);
        var notebook = ActionButton(root, "Notebook Button", 360, 360, "UI/button_cream", "UI/icon_notebook", "Notebook", bold);
        var hint = ActionButton(root, "Hint Button", 740, 300, "UI/button_gold", "UI/icon_hint", "Hint", bold);
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
        var rect = Place(root, name, x, 1790, width, 110);
        var image = AddImage(rect, Sprite(surface), sliced: true);
        var iconImage = AddImage(Place(rect, "Icon", 28, 27, 56, 56), Sprite(icon));
        iconImage.preserveAspect = true;
        var label = Label(rect, "Label", 92, 15, width - 106, 80, bold, text, 39, Ink, TextAlignmentOptions.MidlineLeft).GetComponent<TMP_Text>();
        label.enableAutoSizing = true;
        label.fontSizeMin = 28f;
        label.fontSizeMax = 39f;
        return MakeButton(rect, image);
    }

    // "Hats delivered!" (140, row centre - 50, 800, 100), hidden until a delivery; UI_InGame moves it
    // to the delivered row.
    private static void BuildBanner(RectTransform root, SerializedObject so, TMP_FontAsset bold)
    {
        var banner = Place(root, "Delivery Banner", 140, 895, 800, 100);
        var surface = AddImage(banner, Sprite("UI/panel_clue"), sliced: true);
        surface.color = new Color32(0xFF, 0xF3, 0xD9, 0xF5);
        AddImage(Place(banner, "Border", -4, -4, 808, 108), Sprite("UI/panel_clue_matched"), sliced: true).color = Gold;
        banner.Find("Border").SetAsFirstSibling();
        var text = Label(banner, "Text", 20, 0, 760, 100, bold, "Hats delivered!", 43, Ink, TextAlignmentOptions.Center);
        Set(so, "_banner", banner);
        Set(so, "_bannerText", text.GetComponent<TMP_Text>());
        banner.gameObject.SetActive(false);
    }

    // A dark capsule over the board's lower half, hidden until UI_Toast.Show. It lives in
    // Container/Overlay (a reference-fitted rect after PopUps), so it shows over an open popup too.
    // An existing Overlay/Toast is reused.
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
        var existing = overlay.Find("Toast");
        if (existing != null && existing.TryGetComponent<UI_Toast>(out var current))
        {
            Set(so, "_toast", current);
            return;
        }
        var toast = Place(overlay, "Toast", 110, 1470, 860, 120);
        var image = AddImage(toast, Sprite("UI/capsule_white"), sliced: true);
        image.color = new Color32(0x20, 0x3C, 0x43, 0xF2);
        Undo.AddComponent<CanvasGroup>(toast.gameObject);
        var text = Label(toast, "Text", 36, 8, 788, 104, regular, "First, swipe the top row right.", 32, Color.white,
                         TextAlignmentOptions.Center).GetComponent<TMP_Text>();
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = 24f;
        text.fontSizeMax = 32f;
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
