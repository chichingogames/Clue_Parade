using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Local playtest events (the handoff's report: level starts, moves, undo, hints, failures,
// completions, active time, exports). Kept apart from OfflineData under its own PlayerPrefs key,
// capped at MaxEvents (oldest dropped), saved on every event. Writer: Manager_Flow.
public static class PlaytestLog
{
    private const string Key = "PlaytestLog";
    private const int MaxEvents = 2000;

    [Serializable]
    public class Entry
    {
        public string type;
        public string at;          // ISO 8601, UTC
        public int level;          // 0 = none
        public string data;        // "key=value; key=value"
    }

    [Serializable]
    private class Store
    {
        public string sessionId;
        public List<Entry> events = new List<Entry>();
    }

    private static Store _store;

    private static Store Data
    {
        get
        {
            if (_store != null) return _store;
            try { _store = JsonUtility.FromJson<Store>(PlayerPrefs.GetString(Key, "")); } catch { _store = null; }
            if (_store == null || string.IsNullOrEmpty(_store.sessionId))
                _store = new Store { sessionId = Guid.NewGuid().ToString() };
            _store.events ??= new List<Entry>();
            return _store;
        }
    }

    public static string SessionId => Data.sessionId;
    public static IReadOnlyList<Entry> Events => Data.events;

    public static void Log(string type, int level, string data = "")
    {
        var store = Data;
        store.events.Add(new Entry { type = type, at = DateTime.UtcNow.ToString("o"), level = level, data = data });
        if (store.events.Count > MaxEvents) store.events.RemoveRange(0, store.events.Count - MaxEvents);
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(store));
        PlayerPrefs.Save();
    }

    // A fresh test: new session id, no events.
    public static void Clear()
    {
        _store = new Store { sessionId = Guid.NewGuid().ToString() };
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(_store));
        PlayerPrefs.Save();
    }

    [Serializable]
    private class Report
    {
        public string prototype = "Tilewrap Clues Unity";
        public string version;
        public string exportedAt;
        public string sessionId;
        public int screenWidth;
        public int screenHeight;
        public List<int> completedLevels;
        public int coins;
        public string feedbackGoal;
        public string feedbackWrap;
        public string feedbackDifficulty;
        public List<Entry> events;
    }

    // Writes the report JSON to persistentDataPath and returns its path.
    public static string Export()
    {
        var save = OfflineData.Instance;
        var report = new Report
        {
            version = Application.version,
            exportedAt = DateTime.UtcNow.ToString("o"),
            sessionId = SessionId,
            screenWidth = Screen.width,
            screenHeight = Screen.height,
            completedLevels = save.CompletedLevels,
            coins = save.Coins,
            feedbackGoal = save.FeedbackGoal,
            feedbackWrap = save.FeedbackWrap,
            feedbackDifficulty = save.FeedbackDifficulty,
            events = Data.events,
        };
        string path = Path.Combine(Application.persistentDataPath,
                                   $"Tilewrap_Clues_Onboarding_Playtest_{DateTime.Now:yyyy-MM-dd}.json");
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        return path;
    }
}
