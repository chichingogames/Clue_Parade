using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

// Play-mode checks for the Clue Parade board, driven from the CLI while the game runs:
//   unity command script run --file AgentScripts/PlayCheck.cs --entry PlayCheck.Status
//   unity command script run --file AgentScripts/PlayCheck.cs --entry PlayCheck.Swipe --args '[1, 0, 1, 0]'
public static class PlayCheck
{
    // Level, state, step, moves, and each row's items with [ok] for tiles at home.
    public static string Status()
    {
        var game = Manager_Game.Instance;
        var board = Manager_Board.Instance;
        if (game == null || game.Level == null || board.State == null) return "no level yet";

        var level = game.Level;
        var state = board.State;
        var text = new System.Text.StringBuilder();
        text.Append($"level {game.CurrentLevel} '{level.Name}' {game.State} step {Mathf.Min(state.ActiveStep + 1, state.StepCount)}/{state.StepCount} " +
                    $"activeRow {state.ActiveRow} moves {game.MovesUsed}/{level.MoveLimit} animating {board.IsAnimating}");
        for (int r = 0; r < state.Size; r++)
        {
            text.Append($" | {(state.IsRowConfirmed(r) ? "*" : r == state.ActiveRow ? ">" : " ")}{level.Categories[r]}:");
            for (int c = 0; c < state.Size; c++)
                text.Append(state.IsHome(r, c) ? " [ok]" : " [  ]").Append(FileRef.Instance.GetItem(level.ItemFor(state[r, c])).Name);
        }
        return text.ToString();
    }

    // A real swipe through Manager_Game's input handlers, from the centre of cell (row, col) by
    // (dx, dy) cells (x right, y up), at the cell size on screen. Waits for the slide to settle.
    public static async Task<string> Swipe(int row, int col, float dx, float dy)
    {
        var game = Manager_Game.Instance;
        var board = Manager_Board.Instance;
        var cam = Camera.main;
        Vector2 Screen(float r, float c) => cam.WorldToScreenPoint(board.BoardRoot.TransformPoint(board.CellPosition(r, c)));

        Vector2 start = Screen(row, col);
        Vector2 end = Screen(row - dy, col + dx);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(Manager_Game);
        type.GetMethod("HandlePress", flags).Invoke(game, new object[] { start });
        for (int i = 1; i <= 5; i++)
            type.GetMethod("HandleDrag", flags).Invoke(game, new object[] { start, Vector2.Lerp(start, end, i / 5f) });
        type.GetMethod("HandleRelease", flags).Invoke(game, new object[] { start, end, 0.6f });

        for (int i = 0; i < 200 && board.IsAnimating; i++) await Task.Delay(10);
        return $"swipe from ({start.x:0},{start.y:0}) to ({end.x:0},{end.y:0}) -> " + Status();
    }

    // Press and drag without releasing (to capture a line mid-wrap); Release() lets go where it is.
    public static string Hold(int row, int col, float dx, float dy)
    {
        var game = Manager_Game.Instance;
        var board = Manager_Board.Instance;
        var cam = Camera.main;
        Vector2 Screen(float r, float c) => cam.WorldToScreenPoint(board.BoardRoot.TransformPoint(board.CellPosition(r, c)));
        _start = Screen(row, col);
        _end = Screen(row - dy, col + dx);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(Manager_Game).GetMethod("HandlePress", flags).Invoke(game, new object[] { _start });
        typeof(Manager_Game).GetMethod("HandleDrag", flags).Invoke(game, new object[] { _start, _end });
        return $"holding, offset {board.DragOffset:0.00}";
    }

    public static async Task<string> Release()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(Manager_Game).GetMethod("HandleRelease", flags).Invoke(Manager_Game.Instance, new object[] { _start, _end, 0.6f });
        for (int i = 0; i < 200 && Manager_Board.Instance.IsAnimating; i++) await Task.Delay(10);
        return "released -> " + Status();
    }

    private static Vector2 _start, _end;

    public static string Undo() => $"undo {Manager_Game.Instance.Undo()} -> " + Status();
}
