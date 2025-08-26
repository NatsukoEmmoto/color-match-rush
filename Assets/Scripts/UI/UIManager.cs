using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Centralized UI orchestrator: shows result, locks input, and wires retry.
    /// Keeps GameController free from direct UI dependencies.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ResultPanel resultPanel;

        /// <summary>
        /// Show the result view with given score and hook retry callback.
        /// </summary>
        public void ShowResult(int score, System.Action onRetry)
        {
            if (resultPanel == null) return;
            resultPanel.BindRetry(() => onRetry?.Invoke());
            resultPanel.Show(score);
        }

        /// <summary>
        /// Hide the result view (e.g., after retry is pressed).
        /// </summary>
        public void HideResult()
        {
            if (resultPanel != null)
                resultPanel.gameObject.SetActive(false);
        }
    }
}