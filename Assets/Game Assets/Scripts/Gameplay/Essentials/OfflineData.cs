using System;
using UnityEngine;

// The save: public fields, stored as JSON in one PlayerPrefs key.
//
// Rules (skeleton.md §8):
// - PUBLIC FIELDS ONLY (JsonUtility ignores properties).
// - APPEND-ONLY: never rename or repurpose a field; a new field gets its initializer for old saves.
// - One writer per field (named below); everyone else only reads.
// - Loaded once at boot by SingletonManager; migrations go in Load().
[Serializable]
public class OfflineData
{
    private const string Key = "OfflineData";

    public int CurrentLevel = 1;        // 1-based; the level to open on launch. Writer: Manager_Game (on win)
    public int PassedLevels = 0;        // highest level number ever won. Writer: Manager_Game (on win)
    public bool IsSoundOn = true;       // music + effects. Writer: Manager_Audio.SetSoundOn

    private static OfflineData _instance;

    public static OfflineData Instance
    {
        get
        {
            if (_instance == null) Load();
            return _instance;
        }
    }

    public void Save()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
        PlayerPrefs.Save();
    }

    public static void Load()
    {
        _instance = null;
        if (PlayerPrefs.HasKey(Key))
        {
            try { _instance = JsonUtility.FromJson<OfflineData>(PlayerPrefs.GetString(Key)); }
            catch (Exception e) { Debug.LogError($"[OfflineData] Save could not be read, starting fresh: {e.Message}"); }
        }
        if (_instance == null) _instance = new OfflineData();

        // Migrations / repairs.
        _instance.CurrentLevel = Mathf.Max(1, _instance.CurrentLevel);
        _instance.PassedLevels = Mathf.Max(0, _instance.PassedLevels);
    }

    // Deletes the save; the next Load() starts fresh. Used by the tester.
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        Load();
    }
}
