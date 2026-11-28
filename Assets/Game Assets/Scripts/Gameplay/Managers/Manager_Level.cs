using UnityEngine;

// Turns a level number (1-based) into a LevelDefinition: the content pack's cases, through
// CaseLevelSource (FileRef.Content). Levels are numbered 1..LevelCount without gaps; a saved number
// past the last case (an older build) opens the last one.
public class Manager_Level : Singleton<Manager_Level>
{
    private ILevelSource _source;

    public int LevelCount { get; private set; }

    public override void Awake2()
    {
        _source = new CaseLevelSource(FileRef.Instance);
        LevelCount = _source.Count;
        if (LevelCount == 0) Debug.LogError("[Manager_Level] No cases. Assign FileRef's content library (ShiftSort > Build Assets).");
    }

    public LevelDefinition Load(int levelNumber) => _source.Load(Mathf.Clamp(levelNumber, 1, Mathf.Max(1, LevelCount)));
}
