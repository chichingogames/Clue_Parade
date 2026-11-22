using System;
using System.Collections.Generic;
using UnityEngine;

// Loads authored levels from Resources/Levels/level-NNN.json (level 1 = level-001.json).
//
// File format (schemaVersion 2, Clue Parade):
//   characters  N character ids, one per column, left to right ("mia")
//   categories  N row labels, top to bottom ("Hats")
//   items       N*N item ids, row-major: the item whose home is that cell (the solved board)
//   board       N*N tile ids, row-major from the top-left: the start board. A tile's id is the cell it
//               belongs in when solved (id = row * N + col), so the solved board is 0, 1, 2, ...
//   steps       one per row, in play order: { row, goal, clues: [ { item, text } ] }. Clue text may
//               use TMP rich text (<color=#ED3937>red</color>).
//   moveBudget  0 = unlimited
//   solution    reference moves (axis "row"/"column", line, direction +1 = right/down), checked
//               by ShiftSort > Validate Levels and replayed by the tester.
public class JsonLevelSource : ILevelSource
{
    public const string ResourcesFolder = "Levels";
    public const int SchemaVersion = 2;
    public const int MinSize = 2;
    public const int MaxSize = 4;

    private readonly FileRef _fileRef;

    // Levels naming an item or character that FileRef doesn't have are rejected.
    public JsonLevelSource(FileRef fileRef) => _fileRef = fileRef;

    public static string ResourcePath(int levelNumber) => $"{ResourcesFolder}/level-{levelNumber:000}";

    public static bool Exists(int levelNumber) => Resources.Load<TextAsset>(ResourcePath(levelNumber)) != null;

    public LevelDefinition Load(int levelNumber)
    {
        var text = Resources.Load<TextAsset>(ResourcePath(levelNumber));
        if (text == null) return null;

        string json = text.text;
        Resources.UnloadAsset(text);

        if (!TryParse(json, levelNumber, out var level, out _, out string error)
            || !TryCheckArt(level, _fileRef, out error))
        {
            Debug.LogError($"[JsonLevelSource] {ResourcePath(levelNumber)}.json: {error}");
            return null;
        }
        return level;
    }

    // Every item and character the level names has art in FileRef.
    public static bool TryCheckArt(LevelDefinition level, FileRef fileRef, out string error)
    {
        error = null;
        foreach (string item in level.Items)
            if (!fileRef.TryGetItem(item, out _)) { error = $"item '{item}' is not in FileRef"; return false; }
        foreach (string character in level.Characters)
            if (!fileRef.TryGetCharacter(character, out _)) { error = $"character '{character}' is not in FileRef"; return false; }
        return true;
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

        int n = file.characters?.Length ?? 0;
        if (n < MinSize || n > MaxSize) { error = $"{n} characters, expected {MinSize}..{MaxSize}"; return false; }
        if (file.categories == null || file.categories.Length != n) { error = $"{file.categories?.Length ?? 0} categories, expected {n} (one per row)"; return false; }
        if (file.items == null || file.items.Length != n * n) { error = $"{file.items?.Length ?? 0} items, expected {n * n}"; return false; }
        if (file.board == null || file.board.Length != n * n) { error = $"board has {file.board?.Length ?? 0} tiles, expected {n * n}"; return false; }
        if (file.moveBudget < 0) { error = $"moveBudget {file.moveBudget} is negative"; return false; }

        var itemIds = new HashSet<string>();
        foreach (string item in file.items)
            if (string.IsNullOrEmpty(item) || !itemIds.Add(item)) { error = $"items must be {n * n} different ids ('{item}')"; return false; }

        var seen = new bool[n * n];
        var cells = new TileData[n, n];
        for (int i = 0; i < file.board.Length; i++)
        {
            int id = file.board[i];
            if (id < 0 || id >= n * n || seen[id]) { error = $"board must hold each id 0..{n * n - 1} once (bad id {id} at index {i})"; return false; }
            seen[id] = true;
            cells[i / n, i % n] = new TileData(id);
        }

        if (file.steps == null || file.steps.Length != n) { error = $"{file.steps?.Length ?? 0} steps, expected {n} (one per row)"; return false; }
        var stepRows = new bool[n];
        foreach (var step in file.steps)
        {
            if (step.row < 0 || step.row >= n || stepRows[step.row]) { error = $"steps must name each row 0..{n - 1} once (bad row {step.row})"; return false; }
            stepRows[step.row] = true;
            step.clues ??= new LevelClue[0];
            foreach (var clue in step.clues)
                if (!string.IsNullOrEmpty(clue.item) && !itemIds.Contains(clue.item))
                { error = $"a clue of step '{step.goal}' shows item '{clue.item}', which is not on the board"; return false; }
        }

        level = new LevelDefinition(levelNumber, n, cells, file.moveBudget, file.name,
                                    file.characters, file.categories, file.items, file.steps, file.solution);
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
    public string[] characters;
    public string[] categories;
    public string[] items;
    public int[] board;
    public LevelStep[] steps;
    public int moveBudget;
    public LevelMove[] solution;
}

[Serializable]
public class LevelStep
{
    public int row;
    public string goal;            // "Pack their bags"
    public LevelClue[] clues;
}

[Serializable]
public class LevelClue
{
    public string item;            // item id shown beside the clue (optional)
    public string text;            // "Mia carries the <color=#ED3937>red</color> bag."
}

[Serializable]
public class LevelMove
{
    public string axis;        // "row" or "column"
    public int line;
    public int direction;      // +1 = right / down, -1 = left / up

    public bool IsRow => axis == "row";
}
