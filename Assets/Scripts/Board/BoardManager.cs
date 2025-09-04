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
        [Header("Board Size")]
        [SerializeField] private int width = 8;     // number of columns (x)
        [SerializeField] private int height = 8;    // number of rows (y)

        [Header("Prefabs & Root")]
        [SerializeField] private Piece[] piecePrefabs; // Expected order: Red, Blue, Green, Yellow, Purple
        [SerializeField] private Transform piecesRoot;  // Parent for instantiated pieces

        [Header("Layout")]
        [SerializeField, Tooltip("World-space size of one cell (units).")]
        private float cellSize = 1f;
        [SerializeField, Tooltip("If true, compute origin so the board is centered around (0,0). If false, use explicit origin.")]
        private bool autoCenter = true;
        [SerializeField, Tooltip("Bottom-left world position of the board when autoCenter is false.")]
        private Vector2 explicitOrigin = Vector2.zero;

        [Header("Options")]
        [SerializeField, Tooltip("If true, GenerateBoard will run in Start().")]
        private bool generateOnStart = true;
        [SerializeField, Tooltip("Optional random seed for repeatable boards. 0 = random.")]
        private int randomSeed = 0;
        [SerializeField, Tooltip("Avoid 3-in-a-row/column at startup.")]
        private bool preventInstantMatchesOnStart = true;
        [SerializeField, Tooltip("Maximum times to regenerate board to avoid instant matches.")]
        private int maxInstantMatchRegenerations = 5;

        // Grid storage (row-major: [row, column])
        private Piece[,] grid;
        
        // Services
        private readonly MatchFinder matchFinder = new MatchFinder();
        private readonly GravityRefill gravityRefill = new GravityRefill();
        private readonly ShuffleService shuffleService = new ShuffleService();
        private readonly BoardGenerator boardGenerator = new BoardGenerator();
        private readonly BoardLayout layout = new BoardLayout();   

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Piece[,] Grid => grid;

        private void Awake()
        {
            // Ensure boardRoot exists
            if (piecesRoot == null)
            {
                var root = new GameObject("BoardRoot");
                piecesRoot = root.transform;
                piecesRoot.SetParent(transform, worldPositionStays: false);
            }
        }

        private void Start()
        {
            if (generateOnStart)
            {
                GenerateBoard();
            }
        }

        /// <summary>
        /// Generate a fresh board with random pieces using the configured size.
        /// Clears existing children and recreates grid and pieces.
        /// </summary>
        public void GenerateBoard()
        {
            if (randomSeed != 0)
                Random.InitState(randomSeed);

            ComputeOrigin();
            ClearBoardImmediate();

            boardGenerator.GenerateBoard(
                out grid,
                width,
                height,
                piecePrefabs,
                piecesRoot,
                CellToWorld,
                preventInstantMatchesOnStart,
                maxInstantMatchRegenerations
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
            return layout.CellToWorld(row, column, cellSize);
        }

        /// <summary>
        /// Convert world-space position to nearest grid indices.
        /// Clamps to board bounds.
        /// </summary>
        public void WorldToCell(Vector3 world, out int row, out int column)
        {
            layout.WorldToCell(world, cellSize, width, height, out row, out column);
        }

        /// <summary>
        /// Compute bottom-left origin for the current layout.
        /// </summary>
        private void ComputeOrigin()
        {
            layout.ComputeOrigin(width, height, cellSize, autoCenter, explicitOrigin);
        }

        /// <summary>
        /// Pick a random piece prefab from the configured array.
        /// </summary>
        private Piece GetRandomPiecePrefab()
        {
            if (piecePrefabs == null || piecePrefabs.Length == 0)
            {
                Debug.LogError("[BoardManager] piecePrefabs not assigned or empty.");
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

        #region Swap Operations
        [SerializeField, Tooltip("Seconds to move per swap/bounce.")]
        private float swapMoveDuration = 0.12f;
        [SerializeField] private bool swapInProgress = false;
        [SerializeField, Tooltip("True while the board is resolving matches/cascades.")]
        private bool isResolving = false;
        public bool IsResolving => isResolving;

        private InputController inputController;
        public void SetInputController(InputController controller) => inputController = controller;

        public bool AreAdjacent(Piece a, Piece b)
        {
            if (a == null || b == null) return false;
            int dr = Mathf.Abs(a.Row - b.Row);
            int dc = Mathf.Abs(a.Column - b.Column);
            return (dr == 1 && dc == 0) || (dr == 0 && dc == 1);
        }

        public bool TrySwap(Piece a, Piece b)
        {
            if (isResolving) return false;
            if (swapInProgress) return false;
            if (a == null || b == null || a == b) return false;
            if (!AreAdjacent(a, b)) return false;

            swapInProgress = true;
            if (inputController == null) inputController = FindObjectOfType<InputController>();
            if (inputController) inputController.SetInputLock(true);

            StartCoroutine(SwapRoutine(a, b));
            return true;
        }

        private System.Collections.IEnumerator SwapRoutine(Piece a, Piece b)
        {
            // Cache original indices
            int ar = a.Row, ac = a.Column;
            int br = b.Row, bc = b.Column;

            // Swap in grid + indices
            grid[ar, ac] = b; grid[br, bc] = a;
            a.SetGridIndex(br, bc); b.SetGridIndex(ar, ac);

            // Animate to new positions
            a.MoveTo(CellToWorld(a.Row, a.Column), swapMoveDuration);
            b.MoveTo(CellToWorld(b.Row, b.Column), swapMoveDuration);
            yield return WaitUntilPiecesStop(a, b);

            // Check local matches around both pieces
            bool matched = CreatesMatchAt(a.Row, a.Column) || CreatesMatchAt(b.Row, b.Column);

            if (!matched)
            {
                // Revert to original indices (ar,ac) / (br,bc)
                grid[ar, ac] = a;
                grid[br, bc] = b;

                a.SetGridIndex(ar, ac);
                b.SetGridIndex(br, bc);

                a.MoveTo(CellToWorld(ar, ac), swapMoveDuration);
                b.MoveTo(CellToWorld(br, bc), swapMoveDuration);
                
                yield return WaitUntilPiecesStop(a, b);
            }
            else
            {
                // === Start resolution phase: pause the timer ===
                isResolving = true;
                GameController.Instance?.PauseTimer();

                yield return ResolveBoardLoop();

                // === End resolution phase: resume the timer ===
                isResolving = false;
                GameController.Instance?.StartTimer();
            }

            UnlockInput();
            swapInProgress = false;
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
            if (inputController != null) inputController.SetInputLock(false);
            else
            {
                var ic = FindObjectOfType<InputController>();
                if (ic) ic.SetInputLock(false);
            }
        }
        #endregion

        #region Match Handling

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

        [Header("Resolve")]
        [SerializeField, Tooltip("Seconds to move per falling step.")]
        private float fallMoveDuration = 0.08f;
        
        [SerializeField, Tooltip("How many cells above the top to spawn new pieces before falling.")]
        private float spawnOvershootCells = 1f;

        /// <summary>
        /// Collapse all columns downward using a write-pointer per column.
        /// Returns true if any piece moved.
        /// </summary>
        public bool CollapseColumnsDownward()
        {
            return gravityRefill.CollapseColumnsDownward(grid, CellToWorld, fallMoveDuration);
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
                cellSize,
                spawnOvershootCells,
                fallMoveDuration);
        }

        /// <summary>
        /// Wait until all pieces in the grid finish moving.
        /// </summary>
        private System.Collections.IEnumerator WaitUntilAllPiecesStop()
        {
            return gravityRefill.WaitUntilAllPiecesStop(grid);
        }

        #endregion

        #region Shuffle & Deadboard

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
                fallMoveDuration,
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

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Draw board bounds and cell lines for quick visual validation in Scene view.
            ComputeOrigin();
            Vector2 org = layout.Origin;

            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);

            // Outer rect
            Vector3 bl = new Vector3(org.x, org.y, 0f);
            Vector3 br = new Vector3(org.x + width * cellSize, org.y, 0f);
            Vector3 tl = new Vector3(org.x, org.y + height * cellSize, 0f);
            Vector3 tr = new Vector3(org.x + width * cellSize, org.y + height * cellSize, 0f);
            Gizmos.DrawLine(bl, br); Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl); Gizmos.DrawLine(tl, bl);

            // Grid lines
            for (int c = 1; c < width; c++)
            {
                float x = org.x + c * cellSize;
                Gizmos.DrawLine(new Vector3(x, bl.y, 0f), new Vector3(x, tl.y, 0f));
            }
            for (int r = 1; r < height; r++)
            {
                float y = org.y + r * cellSize;
                Gizmos.DrawLine(new Vector3(bl.x, y, 0f), new Vector3(br.x, y, 0f));
            }
        }  
#endif
    }
}
