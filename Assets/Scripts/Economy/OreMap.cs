using System;
using System.Collections.Generic;

namespace KingdomGame.Economy
{
    /// <summary>The kinds of ore field on the ground.</summary>
    public enum OreKind : byte
    {
        None,
        Iron,
        Copper,
        Coal,
        Stone,
    }

    /// <summary>
    /// Ore fields on the ground, Factorio-style: iron, copper, coal and stone in patches about a dozen to thirty tiles
    /// across, a few hundred to a few thousand in each tile (richest in the middle of a field, and richer the further from
    /// the start). Where they are is worked out from the world's seed, the same on every computer: one field of each kind a
    /// short walk round the start, and further out at most one field in each square of <see cref="Region"/> tiles, inside
    /// it. Drills (Automation/Drill.cs) mine them; only what's been taken is kept (and saved).
    ///
    /// Plain C# with no Unity code: tools/world-tests checks it. <see cref="Generate"/> and <see cref="FieldsNear"/> are
    /// pure maths, safe on any thread (the ground is drawn on another one); what's been taken is only read and changed on
    /// the main thread (<see cref="TakenIn"/> copies a square of it for the ground drawing).
    /// </summary>
    public sealed class OreMap
    {
        /// <summary>The squares (tiles across) that hold at most one field each, well inside them.</summary>
        public const int Region = 96;
        /// <summary>The world's edge (the same as NatureMap.Limit): no ore past it, where house insides and mines are.</summary>
        public const int Limit = 32000;
        /// <summary>How far a field's middle stays from its square's sides: further than the biggest field reaches.</summary>
        const int Margin = 21;
        /// <summary>The share of squares with a field.</summary>
        const float FieldChance = 0.5f;
        /// <summary>The start's own four fields are within this many tiles of the middle (each way).</summary>
        const int StartReach = 64;

        /// <summary>One ore field: a blob round (<see cref="X"/>, <see cref="Y"/>) with a wobbly edge.</summary>
        public struct Field
        {
            public OreKind Kind;
            public float X, Y, Radius;
            /// <summary>How much more each tile holds than a field at the start (1).</summary>
            public float Richness;
            /// <summary>The seed of its edge's wobble.</summary>
            public uint Shape;
            /// <summary>The tiles it can cover (inclusive): nothing of it is outside.</summary>
            public int MinX, MinY, MaxX, MaxY;

            public bool Touches(int x0, int y0, int x1, int y1) => MaxX >= x0 && MinX <= x1 && MaxY >= y0 && MinY <= y1;
        }

        public static OreMap Current { get; set; }

        public readonly uint Seed;
        readonly Dictionary<long, int> taken = new Dictionary<long, int>();

        /// <summary>
        /// A tile's look changed (it's emptier by a quarter, or mined out), or a saved game put back what was taken:
        /// the ground there is drawn again.
        /// </summary>
        public event Action<int, int> Changed;

        public OreMap(uint seed) => Seed = seed;

        // ------------------------------------------------------------ the world's fields --

        /// <summary>What ore a tile has when the world is made, and how much (<paramref name="amount"/>; 0 and None: none).</summary>
        public static OreKind Generate(uint seed, int x, int y, out int amount)
        {
            amount = 0;
            if (x <= -Limit || x >= Limit || y <= -Limit || y >= Limit) return OreKind.None;
            float px = x + 0.5f, py = y + 0.5f;
            if (x > -StartReach && x < StartReach && y > -StartReach && y < StartReach)
                foreach (var f in StartFields(seed))
                    if (Amount(f, px, py, out amount)) return f.Kind;
            if (FieldOf(seed, FloorDiv(x, Region), FloorDiv(y, Region), out var field) && Amount(field, px, py, out amount)) return field.Kind;
            return OreKind.None;
        }

        /// <summary>
        /// How far inside a field a point is: above 0 inside (about 1 in its middle), 0 at its edge, below 0 outside. The
        /// ground drawing uses it for every pixel, so the edges are smooth; a tile has ore when its middle is inside.
        /// </summary>
        public static float Inside(in Field f, float px, float py)
        {
            if (px < f.MinX || px > f.MaxX + 1 || py < f.MinY || py > f.MaxY + 1) return -1f;
            float dx = px - f.X, dy = py - f.Y;
            float d = (float)Math.Sqrt(dx * dx + dy * dy) / f.Radius;
            float wobble = 0.65f * Smooth(px / 5f, py / 5f, f.Shape) + 0.35f * Smooth(px / 2.2f, py / 2.2f, f.Shape + 7u) - 0.5f;
            return 1f - (d + wobble * 0.7f);
        }

        static bool Amount(in Field f, float px, float py, out int amount)
        {
            amount = 0;
            if (Inside(f, px, py) <= 0f) return false;
            float dx = px - f.X, dy = py - f.Y;
            float d = Math.Min(1f, (float)Math.Sqrt(dx * dx + dy * dy) / f.Radius);
            amount = Math.Max(20, (int)Math.Round(Base(f.Kind) * (0.35f + 0.65f * (1f - d)) * f.Richness));
            return true;
        }

