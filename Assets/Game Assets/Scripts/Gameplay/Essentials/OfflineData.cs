using System;
using System.Collections.Generic;
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

    public int CurrentLevel = 1;        // 1-based; the case to open on launch (the one started, the next after a win). Writer: Manager_Game
    public int PassedLevels = 0;        // highest level number ever won. Writer: Manager_Game (on win)
    public bool IsSoundOn = true;       // music + effects. Writer: Manager_Audio.SetSoundOn
    // Campaign and playtest (writer: Manager_Flow). Case ids 1-5 are the five-case build's levels,
    // so a save from it keeps its clears.
    public List<int> CompletedLevels = new List<int>();   // first clears, ascending
    public int Coins = 60;                                 // starting 60 (the pack's economy); first-clear rewards, minus help bought
    public List<string> Lessons = new List<string>();     // retired: the five-case lesson ids (no writer now)
    public string FeedbackGoal = "";                       // retired: the closing feedback answers (no writer now)
    public string FeedbackWrap = "";
    public string FeedbackDifficulty = "";
    // The unfinished case to resume (Manager_Game.ResumeData as JSON; "" = none). Writer: Manager_Game
    public string Resume = "";
    public List<int> SeenCases = new List<int>();          // cases whose first-look settle has played. Writer: Manager_Board

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
        _instance.CompletedLevels ??= new List<int>();
        _instance.Lessons ??= new List<string>();
        _instance.SeenCases ??= new List<int>();
        _instance.Resume ??= "";
    }

    // Deletes the save; the next Load() starts fresh. Used by the tester.
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        Load();
    }
}
