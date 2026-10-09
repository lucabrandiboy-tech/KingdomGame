using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// A small picture being drawn in code, pixel by pixel. (0, 0) is the
    /// bottom-left pixel, like Unity textures. Turned into a sprite with
    /// <see cref="PixelTextures.ToSprite"/>. Only plain math here, so the art can
    /// be previewed outside Unity.
    /// </summary>
    public sealed class PixelImage
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color32[] Pixels;
        /// <summary>Where the sprite's position sits, 0..1 (0.5, 0 = middle of the bottom edge).</summary>
        public Vector2 Pivot = new Vector2(0.5f, 0f);

        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public PixelImage(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color32[width * height];
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public Color32 Get(int x, int y) => Inside(x, y) ? Pixels[y * Width + x] : Clear;

        public void Set(int x, int y, Color32 color)
        {
            if (Inside(x, y)) Pixels[y * Width + x] = color;
        }

        public bool IsEmpty(int x, int y) => Get(x, y).a == 0;

        /// <summary>Draws rows of text (top row first): each character is looked up in the palette, '.' is skipped.</summary>
        public void DrawRows(string[] rows, int left, int top, string keys, Color32[] palette)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                for (int i = 0; i < rows[r].Length; i++)
                {
                    int k = keys.IndexOf(rows[r][i]);
                    if (k >= 0) Set(left + i, top - r, palette[k]);
                }
            }
        }

        /// <summary>
        /// Puts a 1-pixel outline around everything drawn so far: every empty
        /// pixel touching a filled one (not diagonally) becomes the outline color.
        /// </summary>
        public void Outline(Color32 color)
        {
            var copy = (Color32[])Pixels.Clone();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (copy[y * Width + x].a != 0) continue;
                    bool touches =
                        (x > 0 && copy[y * Width + x - 1].a > 0) ||
                        (x < Width - 1 && copy[y * Width + x + 1].a > 0) ||
                        (y > 0 && copy[(y - 1) * Width + x].a > 0) ||
                        (y < Height - 1 && copy[(y + 1) * Width + x].a > 0);
                    if (touches) Pixels[y * Width + x] = color;
                }
            }
        }

        public static Color32 Shade(Color32 c, int amount) => new Color32(
            (byte)Mathf.Clamp(c.r + amount, 0, 255),
            (byte)Mathf.Clamp(c.g + amount, 0, 255),
            (byte)Mathf.Clamp(c.b + amount, 0, 255),
            c.a);

        public static Color32 Mix(Color32 a, Color32 b, float t) => new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(a.r + (b.r - a.r) * t), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(a.g + (b.g - a.g) * t), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(a.b + (b.b - a.b) * t), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(a.a + (b.a - a.a) * t), 0, 255));

        public static Color32 Rgb(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);
    }

    /// <summary>Repeatable noise for natural-looking textures (same inputs, same picture).</summary>
    public static class Noise
    {
        /// <summary>0..1 random value for a whole-number grid point.</summary>
        public static float Hash01(uint seed, int x, int y) =>
            (Destruction.DamageShape.Hash(seed, x, y) & 0xFFFFFF) / 16777216f;

        /// <summary>Smooth noise, 0..1. Wraps every <paramref name="period"/> cells when period > 0, for tiling textures.</summary>
        public static float Value(float x, float y, uint seed, int period = 0)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            int x1 = x0 + 1, y1 = y0 + 1;
            if (period > 0)
            {
                x0 = Wrap(x0, period); x1 = Wrap(x1, period);
                y0 = Wrap(y0, period); y1 = Wrap(y1, period);
            }
            float a = Hash01(seed, x0, y0), b = Hash01(seed, x1, y0);
            float c = Hash01(seed, x0, y1), d = Hash01(seed, x1, y1);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        /// <summary>Several layers of smooth noise added together ("fractal" noise), 0..1.</summary>
        public static float Fractal(float x, float y, uint seed, int octaves, int period = 0)
        {
            float sum = 0, amplitude = 1, total = 0, frequency = 1;
            for (int i = 0; i < octaves; i++)
            {
                int p = period > 0 ? Mathf.RoundToInt(period * frequency) : 0;
                sum += Value(x * frequency, y * frequency, seed + (uint)i * 101u, p) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return sum / total;
        }

        static int Wrap(int v, int period) => ((v % period) + period) % period;
    }
}
