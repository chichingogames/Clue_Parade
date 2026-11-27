using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A customer (or an icon) inside a popup: _layers are stacked uGUI Images in one 600:900 rect,
// filled from a Customer prefab's own layers, so the lesson / win / notebook crew is drawn by the
// same base + accessory art as the board. A name tab (symbol + caption) sits under it.
public class UI_CrewPortrait : MonoBehaviour
{
    // What one portrait shows: a Customer (dressed or not) or an icon, with its caption.
    public struct Entry
    {
        public Customer Customer;
        public Sprite Icon;
        public Sprite Symbol;
        public string Caption;
    }

    public void Show(Entry entry, bool dressed)
    {
        if (entry.Customer != null) ShowCustomer(entry.Customer, dressed, entry.Caption, entry.Symbol);
        else ShowIcon(entry.Icon, entry.Caption);
    }

    // Fills a row of portraits; entries null hides the row's portraits.
    public static void ShowRow(UI_CrewPortrait[] row, Entry[] entries, bool dressed)
    {
        for (int i = 0; i < row.Length; i++)
        {
            if (entries != null && i < entries.Length) row[i].Show(entries[i], dressed);
            else row[i].gameObject.SetActive(false);
        }
    }

    [Tooltip("Back to front, as many as the Customer prefab has layers (5).")]
    [SerializeField] private Image[] _layers = new Image[0];
    [SerializeField] private Image _symbol;
    [SerializeField] private TMP_Text _caption;

    private static readonly List<(Sprite sprite, bool accessory)> Buffer = new List<(Sprite, bool)>();

    // dressed: every accessory on (the target outfit); otherwise the base only.
    public void ShowCustomer(Customer customer, bool dressed, string caption, Sprite symbol)
    {
        gameObject.SetActive(true);
        customer.GetLayers(Buffer);
        for (int i = 0; i < _layers.Length; i++)
        {
            bool on = i < Buffer.Count && Buffer[i].sprite != null && (dressed || !Buffer[i].accessory);
            _layers[i].gameObject.SetActive(on);
            if (on) _layers[i].sprite = Buffer[i].sprite;
        }
        SetCaption(caption, symbol);
    }

    // One sprite in the portrait's place (the category icons of the column lesson).
    public void ShowIcon(Sprite icon, string caption)
    {
        gameObject.SetActive(true);
        for (int i = 0; i < _layers.Length; i++)
        {
            bool on = i == 0 && icon != null;
            _layers[i].gameObject.SetActive(on);
            if (on) _layers[i].sprite = icon;
        }
        SetCaption(caption, null);
    }

    // With a symbol the caption sits to its right; without one it is centred on the whole tab.
    private void SetCaption(string caption, Sprite symbol)
    {
        _caption.text = caption;
        _symbol.gameObject.SetActive(symbol != null);
        if (symbol != null) _symbol.sprite = symbol;

        var rect = _caption.rectTransform;
        var tab = (RectTransform)rect.parent;
        if (!_captionLayout.HasValue) _captionLayout = (rect.anchoredPosition, rect.sizeDelta);
        if (symbol != null)
        {
            (rect.anchoredPosition, rect.sizeDelta) = _captionLayout.Value;
            _caption.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
        }
        else
        {
            rect.anchoredPosition = new Vector2(tab.rect.width * 0.5f, _captionLayout.Value.position.y);
            rect.sizeDelta = new Vector2(tab.rect.width - 16f, _captionLayout.Value.size.y);
            _caption.alignment = TMPro.TextAlignmentOptions.Center;
        }
    }

    private (Vector2 position, Vector2 size)? _captionLayout;
}
