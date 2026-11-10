using System;
using UnityEngine;

// Loads authored levels from Resources/Levels/level-NNN.json (level 1 = level-001.json).
//
// File format (schemaVersion 1):
//   size        board side N (targets are the first N of RowShapes / ColumnColors)
//   board       N*N tile ids, row-major from the top-left. A tile's id is the cell it belongs in
//               when solved: id = row * N + col, so its shape is RowShapes[id / N] and its color
//               ColumnColors[id % N].
//   moveBudget  0 = unlimited
//   solution    reference moves (axis "row"/"column", line, direction +1 = right/down), checked
//               by ShiftSort > Validate Levels and replayed by the tester.
public class JsonLevelSource : ILevelSource
{
    public const string ResourcesFolder = "Levels";
    public const int SchemaVersion = 1;

    // The fixed target order the level files are authored against. Append-only.
    public static readonly TileShape[] RowShapes = { TileShape.Circle, TileShape.Square, TileShape.Triangle, TileShape.Star };
    public static readonly TileColor[] ColumnColors = { TileColor.Coral, TileColor.Teal, TileColor.Yellow, TileColor.Blue };

    private readonly TileShape[] _playableShapes;
    private readonly TileColor[] _playableColors;

    // Levels that need a shape or color without art are rejected.
    public JsonLevelSource(TileShape[] playableShapes, TileColor[] playableColors)
    {
        _playableShapes = playableShapes;
        _playableColors = playableColors;
    }

    public static string ResourcePath(int levelNumber) => $"{ResourcesFolder}/level-{levelNumber:000}";

    public LevelDefinition Load(int levelNumber)
    {
        var text = Resources.Load<TextAsset>(ResourcePath(levelNumber));
        if (text == null) return null;

        string json = text.text;
        Resources.UnloadAsset(text);

        if (!TryParse(json, levelNumber, out var level, out _, out string error))
        {
            Debug.LogError($"[JsonLevelSource] {ResourcePath(levelNumber)}.json: {error}");
            return null;
        }

        for (int i = 0; i < level.Size; i++)
        {
            if (Array.IndexOf(_playableShapes, level.RowTargets[i]) < 0 || Array.IndexOf(_playableColors, level.ColumnTargets[i]) < 0)
            {
                Debug.LogError($"[JsonLevelSource] {ResourcePath(levelNumber)}.json: needs {level.RowTargets[i]} / " +
                               $"{level.ColumnTargets[i]}, which have no art in FileRef.");
                return null;
            }
        }

        return level;
    }

    public static bool TryParse(string json, int levelNumber, out LevelDefinition level, out LevelFile file, out string error)
    {
        level = null;
        error = null;

        try { file = JsonUtility.FromJson<LevelFile>(json); }
        catch (Exception e)
        {
            file = null;
            error = $"invalid JSON ({e.Message})";
            return false;
        }

        if (file == null) { error = "empty file"; return false; }
        if (file.schemaVersion != SchemaVersion) { error = $"schemaVersion {file.schemaVersion}, expected {SchemaVersion}"; return false; }
        if (file.id != levelNumber) { error = $"id {file.id} does not match the file name (level {levelNumber})"; return false; }

        int n = file.size;
        int maxSize = Mathf.Min(RowShapes.Length, ColumnColors.Length);
        if (n < 2 || n > maxSize) { error = $"size {n} is outside 2..{maxSize}"; return false; }
        if (file.board == null || file.board.Length != n * n) { error = $"board has {file.board?.Length ?? 0} tiles, expected {n * n}"; return false; }
        if (file.moveBudget < 0) { error = $"moveBudget {file.moveBudget} is negative"; return false; }

        var seen = new bool[n * n];
        var cells = new TileData[n, n];
        for (int i = 0; i < file.board.Length; i++)
        {
            int id = file.board[i];
            if (id < 0 || id >= n * n || seen[id]) { error = $"board must hold each id 0..{n * n - 1} once (bad id {id} at index {i})"; return false; }
            seen[id] = true;
            cells[i / n, i % n] = new TileData(RowShapes[id / n], ColumnColors[id % n]);
        }

        var rowTargets = new TileShape[n];
        var columnTargets = new TileColor[n];
        Array.Copy(RowShapes, rowTargets, n);
        Array.Copy(ColumnColors, columnTargets, n);

        level = new LevelDefinition(levelNumber, n, rowTargets, columnTargets, cells, file.moveBudget,
                                    file.name, file.instruction, file.tutorial, file.solution);
        return true;
    }
}

// Mirrors the JSON (JsonUtility needs public fields with the exact key names).
[Serializable]
public class LevelFile
{
    public int schemaVersion;
    public int id;
    public string name;
    public string instruction;
    public int size;
    public int[] board;
    public int moveBudget;
    public string tutorial;
    public LevelMove[] solution;
}

[Serializable]
public class LevelMove
{
    public string axis;        // "row" or "column"
    public int line;
    public int direction;      // +1 = right / down, -1 = left / up

    public bool IsRow => axis == "row";
}
