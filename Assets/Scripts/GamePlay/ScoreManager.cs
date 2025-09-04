using UnityEngine;
using System;

namespace ColorMatchRush
{

    /// <summary>
    /// Holds the current score and notifies listeners on change.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        [SerializeField] private int scorePerPiece = 5;

        public static ScoreManager Instance { get; private set; }

        public event Action<int> OnScoreChanged;

        public int CurrentScore { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void AddScore(int matchedPieces)
        {
            int add = matchedPieces * scorePerPiece;
            CurrentScore += add;
            OnScoreChanged?.Invoke(CurrentScore);
        }

        public void ResetScore()
        {
            CurrentScore = 0;
            OnScoreChanged?.Invoke(CurrentScore);
        }
    }
}