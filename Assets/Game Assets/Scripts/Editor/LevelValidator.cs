using UnityEditor;
using UnityEngine;

// Checks every case in FileRef's content library: it converts (structure, bindings), every item
// sprite and subject rig exists, it doesn't start solved, its swap minimum (SwapPuzzle) fits the
// move limit, and the cases are numbered 1..N with nextLevelId leading to the next one (0 only on
// the last). The pack's own solution and minimum are for row / column wraps and are not checked.
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
        int distance = SwapPuzzle.Distance(board.TileIds());
        if (distance != level.MinimumMoves) return $"minimum {level.MinimumMoves} swaps, the solver says {distance}";
        if (level.HasMoveLimit && level.MoveLimit < level.MinimumMoves)
            return $"move limit {level.MoveLimit} is below the {level.MinimumMoves}-swap minimum";
        return null;
    }
}
