using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds every popup under Canvas/Container/PopUps (one PopUp_X per PopupName), on the handoff's
// panel_modal with its buttons and fonts. PopUps gets UI_ReferenceFit, so popups are laid out in the
// same 1080x1920 reference pixels as the HUD. Each popup: a full-screen Dim (blocks taps, fades) and a
// Holder (the panel, scales) whose VerticalLayoutGroup + ContentSizeFitter stack kicker, title, body,
// the popup's own widgets and its buttons; long content scrolls inside a fixed-height ScrollRect.
// The case map (PopupName.LevelSelect) is full screen instead. PopupName.Lesson and Confirm are retired (no popup).
// Re-runnable: a popup whose object (PopUp_{Name}) exists is left as is; delete it to rebuild.
public static class PopUpBuilder
{
    private const float PanelWidth = 940f;
    private static readonly Color DimColor = new Color32(0x07, 0x3F, 0x4B, 0xC9);
    private static readonly Color MutedText = new Color32(0x7C, 0x72, 0x5C, 0xFF);

    private static TMP_FontAsset _bold;
    private static TMP_FontAsset _regular;

    private enum ButtonStyle { Primary, Secondary, Text }

    [MenuItem("ShiftSort/Build PopUps")]
    public static void Build()
    {
        var parentGo = GameObject.Find("/Canvas/Container/PopUps");
        if (parentGo == null)
        {
            Debug.LogError("[PopUpBuilder] Canvas/Container/PopUps is missing. Run ShiftSort > Build Scene first.");
            return;
        }
        if (parentGo.GetComponent<UI_ReferenceFit>() == null) Undo.AddComponent<UI_ReferenceFit>(parentGo);

        _bold = HandoffBuilder.BoldFont();
        _regular = HandoffBuilder.RegularFont();
        var parent = parentGo.transform;

        BuildIfMissing(parent, PopupName.Win, BuildWin);
        BuildIfMissing(parent, PopupName.Failed, BuildFailed);
        BuildIfMissing(parent, PopupName.Hint, BuildHint);
        BuildIfMissing(parent, PopupName.Notebook, BuildNotebook);
        BuildIfMissing(parent, PopupName.LevelSelect, BuildMap);
        BuildIfMissing(parent, PopupName.Menu, BuildMenu);
        BuildIfMissing(parent, PopupName.Feedback, BuildFeedback);

        var manager = Object.FindFirstObjectByType<Manager_PopUp>();
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            if (so.FindProperty("_popUpParent").objectReferenceValue == null)
            {
                so.FindProperty("_popUpParent").objectReferenceValue = parent;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        EditorSceneManager.MarkSceneDirty(parentGo.scene);
        Debug.Log("[PopUpBuilder] Popups are up to date.");
    }

    private static void BuildIfMissing(Transform parent, PopupName name, Action<Transform> build)
    {
        if (parent.Find($"PopUp_{name}") != null) return;
        build(parent);
    }

    // ---------- Popups ----------

    // "Case complete!": completion line, the subjects finished on a white panel, the reward pill and
    // its note, the "UP NEXT" card, Next case and Choose a case.
    private static void BuildWin(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Win>(parent, PopupName.Win);
        holder.Find("Body").GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Top;   // the completion line, centred
        var scene = Fixed(holder, "Scene", 330);
        UIBuilder.AddImage(scene, UIBuilder.Sprite("UI/capsule_white"), sliced: true).pixelsPerUnitMultiplier = 1.2f;
        var subjects = SubjectRow(scene, out var cards);

        var reward = Fixed(holder, "Reward", 92);
        var pill = UIBuilder.Rect(reward, "Pill", UIBuilder.Center, UIBuilder.Center, UIBuilder.Center, Vector2.zero, new Vector2(400f, 92f));
        UIBuilder.AddImage(pill, UIBuilder.Sprite("UI/button_gold"), sliced: true);
        UIBuilder.AddImage(UIBuilder.Place(pill, "Coin", 34, 20, 52, 52), UIBuilder.Sprite("UI/icon_coin")).preserveAspect = true;
        var rewardText = UIBuilder.Label(pill, "Text", 96, 0, 280, 92, _bold, "+30 coins", 42, UIBuilder.Ink, TextAlignmentOptions.Center);
        var note = Text(holder, "Reward Note", _regular, 28, MutedText, "First completion reward");

        var upNext = Fixed(holder, "Up Next", 172);
        UIBuilder.AddImage(upNext, UIBuilder.Sprite("UI/panel_clue"), sliced: true);
        var kicker = UIBuilder.Label(upNext, "Kicker", 36, 24, 700, 36, _bold, "UP NEXT", 26, UIBuilder.Teal, TextAlignmentOptions.MidlineLeft);
        kicker.GetComponent<TMP_Text>().characterSpacing = 6f;
        var nextTitle = UIBuilder.Label(upNext, "Title", 36, 62, 760, 50, _bold, "Cosy pet corner", 40, UIBuilder.Ink, TextAlignmentOptions.MidlineLeft);
        UIBuilder.Label(upNext, "Line", 36, 114, 760, 34, _regular, "A new scene. The same swipe rules.", 26, MutedText, TextAlignmentOptions.MidlineLeft);

        var next = ActionButton(holder, "Next Button", ButtonStyle.Primary, "Next case", out var nextLabel);
        var map = ActionButton(holder, "Map Button", ButtonStyle.Text, "Choose a case", out _);

        SetArray(so, "_subjects", cards);
        Set(so, "_reward", rewardText.GetComponent<TMP_Text>());
        Set(so, "_rewardNote", note);
        Set(so, "_upNext", upNext.gameObject);
        Set(so, "_upNextTitle", nextTitle.GetComponent<TMP_Text>());
        Set(so, "_nextButton", next);
        Set(so, "_nextLabel", nextLabel);
        Set(so, "_mapButton", map);
        Finish(popup, so);
        _ = subjects;
    }

    private static void BuildFailed(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Failed>(parent, PopupName.Failed);
        Set(so, "_retryButton", ActionButton(holder, "Retry Button", ButtonStyle.Primary, "Retry", out _));
        Set(so, "_undoButton", ActionButton(holder, "Undo Button", ButtonStyle.Secondary, "Undo", out _));
        Set(so, "_levelsButton", ActionButton(holder, "Levels Button", ButtonStyle.Text, "Choose a case", out _));
        Finish(popup, so);
    }

    // "Hint": a clue card (bulb, the item's picture, the fact), then the next swipe in words.
    private static void BuildHint(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Hint>(parent, PopupName.Hint, withBody: false);
        var card = Fixed(holder, "Clue Card", 180);
        UIBuilder.AddImage(card, UIBuilder.Sprite("UI/panel_clue"), sliced: true);
        UIBuilder.AddImage(Anchored(card, "Bulb", new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(72f, 72f)),
                           UIBuilder.Sprite("UI/icon_hint")).preserveAspect = true;
        var icon = UIBuilder.AddImage(Anchored(card, "Icon", new Vector2(0f, 0.5f), new Vector2(178f, 0f), new Vector2(120f, 120f)),
                                      UIBuilder.Sprite("Items/crew_00"));
        icon.preserveAspect = true;
        var textRect = UIBuilder.Rect(card, "Text", Vector2.zero, Vector2.one, UIBuilder.Center, new Vector2(125f, 0f), new Vector2(-290f, -30f));
        var text = UIBuilder.AddText(textRect, _bold, "Trail bag gets the raincoat.", 40, UIBuilder.Ink);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = 28f;
        text.fontSizeMax = 40f;
        var swipe = Text(holder, "Swipe", _bold, 42, UIBuilder.Ink, "Swipe the top row left.");
        swipe.alignment = TextAlignmentOptions.TopLeft;
        var wrap = Text(holder, "Wrap", _regular, 34, UIBuilder.Ink, "The first item wraps to the last space.");
        wrap.alignment = TextAlignmentOptions.TopLeft;
        Set(so, "_clueIcon", icon);
        Set(so, "_clueText", text);
        Set(so, "_swipeText", swipe);
        Set(so, "_wrapText", wrap);
        Set(so, "_showButton", ActionButton(holder, "Show Button", ButtonStyle.Primary, "Show the next swipe", out _));
        Set(so, "_tryButton", ActionButton(holder, "Try Button", ButtonStyle.Secondary, "I’ll try it", out _));
        Finish(popup, so);
    }

    // "Notebook": the subject names over three columns, then per category its heading and three
    // entries (icon, name, a check while in place), then the rules line.
    private static void BuildNotebook(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Notebook>(parent, PopupName.Notebook);
        var namesRow = Fixed(holder, "Names", 50);
        var names = new TMP_Text[3];
        for (int c = 0; c < 3; c++)
        {
            names[c] = UIBuilder.AddText(Column(namesRow, $"Name {c + 1}", c), _bold, "Trail", 32, UIBuilder.Teal);
            names[c].enableAutoSizing = true;
            names[c].fontSizeMin = 22f;
            names[c].fontSizeMax = 32f;
        }

        var headings = new TMP_Text[3];
        var icons = new Image[9];
        var labels = new TMP_Text[9];
        var checks = new GameObject[9];
        for (int r = 0; r < 3; r++)
        {
            headings[r] = Text(holder, $"Heading {r + 1}", _bold, 30, UIBuilder.Teal, "CLOTHES");
            headings[r].alignment = TextAlignmentOptions.BottomLeft;
            headings[r].characterSpacing = 4f;
            var row = Fixed(holder, $"Row {r + 1}", 104);
            for (int c = 0; c < 3; c++)
            {
                int i = r * 3 + c;
                var column = Column(row, $"Entry {c + 1}", c);
                icons[i] = UIBuilder.AddImage(Anchored(column, "Icon", new Vector2(0f, 0.5f), new Vector2(50f, 0f), new Vector2(92f, 92f)),
                                              UIBuilder.Sprite("Items/crew_00"));
                icons[i].preserveAspect = true;
                var labelRect = UIBuilder.Rect(column, "Label", Vector2.zero, Vector2.one, UIBuilder.Center, new Vector2(52f, 0f), new Vector2(-108f, 0f));
                labels[i] = UIBuilder.AddText(labelRect, _bold, "Raincoat", 28, UIBuilder.Ink);
                labels[i].alignment = TextAlignmentOptions.MidlineLeft;
                labels[i].textWrappingMode = TextWrappingModes.Normal;
                labels[i].enableAutoSizing = true;
                labels[i].fontSizeMin = 20f;
                labels[i].fontSizeMax = 28f;
                var check = UIBuilder.AddImage(Anchored(column, "Check", new Vector2(0f, 0.5f), new Vector2(86f, 30f), new Vector2(34f, 34f)),
                                               UIBuilder.Sprite("UI/check_badge"));
                checks[i] = check.gameObject;
            }
        }
        MoveBodyToEnd(holder);
        var body = holder.Find("Body").GetComponent<TMP_Text>();
        body.fontSize = 30;
        body.alignment = TextAlignmentOptions.Top;

        SetArray(so, "_names", names);
        SetArray(so, "_headings", headings);
        SetArray(so, "_icons", icons);
        SetArray(so, "_labels", labels);
        SetArray(so, "_checks", checks);
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Primary, "Back to the board", out _));
        Finish(popup, so);
    }

    // The case map, full screen on the teal backdrop: title and progress line, a vertical ScrollRect
    // over a 1080-wide content rect (PopUp_Map sizes it and clones the templates from the campaign),
    // and Back. Templates: the chapter path (map_path, 1080 x 1200), a chapter heading, and a
    // 170-unit node (sprite, number, check badge).
    private static void BuildMap(Transform parent)
    {
        var root = UIBuilder.Rect(parent, $"PopUp_{PopupName.LevelSelect}", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);
        var dim = UIBuilder.Rect(root, "Dim", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, new Vector2(3000f, 3000f));
        var dimImage = UIBuilder.AddImage(dim, null);
        dimImage.color = DimColor;
        dimImage.raycastTarget = true;
        var dimGroup = Undo.AddComponent<CanvasGroup>(dim.gameObject);

        var holder = UIBuilder.Rect(root, "Holder", UIBuilder.Center, UIBuilder.Center, UIBuilder.Center, Vector2.zero, UI_ReferenceFit.ReferenceSize);
        var backdrop = UIBuilder.AddImage(holder, UIBuilder.Sprite("UI/background_teal"));
        backdrop.raycastTarget = true;
        var title = UIBuilder.Label(holder, "Title", 40, 40, 1000, 80, _bold, "Choose a case", 60, UIBuilder.Cream, TextAlignmentOptions.Center);
        var body = UIBuilder.Label(holder, "Body", 40, 124, 1000, 44, _regular, "0/30 cases complete", 32, UIBuilder.Cream, TextAlignmentOptions.Center);

        var area = UIBuilder.Place(holder, "Map", 0, 180, 1080, 1560);
        var viewport = UIBuilder.Rect(area, "Viewport", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);
        var hit = UIBuilder.AddImage(viewport, null);
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;
        Undo.AddComponent<RectMask2D>(viewport.gameObject);
        var content = UIBuilder.Rect(viewport, "Content", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1080f, 6000f));
        var scroll = Undo.AddComponent<ScrollRect>(area.gameObject);
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var path = UIBuilder.AddImage(UIBuilder.Rect(content, "Path Template", new Vector2(0f, 1f), new Vector2(0f, 1f), UIBuilder.Center,
                                                     Vector2.zero, new Vector2(1080f, 1200f)), UIBuilder.Sprite("UI/map_path"));
        var heading = UIBuilder.Rect(content, "Heading Template", new Vector2(0f, 1f), new Vector2(0f, 1f), UIBuilder.Center,
                                     Vector2.zero, new Vector2(900f, 66f));
        UIBuilder.AddImage(heading, UIBuilder.Sprite("UI/capsule_white"), sliced: true).color = UIBuilder.Cream;
        UIBuilder.AddText(UIBuilder.Rect(heading, "Label", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero),
                          _bold, "A busy morning", 36, UIBuilder.Ink);
        var nodeRect = UIBuilder.Rect(content, "Node Template", new Vector2(0f, 1f), new Vector2(0f, 1f), UIBuilder.Center, Vector2.zero, new Vector2(170f, 170f));
        var nodeImage = UIBuilder.AddImage(nodeRect, UIBuilder.Sprite("UI/map_node_open"));
        nodeImage.preserveAspect = true;
        var node = UIBuilder.MakeButton(nodeRect, nodeImage);
        var colors = node.colors;
        colors.disabledColor = Color.white;          // locked nodes keep their own sprite's look
        node.colors = colors;
        UIBuilder.AddText(UIBuilder.Rect(nodeRect, "Number", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero),
                          _bold, "1", 58, UIBuilder.Ink);
        UIBuilder.AddImage(UIBuilder.Rect(nodeRect, "Check", Vector2.one, Vector2.one, UIBuilder.Center, new Vector2(-24f, -24f), new Vector2(60f, 60f)),
                           UIBuilder.Sprite("UI/check_badge"));

        var back = UIBuilder.Place(holder, "Back Button", 290, 1770, 500, 110);
        var backSurface = UIBuilder.AddImage(back, UIBuilder.Sprite("UI/button_cream"), sliced: true);
        UIBuilder.Label(back, "Label", 0, 0, 500, 110, _bold, "Back", 42, UIBuilder.Ink, TextAlignmentOptions.Center);

        var popup = Undo.AddComponent<PopUp_Map>(root.gameObject);
        var so = new SerializedObject(popup);
        so.FindProperty("_popupName").enumValueIndex = (int)PopupName.LevelSelect;
        Set(so, "_holder", holder);
        Set(so, "_dim", dimGroup);
        Set(so, "_title", title.GetComponent<TMP_Text>());
        Set(so, "_body", body.GetComponent<TMP_Text>());
        Set(so, "_scroll", scroll);
        Set(so, "_content", content);
        Set(so, "_pathTemplate", path);
        Set(so, "_headingTemplate", heading);
        Set(so, "_nodeTemplate", node);
        Set(so, "_currentSprite", UIBuilder.Sprite("UI/map_node_current"));
        Set(so, "_openSprite", UIBuilder.Sprite("UI/map_node_open"));
        Set(so, "_lockedSprite", UIBuilder.Sprite("UI/map_node_locked"));
        Set(so, "_backButton", UIBuilder.MakeButton(back, backSurface));
        path.gameObject.SetActive(false);
        heading.gameObject.SetActive(false);
        nodeRect.gameObject.SetActive(false);
        Finish(popup, so);
    }

    private static void BuildMenu(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Menu>(parent, PopupName.Menu);
        var sound = ActionButton(holder, "Sound Button", ButtonStyle.Secondary, "Sound effects: On", out var soundLabel);
        Set(so, "_soundButton", sound);
        Set(so, "_soundLabel", soundLabel);
        Set(so, "_restartButton", ActionButton(holder, "Restart Button", ButtonStyle.Secondary, "Restart this case", out _));
        Set(so, "_levelsButton", ActionButton(holder, "Levels Button", ButtonStyle.Secondary, "Choose a case", out _));
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Primary, "Back to the game", out _));
        Finish(popup, so);
    }

    private static void BuildFeedback(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Feedback>(parent, PopupName.Feedback);
        ScrollArea(holder, "Questions", 700, out var content);
        var question = Text(content, "Question Template", _bold, 34, UIBuilder.Ink, "Was the goal clear?");
        var option = ChoiceRow(content, "Option Template", 86f, withSubtitle: false);
        Set(so, "_questionTemplate", question);
        Set(so, "_optionTemplate", option);
        Set(so, "_saveButton", ActionButton(holder, "Save Button", ButtonStyle.Primary, "Save feedback and results", out _));
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Secondary, "Back to the map", out _));
        Finish(popup, so);
    }

    // ---------- Shell ----------

    // Root (full screen) -> Dim + Holder (panel_modal, vertical layout): [check] kicker, title, [body].
    private static (T popup, RectTransform holder, SerializedObject so) Shell<T>(Transform parent, PopupName name,
        bool withBody = true, bool checkOnTop = false, bool bodyAfterCustom = false) where T : BasePopUp
    {
        var root = UIBuilder.Rect(parent, $"PopUp_{name}", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);

        // Reaches well past the reference rect so it covers notches and letterbox areas too.
        var dim = UIBuilder.Rect(root, "Dim", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, new Vector2(3000f, 3000f));
        var dimImage = UIBuilder.AddImage(dim, null);
        dimImage.color = DimColor;
        dimImage.raycastTarget = true;
        var dimGroup = Undo.AddComponent<CanvasGroup>(dim.gameObject);

        var holder = UIBuilder.Rect(root, "Holder", UIBuilder.Center, UIBuilder.Center, UIBuilder.Center, Vector2.zero, new Vector2(PanelWidth, 400f));
        var panel = UIBuilder.AddImage(holder, UIBuilder.Sprite("UI/panel_modal"), sliced: true);
        panel.raycastTarget = true;
        var layout = Undo.AddComponent<VerticalLayoutGroup>(holder.gameObject);
        layout.padding = new RectOffset(56, 56, 52, 52);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = Undo.AddComponent<ContentSizeFitter>(holder.gameObject);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (checkOnTop)
        {
            var check = Fixed(holder, "Check", 96);
            UIBuilder.AddImage(Anchored(check, "Badge", UIBuilder.Center, Vector2.zero, new Vector2(96f, 96f)),
                               UIBuilder.Sprite("UI/check_badge")).preserveAspect = true;
        }
        var kicker = Text(holder, "Kicker", _bold, 28, UIBuilder.Teal, "PARADE PREP");
        kicker.characterSpacing = 8f;
        var title = Text(holder, "Title", _bold, 58, UIBuilder.Ink, "Title");
        TMP_Text body = withBody ? Text(holder, "Body", _regular, 36, UIBuilder.Ink, "Body") : null;
        if (body != null) body.alignment = TextAlignmentOptions.TopLeft;

        var popup = Undo.AddComponent<T>(root.gameObject);
        var so = new SerializedObject(popup);
        so.FindProperty("_popupName").enumValueIndex = (int)name;
        Set(so, "_holder", holder);
        Set(so, "_dim", dimGroup);
        Set(so, "_kicker", kicker);
        Set(so, "_title", title);
        if (body != null) Set(so, "_body", body);
        _ = bodyAfterCustom;
        return (popup, holder, so);
    }

    private static void Finish(BasePopUp popup, SerializedObject so)
    {
        so.ApplyModifiedPropertiesWithoutUndo();
        popup.gameObject.SetActive(false);
    }

    private static void MoveBodyToEnd(RectTransform holder) => holder.Find("Body").SetAsLastSibling();

    // ---------- Widgets ----------

    private static TMP_Text Text(RectTransform parent, string name, TMP_FontAsset font, float size, Color color, string text)
    {
        var rect = UIBuilder.Rect(parent, name, Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);
        var tmp = UIBuilder.AddText(rect, font, text, size, color);
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.alignment = TextAlignmentOptions.Top;
        tmp.lineSpacing = 6f;
        return tmp;
    }

    // A layout child of fixed height (full width).
    private static RectTransform Fixed(RectTransform parent, string name, float height)
    {
        var rect = UIBuilder.Rect(parent, name, Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);
        var element = Undo.AddComponent<LayoutElement>(rect.gameObject);
        element.minHeight = element.preferredHeight = height;
        return rect;
    }

    private static RectTransform Anchored(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size) =>
        UIBuilder.Rect(parent, name, anchor, anchor, UIBuilder.Center, position, size);

    private static Button ActionButton(RectTransform parent, string name, ButtonStyle style, string text, out TMP_Text label)
    {
        float height = style == ButtonStyle.Primary ? 112f : style == ButtonStyle.Secondary ? 100f : 72f;
        var rect = Fixed(parent, name, height);
        var surface = UIBuilder.AddImage(rect, style == ButtonStyle.Text ? null
                                               : UIBuilder.Sprite(style == ButtonStyle.Primary ? "UI/button_gold" : "UI/button_cream"),
                                         sliced: style != ButtonStyle.Text);
        if (style == ButtonStyle.Text) surface.color = new Color(1f, 1f, 1f, 0f);   // invisible, still catches taps
        var labelRect = UIBuilder.Rect(rect, "Label", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, new Vector2(-40f, 0f));
        label = UIBuilder.AddText(labelRect, _bold, style == ButtonStyle.Text ? $"<u>{text}</u>" : text,
                                  style == ButtonStyle.Primary ? 42 : style == ButtonStyle.Secondary ? 36 : 32,
                                  style == ButtonStyle.Text ? UIBuilder.Teal : UIBuilder.Ink);
        label.enableAutoSizing = true;
        label.fontSizeMin = 24f;
        label.fontSizeMax = label.fontSize;
        return UIBuilder.MakeButton(rect, surface);
    }

    // Three UI_SubjectCards side by side (the win popup's finished scene): each a white card with
    // the subject's rig and a name tab under it.
    private static RectTransform SubjectRow(RectTransform parent, out UI_SubjectCard[] cards)
    {
        var row = UIBuilder.Rect(parent, "Subjects", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, new Vector2(-30f, -30f));
        var layout = Undo.AddComponent<HorizontalLayoutGroup>(row.gameObject);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        cards = new UI_SubjectCard[3];
        for (int i = 0; i < cards.Length; i++)
        {
            var subject = UIBuilder.Rect(row, $"Subject {i + 1}", UIBuilder.Center, UIBuilder.Center, UIBuilder.Center,
                                         Vector2.zero, new Vector2(250f, 290f));
            cards[i] = UIBuilder.SubjectCard(subject, _bold, 250, 240, 50, new[] { 8f, 6f, 234f, 228f }, Color.white, 30);
        }
        return row;
    }

    // A third of `parent`'s width (column c of 3), full height.
    private static RectTransform Column(RectTransform parent, string name, int c) =>
        UIBuilder.Rect(parent, name, new Vector2(c / 3f, 0f), new Vector2((c + 1) / 3f, 1f), UIBuilder.Center, Vector2.zero, Vector2.zero);

    // A fixed-height vertical ScrollRect; content stacks its children (vertical layout, preferred height).
    private static ScrollRect ScrollArea(RectTransform parent, string name, float height, out RectTransform content)
    {
        var area = Fixed(parent, name, height);
        var viewport = UIBuilder.Rect(area, "Viewport", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);
        var hit = UIBuilder.AddImage(viewport, null);
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;                  // drags anywhere in the area scroll it
        Undo.AddComponent<RectMask2D>(viewport.gameObject);

        content = UIBuilder.Rect(viewport, "Content", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        var layout = Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
        layout.spacing = 14f;
        layout.padding = new RectOffset(4, 4, 4, 12);
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = Undo.AddComponent<ScrollRect>(area.gameObject);
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return scroll;
    }

    // UI_ChoiceRow: a tappable cream row with a round node (number / check badge), title and subtitle.
    private static UI_ChoiceRow ChoiceRow(RectTransform parent, string name, float height, bool withSubtitle)
    {
        var rect = Fixed(parent, name, height);
        var surface = UIBuilder.AddImage(rect, UIBuilder.Sprite("UI/panel_clue"), sliced: true);
        var button = UIBuilder.MakeButton(rect, surface);

        var node = Anchored(rect, "Node", new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(64f, 64f));
        var nodeImage = UIBuilder.AddImage(node, UIBuilder.Sprite("UI/capsule_white"), sliced: true);
        var nodeText = UIBuilder.AddText(UIBuilder.Rect(node, "Text", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero),
                                         _bold, "1", 32, UIBuilder.Ink);
        var done = UIBuilder.AddImage(UIBuilder.Rect(node, "Done", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero),
                                      UIBuilder.Sprite("UI/check_badge"));
        done.preserveAspect = true;

        var titleRect = UIBuilder.Rect(rect, "Title", new Vector2(0f, withSubtitle ? 0.45f : 0f), Vector2.one, UIBuilder.Center,
                                       new Vector2(50f, 0f), new Vector2(-140f, 0f));
        var title = UIBuilder.AddText(titleRect, _bold, "Hats on!", withSubtitle ? 36 : 32, UIBuilder.Ink);
        title.alignment = withSubtitle ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.MidlineLeft;
        var subRect = UIBuilder.Rect(rect, "Subtitle", Vector2.zero, new Vector2(1f, 0.45f), UIBuilder.Center,
                                     new Vector2(50f, 0f), new Vector2(-140f, 0f));
        var subtitle = UIBuilder.AddText(subRect, _regular, "Learn · no move limit", 26, new Color32(0x87, 0x72, 0x54, 0xFF));
        subtitle.alignment = TextAlignmentOptions.TopLeft;
        if (!withSubtitle) subRect.gameObject.SetActive(false);

        var row = Undo.AddComponent<UI_ChoiceRow>(rect.gameObject);
        var so = new SerializedObject(row);
        Set(so, "_button", button);
        Set(so, "_surface", surface);
        Set(so, "_node", nodeImage);
        Set(so, "_nodeText", nodeText);
        Set(so, "_doneMark", done.gameObject);
        Set(so, "_title", title);
        Set(so, "_subtitle", subtitle);
        so.ApplyModifiedPropertiesWithoutUndo();
        rect.gameObject.SetActive(false);
        return row;
    }

    // ---------- Serialized fields ----------

    private static void Set(SerializedObject so, string field, Object value)
    {
        var property = so.FindProperty(field);
        if (property == null) Debug.LogError($"[PopUpBuilder] {so.targetObject.GetType().Name} has no field '{field}'.");
        else property.objectReferenceValue = value;
    }

    private static void SetArray(SerializedObject so, string field, Object[] values)
    {
        var property = so.FindProperty(field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
