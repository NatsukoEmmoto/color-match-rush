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
                DestroyImmediate(boardRoot.GetChild(i).gameObject);
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
                Debug.LogError("[BoardManager] piecePrefabs not assigned.");
                return null;
            }
            int index = Random.Range(0, piecePrefabs.Length);
            return piecePrefabs[index];
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
