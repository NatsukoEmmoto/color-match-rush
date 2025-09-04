using UnityEngine;

namespace ColorMatchRush
{
    [CreateAssetMenu(fileName = "BoardConfig", menuName = "ColorMatchRush/BoardConfig", order = 1)]
    public class BoardConfig : ScriptableObject
    {
        [Header("Board Size")]
        [SerializeField] private int width = 8;
        [SerializeField] private int height = 8;

        [Header("Layout")]
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private bool autoCenter = true;
        [SerializeField] private Vector2 explicitOrigin = Vector2.zero;

        [Header("Random/Generation")]
        [SerializeField] private int randomSeed = 0;
        [SerializeField] private bool preventInstantMatchesOnStart = true;
        [SerializeField] private int maxInstantMatchRegenerations = 10;
        [SerializeField] private bool generateOnStart = true;

        [Header("Timings")]
        [SerializeField] private float swapMoveDuration = 0.15f;
        [SerializeField] private float fallMoveDuration = 0.1f;
        [SerializeField] private int spawnOvershootCells = 2;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public bool AutoCenter => autoCenter;
        public Vector2 ExplicitOrigin => explicitOrigin;
        public int RandomSeed => randomSeed;
        public bool PreventInstantMatchesOnStart => preventInstantMatchesOnStart;
        public int MaxInstantMatchRegenerations => maxInstantMatchRegenerations;
        public float SwapMoveDuration => swapMoveDuration;
        public float FallMoveDuration => fallMoveDuration;
        public int SpawnOvershootCells => spawnOvershootCells;
        public bool GenerateOnStart => generateOnStart;

    }
}
