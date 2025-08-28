using TMPro;
using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Central HUD controller for in-game UI (timer, score, etc.).
    /// Pulls timer from GameController; score is updated via public API.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text scoreText;

        private void Start()
        {
            if (GameController.Instance != null)
            {
                GameController.Instance.OnTimerChanged += UpdateTimerText;
                UpdateTimerText(GameController.Instance.RemainingTime);
            }
            
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnScoreChanged += UpdateScoreText;
                UpdateScoreText(ScoreManager.Instance.CurrentScore);
            }
        }

        private void OnDisable()
        {
            if (GameController.Instance != null)
                GameController.Instance.OnTimerChanged -= UpdateTimerText;
        }

        private void UpdateTimerText(float timeSec)
        {
            int sec = Mathf.CeilToInt(timeSec);
            //timerText.text = $"{sec / 60:0}:{sec % 60:00}";
            timerText.text = $"{sec}";
        }

        private void UpdateScoreText(int score)
        {
            if (scoreText == null) return;
            scoreText.text = score.ToString();
        }
    }
}