        // How much a tile in the middle of a field at the start holds.
        static float Base(OreKind kind) => kind == OreKind.Coal ? 500f : kind == OreKind.Stone ? 450f : 600f;

        sealed class StartCache
        {
            public uint Seed;
            public Field[] Fields;
        }

        static StartCache startCache;

        /// <summary>
        /// The four fields round the start, one of each: iron (the biggest), copper, coal and stone, 30 to 42 tiles from the
        /// middle in four directions (clear of the castle and the test outpost).
        /// </summary>
        public static Field[] StartFields(uint seed)
        {
            var cache = startCache;   // (one read: another thread may swap it meanwhile)
            if (cache != null && cache.Seed == seed) return cache.Fields;
            uint s = unchecked(seed * 31u + 0x5747u);
            float a0 = Hash01(s, 0, 0) * (float)Math.PI * 2f;
            var kinds = new[] { OreKind.Iron, OreKind.Copper, OreKind.Coal, OreKind.Stone };
            var fields = new Field[kinds.Length];
            for (int k = 0; k < kinds.Length; k++)
            {
                float a = a0 + k * (float)Math.PI * 0.5f + (Hash01(s, k, 1) - 0.5f) * 0.6f;
                float d = 30f + Hash01(s, k, 2) * 12f;
                float r = (kinds[k] == OreKind.Iron ? 7.5f : kinds[k] == OreKind.Stone ? 5.5f : 6.5f) + Hash01(s, k, 3);
                fields[k] = Make(kinds[k], (float)Math.Cos(a) * d, (float)Math.Sin(a) * d, r, Hash(s, k, 4), 1f);
            }
            startCache = new StartCache { Seed = seed, Fields = fields };
            return fields;
        }

        /// <summary>The field in square (<paramref name="rx"/>, <paramref name="ry"/>), if it has one (the four squares round the middle never do: the start has its own).</summary>
        public static bool FieldOf(uint seed, int rx, int ry, out Field field)
        {
            field = default;
            if ((rx == 0 || rx == -1) && (ry == 0 || ry == -1)) return false;
            uint s = unchecked(seed * 31u + 0x0BE5u);
            if (Hash01(s, rx, ry) >= FieldChance) return false;
            float cx = rx * Region + Margin + Hash01(s + 1u, rx, ry) * (Region - 2 * Margin);
            float cy = ry * Region + Margin + Hash01(s + 2u, rx, ry) * (Region - 2 * Margin);
            float far = (float)Math.Sqrt(cx * cx + cy * cy);
            float r = 5f + Hash01(s + 3u, rx, ry) * 5f + Math.Min(3f, far / 500f);
            float k = Hash01(s + 4u, rx, ry);
            var kind = k < 0.32f ? OreKind.Iron : k < 0.6f ? OreKind.Copper : k < 0.84f ? OreKind.Coal : OreKind.Stone;
            field = Make(kind, cx, cy, r, Hash(s + 5u, rx, ry), Math.Min(4f, 1f + far / 800f));
            return true;
        }

        static Field Make(OreKind kind, float x, float y, float r, uint shape, float richness)
        {
            // The wobble moves the edge out to at most 1.35 radii (Inside: 0.7 x 0.5).
            float reach = r * 1.35f + 1f;
            return new Field
            {
                Kind = kind, X = x, Y = y, Radius = r, Richness = richness, Shape = shape,
                MinX = (int)Math.Floor(x - reach), MinY = (int)Math.Floor(y - reach), MaxX = (int)Math.Ceiling(x + reach), MaxY = (int)Math.Ceiling(y + reach),
            };
        }

        /// <summary>The fields touching the tiles (<paramref name="x0"/>, <paramref name="y0"/>) to (<paramref name="x1"/>, <paramref name="y1"/>) (inclusive).</summary>
        public static void FieldsNear(uint seed, int x0, int y0, int x1, int y1, List<Field> into)
        {
            into.Clear();
            if (x0 < StartReach && x1 > -StartReach && y0 < StartReach && y1 > -StartReach)
                foreach (var f in StartFields(seed))
                    if (f.Touches(x0, y0, x1, y1)) into.Add(f);
            for (int ry = FloorDiv(y0, Region); ry <= FloorDiv(y1, Region); ry++)
                for (int rx = FloorDiv(x0, Region); rx <= FloorDiv(x1, Region); rx++)
                    if (FieldOf(seed, rx, ry, out var f) && f.Touches(x0, y0, x1, y1)) into.Add(f);
        }

        // ------------------------------------------------------------ what's left --

        static long Key(int x, int y) => ((long)x << 32) | (uint)y;

        /// <summary>The ore on a tile now, and how much is left (None and 0: none, or mined out).</summary>
        public OreKind At(int x, int y, out int left)
        {
            var kind = Generate(Seed, x, y, out int amount);
            left = 0;
            if (kind == OreKind.None) return OreKind.None;
            taken.TryGetValue(Key(x, y), out int t);
            left = amount - t;
            if (left > 0) return kind;
            left = 0;
            return OreKind.None;
        }

