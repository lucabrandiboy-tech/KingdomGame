using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Buildings seen from straight above, lit from the top-left: the throne on
    /// its dais, the storehouse, braziers, and the markers used while placing
    /// buildings. The pivot is the middle.
    /// </summary>
    public static class BuildingArt
    {
        static readonly Vector2 Light = new Vector2(-0.55f, 0.6f);

        /// <summary>The throne on a stone dais with steps and a red carpet (2 x 2 tiles, facing down).</summary>
        public static PixelImage Throne()
        {
            const int size = 128;
            var img = new PixelImage(size, size);
            var slab = PixelImage.Rgb(84, 82, 78);
            uint seed = 77u;

            // Three stone steps, each a little higher and smaller.
            for (int step = 0; step < 3; step++)
            {
                float inset = 4f + step * 12f;
                float x0 = inset, y0 = inset - (step == 0 ? 0f : 4f), x1 = size - inset, y1 = size - inset + (step == 2 ? 6f : 0f);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float left = px - x0, right = x1 - px, bottom = py - y0, top = y1 - py;
                        float edge = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
                        if (edge <= -0.5f) continue;
                        float shade = 0.92f + step * 0.06f;
                        if (edge < 3f)
                        {
                            float facing = (left < right ? -Light.x : Light.x) * (Mathf.Min(left, right) < 3f ? 1f : 0f)
                                         + (bottom < top ? -Light.y : Light.y) * (Mathf.Min(bottom, top) < 3f ? 1f : 0f);
                            shade *= 1f + facing * 0.45f;
                        }
                        // Slab joints every 16 pixels.
                        bool joint = (x % 16 == 0 || y % 16 == 0) && edge > 3f;
                        float grain = (Noise.Value(x / 5f, y / 5f, seed + (uint)step) - 0.5f) * 0.16f + (Noise.Hash01(seed, x, y) - 0.5f) * 0.08f;
                        var c = PixelImage.Scale(slab, (shade + grain) * (joint ? 0.7f : 1f));
                        img.Blend(x, y, c, edge + 0.5f);
                    }
            }

            // Red carpet from the bottom edge up to the throne.
            var carpet = PixelImage.Rgb(110, 22, 28);
            for (int y = 2; y < 78; y++)
                for (int x = 50; x < 78; x++)
                {
                    bool hem = x < 53 || x >= 75;
                    float weave = (Noise.Hash01(seed + 5u, x, y) - 0.5f) * 0.1f;
                    var c = hem ? PixelImage.Rgb(176, 136, 58) : PixelImage.Scale(carpet, 0.95f + weave + (x == 53 || x == 74 ? -0.2f : 0f));
                    img.Set(x, y, c);
                }

            // The throne: a high carved back, armrests and a velvet seat.
            var wood = PixelImage.Rgb(62, 38, 24);
            var gold = PixelImage.Rgb(196, 152, 62);
            Box(img, 40, 70, 88, 112, wood, 4f, 1f);              // footprint
            Box(img, 40, 100, 88, 116, PixelImage.Scale(wood, 1.15f), 3f, 1f);    // the tall back
            Box(img, 40, 72, 48, 104, PixelImage.Scale(wood, 1.05f), 2.5f, 1f);   // armrests
            Box(img, 80, 72, 88, 104, PixelImage.Scale(wood, 1.05f), 2.5f, 1f);
            Box(img, 49, 74, 79, 99, PixelImage.Rgb(132, 24, 34), 5f, 1.2f);      // the seat cushion
            for (int x = 40; x < 88; x++) { img.Set(x, 115, gold); img.Set(x, 100, PixelImage.Scale(gold, 0.75f)); }
            img.Ellipse(64f, 108f, 4f, 4f, gold);                                 // crest
            img.Ellipse(63f, 109f, 1.6f, 1.6f, PixelImage.Rgb(255, 230, 150));
            img.Ellipse(44f, 103f, 2.6f, 2.6f, gold);
            img.Ellipse(84f, 103f, 2.6f, 2.6f, gold);
            return img;
        }

        /// <summary>A storehouse with a thatched roof and a porch with crates and sacks (2 x 2 tiles).</summary>
        public static PixelImage Storehouse()
        {
            const int size = 128;
            var img = new PixelImage(size, size);
            uint seed = 91u;

            // Porch: wooden boards along the bottom.
            var board = PixelImage.Rgb(96, 70, 46);
            for (int y = 4; y < 30; y++)
                for (int x = 8; x < 120; x++)
                {
                    int plank = (x - 8) / 9;
                    bool gap = (x - 8) % 9 == 0;
                    float grain = (Noise.Value(x / 2f, y / 9f, seed + (uint)plank) - 0.5f) * 0.25f;
                    img.Set(x, y, gap ? PixelImage.Rgb(30, 22, 16) : PixelImage.Scale(board, 0.9f + grain + (Noise.Hash01(seed, plank, 3) - 0.5f) * 0.15f));
                }
            Crate(img, 16, 8, 18);
            Crate(img, 30, 14, 14);
            Crate(img, 96, 8, 18);
            img.Ellipse(77f, 17f, 7f, 6f, PixelImage.Rgb(12, 10, 8, 90));
            img.Ellipse(76f, 18f, 6.5f, 5.5f, PixelImage.Rgb(150, 128, 92));     // grain sacks
            img.Ellipse(86f, 15f, 5.5f, 5f, PixelImage.Rgb(140, 120, 86));
            img.Ellipse(75f, 20f, 2.4f, 1.6f, PixelImage.Rgb(186, 164, 120));

            // Thatched roof: two slopes meeting at a ridge, straw running down each slope.
            const float roofBottom = 26f, roofTop = 124f, ridge = 76f;
            var straw = PixelImage.Rgb(108, 90, 56);
            for (int y = (int)roofBottom; y < (int)roofTop; y++)
                for (int x = 2; x < size - 2; x++)
                {
                    float py = y + 0.5f;
                    float fringe = (Noise.Value(x / 1.5f, 0.5f, seed + 3u, 0) - 0.5f) * 5f;  // ragged straw ends
                    if (py < roofBottom + 2.5f + fringe || py > roofTop - 2.5f - fringe * 0.6f) continue;
                    bool north = py > ridge;
                    // The north slope faces the light (from the top-left), the south one is in shade.
                    // Brighter near the ridge, darker towards the eaves, like a real slope catching the light.
                    float along = north ? (py - ridge) / (roofTop - ridge) : (ridge - py) / (ridge - roofBottom);
                    float slope = (north ? 1.08f : 0.7f) * (1.06f - along * 0.22f);
                    float side = 1f + (x < 12 ? 0.1f * (1f - x / 12f) : 0f) - (x > size - 14 ? 0.2f * (x - (size - 14)) / 14f : 0f);
                    float streak = Noise.Value(x / 1.2f, py / 12f, seed + (north ? 5u : 7u)) - 0.5f;
                    float fine = Noise.Hash01(seed + 13u, x, y) - 0.5f;
                    float clump = Noise.Fractal(x / 9f, py / 9f, seed + 9u, 2) - 0.5f;
                    float course = Mathf.Sin(py * 0.55f + Noise.Value(x / 6f, 0.5f, seed + 15u, 0) * 2f) > 0.75f ? 0.85f : 1f; // rows of straw bundles
                    float ridgeShadow = 1f - Mathf.Clamp01(1f - Mathf.Abs(py - ridge) / 7f) * 0.25f;
                    var c = PixelImage.Scale(straw, (slope + streak * 0.45f + fine * 0.12f + clump * 0.3f) * side * ridgeShadow * course);
                    c = PixelImage.Mix(c, PixelImage.Rgb(72, 76, 44), PixelImage.Smooth(0.62f, 0.75f, clump + 0.5f) * 0.5f); // old, mossy patches
                    img.Set(x, y, c);
                }
            // Ridge cap with stitching.
            for (int x = 2; x < size - 2; x++)
                for (int y = (int)ridge - 3; y <= (int)ridge + 3; y++)
                {
                    float lit = y > ridge ? 0.95f : 0.7f;
                    var c = PixelImage.Scale(PixelImage.Rgb(98, 78, 46), lit + (Noise.Hash01(seed + 11u, x, y) - 0.5f) * 0.2f);
                    if (x % 10 == 0) c = PixelImage.Rgb(52, 40, 26);
                    img.Set(x, y, c);
                }
            // A smoke hole.
            img.Ellipse(92f, 96f, 4.5f, 4f, PixelImage.Rgb(40, 32, 24));
            img.Ellipse(92f, 96f, 2.6f, 2.3f, PixelImage.Rgb(16, 12, 10));
            return img;
        }

        /// <summary>An iron brazier full of glowing coals.</summary>
        public static PixelImage Brazier()
        {
            var img = new PixelImage(28, 28);
            const float c = 14f;
            var iron = PixelImage.Rgb(46, 44, 46);
            img.Ellipse(c, c, 12f, 12f, PixelImage.Scale(iron, 0.7f));
            img.Ellipse(c - 0.6f, c + 0.6f, 11f, 11f, iron);
            img.Ellipse(c, c, 9f, 9f, PixelImage.Rgb(30, 18, 12));
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > 8.2f) continue;
                    float heat = Noise.Value(x / 2.2f, y / 2.2f, 5u) * (1f - d / 10f);
                    var coal = heat > 0.42f ? PixelImage.Mix(PixelImage.Rgb(230, 92, 24), PixelImage.Rgb(255, 214, 120), (heat - 0.42f) * 3f)
                                            : PixelImage.Mix(PixelImage.Rgb(50, 26, 18), PixelImage.Rgb(200, 60, 20), heat * 2.2f);
                    img.Set(x, y, coal);
                }
            return img;
        }

        /// <summary>A square outline for showing grid cells while placing buildings (tinted green or red).</summary>
        public static PixelImage CellMarker()
        {
            const int size = 64;
            var img = new PixelImage(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float edge = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f));
                    float a = edge < 3f ? 0.9f : 0.18f;
                    img.Set(x, y, PixelImage.Rgb(255, 255, 255, Mathf.RoundToInt(a * 255)));
                }
            return img;
        }

        /// <summary>A plain white square, for bars and fills that are tinted in the game.</summary>
        public static PixelImage White(int size = 4)
        {
            var img = new PixelImage(size, size);
            for (int i = 0; i < img.Pixels.Length; i++) img.Pixels[i] = PixelImage.Rgb(255, 255, 255);
            return img;
        }

        static void Crate(PixelImage img, int x0, int y0, int size)
        {
            Box(img, x0 + 1, y0 - 1, x0 + size + 1, y0 + size - 1, PixelImage.Rgb(10, 8, 6, 110), 1f, 0f);
            Box(img, x0, y0, x0 + size, y0 + size, PixelImage.Rgb(128, 94, 58), 2f, 1f);
            for (int i = 0; i < size; i++)
            {
                img.Set(x0 + i, y0 + i, PixelImage.Rgb(88, 62, 38));                // cross brace
                img.Set(x0 + i, y0 + size - 1 - i, PixelImage.Rgb(88, 62, 38));
            }
        }

        // A filled rectangle with a lit/shaded bevel of the given width.
        static void Box(PixelImage img, int x0, int y0, int x1, int y1, Color32 color, float bevel, float bevelStrength)
        {
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    float left = x + 0.5f - x0, right = x1 - x - 0.5f, bottom = y + 0.5f - y0, top = y1 - y - 0.5f;
                    float shade = 1f;
                    if (bevelStrength > 0f && Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top)) < bevel)
                    {
                        float fx = Mathf.Min(left, right) < bevel ? (left < right ? -Light.x : Light.x) : 0f;
                        float fy = Mathf.Min(bottom, top) < bevel ? (bottom < top ? -Light.y : Light.y) : 0f;
                        shade += (fx + fy) * 0.45f * bevelStrength;
                    }
                    img.Blend(x, y, PixelImage.Scale(color, shade));
                }
        }
    }
}
