using System;
using System.Collections.Generic;
using UnityEngine;

// Registry of popups by PopupName (skeleton.md §9.2): every Popup under _popUpParent (inactive
// included), one at a time. ShowPopup closes the others first. IsPopUpActive is true while one is
// open or still animating closed, so a dismiss tap cannot leak through to the board.
public class Manager_PopUp : Singleton<Manager_PopUp>
{
    [Tooltip("Canvas/Container/PopUps: parent of every popup instance (inactive until shown).")]
    [SerializeField] private Transform _popUpParent;

    public event Action<PopupName> OnPopupShown = delegate { };
    public event Action<PopupName> OnPopupClosed = delegate { };
    public event Action OnPopupCloseFinished = delegate { };

    private readonly Dictionary<PopupName, Popup> _popups = new Dictionary<PopupName, Popup>();

    public bool IsPopUpActive
    {
        get
        {
            foreach (var popup in _popups.Values)
                if (popup.IsOpen || popup.IsClosing) return true;
            return false;
        }
    }

    public bool IsOpen(PopupName name) => _popups.TryGetValue(name, out var popup) && popup.IsOpen;

    public override void Awake2()
    {
        if (_popUpParent == null) return;
        foreach (var popup in _popUpParent.GetComponentsInChildren<Popup>(true))
        {
            if (_popups.ContainsKey(popup.Name))
            {
                Debug.LogWarning($"[Manager_PopUp] Two popups named {popup.Name}; '{popup.name}' ignored.", popup);
                continue;
            }
            _popups[popup.Name] = popup;
            popup.gameObject.SetActive(false);
        }
    }

    public T GetPopup<T>(PopupName name) where T : Popup
    {
        if (_popups.TryGetValue(name, out var popup) && popup is T typed) return typed;
        Debug.LogError($"[Manager_PopUp] No {typeof(T).Name} named {name}.");
        return null;
    }

    public void ShowPopup(PopupName name, Action onClose = null)
    {
        if (!_popups.TryGetValue(name, out var popup))
        {
            Debug.LogError($"[Manager_PopUp] No popup {name}.");
            return;
        }
        foreach (var other in _popups.Values)
            if (other != popup && other.IsOpen) Close(other);

        popup.Show(onClose);
        OnPopupShown(name);
    }

    public void ClosePopup(PopupName name)
    {
        if (_popups.TryGetValue(name, out var popup) && popup.IsOpen) Close(popup);
    }

    public void CloseAll()
    {
        foreach (var popup in _popups.Values)
            if (popup.IsOpen) Close(popup);
    }

    // Listeners re-read IsPopUpActive on OnPopupCloseFinished (another popup may be open by then).
    private void Close(Popup popup)
    {
        OnPopupClosed(popup.Name);
        popup.Close(() => OnPopupCloseFinished());
    }
}
