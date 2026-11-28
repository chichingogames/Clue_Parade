using System;
using System.Collections.Generic;
using Chichingo.TilewrapVaried;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The case map (PopupName.LevelSelect): a vertically scrolling 1080 x totalHeight content rect
// (campaign.json map; top-left pivot, coordinates from the top-left), map_path repeated once per
// 1200-unit chapter section with its heading, and one 170-unit node per case: the current, open
// or locked sprite by the saved unlock state, the live level number, check_badge once cleared.
// Open nodes start their case. Nodes, paths and headings are cloned from inactive templates the
// first time, then only their state changes. The scroll centres on the given node.
public class PopUp_Map : BasePopUp
{
    public enum NodeState { Locked, Open, Current }

    public struct Node
    {
        public int Level;
        public NodeState State;
        public bool Done;
    }

    [SerializeField] private ScrollRect _scroll;
    [SerializeField] private RectTransform _content;
    [SerializeField] private Image _pathTemplate;
    [Tooltip("A chapter's banner (cream capsule) with a TMP_Text child named Label.")]
    [SerializeField] private RectTransform _headingTemplate;
    [SerializeField] private Button _nodeTemplate;
    [SerializeField] private Sprite _currentSprite;
    [SerializeField] private Sprite _openSprite;
    [SerializeField] private Sprite _lockedSprite;
    [SerializeField] private Color _lockedInk = new Color32(0x2D, 0x57, 0x4F, 0xFF);
    [SerializeField] private Color _ink = new Color32(0x40, 0x28, 0x1F, 0xFF);
    [SerializeField] private Button _backButton;

    private readonly Dictionary<int, Button> _nodes = new Dictionary<int, Button>();
    private bool _built;

    public void Setup(string subtitle, CampaignData campaign, IReadOnlyList<Node> nodes, int focusLevel,
                      Action<int> onPick, Action onBack)
    {
        SetTexts("", "Choose a case", subtitle);
        if (!_built) Build(campaign);

        foreach (var node in nodes)
        {
            if (!_nodes.TryGetValue(node.Level, out var button)) continue;
            var image = (Image)button.targetGraphic;
            image.sprite = node.State == NodeState.Current ? _currentSprite : node.State == NodeState.Open ? _openSprite : _lockedSprite;
            button.transform.Find("Number").GetComponent<TMP_Text>().color = node.State == NodeState.Locked ? _lockedInk : _ink;
            button.transform.Find("Check").gameObject.SetActive(node.Done);
            button.interactable = node.State != NodeState.Locked;
            int level = node.Level;
            Bind(button, () => onPick(level));
        }
        Bind(_backButton, onBack);
        Focus(campaign, focusLevel);
    }

    private void Build(CampaignData campaign)
    {
        _built = true;
        var map = campaign.map;
        _content.sizeDelta = new Vector2(_content.sizeDelta.x, map.totalHeight);
        _pathTemplate.gameObject.SetActive(false);
        _headingTemplate.gameObject.SetActive(false);
        _nodeTemplate.gameObject.SetActive(false);

        foreach (var section in map.sections)
        {
            var path = Instantiate(_pathTemplate, _content);
            path.gameObject.SetActive(true);
            path.name = $"Path {section.chapterId}";
            Place((RectTransform)path.transform, map.referenceWidth * 0.5f, section.y + map.sectionHeight * 0.5f);
            path.transform.SetAsFirstSibling();

            // The banner sits across the top of its chapter's section, where the path enters it.
            var heading = Instantiate(_headingTemplate, _content);
            heading.gameObject.SetActive(true);
            heading.name = $"Chapter {section.chapterId}";
            heading.Find("Label").GetComponent<TMP_Text>().text = section.title;
            Place(heading, map.referenceWidth * 0.5f, section.y + 60f);
        }

        foreach (var node in map.nodes)
        {
            var button = Instantiate(_nodeTemplate, _content);
            button.gameObject.SetActive(true);
            button.name = $"Node {node.levelId}";
            button.transform.Find("Number").GetComponent<TMP_Text>().text = node.levelId.ToString();
            Place((RectTransform)button.transform, node.x, node.y);
            _nodes[node.levelId] = button;
        }
    }

    // Centre the viewport on a node (top-left map coordinates).
    private void Focus(CampaignData campaign, int level)
    {
        Canvas.ForceUpdateCanvases();
        float y = campaign.map.totalHeight;
        foreach (var node in campaign.map.nodes)
            if (node.levelId == level) y = node.y;
        float viewport = _scroll.viewport.rect.height;
        float scrollable = Mathf.Max(1f, campaign.map.totalHeight - viewport);
        float top = Mathf.Clamp(y - viewport * 0.5f, 0f, scrollable);
        _scroll.verticalNormalizedPosition = 1f - top / scrollable;
    }

    // Anchor top-left, centre at (x, -y).
    private static void Place(RectTransform rect, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
    }
}
