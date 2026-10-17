using UnityEngine;

namespace KingdomGame.Art
{
    // Underground belts and splitters (Automation/FactoryWorld.cs), for the belt kinds that have them: the conveyor belt,
    // the fast belt and the express belt (belt styles 4, 5 and 6). Drawn facing right, turned for each way they face.
    public static partial class FurnitureArt
    {
        /// <summary>The belt styles that have underground belts and splitters.</summary>
        public static readonly int[] BeltPartStyles = { 4, 5, 6 };

        /// <summary>An underground belt's entrance (things go into the ground) or exit (they come out), of belt style <paramref name="style"/>.</summary>
        public static string UndergroundArt(int style, bool exit) => $"Factory/Underground{style}{(exit ? "Out" : "In")}";
        /// <summary>A splitter of belt style <paramref name="style"/> (two tiles across the way things go).</summary>
        public static string SplitterArt(int style) => $"Factory/Splitter{style}";

        // A straight belt carrying things to the right from x0 to x1 (tiles), one tile wide from y0: rails, the moving
        // top with its slats or rollers, and chevrons on the fast ones (as BeltPiece draws a straight one).
        static void BeltSurface(PixelImage img, float t, int style, float x0, float x1, float y0)
        {
            var look = BeltLookOf(style);
            uint seed = 4100u + (uint)style * 13u;
            int ix0 = Mathf.Max(0, Mathf.FloorToInt(x0 * t)), ix1 = Mathf.Min(img.Width, Mathf.CeilToInt(x1 * t));
            int iy0 = Mathf.Max(0, Mathf.FloorToInt(y0 * t)), iy1 = Mathf.Min(img.Height, Mathf.CeilToInt((y0 + 1f) * t));
            for (int y = iy0; y < iy1; y++)
                for (int x = ix0; x < ix1; x++)
                {
                    float u = (x + 0.5f) / t, v = (y + 0.5f) / t - y0;
                    float coverage = Mathf.Clamp01(Mathf.Min(v - 0.06f, 0.94f - v) * t + 0.5f);
                    if (coverage <= 0f) continue;
                    Color32 c;
                    float h;
                    if (v < 0.16f || v > 0.84f)
                    {
                        float across = v < 0.16f ? (v - 0.06f) / 0.1f : (0.94f - v) / 0.1f;
                        c = PixelImage.Scale(look.Rail, Texture(look.RailSurface, u, v, seed, true));
                        h = 0.07f + 0.03f * Mathf.Sin(Mathf.Clamp01(across) * Mathf.PI);
                    }
                    else
                    {
                        c = PixelImage.Scale(look.Top, Texture(look.TopSurface, u, v * 0.6f, seed + 1u, true));
                        h = 0.055f;
                        float d = Mathf.Abs(u * 4f - Mathf.Floor(u * 4f) - 0.5f);
                        if (look.Slats == 1 && d > 0.44f)
                        {
                            c = PixelImage.Scale(look.Slat, 0.9f);
                            h = 0.05f;
                        }
                        if (look.Arrows)
                        {
                            float w = Mathf.Abs(v - 0.5f), f = u * 2f + w * 1.3f;
                            if (w < 0.24f && f - Mathf.Floor(f) < 0.12f)
                            {
                                c = look.Arrow;
                                h = 0.058f;
                            }
                        }
                    }
                    img.Blend(x, y, c, coverage);
                    Lift(img, x, y, h * t, coverage);
                }
        }

