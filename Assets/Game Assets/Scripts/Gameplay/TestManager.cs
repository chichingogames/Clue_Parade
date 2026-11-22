using Sortify;
using UnityEngine;

/// <summary>
/// A cheat panel for testing: jump between levels, force a win or a loss, make moves and play
/// a level's known solution, all from Inspector buttons.
/// </summary>
/// <remarks>
/// <b>Everything in here is editor-only.</b> The class still compiles into a player as an empty
/// <see cref="MonoBehaviour"/>, so the scene never reports a missing script. Its GameObject
/// (<c>Tester</c>, made by ShiftSort > Build Scene) is tagged <c>EditorOnly</c>, so Unity strips
/// it from builds anyway.
/// <para>
/// Not a <c>Singleton&lt;T&gt;</c> and not in <c>SingletonManager</c>: nothing in the game may call it.
/// </para>
/// <para>
/// Buttons come from <see cref="Sortify.ButtonAttribute"/>, which only binds methods with no
/// parameters, so anything needing an argument reads a serialized field above the buttons.
/// </para>
/// </remarks>
[DisallowMultipleComponent]
public class TestManager : MonoBehaviour
{
#if UNITY_EDITOR

    #region Inspector

    [Header("Level")]
    [Tooltip("Level that 'Load Level' opens. Counts from 1; level 1 is Resources/Levels/level-001.json.")]
    [SerializeField, Min(1)] private int _levelNumber = 1;

    [Header("Move")]
    [Tooltip("On: 'Shift Line' moves a row. Off: a column.")]
    [SerializeField] private bool _shiftRow = true;
    [Tooltip("Row or column index. 0 is the top row / left column.")]
    [SerializeField, Min(0)] private int _lineIndex;
    [Tooltip("Direction: + is right (row) or down (column), - is left / up. A move is always one cell.")]
    [SerializeField] private int _steps = 1;

    [Header("Time")]
    [Tooltip("Speed the game runs at. 0 freezes it, 1 is normal. Slides and the win/fail delay are DOTween on scaled time, so both follow this.")]
    [SerializeField, Range(0f, 4f)] private float _timeScale = 1f;

    #endregion

    #region Level

    [Button("Load Level", header: "Level")]
    private void LoadLevel()
    {
        if (!Ready(out Manager_Game game)) return;
        StopSolution();
        game.StartLevel(_levelNumber);
    }

    [Button("Next Level")]
    private void NextLevel()
    {
        if (!Ready(out Manager_Game game)) return;
        StopSolution();
        game.StartLevel(game.CurrentLevel + 1);
    }

    [Button("Previous Level")]
    private void PreviousLevel()
    {
        if (!Ready(out Manager_Game game)) return;
        StopSolution();
        game.StartLevel(Mathf.Max(1, game.CurrentLevel - 1));
    }

    /// <summary>
    /// Deletes the save (level progress and the sound setting), so the next Play starts at level 1
    /// with sound on. Works outside Play mode. Load / Next / Previous never touch the save; only
    /// winning a level does.
    /// </summary>
    [Button("Clear Saved Progress")]
    private void ClearSavedProgress()
    {
        OfflineData.Clear();
        Debug.Log("[TestManager] save cleared: the next Play starts at level 1 with sound on.");
    }

    [Button("Restart Level")]
    private void RestartLevel()
    {
        if (!Ready(out Manager_Game game)) return;
        StopSolution();
        game.RestartLevel();
    }

    #endregion

    #region Outcome

    /// <summary>
    /// Jumps straight to the won state without touching the board. Use
    /// <see cref="PlaySolution"/> to win the way a player does.
    /// </summary>
    [Button("Force Win", header: "Outcome")]
    private void ForceWin()
    {
        if (!Ready(out Manager_Game game)) return;
        StopSolution();
        game.DebugEndLevel(won: true);
        Debug.Log("[TestManager] forced WON.");
    }

    [Button("Force Fail")]
    private void ForceFail()
    {
        if (!Ready(out Manager_Game game)) return;
        StopSolution();
        game.DebugEndLevel(won: false);
        Debug.Log("[TestManager] forced FAILED.");
    }

    /// <summary>
    /// Restarts the level and plays its authored solution one move at a time, through the same
    /// path as a swipe: moves are counted and the win lands on the last one.
    /// </summary>
    /// <remarks>
    /// Each move waits for <see cref="Manager_Board.OnBoardSettled"/> from the one before, since
    /// <see cref="Manager_Game.ApplyMove"/> refuses a move while a line is still sliding.
    /// </remarks>
    [Button("Play Solution (real win)")]
    private void PlaySolution()
    {
        if (!Ready(out Manager_Game game)) return;
        StopSolution();
        game.RestartLevel();

        var solution = game.Level.Solution;
        if (solution.Length == 0)
        {
            Debug.LogWarning($"[TestManager] level {game.CurrentLevel} has no solution in its file.");
            return;
        }

        _solution = solution;
        _solutionStep = 0;
        Manager_Board.Instance.OnBoardSettled += NextSolutionMove;
        Debug.Log($"[TestManager] playing the {solution.Length}-move solution of level {game.CurrentLevel}.");
        NextSolutionMove();
    }

    #endregion

