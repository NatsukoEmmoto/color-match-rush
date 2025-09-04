using UnityEngine;

namespace ColorMatchRush
{
    /// <summary>
    /// Handles coordinate conversions between grid (row/col) and world space.
    /// Also computes the board origin based on config.
    /// </summary>
    public class BoardLayout
    {
        private Vector2 origin;

        public Vector2 Origin => origin;

        public void ComputeOrigin(int width, int height, float cellSize, bool autoCenter, Vector2 explicitOrigin)
        {
            if (autoCenter)
            {
                float boardW = width * cellSize;
                float boardH = height * cellSize;
                origin = new Vector2(-boardW * 0.5f, -boardH * 0.5f);
            }
            else
            {
                origin = explicitOrigin;
            }
        }

        public Vector3 CellToWorld(int row, int column, float cellSize)
        {
            float x = origin.x + (column + 0.5f) * cellSize;
            float y = origin.y + (row + 0.5f) * cellSize;
            return new Vector3(x, y, 0f);
        }

        public void WorldToCell(Vector3 world, float cellSize, int width, int height, out int row, out int column)
        {
            float localX = world.x - origin.x;
            float localY = world.y - origin.y;
            column = Mathf.Clamp(Mathf.FloorToInt(localX / cellSize), 0, width - 1);
            row    = Mathf.Clamp(Mathf.FloorToInt(localY / cellSize), 0, height - 1);
        }
    }
}