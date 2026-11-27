using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Play mode checks for the onboarding flow (Manager_Flow + popups). Use with PlayCheck.Swipe.
//   Status()            level, state, moves, matched, open popup, coach, required move, save
//   Tap("Win/Next Button")  invokes a popup button: PopUp_{name}/Holder/{button}
//   Hud("Hint Button")  invokes a HUD button under InGame/Reference
public static class FlowCheck
{
    public static string Status()
    {
        var game = Manager_Game.Instance;
        var board = Manager_Board.Instance.State;
        var flow = Manager_Flow.Instance;
        var open = System.Enum.GetValues(typeof(PopupName)).Cast<PopupName>()
            .Where(n => Manager_PopUp.Instance.IsOpen(n)).Select(n => n.ToString());
        var save = OfflineData.Instance;
        var required = game.RequiredMove;
        var guide = GameObject.Find("/GamePlay/Board/Guide");
        return $"level {game.CurrentLevel} '{game.Level?.Name}' {game.State} moves {game.MovesUsed}/{game.Level?.MoveLimit} " +
               $"matched {board?.MatchedCount}/9 popups [{string.Join(",", open)}] paused {game.IsPaused} input {Manager_Input.Instance.AcceptInput} " +
               $"coach \"{flow.Coach}\" required {(required == null ? "-" : required.Describe(3))} guide {(guide != null && guide.activeSelf)} " +
               $"hintPending {flow.HintPending} | save completed [{string.Join(",", save.CompletedLevels)}] coins {save.Coins} lessons [{string.Join(",", save.Lessons)}] " +
               $"feedback {save.FeedbackGoal}/{save.FeedbackWrap}/{save.FeedbackDifficulty} events {PlaytestLog.Events.Count}";
    }

    public static string Tap(string popupAndButton)
    {
        var parts = popupAndButton.Split('/');
        var root = GameObject.Find($"/Canvas/Container/PopUps/PopUp_{parts[0]}");
        if (root == null) return $"no open popup {parts[0]}";
        var t = root.transform.Find("Holder/" + string.Join("/", parts.Skip(1)));
        if (t == null)
        {
            // Rows of the level list / feedback live deeper; search by name.
            t = root.GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == parts.Last());
        }
        if (t == null) return $"no button {popupAndButton}";
        t.GetComponent<Button>().onClick.Invoke();
        return $"tapped {popupAndButton} -> " + Status();
    }

    public static string Hud(string button)
    {
        var t = GameObject.Find($"/Canvas/Container/InGame/Reference/{button}");
        if (t == null) return $"no HUD button {button}";
        var b = t.GetComponent<Button>();
        if (!b.interactable) return $"{button} is not interactable";
        b.onClick.Invoke();
        return $"tapped {button} -> " + Status();
    }

    public static string Toast() =>
        GameObject.Find("/Canvas/Container/Overlay/Toast/Text").GetComponent<TMPro.TMP_Text>().text +
        " alpha " + GameObject.Find("/Canvas/Container/Overlay/Toast").GetComponent<CanvasGroup>().alpha.ToString("0.00");
}
