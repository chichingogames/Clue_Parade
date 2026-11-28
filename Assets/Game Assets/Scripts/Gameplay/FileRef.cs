using System;
using System.Collections.Generic;
using Chichingo.TilewrapVaried;
using UnityEngine;

// The one place assets are assigned. Managers ask FileRef.Instance instead of holding prefab fields.
// Filled by ShiftSort > Build Assets (AssetBuilder).
//
// The cases are the 30 deduction cases (_cases: Assets/Game Assets/Data/delivery_cases.json,
// DeliveryContent). Their item, rig-part and UI sprites come from the content pack's
// TilewrapContentLibrary, made by Tools > Chichingo > Tilewrap > Build 30-level content
// (Assets/TilewrapVaried/Generated), plus the evidence props (_evidenceSprites:
// Sprites/Evidence, keyed by file name). Subjects are drawn from each case's rig layers.
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
    [Tooltip("Assets/TilewrapVaried/Generated/TilewrapContentLibrary.asset")]
    [SerializeField] private TilewrapContentLibrary _content;
    [Tooltip("Assets/Game Assets/Data/delivery_cases.json: the 30 cases (DeliveryContent).")]
    [SerializeField] private TextAsset _cases;
    [Tooltip("Assets/Game Assets/Sprites/Evidence: art the pack lacks (key = sprite name).")]
    [SerializeField] private Sprite[] _evidenceSprites = new Sprite[0];

    [Header("Audio")]
    [SerializeField] private AudioClip _music;
    [SerializeField] private SoundData[] _sounds = new SoundData[0];

    public Cell CellPrefab => _cellPrefab;
    public ItemTile ItemTilePrefab => _itemTilePrefab;
    public TilewrapContentLibrary Content => _content;
    public AudioClip Music => _music;

    private Dictionary<string, Sprite> _spriteMap;
    private DeliveryContent _caseContent;

    // A content sprite by its key ("luggage_00", "mia_base", "fx_star", "wax_seal"), or null.
    public Sprite Sprite(string key)
    {
        if (_spriteMap == null)
        {
            _spriteMap = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            if (_content != null && _content.sprites != null)
                foreach (var entry in _content.sprites) _spriteMap[entry.id] = entry.sprite;
            foreach (var sprite in _evidenceSprites) if (sprite != null) _spriteMap[sprite.name] = sprite;
        }
        return key != null && _spriteMap.TryGetValue(key, out var found) ? found : null;
    }

    // The any-row cases, parsed once (null, logged, when the file is missing or broken).
    public DeliveryContent Cases
    {
        get
        {
            if (_caseContent != null || _cases == null) return _caseContent;
            try { _caseContent = DeliveryContent.Parse(_cases.text); }
            catch (Exception e) { Debug.LogError($"[FileRef] {_cases.name}: {e.Message}", this); }
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
        if (_content == null) { Debug.LogError("[FileRef] Content library missing. Run Tools > Chichingo > Tilewrap > Build 30-level content.", this); problems++; }
        if (Cases == null) { Debug.LogError("[FileRef] Cases missing: assign Assets/Game Assets/Data/delivery_cases.json.", this); problems++; }
        if (_music == null) Debug.LogWarning("[FileRef] No music clip.", this);
        foreach (SoundType type in Enum.GetValues(typeof(SoundType)))
            if (type != SoundType.LineComplete && !TryGetSound(type, out _)) Debug.LogWarning($"[FileRef] No clip for sound {type}.", this);

        if (problems == 0) Debug.Log($"[FileRef] OK. {Cases.cases.Length} cases, {_content.sprites.Length} sprites, {_evidenceSprites.Length} evidence sprites.", this);
    }
}
