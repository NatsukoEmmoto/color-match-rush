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
        [SerializeField] private Transform boardRoot;  // Parent for instantiated pieces

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

        // Cached origin (bottom-left corner in world space)
        private Vector2 origin;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Piece[,] Grid => grid;

        private void Awake()
        {
            // Ensure boardRoot exists
            if (boardRoot == null)
            {
                var root = new GameObject("BoardRoot");
                boardRoot = root.transform;
                boardRoot.SetParent(transform, worldPositionStays: false);
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

            // First pass: build with or without instant-match avoidance
            GenerateBoardInternal(preventInstantMatchesOnStart);

            // Safety pass: if anything slipped through, try a few regenerations
            if (preventInstantMatchesOnStart)
                EnsureNoInstantMatches();
        }
        /// <summary>
        /// Returns true if placing a piece of the given type at (row,col)
        /// would immediately create a 3+ match with already-placed neighbors.
        /// Assumes generation order is from bottom to top, left to right.
        /// Checks only left and down directions (already filled cells).
        /// </summary>
        private bool WouldCreateInstantMatchAt(int row, int col, Piece.PieceType type)
        {
            // Horizontal check: left two
            if (col >= 2)
            {
                var p1 = grid[row, col - 1];
                var p2 = grid[row, col - 2];
                if (p1 != null && p2 != null && p1.Type == type && p2.Type == type)
                    return true;
            }

            // Vertical check: down two
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
        /// Pick a random prefab that does NOT cause an instant 3-match at (row,col).
        /// Tries a limited number of attempts and falls back to any random prefab if needed.
        /// </summary>
        private Piece GetRandomPrefabAvoidingInstantMatch(int row, int col)
        {
            if (piecePrefabs == null || piecePrefabs.Length == 0)
            {
                Debug.LogError("[BoardManager] piecePrefabs not assigned or empty.");
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
                if (!WouldCreateInstantMatchAt(row, col, prefab.Type))
                    return prefab;
            }

            // Fallback: return the last tried valid prefab even if it matches (very unlikely with 5 colors)
            if (lastTried == null)
                Debug.LogError("[BoardManager] Failed to choose a valid prefab (all null?).");
            else
                Debug.LogWarning($"[BoardManager] Fallback to potentially matching prefab at ({row},{col}).");

            return lastTried;
        }

        /// <summary>
        /// Ensure the current grid has no instant matches; if found, regenerates up to a few attempts.
        /// This is only used at startup for safety; normal play uses resolve loop.
        /// </summary>
        private void EnsureNoInstantMatches()
        {
            if (!preventInstantMatchesOnStart || grid == null) return;

            for (int i = 0; i < maxInstantMatchRegenerations; i++)
            {
                var matches = FindAllMatches();
                if (matches == null || matches.Count == 0) return; // already clean

                // Re-generate with the avoidance picker to try a clean board
                Debug.LogWarning($"[BoardManager] Instant matches detected at start. Regenerating (attempt {i + 1}/{maxInstantMatchRegenerations})...");
                GenerateBoardInternal(avoidInstantMatches: true);
            }
        }

        /// <summary>
        /// Internal generator with a switch to avoid instant matches during placement.
        /// </summary>
        private void GenerateBoardInternal(bool avoidInstantMatches)
        {
            ComputeOrigin();
            ClearBoardImmediate();

            grid = new Piece[height, width];

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    Piece prefab = avoidInstantMatches
                        ? GetRandomPrefabAvoidingInstantMatch(row, column)
                        : GetRandomPiecePrefab();

                    if (prefab == null)
                    {
                        Debug.LogError($"[BoardManager] Failed to get valid prefab for position ({row}, {column}). Skipping cell.");
                        continue; // leave null
                    }

                    Piece piece = Instantiate(prefab, boardRoot);
                    Vector3 worldPos = CellToWorld(row, column);
                    piece.Initialize(row, column, prefab.Type, worldPos);
                    grid[row, column] = piece;
                }
            }
        }

        /// <summary>
        /// Destroy all children under boardRoot and reset grid reference.
        /// </summary>
        public void ClearBoardImmediate()
        {
            for (int i = boardRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(boardRoot.GetChild(i).gameObject);
            }
            grid = null;
        }

        /// <summary>
        /// Convert grid coordinates (row, column) to world-space position.
        /// Row 0 is bottom, Column 0 is left.
        /// </summary>
        public Vector3 CellToWorld(int row, int column)
        {
            float x = origin.x + (column + 0.5f) * cellSize;
            float y = origin.y + (row + 0.5f) * cellSize;
            return new Vector3(x, y, 0f);
        }

        /// <summary>
        /// Convert world-space position to nearest grid indices.
        /// Clamps to board bounds.
        /// </summary>
        public void WorldToCell(Vector3 world, out int row, out int column)
        {
            float localX = world.x - origin.x;
            float localY = world.y - origin.y;
            column = Mathf.Clamp(Mathf.FloorToInt(localX / cellSize), 0, width - 1);
            row    = Mathf.Clamp(Mathf.FloorToInt(localY / cellSize), 0, height - 1);
        }

        /// <summary>
        /// Compute bottom-left origin for the current layout.
        /// </summary>
        private void ComputeOrigin()
        {
            if (autoCenter)
            {
                float boardW = width * cellSize;
                float boardH = height * cellSize;
                origin = new Vector2(-boardW * 0.5f, -boardH * 0.5f);
            }
            else
            {
                origin = explicitOrigin;
            }
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
            Piece center = grid[row, column];
            if (center == null) return false;
            var type = center.Type;

            int horiz = 1 + CountDir(row, column, 0, -1, type) + CountDir(row, column, 0, 1, type);
            if (horiz >= 3) return true;

            int vert = 1 + CountDir(row, column, -1, 0, type) + CountDir(row, column, 1, 0, type);
            return vert >= 3;
        }

        private int CountDir(int row, int col, int dr, int dc, Piece.PieceType type)
        {
            int count = 0;
            int r = row + dr, c = col + dc;
            while (r >= 0 && r < height && c >= 0 && c < width)
            {
                var p = grid[r, c];
                if (p == null || p.Type != type) break;
                count++;
                r += dr; c += dc;
            }
            return count;
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

        /// <summary>
        /// Helper to validate indices against the current grid size.
        /// </summary>
        private bool IsInBounds(int row, int col)
        {
            return grid != null &&
                   row >= 0 && row < grid.GetLength(0) &&
                   col >= 0 && col < grid.GetLength(1);
        }

        /// <summary>
        /// Scan the entire grid horizontally and vertically,
        /// collecting all pieces that belong to runs with length >= 3.
        /// Uses a HashSet to avoid duplicates (overlapping H/V runs).
        /// </summary>
        public HashSet<Piece> FindAllMatches()
        {
            var result = new HashSet<Piece>();
            if (grid == null)
                return result;

            // Use actual grid dimensions (defensive against width/height mismatches)
            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            // Horizontal scan (rows)
            for (int r = 0; r < h; r++)
            {
                int c = 0;
                while (c < w)
                {
                    var start = grid[r, c];
                    if (start == null) { c++; continue; }

                    int runStart = c;
                    int runLen = 1;

                    // grow run while same type
                    while (c + runLen < w)
                    {
                        var next = grid[r, c + runLen];
                        if (next == null || next.Type != start.Type) break;
                        runLen++;
                    }

                    if (runLen >= 3)
                    {
                        for (int k = 0; k < runLen; k++)
                        {
                            var p = grid[r, runStart + k];
                            if (p != null) result.Add(p);
                        }
                    }

                    c += runLen; // jump to next segment
                }
            }

            // Vertical scan (columns)
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
                    {
                        for (int k = 0; k < runLen; k++)
                        {
                            var p = grid[runStart + k, c];
                            if (p != null) result.Add(p);
                        }
                    }

                    r += runLen;
                }
            }

            return result;
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
                        // move down
                        grid[write, c] = piece;
                        grid[r, c] = null;

                        piece.SetGridIndex(write, c);
                        piece.MoveTo(CellToWorld(write, c), fallMoveDuration);

                        anyMoved = true;
                    }
                    write++;
                }

                // cells [write..h-1] stay null (to be refilled later)
            }

            return anyMoved;
        }
        
        /// <summary>
        /// Refill empty cells by spawning new pieces above the top row and animating them down.
        /// Returns true if any piece was spawned.
        /// </summary>
        public bool RefillNewPiecesFromTop()
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
                    Piece prefab = GetRandomPiecePrefab();
                    if (prefab == null) continue;

                    // spawn slightly above the target cell and fall down
                    Vector3 targetWorld = CellToWorld(r, c);
                    Vector3 startWorld = targetWorld + new Vector3(0f, cellSize * spawnOvershootCells, 0f);

                    Piece piece = Instantiate(prefab, boardRoot);
                    piece.Initialize(r, c, prefab.Type, startWorld);

                    grid[r, c] = piece;
                    piece.MoveTo(targetWorld, fallMoveDuration);

                    anySpawned = true;
                }
            }

            return anySpawned;
        }

        /// <summary>
        /// Wait until all pieces in the grid finish moving.
        /// </summary>
        private System.Collections.IEnumerator WaitUntilAllPiecesStop()
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

        #endregion

        #region Shuffle & Deadboard

        /// <summary>
        /// Return true if there exists at least one adjacent swap that would create a match.
        /// We simulate a swap in the grid (no animation), check, then revert.
        /// </summary>
        public bool HasAnyValidMove()
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
                    {
                        if (SwapWouldCreateMatch(r, c, r, c + 1)) return true;
                    }
                    // Up neighbor
                    if (r + 1 < h && grid[r + 1, c] != null)
                    {
                        if (SwapWouldCreateMatch(r, c, r + 1, c)) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Temporarily swap grid cells (r1,c1) and (r2,c2), check if either cell creates a match, then revert.
        /// </summary>
        private bool SwapWouldCreateMatch(int r1, int c1, int r2, int c2)
        {
            var p1 = grid[r1, c1];
            var p2 = grid[r2, c2];
            if (p1 == null || p2 == null) return false;

            // swap in place (no animation)
            grid[r1, c1] = p2; grid[r2, c2] = p1;

            bool created =
                CreatesMatchAt(r1, c1) ||
                CreatesMatchAt(r2, c2);

            // revert
            grid[r1, c1] = p1; grid[r2, c2] = p2;
            return created;
        }

        /// <summary>
        /// Shuffle the existing pieces randomly across the grid and animate them to their new cells.
        /// Ensures the result is not an instant-match board and (optionally) has at least one valid move.
        /// Returns true if a shuffle was performed.
        /// </summary>
        public bool ShuffleBoard(int maxAttempts = 20, bool requireValidMove = true)
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

            // Try a few shuffles until constraints are satisfied
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // Fisher–Yates shuffle
                for (int i = pieces.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (pieces[i], pieces[j]) = (pieces[j], pieces[i]);
                }

                // Place into grid (no animation yet)
                int idx = 0;
                for (int r = 0; r < h; r++)
                {
                    for (int c = 0; c < w; c++)
                    {
                        var p = (idx < pieces.Count) ? pieces[idx++] : null;
                        grid[r, c] = p;
                        if (p != null) p.SetGridIndex(r, c); // keep indices in sync
                    }
                }

                // If startup-avoid flag is ON, ensure we don't start with matches
                var matches = FindAllMatches();
                if (matches != null && matches.Count > 0)
                    continue; // try another shuffle to avoid instant matches

                if (requireValidMove && !HasAnyValidMove())
                    continue; // deadboard again, reshuffle

                // Success: animate to new cells
                for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                {
                    var p = grid[r, c];
                    if (p != null)
                        p.MoveTo(CellToWorld(r, c), fallMoveDuration);
                }

                return true;
            }

            Debug.LogWarning("[BoardManager] ShuffleBoard() fell back after max attempts; applying last layout anyway.");
            // Animate last attempt so board doesn't look frozen
            for (int r = 0; r < grid.GetLength(0); r++)
            for (int c = 0; c < grid.GetLength(1); c++)
            {
                var p = grid[r, c];
                if (p != null)
                    p.MoveTo(CellToWorld(r, c), fallMoveDuration);
            }
            return true;
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

                // 2) Collapse gravity
                CollapseColumnsDownward();
                yield return WaitUntilAllPiecesStop();

                // 3) Refill from top
                RefillNewPiecesFromTop();
                yield return WaitUntilAllPiecesStop();

                // 4) Shuffle if no valid moves
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
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);

            // Outer rect
            Vector3 bl = new Vector3(origin.x, origin.y, 0f);
            Vector3 br = new Vector3(origin.x + width * cellSize, origin.y, 0f);
            Vector3 tl = new Vector3(origin.x, origin.y + height * cellSize, 0f);
            Vector3 tr = new Vector3(origin.x + width * cellSize, origin.y + height * cellSize, 0f);
            Gizmos.DrawLine(bl, br); Gizmos.DrawLine(br, tr); Gizmos.DrawLine(tr, tl); Gizmos.DrawLine(tl, bl);

            // Grid lines
            for (int c = 1; c < width; c++)
            {
                float x = origin.x + c * cellSize;
                Gizmos.DrawLine(new Vector3(x, bl.y, 0f), new Vector3(x, tl.y, 0f));
            }
            for (int r = 1; r < height; r++)
            {
                float y = origin.y + r * cellSize;
                Gizmos.DrawLine(new Vector3(bl.x, y, 0f), new Vector3(br.x, y, 0f));
            }
        }
#endif
    }
}
