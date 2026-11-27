using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One tappable row in a popup list: a round node (number or check), a title and a small line under
// it. Used by the level selector (one row per level) and the feedback questions (one per answer).
// Popups instantiate it from an inactive template child.
public class UI_ChoiceRow : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _surface;
    [SerializeField] private Image _node;
    [SerializeField] private TMP_Text _nodeText;
    [Tooltip("check_badge, shown instead of the node text when done.")]
    [SerializeField] private GameObject _doneMark;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _subtitle;

    [SerializeField] private Color _surfaceColor = new Color32(0xFF, 0xF0, 0xD3, 0xFF);
    [SerializeField] private Color _selectedColor = new Color32(0xF2, 0xF7, 0xDE, 0xFF);
    [SerializeField] private Color _nodeColor = new Color32(0xE8, 0xBF, 0x78, 0xFF);
    [SerializeField] private Color _doneColor = new Color32(0x81, 0xBC, 0x5D, 0xFF);

    // done: green node with a check mark instead of nodeText. selected: highlighted surface.
    public void Setup(string nodeText, string title, string subtitle, bool done, bool selected, Action onClick)
    {
        gameObject.SetActive(true);
        _nodeText.text = nodeText;
        _nodeText.gameObject.SetActive(!done);
        _doneMark.SetActive(done);
        _node.color = done ? _doneColor : _nodeColor;
        _title.text = title;
        _subtitle.text = subtitle ?? "";
        _subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
        SetSelected(selected);
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() =>
        {
            Manager_Audio.Instance?.PlaySound(SoundType.UiTap);
            Haptics.Play(HapticType.Light);
            onClick?.Invoke();
        });
    }

    public void SetSelected(bool selected) => _surface.color = selected ? _selectedColor : _surfaceColor;
}
