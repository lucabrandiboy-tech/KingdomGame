using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Trees, stumps, rocks and berry bushes seen from straight above, lit from
    /// the top-left. Each <c>variant</c> number gives a different shape. The pivot
    /// is the middle. Shadows are made from these with <see cref="PixelImage.Shadow"/>.
    /// </summary>
    public static class NatureArt
    {
        static readonly Vector3 Light = new Vector3(-0.55f, 0.6f, 0.58f);

        // ------------------------------------------------------------- trees --

        /// <summary>A tree's leaves from above (about 2.7 tiles across).</summary>
        public static PixelImage Canopy(int variant)
        {
            uint seed = 1000u + (uint)variant * 7919u;
            var palette = new[]
            {
                PixelImage.Rgb(44, 62, 30), PixelImage.Rgb(52, 70, 32), PixelImage.Rgb(40, 56, 30),
                PixelImage.Rgb(58, 74, 34), PixelImage.Rgb(48, 66, 38),
            };
            return Foliage(176, 6 + variant % 3, 44f, 34f, new Vector2(26f, 36f), 900, new Vector2(3.5f, 7f), palette, seed, 1f);
        }

        /// <summary>The bottom of a trunk with its roots, seen from above (shown when the leaves fade).</summary>
        public static PixelImage Trunk(int variant) => TreeBase(variant, false);

        /// <summary>What's left after chopping: roots and a cut face with growth rings.</summary>
        public static PixelImage Stump(int variant) => TreeBase(variant, true);

        static PixelImage TreeBase(int variant, bool cut)
        {
            var img = new PixelImage(44, 44);
            const float c = 22f;
            uint seed = 4000u + (uint)variant * 131u;
            var dice = new Dice(seed);
            var bark = PixelImage.Rgb(66, 48, 34);

            int roots = 5;
            float turn = dice.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < roots; i++)
            {
                float a = turn + i / (float)roots * Mathf.PI * 2f + dice.Range(-0.3f, 0.3f);
                float length = dice.Range(13f, 16f);
                for (int k = 0; k <= 8; k++)
                {
                    float t = k / 8f;
                    float r = Mathf.Lerp(6.5f, 2.6f, t * t);
                    Dome(img, c + Mathf.Cos(a) * length * t, c + Mathf.Sin(a) * length * t, r, r, PixelImage.Scale(bark, 0.9f), 0.5f);
                }
            }
            Dome(img, c, c, 11.5f, 11.5f, bark, cut ? 0.35f : 0.6f);
            for (int y = 0; y < img.Height; y++)                                // bark grooves
                for (int x = 0; x < img.Width; x++)
                {
                    if (img.Get(x, y).a == 0) continue;
                    float a = Mathf.Atan2(y + 0.5f - c, x + 0.5f - c);
                    float groove = Noise.Value(a * 7f, 0.5f, seed, 0) * 0.5f + Noise.Hash01(seed, x, y) * 0.5f;
                    if (groove < 0.3f) img.Set(x, y, PixelImage.Scale(img.Get(x, y), 0.78f));
                }

            if (cut)
            {
                var wood = PixelImage.Rgb(160, 122, 80);
                float ringShift = dice.Range(0f, 6f);
                for (int y = 0; y < img.Height; y++)
                    for (int x = 0; x < img.Width; x++)
                    {
                        float dx = x + 0.5f - c, dy = y + 0.5f - c;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float edge = 9f - d;
                        if (edge <= -0.5f) continue;
                        float wobble = (Noise.Value(Mathf.Atan2(dy, dx) * 3f, 1f, seed + 3u, 0) - 0.5f) * 1.6f;
                        float ring = Mathf.Sin((d + wobble) * 1.6f + ringShift);
                        var col = PixelImage.Scale(wood, (ring > 0.55f ? 0.82f : 1f) * (0.95f + (dx * -0.55f + dy * 0.6f) * 0.012f));
                        if (d < 1.4f) col = PixelImage.Rgb(92, 64, 40);           // the pith
                        img.Blend(x, y, col, edge + 0.5f);
                    }
                for (int k = 0; k < 2; k++)                                        // drying cracks running in from the bark
                {
                    float a = dice.Range(0f, Mathf.PI * 2f);
                    float inner = dice.Range(3f, 6f);
                    img.Line(c + Mathf.Cos(a) * 8.5f, c + Mathf.Sin(a) * 8.5f, c + Mathf.Cos(a + 0.15f) * inner, c + Mathf.Sin(a + 0.15f) * inner,
                        0.8f, PixelImage.Rgb(70, 48, 30, 200));
                }
            }
            return img;
        }

        // ------------------------------------------------------------- rocks --

        /// <summary>A boulder from above: chipped faces, cracks, moss and lichen.</summary>
        public static PixelImage Rock(int variant)
        {
            const int width = 78, height = 68;
            uint seed = 2000u + (uint)variant * 104729u;
            var dice = new Dice(seed);
            var img = new PixelImage(width, height);
            const float cx = width / 2f, cy = height / 2f;
            float rx = dice.Range(28f, 33f), ry = dice.Range(23f, 27f);
            var stone = variant % 3 == 1 ? PixelImage.Rgb(98, 92, 82) : variant % 3 == 2 ? PixelImage.Rgb(86, 88, 90) : PixelImage.Rgb(94, 92, 88);

            // Flat faces: random points, each owning the area nearest to it, each tilted its own way.
            int faces = 11;
            var fx = new float[faces]; var fy = new float[faces]; var tx = new float[faces]; var ty = new float[faces];
            for (int i = 0; i < faces; i++)
            {
                float a = dice.Range(0f, Mathf.PI * 2f), d = Mathf.Sqrt(dice.Next()) * 0.85f;
                fx[i] = cx + Mathf.Cos(a) * rx * d;
                fy[i] = cy + Mathf.Sin(a) * ry * d;
                tx[i] = dice.Range(-0.55f, 0.55f);
                ty[i] = dice.Range(-0.55f, 0.55f);
            }
            var light = Light.normalized;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float around = (Mathf.Atan2(dy, dx) + Mathf.PI) / (2f * Mathf.PI) * 7f;
                    float outline = 1f + (Noise.Value(around, variant * 3.7f, seed, 7) - 0.5f) * 0.34f;
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
                    // A rounded dome, with each face's tilt added on top.
                    float dome = Mathf.Sqrt(Mathf.Max(0.02f, 1f - d * d));
                    float nx = dx * 0.9f + tx[nearest], ny = dy * 0.9f + ty[nearest], nz = dome + 0.35f;
                    float length = Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
                    float lit = (nx * light.x + ny * light.y + nz * light.z) / length;
                    float speckle = (Noise.Hash01(seed + 9u, x, y) - 0.5f) * 0.12f + (Noise.Value(x / 4f, y / 4f, seed + 5u) - 0.5f) * 0.18f;
                    float rim = PixelImage.Smooth(0.7f, 1f, d);                       // darker towards the ground
                    var col = PixelImage.Scale(stone, (0.42f + lit * 0.85f + speckle) * (1f - rim * 0.35f));

                    float seam = Mathf.Sqrt(next) - Mathf.Sqrt(best);                  // distance to the next face
                    if (seam < 0.8f && Noise.Hash01(seed + 13u, nearest, second) < 0.4f)
                        col = PixelImage.Scale(col, 0.55f);                            // a crack between two faces
                    else if (seam < 1.6f)
                        col = PixelImage.Scale(col, 0.88f);                            // a soft edge between faces

                    // Moss on the top, lichen spots anywhere.
                    float moss = Noise.Fractal(x / 9f, y / 9f, seed + 17u, 2);
                    float mossy = PixelImage.Smooth(0.6f, 0.7f, moss) * PixelImage.Smooth(0.85f, 0.5f, d) * (0.55f + Noise.Hash01(seed + 19u, x, y) * 0.45f);
                    col = PixelImage.Mix(col, PixelImage.Scale(PixelImage.Rgb(52, 64, 34), 0.55f + lit * 0.55f), mossy * 0.85f);
                    if (Noise.Hash01(seed + 21u, x / 2, y / 2) < 0.012f) col = PixelImage.Mix(col, PixelImage.Rgb(150, 150, 120), 0.6f);

                    img.Blend(x, y, col, edgePixels + 0.5f);
                }
            }
            return img;
        }

        // ------------------------------------------------------------ bushes --

        /// <summary>A berry bush from above, with or without berries.</summary>
        public static PixelImage Bush(int variant, bool berries)
        {
            uint seed = 3000u + (uint)variant * 6151u;
            var palette = new[]
            {
                PixelImage.Rgb(36, 54, 34), PixelImage.Rgb(42, 62, 36), PixelImage.Rgb(34, 50, 30), PixelImage.Rgb(46, 64, 40),
            };
            var img = Foliage(64, 4, 13f, 15f, new Vector2(9f, 13f), 170, new Vector2(2.8f, 5f), palette, seed, berries ? 1f : 0.82f);
            if (!berries) return img;

            var dice = new Dice(seed + 77u);
            var berry = PixelImage.Rgb(146, 22, 40);
            int placed = 0;
            for (int i = 0; i < 80 && placed < 13; i++)
            {
                float x = dice.Range(10f, 54f), y = dice.Range(10f, 54f);
                if (img.Get((int)x, (int)y).a < 250 || img.Get((int)x + 2, (int)y).a < 250 || img.Get((int)x, (int)y - 2).a < 250) continue;
                img.Ellipse(x + 0.7f, y - 0.7f, 2.4f, 2.4f, PixelImage.Rgb(10, 14, 8, 140));
                img.Ellipse(x, y, 2.2f, 2.2f, PixelImage.Scale(berry, dice.Range(0.8f, 1.15f)));
                img.Ellipse(x - 0.7f, y + 0.7f, 0.9f, 0.9f, PixelImage.Rgb(250, 160, 160, 210));
                placed++;
            }
            return img;
        }

        // ------------------------------------------------------------ shared --

        /// <summary>
        /// Leaves: a lumpy mass made of a few big lobes, covered in small leaf
        /// clumps that each bulge up and catch the light. Higher clumps hide
        /// lower ones, and the gaps between them are dark.
        /// </summary>
        static PixelImage Foliage(int size, int lobeCount, float lobeDistance, float centerRadius, Vector2 lobeRadius,
            int clumpCount, Vector2 clumpRadius, Color32[] palette, uint seed, float brightness)
        {
            var img = new PixelImage(size, size);
            var dice = new Dice(seed);
            float c = size / 2f;

            var lobes = new List<Vector3> { new Vector3(c, c, centerRadius) };
            float turn = dice.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < lobeCount; i++)
            {
                float a = turn + i / (float)lobeCount * Mathf.PI * 2f + dice.Range(-0.35f, 0.35f);
                float d = lobeDistance * dice.Range(0.8f, 1.1f);
                lobes.Add(new Vector3(c + Mathf.Cos(a) * d, c + Mathf.Sin(a) * d, dice.Range(lobeRadius.x, lobeRadius.y)));
            }

            // How far inside the mass a point is (> 0 inside, 1 at a lobe's middle).
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

            float massHeight = size * 0.2f;
            var height = new float[size * size];
            var cover = new float[size * size];
            var tint = new int[size * size];
            for (int i = 0; i < height.Length; i++) height[i] = -1f;

            for (int k = 0; k < clumpCount; k++)
            {
                float x = 0, y = 0, m = -1f;
                for (int tries = 0; tries < 12 && m < -0.08f; tries++)
                {
                    x = dice.Range(4f, size - 4f);
                    y = dice.Range(4f, size - 4f);
                    m = Mass(x, y);
                }
                if (m < -0.08f) continue;
                float r = dice.Range(clumpRadius.x, clumpRadius.y);
                float baseHeight = Mathf.Sqrt(Mathf.Max(0f, m)) * massHeight;
                int colorIndex = dice.Range(0, palette.Length);
                for (int py = Mathf.FloorToInt(y - r - 1); py <= y + r + 1; py++)
                    for (int px = Mathf.FloorToInt(x - r - 1); px <= x + r + 1; px++)
                    {
                        if (px < 0 || py < 0 || px >= size || py >= size) continue;
                        float dx = (px + 0.5f - x) / r, dy = (py + 0.5f - y) / r;
                        float b = 1f - (dx * dx + dy * dy);
                        if (b <= -0.15f) continue;
                        float h = baseHeight + Mathf.Sqrt(Mathf.Max(0f, b)) * r * 0.9f;
                        int i = py * size + px;
                        if (h <= height[i]) continue;
                        height[i] = h;
                        cover[i] = Mathf.Max(cover[i], Mathf.Clamp01((b + 0.15f) * r * 0.5f));
                        tint[i] = colorIndex;
                    }
            }
            // Fill holes deep inside the mass with dark shadowed leaves.
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    if (height[i] >= 0f) continue;
                    float m = Mass(x + 0.5f, y + 0.5f);
                    if (m < 0.25f) continue;
                    height[i] = Mathf.Sqrt(m) * massHeight - 6f;
                    cover[i] = 1f;
                    tint[i] = 0;
                }

            var light = Light.normalized;
            float lowest = float.MaxValue, highest = float.MinValue;
            foreach (float h in height)
            {
                if (h < 0f) continue;
                lowest = Mathf.Min(lowest, h);
                highest = Mathf.Max(highest, h);
            }
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    if (height[i] < 0f) continue;
                    float h = height[i];
                    float hx = Sample(height, size, x + 1, y, h) - Sample(height, size, x - 1, y, h);
                    float hy = Sample(height, size, x, y + 1, h) - Sample(height, size, x, y - 1, h);
                    float nx = -hx * 0.5f, ny = -hy * 0.5f, nz = 1f;
                    float length = Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
                    float lit = Mathf.Max(0f, (nx * light.x + ny * light.y + nz * light.z) / length);

                    // Lower leaves get less light from the sky.
                    float up = (h - lowest) / Mathf.Max(1f, highest - lowest);
                    float occlusion = 0.45f + 0.55f * Mathf.Sqrt(Mathf.Clamp01(up));
                    float leafy = 0.86f + 0.28f * Noise.Value(x / 1.7f, y / 1.7f, seed + 5u);
                    float shade = (0.28f + lit * 0.95f) * occlusion * leafy * brightness;
                    var col = PixelImage.Scale(palette[tint[i]], shade * 1.25f);
                    if (lit > 0.9f && up > 0.6f) col = PixelImage.Mix(col, PixelImage.Rgb(132, 140, 72), (lit - 0.9f) * 3f * brightness);
                    img.Blend(x, y, col, cover[i]);
                }
            }
            return img;
        }

        static float Sample(float[] height, int size, int x, int y, float fallback)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return fallback - 4f;
            float h = height[y * size + x];
            return h < 0f ? fallback - 4f : h;
        }

        // An ellipse lit like a rounded bump (bright towards the top-left).
        static void Dome(PixelImage img, float cx, float cy, float rx, float ry, Color32 color, float strength)
        {
            var light = Light.normalized;
            for (int y = Mathf.FloorToInt(cy - ry - 1); y <= cy + ry + 1; y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= cx + rx + 1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d2 = dx * dx + dy * dy;
                    float edge = (1f - Mathf.Sqrt(d2)) * Mathf.Min(rx, ry) + 0.5f;
                    if (edge <= 0f) continue;
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d2));
                    float lit = dx * light.x + dy * light.y + nz * light.z;
                    img.Blend(x, y, PixelImage.Scale(color, 1f + (lit - 0.55f) * strength * 1.6f), edge);
                }
        }
    }
}
