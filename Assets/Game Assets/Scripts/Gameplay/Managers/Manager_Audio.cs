using System;
using UnityEngine;

// Music and sound effects. Clips come from FileRef (music clip + SoundData table); callers just
// say what happened: Manager_Audio.Instance?.PlaySound(SoundType.X).
// One switch (SoundOn) mutes both music and effects; the HUD's sound button drives it.
// The switch is saved in OfflineData.IsSoundOn.
//
// The two AudioSources live on their own scene objects, Sound/Music and Sound/SFX (made by
// ShiftSort > Build Scene), so their levels are set in the Inspector, live in Play mode too:
//   Sound/Music  Volume = background music level (this script never writes it).
//   Sound/SFX    Volume = master level for every effect; FileRef's per-sound Volume scales on top.
public class Manager_Audio : Singleton<Manager_Audio>
{
    public event Action<bool> OnSoundChanged = delegate { };

    public bool SoundOn { get; private set; }

#if UNITY_EDITOR
    // Tester: log every PlaySound call (sound is hard to check by ear when the Editor is in the background).
    public static bool DebugLogSounds;
#endif

    [SerializeField] private AudioSource _music;
    [SerializeField] private AudioSource _sfx;

    public override void Awake2()
    {
        SoundOn = OfflineData.Instance.IsSoundOn;
        if (_music == null || _sfx == null)
            Debug.LogError("[Manager_Audio] Music / SFX AudioSource not assigned. Run ShiftSort > Build Scene.", this);
    }

    private void Start()
    {
        var clip = FileRef.Instance.Music;
        if (clip == null || _music == null) return;
        _music.clip = clip;
        _music.loop = true;
        _music.mute = !SoundOn;
        _music.Play();
    }

    // variant: which clip of the entry to play (clamped); -1 = a random one.
    public void PlaySound(SoundType type, int variant = -1)
    {
        if (!SoundOn || _sfx == null || !FileRef.Instance.TryGetSound(type, out var data)) return;

        var clips = data.Clips;
        var clip = clips[variant < 0 ? UnityEngine.Random.Range(0, clips.Length) : Mathf.Clamp(variant, 0, clips.Length - 1)];
        if (clip != null) _sfx.PlayOneShot(clip, data.Volume);
#if UNITY_EDITOR
        if (DebugLogSounds) Debug.Log($"[Audio] {type} -> {(clip != null ? clip.name : "null")}");
#endif
    }

    public void SetSoundOn(bool on)
    {
        if (SoundOn == on) return;
        SoundOn = on;
        OfflineData.Instance.IsSoundOn = on;
        OfflineData.Instance.Save();
        if (_music != null) _music.mute = !on;
        if (!on && _sfx != null) _sfx.Stop();
        OnSoundChanged(on);
    }

    public void ToggleSound() => SetSoundOn(!SoundOn);
}
