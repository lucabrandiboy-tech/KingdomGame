using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>What a roof is covered with. Each age has its own (new ones go at the end: assets store the number).</summary>
    public enum RoofStyle
    {
        Thatch,
        WoodShingles,
        ClayTiles,
        Slate,
        Copper,
        CorrugatedIron,
        TarFelt,
        Concrete,
    }

    /// <summary>
    /// Roofs seen from straight above, drawn for the exact shape of each roof the
    /// player builds (any shape: a square cottage, a long hall, an L). The roof
    /// rises from every edge at the same pitch, so it comes out as a hip roof: a
    /// ridge along the middle, hips running to the corners, valleys in inside
    /// corners. Rows of thatch, shingles or tiles follow the edges, the ridges and
    /// hips get a cap, and it's lit from the top-left like all the other art (its
    /// heights go through <see cref="PixelImage.BakeLight"/>). A roof reaches
    /// <see cref="Eave"/> tiles past the cells it covers, so it rests on the walls.
    /// </summary>
    public static class RoofArt
    {
        /// <summary>How far a roof reaches past the cells it covers (over the walls), in tiles.</summary>
        public const float Eave = 0.5f;

        // Which way a bit of roof slopes down to (where its rain runs off).
        const byte Left = 0, Right = 1, Down = 2, Up = 3, Ridge = 4, Flat = 5;
        const int Far = 1 << 20;

        struct Style
        {
            public Color32 Color, Cap;
            public float Slope;       // rise per tile run
            public float Course;      // tiles between rows (0 = none)
            public float TileWidth;   // tiles between joints in a row (0 = no joints)
            public float Step;        // height of each row's lower edge, tiles
            public float CapWidth;    // tiles either side of a ridge or hip
            public float Variation;   // how much single tiles differ in shade
        }

        static Style StyleOf(RoofStyle s)
        {
            switch (s)
            {
                case RoofStyle.Thatch:
                    return new Style { Color = PixelImage.Rgb(150, 122, 74), Cap = PixelImage.Rgb(112, 90, 56), Slope = 1.2f, Course = 0.2f, Step = 0.01f, CapWidth = 0.09f, Variation = 0.05f };
                case RoofStyle.WoodShingles:
                    return new Style { Color = PixelImage.Rgb(118, 96, 74), Cap = PixelImage.Rgb(92, 72, 54), Slope = 1f, Course = 0.15f, TileWidth = 0.13f, Step = 0.012f, CapWidth = 0.06f, Variation = 0.14f };
                case RoofStyle.ClayTiles:
                    return new Style { Color = PixelImage.Rgb(160, 80, 52), Cap = PixelImage.Rgb(136, 66, 44), Slope = 0.75f, Course = 0.16f, TileWidth = 0.11f, Step = 0.016f, CapWidth = 0.07f, Variation = 0.12f };
                case RoofStyle.Slate:
                    return new Style { Color = PixelImage.Rgb(74, 78, 88), Cap = PixelImage.Rgb(98, 100, 106), Slope = 1f, Course = 0.13f, TileWidth = 0.17f, Step = 0.008f, CapWidth = 0.05f, Variation = 0.09f };
                case RoofStyle.Copper:
                    return new Style { Color = PixelImage.Rgb(84, 146, 124), Cap = PixelImage.Rgb(74, 130, 110), Slope = 0.9f, Course = 0f, TileWidth = 0.32f, Step = 0f, CapWidth = 0.05f, Variation = 0.06f };
                case RoofStyle.CorrugatedIron:
                    return new Style { Color = PixelImage.Rgb(110, 112, 112), Cap = PixelImage.Rgb(96, 98, 100), Slope = 0.45f, Course = 1.1f, TileWidth = 0.85f, Step = 0.01f, CapWidth = 0.07f, Variation = 0.05f };
                case RoofStyle.TarFelt:
                    return new Style { Color = PixelImage.Rgb(52, 52, 54), Cap = PixelImage.Rgb(44, 44, 46), Slope = 0.35f, Course = 0.9f, Step = 0.006f, CapWidth = 0.06f, Variation = 0.04f };
                default:
                    return new Style { Color = PixelImage.Rgb(108, 106, 100), Cap = PixelImage.Rgb(126, 124, 118), Slope = 0f, Variation = 0.03f };
            }
        }

        /// <summary>How tall a roof of this kind stands, in tiles (how far its shadow falls).</summary>
        public static float Height(RoofStyle style) => style == RoofStyle.Concrete ? 0.35f : 0.45f + StyleOf(style).Slope * 0.3f;

        /// <summary>
        /// One whole roof. <paramref name="cells"/> is a <paramref name="w"/> x <paramref name="h"/>
        /// grid of the cells it covers (leave at least one empty cell round them, for the eaves).
        /// The picture covers the whole grid at <paramref name="ppt"/> pixels per tile; its pivot is
        /// the bottom-left corner.
        /// </summary>
        public static PixelImage Roof(bool[] cells, int w, int h, int ppt, RoofStyle style, uint seed)
        {
            int W = w * ppt, H = h * ppt;
            var inside = Outline(cells, w, h, ppt);
            var st = StyleOf(style);
            bool flat = st.Slope <= 0f;

            // How far each pixel is from the roof's edge (counting diagonal steps as one): rising
            // at the same pitch from every edge gives a hip roof.
            var d = new int[W * H];
            for (int i = 0; i < d.Length; i++) d[i] = inside[i] ? Far : 0;
            Chamfer(d, null, W, H);

            // Which way each pixel slopes down to, and where two slopes meet (ridges, hips, valleys).
            var face = new byte[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!inside[i]) continue;
                    if (flat) { face[i] = Flat; continue; }
                    int gx = At(d, W, H, x + 1, y) - At(d, W, H, x - 1, y);
                    int gy = At(d, W, H, x, y + 1) - At(d, W, H, x, y - 1);
                    int ax = Mathf.Abs(gx), ay = Mathf.Abs(gy);
                    face[i] = ax > ay ? (gx > 0 ? Left : Right) : ay > ax ? (gy > 0 ? Down : Up) : Ridge;
                }
            var capDistance = new float[W * H];
            var valley = new bool[W * H];
            if (!flat) Caps(face, d, inside, W, H, capDistance, valley);
            else for (int i = 0; i < capDistance.Length; i++) capDistance[i] = Far;

            var img = new PixelImage(W, H) { Pivot = Vector2.zero };
            Paint(img, inside, d, face, capDistance, valley, ppt, st, style, seed);
            return img;
        }

        /// <summary>A soft shadow of the roof's outline, at <paramref name="ppt"/> pixels per tile (pivot: bottom-left of the grid).</summary>
        public static PixelImage Shadow(bool[] cells, int w, int h, int ppt)
        {
            var inside = Outline(cells, w, h, ppt);
            var img = new PixelImage(w * ppt, h * ppt) { Pivot = Vector2.zero };
            for (int i = 0; i < inside.Length; i++)
                if (inside[i]) img.Pixels[i] = new Color32(0, 0, 0, 255);
            return img.Shadow(Mathf.Max(1, ppt / 6), 1f);
        }

        /// <summary>A one-tile sample of a roof (the build bar's ghost while placing it).</summary>
        public static PixelImage Swatch(int ppt, RoofStyle style)
        {
            var st = StyleOf(style);
            int n = ppt * ppt;
            var inside = new bool[n];
            var d = new int[n];
            var face = new byte[n];
            var capDistance = new float[n];
            var valley = new bool[n];
            for (int y = 0; y < ppt; y++)
                for (int x = 0; x < ppt; x++)
                {
                    int i = y * ppt + x;
                    inside[i] = true;
                    d[i] = y + ppt / 2;   // a stretch of roof sloping down towards the bottom
                    face[i] = st.Slope > 0f ? Down : Flat;
                    capDistance[i] = Far;
                }
            var img = new PixelImage(ppt, ppt);
            Paint(img, inside, d, face, capDistance, valley, ppt, st, style, 77u);
            return img;
        }

        // ------------------------------------------------------------ shape --

        // Which pixels the roof covers: its cells, and Eave tiles past them on every open side.
        static bool[] Outline(bool[] cells, int w, int h, int ppt)
        {
            int W = w * ppt, H = h * ppt;
            var inside = new bool[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int cx = x / ppt, cy = y / ppt;
                    float u = (x - cx * ppt + 0.5f) / ppt, v = (y - cy * ppt + 0.5f) / ppt;
                    bool covered = false;
                    for (int dy = -1; dy <= 1 && !covered; dy++)
                        for (int dx = -1; dx <= 1 && !covered; dx++)
                        {
                            int nx = cx + dx, ny = cy + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h || !cells[ny * w + nx]) continue;
                            float ex = dx < 0 ? u : dx > 0 ? 1f - u : 0f;
                            float ey = dy < 0 ? v : dy > 0 ? 1f - v : 0f;
                            covered = Mathf.Max(ex, ey) <= Eave;
                        }
                    inside[y * W + x] = covered;
                }
            return inside;
        }

        // Distance to the nearest 0 (8 neighbours, each one step), in two sweeps; past the picture's
        // edge counts as 0. With `source`, each pixel also learns which 0 is nearest.
        static void Chamfer(int[] d, int[] source, int W, int H)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (d[i] == 0) continue;
                    Relax(d, source, W, H, i, x - 1, y);
                    Relax(d, source, W, H, i, x - 1, y - 1);
                    Relax(d, source, W, H, i, x, y - 1);
                    Relax(d, source, W, H, i, x + 1, y - 1);
                }
            for (int y = H - 1; y >= 0; y--)
                for (int x = W - 1; x >= 0; x--)
                {
                    int i = y * W + x;
                    if (d[i] == 0) continue;
                    Relax(d, source, W, H, i, x + 1, y);
                    Relax(d, source, W, H, i, x + 1, y + 1);
                    Relax(d, source, W, H, i, x, y + 1);
                    Relax(d, source, W, H, i, x - 1, y + 1);
                }
        }

        static void Relax(int[] d, int[] source, int W, int H, int i, int x, int y)
        {
            bool outside = x < 0 || y < 0 || x >= W || y >= H;
            int j = outside ? -1 : y * W + x;
            int via = (outside ? 0 : d[j]) + 1;
            if (via >= d[i]) return;
            d[i] = via;
            if (source != null) source[i] = outside ? -1 : source[j];
        }

        static int At(int[] d, int W, int H, int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? 0 : d[y * W + x];

        // How far each pixel is from the nearest ridge, hip or valley line, and whether that line is a valley.
        static void Caps(byte[] face, int[] d, bool[] inside, int W, int H, float[] capDistance, bool[] valley)
        {
            var dist = new int[W * H];
            var source = new int[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    source[i] = i;
                    bool line = inside[i] && (face[i] == Ridge
                        || Differs(face, inside, W, H, i, x + 1, y) || Differs(face, inside, W, H, i, x, y + 1));
                    dist[i] = line ? 0 : Far;
                }
            Chamfer(dist, source, W, H);
            for (int i = 0; i < dist.Length; i++)
            {
                capDistance[i] = dist[i];
                int s = source[i];
                if (s < 0 || !inside[i]) continue;
                // A valley runs along a crease that dips (the roof is higher on both sides of it).
                int sx = s % W, sy = s / W;
                int around = At(d, W, H, sx + 2, sy) + At(d, W, H, sx - 2, sy) + At(d, W, H, sx, sy + 2) + At(d, W, H, sx, sy - 2);
                valley[i] = around - 4 * d[s] > 0;
            }
        }

        // How bright a whole slope is, lit from the top-left (1 = like flat ground).
        static float FaceShade(byte f, float slope)
        {
            if (f >= Ridge || slope <= 0f) return 1f;
            float gx = f == Left ? slope : f == Right ? -slope : 0f;   // how fast the roof rises to the right
            float gy = f == Down ? slope : f == Up ? -slope : 0f;      // ...and upwards
            var light = ArtLight.Sun;
            float lit = (-gx * light.x - gy * light.y + light.z) / Mathf.Sqrt(gx * gx + gy * gy + 1f) - light.z;
            return 1f + lit * 0.6f;
        }

        static bool Differs(byte[] face, bool[] inside, int W, int H, int i, int x, int y)
        {
            if (x >= W || y >= H) return false;
            int j = y * W + x;
            return inside[j] && face[j] != face[i];
        }

        // ------------------------------------------------------------ paint --

        static void Paint(PixelImage img, bool[] inside, int[] d, byte[] face, float[] capDistance, bool[] valley,
            int ppt, Style st, RoofStyle style, uint seed)
        {
            int W = img.Width, H = img.Height;
            var hs = img.EnsureHeights();
            float t = ppt;
            float capPx = st.CapWidth * t;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!inside[i]) continue;
                    byte f = face[i];
                    float up = d[i] / t;                              // tiles up from the eave
                    float wx = (x + 0.5f) / t, wy = (y + 0.5f) / t;   // tiles across the picture
                    // Along the eave, and a different pattern on each slope.
                    float along = f == Left || f == Right ? wy : wx;
                    uint faceSeed = seed * 31u + f * 977u;
                    // Only the small bumps go into the heights (rows, tiles, caps); the slope itself
                    // is shaded separately and more gently, or the side away from the light goes black.
                    float height = 0f;
                    float tone = FaceShade(f, st.Slope), texture = 1f;
                    var col = st.Color;

                    // Rows (courses) of thatch, shingles or tiles, each overlapping the one below.
                    int row = 0;
                    float inRow = 0f;
                    if (st.Course > 0f)
                    {
                        float r = up / st.Course;
                        row = Mathf.FloorToInt(r);
                        inRow = r - row;
                        height -= st.Step * t * inRow;   // the next row's edge sits on top: a step at each row
                    }
                    int tile = 0;
                    float inTile = 0.5f;
                    if (st.TileWidth > 0f)
                    {
                        float shift = (row % 2) * 0.5f + Noise.Hash01(faceSeed + 5u, row, 0) * 0.3f;
                        float k = along / st.TileWidth + shift;
                        tile = Mathf.FloorToInt(k);
                        inTile = k - tile;
                        tone *= 1f + (Noise.Hash01(faceSeed + 9u, tile, row) - 0.5f) * 2f * st.Variation;
                    }

                    switch (style)
                    {
                        case RoofStyle.Thatch:
                        {
                            // Straw running down the roof, combed into rows; darker where it's old.
                            float straw = Noise.Value(along * 90f, up * 5f, faceSeed + 1u) - 0.5f;
                            float clumps = Noise.Value(along * 14f, up * 3f + row * 0.7f, faceSeed + 2u) - 0.5f;
                            texture = 1f + straw * 0.34f + clumps * 0.16f;
                            height += (straw * 0.012f + clumps * 0.02f) * t;
                            float age = Noise.Fractal(wx * 0.7f, wy * 0.7f, seed + 3u, 3);
                            col = PixelImage.Mix(col, PixelImage.Rgb(104, 96, 78), Mathf.Clamp01((age - 0.45f) * 1.6f));
                            tone *= 1f + (Noise.Hash01(faceSeed + 4u, row, 1) - 0.5f) * 0.08f;
                            break;
                        }
                        case RoofStyle.WoodShingles:
                        {
                            float grain = Noise.Value(along * 160f, up * 9f, faceSeed + 1u) - 0.5f;
                            texture = 1f + grain * 0.22f;
                            if (Mathf.Min(inTile, 1f - inTile) * st.TileWidth < 0.005f) { height -= 0.006f * t; texture *= 0.55f; }
                            float moss = Noise.Fractal(wx * 1.2f, wy * 1.2f, seed + 6u, 3);
                            col = PixelImage.Mix(col, PixelImage.Rgb(84, 92, 62), Mathf.Clamp01((moss - 0.55f) * 2f) * 0.6f);
                            break;
                        }
                        case RoofStyle.ClayTiles:
                        {
                            // Rounded tiles side by side: each one a little hump, with a channel between.
                            float hump = Mathf.Sin(inTile * Mathf.PI);
                            height += hump * 0.03f * t;
                            texture = 0.8f + 0.2f * Mathf.Sqrt(hump) + (Noise.Value(wx * 60f, wy * 60f, faceSeed + 1u) - 0.5f) * 0.1f;
                            float soot = Noise.Fractal(wx * 0.9f, wy * 0.9f, seed + 7u, 3);
                            col = PixelImage.Mix(col, PixelImage.Rgb(118, 70, 52), Mathf.Clamp01((soot - 0.5f) * 1.5f) * 0.7f);
                            break;
                        }
                        case RoofStyle.Slate:
                        {
                            texture = 1f + (Noise.Value(wx * 80f, wy * 80f, faceSeed + 1u) - 0.5f) * 0.12f;
                            if (Mathf.Min(inTile, 1f - inTile) * st.TileWidth < 0.004f) { height -= 0.004f * t; texture *= 0.6f; }
                            if (Noise.Hash01(faceSeed + 8u, tile, row) > 0.93f) col = PixelImage.Rgb(84, 82, 92);   // the odd purple slate
                            break;
                        }
                        case RoofStyle.Copper:
                        {
                            // Long sheets down the roof with raised seams between; green with age, brown where it's newer.
                            float seam = Mathf.Min(inTile, 1f - inTile) * st.TileWidth;
                            if (seam < 0.012f) { height += (0.012f - seam) * 1.4f * t; texture = 1.08f; }
                            float patina = Noise.Fractal(wx * 1.5f, wy * 1.5f, seed + 9u, 4);
                            col = PixelImage.Mix(col, PixelImage.Rgb(122, 92, 58), Mathf.Clamp01((0.38f - patina) * 3f));
                            texture *= 1f + (Noise.Value(along * 30f, up * 200f, faceSeed + 1u) - 0.5f) * 0.08f;
                            break;
                        }
                        case RoofStyle.CorrugatedIron:
                        {
                            // Rippled sheets, overlapping; rust where water stands.
                            float ripple = Mathf.Sin(along * Mathf.PI * 2f / 0.07f);
                            height += ripple * 0.006f * t;
                            texture = 1f + ripple * 0.06f;
                            if (Mathf.Min(inTile, 1f - inTile) * st.TileWidth < 0.012f) texture *= 0.85f;
                            float rust = Noise.Fractal(wx * 2f, wy * 2f, seed + 11u, 4) + (1f - Mathf.Clamp01(up * 2f)) * 0.15f;
                            col = PixelImage.Mix(col, PixelImage.Rgb(112, 70, 46), Mathf.Clamp01((rust - 0.6f) * 2.5f));
                            break;
                        }
                        case RoofStyle.TarFelt:
                        {
                            float grit = Noise.Hash01(seed + 13u, x, y) - 0.5f;
                            texture = 1f + grit * 0.18f + (Noise.Value(wx * 6f, wy * 6f, seed + 14u) - 0.5f) * 0.12f;
                            if (inRow < 0.02f) texture *= 0.75f;   // the overlap of each strip
                            break;
                        }
                        default:
                        {
                            // A flat concrete roof: a raised rim round the edge, gravel and patches of tar inside.
                            float grit = Noise.Hash01(seed + 15u, x, y) - 0.5f;
                            if (up < 0.12f)
                            {
                                height += 0.05f * t;
                                col = PixelImage.Rgb(124, 122, 116);
                                texture = 1f + (Noise.Value(wx * 50f, wy * 50f, seed + 16u) - 0.5f) * 0.12f;
                            }
                            else
                            {
                                texture = 1f + grit * 0.3f;
                                float tar = Noise.Fractal(wx * 1.4f, wy * 1.4f, seed + 17u, 3);
                                col = PixelImage.Mix(col, PixelImage.Rgb(62, 62, 60), Mathf.Clamp01((tar - 0.55f) * 3f));
                                height += grit * 0.002f * t;
                            }
                            break;
                        }
                    }

                    // Ridges and hips get a cap; valleys dip.
                    float cap = capDistance[i];
                    if (cap < capPx)
                    {
                        float k = cap / capPx;
                        if (valley[i])
                        {
                            height -= (1f - k) * 0.012f * t;
                            texture *= 0.8f + 0.2f * k;
                        }
                        else
                        {
                            height += Mathf.Sqrt(Mathf.Max(0f, 1f - k * k)) * st.CapWidth * 0.6f * t;
                            col = st.Cap;
                            texture = 1f + (Noise.Value(wx * 40f, wy * 40f, seed + 18u) - 0.5f) * 0.16f;
                        }
                    }

                    // Dirtier near the eaves, and a dark line along the very edge.
                    tone *= 0.9f + 0.1f * PixelImage.Smooth(0f, 0.6f, up);
                    tone *= 1f + (Noise.Fractal(wx * 0.5f, wy * 0.5f, seed + 19u, 2) - 0.5f) * 0.12f;
                    if (d[i] <= 1) tone *= 0.55f;
                    var c = PixelImage.Scale(col, tone * texture);
                    c.a = 255;
                    img.Pixels[i] = c;
                    hs[i] = height;
                }
            img.BakeLight(0.9f, 0.5f, Mathf.Max(1, Mathf.RoundToInt(0.012f * t)));
        }
    }
}
