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
// PopupName.Lesson, Confirm and Feedback are retired (no popup).
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
        BuildIfMissing(parent, PopupName.Deduction, BuildDeduction);

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

    // "Case complete!": the case question answered (icon + answer) on a card, the completion line
    // (body, when there is no evidence card), the subjects finished on a white panel (or the festival
    // star), the story card (case 5), the evidence card, the reward pill and the stats line, the
    // "UP NEXT" card with the next lead, Next case and Choose a case.
    private static void BuildWin(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Win>(parent, PopupName.Win);
        var body = holder.Find("Body").GetComponent<TMP_Text>();
        body.alignment = TextAlignmentOptions.Top;
        body.fontSize = 30;

        var resolution = Card(holder, "Resolution");
        var question = Text(resolution, "Question", _regular, 28, MutedText, "Which bag was paired with the blue shirt?");
        var answerRow = IconRow(resolution, "Answer", 72f, out var answerIcon, out var answer, _bold, 36);
        _ = answerRow;
        body.transform.SetSiblingIndex(resolution.GetSiblingIndex() + 1);

        var scene = Fixed(holder, "Scene", 300);
        UIBuilder.AddImage(scene, UIBuilder.Sprite("UI/capsule_white"), sliced: true).pixelsPerUnitMultiplier = 1.2f;
        SubjectRow(scene, out var cards);
        var star = UIBuilder.AddImage(Anchored(scene, "Star", UIBuilder.Center, Vector2.zero, new Vector2(260f, 260f)), null);
        star.preserveAspect = true;
        star.gameObject.SetActive(false);

        var story = Card(holder, "Story");
        var storyTitle = Text(story, "Title", _bold, 32, UIBuilder.Ink, "Something is missing…");
        var storyText = Text(story, "Text", _regular, 27, UIBuilder.Ink, "The festival star has not arrived.");

        var evidence = Card(holder, "Evidence");
        Text(evidence, "Heading", _bold, 26, UIBuilder.Teal, "EVIDENCE ADDED TO YOUR CASE FILE").characterSpacing = 3f;
        IconRow(evidence, "Fact", 72f, out var evidenceIcon, out var evidenceText, _regular, 27);

        var reward = Fixed(holder, "Reward", 88);
        var pill = UIBuilder.Rect(reward, "Pill", UIBuilder.Center, UIBuilder.Center, UIBuilder.Center, Vector2.zero, new Vector2(400f, 88f));
        UIBuilder.AddImage(pill, UIBuilder.Sprite("UI/button_gold"), sliced: true);
        UIBuilder.AddImage(UIBuilder.Place(pill, "Coin", 34, 18, 52, 52), UIBuilder.Sprite("UI/icon_coin")).preserveAspect = true;
        var rewardText = UIBuilder.Label(pill, "Text", 96, 0, 280, 88, _bold, "+30 coins", 42, UIBuilder.Ink, TextAlignmentOptions.Center);
        var rewardTmp = rewardText.GetComponent<TMP_Text>();
        rewardTmp.enableAutoSizing = true;          // "Already collected" shrinks to stay clear of the coin
        rewardTmp.fontSizeMin = 24f;
        rewardTmp.fontSizeMax = 42f;
        rewardTmp.overflowMode = TextOverflowModes.Ellipsis;
        var note = Text(holder, "Reward Note", _regular, 26, MutedText, "3 / 3 sets delivered");

        var upNext = Card(holder, "Up Next");
        Text(upNext, "Kicker", _bold, 24, UIBuilder.Teal, "UP NEXT").characterSpacing = 6f;
        var nextTitle = Text(upNext, "Title", _bold, 36, UIBuilder.Ink, "Welcome home");
        var nextLine = Text(upNext, "Line", _regular, 25, MutedText, "Next lead: …");
        foreach (var t in new[] { nextTitle, nextLine }) t.alignment = TextAlignmentOptions.TopLeft;

        var next = ActionButton(holder, "Next Button", ButtonStyle.Primary, "Next case", out var nextLabel);
        var map = ActionButton(holder, "Map Button", ButtonStyle.Text, "Choose a case", out _);

        Set(so, "_question", question);
        Set(so, "_answerIcon", answerIcon);
        Set(so, "_answer", answer);
        Set(so, "_sceneRoot", scene.gameObject);
        SetArray(so, "_subjects", cards);
        Set(so, "_star", star);
        Set(so, "_story", story.gameObject);
        Set(so, "_storyTitle", storyTitle);
        Set(so, "_storyText", storyText);
        Set(so, "_evidence", evidence.gameObject);
        Set(so, "_evidenceIcon", evidenceIcon);
        Set(so, "_evidenceText", evidenceText);
        Set(so, "_reward", rewardTmp);
        Set(so, "_rewardNote", note);
        Set(so, "_upNext", upNext.gameObject);
        Set(so, "_upNextTitle", nextTitle);
        Set(so, "_upNextLine", nextLine);
        Set(so, "_nextButton", next);
        Set(so, "_nextLabel", nextLabel);
        Set(so, "_mapButton", map);
        Finish(popup, so);
    }

    // "Out of moves": body, the +5 moves continue and its caption, Retry, Undo, Choose a case.
    private static void BuildFailed(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Failed>(parent, PopupName.Failed);
        holder.Find("Body").GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Top;
        Set(so, "_continueButton", ActionButton(holder, "Continue Button", ButtonStyle.Primary, "+5 moves · free", out var continueLabel));
        Set(so, "_continueLabel", continueLabel);
        Set(so, "_caption", Text(holder, "Caption", _regular, 26, MutedText, "One continue per attempt."));
        Set(so, "_retryButton", ActionButton(holder, "Retry Button", ButtonStyle.Secondary, "Retry this case · free", out _));
        Set(so, "_undoButton", ActionButton(holder, "Undo Button", ButtonStyle.Secondary, "Undo last swap · free", out _));
        Set(so, "_levelsButton", ActionButton(holder, "Levels Button", ButtonStyle.Text, "Choose a case", out _));
        Finish(popup, so);
    }

    // "Need a little help?": the "Read it another way" card, the body, Show next swap, Open free
    // notebook, the balance caption and Back to the board.
    private static void BuildHint(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Hint>(parent, PopupName.Hint);
        var card = Card(holder, "Explanation");
        Text(card, "Heading", _bold, 30, UIBuilder.Ink, "Read it another way").alignment = TextAlignmentOptions.TopLeft;
        var explanation = Text(card, "Text", _regular, 29, UIBuilder.Ink, "Leo has the blue cap. Match that item to the named column.");
        explanation.alignment = TextAlignmentOptions.TopLeft;
        MoveBodyToEnd(holder);
        var body = holder.Find("Body").GetComponent<TMP_Text>();
        body.fontSize = 29;
        body.color = new Color32(0x76, 0x52, 0x3D, 0xFF);

        Set(so, "_explanation", explanation);
        Set(so, "_showButton", ActionButton(holder, "Show Button", ButtonStyle.Primary, "Show next swap · free", out var showLabel));
        Set(so, "_showLabel", showLabel);
        Set(so, "_notebookButton", ActionButton(holder, "Notebook Button", ButtonStyle.Secondary, "Open free notebook", out _));
        Set(so, "_caption", Text(holder, "Caption", _regular, 25, MutedText, "Balance: 60 coins."));
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Text, "Back to the board", out _));
        Finish(popup, so);
    }

    // "Detective notebook": the Clues / Case file tabs, the intro (body), a scrolling list of
    // sections (a cream card: title and status, then lines of icon + text; both from inactive
    // templates) and Back to the board.
    private static void BuildNotebook(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Notebook>(parent, PopupName.Notebook);
        var tabsRow = Fixed(holder, "Tabs", 78);
        var tabs = new Button[2];
        string[] names = { "Clues", "Case file" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = UIBuilder.Rect(tabsRow, $"Tab {names[i]}", new Vector2(i * 0.5f, 0f), new Vector2((i + 1) * 0.5f, 1f), UIBuilder.Center,
                                     new Vector2(i == 0 ? -8f : 8f, 0f), new Vector2(-16f, 0f));
            var surface = UIBuilder.AddImage(tab, UIBuilder.Sprite("UI/capsule_white"), sliced: true);
            UIBuilder.AddText(UIBuilder.Rect(tab, "Label", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero),
                              _bold, names[i], 32, UIBuilder.Ink);
            tabs[i] = UIBuilder.MakeButton(tab, surface);
        }
        var body = holder.Find("Body").GetComponent<TMP_Text>();
        body.fontSize = 27;
        body.color = new Color32(0x76, 0x52, 0x3D, 0xFF);
        body.transform.SetSiblingIndex(tabsRow.GetSiblingIndex() + 1);
        var scroll = ScrollArea(holder, "Sections", 960, out var content);

        // Section template: a card stacking its header and lines.
        var section = UIBuilder.Rect(content, "Section Template", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);
        UIBuilder.AddImage(section, UIBuilder.Sprite("UI/panel_clue"), sliced: true);
        var sectionLayout = Undo.AddComponent<VerticalLayoutGroup>(section.gameObject);
        sectionLayout.padding = new RectOffset(30, 30, 20, 24);
        sectionLayout.spacing = 8f;
        sectionLayout.childControlWidth = sectionLayout.childControlHeight = true;
        sectionLayout.childForceExpandWidth = true;
        sectionLayout.childForceExpandHeight = false;
        var header = Fixed(section, "Header", 48);
        var title = UIBuilder.AddText(UIBuilder.Rect(header, "Title", Vector2.zero, Vector2.one, UIBuilder.Center, new Vector2(-90f, 0f), new Vector2(-180f, 0f)),
                                      _bold, "Hats", 32, UIBuilder.Ink);
        title.alignment = TextAlignmentOptions.MidlineLeft;
        var status = UIBuilder.AddText(UIBuilder.Rect(header, "Status", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(200f, 0f)),
                                       _bold, "ACTIVE", 22, UIBuilder.Teal);
        status.alignment = TextAlignmentOptions.MidlineRight;
        status.characterSpacing = 4f;
        var line = UIBuilder.Rect(section, "Line Template", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);
        var lineLayout = Undo.AddComponent<HorizontalLayoutGroup>(line.gameObject);
        lineLayout.spacing = 16f;
        lineLayout.childAlignment = TextAnchor.MiddleLeft;
        lineLayout.childControlWidth = lineLayout.childControlHeight = true;
        lineLayout.childForceExpandWidth = lineLayout.childForceExpandHeight = false;
        var icon = UIBuilder.AddImage(UIBuilder.Rect(line, "Icon", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero),
                                      UIBuilder.Sprite("Items/crew_00"));
        icon.preserveAspect = true;
        var iconElement = Undo.AddComponent<LayoutElement>(icon.gameObject);
        iconElement.minWidth = iconElement.preferredWidth = 64f;
        iconElement.minHeight = iconElement.preferredHeight = 64f;
        var text = UIBuilder.AddText(UIBuilder.Rect(line, "Text", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero),
                                     _regular, "Leo has the blue cap.", 28, UIBuilder.Ink);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        Undo.AddComponent<LayoutElement>(text.gameObject).flexibleWidth = 1f;
        line.gameObject.SetActive(false);
        section.gameObject.SetActive(false);

        SetArray(so, "_tabs", tabs);
        Set(so, "_sectionTemplate", section);
        Set(so, "_lineTemplate", line);
        Set(so, "_scroll", scroll);
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Primary, "Back to the board", out _));
        Finish(popup, so);
    }

    // The case map: the story line (body), a scrolling list of chapter headings and case rows (number
    // or check, title with EXPERT, rows / items / sets / moves) and Back. Registered as LevelSelect.
    private static void BuildCases(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Cases>(parent, PopupName.LevelSelect);
        var body = holder.Find("Body").GetComponent<TMP_Text>();
        body.fontSize = 28;
        body.alignment = TextAlignmentOptions.Top;
        var scroll = ScrollArea(holder, "Cases", 1060, out var content);
        var heading = UIBuilder.AddText(Fixed(content, "Heading Template", 64), _bold, "1. Festival preparations", 32, UIBuilder.Teal);
        heading.alignment = TextAlignmentOptions.BottomLeft;
        heading.margin = new Vector4(8f, 0f, 0f, 6f);
        heading.gameObject.SetActive(false);
        Set(so, "_headingTemplate", heading);
        Set(so, "_rowTemplate", ChoiceRow(content, "Case Template", 124f, withSubtitle: true));
        Set(so, "_scroll", scroll);
        Set(so, "_backButton", ActionButton(holder, "Back Button", ButtonStyle.Secondary, "Back to the board", out _));
        Finish(popup, so);
    }

    // "Final deduction": the question (title), the evidence, the feedback line and three answers.
    private static void BuildDeduction(Transform parent)
    {
        var (popup, holder, so) = Shell<PopUp_Deduction>(parent, PopupName.Deduction, withBody: false);
        var card = Card(holder, "Evidence");
        var evidence = Text(card, "Text", _regular, 29, UIBuilder.Ink, "The repair log says KEEP THE CRATE DRY.");
        evidence.alignment = TextAlignmentOptions.TopLeft;
        var feedback = Text(holder, "Feedback", _regular, 28, new Color32(0x76, 0x52, 0x3D, 0xFF), "Use both facts to close this part of the case.");
        var options = new Button[3];
        var labels = new TMP_Text[3];
        for (int i = 0; i < options.Length; i++)
        {
            options[i] = ActionButton(holder, $"Option {i + 1}", ButtonStyle.Secondary, "To protect it from the rain", out labels[i]);
            labels[i].fontSizeMax = 32f;
        }
        Set(so, "_evidence", evidence);
        Set(so, "_feedback", feedback);
        SetArray(so, "_options", options);
        SetArray(so, "_optionLabels", labels);
        Finish(popup, so);
    }

    // A cream card in a popup's layout that grows with its children (stacked, padded).
    private static RectTransform Card(RectTransform parent, string name)
    {
        var card = UIBuilder.Rect(parent, name, Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);
        UIBuilder.AddImage(card, UIBuilder.Sprite("UI/panel_clue"), sliced: true);
        var layout = Undo.AddComponent<VerticalLayoutGroup>(card.gameObject);
        layout.padding = new RectOffset(30, 30, 20, 22);
        layout.spacing = 8f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return card;
    }

    // An icon (size x size) and a wrapping text beside it, in a card's layout.
    private static RectTransform IconRow(RectTransform parent, string name, float size, out Image icon, out TMP_Text text,
                                         TMP_FontAsset font, float fontSize)
    {
        var row = UIBuilder.Rect(parent, name, Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero);
        var layout = Undo.AddComponent<HorizontalLayoutGroup>(row.gameObject);
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        icon = UIBuilder.AddImage(UIBuilder.Rect(row, "Icon", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero),
                                  UIBuilder.Sprite("Items/crew_00"));
        icon.preserveAspect = true;
        var element = Undo.AddComponent<LayoutElement>(icon.gameObject);
        element.minWidth = element.preferredWidth = size;
        element.minHeight = element.preferredHeight = size;
        text = UIBuilder.AddText(UIBuilder.Rect(row, "Text", Vector2.zero, Vector2.one, UIBuilder.Center, Vector2.zero, Vector2.zero),
                                 font, "Purple bag", fontSize, UIBuilder.Ink);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        Undo.AddComponent<LayoutElement>(text.gameObject).flexibleWidth = 1f;
        return row;
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
