using System.Collections.Generic;
using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Responsible for holding board dimensions, piece prefabs and the grid array.
    /// Generates an initial board of pieces and provides helpers for grid<->world conversion.
    /// </summary>
    public class BoardManager : MonoBehaviour
    {
        // === Config ===
        [SerializeField] private BoardConfig boardConfig;
        
        // === References ===
        [Header("Prefabs & Root")]
        [SerializeField] private Piece[] piecePrefabs; // Expected order: Red, Blue, Green, Yellow, Purple
        [SerializeField] private Transform piecesRoot;  // Parent for instantiated pieces

        // === State ===
        private Piece[,] grid;
        
        // === Services ===
        private readonly MatchFinder matchFinder = new MatchFinder();
        private readonly GravityRefill gravityRefill = new GravityRefill();
        private readonly ShuffleService shuffleService = new ShuffleService();
        private readonly BoardGenerator boardGenerator = new BoardGenerator();
        private readonly SwapService swapService = new SwapService();
        private readonly BoardLayout layout = new BoardLayout();   

        // === Public API ===
        public int Width => boardConfig.Width;
        public int Height => boardConfig.Height;
        public float CellSize => boardConfig.CellSize;
        public Piece[,] Grid => grid;
        

        #region Lifecycle

        private void Awake()
        {
            // Ensure boardRoot exists
            if (piecesRoot == null)
            {
                var root = new GameObject("BoardRoot");
                piecesRoot = root.transform;
                piecesRoot.SetParent(transform, worldPositionStays: false);
            }
            if (boardConfig == null)
            {
                Debug.LogError("[BoardManager] BoardConfig is not assigned. Disabling BoardManager.");
                enabled = false;
                return;
            }
            if (inputController == null)
            {
                Debug.LogWarning("[BoardManager] InputController is not assigned. Trying auto-find...");
                inputController = FindObjectOfType<InputController>();
                if (inputController == null)
                {
                    Debug.LogError("[BoardManager] InputController not found. Disabling BoardManager.");
                    enabled = false;
                    return;
                }
            }
        }


        private void Start()
        {
            if (boardConfig.GenerateOnStart)
            {
                GenerateBoard();
            }
        }

        #endregion

        #region Generation & Layout
        /// <summary>
        /// Generate a fresh board with random pieces using the configured size.
        /// Clears existing children and recreates grid and pieces.
        /// </summary>
        public void GenerateBoard()
        {
            if (boardConfig.RandomSeed != 0)
                Random.InitState(boardConfig.RandomSeed);

            ComputeOrigin();
            ClearBoardImmediate();

            boardGenerator.GenerateBoard(
                out grid,
                boardConfig.Width,
                boardConfig.Height,
                piecePrefabs,
                piecesRoot,
                CellToWorld,
                boardConfig.PreventInstantMatchesOnStart,
                boardConfig.MaxInstantMatchRegenerations
            );
        }

        /// <summary>
        /// Destroy all children under boardRoot and reset grid reference.
        /// </summary>
        public void ClearBoardImmediate()
        {
            for (int i = piecesRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(piecesRoot.GetChild(i).gameObject);
            }
            grid = null;
        }

        /// <summary>
        /// Convert grid coordinates (row, column) to world-space position.
        /// Row 0 is bottom, Column 0 is left.
        /// </summary>
        public Vector3 CellToWorld(int row, int column)
        {
            return layout.CellToWorld(row, column, boardConfig.CellSize);
        }

        /// <summary>
        /// Convert world-space position to nearest grid indices.
        /// Clamps to board bounds.
        /// </summary>
        public void WorldToCell(Vector3 world, out int row, out int column)
        {
            layout.WorldToCell(world, boardConfig.CellSize, boardConfig.Width, boardConfig.Height, out row, out column);
        }

        /// <summary>
        /// Compute bottom-left origin for the current layout.
        /// </summary>
        private void ComputeOrigin()
        {
            layout.ComputeOrigin(boardConfig.Width, boardConfig.Height, boardConfig.CellSize, boardConfig.AutoCenter, boardConfig.ExplicitOrigin);
        }

        /// <summary>
        /// Pick a random piece prefab from the configured array.
        /// </summary>
        private Piece GetRandomPiecePrefab()
        {
            if (piecePrefabs == null || piecePrefabs.Length == 0)
            {
                Debug.LogError("[BoardManager] piecePrefabs not assigned or empty.");
                enabled = false;
                return null;
            }

            int index = Random.Range(0, piecePrefabs.Length);
            Piece prefab = piecePrefabs[index];

            if (prefab == null)
            {
                Debug.LogError($"[BoardManager] piecePrefabs[{index}] is null (check Inspector).");
                return null;
            }

            return prefab;
        }

        #endregion
        #region Swap
        private bool swapInProgress = false;
        private bool isResolving = false;
        public bool IsResolving => isResolving;

        [SerializeField] private InputController inputController;

        public bool TrySwap(Piece a, Piece b)
        {
            if (isResolving) return false;
            if (swapInProgress) return false;
            if (a == null || b == null || a == b) return false;
            if (!swapService.AreAdjacent(a, b)) return false;

            swapInProgress = true;
            if (inputController != null) inputController.SetInputLock(true);

            // Start service coroutine (keep hooks and timing as before)
            StartCoroutine(
                swapService.TrySwapCoroutine(
                    grid,
                    a, b,
                    CellToWorld,
                    boardConfig.SwapMoveDuration,
                    (row, col) => CreatesMatchAt(row, col),
                    () => ResolveBoardLoop(),
                    () => { isResolving = true; GameController.Instance?.PauseTimer(); },
                    () => { isResolving = false; GameController.Instance?.StartTimer(); },
                    () => { UnlockInput(); swapInProgress = false; },
                    (pa, pb) => WaitUntilPiecesStop(pa, pb)
                )
            );

            return true;
        }

        private System.Collections.IEnumerator WaitUntilPiecesStop(Piece a, Piece b)
        {
            // simple wait-until both finish their MoveTo
            while ((a != null && a.IsMoving) || (b != null && b.IsMoving))
                yield return null;
        }

        // Returns true if there is a 3+ line including cell (row,col)
        private bool CreatesMatchAt(int row, int column)
        {
            if (grid == null) return false;
            return matchFinder.CreatesMatchAt(grid, row, column);
        }

        private void UnlockInput()
        {
            if (inputController != null)
                inputController.SetInputLock(false);
        }
        #endregion

        #region Matching

        private bool IsInBounds(int row, int col)
        {
            return grid != null &&
                   row >= 0 && row < grid.GetLength(0) &&
                   col >= 0 && col < grid.GetLength(1);
        }

        public HashSet<Piece> FindAllMatches()
        {
            return matchFinder.FindAllMatches(grid);
        }
        
        /// <summary>
        /// Find all matches, destroy matched pieces, and clear their grid cells.
        /// </summary>
        public int RemoveMatches()
        {
            var matches = FindAllMatches();
            if (matches == null || matches.Count == 0) return 0;

            int removed = 0;
            int h = grid?.GetLength(0) ?? 0;
            int w = grid?.GetLength(1) ?? 0;

            foreach (var piece in matches)
            {
                if (piece == null) continue;

                bool cleared = false;

                // Try by declared indices first
                int r = piece.Row, c = piece.Column;
                if (grid != null && IsInBounds(r, c))
                {
                    if (grid[r, c] == null)
                    {
                        Debug.LogWarning($"[BoardManager] Attempting to clear an already-null cell at ({r},{c}).");
                        cleared = true; // cell is already clear; safe to destroy
                    }
                    else if (grid[r, c] == piece)
                    {
                        grid[r, c] = null;
                        cleared = true; // successfully cleared by declared indices
                    }
                }

                // Fallback: locate exact instance in the grid
                if (!cleared && grid != null)
                {
                    for (int rr = 0; rr < h && !cleared; rr++)
                    {
                        for (int cc = 0; cc < w && !cleared; cc++)
                        {
                            if (grid[rr, cc] == piece)
                            {
                                grid[rr, cc] = null;
                                cleared = true;
                            }
                        }
                    }
#if UNITY_EDITOR
                    if (!cleared)
                        Debug.LogWarning($"[BoardManager] Matched piece not found in grid (id={piece.GetInstanceID()}).");
#endif
                }

                if (cleared || grid == null)
                {
                    Destroy(piece.gameObject);
                    removed++;
                }
#if UNITY_EDITOR
                else
                {
                    Debug.LogWarning($"[BoardManager] Skip destroying piece (id={piece.GetInstanceID()}) because grid reference wasn't cleared.");
                }
#endif
            }

            return removed;
        }
        #endregion

        #region Gravity & Refill


        /// <summary>
        /// Collapse all columns downward using a write-pointer per column.
        /// Returns true if any piece moved.
        /// </summary>
        public bool CollapseColumnsDownward()
        {
            return gravityRefill.CollapseColumnsDownward(grid, CellToWorld, boardConfig.FallMoveDuration);
        }
        
        /// <summary>
        /// Refill empty cells by spawning new pieces above the top row and animating them down.
        /// Returns true if any piece was spawned.
        /// </summary>
        public bool RefillNewPiecesFromTop()
        {
            return gravityRefill.RefillNewPiecesFromTop(
                grid,
                GetRandomPiecePrefab,
                piecesRoot,
                CellToWorld,
                boardConfig.CellSize,
                (float)boardConfig.SpawnOvershootCells,
                boardConfig.FallMoveDuration);
        }

        /// <summary>
        /// Wait until all pieces in the grid finish moving.
        /// </summary>
        private System.Collections.IEnumerator WaitUntilAllPiecesStop()
        {
            return gravityRefill.WaitUntilAllPiecesStop(grid);
        }

        #endregion

        #region Shuffle

        #region Resolve
        /// <summary>
        /// Return true if there exists at least one adjacent swap that would create a match.
        /// We simulate a swap in the grid (no animation), check, then revert.
        /// </summary>
        public bool HasAnyValidMove()
        {
            return shuffleService.HasAnyValidMove(grid);
        }

        /// <summary>
        /// Shuffle the existing pieces randomly across the grid and animate them to their new cells.
        /// Ensures the result is not an instant-match board and (optionally) has at least one valid move.
        /// Returns true if a shuffle was performed.
        /// </summary>
        public bool ShuffleBoard(int maxAttempts = 20, bool requireValidMove = true)
        {
            return shuffleService.ShuffleBoard(
                grid,
                CellToWorld,
                boardConfig.FallMoveDuration,
                maxAttempts,
                requireValidMove);
        }

        #endregion


        /// <summary>
        /// Resolve the board by repeatedly removing matches, collapsing, and refilling
        /// until no further matches exist. Keeps input locked via outer context.
        /// </summary>
        private System.Collections.IEnumerator ResolveBoardLoop()
        {
            const int safetyMax = 64; // prevent infinite loops
            int iterations = 0;

            while (iterations++ < safetyMax)
            {
                // 1) Remove current matches
                int removed = RemoveMatches();
                if (removed <= 0)
                    break; // stable: no more matches

                // 2) Add score
                ScoreManager.Instance?.AddScore(removed);

                // 3) Collapse gravity
                CollapseColumnsDownward();
                yield return WaitUntilAllPiecesStop();

                // 4) Refill from top
                RefillNewPiecesFromTop();
                yield return WaitUntilAllPiecesStop();

                // 5) Shuffle if no valid moves
                if (!HasAnyValidMove())
                {
                    ShuffleBoard();
                    yield return WaitUntilAllPiecesStop();
                    // After animation, board will have at least one move (best-effort)
                }

                // loop; newly formed matches (cascades) will be removed next iteration
            }
        }

        #endregion

        
#if UNITY_EDITOR
#region Editor
        private void OnDrawGizmos()
        {
            if (boardConfig == null) return;
            // Draw board bounds and cell lines for quick visual validation in Scene view.
            ComputeOrigin();
            Vector2 org = layout.Origin;

            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);

            // Outer rect
            Vector3 bl = new Vector3(org.x, org.y, 0f);
            Vector3 br = new Vector3(org.x + boardConfig.Width * boardConfig.CellSize, org.y, 0f);
            Vector3 tl = new Vector3(org.x, org.y + boardConfig.Height * boardConfig.CellSize, 0f);
            Vector3 tr = new Vector3(org.x + boardConfig.Width * boardConfig.CellSize, org.y + boardConfig.Height * boardConfig.CellSize, 0f);
            Gizmos.DrawLine(bl, br); Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl); Gizmos.DrawLine(tl, bl);

            // Grid lines
            for (int c = 1; c < boardConfig.Width; c++)
            {
                float x = org.x + c * boardConfig.CellSize;
                Gizmos.DrawLine(new Vector3(x, bl.y, 0f), new Vector3(x, tl.y, 0f));
            }
            for (int r = 1; r < boardConfig.Height; r++)
            {
                float y = org.y + r * boardConfig.CellSize;
                Gizmos.DrawLine(new Vector3(bl.x, y, 0f), new Vector3(br.x, y, 0f));
            }
        }  
#endregion
#endif
    }
}
