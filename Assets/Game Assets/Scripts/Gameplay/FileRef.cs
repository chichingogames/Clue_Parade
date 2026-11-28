using System;
using System.Collections.Generic;
using Chichingo.TilewrapMystery;
using UnityEngine;

// The one place assets are assigned. Managers ask FileRef.Instance instead of holding prefab fields.
// Filled by ShiftSort > Build Assets (AssetBuilder).
//
// Content is the v5 pack's TilewrapContentLibrary (Assets/TilewrapMystery/Generated, made by Tools >
// Chichingo > Tilewrap > Build current 30-level content): the 30 cases (Data/levels_01_30.json, parsed
// into DeliveryContent), the sprites (loaded on demand from Resources, cached once loaded) and the 90
// fitted rig prefabs (Resources/TilewrapMystery/Rigs, loaded on demand).
[AssetPath("FileRef")]
[CreateAssetMenu(fileName = "FileRef", menuName = "ScriptableObjects/FileRef")]
public class FileRef : ScriptableSingleton<FileRef>
{
    [Serializable]
    public struct SoundData
    {
        public SoundType Type;
        [Tooltip("Variants. Manager_Audio.PlaySound picks one at random, or the one asked for.")]
        public AudioClip[] Clips;
        [Range(0f, 1f)] public float Volume;
        [Tooltip("Optional per-clip gain (same order as Clips), on top of Volume; missing = 1.")]
        public float[] ClipVolumes;
    }

    [Header("Prefabs")]
    [SerializeField] private Cell _cellPrefab;
    [SerializeField] private ItemTile _itemTilePrefab;

    [Header("Content")]
    [Tooltip("Assets/TilewrapMystery/Generated/TilewrapContentLibrary.asset")]
    [SerializeField] private TilewrapContentLibrary _content;

    [Header("Audio")]
    [SerializeField] private AudioClip _music;
    [SerializeField] private SoundData[] _sounds = new SoundData[0];

    public Cell CellPrefab => _cellPrefab;
    public ItemTile ItemTilePrefab => _itemTilePrefab;
    public TilewrapContentLibrary Content => _content;
    public AudioClip Music => _music;

    private Dictionary<string, Sprite> _spriteMap;
    private DeliveryContent _caseContent;

    // A content sprite by its key ("luggage_00", "mia_base", "fx_star", "wax_seal"), or null. Loaded
    // from Resources the first time it is asked for.
    public Sprite Sprite(string key)
    {
        if (string.IsNullOrEmpty(key) || _content == null) return null;
        _spriteMap ??= new Dictionary<string, Sprite>(StringComparer.Ordinal);
        if (_spriteMap.TryGetValue(key, out var found)) return found;
        Sprite sprite = null;
        try { sprite = _content.GetSprite(key); }
        catch (Exception) { /* unknown key: null, the caller hides the image */ }
        _spriteMap[key] = sprite;
        return sprite;
    }

    // A subject's fitted rig prefab ("crew_mia"), or null.
    public TilewrapRigView Rig(string id) =>
        string.IsNullOrEmpty(id) ? null : Resources.Load<TilewrapRigView>("TilewrapMystery/Rigs/" + id);

    // The 30 cases, parsed once from the library's campaign (null, logged, when missing or broken).
    public DeliveryContent Cases
    {
        get
        {
            if (_caseContent != null || _content == null || _content.campaignJson == null) return _caseContent;
            try { _caseContent = DeliveryContent.Parse(_content.campaignJson.text); }
            catch (Exception e) { Debug.LogError($"[FileRef] {_content.campaignJson.name}: {e.Message}", this); }
            return _caseContent;
        }
    }

    // False (and no error) when the sound has no clips: a missing sound is silence, not a crash.
    public bool TryGetSound(SoundType type, out SoundData data)
    {
        foreach (var entry in _sounds)
        {
            if (entry.Type != type || entry.Clips == null || entry.Clips.Length == 0) continue;
            data = entry;
            return true;
        }
        data = default;
        return false;
    }

    private void OnValidate()
    {
        _spriteMap = null;
        _caseContent = null;
    }

    [ContextMenu("Validate references")]
    private void ValidateReferences()
    {
        int problems = 0;
        if (_cellPrefab == null) { Debug.LogError("[FileRef] Cell prefab missing.", this); problems++; }
        if (_itemTilePrefab == null) { Debug.LogError("[FileRef] ItemTile prefab missing.", this); problems++; }
        if (_content == null) { Debug.LogError("[FileRef] Content library missing. Run Tools > Chichingo > Tilewrap > Build current 30-level content.", this); problems++; }
        if (Cases == null) { Debug.LogError("[FileRef] Cases missing: the library has no campaign.", this); problems++; }
        if (_music == null) Debug.LogWarning("[FileRef] No music clip.", this);
        foreach (SoundType type in Enum.GetValues(typeof(SoundType)))
            if (type != SoundType.LineComplete && !TryGetSound(type, out _)) Debug.LogWarning($"[FileRef] No clip for sound {type}.", this);

        if (problems == 0) Debug.Log($"[FileRef] OK. {Cases.cases.Length} cases, {_content.Content.sprites.Length} sprites, {_content.Content.rigs.Length} rigs.", this);
    }
}
