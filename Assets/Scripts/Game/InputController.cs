using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Handles click/drag input for swapping adjacent pieces.
    /// - Press (mouse/touch) on a cell to select a source piece
    /// - Drag past a threshold; the dominant axis decides the swap direction
    /// - Requests a swap on BoardManager (validation + animation will be handled there)
    /// Note: BoardManager.TrySwap(Piece a, Piece b) will be implemented in the next task.
    /// </summary>
    public class InputController : MonoBehaviour
    {
        #region Constants
        private const int PRIMARY_MOUSE_BUTTON = 0;
        private const int PRIMARY_TOUCH_INDEX = 0;
        private const float WORLD_Z_POSITION = 0f;
        #endregion

        #region Serialized Fields
        [Header("References")]
        [SerializeField] private BoardManager board;
        [SerializeField] private Camera inputCamera;

        [Header("Settings")]
        [Tooltip("Drag distance as a ratio of cell size to decide a swap (dominant axis). Default 0.3 means 30% of a cell.")]
        [SerializeField] private float dragThresholdCellRatio = 0.3f;

        [Header("Debug")]
        [SerializeField] private bool inputLocked = false; // for inspector view only (optional)
        #endregion

        #region Private Fields
        private InputState currentInputState;
        #endregion

        #region Structs
        private struct InputState
        {
            public bool IsPressed;
            public Vector3 PressWorldPosition;
            public Vector2Int PressGridPosition;
            public Piece PressPiece;

            public void Reset()
            {
                IsPressed = false;
                PressWorldPosition = Vector3.zero;
                PressGridPosition = Vector2Int.zero;
                PressPiece = null;
            }
        }
        #endregion

        #region Properties
        private Camera Cam => inputCamera;
        private float DragThresholdSquared
        {
            get
            {
                if (board == null) return 0f;
                float worldThreshold = dragThresholdCellRatio * board.CellSize;
                return worldThreshold * worldThreshold;
            }
        }
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            if (inputCamera == null) inputCamera = Camera.main;
        }

        private void Reset()
        {
            InitializeReferences();
        }

        private void Update()
        {
            if (!CanProcessInput()) return;

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            HandleMouseInput();
#else
            HandleTouchInput();
#endif
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Public API to lock/unlock input from external systems (e.g., while swapping/clearing).
        /// </summary>
        public void SetInputLock(bool locked)
        {
            inputLocked = locked;
            enabled = !locked; // disable Update while locked
        }
        #endregion

        #region Private Methods
        private void InitializeReferences()
        {
            if (board == null) board = FindObjectOfType<BoardManager>();
            if (inputCamera == null) inputCamera = Camera.main;
            
            // Set up bidirectional reference to avoid FindObjectOfType calls
            if (board != null)
            {
                board.SetInputController(this);
            }
        }

        private bool CanProcessInput()
        {
            return !inputLocked && board != null && Cam != null;
        }

        private void HandleMouseInput()
        {
            if (Input.GetMouseButtonDown(PRIMARY_MOUSE_BUTTON))
            {
                BeginPress(Input.mousePosition);
            }
            else if (currentInputState.IsPressed && Input.GetMouseButton(PRIMARY_MOUSE_BUTTON))
            {
                ContinueDrag(Input.mousePosition);
            }
            else if (currentInputState.IsPressed && Input.GetMouseButtonUp(PRIMARY_MOUSE_BUTTON))
            {
                CancelPress();
            }
        }

        private void HandleTouchInput()
        {
            if (Input.touchCount == 0) return;

            var touch = Input.GetTouch(PRIMARY_TOUCH_INDEX);
            
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    BeginPress(touch.position);
                    break;
                    
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (currentInputState.IsPressed)
                        ContinueDrag(touch.position);
                    break;
                    
                case TouchPhase.Canceled:
                case TouchPhase.Ended:
                    if (currentInputState.IsPressed)
                        CancelPress();
                    break;
            }
        }

        private void BeginPress(Vector2 screenPos)
        {
            var worldPos = ScreenToWorld(screenPos);
            var gridPos = GetGridPosition(worldPos);
            
            if (!IsValidGridPosition(gridPos))
            {
                currentInputState.Reset();
                return;
            }

            var piece = GetPieceAtPosition(gridPos);
            if (piece == null)
            {
                currentInputState.Reset();
                return;
            }

            currentInputState = new InputState
            {
                IsPressed = true,
                PressWorldPosition = worldPos,
                PressGridPosition = gridPos,
                PressPiece = piece
            };
        }

        private void ContinueDrag(Vector2 screenPos)
        {
            if (!currentInputState.IsPressed || currentInputState.PressPiece == null) return;

            var currentWorld = ScreenToWorld(screenPos);
            var delta = currentWorld - currentInputState.PressWorldPosition;

            if (!IsDragThresholdExceeded(delta)) return;

            var targetGridPos = CalculateTargetPosition(delta);
            if (!IsValidSwapTarget(targetGridPos)) return;

            var targetPiece = GetPieceAtPosition(targetGridPos);
            if (targetPiece == null) return;

            ExecuteSwap(currentInputState.PressPiece, targetPiece);
        }

        private void CancelPress()
        {
            currentInputState.Reset();
        }

        private Vector2Int GetGridPosition(Vector3 worldPos)
        {
            board.WorldToCell(worldPos, out int row, out int column);
            return new Vector2Int(column, row);
        }

        private bool IsValidGridPosition(Vector2Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < board.Width &&
                   gridPos.y >= 0 && gridPos.y < board.Height;
        }

        private Piece GetPieceAtPosition(Vector2Int gridPos)
        {
            var grid = board.Grid;
            if (grid == null) return null;
            
            return grid[gridPos.y, gridPos.x];
        }

        private bool IsDragThresholdExceeded(Vector3 delta)
        {
            return delta.sqrMagnitude >= DragThresholdSquared;
        }

        private Vector2Int CalculateTargetPosition(Vector3 delta)
        {
            var targetGridPos = currentInputState.PressGridPosition;
            
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                targetGridPos.x += delta.x > 0f ? 1 : -1;
            }
            else
            {
                targetGridPos.y += delta.y > 0f ? 1 : -1;
            }

            // Clamp to board bounds
            targetGridPos.x = Mathf.Clamp(targetGridPos.x, 0, board.Width - 1);
            targetGridPos.y = Mathf.Clamp(targetGridPos.y, 0, board.Height - 1);

            return targetGridPos;
        }

        private bool IsValidSwapTarget(Vector2Int targetGridPos)
        {
            return targetGridPos != currentInputState.PressGridPosition;
        }

        private void ExecuteSwap(Piece sourcePiece, Piece targetPiece)
        {
            bool swapStarted = board.TrySwap(sourcePiece, targetPiece);
            // BoardManager handles locking/unlocking, so only reset the state here
            currentInputState.Reset();
        }

        private Vector3 ScreenToWorld(Vector2 screenPos)
        {
            var worldPos = Cam.ScreenToWorldPoint(new Vector3(
                screenPos.x, 
                screenPos.y, 
                Mathf.Abs(Cam.transform.position.z)
            ));
            
            worldPos.z = WORLD_Z_POSITION;
            return worldPos;
        }
        #endregion
    }
}
