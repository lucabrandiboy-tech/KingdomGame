using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Trees, stumps, rocks and berry bushes seen from straight above, lit from
    /// the top-left, with heights for normal maps. Each <c>variant</c> number gives
    /// a different shape (variant 3 of the trees is a pine). Sizes scale with
    /// <c>ppu</c> (pixels per tile). The pivot is the middle.
    /// </summary>
    public static class NatureArt
    {
        public const int Variants = 4;

        // ------------------------------------------------------------- trees --

        /// <summary>A tree's crown from above: about 2.8 tiles across (a pine 2.4).</summary>
        public static PixelImage Canopy(int variant, int ppu)
        {
            uint seed = 1000u + (uint)variant * 7919u;
            if (variant % Variants == 3) return Pine(ppu, seed);
            var leaf = new FoliageStyle
            {
                Size = 2.8f, LobeCount = 6 + variant % 3, LobeDistance = 0.7f, CenterRadius = 0.62f, LobeRadius = new Vector2(0.4f, 0.55f),
                Clusters = 420, ClusterRadius = new Vector2(0.065f, 0.12f), LeafLength = new Vector2(0.026f, 0.042f), LeafWidth = 0.45f,
                Palette = new[]
                {
                    PixelImage.Rgb(34, 48, 24), PixelImage.Rgb(42, 56, 26), PixelImage.Rgb(30, 44, 26),
                    PixelImage.Rgb(48, 60, 30), PixelImage.Rgb(38, 52, 32), PixelImage.Rgb(52, 58, 28),
                },
                Brightness = 1f,
            };
            return Foliage(ppu, leaf, seed);
        }

        /// <summary>The bottom of a trunk with its roots, seen from above (shown when the leaves fade).</summary>
        public static PixelImage Trunk(int variant, int ppu) => TreeBase(variant, ppu, false);

        /// <summary>What's left after chopping: roots and a cut face with growth rings.</summary>
        public static PixelImage Stump(int variant, int ppu) => TreeBase(variant, ppu, true);

        static PixelImage TreeBase(int variant, int ppu, bool cut)
        {
            float t = ppu;
            int size = Mathf.CeilToInt(0.8f * t);
            var img = new PixelImage(size, size) { Bumpiness = 1f };
            float c = size / 2f;
            uint seed = 4000u + (uint)variant * 131u;
            var dice = new Dice(seed);
            var bark = PixelImage.Rgb(58, 42, 30);
            var light = ArtLight.Sun;
            var heights = img.EnsureHeights();

            // Roots spreading out, then the trunk on top.
            int roots = 5 + variant % 2;
            float turn = dice.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < roots; i++)
            {
                float a = turn + i / (float)roots * Mathf.PI * 2f + dice.Range(-0.3f, 0.3f);
                float length = dice.Range(0.26f, 0.33f) * t;
                float x0 = c + Mathf.Cos(a) * 0.08f * t, y0 = c + Mathf.Sin(a) * 0.08f * t;
                float bendA = a + dice.Range(-0.4f, 0.4f);
                float xm = c + Mathf.Cos(a) * length * 0.55f, ym = c + Mathf.Sin(a) * length * 0.55f;
                float x1 = xm + Mathf.Cos(bendA) * length * 0.45f, y1 = ym + Mathf.Sin(bendA) * length * 0.45f;
                img.Capsule(x0, y0, xm, ym, 0.065f * t, 0.035f * t, bark, PixelImage.Scale(bark, 0.9f), 0.06f * t, 0.025f * t);
                img.Capsule(xm, ym, x1, y1, 0.035f * t, 0.012f * t, PixelImage.Scale(bark, 0.9f), PixelImage.Scale(bark, 0.8f), 0.025f * t, 0f);
            }
            float trunkR = 0.14f * t;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = trunkR - d;
                    if (edge <= -0.5f) continue;
                    float a = Mathf.Atan2(dy, dx);
                    // Bark: deep ridges running out from the middle, lit from the top-left.
                    float ridge = Noise.Value(a * 9f, d / (0.03f * t), seed + 3u, 0);
                    float fine = Noise.Value(x / (0.008f * t), y / (0.008f * t), seed + 5u);
                    float dome = Mathf.Sqrt(Mathf.Max(0f, 1f - (d / trunkR) * (d / trunkR)));
                    float lit = (dx / trunkR) * light.x + (dy / trunkR) * light.y + dome * light.z;
                    var col = PixelImage.Scale(bark, (0.55f + lit * 0.55f) * (0.7f + ridge * 0.5f) * (0.9f + fine * 0.2f));
                    img.Blend(x, y, col, edge + 0.5f);
                    int i = y * size + x;
                    heights[i] = Mathf.Max(heights[i], dome * 0.12f * t + ridge * 0.012f * t);
                }

            if (cut)
            {
                var wood = PixelImage.Rgb(150, 116, 78);
                float faceR = trunkR * 0.82f;
                float ringShift = dice.Range(0f, 6f);
                float ringSpacing = 0.011f * t;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - c, dy = y + 0.5f - c;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float edge = faceR - d;
                        if (edge <= -0.5f) continue;
                        float a = Mathf.Atan2(dy, dx);
                        float wobble = (Noise.Value(a * 3f, 1f, seed + 7u, 0) - 0.5f) * ringSpacing * 2.5f;
                        float ring = Mathf.Sin((d + wobble) / ringSpacing + ringShift);
                        float grain = Noise.Value(x / (0.004f * t), y / (0.004f * t), seed + 9u);
                        float weather = PixelImage.Smooth(faceR * 0.6f, faceR, d);   // darker towards the bark
                        float shade = (ring > 0.55f ? 0.78f : 1f) * (0.92f + grain * 0.16f) * (1f - weather * 0.3f);
                        var col = PixelImage.Scale(wood, shade);
                        if (d < 0.012f * t) col = PixelImage.Rgb(80, 56, 36);       // the pith
                        img.Blend(x, y, col, edge + 0.5f);
                        int i = y * size + x;
                        heights[i] = 0.13f * t + (ring > 0.55f ? -0.002f * t : 0f);
                    }
                // Drying cracks running in from the bark.
                for (int k = 0; k < 3; k++)
                {
                    float a = dice.Range(0f, Mathf.PI * 2f);
                    float inner = dice.Range(0.2f, 0.5f) * faceR;
                    img.Capsule(c + Mathf.Cos(a) * faceR, c + Mathf.Sin(a) * faceR, c + Mathf.Cos(a + 0.12f) * inner, c + Mathf.Sin(a + 0.12f) * inner,
                        0.004f * t + 0.5f, 0.3f, PixelImage.Rgb(40, 28, 18, 230), PixelImage.Rgb(60, 42, 28, 160), 0.11f * t, 0.12f * t);
                }
            }
            return img;
        }

        // A pine from above: rings of needle-covered branches around a dark middle.
        static PixelImage Pine(int ppu, uint seed)
        {
            float t = ppu;
            int size = Mathf.CeilToInt(2.4f * t);
            var img = new PixelImage(size, size) { Bumpiness = 0.8f };
            float c = size / 2f;
            var dice = new Dice(seed);
            var dark = PixelImage.Rgb(18, 30, 24);
            var mid = PixelImage.Rgb(30, 48, 36);
            var tip = PixelImage.Rgb(58, 78, 54);
            float radius = 1.1f * t;

            // Three tiers of branches, the top tier shortest; needles along each branch.
            for (int tier = 0; tier < 3; tier++)
            {
                int branches = 10 + tier * 2;
                float tierRadius = radius * (1f - tier * 0.22f);
                float baseHeight = tier * 0.25f * t;
                float turn = dice.Range(0f, Mathf.PI * 2f);
                for (int b = 0; b < branches; b++)
                {
                    float a = turn + b / (float)branches * Mathf.PI * 2f + dice.Range(-0.15f, 0.15f);
                    float length = tierRadius * dice.Range(0.82f, 1.05f);
                    int needles = Mathf.RoundToInt(length / t * 260f);
                    for (int k = 0; k < needles; k++)
                    {
                        float along = Mathf.Sqrt(dice.Next()) * length;
                        float spread = (0.06f + along / length * 0.12f) * t;
                        float bx = c + Mathf.Cos(a) * along, by = c + Mathf.Sin(a) * along;
                        float side = dice.Range(-1f, 1f);
                        float px = bx + Mathf.Cos(a + Mathf.PI / 2f) * side * spread * 0.4f;
                        float py = by + Mathf.Sin(a + Mathf.PI / 2f) * side * spread * 0.4f;
                        float na = a + side * 0.9f + dice.Range(-0.25f, 0.25f);
                        float nl = dice.Range(0.025f, 0.05f) * t;
                        float h = baseHeight + (1f - along / length) * 0.3f * t;
                        float tone = dice.Next();
                        var root = PixelImage.Mix(dark, mid, tone * 0.6f);
                        var end = PixelImage.Mix(mid, tip, tone);
                        img.Capsule(px, py, px + Mathf.Cos(na) * nl, py + Mathf.Sin(na) * nl, 0.0045f * t + 0.4f, 0.002f * t + 0.3f, root, end, h, h + nl * 0.3f);
                    }
                }
            }
            ShadeFoliage(img, 0.9f, 0.55f, Mathf.Max(1, Mathf.RoundToInt(0.02f * t)));
            return img;
        }

        // ------------------------------------------------------------- rocks --

        /// <summary>A boulder from above (0.9 to 1.3 tiles): chipped faces, cracks, moss and lichen.</summary>
        public static PixelImage Rock(int variant, int ppu)
        {
            float t = ppu;
            uint seed = 2000u + (uint)variant * 104729u;
            var dice = new Dice(seed);
            float rx = dice.Range(0.42f, 0.6f) * t, ry = rx * dice.Range(0.72f, 0.9f);
            int width = Mathf.CeilToInt(rx * 2.5f), height = Mathf.CeilToInt(ry * 2.5f);
            var img = new PixelImage(width, height) { Bumpiness = 0.9f };
            var heights = img.EnsureHeights();
            float cx = width / 2f, cy = height / 2f;
            var stone = variant % 3 == 1 ? PixelImage.Rgb(88, 82, 74) : variant % 3 == 2 ? PixelImage.Rgb(78, 80, 82) : PixelImage.Rgb(86, 84, 80);

            // Flat faces: random points, each owning the area nearest to it, each tilted its own way.
            int faces = 14;
            var fx = new float[faces]; var fy = new float[faces]; var tx = new float[faces]; var ty = new float[faces];
            for (int i = 0; i < faces; i++)
            {
                float a = dice.Range(0f, Mathf.PI * 2f), d = Mathf.Sqrt(dice.Next()) * 0.9f;
                fx[i] = cx + Mathf.Cos(a) * rx * d;
                fy[i] = cy + Mathf.Sin(a) * ry * d;
                tx[i] = dice.Range(-0.5f, 0.5f);
                ty[i] = dice.Range(-0.5f, 0.5f);
            }
            var light = ArtLight.Sun;
            float crackWidth = 0.004f * t + 0.6f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float around = (Mathf.Atan2(dy, dx) + Mathf.PI) / (2f * Mathf.PI) * 9f;
                    float outline = 1f + (Noise.Value(around, variant * 3.7f, seed, 9) - 0.5f) * 0.3f
                                       + (Noise.Value(around * 4f, variant * 1.3f, seed + 1u, 36) - 0.5f) * 0.06f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / outline;
                    float edgePixels = (1f - d) * Mathf.Min(rx, ry);
                    if (edgePixels <= -0.5f) continue;

                    int nearest = 0, second = 0;
                    float best = float.MaxValue, next = float.MaxValue;
                    for (int i = 0; i < faces; i++)
                    {
                        float ex = x + 0.5f - fx[i], ey = y + 0.5f - fy[i];
                        float e = ex * ex + ey * ey;
                        if (e < best) { next = best; second = nearest; best = e; nearest = i; }
                        else if (e < next) { next = e; second = i; }
                    }
                    float dome = Mathf.Sqrt(Mathf.Max(0.02f, 1f - d * d));
                    float grainX = (Noise.Value(x / (0.012f * t), y / (0.012f * t), seed + 5u) - 0.5f);
                    float grainY = (Noise.Value(x / (0.012f * t) + 31f, y / (0.012f * t), seed + 5u) - 0.5f);
                    float nx = dx * 0.85f + tx[nearest] + grainX * 0.35f, ny = dy * 0.85f + ty[nearest] + grainY * 0.35f, nz = dome + 0.35f;
                    float length = Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
                    float lit = (nx * light.x + ny * light.y + nz * light.z) / length;
                    float speckle = (Noise.Hash01(seed + 9u, x, y) - 0.5f) * 0.1f
                                  + (Noise.Value(x / (0.03f * t), y / (0.03f * t), seed + 7u) - 0.5f) * 0.18f;
                    float rim = PixelImage.Smooth(0.72f, 1f, d);
                    var col = PixelImage.Scale(stone, (0.38f + lit * 0.9f + speckle) * (1f - rim * 0.4f));

                    float seam = Mathf.Sqrt(next) - Mathf.Sqrt(best);
                    bool crack = seam < crackWidth && Noise.Hash01(seed + 13u, Mathf.Min(nearest, second), Mathf.Max(nearest, second)) < 0.45f;
                    if (crack) col = PixelImage.Scale(col, 0.45f);
                    else if (seam < crackWidth * 3f) col = PixelImage.Scale(col, 0.9f);

                    // Moss on the top, lichen spots anywhere.
                    float moss = Noise.Fractal(x / (0.09f * t), y / (0.09f * t), seed + 17u, 3);
                    float mossy = PixelImage.Smooth(0.58f, 0.7f, moss) * PixelImage.Smooth(0.9f, 0.45f, d)
                                * (0.5f + Noise.Value(x / (0.006f * t), y / (0.006f * t), seed + 19u) * 0.5f);
                    col = PixelImage.Mix(col, PixelImage.Scale(PixelImage.Rgb(46, 58, 30), 0.5f + lit * 0.6f), mossy * 0.9f);
                    float lichen = Noise.Value(x / (0.02f * t), y / (0.02f * t), seed + 21u);
                    if (lichen > 0.78f) col = PixelImage.Mix(col, PixelImage.Rgb(140, 138, 112), (lichen - 0.78f) * 2.5f);

                    img.Blend(x, y, col, edgePixels + 0.5f);
                    heights[y * width + x] = dome * Mathf.Min(rx, ry) * 0.7f + (tx[nearest] * dx + ty[nearest] * dy) * 0.08f * t
                                           - (crack ? 0.02f * t : 0f) + mossy * 0.006f * t;
                }
            }
            return img;
        }

        // ------------------------------------------------------------ bushes --

        /// <summary>A berry bush from above (about 1 tile), with or without berries.</summary>
        public static PixelImage Bush(int variant, int ppu, bool berries)
        {
            uint seed = 3000u + (uint)variant * 6151u;
            var style = new FoliageStyle
            {
                Size = 1.05f, LobeCount = 4 + variant % 2, LobeDistance = 0.2f, CenterRadius = 0.24f, LobeRadius = new Vector2(0.15f, 0.21f),
                Clusters = 90, ClusterRadius = new Vector2(0.045f, 0.075f), LeafLength = new Vector2(0.02f, 0.032f), LeafWidth = 0.5f,
                Palette = new[]
                {
                    PixelImage.Rgb(30, 46, 30), PixelImage.Rgb(36, 54, 32), PixelImage.Rgb(28, 42, 28), PixelImage.Rgb(40, 56, 36),
                },
                Brightness = berries ? 1f : 0.85f,
            };
            var img = Foliage(ppu, style, seed);
            if (!berries) return img;

            float t = ppu;
            var dice = new Dice(seed + 77u);
            var berry = PixelImage.Rgb(126, 18, 34);
            var light = ArtLight.Sun;
            var heights = img.EnsureHeights();
            int placed = 0;
            float r = 0.022f * t;
            for (int i = 0; i < 400 && placed < 26; i++)
            {
                float x = dice.Range(0.15f * img.Width, 0.85f * img.Width), y = dice.Range(0.15f * img.Height, 0.85f * img.Height);
                if (img.Get((int)x, (int)y).a < 250 || img.Get((int)(x + r), (int)y).a < 250 || img.Get((int)x, (int)(y - r)).a < 250) continue;
                float baseH = img.HeightAt((int)x, (int)y);
                img.Ellipse(x + r * 0.35f, y - r * 0.35f, r * 1.1f, r * 1.1f, PixelImage.Rgb(8, 12, 6, 150));   // shadow on the leaves
                var tint = PixelImage.Scale(berry, dice.Range(0.8f, 1.2f));
                for (int py = Mathf.FloorToInt(y - r - 1); py <= y + r + 1; py++)
                    for (int px = Mathf.FloorToInt(x - r - 1); px <= x + r + 1; px++)
                    {
                        float dx = (px + 0.5f - x) / r, dy = (py + 0.5f - y) / r;
                        float d2 = dx * dx + dy * dy;
                        float edge = (1f - Mathf.Sqrt(d2)) * r + 0.5f;
                        if (edge <= 0f || !img.Inside(px, py)) continue;
                        float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d2));
                        float lit = dx * light.x + dy * light.y + nz * light.z;
                        var col = PixelImage.Scale(tint, 0.4f + lit * 0.9f);
                        float spec = Mathf.Pow(Mathf.Max(0f, lit), 24f);                     // glossy highlight
                        col = PixelImage.Mix(col, PixelImage.Rgb(255, 210, 210), spec * 0.9f);
                        img.Blend(px, py, col, edge);
                        heights[py * img.Width + px] = baseH + nz * r;
                    }
                placed++;
            }
            return img;
        }

        // ------------------------------------------------------------ shared --

        sealed class FoliageStyle
        {
            public float Size;                // picture size, in tiles
            public int LobeCount;
            public float LobeDistance;        // tiles from the middle
            public float CenterRadius;        // tiles
            public Vector2 LobeRadius;        // tiles
            public int Clusters;
            public Vector2 ClusterRadius;     // tiles
            public Vector2 LeafLength;        // tiles
            public float LeafWidth;           // share of the length
            public Color32[] Palette;
            public float Brightness;
        }

        /// <summary>
        /// Leaves: a lumpy crown made of a few big lobes, covered in clusters of
        /// leaves. Each cluster bulges up; each leaf points out from its cluster's
        /// middle. Higher leaves hide lower ones; the gaps between clusters are dark.
        /// </summary>
        static PixelImage Foliage(int ppu, FoliageStyle style, uint seed)
        {
            float t = ppu;
            int size = Mathf.CeilToInt(style.Size * t);
            var img = new PixelImage(size, size) { Bumpiness = 0.8f };
            var dice = new Dice(seed);
            float c = size / 2f;

            var lobes = new List<Vector3> { new Vector3(c, c, style.CenterRadius * t) };
            float turn = dice.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < style.LobeCount; i++)
            {
                float a = turn + i / (float)style.LobeCount * Mathf.PI * 2f + dice.Range(-0.35f, 0.35f);
                float d = style.LobeDistance * t * dice.Range(0.85f, 1.1f);
                lobes.Add(new Vector3(c + Mathf.Cos(a) * d, c + Mathf.Sin(a) * d, dice.Range(style.LobeRadius.x, style.LobeRadius.y) * t));
            }

            float Mass(float x, float y)
            {
                float best = -10f;
                foreach (var l in lobes)
                {
                    float dx = (x - l.x) / l.z, dy = (y - l.y) / l.z;
                    best = Mathf.Max(best, 1f - (dx * dx + dy * dy));
                }
                return best;
            }

            float crownHeight = style.Size * t * 0.18f;
            var height = img.EnsureHeights();
            for (int i = 0; i < height.Length; i++) height[i] = -1f;
            var cover = new float[size * size];
            var tint = new Color32[size * size];

            for (int k = 0; k < style.Clusters; k++)
            {
                float x = 0, y = 0, m = -1f;
                for (int tries = 0; tries < 16 && m < -0.05f; tries++)
                {
                    x = dice.Range(0.04f * size, 0.96f * size);
                    y = dice.Range(0.04f * size, 0.96f * size);
                    m = Mass(x, y);
                }
                if (m < -0.05f) continue;
                float r = dice.Range(style.ClusterRadius.x, style.ClusterRadius.y) * t;
                float baseHeight = Mathf.Sqrt(Mathf.Max(0f, m)) * crownHeight;
                var clusterColor = style.Palette[dice.Range(0, style.Palette.Length)];

                // Leaves: enough to cover the cluster about 1.6 times.
                float leafLength = Mathf.Lerp(style.LeafLength.x, style.LeafLength.y, 0.5f) * t;
                float leafArea = leafLength * leafLength * style.LeafWidth * 0.8f;
                int leaves = Mathf.Clamp(Mathf.RoundToInt(Mathf.PI * r * r * 1.6f / Mathf.Max(1f, leafArea)), 6, 400);
                for (int l = 0; l < leaves; l++)
                {
                    float la = dice.Range(0f, Mathf.PI * 2f);
                    float ld = Mathf.Sqrt(dice.Next()) * r;
                    float lx = x + Mathf.Cos(la) * ld, ly = y + Mathf.Sin(la) * ld;
                    float len = dice.Range(style.LeafLength.x, style.LeafLength.y) * t;
                    float wid = len * style.LeafWidth * 0.5f;
                    float dir = la + dice.Range(-0.6f, 0.6f);           // pointing out from the cluster's middle
                    float dome = Mathf.Sqrt(Mathf.Max(0f, 1f - (ld / r) * (ld / r)));
                    float h0 = baseHeight + dome * r * 0.75f;
                    var leafColor = PixelImage.Scale(PixelImage.Mix(clusterColor, style.Palette[dice.Range(0, style.Palette.Length)], 0.3f), dice.Range(0.82f, 1.18f));
                    StampLeaf(size, height, cover, tint, lx, ly, dir, len, wid, h0, leafColor);
                }
            }
            // Fill the deep inside of the crown with dark, shadowed leaves.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    if (height[i] >= 0f) continue;
                    float m = Mass(x + 0.5f, y + 0.5f);
                    if (m < 0.3f) continue;
                    height[i] = Mathf.Sqrt(m) * crownHeight * 0.6f;
                    cover[i] = 1f;
                    tint[i] = PixelImage.Scale(style.Palette[0], 0.6f);
                }

            for (int i = 0; i < height.Length; i++)
            {
                if (height[i] < 0f) { height[i] = 0f; continue; }
                var col = tint[i];
                col.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(cover[i]) * 255f);
                img.Pixels[i] = col;
            }
            ShadeFoliage(img, style.Brightness, 0.6f, Mathf.Max(1, Mathf.RoundToInt(0.025f * t)));
            return img;
        }

        // One leaf: a pointed oval along `dir`, bulging up in the middle. Only drawn where it's higher than what's there.
        static void StampLeaf(int size, float[] height, float[] cover, Color32[] tint, float x, float y, float dir, float length, float width, float baseHeight, Color32 color)
        {
            float ca = Mathf.Cos(dir), sa = Mathf.Sin(dir);
            float half = length * 0.5f;
            float cx = x + ca * half, cy = y + sa * half;            // leaves grow from (x, y) outwards
            float reach = half + 1.5f;
            for (int py = Mathf.FloorToInt(cy - reach); py <= cy + reach; py++)
            {
                if (py < 0 || py >= size) continue;
                for (int px = Mathf.FloorToInt(cx - reach); px <= cx + reach; px++)
                {
                    if (px < 0 || px >= size) continue;
                    float dx = px + 0.5f - cx, dy = py + 0.5f - cy;
                    float along = (dx * ca + dy * sa) / half;            // -1..1 base to tip
                    float across = (-dx * sa + dy * ca);
                    if (along < -1f || along > 1f) continue;
                    float w = width * (1f - along * along) * (along > 0f ? 1f - along * 0.35f : 1f);   // pointed at the tip
                    if (w <= 0.05f) continue;
                    float edge = w - Mathf.Abs(across) + 0.5f;
                    if (edge <= 0f) continue;
                    float bulge = Mathf.Sqrt(Mathf.Max(0f, 1f - (across / w) * (across / w)));
                    float h = baseHeight + bulge * w * 0.6f + (1f - Mathf.Abs(along)) * w * 0.3f;
                    int i = py * size + px;
                    if (h <= height[i]) continue;
                    height[i] = h;
                    tint[i] = Mathf.Abs(across) < 0.6f && along > -0.8f && along < 0.7f ? PixelImage.Scale(color, 0.9f) : color;   // midrib
                    cover[i] = Mathf.Max(cover[i], Mathf.Clamp01(edge));
                }
            }
        }

        /// <summary>Lights foliage by its heights: leaves facing the top-left brighter, low and hidden leaves darker.</summary>
        static void ShadeFoliage(PixelImage img, float brightness, float occlusion, int radius)
        {
            int w = img.Width, h = img.Height;
            var height = img.EnsureHeights();
            var light = ArtLight.Sun;
            float lowest = float.MaxValue, highest = float.MinValue;
            for (int i = 0; i < height.Length; i++)
            {
                if (img.Pixels[i].a == 0) continue;
                lowest = Mathf.Min(lowest, height[i]);
                highest = Mathf.Max(highest, height[i]);
            }
            var blurred = PixelImage.BlurClamped(height, w, h, radius);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    var c = img.Pixels[i];
                    if (c.a == 0) continue;
                    float hx = Sample(img, height, x + 1, y, height[i]) - Sample(img, height, x - 1, y, height[i]);
                    float hy = Sample(img, height, x, y + 1, height[i]) - Sample(img, height, x, y - 1, height[i]);
                    float nx = -hx * 0.5f, ny = -hy * 0.5f;
                    float len = Mathf.Sqrt(nx * nx + ny * ny + 1f);
                    float lit = Mathf.Max(0f, (nx * light.x + ny * light.y + light.z) / len);
                    float up = (height[i] - lowest) / Mathf.Max(1f, highest - lowest);
                    float cavity = Mathf.Clamp((height[i] - blurred[i]) / Mathf.Max(1f, radius * 0.8f), -1f, 1f);
                    float shade = (0.25f + lit * 1.05f) * (0.45f + 0.55f * Mathf.Sqrt(Mathf.Clamp01(up))) * (1f + cavity * occlusion * 0.5f) * brightness;
                    var col = PixelImage.Scale(c, shade * 1.25f);
                    if (lit > 0.92f && up > 0.55f) col = PixelImage.Mix(col, PixelImage.Rgb(120, 132, 70), (lit - 0.92f) * 4f * brightness);
                    col.a = c.a;
                    img.Pixels[i] = col;
                }
        }

        static float Sample(PixelImage img, float[] height, int x, int y, float fallback)
        {
            if (!img.Inside(x, y)) return fallback - 3f;
            int i = y * img.Width + x;
            return img.Pixels[i].a == 0 ? fallback - 3f : height[i];
        }
    }
}
