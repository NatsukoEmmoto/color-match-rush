using System.Collections;
using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Represents a single piece on the match-3 board.
    /// Holds grid indices (row/column) and type, and provides snap/move operations.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Piece : MonoBehaviour
    {
        public enum PieceType
        {
            Red,
            Blue,
            Green,
            Yellow,
            Purple,
            // Future special pieces
            LineH,   // Horizontal line clear
            LineV,   // Vertical line clear
            Rainbow  // Rainbow (clear all of the same color)
        }

        [Header("Grid Index (Row/Column)")]
        [SerializeField] private int row;
        [SerializeField] private int column;

        [Header("Type")]
        [SerializeField] private PieceType type = PieceType.Red;

        [Header("Move Settings")]
        [SerializeField, Tooltip("Default duration in seconds for MoveTo.")] private float defaultMoveDuration = 0.12f;
        [SerializeField, Tooltip("If true, block new moves during an active move.")] private bool lockDuringMove = true;
        [SerializeField, Tooltip("Animation curve for movement.")] private AnimationCurve moveEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Coroutine moveRoutine;
        private Transform cachedTf;

        #region Public Properties
        public int Row => row;
        public int Column => column;
        public PieceType Type => type;
        public bool IsMoving => moveRoutine != null;
        #endregion

        private void Awake()
        {
            cachedTf = transform;
        }

        /// <summary>
        /// Initialize piece data. Should be called immediately after instantiation by BoardManager.
        /// </summary>
        public void Initialize(int row, int column, PieceType type, Vector3 worldPosition)
        {
            this.row = row;
            this.column = column;
            this.type = type;
            SnapTo(worldPosition);
        }

        /// <summary>
        /// Update only the grid index values (row/column).
        /// </summary>
        public void SetGridIndex(int newRow, int newColumn)
        {
            row = newRow;
            column = newColumn;
        }

        /// <summary>
        /// Instantly snap to the given world position.
        /// </summary>
        public void SnapTo(Vector3 worldPosition)
        {
            if (moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
                moveRoutine = null;
            }
            cachedTf.position = worldPosition;
        }

        /// <summary>
        /// Smoothly move to the given world position. Optional callback after completion.
        /// </summary>
        public void MoveTo(Vector3 targetWorldPosition, float duration = -1f, System.Action onCompleted = null)
        {
            if (duration <= 0f) duration = defaultMoveDuration;

            if (lockDuringMove && moveRoutine != null)
            {
                StopCoroutine(moveRoutine);
            }
            moveRoutine = StartCoroutine(Co_MoveTo(targetWorldPosition, duration, onCompleted));
        }

        private IEnumerator Co_MoveTo(Vector3 targetPos, float duration, System.Action onCompleted)
        {
            Vector3 start = cachedTf.position;
            float t = 0f;
            while (t < 1f)
            {
                t += (duration <= 0f ? 1f : Time.deltaTime / duration);
                float eased = moveEase.Evaluate(Mathf.Clamp01(t));
                cachedTf.position = Vector3.LerpUnclamped(start, targetPos, eased);
                yield return null;
            }
            cachedTf.position = targetPos;
            moveRoutine = null;
            onCompleted?.Invoke();
        }

        /// <summary>
        /// Swap grid indices of two pieces. Does not move their world positions.
        /// </summary>
        public static void SwapIndex(Piece a, Piece b)
        {
            int r = a.row; int c = a.column;
            a.row = b.row; a.column = b.column;
            b.row = r;     b.column = c;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // Debug visualization for row/column and type in Scene view
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.4f, $"({row},{column}) {type}");
        }
#endif
    }
}
