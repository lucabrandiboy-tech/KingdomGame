using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// The ground, drawn in code as one picture that repeats seamlessly (its
    /// right edge continues into its left edge, its top into its bottom): dark
    /// grass with patches of bare soil, pebbles, twigs and dead leaves. The world
    /// generator lays a soft tint over it so the repeat isn't noticeable.
    /// </summary>
    public static class GroundArt
    {
        static readonly Color32 GrassDark = PixelImage.Rgb(30, 40, 24);
        static readonly Color32 GrassMid = PixelImage.Rgb(50, 62, 34);
        static readonly Color32 GrassLight = PixelImage.Rgb(74, 82, 46);
        static readonly Color32 GrassDry = PixelImage.Rgb(92, 86, 56);
        static readonly Color32 SoilDark = PixelImage.Rgb(40, 33, 26);
        static readonly Color32 SoilMid = PixelImage.Rgb(60, 49, 37);
        static readonly Color32 SoilLight = PixelImage.Rgb(80, 67, 50);

        /// <param name="size">Width and height in pixels; a multiple of 256 (1024 = 16 tiles).</param>
        public static PixelImage Ground(int size, uint seed)
        {
            var image = new PixelImage(size, size) { Pivot = Vector2.zero };
            var soil = new float[size * size];
            float inv = 1f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x * inv, v = y * inv;
                    // Every noise repeats a whole number of times across the picture, so it tiles.
                    float macro = Noise.Fractal(u * 4f, v * 4f, seed, 3, 4);
                    float patch = Noise.Fractal(u * 6f, v * 6f, seed + 11u, 4, 6);
                    float clump = Noise.Fractal(u * 32f, v * 32f, seed + 23u, 2, 32);
                    float fine = Noise.Value(u * 256f, v * 256f, seed + 31u, 256);
                    float grain = Noise.Value(u * 128f, v * 128f, seed + 37u, 128);

                    // Bare soil where the patch noise is high, with a ragged edge.
                    float edge = patch + (clump - 0.5f) * 0.24f + (fine - 0.5f) * 0.1f;
                    float s = PixelImage.Smooth(0.6f, 0.67f, edge);
                    soil[y * size + x] = s;

                    float g = 0.42f + (macro - 0.5f) * 0.7f + (clump - 0.5f) * 0.8f + (fine - 0.5f) * 0.55f;
                    var grass = Ramp(GrassDark, GrassMid, GrassLight, g);
                    grass = PixelImage.Mix(grass, GrassDry, PixelImage.Smooth(0.6f, 0.8f, macro) * 0.55f);
                    // Grass thins out and gets darker just before the soil starts.
                    grass = PixelImage.Scale(grass, 1f - PixelImage.Smooth(0.45f, 0.62f, edge) * 0.25f);

                    var dirt = Ramp(SoilDark, SoilMid, SoilLight, 0.45f + (grain - 0.5f) * 0.7f + (clump - 0.5f) * 0.45f);
                    image.Pixels[y * size + x] = PixelImage.Mix(grass, dirt, s);
                }
            }

            var dice = new Dice(seed + 101u);

            // Grass blades: short strokes, dark at the root and lighter at the tip,
            // leaning the way the wind laid them (a slowly changing direction).
            int blades = size * size / 7;
            for (int i = 0; i < blades; i++)
            {
                float x = dice.Range(0f, size), y = dice.Range(0f, size);
                float s = soil[Mathf.Clamp((int)y, 0, size - 1) * size + Mathf.Clamp((int)x, 0, size - 1)];
                if (s > 0.55f && !dice.Chance(0.06f)) continue;
                float lean = Noise.Value(x * inv * 8f, y * inv * 8f, seed + 41u, 8) * Mathf.PI * 2f;
                float angle = lean + dice.Range(-0.9f, 0.9f);
                float length = dice.Range(2.5f, 6.5f);
                float tone = dice.Next();
                var root = PixelImage.Scale(Ramp(GrassDark, GrassMid, GrassLight, tone * 0.7f), 0.75f);
                var tip = tone > 0.82f ? PixelImage.Mix(GrassLight, GrassDry, 0.6f) : Ramp(GrassDark, GrassMid, GrassLight, 0.35f + tone * 0.65f);
                root.a = 170;
                tip.a = 200;
                float mx = x + Mathf.Cos(angle) * length * 0.5f, my = y + Mathf.Sin(angle) * length * 0.5f;
                image.Line(x, y, mx, my, 1.1f, root, wrap: true);
                image.Line(mx, my, x + Mathf.Cos(angle) * length, y + Mathf.Sin(angle) * length, 0.9f, tip, wrap: true);
            }

            // Pebbles, mostly on bare soil: a soft shadow, then the stone lit from the top-left.
            int pebbles = size * size / 700;
            for (int i = 0; i < pebbles; i++)
            {
                float x = dice.Range(0f, size), y = dice.Range(0f, size);
                float s = soil[(int)y * size + (int)x];
                if (s < 0.5f && !dice.Chance(0.12f)) continue;
                float rx = dice.Range(1.2f, 3.4f), ry = rx * dice.Range(0.6f, 1f);
                var stone = PixelImage.Scale(PixelImage.Rgb(98, 92, 82), dice.Range(0.75f, 1.2f));
                image.EllipseWrapped(x + 0.8f, y - 0.8f, rx + 0.4f, ry + 0.4f, PixelImage.Rgb(14, 12, 10, 120));
                image.EllipseWrapped(x, y, rx, ry, stone);
                image.EllipseWrapped(x - rx * 0.3f, y + ry * 0.3f, rx * 0.45f, ry * 0.45f, PixelImage.Scale(stone, 1.25f));
            }

            // Twigs on the soil and dead leaves everywhere.
            int twigs = size * size / 9000;
            for (int i = 0; i < twigs; i++)
            {
                float x = dice.Range(0f, size), y = dice.Range(0f, size);
                if (soil[(int)y * size + (int)x] < 0.4f) continue;
                float a = dice.Range(0f, Mathf.PI * 2f), length = dice.Range(5f, 14f);
                float ex = x + Mathf.Cos(a) * length, ey = y + Mathf.Sin(a) * length;
                image.Line(x + 0.7f, y - 0.7f, ex + 0.7f, ey - 0.7f, 1.4f, PixelImage.Rgb(12, 10, 8, 110), wrap: true);
                image.Line(x, y, ex, ey, 1.1f, PixelImage.Rgb(78, 58, 40), wrap: true);
            }
            int leaves = size * size / 1400;
            for (int i = 0; i < leaves; i++)
            {
                float x = dice.Range(0f, size), y = dice.Range(0f, size);
                var leaf = dice.Chance(0.5f) ? PixelImage.Rgb(96, 66, 34) : PixelImage.Rgb(112, 84, 40);
                leaf = PixelImage.Scale(leaf, dice.Range(0.65f, 1.05f));
                image.EllipseWrapped(x, y, dice.Range(0.9f, 1.8f), dice.Range(0.7f, 1.3f), leaf);
            }
            return image;
        }

        // Dark -> mid -> light as t goes 0 -> 0.5 -> 1.
        static Color32 Ramp(Color32 dark, Color32 mid, Color32 light, float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? PixelImage.Mix(dark, mid, t * 2f) : PixelImage.Mix(mid, light, (t - 0.5f) * 2f);
        }
    }
}
