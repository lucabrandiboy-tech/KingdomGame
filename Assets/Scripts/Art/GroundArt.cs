using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Grass ground, drawn in code as one picture that repeats seamlessly
    /// (its right edge continues into its left edge, its top into its bottom).
    /// </summary>
    public static class GroundArt
    {
        /// <param name="sizeInTiles">Picture size in tiles; must be even (8 is a good size).</param>
        public static PixelImage Grass(int sizeInTiles, uint seed)
        {
            int size = sizeInTiles * PixelArt.PixelsPerUnit;
            var image = new PixelImage(size, size) { Pivot = Vector2.zero };

            var grass = new[]
            {
                PixelImage.Rgb(58, 88, 48), PixelImage.Rgb(66, 98, 52),
                PixelImage.Rgb(74, 108, 56), PixelImage.Rgb(84, 118, 60),
            };
            var dirt = new[] { PixelImage.Rgb(104, 86, 60), PixelImage.Rgb(116, 96, 66) };
            int period = sizeInTiles;  // noise cells per picture, so it tiles

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)PixelArt.PixelsPerUnit, v = y / (float)PixelArt.PixelsPerUnit;
                    float patch = Noise.Fractal(u, v, seed, 3, period);
                    float fine = Noise.Value(x / 4f, y / 4f, seed + 9u, size / 4);
                    int level = Mathf.Clamp(Mathf.FloorToInt(patch * 4.2f - 0.4f + (fine - 0.5f) * 0.9f), 0, grass.Length - 1);
                    var c = grass[level];

                    float bare = Noise.Fractal(u * 0.5f, v * 0.5f, seed + 31u, 2, period / 2);
                    if (bare < 0.28f) c = dirt[(fine > 0.5f) ? 1 : 0];
                    else if (bare < 0.31f) c = PixelImage.Mix(c, dirt[0], 0.5f);

                    image.Pixels[y * size + x] = c;
                }
            }

            // Grass blades: short darker strokes with a lighter tip.
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float h = Noise.Hash01(seed + 3u, x, y);
                    var here = image.Pixels[y * size + x];
                    bool onDirt = here.r > here.g;
                    if (onDirt)
                    {
                        if (h < 0.012f) image.Pixels[y * size + x] = PixelImage.Rgb(140, 132, 120); // pebble
                        continue;
                    }
                    if (h < 0.05f)
                    {
                        image.Pixels[y * size + x] = PixelImage.Shade(here, -18);
                        int up = ((y + 1) % size) * size + x;
                        image.Pixels[up] = PixelImage.Shade(here, -10);
                    }
                    else if (h < 0.065f)
                    {
                        image.Pixels[y * size + x] = PixelImage.Shade(here, 14);
                    }
                    else if (h < 0.0652f)
                    {
                        // A tiny flower.
                        var petal = Noise.Hash01(seed + 5u, x, y) < 0.5f ? PixelImage.Rgb(236, 230, 210) : PixelImage.Rgb(238, 206, 84);
                        Wrap(image, x - 1, y, petal); Wrap(image, x + 1, y, petal);
                        Wrap(image, x, y - 1, petal); Wrap(image, x, y + 1, petal);
                        Wrap(image, x, y, PixelImage.Rgb(214, 150, 50));
                    }
                }
            }
            return image;
        }

        static void Wrap(PixelImage image, int x, int y, Color32 c)
        {
            x = ((x % image.Width) + image.Width) % image.Width;
            y = ((y % image.Height) + image.Height) % image.Height;
            image.Pixels[y * image.Width + x] = c;
        }
    }
}
