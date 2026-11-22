using UnityEngine;

// Turns a level number (1-based) into a LevelDefinition from Resources/Levels/level-NNN.json.
// Levels are numbered without gaps from 1; past the last file play loops back to level 1, so a saved
// level number from an older build (or more wins than files) still opens a level.
public class Manager_Level : Singleton<Manager_Level>
{
    private ILevelSource _source;

    // Number of level files, counted from level-001 up to the first missing number.
    public int LevelCount { get; private set; }

    public override void Awake2()
    {
        _source = new JsonLevelSource(FileRef.Instance);
        LevelCount = 0;
        while (JsonLevelSource.Exists(LevelCount + 1)) LevelCount++;
        if (LevelCount == 0) Debug.LogError("[Manager_Level] No level files in Resources/Levels.");
    }

    // The file a level number plays: n itself, or n wrapped into 1..LevelCount.
    public int FileNumber(int levelNumber) =>
        LevelCount == 0 ? levelNumber : (Mathf.Max(1, levelNumber) - 1) % LevelCount + 1;

    public LevelDefinition Load(int levelNumber) => _source.Load(FileNumber(levelNumber));
}
