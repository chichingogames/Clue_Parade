using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "Out of moves" (your deliveries are still earned): how many sets are delivered, the one continue
// per attempt ("+5 moves · free", or · 80 coins after the teaching cases; hidden once used), Retry
// (same start, free), Undo (free, returns the move) and Choose a case.
public class PopUp_Failed : BasePopUp
{
    [SerializeField] private Button _continueButton;
    [SerializeField] private TMP_Text _continueLabel;
    [SerializeField] private TMP_Text _caption;
    [SerializeField] private Button _undoButton;
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _levelsButton;

    // onContinue null hides the continue (already used).
    public void Setup(string body, string continueLabel, bool canContinue, string caption, Action onContinue,
                      bool canUndo, Action onUndo, Action onRetry, Action onLevels)
    {
        SetTexts("YOUR DELIVERIES ARE STILL EARNED", "Out of moves", body);
        Bind(_continueButton, _continueLabel, continueLabel, onContinue);
        _continueButton.interactable = canContinue;
        _caption.text = caption ?? "";
        _caption.gameObject.SetActive(!string.IsNullOrEmpty(caption));
        Bind(_undoButton, onUndo);
        _undoButton.interactable = canUndo;
        Bind(_retryButton, onRetry);
        Bind(_levelsButton, onLevels);
    }
}
