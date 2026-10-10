using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Economy
{
    /// <summary>
    /// Every tree, rock and bush in an endless world. What grows where is worked out from the
    /// world's seed (the generator, the same on every computer), a square of
    /// <see cref="ChunkTiles"/> tiles at a time, when something first asks about it; only
    /// changes (cut, planted, gone, growing back) are kept for good and saved. Squares nobody
    /// has changed are forgotten again when there are too many, and worked out again if needed.
    ///
    /// Only the ones near players and kingdoms are real objects (<see cref="ResourceNode"/>s,
    /// made by World.NatureStreamer); they write what happens to them back here. Walkers'
    /// paths read <see cref="Blocks"/> straight from here.
    ///
    /// The world goes <see cref="Limit"/> tiles out from the middle each way (hours of
    /// walking): further out, the numbers a computer keeps positions in get too coarse.
    /// </summary>
    public sealed class NatureMap
    {
        public const int ChunkTiles = 32;
        /// <summary>The world's edge, in tiles from the middle (house insides and mines are beyond it).</summary>
        public const int Limit = 32000;
        const int MaxChunks = 6000;

        [Flags]
        enum Flag : byte
        {
            None = 0,
            /// <summary>Solid: people bump into it and go round it.</summary>
            Blocks = 1,
            /// <summary>Planted by a kingdom (no jitter, and saved in full).</summary>
            Planted = 2,
            /// <summary>Not as the world made it (saved).</summary>
            Changed = 4,
        }

        sealed class Chunk
        {
            public readonly byte[] Kind = new byte[ChunkTiles * ChunkTiles];       // 0 = nothing, else kinds[k - 1]
            public readonly byte[] Variant = new byte[ChunkTiles * ChunkTiles];
            public readonly short[] Left = new short[ChunkTiles * ChunkTiles];
            public readonly Flag[] Flags = new Flag[ChunkTiles * ChunkTiles];
            public int Changed;    // cells changed (the square is kept for good when > 0)
            public int Live;       // cells shown as real objects
            public int Used;       // the frame it was last asked about
        }

        /// <summary>Time state while a node isn't a real object: growing (start, end) or growing back (at end).</summary>
        struct Timer
        {
            public bool Growing;
            public float Start, End;
        }

        /// <summary>What stands at a cell when the world is made: a kind (or null) and a picture variant.</summary>
        public delegate ResourceNodeConfig Generator(int x, int y, out int variant);

        public static NatureMap Current { get; set; }

        public readonly uint Seed;
        readonly Generator generate;
        readonly List<ResourceNodeConfig> kinds = new List<ResourceNodeConfig>();
        readonly Dictionary<ResourceNodeConfig, byte> kindIndex = new Dictionary<ResourceNodeConfig, byte>();
        readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();
        readonly Dictionary<Vector2Int, Timer> timers = new Dictionary<Vector2Int, Timer>();
        readonly Dictionary<Vector2Int, ResourceNode> live = new Dictionary<Vector2Int, ResourceNode>();
        Vector2Int lastKey = new Vector2Int(int.MinValue, int.MinValue);
        Chunk lastChunk;

        /// <summary>A cell started or stopped being solid (for zombie paths).</summary>
        public event Action<Vector2Int> BlockingChanged;
        /// <summary>A node was planted (or put back from a save): the streamer makes it if that part of the world is shown.</summary>
        public event Action<Vector2Int> Planted;

        /// <summary>Squares worked out and kept in memory right now (for the debug screen and tests).</summary>
        public int Squares => chunks.Count;

        public NatureMap(uint seed, Generator generator)
        {
            Seed = seed;
            generate = generator;
        }

        /// <summary>Whether a cell is in the world (not past its edge).</summary>
        public static bool Inside(Vector2Int cell) => cell.x > -Limit && cell.x < Limit && cell.y > -Limit && cell.y < Limit;

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);

        Chunk ChunkFor(Vector2Int cell, out int i)
        {
            var key = new Vector2Int(FloorDiv(cell.x, ChunkTiles), FloorDiv(cell.y, ChunkTiles));
            i = (cell.y - key.y * ChunkTiles) * ChunkTiles + (cell.x - key.x * ChunkTiles);
            if (key == lastKey && lastChunk != null) return lastChunk;
            if (!chunks.TryGetValue(key, out var chunk))
            {
                if (chunks.Count >= MaxChunks) Forget();
                chunk = Make(key);
                chunks[key] = chunk;
            }
            chunk.Used = Time.frameCount;
            lastKey = key;
            lastChunk = chunk;
            return chunk;
        }

        // Works out what grows in a square (the same every time, from the seed).
        Chunk Make(Vector2Int key)
        {
            var chunk = new Chunk();
            for (int y = 0; y < ChunkTiles; y++)
                for (int x = 0; x < ChunkTiles; x++)
                {
                    int wx = key.x * ChunkTiles + x, wy = key.y * ChunkTiles + y;
                    int variant = 0;
                    var config = generate != null ? generate(wx, wy, out variant) : null;
                    if (config == null) continue;
                    int i = y * ChunkTiles + x;
                    chunk.Kind[i] = KindIndex(config);
                    chunk.Variant[i] = (byte)Mathf.Clamp(variant, 0, 255);
                    chunk.Left[i] = (short)Mathf.Clamp(config.amount, 0, short.MaxValue);
                    chunk.Flags[i] = Flag.Blocks;
                }
            return chunk;
        }

        // Too many squares: forget the oldest half of those nobody changed and nothing is shown in.
        void Forget()
        {
            var unchanged = new List<KeyValuePair<Vector2Int, Chunk>>();
            foreach (var pair in chunks)
                if (pair.Value.Changed == 0 && pair.Value.Live == 0) unchanged.Add(pair);
            unchanged.Sort((a, b) => a.Value.Used.CompareTo(b.Value.Used));
            for (int k = 0; k < unchanged.Count / 2; k++) chunks.Remove(unchanged[k].Key);
            lastChunk = null;
            lastKey = new Vector2Int(int.MinValue, int.MinValue);
        }

        byte KindIndex(ResourceNodeConfig config)
        {
            if (kindIndex.TryGetValue(config, out byte k)) return k;
            if (kinds.Count >= 255) throw new InvalidOperationException("More than 255 kinds of trees, rocks and bushes.");
            kinds.Add(config);
            k = (byte)kinds.Count;
            kindIndex[config] = k;
            return k;
        }

        static void MarkChanged(Chunk chunk, int i)
        {
            if ((chunk.Flags[i] & Flag.Changed) != 0) return;
            chunk.Flags[i] |= Flag.Changed;
            chunk.Changed++;
        }

        // ---------------------------------------------------------- reading --

        /// <summary>Whether a tree, rock or bush (or a stump) stands in the way here. Past the world's edge: yes.</summary>
        public bool Blocks(Vector2Int cell)
        {
            if (!Inside(cell)) return true;
            var chunk = ChunkFor(cell, out int i);
            return (chunk.Flags[i] & Flag.Blocks) != 0;
        }

        /// <summary>Whether a tree, rock or bush stands here with something left to gather (it's in the way of building).</summary>
        public bool Standing(Vector2Int cell, out NodeLook look)
        {
            look = NodeLook.Tree;
            if (!Inside(cell)) return false;
            if (live.TryGetValue(cell, out var node) && node != null)
            {
                look = node.Config.look;
                return !node.Empty;
            }
            var chunk = ChunkFor(cell, out int i);
            if (chunk.Kind[i] == 0) return false;
            look = kinds[chunk.Kind[i] - 1].look;
            Settle(cell, chunk, i);
            return chunk.Left[i] > 0;
        }

        // A node that isn't a real object finished growing (or growing back) while nobody was near: it's full now.
        void Settle(Vector2Int cell, Chunk chunk, int i)
        {
            if (!timers.TryGetValue(cell, out var t) || Time.time < t.End || live.ContainsKey(cell)) return;
            timers.Remove(cell);
            if (chunk.Kind[i] == 0) return;
            chunk.Left[i] = (short)Mathf.Clamp(kinds[chunk.Kind[i] - 1].amount, 0, short.MaxValue);
            SetBlocks(cell, chunk, i, true);
        }

        /// <summary>What kind stands at a cell, or null.</summary>
        public ResourceNodeConfig KindAt(Vector2Int cell)
        {
            if (!Inside(cell)) return null;
            var chunk = ChunkFor(cell, out int i);
            return chunk.Kind[i] != 0 ? kinds[chunk.Kind[i] - 1] : null;
        }

        public bool IsPlanted(Vector2Int cell) => Inside(cell) && (ChunkFor(cell, out int i).Flags[i] & Flag.Planted) != 0;
        public int VariantAt(Vector2Int cell) => Inside(cell) ? ChunkFor(cell, out int i).Variant[i] : 0;

        /// <summary>The real object for a cell, if that part of the world is shown, or null.</summary>
        public ResourceNode Live(Vector2Int cell) => live.TryGetValue(cell, out var n) && n != null ? n : null;

        /// <summary>
        /// The nearest cell to <paramref name="from"/> (within <paramref name="radius"/> tiles) with a tree, rock or
        /// bush giving <paramref name="type"/> that has something left, skipping cells <paramref name="skip"/> says to.
        /// Searches outwards ring by ring, so near ones are found quickly.
        /// </summary>
        public bool FindNearest(Vector2 from, ResourceType type, int radius, Func<Vector2Int, bool> skip, out Vector2Int found)
        {
            var center = new Vector2Int(Mathf.FloorToInt(from.x), Mathf.FloorToInt(from.y));
            found = default;
            float best = float.MaxValue;
            for (int r = 0; r <= radius; r++)
            {
                // Once something is found, rings further out than it can't hold anything nearer.
                if (best < float.MaxValue && r - 1 > Mathf.Sqrt(best)) break;
                for (int dy = -r; dy <= r; dy++)
                {
                    bool edgeRow = dy == -r || dy == r;
                    // The ring only: whole top and bottom rows, just the two ends of the rows between.
                    for (int dx = -r; dx <= r; dx += edgeRow ? 1 : 2 * r)
                    {
                        var cell = new Vector2Int(center.x + dx, center.y + dy);
                        var kind = KindAt(cell);
                        if (kind != null && kind.resource == type && Standing(cell, out _) && (skip == null || !skip(cell)))
                        {
                            float d = (new Vector2(cell.x + 0.5f, cell.y + 0.5f) - from).sqrMagnitude;
                            if (d < best) { best = d; found = cell; }
                        }
                        if (r == 0) break;
                    }
                }
            }
            return best < float.MaxValue;
        }

        // ---------------------------------------------------------- changes --

        /// <summary>
        /// A young tree or bush planted at <paramref name="cell"/>, fully grown in <paramref name="growSeconds"/>.
        /// Returns its object if that part of the world is shown (the streamer makes it), else null.
        /// </summary>
        public ResourceNode Plant(Vector2Int cell, ResourceNodeConfig config, int variantIndex, float growSeconds)
        {
            if (!Inside(cell) || config == null) return null;
            Clear(cell);
            var chunk = ChunkFor(cell, out int i);
            chunk.Kind[i] = KindIndex(config);
            chunk.Variant[i] = (byte)Mathf.Clamp(variantIndex, 0, 255);
            chunk.Left[i] = 0;
            chunk.Flags[i] |= Flag.Planted;
            MarkChanged(chunk, i);
            float now = Time.time;
            timers[cell] = new Timer { Growing = true, Start = now, End = now + Mathf.Max(1f, growSeconds) };
            SetBlocks(cell, chunk, i, true);
            Planted?.Invoke(cell);
            return Live(cell);
        }

        /// <summary>Takes away whatever stands at a cell, for good (a building goes up there).</summary>
        public void Clear(Vector2Int cell)
        {
            if (!Inside(cell)) return;
            var chunk = ChunkFor(cell, out int i);
            if (chunk.Kind[i] == 0) return;
            if (live.TryGetValue(cell, out var node))
            {
                Detach(cell, node);
                if (node != null)
                {
                    node.Unbind();
                    UnityEngine.Object.Destroy(node.gameObject);
                }
                chunk = ChunkFor(cell, out i);
            }
            chunk.Kind[i] = 0;
            chunk.Left[i] = 0;
            timers.Remove(cell);
            chunk.Flags[i] &= ~Flag.Planted;
            MarkChanged(chunk, i);   // (before telling anyone: a changed square is never forgotten)
            SetBlocks(cell, chunk, i, false);
        }

        // ------------------------------------------------------ live objects --

        /// <summary>A node's object was made for this cell (it reads its state with <see cref="StateOf"/>).</summary>
        internal void Attach(Vector2Int cell, ResourceNode node)
        {
            if (live.ContainsKey(cell)) return;
            live[cell] = node;
            ChunkFor(cell, out _).Live++;
        }

        /// <summary>A node's object is going (that part of the world isn't shown): what happened to it is kept here.</summary>
        internal void Detach(Vector2Int cell, ResourceNode node)
        {
            if (!live.TryGetValue(cell, out var n) || n != node) return;
            live.Remove(cell);
            var chunk = ChunkFor(cell, out _);
            chunk.Live = Mathf.Max(0, chunk.Live - 1);
        }

        /// <summary>
        /// The state of a node for its object: how much is left, and (as times on Time.time) whether
        /// it's growing (from <paramref name="start"/> to <paramref name="end"/>) or growing back at <paramref name="end"/>.
        /// </summary>
        public void StateOf(Vector2Int cell, out int amountLeft, out bool growing, out float start, out float end)
        {
            var chunk = ChunkFor(cell, out int i);
            Settle(cell, chunk, i);
            amountLeft = chunk.Left[i];
            growing = false;
            start = end = 0f;
            if (timers.TryGetValue(cell, out var t))
            {
                growing = t.Growing;
                start = t.Start;
                end = t.End;
            }
        }

        /// <summary>A live node's state changed (gathered from, grew, used up): written down here.</summary>
        internal void Record(Vector2Int cell, int amountLeft, bool growing, float start, float end, bool waiting)
        {
            var chunk = ChunkFor(cell, out int i);
            // Nothing happened to it (it's only going because nobody is near): it stays as the world made it.
            if (!growing && !waiting && chunk.Left[i] == amountLeft && !timers.ContainsKey(cell)) return;
            chunk.Left[i] = (short)Mathf.Clamp(amountLeft, 0, short.MaxValue);
            if (growing) timers[cell] = new Timer { Growing = true, Start = start, End = end };
            else if (waiting) timers[cell] = new Timer { Growing = false, Start = 0f, End = end };
            else timers.Remove(cell);
            MarkChanged(chunk, i);
        }

        /// <summary>A live node became solid or stopped being (a rock used up disappears; a stump stays).</summary>
        internal void SetBlocks(Vector2Int cell, bool blocks)
        {
            if (!Inside(cell)) return;
            var chunk = ChunkFor(cell, out int i);
            SetBlocks(cell, chunk, i, blocks);
        }

        void SetBlocks(Vector2Int cell, Chunk chunk, int i, bool blocks)
        {
            bool was = (chunk.Flags[i] & Flag.Blocks) != 0;
            if (was == blocks) return;
            if (blocks) chunk.Flags[i] |= Flag.Blocks;
            else chunk.Flags[i] &= ~Flag.Blocks;
            BlockingChanged?.Invoke(cell);
        }

        // ------------------------------------------------------------ saving --

        /// <summary>One cell that isn't as the world made it.</summary>
        public struct Change
        {
            public Vector2Int Cell;
            /// <summary>What stands there now, or null if nothing does.</summary>
            public ResourceNodeConfig Kind;
            public bool Planted;
            public int Variant;
            public int Left;
            /// <summary>0..1 while growing, -1 otherwise.</summary>
            public float Grown;
            public float GrowSeconds;
            /// <summary>Seconds until a used-up one grows back, -1 if it isn't waiting to.</summary>
            public float RegrowLeft;
        }

        /// <summary>Every cell that isn't as the world made it (live objects' latest state included).</summary>
        public List<Change> Changes()
        {
            foreach (var pair in live)
                if (pair.Value != null) pair.Value.WriteBack();
            float now = Time.time;
            var changes = new List<Change>();
            // (The changed squares are listed first: settling a cell tells listeners, who may ask about other squares.)
            var changed = new List<KeyValuePair<Vector2Int, Chunk>>();
            foreach (var pair in chunks)
                if (pair.Value.Changed > 0) changed.Add(pair);
            foreach (var pair in changed)
            {
                var chunk = pair.Value;
                for (int i = 0; i < chunk.Flags.Length; i++)
                {
                    if ((chunk.Flags[i] & Flag.Changed) == 0) continue;
                    var cell = new Vector2Int(pair.Key.x * ChunkTiles + i % ChunkTiles, pair.Key.y * ChunkTiles + i / ChunkTiles);
                    Settle(cell, chunk, i);
                    var c = new Change
                    {
                        Cell = cell, Kind = chunk.Kind[i] != 0 ? kinds[chunk.Kind[i] - 1] : null, Planted = (chunk.Flags[i] & Flag.Planted) != 0,
                        Variant = chunk.Variant[i], Left = chunk.Left[i], Grown = -1f, RegrowLeft = -1f,
                    };
                    if (timers.TryGetValue(cell, out var t))
                    {
                        if (t.Growing)
                        {
                            c.GrowSeconds = Mathf.Max(1f, t.End - t.Start);
                            c.Grown = Mathf.Clamp01((now - t.Start) / c.GrowSeconds);
                        }
                        else c.RegrowLeft = Mathf.Max(0f, t.End - now);
                    }
                    changes.Add(c);
                }
            }
            return changes;
        }

        /// <summary>Puts back one change from a save.</summary>
        public void Apply(Change c)
        {
            if (!Inside(c.Cell)) return;
            if (c.Kind == null)
            {
                Clear(c.Cell);
                var gone = ChunkFor(c.Cell, out int g);
                MarkChanged(gone, g);   // (nothing stood there, as the world is made now: still noted, so it stays saved)
                return;
            }
            if (live.ContainsKey(c.Cell)) Clear(c.Cell);
            var chunk = ChunkFor(c.Cell, out int i);
            chunk.Kind[i] = KindIndex(c.Kind);
            chunk.Variant[i] = (byte)Mathf.Clamp(c.Variant, 0, 255);
            chunk.Left[i] = (short)Mathf.Clamp(c.Left, 0, short.MaxValue);
            if (c.Planted) chunk.Flags[i] |= Flag.Planted;
            else chunk.Flags[i] &= ~Flag.Planted;
            MarkChanged(chunk, i);
            timers.Remove(c.Cell);
            float now = Time.time;
            bool blocks;
            if (c.Grown >= 0f && c.Grown < 1f)
            {
                float total = Mathf.Max(1f, c.GrowSeconds);
                float start = now - c.Grown * total;
                timers[c.Cell] = new Timer { Growing = true, Start = start, End = start + total };
                blocks = true;
            }
            else
            {
                if (c.Left <= 0 && c.RegrowLeft >= 0f) timers[c.Cell] = new Timer { Growing = false, End = now + c.RegrowLeft };
                // Used up: a stump or bare bush stays in the way; a rock is gone.
                blocks = c.Left > 0 || c.Kind.look != NodeLook.Rock;
            }
            SetBlocks(c.Cell, chunk, i, blocks);
            Planted?.Invoke(c.Cell);
        }
    }
}
