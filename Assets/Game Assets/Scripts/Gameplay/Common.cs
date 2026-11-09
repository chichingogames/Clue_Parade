using System;

// Append-only: these enums are saved / serialized by ordinal. Never renumber or reuse a value.
public enum GameState { Loading = 0, Playing = 1, Won = 2, Failed = 3 }

// Only values with art in FileRef are used in levels (see FileRef.PlayableShapes / PlayableColors).
public enum TileShape { Circle = 0, Square = 1, Triangle = 2, Diamond = 3, Hexagon = 4, Star = 5 }

public enum TileColor { Coral = 0, Teal = 1, Yellow = 2, Blue = 3, Purple = 4 }

[Serializable]
public struct TileData
{
    public TileShape Shape;
    public TileColor Color;

    public TileData(TileShape shape, TileColor color)
    {
        Shape = shape;
        Color = color;
    }

    public override string ToString() => $"{Color} {Shape}";
}

public static class SortOrder
{
    public const int Board = 0;
    public const int Slot = 1;
    public const int Marker = 1;
    public const int MarkerIcon = 2;
    public const int TileBody = 4;
    public const int TileCheck = 5;
}
