using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColorMatchRush
{
    public class ResultPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private Button retryButton;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        public void Show(int score)
        {
            scoreText.text = $"Score: {score}";
            gameObject.SetActive(true);
        }

        public void BindRetry(System.Action onRetry)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(() => onRetry?.Invoke());
        }
    }
}