using System;
using System.Collections;
using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Handles adjacency check and swap coroutine.
    /// Stateless: all state is provided via parameters.
    /// </summary>
    public class SwapService
    {
        /// <summary>
        /// Returns true if two pieces are orthogonally adjacent.
        /// </summary>
        public bool AreAdjacent(Piece a, Piece b)
        {
            if (a == null || b == null) return false;
            int dr = Mathf.Abs(a.Row - b.Row);
            int dc = Mathf.Abs(a.Column - b.Column);
            return (dr == 1 && dc == 0) || (dr == 0 && dc == 1);
        }

        /// <summary>
        /// Performs a swap attempt with animation and resolution hook.
        /// Keeps behavior identical to the previous BoardManager coroutine.
        /// </summary>
        public IEnumerator TrySwapCoroutine(
            Piece[,] grid,
            Piece a,
            Piece b,
            Func<int,int,Vector3> cellToWorld,
            float swapMoveDuration,
            Func<int,int,bool> createsMatchAt,          // (row,col) -> bool
            Func<IEnumerator> resolveBoardLoop,         // resolution loop to run on success
            Action onResolveStart,                      // pause timer / set resolving=true
            Action onResolveEnd,                        // resume timer / set resolving=false
            Action onUnlockInput,                       // input unlock callback
            Func<Piece,Piece,IEnumerator> waitUntilPiecesStop // wait for both pieces to complete their movement animations
        )
        {
            // Cache original indices
            int ar = a.Row, ac = a.Column;
            int br = b.Row, bc = b.Column;

            // Swap in grid + indices
            grid[ar, ac] = b; grid[br, bc] = a;
            a.SetGridIndex(br, bc); b.SetGridIndex(ar, ac);

            // Animate to new positions
            a.MoveTo(cellToWorld(a.Row, a.Column), swapMoveDuration);
            b.MoveTo(cellToWorld(b.Row, b.Column), swapMoveDuration);
            yield return waitUntilPiecesStop(a, b);

            // Check local matches around both pieces
            bool matched = createsMatchAt(a.Row, a.Column) || createsMatchAt(b.Row, b.Column);

            if (!matched)
            {
                // Revert to original indices (ar,ac) / (br,bc)
                grid[ar, ac] = a;
                grid[br, bc] = b;

                a.SetGridIndex(ar, ac);
                b.SetGridIndex(br, bc);

                a.MoveTo(cellToWorld(ar, ac), swapMoveDuration);
                b.MoveTo(cellToWorld(br, bc), swapMoveDuration);
                
                yield return waitUntilPiecesStop(a, b);

                onUnlockInput?.Invoke();
                yield break;
            }

            // === Start resolution phase ===
            onResolveStart?.Invoke();

            // Run resolution loop (cascades etc.)
            if (resolveBoardLoop != null)
                yield return resolveBoardLoop();

            // === End resolution phase ===
            onResolveEnd?.Invoke();

            onUnlockInput?.Invoke();
        }
    }
}