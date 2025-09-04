using System;
using System.Collections;
using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Handles gravity (collapse), refill (spawn from top), and waiting
    /// until all pieces stop moving. Stateless: all data via parameters.
    /// </summary>
    public class GravityRefill
    {
        /// <summary>
        /// Collapse all columns downward using a write-pointer per column.
        /// Returns true if any piece moved.
        /// </summary>
        public bool CollapseColumnsDownward(
            Piece[,] grid,
            Func<int,int,Vector3> cellToWorld,
            float moveDuration)
        {
            if (grid == null) return false;

            int h = grid.GetLength(0);
            int w = grid.GetLength(1);
            bool anyMoved = false;

            for (int c = 0; c < w; c++)
            {
                int write = 0; // next row to fill in this column (from bottom)
                for (int r = 0; r < h; r++)
                {
                    var piece = grid[r, c];
                    if (piece == null) continue;

                    if (r != write)
                    {
                        grid[write, c] = piece;
                        grid[r, c] = null;

                        piece.SetGridIndex(write, c);
                        piece.MoveTo(cellToWorld(write, c), moveDuration);

                        anyMoved = true;
                    }
                    write++;
                }
            }
            return anyMoved;
        }

        /// <summary>
        /// Refill empty cells by spawning new pieces above the top row and animating them down.
        /// Returns true if any piece was spawned.
        /// </summary>
        public bool RefillNewPiecesFromTop(
            Piece[,] grid,
            Func<Piece> getRandomPiecePrefab,
            Transform piecesRoot,
            Func<int,int,Vector3> cellToWorld,
            float cellSize,
            float spawnOvershootCells,
            float moveDuration)
        {
            if (grid == null) return false;

            int h = grid.GetLength(0);
            int w = grid.GetLength(1);
            bool anySpawned = false;

            for (int c = 0; c < w; c++)
            {
                for (int r = h - 1; r >= 0; r--)
                {
                    if (grid[r, c] != null) continue;

                    // pick a random prefab
                    Piece prefab = getRandomPiecePrefab();
                    if (prefab == null) continue;

                    // spawn slightly above the target cell and fall down
                    Vector3 targetWorld = cellToWorld(r, c);
                    Vector3 startWorld = targetWorld + new Vector3(0f, cellSize * spawnOvershootCells, 0f);

                    Piece piece = UnityEngine.Object.Instantiate(prefab, piecesRoot);
                    piece.Initialize(r, c, prefab.Type, startWorld);

                    grid[r, c] = piece;
                    piece.MoveTo(targetWorld, moveDuration);

                    anySpawned = true;
                }
            }
            return anySpawned;
        }

        /// <summary>
        /// Wait until all pieces in the grid finish moving.
        /// </summary>
        public IEnumerator WaitUntilAllPiecesStop(Piece[,] grid)
        {
            if (grid == null) yield break;

            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            while (true)
            {
                bool anyMoving = false;

                for (int r = 0; r < h && !anyMoving; r++)
                {
                    for (int c = 0; c < w && !anyMoving; c++)
                    {
                        var p = grid[r, c];
                        if (p != null && p.IsMoving)
                            anyMoving = true;
                    }
                }

                if (!anyMoving) yield break;
                yield return null;
            }
        }
    }
}