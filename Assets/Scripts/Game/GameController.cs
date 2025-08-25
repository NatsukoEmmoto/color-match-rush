using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Controls overall game flow including countdown timer.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        [Header("Timer Settings")]
        [SerializeField, Tooltip("Initial countdown time in seconds.")]
        private float timeLimit = 30f;

        private float remainingTime;
        private bool isRunning = false;

        public float RemainingTime => remainingTime;

        private void Start()
        {
            ResetTimer();
            StartTimer();
        }

        private void Update()
        {
            if (!isRunning) return;

            remainingTime -= Time.deltaTime;
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
        public void ResetTimer()
        {
            remainingTime = timeLimit;
        }

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
            Debug.Log("[GameController] Time is up!");
            // TODO: Show result screen or handle end-of-game logic
        }
    }
}