using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Wall tile pictures (one grid tile, 64 x 64 pixels): stone bricks and wooden
    /// planks with bevels, texture, chips, grain, knots and nails. Pixels are in
    /// Unity's order (bottom row first). Pure math, so it can be previewed outside Unity.
    /// </summary>
    public static class WallArt
    {
        public static Color32[] Bricks(int size, Color32 mortar, Color32[] colors, int variant)
        {
            int brickWidth = size / 2, brickHeight = size / 4;   // both include the mortar line
            const int mortarWidth = 2, bevel = 2;
            uint seed = 500u + (uint)variant * 31u;
            var pixels = new Color32[size * size];

            for (int row = 0; row < size; row++)                 // row 0 = top of the picture
            {
                int course = row / brickHeight;
                int yIn = row % brickHeight;
                int shift = (course % 2) * (brickWidth / 2);
                for (int x = 0; x < size; x++)
                {
                    int xs = x + shift;
                    int xIn = xs % brickWidth;
                    int brick = xs / brickWidth;
                    Color32 c;

                    if (yIn >= brickHeight - mortarWidth || xIn >= brickWidth - mortarWidth)
                    {
                        c = PixelImage.Shade(mortar, Mathf.RoundToInt((Noise.Hash01(seed, x, row) - 0.5f) * 10f));
                        if (yIn == brickHeight - mortarWidth && xIn < brickWidth - mortarWidth) c = PixelImage.Shade(mortar, -12); // shadow under the brick
                    }
                    else
                    {
                        var baseColor = Pick(colors, seed, brick, course);
                        int tint = Mathf.RoundToInt((Noise.Hash01(seed + 3u, brick, course) - 0.5f) * 14f);
                        float grain = Noise.Value(x / 3f, row / 3f, seed + 11u) - 0.5f;
                        c = PixelImage.Shade(baseColor, tint + Mathf.RoundToInt(grain * 22f));

                        int fromRight = brickWidth - mortarWidth - 1 - xIn;
                        int fromBottom = brickHeight - mortarWidth - 1 - yIn;
                        if (yIn < bevel || xIn < bevel) c = PixelImage.Shade(c, yIn == 0 || xIn == 0 ? 26 : 14);
                        else if (fromBottom < bevel || fromRight < bevel) c = PixelImage.Shade(c, fromBottom == 0 || fromRight == 0 ? -28 : -14);

                        // Chips and pits: small dark specks with a light lip above them.
                        if (Noise.Hash01(seed + 21u, x / 2, row / 2) < 0.035f) c = PixelImage.Shade(baseColor, -34);
                    }
                    pixels[(size - 1 - row) * size + x] = c;
                }
            }
            return pixels;
        }

        public static Color32[] Planks(int size, Color32 gap, Color32[] colors, int variant)
        {
            int plankWidth = size / 4;                            // includes the gap
            const int gapWidth = 2;
            uint seed = 900u + (uint)variant * 53u;
            var pixels = new Color32[size * size];

            for (int row = 0; row < size; row++)
            {
                for (int x = 0; x < size; x++)
                {
                    int plank = x / plankWidth;
                    int xIn = x % plankWidth;
                    Color32 c;
                    if (xIn >= plankWidth - gapWidth)
                    {
                        c = gap;
                    }
                    else
                    {
                        var baseColor = Pick(colors, seed, plank, 0);
                        // Wavy grain running down the plank.
                        float wave = Mathf.Sin(row * 0.12f + plank * 1.7f + variant) * 1.4f;
                        float grain = Noise.Value((xIn + wave) * 0.9f, row / 14f, seed + (uint)plank * 7u);
                        int shade = grain < 0.3f ? -22 : grain > 0.72f ? 10 : 0;
                        c = PixelImage.Shade(baseColor, shade);
                        if (xIn == 0) c = PixelImage.Shade(c, 20);
                        else if (xIn >= plankWidth - gapWidth - 2) c = PixelImage.Shade(c, -20);
                    }
                    pixels[(size - 1 - row) * size + x] = c;
                }
            }

            // Knots, and nails near the top and bottom of every plank.
            for (int plank = 0; plank < size / plankWidth; plank++)
            {
                if (Noise.Hash01(seed + 40u, plank, 0) < 0.55f)
                {
                    int kx = plank * plankWidth + 4 + Mathf.RoundToInt(Noise.Hash01(seed + 41u, plank, 0) * (plankWidth - 10));
                    int ky = 12 + Mathf.RoundToInt(Noise.Hash01(seed + 42u, plank, 0) * (size - 24));
                    for (int dy = -3; dy <= 3; dy++)
                        for (int dx = -2; dx <= 2; dx++)
                        {
                            float d = dx * dx / 4f + dy * dy / 9f;
                            if (d > 1f) continue;
                            int i = (size - 1 - (ky + dy)) * size + kx + dx;
                            if (i >= 0 && i < pixels.Length) pixels[i] = PixelImage.Shade(pixels[i], d > 0.5f ? -30 : -12);
                        }
                }
                foreach (int ny in new[] { 6, size - 8 })
                {
                    int nx = plank * plankWidth + plankWidth / 2 - 2;
                    SetTop(pixels, size, nx, ny, PixelImage.Rgb(150, 150, 158));
                    SetTop(pixels, size, nx + 1, ny, PixelImage.Rgb(90, 90, 100));
                    SetTop(pixels, size, nx, ny + 1, PixelImage.Rgb(90, 90, 100));
                    SetTop(pixels, size, nx + 1, ny + 1, PixelImage.Rgb(60, 60, 70));
                }
            }
            return pixels;
        }

        static void SetTop(Color32[] pixels, int size, int x, int rowFromTop, Color32 c)
        {
            if (x < 0 || x >= size || rowFromTop < 0 || rowFromTop >= size) return;
            pixels[(size - 1 - rowFromTop) * size + x] = c;
        }

        static Color32 Pick(Color32[] colors, uint seed, int a, int b)
        {
            if (colors == null || colors.Length == 0) return PixelImage.Rgb(128, 128, 128);
            return colors[Destruction.DamageShape.Hash(seed, a, b) % (uint)colors.Length];
        }
    }
}