    #region Moves

    [Button("Shift Line", header: "Moves")]
    private void ShiftLine()
    {
        if (!Ready(out Manager_Game game)) return;

        int size = game.Level.Size;
        if (_lineIndex >= size || _steps == 0)
        {
            Debug.LogWarning($"[TestManager] need a line index below {size} and non-zero steps.");
            return;
        }

        if (!game.ApplyMove(_shiftRow, _lineIndex, _steps))
            Debug.LogWarning($"[TestManager] move refused: state is {game.State}, or a line is still moving.");
    }

    [Button("Log State")]
    private void LogState()
    {
        if (!Ready(out Manager_Game game)) return;

        var level = game.Level;
        var board = Manager_Board.Instance.State;
        string moves = level.HasMoveLimit ? $"{game.MovesUsed}/{level.MoveLimit} moves" : $"{game.MovesUsed} moves (unlimited)";

        // One line per row: [ok] = the tile is home, * = confirmed row, > = the active step's row.
        var rows = new System.Text.StringBuilder();
        for (int r = 0; r < board.Size; r++)
        {
            string mark = board.IsRowConfirmed(r) ? "*" : r == board.ActiveRow ? ">" : " ";
            rows.Append($"\n {mark} {level.Categories[r],-8}");
            for (int c = 0; c < board.Size; c++)
                rows.Append(board.IsHome(r, c) ? "[ok] " : "[  ] ").Append(level.ItemFor(board[r, c])).Append("   ");
        }

        var save = OfflineData.Instance;
        Debug.Log($"[TestManager] save: current {save.CurrentLevel}, passed {save.PassedLevels}, sound {(save.IsSoundOn ? "on" : "off")}");
        Debug.Log($"[TestManager] level {game.CurrentLevel} (file {level.Number}, \"{level.Name}\") | {game.State} | " +
                  $"step {Mathf.Min(board.ActiveStep + 1, board.StepCount)}/{board.StepCount} | {moves}{rows}");
    }

    #endregion

    #region Time

    [Button("Apply Time Scale", header: "Time")]
    private void ApplyTimeScale() => SetTimeScale(_timeScale);

    [Button("Slow (0.25x)")]
    private void SlowMotion() => SetTimeScale(0.25f);

    [Button("Normal (1x)")]
    private void NormalSpeed() => SetTimeScale(1f);

    [Button("Fast (2x)")]
    private void FastForward() => SetTimeScale(2f);

    // Unity resets Time.timeScale to 1 when Play mode ends, so nothing here outlives the session.
    private void SetTimeScale(float scale)
    {
        scale = Mathf.Max(0f, scale);
        Time.timeScale = scale;
        _timeScale = scale;             // keep the Inspector honest
        Debug.Log($"[TestManager] time scale = {scale:0.###}{(scale <= 0f ? "  (paused)" : string.Empty)}");
    }

    #endregion

    #region Tools

    /// <summary>
    /// Logs every sound effect and haptic pulse as it plays: "[Audio] TileMatch -> tile_match_02",
    /// "[Haptics] Light". Haptics do nothing in the Editor, so this is the way to check them here.
    /// </summary>
    [Button("Log Sounds & Haptics: On / Off", header: "Tools")]
    private void ToggleSoundLog()
    {
        bool on = !Manager_Audio.DebugLogSounds;
        Manager_Audio.DebugLogSounds = on;
        Haptics.DebugLog = on;
        Debug.Log($"[TestManager] sound + haptics log {(on ? "ON" : "OFF")}.");
    }

    /// <summary>Runs ShiftSort > Validate Levels. Works outside Play mode.</summary>
    [Button("Validate Level Files")]
    private void ValidateLevels() => UnityEditor.EditorApplication.ExecuteMenuItem("ShiftSort/Validate Levels");

    #endregion

    #region Private

    private LevelMove[] _solution;
    private int _solutionStep;

    private void NextSolutionMove()
    {
        var game = Manager_Game.Instance;
        if (_solution == null || game == null || game.State != GameState.Playing || _solutionStep >= _solution.Length)
        {
            StopSolution();
            return;
        }

        var move = _solution[_solutionStep++];
        if (!game.ApplyMove(move.IsRow, move.line, move.direction))
        {
            Debug.LogWarning($"[TestManager] solution stopped at move {_solutionStep}: the board was busy.");
            StopSolution();
        }
    }

    private void StopSolution()
    {
        _solution = null;
        if (Manager_Board.Instance != null) Manager_Board.Instance.OnBoardSettled -= NextSolutionMove;
    }

    private void OnDisable() => StopSolution();

    /// <summary>
    /// Whether the game is running and has a level. Every Play-mode button goes through this,
    /// so a press outside Play mode says so instead of throwing a NullReference.
    /// </summary>
    private static bool Ready(out Manager_Game game)
    {
        game = null;

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[TestManager] enter Play mode first; the managers do not exist until then.");
            return false;
        }

        game = Manager_Game.Instance;
        if (game == null || Manager_Board.Instance == null || game.Level == null)
        {
            Debug.LogWarning("[TestManager] the gameplay managers are not initialised yet.");
            game = null;
            return false;
        }

        return true;
    }

    #endregion

#endif
}
