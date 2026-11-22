using System;

// Append-only: these enums are saved / serialized by ordinal. Never renumber or reuse a value.
public enum GameState { Loading = 0, Playing = 1, Won = 2, Failed = 3 }

// Strength of a vibration pulse (Haptics.Play). Not serialized.
public enum HapticType { Light = 0, Medium = 1, Heavy = 2 }

// Stored by ordinal in FileRef's sound table. Audio file name = snake_case of the name
// (TileMatch -> tile_match.wav, or tile_match_01.wav, tile_match_02.wav... for variants).
// TileMatch is no longer played (a per-tile sound would give the answer away), but keeps its ordinal.
public enum SoundType { UiTap = 0, SwipeSettle = 1, TileMatch = 2, LineComplete = 3, LevelComplete = 4, OutOfMoves = 5, Restart = 6, Undo = 7 }

// A tile is known by the cell it belongs in when the board is solved: Id = row * size + col.
// What it shows (item art and name) comes from LevelDefinition.ItemFor(tile).
[Serializable]
public struct TileData : IEquatable<TileData>
{
    public int Id;

    public TileData(int id) => Id = id;

    public int HomeRow(int size) => Id / size;
    public int HomeColumn(int size) => Id % size;

    public bool Equals(TileData other) => Id == other.Id;
    public override bool Equals(object obj) => obj is TileData other && Equals(other);
    public override int GetHashCode() => Id;
    public override string ToString() => $"#{Id}";
}

// Sorting orders on the Default layer: tiles, then the row outline and frame over them, then checks
// and the category tags (which overlap the frame's left edge).
public static class SortOrder
{
    public const int Board = 0;
    public const int TileBody = 4;      // ItemTile surface
    public const int TileItem = 5;      // ItemTile item art
    public const int TileLabel = 6;     // ItemTile name
    public const int ActiveRow = 7;     // outline around the active step's row
    public const int Frame = 8;         // wooden frame, a foreground layer over the tiles
    public const int TileCheck = 9;     // check on a confirmed row's cells
    public const int Tag = 10;          // category tag (Hats / Gear / Bags)
    public const int TagLabel = 11;
}
