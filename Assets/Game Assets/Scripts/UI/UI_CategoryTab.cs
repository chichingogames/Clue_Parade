using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The tab beside a board row with its category label ("Gear"). Gold while its row's clues are on
// screen. A tap selects the category (Manager_Board.SelectRow); it costs no move. Pure view.
public class UI_CategoryTab : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private Image _tab;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Sprite _normal;
    [SerializeField] private Sprite _active;

    public void Setup(string category, Action onTap)
    {
        gameObject.SetActive(true);
        _label.text = category;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(() => onTap?.Invoke());
    }

    public void SetActive(bool active) => _tab.sprite = active ? _active : _normal;
}
