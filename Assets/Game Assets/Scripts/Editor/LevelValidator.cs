using Chichingo.TilewrapVaried;
using UnityEditor;
using UnityEngine;

// Checks every case in FileRef's content library: it converts (structure, bindings), every item
// sprite and subject rig exists, it doesn't start solved, its reference solution solves it within
// the move limit, the hint table agrees with the stated minimum, and the cases are numbered 1..N
// with nextLevelId leading to the next one (0 only on the last).
public static class LevelValidator
{
    [MenuItem("ShiftSort/Validate Levels")]
    public static string Validate()
    {
        var content = FileRef.Instance.Content;
        if (content == null || content.levels == null)
        {
            Debug.LogError("[LevelValidator] FileRef has no content library. Run ShiftSort > Build Assets.");
            return "no content";
        }

        int ok = 0, failed = 0;
        var source = new CaseLevelSource(FileRef.Instance);
        if (source.Count != content.levels.Length)
            Debug.LogWarning($"[LevelValidator] {content.levels.Length} case files, but only 1..{source.Count} are numbered without a gap.");

        foreach (var file in content.levels)
        {
            string error = Check(file, source.Count);
            if (error == null) ok++;
            else
            {
                failed++;
                Debug.LogError($"[LevelValidator] {AssetDatabase.GetAssetPath(file)}: {error}", file);
            }
        }

        string summary = $"[LevelValidator] {ok} case(s) OK, {failed} failed.";
        if (failed > 0) Debug.LogError(summary);
        else Debug.Log(summary);
        return summary;
    }

    private static string Check(TextAsset file, int count)
    {
        if (!CaseLevelSource.TryConvert(file, out var level, out string error)) return error;
        if (!CaseLevelSource.TryCheckArt(level, FileRef.Instance, out error)) return error;

        bool last = level.Number == count;
        if (last ? level.NextLevelId != 0 : level.NextLevelId != level.Number + 1)
            return $"nextLevelId {level.NextLevelId} (expected {(last ? 0 : level.Number + 1)})";

        var board = new BoardState(level);
        if (board.IsSolved) return "the board starts solved";
        if (level.Solution.Length == 0) return "no solution to verify";
        if (level.HasMoveLimit && level.Solution.Length > level.MoveLimit)
            return $"solution takes {level.Solution.Length} moves, the limit is {level.MoveLimit}";

        foreach (var move in level.Solution)
        {
            if (move == null || move.line < 0 || move.line >= level.Size || (move.direction != 1 && move.direction != -1))
                return "bad solution move";
            if (move.IsRow) board.ShiftRow(move.line, move.direction);
            else board.ShiftColumn(move.line, move.direction);
        }
        if (!board.IsSolved) return "the solution does not solve the board";

        // The hint table's distance from the start board is the true minimum.
        var start = new BoardState(level);
        int distance = Distance(start, FileRef.Instance.HintDistances);
        if (distance >= 0 && distance != level.MinimumMoves)
            return $"minimumMoves is {level.MinimumMoves}, the hint table says {distance}";
        return null;
    }

    // Swipes of a shortest solve, or -1 without the table.
    private static int Distance(BoardState state, byte[] distances)
    {
        if (distances == null) return -1;
        var board = new string[state.CellCount];
        var goal = new string[state.CellCount];
        int[] ids = state.TileIds();
        for (int i = 0; i < ids.Length; i++) { board[i] = ids[i].ToString(); goal[i] = i.ToString(); }
        int steps = 0;
        while (TilewrapWrapPuzzle.TryNextMove(board, goal, distances, out var move))
        {
            board = TilewrapWrapPuzzle.ApplyMove(board, move);
            if (++steps > 40) return -1;
        }
        return steps;
    }
}
