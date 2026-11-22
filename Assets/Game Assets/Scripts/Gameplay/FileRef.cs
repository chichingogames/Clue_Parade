using System;
using UnityEngine;

// The one place assets are assigned. Managers ask FileRef.Instance instead of holding prefab fields.
// Filled by ShiftSort > Build Assets (AssetBuilder); names can be edited here afterwards.
[AssetPath("FileRef")]
[CreateAssetMenu(fileName = "FileRef", menuName = "ScriptableObjects/FileRef")]
public class FileRef : ScriptableSingleton<FileRef>
{
    // A board item. Id = its art's file name (Sprites/Tilewrap/Items/{Id}.png), used by level files.
    [Serializable]
    public struct ItemData
    {
        public string Id;
        [Tooltip("Shown under the item on its tile.")]
        public string Name;
        public Sprite Sprite;
    }

    // A character (a board column). Poses[k] is shown after k steps are confirmed
    // (neutral, gear, bag, complete for the first scene's art).
    [Serializable]
    public struct CharacterData
    {
        public string Id;
        public string Name;
        public Sprite[] Poses;
    }

    [Serializable]
    public struct SoundData
    {
        public SoundType Type;
        [Tooltip("Variants. Manager_Audio.PlaySound picks one at random, or the one asked for.")]
        public AudioClip[] Clips;
        [Range(0f, 1f)] public float Volume;
    }

    [Header("Prefabs")]
    [SerializeField] private Cell _cellPrefab;
    [SerializeField] private ItemTile _itemTilePrefab;
    [SerializeField] private CategoryTag _categoryTagPrefab;

    [Header("Board")]
    [Tooltip("Tile surface of rows that are not the active step's.")]
    [SerializeField] private Sprite _tileSurface;
    [Tooltip("Tile surface of the active step's row.")]
    [SerializeField] private Sprite _tileSurfaceActive;

    [Header("Catalog")]
    [SerializeField] private ItemData[] _items = new ItemData[0];
    [SerializeField] private CharacterData[] _characters = new CharacterData[0];

    [Header("Audio")]
    [SerializeField] private AudioClip _music;
    [SerializeField] private SoundData[] _sounds = new SoundData[0];

    public Cell CellPrefab => _cellPrefab;
    public ItemTile ItemTilePrefab => _itemTilePrefab;
    public CategoryTag CategoryTagPrefab => _categoryTagPrefab;
    public Sprite TileSurface => _tileSurface;
    public Sprite TileSurfaceActive => _tileSurfaceActive;
    public AudioClip Music => _music;

    public bool TryGetItem(string id, out ItemData item)
    {
        foreach (var entry in _items)
        {
            if (entry.Id != id || entry.Sprite == null) continue;
            item = entry;
            return true;
        }
        item = default;
        return false;
    }

    // Logs and returns a stand-in (the id as its name, no art) for an unknown item.
    public ItemData GetItem(string id)
    {
        if (TryGetItem(id, out var item)) return item;
        Debug.LogError($"[FileRef] No item '{id}'.", this);
        return new ItemData { Id = id, Name = id };
    }

    public bool TryGetCharacter(string id, out CharacterData character)
    {
        foreach (var entry in _characters)
        {
            if (entry.Id != id || entry.Poses == null || entry.Poses.Length == 0) continue;
            character = entry;
            return true;
        }
        character = default;
        return false;
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

    [ContextMenu("Validate references")]
    private void ValidateReferences()
    {
        int problems = 0;
        if (_cellPrefab == null) { Debug.LogError("[FileRef] Cell prefab missing.", this); problems++; }
        if (_itemTilePrefab == null) { Debug.LogError("[FileRef] ItemTile prefab missing.", this); problems++; }
        if (_categoryTagPrefab == null) { Debug.LogError("[FileRef] CategoryTag prefab missing.", this); problems++; }
        if (_tileSurface == null || _tileSurfaceActive == null) { Debug.LogError("[FileRef] Tile surfaces missing.", this); problems++; }
        if (_music == null) Debug.LogWarning("[FileRef] No music clip.", this);
        foreach (SoundType type in Enum.GetValues(typeof(SoundType)))
            if (type != SoundType.TileMatch && !TryGetSound(type, out _)) Debug.LogWarning($"[FileRef] No clip for sound {type}.", this);
        foreach (var item in _items)
            if (item.Sprite == null || string.IsNullOrEmpty(item.Name)) { Debug.LogError($"[FileRef] Item '{item.Id}' needs a sprite and a name.", this); problems++; }

        if (problems == 0) Debug.Log($"[FileRef] OK. {_items.Length} items, {_characters.Length} characters.", this);
    }
}
