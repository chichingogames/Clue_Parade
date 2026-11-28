using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

// Play-mode checks for the any-row board, driven from the CLI while the game runs:
//   unity command script run --file AgentScripts/PlayCheck.cs --entry PlayCheck.Status
//   unity command script run --file AgentScripts/PlayCheck.cs --entry PlayCheck.Swipe --args '[0, 1, 1, 0]'
public static class PlayCheck
{
    // Case, state, moves, focus, sets delivered, the board by rows and what each subject shows.
    public static string Status()
    {
        var game = Manager_Game.Instance;
        if (game == null || game.Level == null || game.Model == null) return "no level yet";
        var level = game.Level;
        var model = game.Model;
        var text = new System.Text.StringBuilder();
        text.Append($"case {game.CurrentLevel} '{level.title}' {game.State} moves {game.MovesUsed}/{level.moveLimit} " +
                    $"focus {game.FocusedSet?.label ?? "-"} delivered {model.Stage}/{level.sets.Length} busy {game.Busy} selected {game.Selected}");
        for (int r = 0; r < level.rows; r++)
        {
            text.Append(" |");
            for (int c = 0; c < 3; c++) text.Append(' ').Append(level.Item(model.Board[r * 3 + c])?.label ?? "-");
        }
        text.Append(" | shown parts:");
        foreach (var card in Object.FindObjectsByType<UI_SubjectCard>(FindObjectsSortMode.None))
        {
            if (card.transform.parent.name != "Reference") continue;
            var rig = card.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == "Rig");
            int on = rig == null ? 0 : rig.GetComponentsInChildren<UnityEngine.UI.Image>(false).Length;
            text.Append($" {card.name}={on}");
        }
        return text.ToString();
    }

    // A real drag through Manager_Game's input handlers, from the centre of cell (row, col) by
    // (dx, dy) cells (x right, y up): (1, 0) swaps with the right neighbour. Waits for the move.
    public static async Task<string> Swipe(int row, int col, float dx, float dy)
    {
        var (start, end) = Points(row, col, dx, dy);
        Invoke("HandlePress", start);
        for (int i = 1; i <= 5; i++) Invoke("HandleDrag", start, Vector2.Lerp(start, end, i / 5f));
        Invoke("HandleRelease", start, end, 0.6f);
        await Settle();
        return $"swipe ({row},{col}) by ({dx},{dy}) -> " + Status();
    }

    // A tap on cell (row, col) (select, then tap a neighbour to swap).
    public static async Task<string> Tap(int row, int col)
    {
        var (start, _) = Points(row, col, 0, 0);
        Invoke("HandlePress", start);
        Invoke("HandleRelease", start, start, 0.1f);
        await Settle();
        return $"tap ({row},{col}) -> " + Status();
    }

    // Opens case n fresh (Tester-style), waiting for the board to settle.
    public static async Task<string> Load(int n)
    {
        Manager_Flow.Instance.StartLevel(n);
        await Settle();
        await Task.Delay(1500);
        return Status();
    }

    // Follows the hint for up to `count` swaps (or until the case ends).
    public static async Task<string> Hint(int count)
    {
        for (int i = 0; i < count && Manager_Game.Instance.State == GameState.Playing; i++)
        {
            var game = Manager_Game.Instance;
            var h = DeliveryRules.Hint(game.Model, game.Level, game.FocusSet);
            if (h == null) break;
            if (!game.ApplyMove(h.From, h.To)) return "move refused -> " + Status();
            await Settle();
        }
        return Status();
    }

    // Swaps cells a and b back and forth n times (spends moves without progress).
    public static async Task<string> Waste(int n, int a, int b)
    {
        for (int i = 0; i < n && Manager_Game.Instance.State == GameState.Playing; i++)
        {
            Manager_Game.Instance.ApplyMove(a, b);
            await Settle();
        }
        await Task.Delay(1200);
        return Status();
    }

    public static string Undo() => $"undo {Manager_Game.Instance.Undo()} -> " + Status();

    private static (Vector2, Vector2) Points(int row, int col, float dx, float dy)
    {
        var board = Manager_Board.Instance;
        var cam = Camera.main;
        Vector2 Screen(float r, float c) => cam.WorldToScreenPoint(board.BoardRoot.TransformPoint(board.CellPosition(r, c)));
        return (Screen(row, col), Screen(row - dy, col + dx));
    }

    private static void Invoke(string method, params object[] args) =>
        typeof(Manager_Game).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Manager_Game.Instance, args);

    private static async Task Settle()
    {
        await Task.Delay(50);
        for (int i = 0; i < 400 && Manager_Game.Instance.Busy; i++) await Task.Delay(10);
    }
}
