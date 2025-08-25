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

        private void Reset()
        {
            if (timerText == null)
                timerText = GetComponentInChildren<TMP_Text>();
        }

        private void Start()
        {
            if (GameController.Instance != null)
            {
                GameController.Instance.OnTimerChanged += UpdateTimerText;
                UpdateTimerText(GameController.Instance.RemainingTime);
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
            timerText.text = $"{sec / 60:0}:{sec % 60:00}";
        }
    }
}