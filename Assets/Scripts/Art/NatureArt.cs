using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Trees, stumps, rocks and berry bushes, drawn in code with light from the
    /// top-left. Each <paramref name="variant"/> number gives a slightly
    /// different shape. The pivot is where the object touches the ground.
    /// </summary>
    public static class NatureArt
    {
        static readonly Color32 Shadow = PixelImage.Rgb(20, 30, 16, 90);

        // ------------------------------------------------------------- trees --

        public static PixelImage Tree(int variant)
        {
            const int width = 104, height = 112;
            var image = new PixelImage(width, height) { Pivot = new Vector2(0.5f, 8f / height) };
            uint seed = 1000u + (uint)variant * 7919u;

            Ellipse(image, 52, 8, 22, 7, Shadow);                 // shadow on the ground

            var bark = PixelImage.Rgb(112, 74, 46);                // trunk below the leaves
            var barkDark = PixelImage.Rgb(78, 50, 32);
            for (int y = 6; y <= 30; y++)
            {
                int half = y < 9 ? 7 : 5;                          // roots spread at the bottom
                for (int x = 52 - half; x <= 52 + half; x++)
                {
                    bool groove = (x - 52 + 8) % 4 == 0 && Noise.Hash01(seed, x, y / 3) < 0.8f;   // vertical bark lines
                    var c = groove ? barkDark : bark;
                    if (x >= 52 + half - 2) c = PixelImage.Shade(barkDark, -6);
                    else if (x <= 52 - half + 1) c = PixelImage.Shade(bark, 16);
                    image.Set(x, y, c);
                }
            }

            // The canopy is a big circle with smaller leaf clumps around it.
            var greens = new[]
            {
                PixelImage.Rgb(28, 64, 36), PixelImage.Rgb(40, 86, 44), PixelImage.Rgb(56, 108, 52),
                PixelImage.Rgb(78, 132, 60), PixelImage.Rgb(112, 160, 72),
            };
            const int cx = 52, cy = 66;
            var blobs = new Vector3[8];
            blobs[0] = new Vector3(cx, cy, 34);
            for (int i = 1; i < blobs.Length; i++)
            {
                float angle = (i / 7f) * Mathf.PI * 2f + Noise.Hash01(seed, i, 0) * 0.6f;
                float distance = 22f + Noise.Hash01(seed, i, 1) * 6f;
                float radius = 15f + Noise.Hash01(seed, i, 2) * 6f;
                blobs[i] = new Vector3(cx + Mathf.Cos(angle) * distance, cy + Mathf.Sin(angle) * distance * 0.85f, radius);
            }
            Leaves(image, blobs, greens, seed);
            image.Outline(PixelImage.Rgb(18, 40, 24));
            return image;
        }

        public static PixelImage Stump(int variant)
        {
            var image = new PixelImage(30, 22) { Pivot = new Vector2(0.5f, 4f / 22f) };
            Ellipse(image, 15, 4, 13, 4, Shadow);
            var bark = PixelImage.Rgb(98, 64, 40);
            var wood = PixelImage.Rgb(188, 146, 96);
            var ring = PixelImage.Rgb(150, 110, 70);
            for (int y = 4; y <= 10; y++)                              // side of the stump
                for (int x = 5; x <= 24; x++)
                    image.Set(x, y, x > 20 ? PixelImage.Shade(bark, -20) : bark);
            for (int y = 9; y <= 17; y++)                              // cut top with rings
            {
                for (int x = 4; x <= 25; x++)
                {
                    float dx = (x - 14.5f) / 10.5f, dy = (y - 13f) / 4.5f;
                    float d = dx * dx + dy * dy;
                    if (d > 1f) continue;
                    var c = d > 0.75f ? bark : wood;
                    if (d <= 0.75f && (Mathf.RoundToInt(Mathf.Sqrt(d) * 6f + variant) % 2 == 0)) c = ring;
                    image.Set(x, y, c);
                }
            }
            image.Outline(PixelImage.Rgb(40, 28, 20));
            return image;
        }

        // ------------------------------------------------------------- rocks --

        public static PixelImage Rock(int variant)
        {
            const int width = 60, height = 48;
            var image = new PixelImage(width, height) { Pivot = new Vector2(0.5f, 6f / height) };
            uint seed = 2000u + (uint)variant * 104729u;
            Ellipse(image, 30, 6, 26, 6, Shadow);

            var greys = new[]
            {
                PixelImage.Rgb(64, 64, 72), PixelImage.Rgb(88, 88, 96), PixelImage.Rgb(112, 112, 118),
                PixelImage.Rgb(138, 136, 140), PixelImage.Rgb(170, 168, 168),
            };
            const float cx = 30, cy = 22, rx = 25, ry = 17;
            for (int y = 4; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - cx) / rx, dy = (y - cy) / ry;
                    // Lumpy outline (noise that wraps around the circle, so there's no seam).
                    float around = (Mathf.Atan2(dy, dx) + Mathf.PI) / (2f * Mathf.PI) * 6f;
                    float edge = 1f + (Noise.Value(around, variant, seed, 6) - 0.5f) * 0.35f;
                    if (y < 7 || dx * dx + dy * dy > edge * edge) continue;

                    // Flat faces: the rock is split into chunky cells that each catch the light differently.
                    float cell = Noise.Value(x / 9f, y / 9f, seed + 5u);
                    float light = -dx * 0.6f + dy * 0.8f + (cell - 0.5f) * 1.2f;
                    int level = Mathf.Clamp(Mathf.RoundToInt(1.6f + light * 1.4f), 0, greys.Length - 1);
                    var c = greys[level];
                    if (Noise.Hash01(seed, x, y) < 0.06f) c = PixelImage.Shade(c, -14);      // grain
                    image.Set(x, y, c);
                }
            }
            // A couple of cracks.
            for (int k = 0; k < 2; k++)
            {
                int x = 18 + Mathf.RoundToInt(Noise.Hash01(seed, k, 9) * 24f);
                int y = 26 + Mathf.RoundToInt(Noise.Hash01(seed, k, 10) * 8f);
                for (int s = 0; s < 9; s++)
                {
                    if (image.IsEmpty(x, y)) break;
                    image.Set(x, y, greys[0]);
                    y--;
                    x += Noise.Hash01(seed, k, s + 20) < 0.5f ? -1 : 1;
                }
            }
            // Moss on top.
            for (int i = 0; i < 26; i++)
            {
                int x = 12 + Mathf.RoundToInt(Noise.Hash01(seed, i, 30) * 30f);
                int y = 30 + Mathf.RoundToInt(Noise.Hash01(seed, i, 31) * 8f);
                if (!image.IsEmpty(x, y) && !image.IsEmpty(x, y + 2)) image.Set(x, y, PixelImage.Rgb(84, 118, 58));
            }
            image.Outline(PixelImage.Rgb(34, 32, 40));
            return image;
        }

        // ------------------------------------------------------------ bushes --

        public static PixelImage Bush(int variant, bool berries)
        {
            const int width = 50, height = 42;
            var image = new PixelImage(width, height) { Pivot = new Vector2(0.5f, 5f / height) };
            uint seed = 3000u + (uint)variant * 6151u;
            Ellipse(image, 25, 5, 20, 5, Shadow);

            var greens = new[]
            {
                PixelImage.Rgb(26, 58, 40), PixelImage.Rgb(36, 78, 48), PixelImage.Rgb(50, 98, 56),
                PixelImage.Rgb(70, 120, 62), PixelImage.Rgb(96, 146, 72),
            };
            var blobs = new[]
            {
                new Vector3(25, 20, 14),
                new Vector3(14, 16, 10 + Noise.Hash01(seed, 0, 0) * 3),
                new Vector3(36, 16, 10 + Noise.Hash01(seed, 1, 0) * 3),
                new Vector3(19, 27, 10 + Noise.Hash01(seed, 2, 0) * 2),
                new Vector3(31, 28, 9 + Noise.Hash01(seed, 3, 0) * 3),
            };
            Leaves(image, blobs, greens, seed);

            if (berries)
            {
                var berry = PixelImage.Rgb(206, 38, 64);
                var berryShine = PixelImage.Rgb(255, 140, 150);
                int placed = 0;
                for (int i = 0; i < 60 && placed < 11; i++)
                {
                    int x = 8 + Mathf.RoundToInt(Noise.Hash01(seed, i, 40) * 33f);
                    int y = 10 + Mathf.RoundToInt(Noise.Hash01(seed, i, 41) * 24f);
                    if (image.IsEmpty(x, y) || image.IsEmpty(x + 1, y) || image.IsEmpty(x, y - 1) || image.Get(x, y).a < 255) continue;
                    image.Set(x, y, berry); image.Set(x + 1, y, berry);
                    image.Set(x, y - 1, PixelImage.Shade(berry, -50)); image.Set(x + 1, y - 1, PixelImage.Shade(berry, -50));
                    image.Set(x, y, berryShine);
                    placed++;
                }
            }
            image.Outline(PixelImage.Rgb(16, 36, 24));
            return image;
        }

        // ------------------------------------------------------------ shared --

        // Fills overlapping leaf clumps (x, y, radius) with lit and shaded greens and a leafy texture.
        static void Leaves(PixelImage image, Vector3[] blobs, Color32[] greens, uint seed)
        {
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    float best = -1f;
                    float lightAtBest = 0f;
                    foreach (var b in blobs)
                    {
                        float dx = (x - b.x) / b.z, dy = (y - b.y) / b.z;
                        float d2 = dx * dx + dy * dy;
                        float bump = 1f - d2;                          // how far "up" this clump is here
                        if (d2 > 1f || bump <= best) continue;
                        best = bump;
                        lightAtBest = -dx * 0.7f + dy * 0.7f;          // facing the top-left = brighter
                    }
                    if (best < 0f) continue;

                    float leaf = Noise.Value(x / 3.2f, y / 3.2f, seed + 77u) - 0.5f;
                    float value = 2.0f + lightAtBest * 1.6f + leaf * 1.8f + best * 0.6f;
                    int level = Mathf.Clamp(Mathf.FloorToInt(value), 0, greens.Length - 1);
                    image.Set(x, y, greens[level]);
                }
            }
        }

        static void Ellipse(PixelImage image, int cx, int cy, int rx, int ry, Color32 color)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
                    if (dx * dx + dy * dy <= 1f && image.IsEmpty(x, y)) image.Set(x, y, color);
                }
            }
        }
    }
}
