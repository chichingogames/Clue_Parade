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

        BuildIfMissing(parent, PopupName.Lesson, BuildLesson);
        BuildIfMissing(parent, PopupName.Win, BuildWin);
        BuildIfMissing(parent, PopupName.Failed, BuildFailed);
        BuildIfMissing(parent, PopupName.Hint, BuildHint);
        BuildIfMissing(parent, PopupName.Notebook, BuildNotebook);
        BuildIfMissing(parent, PopupName.LevelSelect, BuildLevelSelect);
        BuildIfMissing(parent, PopupName.Menu, BuildMenu);
        BuildIfMissing(parent, PopupName.Confirm, BuildConfirm);
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

    private static void BuildLesson(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Lesson>(parent, PopupName.Lesson);
        var crewRow = CrewRow(holder, out var crew);
        var moves = Fixed(holder, "Moves Example", 150);
        var panel = UIBuilder.Rect(moves, "Panel", UIBuilder.Center, UIBuilder.Center, UIBuilder.Center, Vector2.zero, new Vector2(360f, 140f));
        UIBuilder.AddImage(panel, UIBuilder.Sprite("UI/panel_moves"), sliced: true);
        var value = UIBuilder.Label(panel, "Value", 0, 4, 360, 84, _bold, "12", 84, UIBuilder.Ink, TextAlignmentOptions.Center);
        UIBuilder.Label(panel, "Label", 0, 92, 360, 38, _regular, "moves left", 30, UIBuilder.Ink, TextAlignmentOptions.Center);
        var button = ActionButton(holder, "Button", ButtonStyle.Primary, "Show me", out var label);

        SetArray(so, "_crew", crew);
        Set(so, "_crewRow", crewRow.gameObject);
        Set(so, "_movesExample", moves.gameObject);
        Set(so, "_movesValue", value.GetComponent<TMP_Text>());
        Set(so, "_button", button);
        Set(so, "_buttonLabel", label);
        Finish(popup, so);
    }

    private static void BuildWin(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Win>(parent, PopupName.Win, checkOnTop: true);
        holder.Find("Body").GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Top;   // the payoff line, centred
        var crewRow = CrewRow(holder, out var crew);
        var matched = Text(holder, "Matched", _bold, 38, UIBuilder.Ink, "9/9 items matched");
        var reward = Fixed(holder, "Reward", 76);
        var pill = UIBuilder.Rect(reward, "Pill", UIBuilder.Center, UIBuilder.Center, UIBuilder.Center, Vector2.zero, new Vector2(560f, 72f));
        UIBuilder.AddImage(pill, UIBuilder.Sprite("UI/capsule_white"), sliced: true).color = new Color32(0xFF, 0xF0, 0xBC, 0xFF);
        UIBuilder.AddImage(UIBuilder.Place(pill, "Coin", 22, 12, 48, 48), UIBuilder.Sprite("UI/icon_coin")).preserveAspect = true;
        var rewardText = UIBuilder.Label(pill, "Text", 80, 0, 460, 72, _bold, "+30 coins", 32, UIBuilder.Ink, TextAlignmentOptions.Center);
        var stats = Text(holder, "Stats", _regular, 30, MutedText, "1 swipe · relaxed practice");
        var next = ActionButton(holder, "Next Button", ButtonStyle.Primary, "Next", out var nextLabel);
        var levels = ActionButton(holder, "Levels Button", ButtonStyle.Secondary, "Choose a level", out _);

        SetArray(so, "_crew", crew);
        Set(so, "_matched", matched);
        Set(so, "_reward", rewardText.GetComponent<TMP_Text>());
        Set(so, "_stats", stats);
        Set(so, "_nextButton", next);
        Set(so, "_nextLabel", nextLabel);
        Set(so, "_levelsButton", levels);
        Finish(popup, so);
        _ = crewRow;
    }

    private static void BuildFailed(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Failed>(parent, PopupName.Failed);
        Set(so, "_undoButton", ActionButton(holder, "Undo Button", ButtonStyle.Primary, "Undo last move", out _));
        Set(so, "_retryButton", ActionButton(holder, "Retry Button", ButtonStyle.Secondary, "Try again", out _));
        Set(so, "_levelsButton", ActionButton(holder, "Levels Button", ButtonStyle.Text, "Choose a level", out _));
        Finish(popup, so);
    }

    private static void BuildHint(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Hint>(parent, PopupName.Hint, bodyAfterCustom: true);
        var card = Fixed(holder, "Clue Card", 200);
        UIBuilder.AddImage(card, UIBuilder.Sprite("UI/panel_clue"), sliced: true);
        var icon = UIBuilder.AddImage(Anchored(card, "Icon", new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(150f, 150f)),
                                      UIBuilder.Sprite("Items/red_bag"));
        icon.preserveAspect = true;
        var textRect = UIBuilder.Rect(card, "Text", Vector2.zero, Vector2.one, UIBuilder.Center, new Vector2(100f, 0f), new Vector2(-240f, -30f));
        var text = UIBuilder.AddText(textRect, _bold, "Mia carries the red bag.", 44, UIBuilder.Ink);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = 30f;
        text.fontSizeMax = 46f;
        MoveBodyToEnd(holder);
        Set(so, "_clueIcon", icon);
        Set(so, "_clueText", text);
        Set(so, "_showButton", ActionButton(holder, "Show Button", ButtonStyle.Primary, "Show the next swipe", out _));
        Set(so, "_tryButton", ActionButton(holder, "Try Button", ButtonStyle.Secondary, "I’ll try it", out _));
        Finish(popup, so);
    }

    private static void BuildNotebook(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Notebook>(parent, PopupName.Notebook, withBody: false);
        CrewRow(holder, out var crew);
        var scroll = ScrollArea(holder, "Clues", 600, out var content);
        var body = Text(content, "Body", _regular, 34, UIBuilder.Ink, "");
        body.alignment = TextAlignmentOptions.TopLeft;
        Set(so, "_body", body);
        SetArray(so, "_crew", crew);
        Set(so, "_scroll", scroll);
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Primary, "Back to the board", out _));
        Finish(popup, so);
    }

    private static void BuildLevelSelect(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_LevelSelect>(parent, PopupName.LevelSelect);
        var list = UIBuilder.Rect(holder, "Rows", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);
        var layout = Undo.AddComponent<VerticalLayoutGroup>(list.gameObject);
        layout.spacing = 12f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var row = ChoiceRow(list, "Row Template", 108f, withSubtitle: true);
        Set(so, "_rowTemplate", row);
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Secondary, "Back to this level", out _));
        Finish(popup, so);
    }

    private static void BuildMenu(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Menu>(parent, PopupName.Menu);
        var sound = ActionButton(holder, "Sound Button", ButtonStyle.Secondary, "Sound effects: On", out var soundLabel);
        Set(so, "_soundButton", sound);
        Set(so, "_soundLabel", soundLabel);
        Set(so, "_restartButton", ActionButton(holder, "Restart Button", ButtonStyle.Secondary, "Restart this level", out _));
        Set(so, "_levelsButton", ActionButton(holder, "Levels Button", ButtonStyle.Secondary, "Choose a level", out _));
        Set(so, "_exportButton", ActionButton(holder, "Export Button", ButtonStyle.Secondary, "Save results (JSON)", out _));
        Set(so, "_freshButton", ActionButton(holder, "Fresh Button", ButtonStyle.Text, "Start a fresh test", out _));
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Primary, "Back to the game", out _));
        Finish(popup, so);
    }

    private static void BuildConfirm(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Confirm>(parent, PopupName.Confirm);
        Set(so, "_yesButton", ActionButton(holder, "Yes Button", ButtonStyle.Primary, "Yes", out var yes));
        Set(so, "_yesLabel", yes);
        Set(so, "_noButton", ActionButton(holder, "No Button", ButtonStyle.Secondary, "No", out var no));
        Set(so, "_noLabel", no);
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
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Secondary, "Back to the levels", out _));
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

    // Three UI_CrewPortraits side by side (360 high): the art (5 stacked layer Images in a 600:900 box)
    // and a name tab (symbol + caption) under it.
    private static RectTransform CrewRow(RectTransform parent, out UI_CrewPortrait[] crew)
    {
        var row = Fixed(parent, "Crew", 360);
        var layout = Undo.AddComponent<HorizontalLayoutGroup>(row.gameObject);
        layout.spacing = 24f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        crew = new UI_CrewPortrait[3];
        for (int i = 0; i < crew.Length; i++)
        {
            var portrait = UIBuilder.Rect(row, $"Portrait {i + 1}", UIBuilder.Center, UIBuilder.Center, UIBuilder.Center,
                                          Vector2.zero, new Vector2(250f, 360f));
            var art = UIBuilder.Place(portrait, "Art", 25, 0, 200, 300);
            var layers = new Image[5];
            for (int l = 0; l < layers.Length; l++)
            {
                var layer = UIBuilder.Rect(art, $"Layer {l + 1}", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);
                layers[l] = UIBuilder.AddImage(layer, null);
                layers[l].preserveAspect = true;
            }
            var tab = UIBuilder.Place(portrait, "Name Tab", 10, 296, 230, 60);
            UIBuilder.AddImage(tab, UIBuilder.Sprite("UI/tab_name"), sliced: true);
            var symbol = UIBuilder.AddImage(UIBuilder.Place(tab, "Symbol", 16, 13, 34, 34), UIBuilder.Sprite("UI/symbol_mia"));
            symbol.preserveAspect = true;
            var caption = UIBuilder.Label(tab, "Caption", 56, 0, 166, 60, _bold, "Mia", 32, UIBuilder.Ink, TextAlignmentOptions.MidlineLeft)
                                   .GetComponent<TMP_Text>();
            caption.enableAutoSizing = true;
            caption.fontSizeMin = 20f;
            caption.fontSizeMax = 32f;

            var component = Undo.AddComponent<UI_CrewPortrait>(portrait.gameObject);
            var so = new SerializedObject(component);
            SetArray(so, "_layers", layers);
            Set(so, "_symbol", symbol);
            Set(so, "_caption", caption);
            so.ApplyModifiedPropertiesWithoutUndo();
            crew[i] = component;
        }
        return row;
    }

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
