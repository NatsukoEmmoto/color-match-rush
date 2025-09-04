using System.Collections.Generic;
using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Builds an initial board and (optionally) avoids instant matches.
    /// Stateless: all data provided via parameters.
    /// </summary>
    public class BoardGenerator
    {
        /// <summary>
        /// Generate a fresh board (width x height) using given prefabs.
        /// If avoidInstantMatches is true, tries to ensure no initial 3+ runs exist.
        /// </summary>
        public void GenerateBoard(
            out Piece[,] grid,
            int width,
            int height,
            Piece[] piecePrefabs,
            Transform piecesRoot,
            System.Func<int,int,Vector3> cellToWorld,
            bool avoidInstantMatches,
            int maxInstantMatchRegenerations)
        {
            // First pass
            grid = BuildGrid(width, height, piecePrefabs, piecesRoot, cellToWorld, avoidInstantMatches);

            // Safety pass: try a few regenerations if any matches slipped through
            if (avoidInstantMatches)
            {
                for (int i = 0; i < maxInstantMatchRegenerations; i++)
                {
                    var matches = FindAllMatches(grid);
                    if (matches == null || matches.Count == 0) return;

                    Debug.LogWarning($"[BoardGenerator] Instant matches detected at start. Regenerating (attempt {i + 1}/{maxInstantMatchRegenerations})...");
                    // Rebuild with avoidance
                    ClearChildren(piecesRoot);
                    grid = BuildGrid(width, height, piecePrefabs, piecesRoot, cellToWorld, avoidInstantMatches: true);
                }
            }
        }

        private Piece[,] BuildGrid(
            int width,
            int height,
            Piece[] piecePrefabs,
            Transform piecesRoot,
            System.Func<int,int,Vector3> cellToWorld,
            bool avoidInstantMatches)
        {
            var grid = new Piece[height, width];

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    Piece prefab = avoidInstantMatches
                        ? GetRandomPrefabAvoidingInstantMatch(grid, row, col, piecePrefabs)
                        : GetRandomPiecePrefab(piecePrefabs);

                    if (prefab == null)
                    {
                        Debug.LogError($"[BoardGenerator] Failed to get valid prefab for ({row},{col}). Skipping.");
                        continue;
                    }

                    var piece = Object.Instantiate(prefab, piecesRoot);
                    Vector3 worldPos = cellToWorld(row, col);
                    piece.Initialize(row, col, prefab.Type, worldPos);
                    grid[row, col] = piece;
                }
            }

            return grid;
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.Destroy(root.GetChild(i).gameObject);
        }

        // === Helpers (ported from BoardManager; no behavior change) ===

        private Piece GetRandomPiecePrefab(Piece[] piecePrefabs)
        {
            if (piecePrefabs == null || piecePrefabs.Length == 0)
            {
                Debug.LogError("[BoardGenerator] piecePrefabs not assigned or empty.");
                return null;
            }

            int index = Random.Range(0, piecePrefabs.Length);
            Piece prefab = piecePrefabs[index];

            if (prefab == null)
            {
                Debug.LogError($"[BoardGenerator] piecePrefabs[{index}] is null (check Inspector).");
                return null;
            }

            return prefab;
        }

        /// <summary>
        /// Pick a random prefab that does NOT cause an instant 3-match at (row,col).
        /// </summary>
        private Piece GetRandomPrefabAvoidingInstantMatch(Piece[,] grid, int row, int col, Piece[] piecePrefabs)
        {
            if (piecePrefabs == null || piecePrefabs.Length == 0)
            {
                Debug.LogError("[BoardGenerator] piecePrefabs not assigned or empty.");
                return null;
            }

            const int MaxAttemptsToAvoidInstantMatch = 12;
            Piece lastTried = null;

            for (int attempt = 0; attempt < MaxAttemptsToAvoidInstantMatch; attempt++)
            {
                int idx = Random.Range(0, piecePrefabs.Length);
                var prefab = piecePrefabs[idx];
                if (prefab == null) continue;
                lastTried = prefab;
                if (!WouldCreateInstantMatchAt(grid, row, col, prefab.Type))
                    return prefab;
            }

            if (lastTried == null)
                Debug.LogError("[BoardGenerator] Failed to choose a valid prefab (all null?).");
            else
                Debug.LogWarning($"[BoardGenerator] Fallback to potentially matching prefab at ({row},{col}).");

            return lastTried;
        }

        /// <summary>
        /// Returns true if placing a type at (row,col) would immediately create a 3+ run.
        /// Checks only left and down directions (already filled cells).
        /// </summary>
        private bool WouldCreateInstantMatchAt(Piece[,] grid, int row, int col, Piece.PieceType type)
        {
            // Horizontal: left two
            if (col >= 2)
            {
                var p1 = grid[row, col - 1];
                var p2 = grid[row, col - 2];
                if (p1 != null && p2 != null && p1.Type == type && p2.Type == type)
                    return true;
            }

            // Vertical: down two
            if (row >= 2)
            {
                var p1 = grid[row - 1, col];
                var p2 = grid[row - 2, col];
                if (p1 != null && p2 != null && p1.Type == type && p2.Type == type)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Minimal in-place match scanner (duplicated logic of FindAllMatches for boot-time safety).
        /// </summary>
        private HashSet<Piece> FindAllMatches(Piece[,] grid)
        {
            var result = new HashSet<Piece>();
            if (grid == null) return result;

            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            // Horizontal scan
            for (int r = 0; r < h; r++)
            {
                int c = 0;
                while (c < w)
                {
                    var start = grid[r, c];
                    if (start == null) { c++; continue; }

                    int runStart = c;
                    int runLen = 1;

                    while (c + runLen < w)
                    {
                        var next = grid[r, c + runLen];
                        if (next == null || next.Type != start.Type) break;
                        runLen++;
                    }

                    if (runLen >= 3)
                        for (int k = 0; k < runLen; k++)
                            result.Add(grid[r, runStart + k]);

                    c += runLen;
                }
            }

            // Vertical scan
            for (int c = 0; c < w; c++)
            {
                int r = 0;
                while (r < h)
                {
                    var start = grid[r, c];
                    if (start == null) { r++; continue; }

                    int runStart = r;
                    int runLen = 1;

                    while (r + runLen < h)
                    {
                        var next = grid[r + runLen, c];
                        if (next == null || next.Type != start.Type) break;
                        runLen++;
                    }

                    if (runLen >= 3)
                        for (int k = 0; k < runLen; k++)
                            result.Add(grid[runStart + k, c]);

                    r += runLen;
                }
            }

            return result;
        }
    }
}