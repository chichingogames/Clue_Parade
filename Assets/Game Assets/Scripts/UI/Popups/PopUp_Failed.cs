using System;
using UnityEngine;
using UnityEngine.UI;

// "Out of moves": how many items are matched, then Retry (same start), Undo (free, returns the
// move) and Choose a case. No continue purchase; nothing is lost while an ad is unavailable.
public class PopUp_Failed : BasePopUp
{
    [SerializeField] private Button _undoButton;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _levelsButton;

    public void Setup(string body, Action onUndo, Action onRetry, Action onLevels)
    {
        SetTexts("", "Out of moves", body);
        Bind(_undoButton, onUndo);
        Bind(_retryButton, onRetry);
        Bind(_levelsButton, onLevels);
    }
}
