using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Wall tiles seen from straight above (one grid tile, 64 x 64 pixels): the
    /// tops of fitted stone blocks, and the cut ends of palisade logs. Pixels are
    /// in Unity's order (bottom row first). Every pixel is solid (the wall's
    /// collider follows the pixels), so the gaps between logs are dark wood, not holes.
    /// </summary>
    public static class WallArt
    {
        static readonly Vector2 Light = new Vector2(-0.55f, 0.6f);

        /// <summary>Stone blocks with chiseled edges, mortar, chips, cracks and a little moss.</summary>
        public static Color32[] Stone(int size, Color32 mortar, Color32[] colors, int variant)
        {
            uint seed = 500u + (uint)variant * 31u;
            var dice = new Dice(seed);
            var pixels = new Color32[size * size];
            const int rows = 2;
            const float bevel = 5.5f;

            // Row boundaries and, per row, block boundaries (in pixels; the tile edge is a joint too).
            var rowEdges = new int[rows + 1];
            for (int r = 0; r <= rows; r++) rowEdges[r] = r * size / rows;
            var blockEdges = new int[rows][];
            for (int r = 0; r < rows; r++)
            {
                var edges = new System.Collections.Generic.List<int> { 0 };
                int x = 0;
                while (true)
                {
                    int w = dice.Range(size / 3, size * 2 / 3 + 1);
                    if (x + w > size - size / 4) break;
                    x += w;
                    edges.Add(x);
                }
                edges.Add(size);
                blockEdges[r] = edges.ToArray();
            }

            for (int y = 0; y < size; y++)
            {
                int row = Mathf.Min(rows - 1, y * rows / size);
                float y0 = rowEdges[row] + 1f, y1 = rowEdges[row + 1] - 1f;   // 2-pixel joints (1 from each side)
                var edges = blockEdges[row];
                for (int x = 0; x < size; x++)
                {
                    int block = 0;
                    while (block < edges.Length - 2 && x >= edges[block + 1]) block++;
                    float x0 = edges[block] + 1f, x1 = edges[block + 1] - 1f;
                    float px = x + 0.5f, py = y + 0.5f;

                    // Distance in from each side of the block, with chipped, uneven edges.
                    float left = px - x0, right = x1 - px, bottom = py - y0, top = y1 - py;
                    float edge = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
                    // Rounded corners.
                    float cornerX = Mathf.Max(0f, bevel - Mathf.Min(left, right)), cornerY = Mathf.Max(0f, bevel - Mathf.Min(bottom, top));
                    if (cornerX > 0f && cornerY > 0f) edge = Mathf.Min(edge, bevel - Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY));
                    // Chipped, uneven edges.
                    float rough = edge < bevel + 2f ? (Noise.Value(x / 2.3f, y / 2.3f, seed + 3u) - 0.5f) * 2.4f : 0f;
                    float inside = edge + rough;

                    Color32 c;
                    if (inside <= 0f)
                    {
                        // Mortar: gritty and darker in the deepest middle of the joint.
                        float grit = (Noise.Hash01(seed + 7u, x, y) - 0.5f) * 0.25f;
                        c = PixelImage.Scale(mortar, 0.8f + grit + Mathf.Clamp(inside, -2f, 0f) * 0.08f);
                        if (Noise.Fractal(x / 6f, y / 6f, seed + 11u, 2) > 0.66f) c = PixelImage.Mix(c, PixelImage.Rgb(44, 58, 30), 0.65f); // moss
                    }
                    else
                    {
                        // Weathered: a little darker and browner than the material's own colors.
                        var stone = PixelImage.Scale(PixelImage.Mix(Pick(colors, seed, block, row), PixelImage.Rgb(90, 84, 74), 0.3f), 0.86f);
                        float tint = (Noise.Hash01(seed + 13u, block, row) - 0.5f) * 0.16f;
                        float blotch = (Noise.Fractal(x / 9f, y / 9f, seed + 17u, 3) - 0.5f) * 0.3f;
                        float speck = (Noise.Value(x / 1.4f, y / 1.4f, seed + 19u) - 0.5f) * 0.1f;
                        float shade = 1f + tint + blotch + speck;

                        // The chiseled edge slopes down to the joint: lit on the top-left sides, dark on the bottom-right.
                        if (inside < bevel)
                        {
                            float slope = 1f - inside / bevel;
                            float nx = left < right ? -1f : 1f, ny = bottom < top ? -1f : 1f;
                            float wx = Mathf.Clamp01(1f - Mathf.Min(left, right) / bevel), wy = Mathf.Clamp01(1f - Mathf.Min(bottom, top) / bevel);
                            float facing = (nx * wx * Light.x + ny * wy * Light.y) / Mathf.Max(0.001f, Mathf.Sqrt(wx * wx + wy * wy));
                            shade *= 1f + facing * slope * 0.7f - slope * 0.12f;
                        }
                        c = PixelImage.Scale(stone, shade);
                        if (Noise.Hash01(seed + 23u, x / 3, y / 3) < 0.025f) c = PixelImage.Scale(stone, 0.55f);       // pits
                        else if (Noise.Hash01(seed + 29u, x, y) < 0.01f) c = PixelImage.Scale(stone, 1.3f);           // glints
                    }
                    pixels[y * size + x] = c;
                }
            }

            // A hairline crack across some blocks.
            for (int k = 0; k < 2; k++)
            {
                if (!dice.Chance(0.6f)) continue;
                float x = dice.Range(6f, size - 6f), y = dice.Range(6f, size - 6f);
                float a = dice.Range(0f, Mathf.PI * 2f);
                for (int s = 0; s < 14; s++)
                {
                    int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
                    if (ix < 0 || iy < 0 || ix >= size || iy >= size) break;
                    pixels[iy * size + ix] = PixelImage.Scale(pixels[iy * size + ix], 0.55f);
                    a += dice.Range(-0.5f, 0.5f);
                    x += Mathf.Cos(a);
                    y += Mathf.Sin(a);
                }
            }
            return pixels;
        }

        /// <summary>The cut tops of upright logs: bark rims, growth rings, cracks, dark gaps between logs.</summary>
        public static Color32[] Logs(int size, Color32 gap, Color32[] colors, int variant)
        {
            uint seed = 900u + (uint)variant * 53u;
            var dice = new Dice(seed);
            var pixels = new Color32[size * size];
            const int perRow = 2;
            int count = perRow * perRow;
            var lx = new float[count]; var ly = new float[count]; var lr = new float[count];
            var crackA = new float[count]; var ringShift = new float[count];
            for (int i = 0; i < count; i++)
            {
                float cell = size / (float)perRow;
                lx[i] = (i % perRow + 0.5f) * cell + dice.Range(-1.5f, 1.5f);
                ly[i] = (i / perRow + 0.5f) * cell + dice.Range(-1.5f, 1.5f);
                lr[i] = cell * 0.5f - dice.Range(0.3f, 1.6f);
                crackA[i] = dice.Range(0f, Mathf.PI * 2f);
                ringShift[i] = dice.Range(0f, 6f);
            }

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    int log = -1;
                    float best = float.MaxValue;
                    for (int i = 0; i < count; i++)
                    {
                        float d = Mathf.Sqrt((px - lx[i]) * (px - lx[i]) + (py - ly[i]) * (py - ly[i])) / lr[i];
                        if (d < best) { best = d; log = i; }
                    }

                    Color32 c;
                    float dx = px - lx[log], dy = py - ly[log];
                    float r = best * lr[log];
                    float edgeWobble = (Noise.Value(Mathf.Atan2(dy, dx) * 2.5f + log * 10f, 0.5f, seed, 0) - 0.5f) * 1.6f;
                    float rim = lr[log] + edgeWobble;
                    if (r > rim)
                    {
                        // Between logs: deep shadow, a bit of dirt.
                        c = PixelImage.Scale(gap, 0.85f + (Noise.Hash01(seed + 3u, x, y) - 0.5f) * 0.3f);
                    }
                    else if (r > rim - 3.2f)
                    {
                        // Bark rim, lit on the top-left side.
                        float facing = (dx * Light.x + dy * Light.y) / Mathf.Max(0.001f, r);
                        float striation = Noise.Value(Mathf.Atan2(dy, dx) * 9f, r * 0.8f, seed + 5u, 0);
                        c = PixelImage.Scale(PixelImage.Rgb(70, 50, 34), 0.75f + facing * 0.35f + (striation - 0.5f) * 0.45f);
                    }
                    else
                    {
                        // Cut face: growth rings, slightly domed, a dark pith and a crack or two.
                        // Weathered end grain: greyer and darker than fresh wood.
                        var wood = PixelImage.Scale(PixelImage.Mix(Pick(colors, seed, log, 0), PixelImage.Rgb(112, 100, 86), 0.4f), 0.88f);
                        float wobble = (Noise.Value(Mathf.Atan2(dy, dx) * 3f, log * 5f, seed + 7u, 0) - 0.5f) * 1.5f;
                        float ring = Mathf.Sin((r + wobble) * 1.45f + ringShift[log]);
                        float shade = (ring > 0.6f ? 0.8f : 1f) + (Noise.Hash01(seed + 9u, x, y) - 0.5f) * 0.08f;
                        shade *= 1f + (dx * Light.x + dy * Light.y) / lr[log] * 0.18f;
                        shade *= 1f - Mathf.Clamp01((r - (rim - 6f)) / 3f) * 0.15f;     // darker just inside the bark
                        c = PixelImage.Scale(wood, shade);
                        if (r < 1.6f) c = PixelImage.Scale(wood, 0.55f);
                        // One drying crack running in from the bark, narrowing as it goes.
                        float off = Mathf.Abs(Mathf.Sin(Mathf.Atan2(dy, dx) - crackA[log])) * r;
                        float depth = (r - rim * 0.45f) / (rim * 0.5f);
                        if (depth > 0f && off < 0.35f + depth * 0.5f && Mathf.Cos(Mathf.Atan2(dy, dx) - crackA[log]) > 0f)
                            c = PixelImage.Scale(wood, 0.5f);
                    }
                    pixels[y * size + x] = c;
                }
            }
            return pixels;
        }

        static Color32 Pick(Color32[] colors, uint seed, int a, int b)
        {
            if (colors == null || colors.Length == 0) return PixelImage.Rgb(128, 128, 128);
            return colors[Destruction.DamageShape.Hash(seed, a, b) % (uint)colors.Length];
        }
    }
}
