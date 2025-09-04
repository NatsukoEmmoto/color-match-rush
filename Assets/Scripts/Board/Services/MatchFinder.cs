using System.Collections.Generic;

namespace ColorMatchRush
{
    /// <summary>
    /// Provides helper methods to find matches in the board grid.
    /// Stateless: operates only on provided Piece[,] grid.
    /// </summary>
    public class MatchFinder
    {
        // Returns true if there is a 3+ line including cell (row,col)
        public bool CreatesMatchAt(Piece[,] grid, int row, int column)
        {
            Piece center = grid[row, column];
            if (center == null) return false;
            var type = center.Type;

            int horiz = 1 + CountDir(grid, row, column, 0, -1, type) 
                          + CountDir(grid, row, column, 0, 1, type);
            if (horiz >= 3) return true;

            int vert = 1 + CountDir(grid, row, column, -1, 0, type) 
                         + CountDir(grid, row, column, 1, 0, type);
            return vert >= 3;
        }

        private int CountDir(Piece[,] grid, int row, int col, int dr, int dc, Piece.PieceType type)
        {
            int count = 0;
            int h = grid.GetLength(0);
            int w = grid.GetLength(1);
            int r = row + dr, c = col + dc;
            while (r >= 0 && r < h && c >= 0 && c < w)
            {
                var p = grid[r, c];
                if (p == null || p.Type != type) break;
                count++;
                r += dr; c += dc;
            }
            return count;
        }

        /// <summary>
        /// Scan the grid horizontally and vertically, collecting all pieces in runs >=3.
        /// </summary>
        public HashSet<Piece> FindAllMatches(Piece[,] grid)
        {
            var result = new HashSet<Piece>();
            if (grid == null) return result;

            int h = grid.GetLength(0);
            int w = grid.GetLength(1);

            // Horizontal
            for (int r = 0; r < h; r++)
            {
                int c = 0;
                while (c < w)
                {
                    var start = grid[r, c];
                    if (start == null) { c++; continue; }

                    int runStart = c;
                    int runLen = 1;
                    while (c + runLen < w)
                    {
                        var next = grid[r, c + runLen];
                        if (next == null || next.Type != start.Type) break;
                        runLen++;
                    }

                    if (runLen >= 3)
                        for (int k = 0; k < runLen; k++)
                            result.Add(grid[r, runStart + k]);

                    c += runLen;
                }
            }

            // Vertical
            for (int c = 0; c < w; c++)
            {
                int r = 0;
                while (r < h)
                {
                    var start = grid[r, c];
                    if (start == null) { r++; continue; }

                    int runStart = r;
                    int runLen = 1;
                    while (r + runLen < h)
                    {
                        var next = grid[r + runLen, c];
                        if (next == null || next.Type != start.Type) break;
                        runLen++;
                    }

                    if (runLen >= 3)
                        for (int k = 0; k < runLen; k++)
                            result.Add(grid[runStart + k, c]);

                    r += runLen;
                }
            }

            return result;
        }
    }
}