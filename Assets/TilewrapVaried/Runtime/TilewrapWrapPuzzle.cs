using System;
using System.Collections.Generic;

namespace Chichingo.TilewrapVaried
{
    public static class TilewrapWrapPuzzle
    {
        private static readonly int[] Factorial = { 1, 1, 2, 6, 24, 120, 720, 5040, 40320, 362880 };
        public static string[] ApplyMove(string[] board, MoveData move)
        {
            ValidateMove(board, move);
            var result = (string[])board.Clone();
            bool row = move.axis == "row";
            for (int i = 0; i < 3; i++)
            {
                int source = row ? move.line * 3 + i : i * 3 + move.line;
                int destLine = (i + move.direction + 3) % 3;
                int dest = row ? move.line * 3 + destLine : destLine * 3 + move.line;
                result[dest] = board[source];
            }
            return result;
        }
        private static void ValidateMove(string[] board, MoveData move)
        {
            if (board == null || board.Length != 9 || move == null || move.line < 0 || move.line >= 3 ||
                (move.direction != -1 && move.direction != 1) || (move.axis != "row" && move.axis != "column"))
                throw new ArgumentException("Invalid board or wrap move.");
        }
        public static bool IsWon(string[] board, string[] goal)
        {
            if (board == null || goal == null || board.Length != 9 || goal.Length != 9) return false;
            for (int i = 0; i < 9; i++) if (!string.Equals(board[i], goal[i], StringComparison.Ordinal)) return false;
            return true;
        }
        private static int Rank(string[] board, string[] goal)
        {
            var normalized = new int[9];
            var seen = new HashSet<int>();
            for (int i = 0; i < 9; i++)
            {
                normalized[i] = Array.IndexOf(goal, board[i]);
                if (normalized[i] < 0 || !seen.Add(normalized[i])) throw new ArgumentException("Board must be a permutation of the goal.");
            }
            int rank = 0;
            for (int i = 0; i < 9; i++)
            {
                int smaller = 0;
                for (int j = i + 1; j < 9; j++) if (normalized[j] < normalized[i]) smaller++;
                rank += smaller * Factorial[8 - i];
            }
            return rank;
        }
        // The small precomputed table avoids a search or frame stall on a mobile device.
        // All cases normalize IDs against their own goal; no character/color convention is used.
        public static bool TryNextMove(string[] board, string[] goal, byte[] distances, out MoveData next)
        {
            next = null;
            if (board == null || goal == null || board.Length != 9 || goal.Length != 9 || distances == null || distances.Length != 362880)
                throw new ArgumentException("Invalid hint input or missing hint table.");
            int current = distances[Rank(board, goal)];
            if (current == 0 || current == 255) return false;
            foreach (string axis in new[] { "row", "column" })
                for (int line = 0; line < 3; line++)
                    foreach (int direction in new[] { -1, 1 })
                    {
                        var move = new MoveData { axis = axis, line = line, direction = direction };
                        var changed = ApplyMove(board, move);
                        if (distances[Rank(changed, goal)] == current - 1)
                        {
                            next = move;
                            return true;
                        }
                    }
            throw new InvalidOperationException("Corrupt hint table: reachable state has no reducing move.");
        }
    }
}
