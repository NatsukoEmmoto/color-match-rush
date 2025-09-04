using System.Collections.Generic;
using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Handles deadboard detection (HasAnyValidMove) and board shuffling.
    /// Stateless: operates only on provided grid and delegates match checks.
    /// </summary>
    public class ShuffleService
    {
        private readonly MatchFinder matchFinder = new MatchFinder();

        /// <summary>
        /// Return true if there exists at least one adjacent swap that would create a match.
        /// </summary>
        public bool HasAnyValidMove(Piece[,] grid)
        {
            if (grid == null) return false;
            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    var a = grid[r, c];
                    if (a == null) continue;

                    // Right neighbor
                    if (c + 1 < w && grid[r, c + 1] != null)
                        if (SwapWouldCreateMatch(grid, r, c, r, c + 1)) return true;

                    // Up neighbor
                    if (r + 1 < h && grid[r + 1, c] != null)
                        if (SwapWouldCreateMatch(grid, r, c, r + 1, c)) return true;
                }
            }
            return false;
        }

        private bool SwapWouldCreateMatch(Piece[,] grid, int r1, int c1, int r2, int c2)
        {
            var p1 = grid[r1, c1];
            var p2 = grid[r2, c2];
            if (p1 == null || p2 == null) return false;

            // swap in place (no animation)
            grid[r1, c1] = p2; grid[r2, c2] = p1;

            bool created =
                matchFinder.CreatesMatchAt(grid, r1, c1) ||
                matchFinder.CreatesMatchAt(grid, r2, c2);

            // revert
            grid[r1, c1] = p1; grid[r2, c2] = p2;
            return created;
        }

        /// <summary>
        /// Shuffle the existing pieces randomly across the grid and animate them to their new cells.
        /// Ensures the result is not an instant-match board and (optionally) has at least one valid move.
        /// </summary>
        public bool ShuffleBoard(
            Piece[,] grid,
            System.Func<int,int,Vector3> cellToWorld,
            float moveDuration,
            int maxAttempts = 20,
            bool requireValidMove = true)
        {
            if (grid == null) return false;

            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            // Collect current non-null pieces
            var pieces = new List<Piece>(h * w);
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    if (grid[r, c] != null) pieces.Add(grid[r, c]);

            if (pieces.Count <= 1) return false;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Fisher–Yates shuffle
                for (int i = pieces.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (pieces[i], pieces[j]) = (pieces[j], pieces[i]);
                }

                // Place into grid
                int idx = 0;
                for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                {
                    var p = (idx < pieces.Count) ? pieces[idx++] : null;
                    grid[r, c] = p;
                    if (p != null) p.SetGridIndex(r, c);
                }

                // Ensure not starting with matches
                var matches = matchFinder.FindAllMatches(grid);
                if (matches != null && matches.Count > 0)
                    continue;

                if (requireValidMove && !HasAnyValidMove(grid))
                    continue;

                // Success: animate
                for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                {
                    var p = grid[r, c];
                    if (p != null)
                        p.MoveTo(cellToWorld(r, c), moveDuration);
                }
                return true;
            }

            Debug.LogWarning("[ShuffleService] ShuffleBoard() fell back after max attempts; applying last layout anyway.");
            for (int r = 0; r < grid.GetLength(0); r++)
            for (int c = 0; c < grid.GetLength(1); c++)
            {
                var p = grid[r, c];
                if (p != null)
                    p.MoveTo(cellToWorld(r, c), moveDuration);
            }
            return true;
        }
    }
}