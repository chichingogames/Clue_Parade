using UnityEngine;
#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
using CandyCoded.HapticFeedback;
#endif

// Device vibration through the CandyCoded HapticFeedback package (xyz.candycoded.hapticfeedback).
// Always on: there is no setting. Call it next to the matching sound, one pulse per event:
//   Haptics.Play(HapticType.Light);
// The package does nothing in the Editor and only compiles for Android / iOS / Editor, hence the
// #if: other targets build without it. A native failure is logged once and never breaks gameplay.
public static class Haptics
{
#if UNITY_EDITOR
    // Tester: log every pulse (there is nothing to feel in the Editor).
    public static bool DebugLog;
#endif

    private static bool _failed;

    public static void Play(HapticType type)
    {
#if UNITY_EDITOR
        if (DebugLog) Debug.Log($"[Haptics] {type}");
#endif
        if (_failed) return;

#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
        try
        {
            switch (type)
            {
                case HapticType.Light: HapticFeedback.LightFeedback(); break;
                case HapticType.Medium: HapticFeedback.MediumFeedback(); break;
                case HapticType.Heavy: HapticFeedback.HeavyFeedback(); break;
            }
        }
        catch (System.Exception e)
        {
            _failed = true;
            Debug.LogWarning($"[Haptics] Disabled for this session: {e.Message}");
        }
#endif
    }
}
