using System;
using System.Collections.Generic;
using UnityEngine;

// The one place assets are assigned. Managers ask FileRef.Instance instead of holding prefab fields.
[AssetPath("FileRef")]
[CreateAssetMenu(fileName = "FileRef", menuName = "ScriptableObjects/FileRef")]
public class FileRef : ScriptableSingleton<FileRef>
{
    [Serializable]
    public struct TileSprite
    {
        public TileShape Shape;
        public TileColor Color;
        public Sprite Sprite;
    }

    [Serializable]
    public struct ShapeSprite
    {
        public TileShape Shape;
        public Sprite Sprite;
    }

    [Serializable]
    public struct ColorSprite
    {
        public TileColor Color;
        public Sprite Sprite;
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
    [SerializeField] private Tile _tilePrefab;
    [SerializeField] private TargetMarker _targetMarkerPrefab;

    [Header("Tiles")]
    [Tooltip("One pre-rendered sprite per shape × color. Levels only use shapes and colors that have every combination.")]
    [SerializeField] private TileSprite[] _tileSprites = new TileSprite[0];

    [Header("Targets")]
    [SerializeField] private ShapeSprite[] _rowTargetSprites = new ShapeSprite[0];
    [SerializeField] private ColorSprite[] _columnTargetSprites = new ColorSprite[0];

    [Header("Tutorial cues (Manager_Tutorial)")]
    [SerializeField] private Sprite _tutorialHand;
    [SerializeField] private Sprite _tutorialLineOutline;
    [SerializeField] private Sprite _tutorialDestination;
    [SerializeField] private Sprite _tutorialArrow;

    [Header("Audio")]
    [SerializeField] private AudioClip _music;
    [SerializeField] private SoundData[] _sounds = new SoundData[0];

    public Cell CellPrefab => _cellPrefab;
    public Tile TilePrefab => _tilePrefab;
    public TargetMarker TargetMarkerPrefab => _targetMarkerPrefab;
    public Sprite TutorialHand => _tutorialHand;
    public Sprite TutorialLineOutline => _tutorialLineOutline;
    public Sprite TutorialDestination => _tutorialDestination;
    public Sprite TutorialArrow => _tutorialArrow;
    public AudioClip Music => _music;

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

    public Sprite GetTileSprite(TileData data)
    {
        foreach (var entry in _tileSprites)
            if (entry.Shape == data.Shape && entry.Color == data.Color) return entry.Sprite;
        Debug.LogError($"[FileRef] No tile sprite for {data}.", this);
        return null;
    }

    public Sprite GetRowTargetSprite(TileShape shape)
    {
        foreach (var entry in _rowTargetSprites)
            if (entry.Shape == shape) return entry.Sprite;
        Debug.LogError($"[FileRef] No row target sprite for {shape}.", this);
        return null;
    }

    public Sprite GetColumnTargetSprite(TileColor color)
    {
        foreach (var entry in _columnTargetSprites)
            if (entry.Color == color) return entry.Sprite;
        Debug.LogError($"[FileRef] No column target sprite for {color}.", this);
        return null;
    }

    // Shapes that have a tile for every playable color and a row target sprite.
    public TileShape[] PlayableShapes
    {
        get
        {
            var colors = TileColorsInTable();
            var result = new List<TileShape>();
            foreach (TileShape shape in Enum.GetValues(typeof(TileShape)))
            {
                if (!HasRowTarget(shape)) continue;
                bool complete = colors.Count > 0;
                foreach (var color in colors) complete &= HasTile(shape, color);
                if (complete) result.Add(shape);
            }
            return result.ToArray();
        }
    }

    // Colors that have a tile for every playable shape and a column target sprite.
    public TileColor[] PlayableColors
    {
        get
        {
            var shapes = PlayableShapes;
            var result = new List<TileColor>();
            foreach (TileColor color in Enum.GetValues(typeof(TileColor)))
            {
                if (!HasColumnTarget(color)) continue;
                bool complete = shapes.Length > 0;
                foreach (var shape in shapes) complete &= HasTile(shape, color);
                if (complete) result.Add(color);
            }
            return result.ToArray();
        }
    }

    private List<TileColor> TileColorsInTable()
    {
        var colors = new List<TileColor>();
        foreach (var entry in _tileSprites)
            if (entry.Sprite != null && HasColumnTarget(entry.Color) && !colors.Contains(entry.Color))
                colors.Add(entry.Color);
        return colors;
    }

    private bool HasTile(TileShape shape, TileColor color)
    {
        foreach (var entry in _tileSprites)
            if (entry.Shape == shape && entry.Color == color && entry.Sprite != null) return true;
        return false;
    }

    private bool HasRowTarget(TileShape shape)
    {
        foreach (var entry in _rowTargetSprites)
            if (entry.Shape == shape && entry.Sprite != null) return true;
        return false;
    }

    private bool HasColumnTarget(TileColor color)
    {
        foreach (var entry in _columnTargetSprites)
            if (entry.Color == color && entry.Sprite != null) return true;
        return false;
    }

    [ContextMenu("Validate references")]
    private void ValidateReferences()
    {
        int problems = 0;
        if (_cellPrefab == null) { Debug.LogError("[FileRef] Cell prefab missing.", this); problems++; }
        if (_tilePrefab == null) { Debug.LogError("[FileRef] Tile prefab missing.", this); problems++; }
        if (_targetMarkerPrefab == null) { Debug.LogError("[FileRef] Target marker prefab missing.", this); problems++; }
        if (_music == null) { Debug.LogWarning("[FileRef] No music clip.", this); }
        foreach (SoundType type in Enum.GetValues(typeof(SoundType)))
            if (!TryGetSound(type, out _)) Debug.LogWarning($"[FileRef] No clip for sound {type}.", this);

        var shapes = PlayableShapes;
        var colors = PlayableColors;
        if (Mathf.Min(shapes.Length, colors.Length) < 2)
        {
            Debug.LogError("[FileRef] Fewer than 2 playable shapes or colors: fill the tile and target tables.", this);
            problems++;
        }

        if (problems == 0)
            Debug.Log($"[FileRef] OK. Playable shapes: {string.Join(", ", shapes)}. " +
                      $"Playable colors: {string.Join(", ", colors)}. Max board {Mathf.Min(shapes.Length, colors.Length)}x.", this);
    }
}
