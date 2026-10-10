using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Economy
{
    /// <summary>
    /// Every tree, rock and bush on the map, kept as data: what it is, how much is left,
    /// and whether it's growing or growing back. A 1024-tile map has well over 100,000 of
    /// them, far too many to be real objects at once, so only the ones near players and
    /// kingdoms are made into <see cref="ResourceNode"/>s (by World.NatureStreamer). Those
    /// write what happens to them back here when they're taken away again.
    ///
    /// Walkers' paths read <see cref="Blocks"/> straight from here, so trees far from
    /// anyone still stand in the way.
    /// </summary>
    public sealed class NatureMap
    {
        [Flags]
        enum Flag : byte
        {
            None = 0,
            /// <summary>Solid: people bump into it and go round it.</summary>
            Blocks = 1,
            /// <summary>Planted by a kingdom (no jitter, and saved in full).</summary>
            Planted = 2,
            /// <summary>Not as the map made it (saved).</summary>
            Changed = 4,
        }

        /// <summary>Time state while a node isn't a real object: growing (start, end) or growing back (at).</summary>
        struct Timer
        {
            public bool Growing;
            public float Start, End;
        }

        public static NatureMap Current { get; set; }

        public readonly RectInt Cells;
        public readonly uint Seed;
        readonly List<ResourceNodeConfig> kinds = new List<ResourceNodeConfig>();
        readonly Dictionary<ResourceNodeConfig, byte> kindIndex = new Dictionary<ResourceNodeConfig, byte>();
        readonly byte[] kind;      // 0 = nothing, else kinds[kind - 1]
        readonly byte[] variant;
        readonly short[] left;
        readonly Flag[] flags;
        readonly Dictionary<int, Timer> timers = new Dictionary<int, Timer>();
        readonly Dictionary<int, ResourceNode> live = new Dictionary<int, ResourceNode>();

        /// <summary>A cell started or stopped being solid (for zombie paths).</summary>
        public event Action<Vector2Int> BlockingChanged;
        /// <summary>A node was planted where there was none: the streamer makes it if that part of the map is shown.</summary>
        public event Action<Vector2Int> Planted;

        public NatureMap(RectInt cells, uint seed)
        {
            Cells = cells;
            Seed = seed;
            int n = cells.width * cells.height;
            kind = new byte[n];
            variant = new byte[n];
            left = new short[n];
            flags = new Flag[n];
        }

        public bool Inside(Vector2Int cell) => Cells.Contains(cell);
        public int Index(Vector2Int cell) => (cell.y - Cells.y) * Cells.width + (cell.x - Cells.x);
        public Vector2Int CellOf(int index) => new Vector2Int(Cells.x + index % Cells.width, Cells.y + index / Cells.width);

        /// <summary>Whether a tree, rock or bush (or a stump) stands in the way here.</summary>
        public bool Blocks(Vector2Int cell) => Inside(cell) && (flags[Index(cell)] & Flag.Blocks) != 0;

        /// <summary>Whether a tree, rock or bush stands here with something left to gather (it's in the way of building).</summary>
        public bool Standing(Vector2Int cell, out NodeLook look)
        {
            look = NodeLook.Tree;
            if (!Inside(cell)) return false;
            int i = Index(cell);
            if (kind[i] == 0) return false;
            if (live.TryGetValue(i, out var node) && node != null)
            {
                look = node.Config.look;
                return !node.Empty;
            }
            look = kinds[kind[i] - 1].look;
            Settle(i);
            return left[i] > 0;
        }

        // A node that isn't a real object finished growing (or growing back) while nobody was near:
        // it's full now. (A real object does this itself in its Update.)
        void Settle(int i)
        {
            if (!timers.TryGetValue(i, out var t) || Time.time < t.End || live.ContainsKey(i)) return;
            timers.Remove(i);
            var config = KindOf(i);
            if (config == null) return;
            left[i] = (short)Mathf.Clamp(config.amount, 0, short.MaxValue);
            SetBlocks(i, true);
        }

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
                        if (!Inside(cell)) continue;
                        int i = Index(cell);
                        if (kind[i] == 0 || kinds[kind[i] - 1].resource != type) continue;
                        if (!Standing(cell, out _) || (skip != null && skip(cell))) continue;
                        float d = (new Vector2(cell.x + 0.5f, cell.y + 0.5f) - from).sqrMagnitude;
                        if (d < best) { best = d; found = cell; }
                    }
                }
            }
            return best < float.MaxValue;
        }

        /// <summary>What kind stands at a cell, or null.</summary>
        public ResourceNodeConfig KindAt(Vector2Int cell) => Inside(cell) && kind[Index(cell)] != 0 ? kinds[kind[Index(cell)] - 1] : null;

        /// <summary>The real object for a cell, if that part of the map is shown, or null.</summary>
        public ResourceNode Live(Vector2Int cell) => Inside(cell) && live.TryGetValue(Index(cell), out var n) && n != null ? n : null;

        public bool IsPlanted(int index) => (flags[index] & Flag.Planted) != 0;
        public byte VariantAt(int index) => variant[index];

        byte KindIndex(ResourceNodeConfig config)
        {
            if (kindIndex.TryGetValue(config, out byte k)) return k;
            if (kinds.Count >= 255) throw new InvalidOperationException("More than 255 kinds of trees, rocks and bushes.");
            kinds.Add(config);
            k = (byte)kinds.Count;
            kindIndex[config] = k;
            return k;
        }

        // ---------------------------------------------------------- the map --

        /// <summary>Puts a full-grown node on the map as it's made (not a change: not saved).</summary>
        public void Generate(Vector2Int cell, ResourceNodeConfig config, int variantIndex)
        {
            if (!Inside(cell) || config == null) return;
            int i = Index(cell);
            kind[i] = KindIndex(config);
            variant[i] = (byte)variantIndex;
            left[i] = (short)Mathf.Clamp(config.amount, 0, short.MaxValue);
            flags[i] = Flag.Blocks;
        }

        /// <summary>
        /// A young tree or bush planted at <paramref name="cell"/>, fully grown in <paramref name="growSeconds"/>.
        /// Returns its object if that part of the map is shown (the streamer makes it), else null.
        /// </summary>
        public ResourceNode Plant(Vector2Int cell, ResourceNodeConfig config, int variantIndex, float growSeconds)
        {
            if (!Inside(cell) || config == null) return null;
            Clear(cell);
            int i = Index(cell);
            kind[i] = KindIndex(config);
            variant[i] = (byte)variantIndex;
            left[i] = 0;
            flags[i] = Flag.Planted | Flag.Changed;
            float now = Time.time;
            timers[i] = new Timer { Growing = true, Start = now, End = now + Mathf.Max(1f, growSeconds) };
            SetBlocks(i, true);
            Planted?.Invoke(cell);
            return Live(cell);
        }

        /// <summary>Takes away whatever stands at a cell, for good (a building goes up there).</summary>
        public void Clear(Vector2Int cell)
        {
            if (!Inside(cell)) return;
            int i = Index(cell);
            if (kind[i] == 0) return;
            if (live.TryGetValue(i, out var node))
            {
                live.Remove(i);
                if (node != null) node.Unbind();
                if (node != null) UnityEngine.Object.Destroy(node.gameObject);
            }
            kind[i] = 0;
            left[i] = 0;
            timers.Remove(i);
            SetBlocks(i, false);
            flags[i] = Flag.Changed;
        }

        // ------------------------------------------------------ live objects --

        /// <summary>A node's object was made for this cell (it reads its state with <see cref="StateOf"/>).</summary>
        internal void Attach(int index, ResourceNode node) => live[index] = node;

        /// <summary>A node's object is going (that part of the map isn't shown): what happened to it is kept here.</summary>
        internal void Detach(int index, ResourceNode node)
        {
            if (live.TryGetValue(index, out var n) && n == node) live.Remove(index);
        }

        /// <summary>The cells shown as real objects right now.</summary>
        public IEnumerable<int> LiveCells => live.Keys;

        /// <summary>
        /// The state of a node for its object: how much is left, and (as times on Time.time) whether
        /// it's growing (from <paramref name="start"/> to <paramref name="end"/>) or growing back at <paramref name="end"/>.
        /// </summary>
        public void StateOf(int index, out int amountLeft, out bool growing, out float start, out float end)
        {
            Settle(index);
            amountLeft = left[index];
            growing = false;
            start = end = 0f;
            if (timers.TryGetValue(index, out var t))
            {
                growing = t.Growing;
                start = t.Start;
                end = t.End;
            }
        }

        /// <summary>A live node's state changed (gathered from, grew, used up): written down here.</summary>
        internal void Record(int index, int amountLeft, bool growing, float start, float end, bool waiting)
        {
            // Nothing happened to it (it's only going because nobody is near): it stays as the map made it.
            if (!growing && !waiting && left[index] == amountLeft && !timers.ContainsKey(index)) return;
            left[index] = (short)Mathf.Clamp(amountLeft, 0, short.MaxValue);
            if (growing) timers[index] = new Timer { Growing = true, Start = start, End = end };
            else if (waiting) timers[index] = new Timer { Growing = false, Start = 0f, End = end };
            else timers.Remove(index);
            flags[index] |= Flag.Changed;
        }

        /// <summary>A live node became solid or stopped being (a rock used up disappears; a stump stays).</summary>
        internal void SetBlocks(int index, bool blocks)
        {
            bool was = (flags[index] & Flag.Blocks) != 0;
            if (was == blocks) return;
            if (blocks) flags[index] |= Flag.Blocks;
            else flags[index] &= ~Flag.Blocks;
            BlockingChanged?.Invoke(CellOf(index));
        }

        /// <summary>Whether a cell has a node at all (for the streamer).</summary>
        public bool Has(int index) => kind[index] != 0;
        public ResourceNodeConfig KindOf(int index) => kind[index] != 0 ? kinds[kind[index] - 1] : null;

        // ------------------------------------------------------------ saving --

        /// <summary>One cell that isn't as the map made it.</summary>
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

        /// <summary>Every cell that isn't as the map made it (live objects' latest state included).</summary>
        public List<Change> Changes()
        {
            foreach (var pair in live)
                if (pair.Value != null) pair.Value.WriteBack();
            float now = Time.time;
            var changes = new List<Change>();
            for (int i = 0; i < flags.Length; i++)
            {
                if ((flags[i] & Flag.Changed) == 0) continue;
                Settle(i);
                var c = new Change { Cell = CellOf(i), Kind = KindOf(i), Planted = IsPlanted(i), Variant = variant[i], Left = left[i], Grown = -1f, RegrowLeft = -1f };
                if (timers.TryGetValue(i, out var t))
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
            return changes;
        }

        /// <summary>Puts back one change from a save (before any of that part of the map is shown).</summary>
        public void Apply(Change c)
        {
            if (!Inside(c.Cell)) return;
            int i = Index(c.Cell);
            if (c.Kind == null)
            {
                Clear(c.Cell);
                return;
            }
            if (live.ContainsKey(i)) Clear(c.Cell);
            kind[i] = KindIndex(c.Kind);
            variant[i] = (byte)Mathf.Clamp(c.Variant, 0, 255);
            left[i] = (short)Mathf.Clamp(c.Left, 0, short.MaxValue);
            flags[i] = Flag.Changed | (c.Planted ? Flag.Planted : Flag.None) | (flags[i] & Flag.Blocks);
            timers.Remove(i);
            float now = Time.time;
            bool blocks;
            if (c.Grown >= 0f && c.Grown < 1f)
            {
                float total = Mathf.Max(1f, c.GrowSeconds);
                float start = now - c.Grown * total;
                timers[i] = new Timer { Growing = true, Start = start, End = start + total };
                blocks = true;
            }
            else
            {
                if (c.Left <= 0 && c.RegrowLeft >= 0f) timers[i] = new Timer { Growing = false, End = now + c.RegrowLeft };
                // Used up: a stump or bare bush stays in the way; a rock is gone.
                blocks = c.Left > 0 || c.Kind.look != NodeLook.Rock;
            }
            SetBlocks(i, blocks);
            Planted?.Invoke(c.Cell);
        }
    }
}
