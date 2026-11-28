using System.IO;
using UnityEngine;

// Backs up / restores the Editor's save keys (OfflineData, PlaytestLog) around agent play tests.
public static class SaveBackup
{
    private static readonly string[] Keys = { "OfflineData", "PlaytestLog" };

    public static string Backup(string folder)
    {
        Directory.CreateDirectory(folder);
        foreach (string key in Keys)
            File.WriteAllText(Path.Combine(folder, key + ".txt"), PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : "<none>");
        return "backed up: " + (PlayerPrefs.HasKey("OfflineData") ? PlayerPrefs.GetString("OfflineData") : "<none>");
    }

    public static string Restore(string folder)
    {
        foreach (string key in Keys)
        {
            string value = File.ReadAllText(Path.Combine(folder, key + ".txt"));
            if (value == "<none>") PlayerPrefs.DeleteKey(key);
            else PlayerPrefs.SetString(key, value);
        }
        PlayerPrefs.Save();
        return "restored: " + (PlayerPrefs.HasKey("OfflineData") ? PlayerPrefs.GetString("OfflineData") : "<none>");
    }
}
