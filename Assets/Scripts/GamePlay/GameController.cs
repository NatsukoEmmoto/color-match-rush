using System;
using System.Collections;
using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Controls overall game flow including countdown timer.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }

        [Header("Timer Settings")]
        [SerializeField, Tooltip("Time limit in seconds for one stage.")]
        private float timeLimit = 30f; // [sec]

        [Header("Refs")]
        [SerializeField] private UIManager uiManager;
        [SerializeField] private InputController inputController;
        [SerializeField] private BoardManager board;
        private float remainingTime;
        private bool isRunning = false;
        private bool gameOver  = false;
        public float RemainingTime => remainingTime;
        public event Action<float> OnTimerChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // DontDestroyOnLoad(gameObject); // enable if scene changes
        }

        private void Start()
        {
            ResetTimer();
            StartTimer();
        }

        private void Update()
        {
            if (!isRunning || gameOver) return;

            float oldTime = remainingTime;
            remainingTime -= Time.deltaTime;

            if (Mathf.FloorToInt(oldTime) != Mathf.FloorToInt(remainingTime))
                OnTimerChanged?.Invoke(remainingTime);

            if (remainingTime <= 0f)
            {
                remainingTime = 0f;
                isRunning = false;
                OnTimerEnd();
            }
        }

        /// <summary>
        /// Reset timer to the initial start time.
        /// </summary>
        public void ResetTimer() => remainingTime = timeLimit;

        /// <summary>
        /// Start/resume countdown.
        /// </summary>
        public void StartTimer() => isRunning = true;

        /// <summary>
        /// Pause countdown.
        /// </summary>
        public void PauseTimer() => isRunning = false;

        private void OnTimerEnd()
        {
            if (gameOver) return;
            gameOver = true;

            // 1) Disable player input
            inputController?.SetInputLock(true);

            // 2) If the board is still resolving, wait until it's stable before showing results
            StartCoroutine(ShowResultWhenStable());
        }

        private IEnumerator ShowResultWhenStable()
        {
            // Wait until BoardManager finishes resolving
            while (board != null && board.IsResolving)
                yield return null;

            // 3) Show result screen (use current score from ScoreManager)
            int score = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            if (uiManager != null)
            {
                uiManager.ShowResult(score, () => Retry());
            }
        }

        private void Retry()
        {
            // Reinitialize score/board/timer for a new game
            ScoreManager.Instance?.ResetScore();
            board?.GenerateBoard();
            ResetTimer();

            // Hide result panel and re-enable input
            if (uiManager != null) uiManager.HideResult();
            inputController?.SetInputLock(false);

            gameOver = false;
            StartTimer();
        }
    }
}