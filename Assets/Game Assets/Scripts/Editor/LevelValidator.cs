using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Checks every Resources/Levels/level-NNN.json: it parses, it isn't already solved, its reference
// solution solves it, and the solution fits the move budget. Also reports gaps in the numbering.
public static class LevelValidator
{
    private const string LevelsFolder = "Assets/Resources/" + JsonLevelSource.ResourcesFolder;
    private static readonly Regex FileName = new Regex(@"^level-(\d+)$");

    [MenuItem("ShiftSort/Validate Levels")]
    public static string Validate()
    {
        int ok = 0, failed = 0, highest = 0;
        var found = new System.Collections.Generic.HashSet<int>();

        foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { LevelsFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var match = FileName.Match(Path.GetFileNameWithoutExtension(path));
            if (!match.Success || Path.GetExtension(path) != ".json") continue;

            int number = int.Parse(match.Groups[1].Value);
            found.Add(number);
            highest = Mathf.Max(highest, number);

            string error = Check(File.ReadAllText(path), number);
            if (error == null) ok++;
            else
            {
                failed++;
                Debug.LogError($"[LevelValidator] {path}: {error}", AssetDatabase.LoadAssetAtPath<TextAsset>(path));
            }
        }

        for (int n = 1; n < highest; n++)
            if (!found.Contains(n)) Debug.LogWarning($"[LevelValidator] No file for level {n}; it will be generated.");

        string summary = $"[LevelValidator] {ok} level(s) OK, {failed} failed.";
        if (failed > 0) Debug.LogError(summary);
        else Debug.Log(summary);
        return summary;
    }

    private static string Check(string json, int number)
    {
        if (!JsonLevelSource.TryParse(json, number, out var level, out var file, out string error)) return error;

        var board = new BoardState(level);
        if (board.IsSolved) return "the board starts solved";
        if (file.solution == null || file.solution.Length == 0) return "no solution to verify";
        if (level.HasMoveLimit && file.solution.Length > level.MoveLimit)
            return $"solution takes {file.solution.Length} moves, budget is {level.MoveLimit}";

        foreach (var move in file.solution)
        {
            if (move.line < 0 || move.line >= level.Size || (move.direction != 1 && move.direction != -1))
                return $"bad solution move ({move.axis} {move.line} {move.direction})";

            if (move.axis == "row") board.ShiftRow(move.line, move.direction);
            else if (move.axis == "column") board.ShiftColumn(move.line, move.direction);
            else return $"bad solution axis '{move.axis}'";
        }

        return board.IsSolved ? null : "the solution does not solve the board";
    }
}
