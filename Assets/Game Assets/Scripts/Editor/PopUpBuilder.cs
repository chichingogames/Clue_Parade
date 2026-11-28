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
        BuildIfMissing(parent, PopupName.LevelSelect, BuildCases);
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
        var rewardTmp = rewardText.GetComponent<TMP_Text>();
        rewardTmp.enableAutoSizing = true;          // "Already collected" shrinks to stay clear of the coin
        rewardTmp.fontSizeMin = 24f;
        rewardTmp.fontSizeMax = 42f;
        rewardTmp.overflowMode = TextOverflowModes.Ellipsis;
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

    // "A helpful next swap": the two tiles on a pale panel (item -> neighbour, or "Empty space"),
    // the instruction, the body (why it helps), "Show me on the board", "I'll try it".
    private static void BuildHint(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Hint>(parent, PopupName.Hint);
        var pair = Fixed(holder, "Pair", 170);
        UIBuilder.AddImage(pair, UIBuilder.Sprite("UI/panel_clue"), sliced: true).color = new Color32(0xFF, 0xF3, 0xCE, 0xFF);
        var item = UIBuilder.AddImage(Anchored(pair, "Item", UIBuilder.Center, new Vector2(-150f, 0f), new Vector2(150f, 140f)),
                                      UIBuilder.Sprite("Items/crew_00"));
        item.preserveAspect = true;
        var arrow = UIBuilder.AddImage(Anchored(pair, "Arrow", UIBuilder.Center, Vector2.zero, new Vector2(90f, 60f)),
                                       UIBuilder.Sprite("Tutorial/tutorial_arrow"));
        arrow.preserveAspect = true;
        var other = UIBuilder.AddImage(Anchored(pair, "Other", UIBuilder.Center, new Vector2(150f, 0f), new Vector2(150f, 140f)),
                                       UIBuilder.Sprite("Items/crew_01"));
        other.preserveAspect = true;
        var empty = UIBuilder.AddText(Anchored(pair, "Empty", UIBuilder.Center, new Vector2(150f, 0f), new Vector2(180f, 120f)), _bold,
                                      "Empty\nspace", 32, new Color32(0xA3, 0x78, 0x33, 0xFF));
        empty.alignment = TextAlignmentOptions.Center;
        empty.textWrappingMode = TextWrappingModes.Normal;

        var instruction = Text(holder, "Instruction", _bold, 42, UIBuilder.Ink, "Swipe the purple cap right.");
        MoveBodyToEnd(holder);
        var body = holder.Find("Body").GetComponent<TMP_Text>();
        body.fontSize = 32;
        body.color = new Color32(0x76, 0x52, 0x3D, 0xFF);

        Set(so, "_itemIcon", item);
        Set(so, "_otherIcon", other);
        Set(so, "_emptyLabel", empty.gameObject);
        Set(so, "_instruction", instruction);
        Set(so, "_showButton", ActionButton(holder, "Show Button", ButtonStyle.Primary, "Show me on the board", out _));
        Set(so, "_tryButton", ActionButton(holder, "Try Button", ButtonStyle.Text, "I’ll try it", out _));
        empty.gameObject.SetActive(false);
        Finish(popup, so);
    }

    // "Your notebook": in a scrolling list, one section per set (up to 5): a cream card with the set's
    // name, its status on the right, and three facts (icon + sentence). Then the rules line (body)
    // and Back to the board.
    private static void BuildNotebook(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Notebook>(parent, PopupName.Notebook);
        MoveBodyToEnd(holder);
        var body = holder.Find("Body").GetComponent<TMP_Text>();
        body.fontSize = 28;
        body.color = new Color32(0x76, 0x52, 0x3D, 0xFF);
        holder.Find("Body").SetSiblingIndex(holder.Find("Title").GetSiblingIndex() + 1);
        ScrollArea(holder, "Sets", 980, out var content);

        var sections = so.FindProperty("_sections");
        sections.arraySize = 5;
        for (int i = 0; i < 5; i++)
        {
            var section = Fixed(content, $"Set {i + 1}", 300);
            var surface = UIBuilder.AddImage(section, UIBuilder.Sprite("UI/panel_clue"), sliced: true);
            var label = UIBuilder.AddText(Anchored(section, "Label", new Vector2(0f, 1f), new Vector2(210f, -40f), new Vector2(360f, 50f)),
                                          _bold, "Hats", 34, UIBuilder.Ink);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var status = UIBuilder.AddText(Anchored(section, "Status", new Vector2(1f, 1f), new Vector2(-130f, -40f), new Vector2(220f, 40f)),
                                           _bold, "ACTIVE", 22, UIBuilder.Teal);
            status.alignment = TextAlignmentOptions.MidlineRight;
            status.characterSpacing = 4f;
            var icons = new Image[3];
            var texts = new TMP_Text[3];
            for (int f = 0; f < 3; f++)
            {
                var fact = Anchored(section, $"Fact {f + 1}", new Vector2(0.5f, 1f), new Vector2(0f, -100f - f * 72f), new Vector2(820f, 68f));
                icons[f] = UIBuilder.AddImage(Anchored(fact, "Icon", new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(76f, 64f)),
                                              UIBuilder.Sprite("Items/crew_00"));
                icons[f].preserveAspect = true;
                texts[f] = UIBuilder.AddText(UIBuilder.Rect(fact, "Text", Vector2.zero, Vector2.one, UIBuilder.Center, new Vector2(52f, 0f), new Vector2(-104f, 0f)),
                                             _regular, "Mia gets the red cap.", 28, UIBuilder.Ink);
                texts[f].alignment = TextAlignmentOptions.MidlineLeft;
                texts[f].textWrappingMode = TextWrappingModes.Normal;
                texts[f].enableAutoSizing = true;
                texts[f].fontSizeMin = 20f;
                texts[f].fontSizeMax = 28f;
            }
            var view = sections.GetArrayElementAtIndex(i);
            view.FindPropertyRelative("Root").objectReferenceValue = section.gameObject;
            view.FindPropertyRelative("Surface").objectReferenceValue = surface;
            view.FindPropertyRelative("Label").objectReferenceValue = label;
            view.FindPropertyRelative("Status").objectReferenceValue = status;
            SetArray(view.FindPropertyRelative("Icons"), icons);
            SetArray(view.FindPropertyRelative("Texts"), texts);
        }
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Primary, "Back to the board", out _));
        Finish(popup, so);
    }

    // "Choose a case": progress line (body), a scrolling list of case rows (number or check, title,
    // what the case introduces) and Back. Registered as LevelSelect.
    private static void BuildCases(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Cases>(parent, PopupName.LevelSelect);
        var body = holder.Find("Body").GetComponent<TMP_Text>();
        body.fontSize = 30;
        body.alignment = TextAlignmentOptions.Top;
        ScrollArea(holder, "Cases", 760, out var content);
        Set(so, "_rowTemplate", ChoiceRow(content, "Case Template", 130f, withSubtitle: true));
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Secondary, "Back to the board", out _));
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

    private static void SetArray(SerializedObject so, string field, Object[] values) => SetArray(so.FindProperty(field), values);

    private static void SetArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
