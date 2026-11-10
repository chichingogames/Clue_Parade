using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Names Android builds [GameName]_[version]_[date]_[additionalInformation].apk (or .aab):
//   GameName              Player Settings > Product Name (spaces and invalid characters removed)
//   version               Player Settings > Version (bundleVersion)
//   date                  the build day, BuildNameSettings.DateFormat (default yyyy-MM-dd)
//   additionalInformation BuildNameSettings.AdditionalInformation; left out (with its "_") when empty
//
// Runs before every build started from the Build button (Build Settings / Build Profiles window):
// after you pick the save location, the file name is replaced and the normal build continues in the
// same folder. Builds started from code bypass the window; call BuildNaming.Apply(ref options) there.
// Other platforms and "Export Project" (a Gradle folder, not a file) are left alone.
//
// Settings: ShiftSort > Build Name Settings (an editor-only asset; never ships).
[InitializeOnLoad]
public static class BuildNaming
{
    static BuildNaming() => BuildPlayerWindow.RegisterBuildPlayerHandler(BuildFromWindow);

    private static void BuildFromWindow(BuildPlayerOptions options)
    {
        Apply(ref options);
        BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
    }

    // Renames options.locationPathName for an Android apk/aab build; no-op otherwise.
    public static void Apply(ref BuildPlayerOptions options)
    {
        if (options.target != BuildTarget.Android || EditorUserBuildSettings.exportAsGoogleAndroidProject) return;
        if (string.IsNullOrEmpty(options.locationPathName)) return;

        string extension = EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk";
        string folder = Path.GetDirectoryName(options.locationPathName) ?? "";
        string path = Path.Combine(folder, FileName(DateTime.Now) + extension);

        Debug.Log($"[BuildNaming] {Path.GetFileName(options.locationPathName)} -> {Path.GetFileName(path)}");
        options.locationPathName = path;
    }

    // The name without extension, e.g. Tilewrap_1.0.0_2026-11-10_internal-test.
    public static string FileName(DateTime date)
    {
        var settings = BuildNameSettings.Load();
        string name = Clean(PlayerSettings.productName);
        string version = Clean(PlayerSettings.bundleVersion);
        string day = Clean(date.ToString(string.IsNullOrWhiteSpace(settings.DateFormat) ? BuildNameSettings.DefaultDateFormat : settings.DateFormat));
        string info = Clean(settings.AdditionalInformation);

        string file = $"{name}_{version}_{day}";
        return string.IsNullOrEmpty(info) ? file : $"{file}_{info}";
    }

    // Keeps the four parts readable and file-system safe: spaces become '-', characters that are
    // invalid in file names (and '_', the part separator) are dropped.
    private static string Clean(string part)
    {
        if (string.IsNullOrWhiteSpace(part)) return "";
        part = Regex.Replace(part.Trim(), @"\s+", "-");
        foreach (char c in Path.GetInvalidFileNameChars()) part = part.Replace(c.ToString(), "");
        return part.Replace("_", "");
    }

    [MenuItem("ShiftSort/Build Name Settings")]
    private static void SelectSettings()
    {
        Selection.activeObject = BuildNameSettings.Load();
        EditorGUIUtility.PingObject(Selection.activeObject);
    }
}
