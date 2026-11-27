using System;
using UnityEngine;
using UnityEngine.UI;

// "Out of moves": how many items are matched, then Undo last move (free), Try again (same start)
// and Choose a level. No continue purchase.
public class PopUp_Failed : BasePopUp
{
    [SerializeField] private Button _undoButton;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _levelsButton;

    public void Setup(string body, Action onUndo, Action onRetry, Action onLevels)
    {
        SetTexts("TAKE ANOTHER LOOK", "Out of moves", body);
        Bind(_undoButton, onUndo);
        Bind(_retryButton, onRetry);
        Bind(_levelsButton, onLevels);
    }
}
