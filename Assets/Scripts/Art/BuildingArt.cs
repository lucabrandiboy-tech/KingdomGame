using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Buildings seen from straight above, lit from the top-left, with heights
    /// for normal maps: the throne on its dais, the storehouse, and braziers.
    /// Sizes scale with <c>ppu</c> (pixels per tile). The pivot is the middle.
    /// Also the small markers used while placing buildings.
    /// </summary>
    public static class BuildingArt
    {
        /// <summary>The throne on a stone dais with steps and a red carpet (2 x 2 tiles, facing down).</summary>
        public static PixelImage Throne(int ppu)
        {
            float t = ppu;
            int size = 2 * ppu;
            var img = new PixelImage(size, size) { Bumpiness = 1f };
            var heights = img.EnsureHeights();
            uint seed = 77u;
            var slab = PixelImage.Rgb(78, 76, 72);

            // Three stone steps, each a little higher and smaller.
            for (int step = 0; step < 3; step++)
            {
                float inset = (0.03f + step * 0.17f) * t;
                float x0 = inset, y0 = inset - (step == 0 ? 0f : 0.06f * t), x1 = size - inset, y1 = size - inset + (step == 2 ? 0.1f * t : 0f);
                float stepTop = (0.04f + step * 0.045f) * t;
                float bevel = 0.025f * t;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float px = x + 0.5f, py = y + 0.5f;
                        float edge = Mathf.Min(Mathf.Min(px - x0, x1 - px), Mathf.Min(py - y0, y1 - py));
                        edge += (Noise.Value(x / (0.02f * t), y / (0.02f * t), seed + (uint)step) - 0.5f) * 0.005f * t;
                        if (edge <= -0.5f) continue;
                        // Slab joints, staggered row by row; worn and lighter in the middle where people walk.
                        float sy = py / (0.3f * t);
                        float sx = px / (0.4f * t) + (Mathf.FloorToInt(sy) % 2) * 0.5f + step * 0.27f;
                        float jx = Mathf.Abs(sx - Mathf.Round(sx)) * 0.4f * t, jy = Mathf.Abs(sy - Mathf.Round(sy)) * 0.3f * t;
                        bool joint = Mathf.Min(jx, jy) < 0.0025f * t + 0.5f && edge > bevel * 1.5f;
                        float worn = PixelImage.Smooth(0.35f * t, 0f, Mathf.Abs(px - size / 2f)) * PixelImage.Smooth(0f, 0.2f * t, edge);
                        float grain = (Noise.Value(x / (0.004f * t), y / (0.004f * t), seed + 9u) - 0.5f) * 0.14f
                                    + (Noise.Fractal(x / (0.06f * t), y / (0.06f * t), seed + 11u, 2) - 0.5f) * 0.2f;
                        float slabTint = (Noise.Hash01(seed + 13u, Mathf.FloorToInt(sx), Mathf.FloorToInt(sy)) - 0.5f) * 0.12f;
                        var c = PixelImage.Scale(slab, (1f + grain + slabTint + worn * 0.12f) * (joint ? 0.72f : 1f));
                        img.Blend(x, y, c, edge + 0.5f);
                        heights[y * size + x] = stepTop * Mathf.Sqrt(Mathf.Clamp01(edge / bevel)) - (joint ? 0.004f * t : 0f) + grain * 0.004f * t;
                    }
            }

            // Red carpet from the bottom edge up to the throne, with a gold border and fringe.
            var carpet = PixelImage.Rgb(92, 16, 22);
            float cx0 = 0.78f * t, cx1 = 1.22f * t, cy0 = 0.04f * t, cy1 = 1.25f * t;
            int fringeStep = Mathf.Max(2, Mathf.RoundToInt(0.008f * t));
            for (int y = Mathf.FloorToInt(cy0); y < cy1; y++)
                for (int x = Mathf.FloorToInt(cx0); x < cx1; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float fromSide = Mathf.Min(px - cx0, cx1 - px);
                    bool fringe = py < cy0 + 0.03f * t;
                    if (fringe && x % fringeStep == 0) continue;
                    float weave = (Mathf.Sin(px / (0.0025f * t)) * Mathf.Sin(py / (0.0025f * t))) * 0.05f
                                + (Noise.Value(x / (0.003f * t), y / (0.003f * t), seed + 15u) - 0.5f) * 0.12f;
                    Color32 c;
                    if (fromSide < 0.035f * t)
                    {
                        // Gold border with a diamond pattern.
                        float d = Mathf.Abs(((py / (0.04f * t)) % 1f) - 0.5f) + Mathf.Abs(fromSide / (0.035f * t) - 0.5f);
                        c = d < 0.35f ? PixelImage.Rgb(170, 126, 50) : PixelImage.Rgb(110, 76, 28);
                    }
                    else if (fringe) c = PixelImage.Rgb(150, 112, 50);
                    else
                    {
                        float pattern = Mathf.Abs(((py / (0.12f * t)) % 1f) - 0.5f) + Mathf.Abs((px - size / 2f) / (0.12f * t));
                        c = pattern < 0.18f ? PixelImage.Rgb(140, 100, 40) : carpet;
                    }
                    float shade = 1f + weave - PixelImage.Smooth(0.02f * t, 0f, fromSide) * 0.3f;
                    img.Set(x, y, PixelImage.Scale(c, shade));
                    heights[y * size + x] = heights[y * size + x] + 0.006f * t + weave * 0.004f * t;
                }

            // The throne: a high carved back, armrests and a velvet seat.
            var wood = PixelImage.Rgb(46, 28, 18);
            var gold = PixelImage.Rgb(196, 150, 60);
            var velvet = PixelImage.Rgb(116, 20, 30);
            float baseH = 0.13f * t;
            Block(img, 0.62f * t, 1.12f * t, 1.38f * t, 1.72f * t, wood, baseH, 0.05f * t, 0.012f * t, seed + 21u, true);          // seat frame
            Block(img, 0.6f * t, 1.6f * t, 1.4f * t, 1.86f * t, PixelImage.Scale(wood, 1.15f), baseH + 0.3f * t, 0.15f * t, 0.016f * t, seed + 23u, true);  // tall back
            Block(img, 0.6f * t, 1.14f * t, 0.76f * t, 1.66f * t, PixelImage.Scale(wood, 1.05f), baseH + 0.12f * t, 0.06f * t, 0.01f * t, seed + 25u, true);   // armrests
            Block(img, 1.24f * t, 1.14f * t, 1.4f * t, 1.66f * t, PixelImage.Scale(wood, 1.05f), baseH + 0.12f * t, 0.06f * t, 0.01f * t, seed + 27u, true);
            Block(img, 0.8f * t, 1.18f * t, 1.2f * t, 1.58f * t, velvet, baseH + 0.08f * t, 0.05f * t, 0.03f * t, seed + 29u, false); // cushion
            // Button tufting on the cushion.
            for (int bx = 0; bx < 3; bx++)
                for (int by = 0; by < 3; by++)
                    Dent(img, (0.88f + bx * 0.12f) * t, (1.26f + by * 0.12f) * t, 0.018f * t, 0.012f * t, PixelImage.Scale(velvet, 0.6f));
            // Gold trim along the back and gold knobs on the armrests, a crest with a ruby.
            img.Capsule(0.6f * t, 1.85f * t, 1.4f * t, 1.85f * t, 0.012f * t, 0.012f * t, gold, gold, baseH + 0.46f * t, baseH + 0.46f * t);
            img.Capsule(0.6f * t, 1.61f * t, 1.4f * t, 1.61f * t, 0.008f * t, 0.008f * t, PixelImage.Scale(gold, 0.8f), PixelImage.Scale(gold, 0.8f), baseH + 0.44f * t, baseH + 0.44f * t);
            Gem(img, 0.68f * t, 1.17f * t, 0.035f * t, gold, baseH + 0.2f * t);
            Gem(img, 1.32f * t, 1.17f * t, 0.035f * t, gold, baseH + 0.2f * t);
            Gem(img, t, 1.74f * t, 0.06f * t, gold, baseH + 0.5f * t);
            Gem(img, t, 1.74f * t, 0.028f * t, PixelImage.Rgb(170, 20, 40), baseH + 0.53f * t);
            img.BakeLight(0.7f, 0.3f, Mathf.Max(1, Mathf.RoundToInt(0.02f * t)));
            return img;
        }

        /// <summary>A storehouse with a thatched roof and a porch with crates, sacks and a barrel (2 x 2 tiles).</summary>
        public static PixelImage Storehouse(int ppu)
        {
            float t = ppu;
            int size = 2 * ppu;
            var img = new PixelImage(size, size) { Bumpiness = 1f };
            var heights = img.EnsureHeights();
            uint seed = 91u;
            var dice = new Dice(seed);

            // Porch: wooden boards along the bottom.
            var board = PixelImage.Rgb(84, 62, 42);
            float porchTop = 0.46f * t, boardWidth = 0.09f * t;
            for (int y = Mathf.RoundToInt(0.04f * t); y < porchTop; y++)
                for (int x = Mathf.RoundToInt(0.08f * t); x < size - 0.08f * t; x++)
                {
                    int plank = Mathf.FloorToInt(x / boardWidth);
                    float inPlank = x - plank * boardWidth;
                    bool gap = inPlank < 0.006f * t + 0.5f;
                    float grain = (Noise.Value(x / (0.004f * t), y / (0.05f * t), seed + (uint)plank) - 0.5f) * 0.3f;
                    float tint = (Noise.Hash01(seed, plank, 3) - 0.5f) * 0.16f;
                    img.Set(x, y, gap ? PixelImage.Rgb(22, 16, 12) : PixelImage.Scale(board, 0.9f + grain + tint));
                    heights[y * size + x] = gap ? 0f : 0.02f * t + grain * 0.003f * t;
                }
            // Nails at the board ends.
            for (float x = 0.08f * t + boardWidth * 0.5f; x < size - 0.08f * t; x += boardWidth)
                foreach (float y in new[] { 0.08f * t, porchTop - 0.04f * t })
                    Gem(img, x, y, 0.006f * t + 0.5f, PixelImage.Rgb(70, 70, 76), 0.025f * t);
            Crate(img, 0.18f * t, 0.12f * t, 0.26f * t, seed + 1u);
            Crate(img, 0.44f * t, 0.2f * t, 0.2f * t, seed + 2u);
            Crate(img, 1.6f * t, 0.1f * t, 0.26f * t, seed + 3u);
            Barrel(img, 1.38f * t, 0.24f * t, 0.12f * t);
            Sack(img, 0.95f * t, 0.24f * t, 0.11f * t, 0.09f * t, PixelImage.Rgb(132, 112, 80));
            Sack(img, 1.1f * t, 0.2f * t, 0.09f * t, 0.08f * t, PixelImage.Rgb(122, 104, 74));

            // Thatched roof: straw laid down each slope in rows, meeting at a ridge.
            float roofBottom = 0.4f * t, roofTop = 1.96f * t, ridge = 1.2f * t, eave = 0.03f * t;
            var straw = PixelImage.Rgb(98, 82, 52);
            var under = PixelImage.Rgb(40, 32, 22);
            for (int y = Mathf.FloorToInt(roofBottom); y < roofTop; y++)
                for (int x = Mathf.FloorToInt(eave); x < size - eave; x++)
                {
                    img.Set(x, y, under);
                    float py = y + 0.5f;
                    float slopeH = py > ridge ? (roofTop - py) / (roofTop - ridge) : (py - roofBottom) / (ridge - roofBottom);
                    heights[y * size + x] = 0.25f * t + slopeH * 0.35f * t;
                }
            int strands = Mathf.RoundToInt(size * (roofTop - roofBottom) / (t * t) * 9000f);
            for (int i = 0; i < strands; i++)
            {
                float x = dice.Range(eave, size - eave);
                float y = dice.Range(roofBottom, roofTop);
                bool north = y > ridge;
                // Straw points down the slope, away from the ridge.
                float dir = (north ? Mathf.PI / 2f : -Mathf.PI / 2f) + dice.Range(-0.18f, 0.18f);
                float length = dice.Range(0.05f, 0.12f) * t;
                float startY = y - Mathf.Sin(dir) * length * 0.5f;
                if (north ? startY < ridge - 0.01f * t : startY > ridge + 0.01f * t) continue;
                if (north ? y + Mathf.Sin(dir) * length * 0.5f > roofTop : y + Mathf.Sin(dir) * length * 0.5f < roofBottom) continue;
                float slopeH = north ? (roofTop - y) / (roofTop - ridge) : (y - roofBottom) / (ridge - roofBottom);
                float tone = dice.Next();
                float course = Mathf.Sin((north ? roofTop - y : y - roofBottom) / (0.07f * t) * Mathf.PI);   // rows of bundles
                var c = PixelImage.Scale(PixelImage.Mix(straw, PixelImage.Rgb(130, 112, 72), tone), 0.75f + course * 0.12f + (north ? 0.1f : -0.08f));
                float hh = 0.27f * t + slopeH * 0.35f * t + course * 0.01f * t;
                float sx = x - Mathf.Cos(dir) * length * 0.5f;
                img.Capsule(sx, startY, sx + Mathf.Cos(dir) * length, startY + Mathf.Sin(dir) * length, 0.004f * t + 0.5f, 0.0025f * t + 0.4f,
                    PixelImage.Scale(c, 0.8f), c, hh, hh + 0.005f * t);
            }
            // Old, mossy patches and a darker ridge cap bound with rope.
            for (int y = Mathf.FloorToInt(roofBottom); y < roofTop; y++)
                for (int x = Mathf.FloorToInt(eave); x < size - eave; x++)
                {
                    float moss = Noise.Fractal(x / (0.12f * t), y / (0.12f * t), seed + 5u, 3);
                    if (moss > 0.6f) img.Set(x, y, PixelImage.Mix(img.Get(x, y), PixelImage.Rgb(52, 60, 34), PixelImage.Smooth(0.6f, 0.72f, moss) * 0.6f));
                    float py = y + 0.5f;
                    if (Mathf.Abs(py - ridge) < 0.04f * t)
                    {
                        float bind = Mathf.Abs(Mathf.Sin(x / (0.06f * t) * Mathf.PI));
                        var cap = PixelImage.Scale(PixelImage.Rgb(76, 62, 40), 0.7f + (Noise.Hash01(seed + 7u, x, y) - 0.5f) * 0.2f + (py > ridge ? 0.15f : 0f));
                        if (bind < 0.12f) cap = PixelImage.Rgb(46, 36, 24);
                        img.Set(x, y, cap);
                        heights[y * size + x] = 0.62f * t + (1f - Mathf.Abs(py - ridge) / (0.04f * t)) * 0.02f * t;
                    }
                }
            // A smoke hole near the ridge.
            Dent(img, 1.45f * t, 1.45f * t, 0.05f * t, 0.1f * t, PixelImage.Rgb(14, 10, 8));
            img.BakeLight(0.75f, 0.6f, Mathf.Max(1, Mathf.RoundToInt(0.02f * t)));
            return img;
        }

        /// <summary>An iron brazier full of glowing coals (about 0.45 tiles).</summary>
        public static PixelImage Brazier(int ppu)
        {
            float t = ppu;
            int size = Mathf.CeilToInt(0.34f * t);
            var img = new PixelImage(size, size) { Bumpiness = 1f };
            var heights = img.EnsureHeights();
            float c = size / 2f;
            var iron = PixelImage.Rgb(40, 38, 40);
            var light = ArtLight.Sun;
            float outer = 0.16f * t, inner = 0.12f * t;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = outer - d;
                    if (edge <= -0.5f) continue;
                    int i = y * size + x;
                    if (d > inner)
                    {
                        // The iron rim: a rounded ring, lit from the top-left.
                        float across = (d - inner) / (outer - inner) * 2f - 1f;
                        float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - across * across));
                        float lit = (dx / d) * across * light.x + (dy / d) * across * light.y + nz * light.z;
                        img.Blend(x, y, PixelImage.Scale(iron, 0.5f + lit * 0.9f), edge + 0.5f);
                        heights[i] = 0.06f * t + nz * 0.02f * t;
                    }
                    else
                    {
                        // Coals: dark lumps with glowing cracks, hottest in the middle.
                        // Coals: dark lumps of charcoal with glowing cracks between them, hottest in the middle.
                        float lump = Noise.Value(x / (0.012f * t), y / (0.012f * t), 5u);
                        float crack = Mathf.Abs(Noise.Value(x / (0.018f * t), y / (0.018f * t), 7u) - 0.5f);
                        float glow = PixelImage.Smooth(0.09f, 0.0f, crack) * (1.1f - d / inner) + PixelImage.Smooth(0.5f, 0f, d / inner) * 0.35f;
                        var coal = PixelImage.Mix(PixelImage.Scale(PixelImage.Rgb(34, 28, 26), 0.6f + lump * 0.8f), PixelImage.Rgb(255, 120, 40), Mathf.Clamp01(glow));
                        if (glow > 0.8f) coal = PixelImage.Mix(coal, PixelImage.Rgb(255, 220, 140), (glow - 0.8f) * 2f);
                        img.Set(x, y, coal);
                        heights[i] = 0.03f * t + lump * 0.015f * t;
                    }
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

        // ------------------------------------------------------------ pieces --

        // A raised block with a bevel, wood grain (or cloth) and a contact shadow.
        static void Block(PixelImage img, float x0, float y0, float x1, float y1, Color32 color, float height, float raise, float bevel, uint seed, bool woodGrain)
        {
            float t = img.Width / 2f;
            var heights = img.EnsureHeights();
            for (int y = Mathf.FloorToInt(y0 - raise * 0.6f); y < y1; y++)
                for (int x = Mathf.FloorToInt(x0); x < x1 + raise * 0.5f; x++)
                    if (img.Inside(x, y)) img.Blend(x, y, PixelImage.Rgb(6, 4, 4, 120), 1f);
            for (int y = Mathf.FloorToInt(y0); y < y1; y++)
                for (int x = Mathf.FloorToInt(x0); x < x1; x++)
                {
                    if (!img.Inside(x, y)) continue;
                    float px = x + 0.5f, py = y + 0.5f;
                    float edge = Mathf.Min(Mathf.Min(px - x0, x1 - px), Mathf.Min(py - y0, y1 - py));
                    float slope = Mathf.Clamp01(edge / Mathf.Max(0.5f, bevel));
                    float grain = woodGrain
                        ? (Noise.Value(px / (0.003f * t), py / (0.03f * t), seed) - 0.5f) * 0.35f
                        : (Noise.Value(px / (0.004f * t), py / (0.004f * t), seed) - 0.5f) * 0.15f;
                    img.Set(x, y, PixelImage.Scale(color, 1f + grain));
                    heights[y * img.Width + x] = height + raise * Mathf.Sqrt(slope) + grain * 0.004f * t;
                }
        }

        // A small round knob, gem or nail head, glossy.
        static void Gem(PixelImage img, float x, float y, float r, Color32 color, float height)
        {
            var heights = img.EnsureHeights();
            var light = ArtLight.Sun;
            for (int py = Mathf.FloorToInt(y - r - 1); py <= y + r + 1; py++)
                for (int px = Mathf.FloorToInt(x - r - 1); px <= x + r + 1; px++)
                {
                    if (!img.Inside(px, py)) continue;
                    float dx = (px + 0.5f - x) / r, dy = (py + 0.5f - y) / r;
                    float d2 = dx * dx + dy * dy;
                    float edge = (1f - Mathf.Sqrt(d2)) * r + 0.5f;
                    if (edge <= 0f) continue;
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d2));
                    float lit = dx * light.x + dy * light.y + nz * light.z;
                    var c = PixelImage.Scale(color, 0.45f + lit * 0.8f);
                    c = PixelImage.Mix(c, PixelImage.Rgb(255, 244, 200), Mathf.Pow(Mathf.Max(0f, lit), 20f) * 0.9f);
                    img.Blend(px, py, c, edge);
                    heights[py * img.Width + px] = height + nz * r;
                }
        }

        // A dip (button tuft, smoke hole): darker, lower in the middle.
        static void Dent(PixelImage img, float x, float y, float rx, float ry, Color32 color)
        {
            var heights = img.EnsureHeights();
            for (int py = Mathf.FloorToInt(y - ry - 1); py <= y + ry + 1; py++)
                for (int px = Mathf.FloorToInt(x - rx - 1); px <= x + rx + 1; px++)
                {
                    if (!img.Inside(px, py)) continue;
                    float dx = (px + 0.5f - x) / rx, dy = (py + 0.5f - y) / ry;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= 1f) continue;
                    float depth = Mathf.Sqrt(1f - d2);
                    img.Blend(px, py, color, depth * 0.85f);
                    heights[py * img.Width + px] -= depth * Mathf.Min(rx, ry) * 0.8f;
                }
        }

        static void Crate(PixelImage img, float x0, float y0, float s, uint seed)
        {
            float t = img.Width / 2f;
            var wood = PixelImage.Rgb(112, 82, 52);
            Block(img, x0, y0, x0 + s, y0 + s, wood, 0.03f * t, 0.12f * t, 0.012f * t, seed, true);
            float top = 0.15f * t;
            // Boards across the lid and a frame with a cross brace.
            for (int b = 1; b < 3; b++)
            {
                float y = y0 + s * b / 3f;
                img.Capsule(x0 + 0.01f * t, y, x0 + s - 0.01f * t, y, 0.003f * t + 0.4f, 0.003f * t + 0.4f, PixelImage.Rgb(40, 28, 18), PixelImage.Rgb(40, 28, 18), top - 0.004f * t, top - 0.004f * t);
            }
            float f = 0.018f * t;
            var frame = PixelImage.Rgb(92, 66, 42);
            img.Capsule(x0 + f, y0 + f, x0 + s - f, y0 + f, f * 0.6f, f * 0.6f, frame, frame, top + 0.004f * t, top + 0.004f * t);
            img.Capsule(x0 + f, y0 + s - f, x0 + s - f, y0 + s - f, f * 0.6f, f * 0.6f, frame, frame, top + 0.004f * t, top + 0.004f * t);
            img.Capsule(x0 + f, y0 + f, x0 + s - f, y0 + s - f, f * 0.5f, f * 0.5f, frame, frame, top + 0.006f * t, top + 0.006f * t);
            foreach (var corner in new[] { new Vector2(x0 + f, y0 + f), new Vector2(x0 + s - f, y0 + f), new Vector2(x0 + f, y0 + s - f), new Vector2(x0 + s - f, y0 + s - f) })
                Gem(img, corner.x, corner.y, 0.006f * t + 0.5f, PixelImage.Rgb(80, 80, 86), top + 0.01f * t);
        }

        static void Barrel(PixelImage img, float x, float y, float r)
        {
            float t = img.Width / 2f;
            var heights = img.EnsureHeights();
            var light = ArtLight.Sun;
            var wood = PixelImage.Rgb(100, 72, 46);
            img.Ellipse(x + r * 0.25f, y - r * 0.3f, r * 1.1f, r * 1.1f, PixelImage.Rgb(6, 4, 4, 110));
            for (int py = Mathf.FloorToInt(y - r - 1); py <= y + r + 1; py++)
                for (int px = Mathf.FloorToInt(x - r - 1); px <= x + r + 1; px++)
                {
                    if (!img.Inside(px, py)) continue;
                    float dx = px + 0.5f - x, dy = py + 0.5f - y;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = r - d;
                    if (edge <= -0.5f) continue;
                    Color32 c;
                    if (d > r * 0.86f) c = PixelImage.Rgb(52, 50, 52);                     // iron hoop
                    else
                    {
                        float a = Mathf.Atan2(dy, dx);
                        float stave = Mathf.Abs(Mathf.Sin(a * 7f));                           // the lid's boards
                        float grain = Noise.Value(px / (0.003f * t), py / (0.02f * t), 33u);
                        c = PixelImage.Scale(wood, (stave < 0.08f ? 0.5f : 1f) * (0.85f + grain * 0.3f));
                    }
                    float lit = (dx / r) * light.x + (dy / r) * light.y + light.z;
                    img.Blend(px, py, PixelImage.Scale(c, 0.7f + lit * 0.35f), edge + 0.5f);
                    heights[py * img.Width + px] = 0.17f * t + (d > r * 0.86f ? 0.006f * t : 0f);
                }
        }

        static void Sack(PixelImage img, float x, float y, float rx, float ry, Color32 cloth)
        {
            float t = img.Width / 2f;
            var heights = img.EnsureHeights();
            var light = ArtLight.Sun;
            img.Ellipse(x + rx * 0.25f, y - ry * 0.3f, rx * 1.1f, ry * 1.1f, PixelImage.Rgb(6, 4, 4, 110));
            for (int py = Mathf.FloorToInt(y - ry - 1); py <= y + ry + 1; py++)
                for (int px = Mathf.FloorToInt(x - rx - 1); px <= x + rx + 1; px++)
                {
                    if (!img.Inside(px, py)) continue;
                    float dx = (px + 0.5f - x) / rx, dy = (py + 0.5f - y) / ry;
                    float d2 = dx * dx + dy * dy;
                    float edge = (1f - Mathf.Sqrt(d2)) * Mathf.Min(rx, ry) + 0.5f;
                    if (edge <= 0f) continue;
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d2));
                    float weave = Mathf.Sin(px / (0.002f * t)) * Mathf.Sin(py / (0.002f * t)) * 0.06f;
                    float fold = (Noise.Value(px / (0.03f * t), py / (0.03f * t), 41u) - 0.5f) * 0.3f;
                    float lit = (dx + fold) * light.x + (dy + fold) * light.y + nz * light.z;
                    img.Blend(px, py, PixelImage.Scale(cloth, 0.45f + lit * 0.7f + weave), edge);
                    heights[py * img.Width + px] = 0.03f * t + nz * Mathf.Min(rx, ry) * 0.8f + fold * 0.01f * t;
                }
            // The tied neck.
            Gem(img, x, y + ry * 0.75f, Mathf.Min(rx, ry) * 0.22f, PixelImage.Scale(cloth, 0.75f), 0.03f * t + Mathf.Min(rx, ry) * 0.7f);
        }
    }
}
