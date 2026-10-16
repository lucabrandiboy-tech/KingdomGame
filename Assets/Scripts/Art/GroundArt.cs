using System.Collections.Generic;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// The ground: three pictures that each repeat seamlessly (the right edge
    /// continues into the left, the top into the bottom): dark grass, dry
    /// khaki grass and mud. The map shows them in big patches. They have heights,
    /// so torchlight shows the blades and pebbles.
    ///
    /// Sizes are in pixels per tile (<c>ppu</c>); everything scales with it, so the same
    /// picture can be made at 1024 per tile (the baked art) or smaller (quick previews).
    /// Strokes are sized for 512 per tile and scaled by <c>k</c>.
    /// </summary>
    public static class GroundArt
    {
        /// <summary>How many tiles each ground picture covers before it repeats.</summary>
        public const int Tiles = 4;

        public static PixelImage Grass(int ppu, uint seed)
        {
            int n = Tiles * ppu;
            float k = ppu / 512f;
            var img = new PixelImage(n, n) { Pivot = Vector2.zero, Bumpiness = 0.6f };
            var dice = new Dice(seed);

            // Soil under the grass.
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float m = Field(x, y, n, Tiles * 6, seed + 1u, 3);
                    float f = Field(x, y, n, Tiles * 40, seed + 2u, 2);
                    img.Pixels[y * n + x] = PixelImage.Mix(PixelImage.Rgb(42, 39, 30), PixelImage.Rgb(34, 42, 28), m + (f - 0.5f) * 0.5f);
                }

            // A dense carpet of short blades, then longer tufts on top.
            int carpet = Mathf.RoundToInt(Tiles * Tiles * 11000f);
            for (int i = 0; i < carpet; i++)
            {
                float x = dice.Range(0f, n), y = dice.Range(0f, n);
                float lean = Field((int)x, (int)y, n, Tiles * 3, seed + 3u, 2) * Mathf.PI * 4f;
                float a = lean + dice.Range(-1.1f, 1.1f);
                float length = dice.Range(9f, 22f) * k;
                float tone = dice.Next() * 0.8f + Field((int)x, (int)y, n, Tiles * 8, seed + 4u, 2) * 0.4f;
                Blade(img, x, y, a, length, 1.7f * k, 0.5f * k, tone, 0.12f, false);
            }
            int tufts = Tiles * Tiles * 70;
            for (int i = 0; i < tufts; i++)
            {
                float cx = dice.Range(0f, n), cy = dice.Range(0f, n);
                int blades = dice.Range(6, 13);
                float spin = dice.Range(0f, Mathf.PI * 2f);
                float tone = dice.Next();
                for (int b = 0; b < blades; b++)
                {
                    float a = spin + b / (float)blades * Mathf.PI * 2f + dice.Range(-0.3f, 0.3f);
                    float length = dice.Range(22f, 46f) * k;
                    Blade(img, cx + Mathf.Cos(a) * 2f * k, cy + Mathf.Sin(a) * 2f * k, a, length, 2.2f * k, 0.6f * k,
                        Mathf.Clamp01(tone + dice.Range(-0.2f, 0.25f)), 0.22f, dice.Chance(0.12f));
                }
            }
            Litter(img, seed + 9u, dice, n, k, Tiles * Tiles * 14, 0.4f);
            img.BakeLight(0.55f, 0.55f, Mathf.Max(1, Mathf.RoundToInt(6f * k)), wrap: true);
            return img;
        }

        public static PixelImage DryGrass(int ppu, uint seed)
        {
            int n = Tiles * ppu;
            float k = ppu / 512f;
            var img = new PixelImage(n, n) { Pivot = Vector2.zero, Bumpiness = 0.6f };
            var dice = new Dice(seed);

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float m = Field(x, y, n, Tiles * 5, seed + 1u, 3);
                    float f = Field(x, y, n, Tiles * 48, seed + 2u, 1);
                    img.Pixels[y * n + x] = PixelImage.Mix(PixelImage.Rgb(66, 56, 38), PixelImage.Rgb(92, 82, 54), m * 0.8f + (f - 0.5f) * 0.4f);
                }

            // Long, pale blades laid flat mostly one way (wind and trampling), some still green.
            int blades = Mathf.RoundToInt(Tiles * Tiles * 5200f);
            for (int i = 0; i < blades; i++)
            {
                float x = dice.Range(0f, n), y = dice.Range(0f, n);
                float lean = Field((int)x, (int)y, n, Tiles * 2, seed + 3u, 2) * Mathf.PI * 2.5f + 0.6f;
                float a = lean + dice.Range(-0.55f, 0.55f);
                float length = dice.Range(22f, 62f) * k;
                float tone = dice.Next();
                bool green = dice.Chance(0.14f);
                var root = green ? PixelImage.Rgb(38, 48, 24) : PixelImage.Rgb(78, 66, 40);
                var tip = green ? PixelImage.Rgb(80, 90, 46) : PixelImage.Mix(PixelImage.Rgb(132, 120, 78), PixelImage.Rgb(168, 154, 104), tone);
                Stroke(img, x, y, a, length, 2f * k, 0.6f * k, root, tip, 0.2f, true);
            }
            Litter(img, seed + 9u, dice, n, k, Tiles * Tiles * 8, 0.2f);
            img.BakeLight(0.45f, 0.6f, Mathf.Max(1, Mathf.RoundToInt(7f * k)), wrap: true);
            return img;
        }

        public static PixelImage Mud(int ppu, uint seed)
        {
            int n = Tiles * ppu;
            float k = ppu / 512f;
            var img = new PixelImage(n, n) { Pivot = Vector2.zero, Bumpiness = 0.8f };
            var heights = img.EnsureHeights();
            var dice = new Dice(seed);

            // Packed earth: lumpy, with darker damp hollows.
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Warp the big shapes a little so the damp hollows don't follow the noise grid.
                    int wx = x + Mathf.RoundToInt((Field(x, y, n, Tiles * 4, seed + 11u, 2) - 0.5f) * 0.3f * ppu);
                    int wy = y + Mathf.RoundToInt((Field(x, y, n, Tiles * 4, seed + 12u, 2) - 0.5f) * 0.3f * ppu);
                    float big = Field(PixelImage.Wrap(wx, n), PixelImage.Wrap(wy, n), n, Tiles * 3, seed + 1u, 4);
                    float lumps = Field(x, y, n, Tiles * 22, seed + 2u, 3);
                    float grain = Field(x, y, n, Tiles * 110, seed + 3u, 1);
                    float h = lumps * 10f * k + grain * 2f * k + big * 6f * k;
                    heights[y * n + x] = h;
                    float damp = PixelImage.Smooth(0.46f, 0.26f, big) * 0.6f;
                    var c = PixelImage.Mix(PixelImage.Rgb(52, 45, 36), PixelImage.Rgb(84, 74, 60), lumps * 0.8f + grain * 0.35f - 0.1f);
                    img.Pixels[y * n + x] = PixelImage.Mix(c, PixelImage.Rgb(34, 31, 27), damp * 0.55f);
                }

            // Cracks where it dried out.
            int cracks = Tiles * Tiles * 10;
            for (int i = 0; i < cracks; i++)
            {
                float x = dice.Range(0f, n), y = dice.Range(0f, n), a = dice.Range(0f, Mathf.PI * 2f);
                int steps = dice.Range(4, 10);
                for (int s = 0; s < steps; s++)
                {
                    float length = dice.Range(8f, 20f) * k;
                    float nx = x + Mathf.Cos(a) * length, ny = y + Mathf.Sin(a) * length;
                    img.Line(x, y, nx, ny, 1.4f * k + 0.6f, PixelImage.Rgb(20, 15, 11, 200), wrap: true);
                    x = nx; y = ny;
                    a += dice.Range(-0.7f, 0.7f);
                }
            }
            // Pebbles and small stones, lit from the top-left with a shadow.
            int stones = Tiles * Tiles * 90;
            for (int i = 0; i < stones; i++)
            {
                float x = dice.Range(0f, n), y = dice.Range(0f, n);
                float r = dice.Range(2.5f, dice.Chance(0.08f) ? 14f : 7f) * k;
                Pebble(img, x, y, r, r * dice.Range(0.6f, 1f), PixelImage.Scale(PixelImage.Rgb(92, 86, 76), dice.Range(0.65f, 1.15f)), true);
            }
            Litter(img, seed + 9u, dice, n, k, Tiles * Tiles * 6, 0.1f);
            img.BakeLight(0.7f, 0.5f, Mathf.Max(1, Mathf.RoundToInt(5f * k)), wrap: true);
            return img;
        }

        /// <summary>
        /// An ore field's ground (Economy/OreMap.cs), repeating like the others: dark gravelly earth with angular chunks of
        /// the ore in clumps, lit from the top-left. Iron: blue-grey rocks with rust on them; copper: orange-brown rocks with
        /// green patina; coal: glossy black lumps; stone: pale cobbles.
        /// </summary>
        public static PixelImage Ore(int ppu, OreKind kind, uint seed)
        {
            int n = Tiles * ppu;
            float k = ppu / 512f;
            var img = new PixelImage(n, n) { Pivot = Vector2.zero, Bumpiness = 0.8f };
            var heights = img.EnsureHeights();
            var dice = new Dice(seed);
            Color32 soil, rock, fleck;
            float fleckShare;
            switch (kind)
            {
                case OreKind.Copper: soil = PixelImage.Rgb(56, 44, 34); rock = PixelImage.Rgb(170, 96, 54); fleck = PixelImage.Rgb(66, 148, 126); fleckShare = 0.45f; break;
                case OreKind.Coal: soil = PixelImage.Rgb(54, 44, 36); rock = PixelImage.Rgb(50, 50, 58); fleck = PixelImage.Rgb(120, 120, 134); fleckShare = 0.35f; break;
                case OreKind.Stone: soil = PixelImage.Rgb(66, 60, 52); rock = PixelImage.Rgb(150, 142, 128); fleck = PixelImage.Rgb(112, 104, 94); fleckShare = 0.3f; break;
                default: soil = PixelImage.Rgb(50, 46, 46); rock = PixelImage.Rgb(108, 124, 152); fleck = PixelImage.Rgb(150, 82, 54); fleckShare = 0.5f; break;
            }

            // Lumpy, gravelly earth.
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float lumps = Field(x, y, n, Tiles * 18, seed + 1u, 3);
                    float grain = Field(x, y, n, Tiles * 90, seed + 2u, 1);
                    heights[y * n + x] = lumps * 8f * k + grain * 2f * k;
                    img.Pixels[y * n + x] = PixelImage.Mix(PixelImage.Scale(soil, 0.7f), soil, lumps * 0.8f + grain * 0.3f);
                }
            int gravel = Tiles * Tiles * 160;
            for (int i = 0; i < gravel; i++)
            {
                float r = dice.Range(3f, 8f) * k;
                Pebble(img, dice.Range(0f, n), dice.Range(0f, n), r, r * dice.Range(0.6f, 1f), PixelImage.Scale(PixelImage.Mix(soil, PixelImage.Rgb(96, 90, 82), 0.5f), dice.Range(0.7f, 1.2f)), true);
            }

            // The ore: broken, angular chunks of every size (more small than big), gathered in clumps with earth between
            // them, some with flecks of colour on them (rust, patina, a glint on coal).
            int lumpCount = Tiles * Tiles * 520;
            for (int i = 0; i < lumpCount; i++)
            {
                float x = dice.Range(0f, n), y = dice.Range(0f, n);
                float clump = Field(PixelImage.Wrap((int)x, n), PixelImage.Wrap((int)y, n), n, Tiles * 2, seed + 3u, 2);
                if (dice.Next() > 0.3f + PixelImage.Smooth(0.36f, 0.62f, clump) * 0.7f) continue;
                float r = dice.Range(14f, dice.Chance(0.12f) ? 64f : 32f) * k;
                var c = PixelImage.Scale(PixelImage.Mix(rock, soil, dice.Range(0f, 0.25f)), dice.Range(0.75f, 1.2f));
                Chunk(img, dice, x, y, r, c);
                if (!dice.Chance(fleckShare)) continue;
                int flecks = dice.Range(1, 4);
                for (int f = 0; f < flecks; f++)
                {
                    float fr = r * dice.Range(0.12f, 0.26f);
                    Pebble(img, x + dice.Range(-0.45f, 0.45f) * r, y + dice.Range(-0.45f, 0.45f) * r, fr, fr * dice.Range(0.5f, 0.9f),
                        PixelImage.Scale(fleck, dice.Range(0.8f, 1.15f)), true);
                }
            }
            img.BakeLight(0.7f, 0.55f, Mathf.Max(1, Mathf.RoundToInt(5f * k)), wrap: true);
            return img;
        }

        /// <summary>
        /// The masks that show the ore fields over one square of an endless world (like <see cref="PatchMasksAt"/>), one for each
        /// kind of ore (<c>masks[(int)kind - 1]</c>), made only for the kinds there (the others are left null). A field shows
        /// inside its wobbly edge (the same edge that says which tiles have ore), thinning out into clumps near it; a tile
        /// shows sparser, in clumps, as it's mined, and a mined-out one is bare ground. <paramref name="taken"/>: what's been taken from the tiles round the square,
        /// (tiles + 2) across from (originX - 1, originY - 1) (OreMap.TakenIn), or null for nothing. False: no field touches it.
        /// Plain maths: safe on another thread.
        /// </summary>
        public static bool OreMasksAt(int originX, int originY, int tiles, int detail, uint seed, int[] taken, byte[][] masks)
        {
            for (int i = 0; i < masks.Length; i++) masks[i] = null;
            var fields = new List<OreMap.Field>();
            OreMap.FieldsNear(seed, originX - 1, originY - 1, originX + tiles, originY + tiles, fields);
            if (fields.Count == 0) return false;

            // How full each tile looks (0..1), with one more all round so the square's edge blends into its neighbours'.
            int m = tiles + 2;
            var full = new float[m * m];
            for (int y = 0; y < m; y++)
                for (int x = 0; x < m; x++)
                {
                    if (OreMap.Generate(seed, originX - 1 + x, originY - 1 + y, out int amount) == OreKind.None) continue;
                    full[y * m + x] = OreMap.Level(amount, taken != null ? taken[y * m + x] : 0) / 4f;
                }

            int n = tiles * detail;
            foreach (var f in fields)
            {
                int kind = (int)f.Kind - 1;
                if (kind < 0 || kind >= masks.Length) continue;
                var mask = masks[kind] ??= new byte[n * n];
                int px0 = Mathf.Max(0, (f.MinX - originX) * detail), px1 = Mathf.Min(n, (f.MaxX + 1 - originX) * detail);
                int py0 = Mathf.Max(0, (f.MinY - originY) * detail), py1 = Mathf.Min(n, (f.MaxY + 1 - originY) * detail);
                for (int y = py0; y < py1; y++)
                {
                    float wy = originY + (y + 0.5f) / detail;
                    for (int x = px0; x < px1; x++)
                    {
                        float wx = originX + (x + 0.5f) / detail;
                        // Roughly how many tiles in from the field's edge.
                        float inside = OreMap.Inside(f, wx, wy) * f.Radius;
                        if (inside < -0.3f) continue;
                        // How full it is here: the tiles' levels blended between their middles.
                        float gx = wx - 0.5f - (originX - 1), gy = wy - 0.5f - (originY - 1);
                        int ix = Mathf.Clamp(Mathf.FloorToInt(gx), 0, m - 2), iy = Mathf.Clamp(Mathf.FloorToInt(gy), 0, m - 2);
                        float fx = Mathf.Clamp01(gx - ix), fy = Mathf.Clamp01(gy - iy);
                        int i = iy * m + ix;
                        float level = Mathf.Lerp(Mathf.Lerp(full[i], full[i + 1], fx), Mathf.Lerp(full[i + m], full[i + m + 1], fx), fy);
                        if (level <= 0f) continue;
                        // Clumps a third of a tile across: an emptier tile shows only the low ones, and so does the
                        // last tile and a half in from the edge, so a field thins out into scattered clumps.
                        level *= Mathf.Clamp01(0.3f + inside * 0.5f);
                        float clump = OreMap.Smooth(wx * 3f, wy * 3f, seed + 911u) * 0.7f + OreMap.Smooth(wx * 7f, wy * 7f, seed + 912u) * 0.3f;
                        float v = Mathf.Min(inside * 2.5f + 0.5f, (level - clump * 0.9f) * 6f + 0.5f);
                        byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
                        int p = y * n + x;
                        if (a > mask[p]) mask[p] = a;
                    }
                }
            }
            return true;
        }

        // A grass blade of the dark-grass palette; tone 0..1 = dark to light.
        static void Blade(PixelImage img, float x, float y, float angle, float length, float rootWidth, float tipWidth, float tone, float height, bool yellow)
        {
            var root = PixelImage.Mix(PixelImage.Rgb(26, 32, 20), PixelImage.Rgb(44, 54, 32), tone);
            var tip = PixelImage.Mix(PixelImage.Rgb(56, 68, 40), PixelImage.Rgb(98, 110, 68), tone);
            if (yellow) tip = PixelImage.Mix(tip, PixelImage.Rgb(110, 104, 62), 0.6f);
            Stroke(img, x, y, angle, length, rootWidth, tipWidth, root, tip, height, true);
        }

        static void Stroke(PixelImage img, float x, float y, float angle, float length, float rootWidth, float tipWidth,
            Color32 root, Color32 tip, float height, bool wrap)
        {
            // Slightly curved: two segments.
            float bend = (Noise.Hash01((uint)(x * 7f), (int)y, (int)(angle * 100f)) - 0.5f) * 0.5f;
            float mx = x + Mathf.Cos(angle) * length * 0.55f, my = y + Mathf.Sin(angle) * length * 0.55f;
            float a2 = angle + bend;
            float ex = mx + Mathf.Cos(a2) * length * 0.45f, ey = my + Mathf.Sin(a2) * length * 0.45f;
            var mid = PixelImage.Mix(root, tip, 0.55f);
            float midWidth = (rootWidth + tipWidth) * 0.5f;
            float h = height * length;
            img.Capsule(x, y, mx, my, rootWidth, midWidth, root, mid, 0f, h * 0.6f, wrap);
            img.Capsule(mx, my, ex, ey, midWidth, tipWidth, mid, tip, h * 0.6f, h, wrap);
        }

        // A broken chunk of rock seen from above (wrapping round the picture's edges): an uneven polygon of 5 to 8 sides,
        // each sloping down from a flat top to its edge, every face lit by the sun on its own, with a shadow under it.
        static void Chunk(PixelImage img, Dice dice, float x, float y, float r, Color32 stone)
        {
            var heights = img.EnsureHeights();
            var light = ArtLight.Sun;
            int sides = dice.Range(5, 9);
            var cx = new float[sides];
            var cy = new float[sides];
            float spin = dice.Range(0f, Mathf.PI * 2f), squash = dice.Range(0.65f, 1f);
            for (int i = 0; i < sides; i++)
            {
                float a = spin + (i + dice.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / sides;
                float d = r * dice.Range(0.7f, 1f);
                cx[i] = Mathf.Cos(a) * d;
                cy[i] = Mathf.Sin(a) * d * squash;
            }
            float top = r * dice.Range(0.3f, 0.5f);                    // how far in from the edge the flat top starts
            float tall = r * dice.Range(0.45f, 0.7f);
            var tilt = new Vector2(dice.Range(-0.15f, 0.15f), dice.Range(-0.15f, 0.15f));   // the top isn't quite flat
            img.EllipseWrapped(x + r * 0.22f, y - r * 0.28f, r * 1.05f, r * squash * 1.05f, PixelImage.Rgb(10, 8, 6, 120));
            float reach = r + 2f;
            for (int py = Mathf.FloorToInt(y - reach); py <= y + reach; py++)
                for (int px = Mathf.FloorToInt(x - reach); px <= x + reach; px++)
                {
                    float lx = px + 0.5f - x, ly = py + 0.5f - y;
                    // The nearest edge (the polygon is convex round its middle): how far inside it, and which way it faces.
                    float inside = float.MaxValue;
                    Vector2 outward = Vector2.zero;
                    for (int i = 0; i < sides; i++)
                    {
                        int j = (i + 1) % sides;
                        float ex = cx[j] - cx[i], ey = cy[j] - cy[i];
                        float len = Mathf.Sqrt(ex * ex + ey * ey);
                        if (len < 1e-4f) continue;
                        var nrm = new Vector2(ey / len, -ex / len);   // (the vertices go anticlockwise: this points out)
                        float dist = -((lx - cx[i]) * nrm.x + (ly - cy[i]) * nrm.y);
                        if (dist < inside) { inside = dist; outward = nrm; }
                    }
                    float coverage = Mathf.Clamp01(inside + 0.5f);
                    if (coverage <= 0f) continue;
                    // A sloping face near the edge, the flat (slightly tilted) top further in.
                    Vector3 normal = inside < top
                        ? new Vector3(outward.x * 0.85f, outward.y * 0.85f, 1f).normalized
                        : new Vector3(tilt.x, tilt.y, 1f).normalized;
                    float lit = Mathf.Max(0f, normal.x * light.x + normal.y * light.y + normal.z * light.z);
                    var c = PixelImage.Scale(stone, 0.45f + lit * 0.75f);
                    int tx = PixelImage.Wrap(px, img.Width), ty = PixelImage.Wrap(py, img.Height);
                    img.Blend(tx, ty, c, coverage);
                    int k = ty * img.Width + tx;
                    float h = tall * Mathf.Clamp01(inside / Mathf.Max(1f, top));
                    heights[k] = Mathf.Max(heights[k], h * coverage);
                }
        }

        static void Pebble(PixelImage img, float x, float y, float rx, float ry, Color32 stone, bool wrap)
        {
            var heights = img.EnsureHeights();
            var light = ArtLight.Sun;
            float reach = Mathf.Max(rx, ry) + 2f;
            // Contact shadow first.
            img.EllipseWrapped(x + rx * 0.25f, y - ry * 0.3f, rx * 1.15f, ry * 1.15f, PixelImage.Rgb(10, 8, 6, 120));
            for (int py = Mathf.FloorToInt(y - reach); py <= y + reach; py++)
                for (int px = Mathf.FloorToInt(x - reach); px <= x + reach; px++)
                {
                    float dx = (px + 0.5f - x) / rx, dy = (py + 0.5f - y) / ry;
                    float d2 = dx * dx + dy * dy;
                    float edge = (1f - Mathf.Sqrt(d2)) * Mathf.Min(rx, ry) + 0.5f;
                    if (edge <= 0f) continue;
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d2));
                    float lit = dx * light.x + dy * light.y + nz * light.z;
                    var c = PixelImage.Scale(stone, 0.55f + lit * 0.6f);
                    int tx = wrap ? PixelImage.Wrap(px, img.Width) : px, ty = wrap ? PixelImage.Wrap(py, img.Height) : py;
                    if (!img.Inside(tx, ty)) continue;
                    img.Blend(tx, ty, c, edge);
                    int i = ty * img.Width + tx;
                    heights[i] = Mathf.Max(heights[i], nz * Mathf.Min(rx, ry) * 0.8f);
                }
        }

        // Dead leaves and twigs.
        static void Litter(PixelImage img, uint seed, Dice dice, int n, float k, int count, float twigShare)
        {
            for (int i = 0; i < count; i++)
            {
                float x = dice.Range(0f, n), y = dice.Range(0f, n);
                if (dice.Chance(twigShare))
                {
                    float a = dice.Range(0f, Mathf.PI * 2f), length = dice.Range(20f, 60f) * k;
                    img.Capsule(x + 2f * k, y - 2f * k, x + 2f * k + Mathf.Cos(a) * length, y - 2f * k + Mathf.Sin(a) * length, 2.6f * k, 1.6f * k,
                        PixelImage.Rgb(8, 6, 4, 110), PixelImage.Rgb(8, 6, 4, 110), 0f, 0f, true);
                    img.Capsule(x, y, x + Mathf.Cos(a) * length, y + Mathf.Sin(a) * length, 2.2f * k, 1.2f * k,
                        PixelImage.Rgb(70, 52, 36), PixelImage.Rgb(88, 66, 44), 2f * k, 1f * k, true);
                }
                else
                {
                    var leaf = dice.Chance(0.5f) ? PixelImage.Rgb(84, 58, 30) : PixelImage.Rgb(100, 76, 36);
                    leaf = PixelImage.Scale(leaf, dice.Range(0.6f, 1f));
                    float a = dice.Range(0f, Mathf.PI * 2f), length = dice.Range(7f, 13f) * k;
                    img.Capsule(x, y, x + Mathf.Cos(a) * length, y + Mathf.Sin(a) * length, 3.2f * k, 1f * k, leaf, PixelImage.Scale(leaf, 0.8f), 1.5f * k, 1f * k, true);
                }
            }
        }

        /// <summary>
        /// The big patches across the map, at a map position in tiles: how much dry
        /// grass and how much mud is there (the patch shows where the value is above
        /// 0.5). Smooth values that change over a tile or two; <see cref="PatchMasks"/>
        /// makes their edges ragged.
        /// </summary>
        public static void Patches(float x, float y, uint seed, out float dry, out float mud)
        {
            float d = Noise.Fractal(x / 11f, y / 11f, seed + 71u, 4) + (Noise.Value(x / 1.7f, y / 1.7f, seed + 72u) - 0.5f) * 0.1f;
            float m = Noise.Fractal(x / 7f + 50f, y / 7f, seed + 73u, 4) + (Noise.Value(x / 1.3f, y / 1.3f, seed + 74u) - 0.5f) * 0.1f;
            // The ground around the start (throne and storehouse) is trampled to mud.
            float dx = x + 2.5f, dy = y - 0.5f;
            float trampled = PixelImage.Smooth(6f, 1.5f, Mathf.Sqrt(dx * dx * 0.6f + dy * dy)) * (0.75f + (Noise.Value(x / 1.5f, y / 1.5f, seed + 75u) - 0.5f) * 0.6f);
            dry = Mathf.Clamp01((d - 0.56f) * 5f + 0.5f) * (1f - trampled);
            mud = Mathf.Max(Mathf.Clamp01((m - 0.63f) * 5f + 0.5f), trampled);
        }

        /// <summary>
        /// The masks that show the dry grass and mud over a whole map
        /// (<paramref name="mapSize"/> tiles across, <paramref name="detail"/> mask
        /// pixels per tile, bottom row first): each byte is the mask's alpha, and a
        /// patch shows where it's above half. Fine noise makes the edges ragged, with
        /// tufts of grass reaching into the dirt and specks of dirt in the grass, like
        /// real trampled ground instead of clean cut-out curves.
        /// </summary>
        public static void PatchMasks(int mapSize, int detail, uint seed, byte[] dry, byte[] mud)
        {
            // The smooth patch values, 4 per tile (2 on big maps: they're 7 to 11 tiles across), blended in between.
            int coarse = mapSize > 512 ? 2 : 4;
            int cn = mapSize * coarse + 2;
            var cd = new float[cn * cn];
            var cm = new float[cn * cn];
            float half = mapSize / 2f;
            for (int y = 0; y < cn; y++)
                for (int x = 0; x < cn; x++)
                {
                    Patches(x / (float)coarse - half, y / (float)coarse - half, seed, out float d, out float m);
                    cd[y * cn + x] = d;
                    cm[y * cn + x] = m;
                }

            // Fine noise in two repeating tables, 8 and 5 tiles across, added together
            // (so the pattern only repeats every 40 tiles): tuft-sized bumps, a quarter
            // of a tile across, with smaller frays on them.
            var fineA = FineNoise(8, detail, seed + 81u);
            var fineB = FineNoise(5, detail, seed + 82u);
            int na = 8 * detail, nb = 5 * detail;

            int n = mapSize * detail;
            for (int y = 0; y < n; y++)
            {
                float gy = (y + 0.5f) * coarse / detail;
                int y0 = (int)gy;
                float fy = gy - y0;
                int ra = (y % na) * na, rb = (y % nb) * nb, rb2 = ((y + nb / 2) % nb) * nb;
                for (int x = 0; x < n; x++)
                {
                    float gx = (x + 0.5f) * coarse / detail;
                    int x0 = (int)gx;
                    float fx = gx - x0;
                    int i = y0 * cn + x0;
                    float d = Mathf.Lerp(Mathf.Lerp(cd[i], cd[i + 1], fx), Mathf.Lerp(cd[i + cn], cd[i + cn + 1], fx), fy);
                    float m = Mathf.Lerp(Mathf.Lerp(cm[i], cm[i + 1], fx), Mathf.Lerp(cm[i + cn], cm[i + cn + 1], fx), fy);
                    float noiseDry = fineA[ra + x % na] + fineB[rb + x % nb];
                    float noiseMud = fineA[ra + (x + na / 2) % na] + fineB[rb2 + x % nb];
                    dry[y * n + x] = MaskAlpha(d + noiseDry * 0.8f);
                    mud[y * n + x] = MaskAlpha(m + noiseMud * 0.8f);
                }
            }
        }

        /// <summary>
        /// Like <see cref="PatchMasks"/>, for one square of an endless world: <paramref name="tiles"/> across,
        /// its bottom-left corner at tile (<paramref name="originX"/>, <paramref name="originY"/>). Squares next to
        /// each other join up exactly (everything is worked out from world positions). Plain maths: safe to run
        /// on another thread.
        /// </summary>
        public static void PatchMasksAt(int originX, int originY, int tiles, int detail, uint seed, byte[] dry, byte[] mud)
        {
            const int coarse = 2;   // patch samples per tile (the patches are big and soft; the fine noise gives the ragged edges)
            int cn = tiles * coarse + 2;
            var cd = new float[cn * cn];
            var cm = new float[cn * cn];
            for (int y = 0; y < cn; y++)
                for (int x = 0; x < cn; x++)
                {
                    Patches(originX + x / (float)coarse, originY + y / (float)coarse, seed, out float d, out float m);
                    cd[y * cn + x] = d;
                    cm[y * cn + x] = m;
                }
            var fineA = FineNoise(8, detail, seed + 81u);
            var fineB = FineNoise(5, detail, seed + 82u);
            int na = 8 * detail, nb = 5 * detail;
            int n = tiles * detail;
            int px0 = originX * detail, py0 = originY * detail;   // this square's first pixel in the whole world
            for (int y = 0; y < n; y++)
            {
                float gy = (y + 0.5f) * coarse / detail;
                int y0 = (int)gy;
                float fy = gy - y0;
                int wy = py0 + y;
                int ra = Mod(wy, na) * na, rb = Mod(wy, nb) * nb, rb2 = Mod(wy + nb / 2, nb) * nb;
                for (int x = 0; x < n; x++)
                {
                    float gx = (x + 0.5f) * coarse / detail;
                    int x0 = (int)gx;
                    float fx = gx - x0;
                    int i = y0 * cn + x0;
                    int wx = px0 + x;
                    float d = Mathf.Lerp(Mathf.Lerp(cd[i], cd[i + 1], fx), Mathf.Lerp(cd[i + cn], cd[i + cn + 1], fx), fy);
                    float m = Mathf.Lerp(Mathf.Lerp(cm[i], cm[i + 1], fx), Mathf.Lerp(cm[i + cn], cm[i + cn + 1], fx), fy);
                    float noiseDry = fineA[ra + Mod(wx, na)] + fineB[rb + Mod(wx, nb)];
                    float noiseMud = fineA[ra + Mod(wx + na / 2, na)] + fineB[rb2 + Mod(wx, nb)];
                    dry[y * n + x] = MaskAlpha(d + noiseDry * 0.8f);
                    mud[y * n + x] = MaskAlpha(m + noiseMud * 0.8f);
                }
            }
        }

        /// <summary>
        /// The soft tint (<see cref="Tint"/>) over one square of an endless world, <paramref name="detail"/> pixels per
        /// tile. <paramref name="forest"/> says how much forest there is at a world position (0..1, the same noise
        /// the trees come from) and <paramref name="treeLine"/>/<paramref name="bushLine"/> where the woods start.
        /// Safe to run on another thread.
        /// </summary>
        public static void TintAt(int originX, int originY, int tiles, int detail, uint seed, System.Func<float, float, float> forest,
            float treeLine, float bushLine, Color32[] pixels)
        {
            int n = tiles * detail;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float tx = originX + (x + 0.5f) / detail, ty = originY + (y + 0.5f) / detail;
                    float wood = PixelImage.Smooth(bushLine, treeLine + 0.04f, forest(tx - 0.5f, ty - 0.5f));
                    Patches(tx, ty, seed, out float dryPatch, out float mudPatch);
                    pixels[y * n + x] = Tint(tx, ty, seed, wood, 0f, dryPatch, mudPatch);
                }
        }

        static int Mod(int v, int m) => ((v % m) + m) % m;

        // A sharp but smooth step around 0.5, so the GPU's blending between mask pixels
        // still gives smooth edges at the cut-off.
        static byte MaskAlpha(float v) => (byte)Mathf.RoundToInt(Mathf.Clamp01((v - 0.5f) * 4f + 0.5f) * 255f);

        // Seamless fine noise, -0.25..0.25, for a square <paramref name="tiles"/> across.
        static float[] FineNoise(int tiles, int detail, uint seed)
        {
            int n = tiles * detail;
            var table = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    table[y * n + x] = (Noise.Fractal(x * 4f / detail, y * 4f / detail, seed, 2, tiles * 4) - 0.5f) * 0.5f;
            return table;
        }

        /// <summary>
        /// The soft tint laid over the ground at a spot (map position in tiles):
        /// darker under forests (<paramref name="wood"/> 0..1), khaki where the
        /// ground is dry, and blotches of lighter and darker grass a few tiles
        /// across so big areas aren't flat. Darker near the map's edge (<paramref name="rim"/> 0..1).
        /// <paramref name="dryPatch"/> and <paramref name="mudPatch"/> are the <see cref="Patches"/>
        /// values there: along a patch's edge the tint blends both sides toward a
        /// color in between, so the ragged edge fades softly instead of being a hard cut.
        /// </summary>
        public static Color32 Tint(float x, float y, uint seed, float wood, float rim, float dryPatch, float mudPatch)
        {
            float dry = Noise.Fractal(x / 9f, y / 9f, seed + 71u, 2);
            float blotch = Noise.Fractal(x / 5f, y / 5f, seed + 73u, 2);
            float mottle = Noise.Fractal(x / 2.2f, y / 2.2f, seed + 76u, 2);

            // Lighter and darker grass, then the dry khaki, then the patch edges, then the
            // forest shade, each laid over the last.
            var c = PixelImage.Rgb(0, 0, 0, 0);
            c = Over(c, PixelImage.Rgb(70, 78, 42, Mathf.RoundToInt(255 * PixelImage.Smooth(0.55f, 0.75f, mottle) * 0.1f)));
            c = Over(c, PixelImage.Rgb(6, 8, 4, Mathf.RoundToInt(255 * PixelImage.Smooth(0.45f, 0.2f, mottle) * 0.16f)));
            c = Over(c, PixelImage.Rgb(8, 10, 6, Mathf.RoundToInt(255 * PixelImage.Smooth(0.45f, 0.75f, blotch) * 0.16f)));
            c = Over(c, PixelImage.Rgb(96, 84, 52, Mathf.RoundToInt(255 * PixelImage.Smooth(0.58f, 0.75f, dry) * 0.16f * (1f - wood))));
            c = Over(c, PixelImage.Rgb(62, 62, 38, Mathf.RoundToInt(255 * EdgeBand(dryPatch) * 0.4f)));
            c = Over(c, PixelImage.Rgb(40, 36, 24, Mathf.RoundToInt(255 * EdgeBand(mudPatch) * 0.35f)));
            if (wood > 0.02f) c = Over(c, PixelImage.Rgb(6, 12, 6, Mathf.RoundToInt(255 * (0.08f + 0.24f * wood))));
            if (rim > 0f) c = Over(c, PixelImage.Rgb(4, 6, 4, Mathf.RoundToInt(220 * rim)));
            return c;
        }

        // 1 on a patch's edge (value 0.5), fading to 0 a tile or so to either side.
        static float EdgeBand(float patch) => PixelImage.Smooth(0.38f, 0f, Mathf.Abs(patch - 0.5f));

        // Lays a see-through color over another one ("over" blending), keeping how see-through the result is.
        static Color32 Over(Color32 under, Color32 over)
        {
            float a = over.a / 255f, b = under.a / 255f;
            float outA = a + b * (1f - a);
            if (outA <= 0f) return PixelImage.Rgb(0, 0, 0, 0);
            float Mix(byte top, byte bottom) => (top * a + bottom * b * (1f - a)) / outA;
            return new Color32((byte)Mathf.RoundToInt(Mix(over.r, under.r)), (byte)Mathf.RoundToInt(Mix(over.g, under.g)),
                (byte)Mathf.RoundToInt(Mix(over.b, under.b)), (byte)Mathf.RoundToInt(outA * 255f));
        }

        // Fractal noise that repeats exactly across the picture.
        static float Field(int x, int y, int size, int cellsAcross, uint seed, int octaves)
        {
            float u = x * (float)cellsAcross / size, v = y * (float)cellsAcross / size;
            return Noise.Fractal(u, v, seed, octaves, cellsAcross);
        }
    }
}
