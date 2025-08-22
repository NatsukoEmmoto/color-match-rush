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
            {
                Random.InitState(randomSeed);
            }

            ComputeOrigin();
            ClearBoardImmediate();

            grid = new Piece[height, width];

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    Piece prefab = GetRandomPiecePrefab();
                    if (prefab == null)
                    {
                        Debug.LogError($"[BoardManager] Failed to get valid prefab for position ({row}, {column}). Skipping cell.");
                        continue; // Skip this cell, leave it null in the grid
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
                //TODO: Handle match and refill
                RemoveMatches();
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
        /// Scan the entire grid horizontally and vertically,
        /// collecting all pieces that belong to runs with length >= 3.
        /// Uses a HashSet to avoid duplicates (overlapping H/V runs).
        /// Null-safe: skips empty cells.
        /// </summary>
        public HashSet<Piece> FindAllMatches()
        {
            var result = new HashSet<Piece>();
            if (grid == null) return result;

            // Horizontal scan (rows)
            for (int r = 0; r < height; r++)
            {
                int c = 0;
                while (c < width)
                {
                    Piece start = grid[r, c];
                    if (start == null) { c++; continue; }

                    int runStart = c;
                    int runLen = 1;

                    // grow run while same type
                    while (c + runLen < width)
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
            for (int c = 0; c < width; c++)
            {
                int r = 0;
                while (r < height)
                {
                    Piece start = grid[r, c];
                    if (start == null) { r++; continue; }

                    int runStart = r;
                    int runLen = 1;

                    while (r + runLen < height)
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
        /// Returns number of pieces removed.
        /// </summary>
        public int RemoveMatches()
        {
            var matches = FindAllMatches();
            if (matches == null || matches.Count == 0)
                return 0;

            int removed = 0;
            foreach (var piece in matches)
            {
                if (piece == null) continue;

                // Clear from grid
                grid[piece.Row, piece.Column] = null;

                // Destroy game object
                Destroy(piece.gameObject);
                removed++;
            }

            return removed;
        }
        #endregion

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
