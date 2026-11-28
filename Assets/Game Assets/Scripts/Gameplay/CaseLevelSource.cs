using System;
using System.Collections.Generic;
using Chichingo.TilewrapVaried;
using UnityEngine;

// Loads the content pack's cases (Assets/TilewrapVaried/Data/Levels/level_NNN.json, listed in
// FileRef.Content.levels) and converts each into a LevelDefinition. A case file holds:
//   startingCells / goalCells   9 item ids, row-major. goalCells is the solved board, so an item's
//                               tile id is its index in goalCells (= its target cell).
//   subjects[] (column, name, rigId), categories[] (row, label), items[] (id, label, sprite,
//   targetCell, clue), moveLimit (0 = unlimited), minimumMoves, title / goalText / setupText /
//   completionText, difficulty, nextLevelId. Its solution, requiredFirstSwipe and tutorial are
//   for row / column wraps and are not used: this build swaps neighbours, with no tutorial.
// Files are parsed once and cached. A case naming a sprite or rig that FileRef lacks is rejected.
public class CaseLevelSource : ILevelSource
{
    private readonly FileRef _fileRef;
    private readonly Dictionary<int, TextAsset> _files = new Dictionary<int, TextAsset>();
    private readonly Dictionary<int, LevelDefinition> _cache = new Dictionary<int, LevelDefinition>();

    public int Count { get; }

    public CaseLevelSource(FileRef fileRef)
    {
        _fileRef = fileRef;
        var levels = fileRef.Content != null ? fileRef.Content.levels : null;
        if (levels == null) return;
        foreach (var file in levels)
        {
            if (file == null) continue;
            var data = JsonUtility.FromJson<TilewrapCaseData>(file.text);
            if (data != null && data.id > 0) _files[data.id] = file;
        }
        while (_files.ContainsKey(Count + 1)) Count++;
    }

    public LevelDefinition Load(int levelNumber)
    {
        if (_cache.TryGetValue(levelNumber, out var cached)) return cached;
        if (!_files.TryGetValue(levelNumber, out var file)) return null;

        if (!TryConvert(file, out var level, out string error) || !TryCheckArt(level, _fileRef, out error))
        {
            Debug.LogError($"[CaseLevelSource] {file.name}: {error}");
            return null;
        }
        _cache[levelNumber] = level;
        return level;
    }

    // Every item sprite and subject rig the case names is in FileRef.
    public static bool TryCheckArt(LevelDefinition level, FileRef fileRef, out string error)
    {
        error = null;
        foreach (var item in level.Items)
            if (fileRef.Sprite(item.Sprite) == null) { error = $"item sprite '{item.Sprite}' is not in the content library"; return false; }
        foreach (var subject in level.Subjects)
            if (fileRef.Rig(subject.RigId) == null) { error = $"rig '{subject.RigId}' is not in the content library"; return false; }
        return true;
    }

    public static bool TryConvert(TextAsset file, out LevelDefinition level, out string error)
    {
        level = null;
        error = null;
        TilewrapCaseData data;
        try { data = TilewrapCaseData.Parse(file); }     // structure: 3x3, permutation, 3 subjects / categories / 9 items
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }

        int n = data.rows;
        var subjects = new LevelSubject[n];
        foreach (var s in data.subjects)
        {
            if (s.column < 0 || s.column >= n || subjects[s.column] != null) { error = $"subject '{s.id}' has a bad column {s.column}"; return false; }
            subjects[s.column] = new LevelSubject { Id = s.id, Name = s.name, RigId = s.rigId };
        }

        var categories = new string[n];
        var categoryRows = new Dictionary<string, int>();
        foreach (var c in data.categories)
        {
            if (c.row < 0 || c.row >= n || categories[c.row] != null) { error = $"category '{c.id}' has a bad row {c.row}"; return false; }
            categories[c.row] = c.label;
            categoryRows[c.id] = c.row;
        }

        var items = new LevelItem[n * n];
        foreach (var item in data.items)
        {
            if (!categoryRows.TryGetValue(item.categoryId ?? "", out int row) || row != item.targetCell / n)
            { error = $"item '{item.id}' is in category '{item.categoryId}' but targets row {item.targetCell / n}"; return false; }
            items[item.targetCell] = new LevelItem { Id = item.id, Label = item.label, Sprite = item.sprite, Clue = item.clue ?? "" };
        }

        var cells = new TileData[n, n];
        for (int i = 0; i < data.startingCells.Length; i++)
            cells[i / n, i % n] = new TileData(Array.IndexOf(data.goalCells, data.startingCells[i]));

        // The pack's numbers are for wrap swipes; this build swaps neighbours. The minimum is the swap
        // board's, and a limited case keeps the pack's slack over its minimum (at least 2 spare).
        var ids = new int[n * n];
        for (int i = 0; i < ids.Length; i++) ids[i] = cells[i / n, i % n].Id;
        int minimum = SwapPuzzle.Distance(ids);
        if (minimum < 0) { error = "the swap solver only handles 3x3 boards"; return false; }
        int limit = data.moveLimit > 0 ? minimum + Math.Max(MinimumSpare, data.moveLimit - data.minimumMoves) : 0;

        level = new LevelDefinition(data.id, n, cells, limit, minimum,
                                    data.title, data.goalText, data.setupText, data.completionText, data.difficulty,
                                    data.chapterId, data.nextLevelId, data.firstClearCoins,
                                    subjects, categories, items);
        return true;
    }

    private const int MinimumSpare = 2;
}
