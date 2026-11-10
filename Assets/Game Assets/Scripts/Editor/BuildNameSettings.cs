using System;
using UnityEditor;
using UnityEngine;

// Editor-only settings for BuildNaming. The asset lives next to this script in an Editor folder,
// so it never ships. Open it with ShiftSort > Build Name Settings.
public class BuildNameSettings : ScriptableObject
{
    public const string AssetPath = "Assets/Game Assets/Scripts/Editor/BuildNameSettings.asset";
    public const string DefaultDateFormat = "yyyy-MM-dd";

    [Tooltip("Last part of the file name, e.g. \"internal-test\" or \"store\". Leave empty to drop it.\nSpaces become '-'; '_' and characters invalid in file names are removed.")]
    public string AdditionalInformation = "";

    [Tooltip("C# date format for the date part. Default yyyy-MM-dd.")]
    public string DateFormat = DefaultDateFormat;

    public static BuildNameSettings Load()
    {
        var settings = AssetDatabase.LoadAssetAtPath<BuildNameSettings>(AssetPath);
        if (settings != null) return settings;

        settings = CreateInstance<BuildNameSettings>();
        AssetDatabase.CreateAsset(settings, AssetPath);
        AssetDatabase.SaveAssets();
        return settings;
    }
}

// Shows the file name the next build will get.
[CustomEditor(typeof(BuildNameSettings))]
public class BuildNameSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        string extension = EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk";
        EditorGUILayout.LabelField("Next Android build", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(BuildNaming.FileName(DateTime.Now) + extension, EditorStyles.textField,
                                        GUILayout.Height(EditorGUIUtility.singleLineHeight));
        EditorGUILayout.HelpBox("GameName = Player Settings > Product Name, version = Player Settings > Version. " +
                                "Applied when you build from the Build window (Android, not Export Project).", MessageType.None);
    }
}
