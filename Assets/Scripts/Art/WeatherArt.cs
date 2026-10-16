using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// The weather's small pictures: a streak of rain, a snowflake, a splash, drifting fog and lying snow. They're
    /// simple and quick to draw, so the game draws them itself when it starts (they aren't baked).
    /// </summary>
    public static class WeatherArt
    {
        /// <summary>A falling raindrop seen from above at an angle: a thin streak, brightest at its head (pointing right).</summary>
        public static PixelImage RainStreak(int ppu)
        {
            int length = Mathf.Max(8, Mathf.RoundToInt(0.55f * ppu)), width = Mathf.Max(3, Mathf.RoundToInt(0.035f * ppu));
            var img = new PixelImage(length, width) { Pivot = new Vector2(1f, 0.5f) };
            for (int x = 0; x < length; x++)
                for (int y = 0; y < width; y++)
                {
                    float along = (x + 0.5f) / length;                    // 0 tail .. 1 head
                    float across = Mathf.Abs((y + 0.5f) / width - 0.5f) * 2f;
                    float a = along * along * (1f - across * across);
                    img.Set(x, y, new Color32(206, 216, 232, (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(a))));
                }
            return img;
        }

        /// <summary>A soft snowflake (a little bright dot).</summary>
        public static PixelImage Snowflake(int ppu)
        {
            int size = Mathf.Max(5, Mathf.RoundToInt(0.09f * ppu));
            var img = new PixelImage(size, size);
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / c;
                    float a = Mathf.Clamp01(1f - d);
                    img.Set(x, y, new Color32(240, 244, 250, (byte)Mathf.RoundToInt(255f * a * a)));
                }
            return img;
        }

        /// <summary>A ring where a drop hit the ground.</summary>
        public static PixelImage Splash(int ppu)
        {
            int size = Mathf.Max(7, Mathf.RoundToInt(0.22f * ppu));
            var img = new PixelImage(size, size);
            float c = size / 2f, r = c * 0.75f, w = Mathf.Max(0.8f, c * 0.18f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c));
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - r) / w);
                    img.Set(x, y, new Color32(200, 212, 228, (byte)Mathf.RoundToInt(200f * a)));
                }
            return img;
        }

        /// <summary>A square of drifting fog that repeats seamlessly: soft clouds, white, see-through between them.</summary>
        public static PixelImage Fog(int size, uint seed)
        {
            var img = new PixelImage(size, size) { Pivot = Vector2.zero };   // (laid out from its corner, like the ground)
            const int Cells = 4;   // big soft blobs: four across the square
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Noise.Fractal(x * Cells / (float)size, y * Cells / (float)size, seed, 4, Cells);
                    float a = PixelImage.Smooth(0.25f, 0.75f, n);
                    img.Set(x, y, new Color32(214, 218, 222, (byte)Mathf.RoundToInt(255f * (0.35f + 0.65f * a))));
                }
            return img;
        }

        /// <summary>
        /// Snow lying on the ground, repeating seamlessly: bright, faintly blue in the hollows, with soft drifts and a
        /// little sparkle. Opaque (how much shows is up to whoever draws it).
        /// </summary>
        public static PixelImage SnowGround(int size, uint seed)
        {
            var img = new PixelImage(size, size) { Pivot = Vector2.zero };
            var high = PixelImage.Rgb(240, 243, 248);
            var low = PixelImage.Rgb(206, 214, 228);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Soft drifts (gentle, so the repeat doesn't show), and a fine grain.
                    float drift = Noise.Fractal(x * 5f / size, y * 5f / size, seed, 4, 5);
                    float fine = Noise.Value(x * 64f / size, y * 64f / size, seed + 9u, 64);
                    var col = PixelImage.Mix(low, high, 0.45f + 0.4f * PixelImage.Smooth(0.25f, 0.8f, drift) + fine * 0.15f);
                    if (Noise.Hash01(seed + 21u, x, y) > 0.9975f) col = PixelImage.Rgb(255, 255, 255);   // sparkle
                    img.Set(x, y, col);
                }
            return img;
        }
    }
}