        /// <summary>Takes one ore from a tile: false if it has none left.</summary>
        public bool Mine(int x, int y, out OreKind kind)
        {
            kind = Generate(Seed, x, y, out int amount);
            if (kind == OreKind.None) return false;
            long key = Key(x, y);
            taken.TryGetValue(key, out int t);
            if (t >= amount)
            {
                kind = OreKind.None;
                return false;
            }
            taken[key] = t + 1;
            if (Level(amount, t + 1) != Level(amount, t)) Changed?.Invoke(x, y);
            return true;
        }

        /// <summary>How full a tile looks: 4 (more than three quarters left) down to 1, and 0 when it's mined out.</summary>
        public static int Level(int amount, int taken) =>
            amount <= 0 || taken >= amount ? 0 : (int)Math.Ceiling(4.0 * (amount - taken) / amount);

        /// <summary>How much has been taken from a tile.</summary>
        public int TakenAt(int x, int y) => taken.TryGetValue(Key(x, y), out int t) ? t : 0;

        /// <summary>Tiles anything has been taken from.</summary>
        public int TakenTiles => taken.Count;

        /// <summary>
        /// A copy of what's been taken from the tiles (<paramref name="x0"/>, <paramref name="y0"/>) to (x0 + w - 1, y0 + h - 1),
        /// row by row from the bottom; null when nothing has (for drawing the ground on another thread).
        /// </summary>
        public int[] TakenIn(int x0, int y0, int w, int h)
        {
            if (taken.Count == 0) return null;
            int[] result = null;
            if (taken.Count < w * h)
            {
                foreach (var pair in taken)
                {
                    int x = (int)(pair.Key >> 32), y = (int)(uint)pair.Key;
                    if (x < x0 || x >= x0 + w || y < y0 || y >= y0 + h) continue;
                    (result ??= new int[w * h])[(y - y0) * w + (x - x0)] = pair.Value;
                }
                return result;
            }
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (taken.TryGetValue(Key(x0 + x, y0 + y), out int t)) (result ??= new int[w * h])[y * w + x] = t;
            return result;
        }

        /// <summary>What's been taken, for a save: x, y, amount, x, y, amount...</summary>
        public int[] Save()
        {
            var data = new int[taken.Count * 3];
            int i = 0;
            foreach (var pair in taken)
            {
                data[i++] = (int)(pair.Key >> 32);
                data[i++] = (int)(uint)pair.Key;
                data[i++] = pair.Value;
            }
            return data;
        }

        /// <summary>Puts back what a saved game had taken (older saves: nothing).</summary>
        public void Load(int[] data)
        {
            var before = new List<long>(taken.Keys);
            taken.Clear();
            foreach (long key in before) Changed?.Invoke((int)(key >> 32), (int)(uint)key);
            if (data == null) return;
            for (int i = 0; i + 2 < data.Length; i += 3)
            {
                if (data[i + 2] <= 0) continue;
                taken[Key(data[i], data[i + 1])] = data[i + 2];
                Changed?.Invoke(data[i], data[i + 1]);
            }
        }

        // ------------------------------------------------------------ names --

        /// <summary>What a drill gets out of it.</summary>
        public static ResourceType Item(OreKind kind) =>
            kind == OreKind.Copper ? ResourceType.CopperOre : kind == OreKind.Coal ? ResourceType.Coal : kind == OreKind.Stone ? ResourceType.Stone : ResourceType.IronOre;

        public static string Name(OreKind kind) =>
            kind == OreKind.Iron ? "iron ore" : kind == OreKind.Copper ? "copper ore" : kind == OreKind.Coal ? "coal" : kind == OreKind.Stone ? "stone" : "nothing";

        // ------------------------------------------------------------ maths --

        static int FloorDiv(int v, int d) => v >= 0 ? v / d : -((-v + d - 1) / d);

        static uint Hash(uint seed, int x, int y)
        {
            unchecked
            {
                uint h = seed ^ 0x9E3779B9u;
                h ^= (uint)x * 0x85EBCA6Bu;
                h = ((h << 13) | (h >> 19)) * 5u + 0xE6546B64u;
                h ^= (uint)y * 0xC2B2AE35u;
                h = ((h << 13) | (h >> 19)) * 5u + 0xE6546B64u;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>0 (included) to 1 (not included), the same for the same seed and spot.</summary>
        public static float Hash01(uint seed, int x, int y) => (Hash(seed, x, y) & 0xFFFFFF) / 16777216f;

        /// <summary>Smooth noise, 0..1 (for edges and for the clumps of ore the ground drawing shows).</summary>
        public static float Smooth(float x, float y, uint seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            float sx = fx * fx * (3f - 2f * fx), sy = fy * fy * (3f - 2f * fy);
            float a = Hash01(seed, x0, y0), b = Hash01(seed, x0 + 1, y0), c = Hash01(seed, x0, y0 + 1), d = Hash01(seed, x0 + 1, y0 + 1);
            float bottom = a + (b - a) * sx, top = c + (d - c) * sx;
            return bottom + (top - bottom) * sy;
        }
    }
}
