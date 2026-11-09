using System;
using UnityEngine;

// Generates a level by scrambling a solved board with random swipes, so every level is solvable.
// Seeded by level number: the same level always comes out the same.
public class ProceduralLevelSource : ILevelSource
{
    private readonly TileShape[] _shapes;
    private readonly TileColor[] _colors;

    // Only these shapes and colors are used (the ones that have art).
    public ProceduralLevelSource(TileShape[] shapes, TileColor[] colors)
    {
        _shapes = shapes;
        _colors = colors;
    }

    public LevelDefinition Load(int levelNumber)
    {
        var rng = new System.Random(levelNumber * 7919 + 17);

        int maxSize = Mathf.Min(Tuning.MaxBoardSize, _shapes.Length, _colors.Length);
        int minSize = Mathf.Min(Tuning.MinBoardSize, maxSize);
        int size = Mathf.Clamp(Tuning.MinBoardSize + (levelNumber - 1) / Tuning.LevelsPerSizeStep, minSize, maxSize);

        var rowTargets = PickDistinct(rng, size, _shapes);
        var columnTargets = PickDistinct(rng, size, _colors);

        var board = BoardState.Solved(size, rowTargets, columnTargets);
        Scramble(board, rng, size * Tuning.ScrambleMovesPerSize);

        return new LevelDefinition(levelNumber, size, rowTargets, columnTargets, board.CopyCells(), moveLimit: 0);
    }

    private static void Scramble(BoardState board, System.Random rng, int moves)
    {
        bool lastWasRow = false;
        int lastIndex = -1, lastDir = 0;

        for (int i = 0; i < moves || board.IsSolved; i++)
        {
            bool isRow = rng.Next(2) == 0;
            int index = rng.Next(board.Size);
            int dir = rng.Next(2) == 0 ? -1 : 1;

            // Don't immediately undo the previous swipe.
            if (isRow == lastWasRow && index == lastIndex && dir == -lastDir)
            {
                i--;
                continue;
            }

            if (isRow) board.ShiftRow(index, dir);
            else board.ShiftColumn(index, dir);

            lastWasRow = isRow;
            lastIndex = index;
            lastDir = dir;
        }
    }

    private static T[] PickDistinct<T>(System.Random rng, int count, T[] source)
    {
        var pool = (T[])source.Clone();
        for (int i = pool.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        var result = new T[count];
        Array.Copy(pool, result, count);
        return result;
    }
}
