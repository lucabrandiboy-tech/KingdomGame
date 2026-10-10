using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Wall tiles seen from straight above (one grid tile, <c>size</c> x <c>size</c>
    /// pixels): rough fieldstones in mortar, and the cut ends of palisade logs.
    /// Every pixel is solid (the wall's collider follows its pixels), so the gaps
    /// between logs are dark wood, not holes. The picture has heights for its
    /// normal map. Pixels are in Unity's order (bottom row first).
    /// </summary>
    public static class WallArt
    {
        /// <summary>
        /// Rough fieldstones of mixed sizes packed in dark mortar, each one rounded and
        /// lit from the top-left, with lichen, moss in the joints and a few cracks.
        /// Irregular stones, so a long wall reads as stonework, not as a grid of tiles.
        /// </summary>
        public static PixelImage Stone(int size, Color32 mortar, Color32[] colors, int variant)
        {
            float t = size;   // one tile
            uint seed = 500u + (uint)variant * 31u;
            var dice = new Dice(seed);
            var img = new PixelImage(size, size) { Bumpiness = 1f };
            var heights = img.EnsureHeights();
            float joint = 0.008f * t + 0.5f;        // half the joint between stones
            float bevel = 0.07f * t;                 // how far in from its edge a stone is rounded
            float top = 0.09f * t;

            // Stones: a jittered 3 x 3 grid, each with a random size boost (bigger
            // stones push their neighbors' edges back) and its own height.
            const int grid = 3;
            int count = grid * grid;
            var cx = new float[count];
            var cy = new float[count];
            var boost = new float[count];
            var lift = new float[count];
            for (int gy = 0; gy < grid; gy++)
                for (int gx = 0; gx < grid; gx++)
                {
                    int k = gy * grid + gx;
                    cx[k] = (gx + 0.5f + dice.Range(-0.32f, 0.32f)) * t / grid;
                    cy[k] = (gy + 0.5f + dice.Range(-0.32f, 0.32f)) * t / grid;
                    boost[k] = dice.Range(0f, 0.11f) * t;
                    lift[k] = dice.Range(-0.02f, 0.02f) * t;
                }

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    // Nearest and second-nearest stone (distances shrunk by each stone's size boost),
                    // looked up at a wobbled position so the stones' outlines are irregular.
                    float wx = px + (Noise.Value(x / (0.12f * t), y / (0.12f * t), seed + 5u) - 0.5f) * 0.09f * t;
                    float wy = py + (Noise.Value(x / (0.12f * t), y / (0.12f * t), seed + 6u) - 0.5f) * 0.09f * t;
                    int nearest = 0;
                    float d1 = float.MaxValue, d2 = float.MaxValue;
                    for (int k = 0; k < count; k++)
                    {
                        float dx = wx - cx[k], dy = wy - cy[k];
                        float d = Mathf.Sqrt(dx * dx + dy * dy) - boost[k];
                        if (d < d1) { d2 = d1; d1 = d; nearest = k; }
                        else if (d < d2) d2 = d;
                    }
                    // Distance to the stone's edge: halfway to the next stone, or the tile's edge.
                    float tileEdge = Mathf.Min(Mathf.Min(px, t - px), Mathf.Min(py, t - py));
                    float edge = Mathf.Min((d2 - d1) * 0.5f, tileEdge);
                    float chip = (Noise.Value(x / (0.03f * t), y / (0.03f * t), seed + 3u) - 0.5f) * 0.03f * t
                               + (Noise.Value(x / (0.01f * t), y / (0.01f * t), seed + 4u) - 0.5f) * 0.006f * t;
                    float inside = edge - joint + chip;
                    int i = y * size + x;

                    if (inside <= 0f)
                    {
                        // Mortar: dark, gritty, sunk deep, with moss and dirt in it.
                        float grit = Noise.Value(x / (0.004f * t), y / (0.004f * t), seed + 7u) - 0.5f;
                        var c = PixelImage.Scale(mortar, 0.85f + grit * 0.3f);
                        float moss = Noise.Fractal(x / (0.05f * t), y / (0.05f * t), seed + 11u, 2);
                        if (moss > 0.55f) c = PixelImage.Mix(c, PixelImage.Rgb(34, 46, 22), PixelImage.Smooth(0.55f, 0.68f, moss) * 0.85f);
                        img.Pixels[i] = c;
                        heights[i] = Mathf.Max(0f, 0.012f * t + inside * 0.3f) + grit * 0.004f * t;
                        continue;
                    }

                    // The stone: weathered material color, its own tint, blotches and fine grain.
                    var stone = PixelImage.Mix(Pick(colors, seed, nearest, variant), PixelImage.Rgb(84, 80, 72), 0.3f);
                    float tint = (Noise.Hash01(seed + 13u, nearest, 0) - 0.5f) * 0.22f;
                    float warm = Noise.Hash01(seed + 14u, nearest, 1);
                    if (warm > 0.75f) stone = PixelImage.Mix(stone, PixelImage.Rgb(110, 92, 70), 0.35f);   // a few browner stones
                    float blotch = (Noise.Fractal(x / (0.06f * t), y / (0.06f * t), seed + 17u + (uint)nearest, 3) - 0.5f) * 0.32f;
                    float grain = (Noise.Value(x / (0.004f * t), y / (0.004f * t), seed + 19u) - 0.5f) * 0.14f;
                    var col = PixelImage.Scale(stone, 0.88f * (1f + tint + blotch + grain));

                    // Rounded: flat-ish top, curving down to the joint.
                    float slope = Mathf.Clamp01(inside / bevel);
                    float dome = 1f - (1f - slope) * (1f - slope);
                    float surface = (top + lift[nearest]) * dome + blotch * 0.02f * t + grain * 0.006f * t;

                    // Pits, pale flecks and lichen.
                    int pitCell = Mathf.Max(1, Mathf.RoundToInt(0.006f * t));
                    if (Noise.Hash01(seed + 29u, x / pitCell, y / pitCell) < 0.004f) { col = PixelImage.Scale(col, 0.8f); surface -= 0.003f * t; }
                    else if (Noise.Hash01(seed + 31u, x, y) < 0.004f) col = PixelImage.Scale(col, 1.3f);
                    float lichen = Noise.Fractal(x / (0.025f * t), y / (0.025f * t), seed + 41u, 3);
                    if (lichen > 0.68f) col = PixelImage.Mix(col, PixelImage.Rgb(132, 130, 92), PixelImage.Smooth(0.68f, 0.78f, lichen) * 0.55f);
                    // Moss creeping up from the joints.
                    float edgeMoss = Noise.Fractal(x / (0.05f * t), y / (0.05f * t), seed + 37u, 2);
                    if (inside < bevel && edgeMoss > 0.6f)
                        col = PixelImage.Mix(col, PixelImage.Rgb(40, 52, 26), PixelImage.Smooth(0.6f, 0.72f, edgeMoss) * (1f - slope) * 0.8f);

                    img.Pixels[i] = col;
                    heights[i] = surface;
                }

            // A hairline crack across a few stones.
            for (int k = 0; k < 3; k++)
            {
                if (!dice.Chance(0.5f)) continue;
                float x = dice.Range(0.15f * t, 0.85f * t), y = dice.Range(0.15f * t, 0.85f * t);
                float a = dice.Range(0f, Mathf.PI * 2f);
                for (int s = 0; s < 7; s++)
                {
                    float step = dice.Range(0.015f, 0.035f) * t;
                    float nx = x + Mathf.Cos(a) * step, ny = y + Mathf.Sin(a) * step;
                    img.Capsule(x, y, nx, ny, 0.0025f * t + 0.4f, 0.0015f * t + 0.3f, PixelImage.Rgb(24, 22, 20, 210), PixelImage.Rgb(24, 22, 20, 170), 0.07f * t, 0.07f * t);
                    x = nx; y = ny;
                    a += dice.Range(-0.6f, 0.6f);
                }
            }
            for (int i = 0; i < img.Pixels.Length; i++) img.Pixels[i].a = 255;
            img.BakeLight(0.9f, 0.5f, Mathf.Max(1, Mathf.RoundToInt(0.014f * t)));
            return img;
        }

        /// <summary>The cut tops of upright logs: bark rims, growth rings, cracks, dark gaps between logs.</summary>
        public static PixelImage Logs(int size, Color32 gap, Color32[] colors, int variant)
        {
            float t = size;
            uint seed = 900u + (uint)variant * 53u;
            var dice = new Dice(seed);
            var img = new PixelImage(size, size) { Bumpiness = 1f };
            var heights = img.EnsureHeights();
            const int perRow = 2;
            int count = perRow * perRow;
            var lx = new float[count]; var ly = new float[count]; var lr = new float[count];
            var crackA = new float[count]; var ringShift = new float[count];
            for (int i = 0; i < count; i++)
            {
                float cell = size / (float)perRow;
                lx[i] = (i % perRow + 0.5f) * cell + dice.Range(-0.01f, 0.01f) * t;
                ly[i] = (i / perRow + 0.5f) * cell + dice.Range(-0.01f, 0.01f) * t;
                lr[i] = cell * 0.5f - dice.Range(0.004f, 0.02f) * t;
                crackA[i] = dice.Range(0f, Mathf.PI * 2f);
                ringShift[i] = dice.Range(0f, 6f);
            }
            float barkWidth = 0.035f * t;
            float ringSpacing = 0.009f * t;
            float logTop = 0.14f * t;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    int log = 0;
                    float best = float.MaxValue;
                    for (int i = 0; i < count; i++)
                    {
                        float d = Mathf.Sqrt((px - lx[i]) * (px - lx[i]) + (py - ly[i]) * (py - ly[i])) / lr[i];
                        if (d < best) { best = d; log = i; }
                    }

                    float dx = px - lx[log], dy = py - ly[log];
                    float r = best * lr[log];
                    float angle = Mathf.Atan2(dy, dx);
                    float rim = lr[log] + (Noise.Value(angle * 3f + log * 10f, 0.5f, seed, 0) - 0.5f) * 0.02f * t;
                    int idx = y * size + x;
                    Color32 c;
                    float h;
                    if (r > rim)
                    {
                        // Between logs: deep shadow, with dirt and bits of straw.
                        float dirt = Noise.Value(x / (0.006f * t), y / (0.006f * t), seed + 3u);
                        c = PixelImage.Scale(gap, 0.75f + dirt * 0.4f);
                        h = Mathf.Max(0f, 0.02f * t - (r - rim) * 0.6f);
                    }
                    else if (r > rim - barkWidth)
                    {
                        // Bark: rough strips running round the log.
                        float strip = Noise.Value(angle * 30f, r / (0.008f * t), seed + 5u, 0);
                        float fibre = Noise.Value(angle * 120f, r / (0.003f * t), seed + 6u, 0);
                        c = PixelImage.Scale(PixelImage.Rgb(62, 46, 32), 0.6f + strip * 0.55f + fibre * 0.2f);
                        float across = (r - (rim - barkWidth)) / barkWidth;      // 0 inside .. 1 outer edge
                        h = logTop + 0.01f * t * Mathf.Sin(across * Mathf.PI) - across * across * 0.04f * t + strip * 0.006f * t;
                    }
                    else
                    {
                        // Cut face: growth rings, slightly domed, a dark pith and cracks.
                        var wood = PixelImage.Scale(PixelImage.Mix(Pick(colors, seed, log, 0), PixelImage.Rgb(116, 102, 84), 0.4f), 0.86f);
                        float wobble = (Noise.Value(angle * 3f, log * 5f, seed + 7u, 0) - 0.5f) * ringSpacing * 3f
                                     + (Noise.Value(angle * 11f, log * 3f, seed + 8u, 0) - 0.5f) * ringSpacing;
                        float ring = Mathf.Sin((r + wobble) / ringSpacing + ringShift[log]);
                        float grain = Noise.Value(x / (0.003f * t), y / (0.003f * t), seed + 9u);
                        float saw = Mathf.Abs(Mathf.Sin((px * 0.7f + py * 0.7f) / (0.006f * t) + ringShift[log])) * 0.06f;   // saw marks
                        float weather = PixelImage.Smooth(0.55f, 1f, r / (rim - barkWidth));
                        float shade = (ring > 0.55f ? 0.78f : 1f) * (0.9f + grain * 0.18f - saw) * (1f - weather * 0.35f);
                        c = PixelImage.Scale(wood, shade);
                        if (r < 0.012f * t) c = PixelImage.Scale(wood, 0.5f);
                        float dome = 1f - (r / rim) * (r / rim);
                        h = logTop - 0.006f * t + dome * 0.012f * t - (ring > 0.55f ? 0.0015f * t : 0f) - saw * 0.01f * t;
                        // Two drying cracks running in from the bark.
                        for (int k = 0; k < 2; k++)
                        {
                            float ca = crackA[log] + k * 2.4f;
                            float off = Mathf.Abs(Mathf.Sin(angle - ca)) * r;
                            float depth = (r - rim * 0.35f) / (rim * 0.6f);
                            if (depth > 0f && Mathf.Cos(angle - ca) > 0f && off < (0.002f + depth * 0.004f) * t + 0.5f)
                            {
                                c = PixelImage.Scale(wood, 0.35f);
                                h -= 0.02f * t;
                            }
                        }
                    }
                    img.Pixels[idx] = c;
                    heights[idx] = h;
                }
            }
            img.BakeLight(0.8f, 0.7f, Mathf.Max(1, Mathf.RoundToInt(0.012f * t)));
            return img;
        }

        static Color32 Pick(Color32[] colors, uint seed, int a, int b)
        {
            if (colors == null || colors.Length == 0) return PixelImage.Rgb(128, 128, 128);
            return colors[Destruction.DamageShape.Hash(seed, a, b) % (uint)colors.Length];
        }
    }
}
