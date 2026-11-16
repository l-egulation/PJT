using System.Collections.Generic;

namespace ForestCombatV2
{
    // The loading window's round edge in 2 px blocks (opening-playable.md 「로딩」, user decision 2026-11-13): drawn by the
    // jaggies rule, not by a plain distance test. A plain test gives steps like 6 4 3 3 2 3 2 2 1 2 … whose lengths shrink
    // and grow again, which reads as a bumpy edge. Here one eighth of the plain circle (12 o'clock to 45°) keeps its step
    // heights but has its step lengths sorted longest first, so they only shrink towards 45°; the other seven eighths
    // mirror it. The paper rim and the two shade rings are the cells touching that edge, so they follow the same steps.
    public static class LoadingWindowCircle
    {
        public const int Blocks = ChapterLoadingView.Window / 2;
        const int Half = Blocks / 2;
        public const byte Outside = 0, Inside = 1, Rim = 2, Shade = 3, DeepShade = 4;

        // Block codes, row-major, Blocks x Blocks, for a window `radius` window pixels across from the middle.
        // Shade and DeepShade cells are inside the window too; Rim lies just outside it.
        public static byte[] Draw(int radius)
        {
            var cells = new byte[Blocks * Blocks];
            if (radius <= 0) return cells;
            var heights = ColumnHeights(radius / 2f);
            var inside = new bool[Blocks * Blocks];
            for (int y = 0; y < Blocks; y++)
                for (int x = 0; x < Blocks; x++)
                    inside[y * Blocks + x] = Filled(heights, Quadrant(x), Quadrant(y));
            var inner = new bool[Blocks * Blocks];
            for (int y = 0; y < Blocks; y++)
                for (int x = 0; x < Blocks; x++)
                {
                    int i = y * Blocks + x;
                    if (!inside[i]) { if (Touches(inside, x, y, true)) cells[i] = Rim; continue; }
                    if (Touches(inside, x, y, false)) cells[i] = Shade;
                    else { cells[i] = Inside; inner[i] = true; }
                }
            for (int y = 0; y < Blocks; y++)
                for (int x = 0; x < Blocks; x++)
                {
                    int i = y * Blocks + x;
                    if (inner[i] && Touches(inner, x, y, false)) cells[i] = DeepShade;
                }
            return cells;
        }

        // Cells in each quarter column (column 0 next to the middle) for a circle of `radius` blocks.
        public static int[] ColumnHeights(float radius)
        {
            var plain = new int[Half];
            for (int x = 0; x < Half; x++)
                for (int y = 0; y < Half; y++)
                    if ((x + .5f) * (x + .5f) + (y + .5f) * (y + .5f) <= radius * radius) plain[x]++;
            // The eighth above the diagonal: columns taller than they are far from the middle.
            var steps = new List<int>(); var lengths = new List<int>();
            for (int x = 0; x < Half && plain[x] > x; x++)
            {
                if (steps.Count > 0 && steps[steps.Count - 1] == plain[x]) lengths[lengths.Count - 1]++;
                else { steps.Add(plain[x]); lengths.Add(1); }
            }
            lengths.Sort((a, b) => b.CompareTo(a));
            var heights = new int[Half];
            int column = 0;
            for (int s = 0; s < steps.Count; s++)
                for (int n = 0; n < lengths[s]; n++) heights[column++] = steps[s];
            return heights;
        }

        // Quarter coordinates: 0 next to the middle, growing outwards.
        static int Quadrant(int i) => i >= Half ? i - Half : Half - 1 - i;
        // Above the diagonal the column heights decide; below it, their mirror.
        static bool Filled(int[] heights, int x, int y) => x <= y ? y < heights[x] : x < heights[y];

        // Whether a neighbour up/down/left/right is `want` (cells past the grid count as outside).
        static bool Touches(bool[] grid, int x, int y, bool want)
        {
            return At(grid, x + 1, y) == want || At(grid, x - 1, y) == want || At(grid, x, y + 1) == want || At(grid, x, y - 1) == want;
        }
        static bool At(bool[] grid, int x, int y) => x >= 0 && y >= 0 && x < Blocks && y < Blocks && grid[y * Blocks + x];
    }
}
