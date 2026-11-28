using UnityEngine;

// Turns a case number (1-based) into its DeliveryCase (FileRef.Cases). Cases are numbered
// 1..LevelCount in file order; a saved number past the last case opens the last one.
public class Manager_Level : Singleton<Manager_Level>
{
    private DeliveryCase[] _cases = new DeliveryCase[0];

    public int LevelCount => _cases.Length;

    public override void Awake2()
    {
        var content = FileRef.Instance.Cases;
        if (content != null) _cases = content.cases;
        if (LevelCount == 0) Debug.LogError("[Manager_Level] No cases. Assign FileRef's Cases (Assets/Game Assets/Data/delivery_cases.json).");
    }

    public DeliveryCase Load(int levelNumber) => LevelCount == 0 ? null : _cases[Mathf.Clamp(levelNumber, 1, LevelCount) - 1];
}
