using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// A picture being drawn in code. (0, 0) is the bottom-left pixel, like Unity
    /// textures. Turned into a sprite with <see cref="PixelTextures.ToSprite"/>.
    /// Only plain math here, so the art can be previewed outside Unity.
    /// </summary>
    public sealed class PixelImage
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Color32[] Pixels;
        /// <summary>Where the sprite's position sits, 0..1 (0.5, 0.5 = the middle).</summary>
        public Vector2 Pivot = new Vector2(0.5f, 0.5f);

        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public PixelImage(int width, int height)
        {
            Width = width;
            Height = height;
            Pixels = new Color32[width * height];
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public Color32 Get(int x, int y) => Inside(x, y) ? Pixels[y * Width + x] : Clear;
        public bool IsEmpty(int x, int y) => Get(x, y).a == 0;

        public void Set(int x, int y, Color32 color)
        {
            if (Inside(x, y)) Pixels[y * Width + x] = color;
        }

        /// <summary>Paints <paramref name="color"/> over what's there, using its alpha times <paramref name="coverage"/>.</summary>
        public void Blend(int x, int y, Color32 color, float coverage = 1f)
        {
            if (!Inside(x, y)) return;
            float a = color.a / 255f * Mathf.Clamp01(coverage);
            if (a <= 0f) return;
            int i = y * Width + x;
            var under = Pixels[i];
            float ua = under.a / 255f;
            float outA = a + ua * (1f - a);
            if (outA <= 0f) return;
            Pixels[i] = new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt((color.r * a + under.r * ua * (1f - a)) / outA), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((color.g * a + under.g * ua * (1f - a)) / outA), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt((color.b * a + under.b * ua * (1f - a)) / outA), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(outA * 255f), 0, 255));
        }

        /// <summary>An ellipse with a soft (anti-aliased) edge.</summary>
        public void Ellipse(float cx, float cy, float rx, float ry, Color32 color)
        {
            for (int y = Mathf.FloorToInt(cy - ry - 1); y <= cy + ry + 1; y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= cx + rx + 1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = (1f - d) * Mathf.Min(rx, ry);          // pixels inside the edge
                    if (edge > -0.5f) Blend(x, y, color, edge + 0.5f);
                }
        }

        /// <summary>A soft blurred shadow shaped like this picture (black, <paramref name="opacity"/> 0..1), with room for the blur.</summary>
        public PixelImage Shadow(int blur, float opacity)
        {
            int pad = blur + 1;
            var shadow = new PixelImage(Width + pad * 2, Height + pad * 2);
            var alpha = new float[shadow.Width * shadow.Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    alpha[(y + pad) * shadow.Width + x + pad] = Pixels[y * Width + x].a / 255f;
            for (int pass = 0; pass < 2; pass++)
            {
                alpha = BoxBlur(alpha, shadow.Width, shadow.Height, blur, true);
                alpha = BoxBlur(alpha, shadow.Width, shadow.Height, blur, false);
            }
            for (int i = 0; i < alpha.Length; i++)
                shadow.Pixels[i] = new Color32(0, 0, 0, (byte)Mathf.Clamp(Mathf.RoundToInt(alpha[i] * opacity * 255f), 0, 255));
            shadow.Pivot = new Vector2((Pivot.x * Width + pad) / shadow.Width, (Pivot.y * Height + pad) / shadow.Height);
            return shadow;
        }

        static float[] BoxBlur(float[] src, int w, int h, int r, bool horizontal)
        {
            if (r <= 0) return src;
            var dst = new float[src.Length];
            float scale = 1f / (r * 2 + 1);
            for (int line = 0; line < (horizontal ? h : w); line++)
            {
                int length = horizontal ? w : h;
                float sum = 0;
                for (int k = -r; k <= r; k++) sum += Sample(src, w, h, horizontal, line, k);
                for (int i = 0; i < length; i++)
                {
                    int index = horizontal ? line * w + i : i * w + line;
                    dst[index] = sum * scale;
                    sum += Sample(src, w, h, horizontal, line, i + r + 1) - Sample(src, w, h, horizontal, line, i - r);
                }
            }
            return dst;
        }

        static float Sample(float[] src, int w, int h, bool horizontal, int line, int i)
        {
            int length = horizontal ? w : h;
            if (i < 0 || i >= length) return 0f;
            return src[horizontal ? line * w + i : i * w + line];
        }

        /// <summary>Copies <paramref name="source"/> in with its bottom-left corner at (x, y), replacing what's there.</summary>
        public void Stamp(PixelImage source, int x, int y)
        {
            for (int sy = 0; sy < source.Height; sy++)
                for (int sx = 0; sx < source.Width; sx++)
                    Set(x + sx, y + sy, source.Pixels[sy * source.Width + sx]);
        }

        /// <summary>A soft line <paramref name="width"/> pixels wide (coordinates wrap around the edges if <paramref name="wrap"/>).</summary>
        public void Line(float x0, float y0, float x1, float y1, float width, Color32 color, bool wrap = false)
        {
            float r = width * 0.5f;
            float dx = x1 - x0, dy = y1 - y0;
            float lengthSq = Mathf.Max(1e-6f, dx * dx + dy * dy);
            int minX = Mathf.FloorToInt(Mathf.Min(x0, x1) - r - 1), maxX = Mathf.CeilToInt(Mathf.Max(x0, x1) + r + 1);
            int minY = Mathf.FloorToInt(Mathf.Min(y0, y1) - r - 1), maxY = Mathf.CeilToInt(Mathf.Max(y0, y1) + r + 1);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f - x0, py = y + 0.5f - y0;
                    float t = Mathf.Clamp01((px * dx + py * dy) / lengthSq);
                    float ex = px - dx * t, ey = py - dy * t;
                    float cover = Mathf.Clamp01(r + 0.5f - Mathf.Sqrt(ex * ex + ey * ey));
                    if (cover <= 0f) continue;
                    if (wrap) BlendWrapped(x, y, color, cover);
                    else Blend(x, y, color, cover);
                }
        }

        /// <summary>Like <see cref="Blend"/>, but coordinates wrap around the edges (for repeating textures).</summary>
        public void BlendWrapped(int x, int y, Color32 color, float coverage = 1f) =>
            Blend(Wrap(x, Width), Wrap(y, Height), color, coverage);

        /// <summary>An ellipse with a soft edge whose coordinates wrap around the edges.</summary>
        public void EllipseWrapped(float cx, float cy, float rx, float ry, Color32 color)
        {
            for (int y = Mathf.FloorToInt(cy - ry - 1); y <= cy + ry + 1; y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= cx + rx + 1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float edge = (1f - Mathf.Sqrt(dx * dx + dy * dy)) * Mathf.Min(rx, ry);
                    if (edge > -0.5f) BlendWrapped(x, y, color, edge + 0.5f);
                }
        }

        public static int Wrap(int v, int period) => ((v % period) + period) % period;

        /// <summary>
        /// Gives fully transparent pixels next to the picture the color of their
        /// neighbors (still transparent). Smooth (bilinear) texture filtering
        /// blends edge pixels with their transparent neighbors; without this the
        /// edges would get a dark fringe.
        /// </summary>
        public void BleedEdges(int passes = 3)
        {
            var next = (Color32[])Pixels.Clone();
            var filled = new bool[Pixels.Length];
            for (int i = 0; i < Pixels.Length; i++) filled[i] = Pixels[i].a > 0;
            var newlyFilled = new System.Collections.Generic.List<int>();
            for (int pass = 0; pass < passes; pass++)
            {
                newlyFilled.Clear();
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                    {
                        int i = y * Width + x;
                        if (filled[i]) continue;
                        int r = 0, g = 0, b = 0, n = 0;
                        for (int oy = -1; oy <= 1; oy++)
                            for (int ox = -1; ox <= 1; ox++)
                            {
                                int nx = x + ox, ny = y + oy;
                                if ((ox == 0 && oy == 0) || !Inside(nx, ny)) continue;
                                int j = ny * Width + nx;
                                if (!filled[j]) continue;
                                r += next[j].r; g += next[j].g; b += next[j].b; n++;
                            }
                        if (n == 0) continue;
                        next[i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0);
                        newlyFilled.Add(i);
                    }
                foreach (int i in newlyFilled) filled[i] = true;
            }
            System.Array.Copy(next, Pixels, Pixels.Length);
        }

        public static Color32 Shade(Color32 c, int amount) => new Color32(
            (byte)Mathf.Clamp(c.r + amount, 0, 255),
            (byte)Mathf.Clamp(c.g + amount, 0, 255),
            (byte)Mathf.Clamp(c.b + amount, 0, 255),
            c.a);

        /// <summary>Multiplies the color's brightness (1 = unchanged).</summary>
        public static Color32 Scale(Color32 c, float factor) => new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * factor), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * factor), 0, 255),
            c.a);

        public static Color32 Mix(Color32 a, Color32 b, float t) => new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(a.r + (b.r - a.r) * t), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(a.g + (b.g - a.g) * t), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(a.b + (b.b - a.b) * t), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(a.a + (b.a - a.a) * t), 0, 255));

        public static Color32 Rgb(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);

        public static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>Repeatable random numbers for drawing (same seed, same picture).</summary>
    public struct Dice
    {
        uint state;

        public Dice(uint seed)
        {
            state = seed * 747796405u + 2891336453u;
            if (state == 0) state = 1;
            for (int i = 0; i < 4; i++) Next();
        }

        /// <summary>0 (included) to 1 (not included).</summary>
        public float Next()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / 16777216f;
        }

        public float Range(float min, float max) => min + (max - min) * Next();
        public int Range(int min, int maxExclusive) => Mathf.Min(maxExclusive - 1, min + Mathf.FloorToInt((maxExclusive - min) * Next()));
        public bool Chance(float probability) => Next() < probability;
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