        // The housing an underground belt goes into or comes out of, over x0..x1, with its dark mouth on the side
        // facing the belt (left when <paramref name="mouthLeft"/>), and the belt kind's colour on its back.
        static void Tunnel(PixelImage img, float t, int style, float x0, float x1, bool mouthLeft, uint seed)
        {
            var look = BeltLookOf(style);
            var steel = PixelImage.Rgb(92, 94, 100);
            Block(img, t, x0, 0.04f, x1, 0.96f, steel, 0.26f, 0.05f, Surface.Metal, seed);
            // The coloured band on its far end, ribs across the top, and its mouth.
            float bx0 = mouthLeft ? x1 - 0.12f : x0, bx1 = mouthLeft ? x1 : x0 + 0.12f;
            Block(img, t, bx0, 0.06f, bx1, 0.94f, look.Rail, 0.28f, 0.02f, Surface.Metal, seed + 1u);
            for (int i = 0; i < 3; i++)
            {
                float x = Mathf.Lerp(x0 + 0.1f, x1 - 0.18f, i / 2f) + (mouthLeft ? 0f : 0.08f);
                Rod(img, t, x, 0.1f, x, 0.9f, 0.015f, PixelImage.Scale(steel, 1.2f), 0.27f, 0.27f);
            }
            float mx0 = mouthLeft ? x0 : x1 - 0.13f, mx1 = mouthLeft ? x0 + 0.13f : x1;
            Block(img, t, mx0, 0.17f, mx1, 0.83f, PixelImage.Rgb(14, 13, 12), 0.03f, 0.01f, Surface.Plain, seed + 2u);
            if (look.Arrows)
            {
                // An arrow on top: the way things go.
                float cx = (x0 + x1) * 0.5f;
                Rod(img, t, cx - 0.08f, 0.32f, cx + 0.06f, 0.5f, 0.022f, look.Arrow, 0.29f, 0.29f);
                Rod(img, t, cx - 0.08f, 0.68f, cx + 0.06f, 0.5f, 0.022f, look.Arrow, 0.29f, 0.29f);
            }
        }

        /// <summary>
        /// An underground belt facing right, turned to <paramref name="rotation"/>. Its entrance: the belt runs into a housing on
        /// its right half (things go under the ground there). Its exit: they come out of a housing on its left half onto the belt.
        /// </summary>
        public static PixelImage UndergroundPiece(int ppu, int style, bool exit, int rotation)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            uint s = 6100u + (uint)style * 7u + (exit ? 3u : 0u);
            if (exit)
            {
                BeltSurface(img, t, style, 0.4f, 1f, 0f);
                Tunnel(img, t, style, 0.02f, 0.55f, false, s);
            }
            else
            {
                BeltSurface(img, t, style, 0f, 0.6f, 0f);
                Tunnel(img, t, style, 0.45f, 0.98f, true, s);
            }
            return Finish(img, t, rotation, 0.6f, 0.4f);
        }

        /// <summary>
        /// A splitter facing right (two tiles high: two belts side by side), turned to <paramref name="rotation"/>: the belts run
        /// under a housing across them both that shares out what comes in between the two belts going out.
        /// </summary>
        public static PixelImage SplitterPiece(int ppu, int style, int rotation)
        {
            float t = ppu;
            var img = Canvas(1f, 2f, t);
            uint s = 6200u + (uint)style * 7u;
            var look = BeltLookOf(style);
            BeltSurface(img, t, style, 0f, 1f, 0f);
            BeltSurface(img, t, style, 0f, 1f, 1f);
            var steel = PixelImage.Rgb(96, 98, 104);
            Block(img, t, 0.3f, 0.03f, 0.7f, 1.97f, steel, 0.3f, 0.05f, Surface.Metal, s);
            Block(img, t, 0.33f, 0.06f, 0.67f, 0.18f, look.Rail, 0.31f, 0.02f, Surface.Metal, s + 1u);
            Block(img, t, 0.33f, 1.82f, 0.67f, 1.94f, look.Rail, 0.31f, 0.02f, Surface.Metal, s + 1u);
            // Where the belts go under it, and the flap in the middle that sends things one way and the other.
            foreach (float y in new[] { 0.5f, 1.5f })
            {
                Block(img, t, 0.3f, y - 0.3f, 0.36f, y + 0.3f, PixelImage.Rgb(16, 15, 14), 0.05f, 0.005f, Surface.Plain, s + 2u);
                Block(img, t, 0.64f, y - 0.3f, 0.7f, y + 0.3f, PixelImage.Rgb(16, 15, 14), 0.05f, 0.005f, Surface.Plain, s + 2u);
            }
            Round(img, t, 0.5f, 1f, 0.14f, PixelImage.Scale(steel, 0.8f), 0.36f, 0.03f, Surface.Metal, s + 3u);
            Rod(img, t, 0.5f, 1f, 0.62f, 0.62f, 0.025f, look.Arrows ? look.Arrow : PixelImage.Rgb(200, 190, 170), 0.38f, 0.38f);
            Rod(img, t, 0.5f, 1f, 0.62f, 1.38f, 0.025f, look.Arrows ? look.Arrow : PixelImage.Rgb(200, 190, 170), 0.38f, 0.38f);
            Stud(img, t, 0.5f, 1f, 0.04f, Iron, 0.4f);
            return Finish(img, t, rotation, 0.6f, 0.4f);
        }
    }
}
