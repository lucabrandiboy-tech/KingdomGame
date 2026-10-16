using System;
using System.Collections.Generic;

namespace KingdomGame.Automation
{
    /// <summary>
    /// Pollution in the air, Factorio-style: machines put it out where they stand (furnaces, drills, assemblers, generators;
    /// four times as much while making Craftables); it lies in squares of <see cref="Chunk"/> tiles; a square with more
    /// than <see cref="SpreadAbove"/> gives a share to each of its four neighbours every second, so a cloud spreads out
    /// round a factory; and every square absorbs some (open ground a little, trees much more: <see cref="ExtraAbsorb"/>).
    /// A cloud stops growing where what's absorbed matches what's made. Zombie bases the cloud reaches take it in and
    /// attack, and the more there is the bigger and tougher the hordes (Building/Factory.cs, Zombies/).
    /// Plain C# (tools/tests/PollutionTests.cs).
    /// </summary>
    public sealed class PollutionMap
    {
        /// <summary>The squares pollution lies in, tiles across.</summary>
        public const int Chunk = 32;

        readonly Dictionary<long, float> amounts = new Dictionary<long, float>();
        readonly Dictionary<long, float> changes = new Dictionary<long, float>();
        readonly List<long> keys = new List<long>();

        /// <summary>The share of a square's pollution it gives each neighbour a second (while above <see cref="SpreadAbove"/>).</summary>
        public float SpreadShare = 0.02f;
        public float SpreadAbove = 15f;
        /// <summary>What any square absorbs a second.</summary>
        public float Absorb = 0.05f;
        /// <summary>What a square absorbs a second on top (its trees), or null: nothing more.</summary>
        public Func<int, int, float> ExtraAbsorb;

        /// <summary>All the pollution in the air now.</summary>
        public float Total { get; private set; }
        /// <summary>All ever made (how far the zombies have come on: their "evolution").</summary>
        public double Produced { get; private set; }
        public int ChunkCount => amounts.Count;

        static long Key(int cx, int cy) => ((long)cx << 32) | (uint)cy;
        static int KeyX(long k) => (int)(k >> 32);
        static int KeyY(long k) => (int)(uint)k;

        /// <summary>The square a world position (tiles) is in.</summary>
        public static int ChunkOf(float v) => (int)Math.Floor(v / Chunk);

        /// <summary>A machine at (<paramref name="x"/>, <paramref name="y"/>) puts out <paramref name="amount"/>.</summary>
        public void Emit(float x, float y, float amount)
        {
            if (amount <= 0f || float.IsNaN(amount)) return;
            long k = Key(ChunkOf(x), ChunkOf(y));
            amounts.TryGetValue(k, out float p);
            amounts[k] = p + amount;
            Total += amount;
            Produced += amount;
        }

        /// <summary>The pollution in square (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
        public float At(int cx, int cy) => amounts.TryGetValue(Key(cx, cy), out float p) ? p : 0f;

        /// <summary>The pollution in the square a world position is in.</summary>
        public float AtPosition(float x, float y) => At(ChunkOf(x), ChunkOf(y));

        /// <summary>Takes up to <paramref name="most"/> out of a square (a zombie base breathing it in); returns how much.</summary>
        public float Take(int cx, int cy, float most)
        {
            long k = Key(cx, cy);
            if (most <= 0f || !amounts.TryGetValue(k, out float p)) return 0f;
            float taken = Math.Min(p, most);
            if (p - taken <= 0.01f) amounts.Remove(k);
            else amounts[k] = p - taken;
            Total = Math.Max(0f, Total - taken);
            return taken;
        }

        /// <summary>Every square with pollution in it.</summary>
        public IEnumerable<(int X, int Y, float Amount)> Chunks()
        {
            foreach (var pair in amounts) yield return (KeyX(pair.Key), KeyY(pair.Key), pair.Value);
        }

        /// <summary><paramref name="seconds"/> pass: thick squares spread to their neighbours, every square absorbs some.</summary>
        public void Tick(float seconds)
        {
            if (seconds <= 0f || amounts.Count == 0) return;
            changes.Clear();
            float share = Math.Min(0.2f, SpreadShare * seconds);
            foreach (var pair in amounts)
            {
                if (pair.Value <= SpreadAbove) continue;
                float give = pair.Value * share;
                int cx = KeyX(pair.Key), cy = KeyY(pair.Key);
                Add(changes, pair.Key, -4f * give);
                Add(changes, Key(cx + 1, cy), give);
                Add(changes, Key(cx - 1, cy), give);
                Add(changes, Key(cx, cy + 1), give);
                Add(changes, Key(cx, cy - 1), give);
            }
            foreach (var c in changes)
            {
                amounts.TryGetValue(c.Key, out float p);
                amounts[c.Key] = p + c.Value;
            }
            keys.Clear();
            keys.AddRange(amounts.Keys);
            float total = 0f;
            foreach (long k in keys)
            {
                float absorb = Absorb + (ExtraAbsorb != null ? Math.Max(0f, ExtraAbsorb(KeyX(k), KeyY(k))) : 0f);
                float p = amounts[k] - absorb * seconds;
                if (p <= 0.01f)
                {
                    amounts.Remove(k);
                    continue;
                }
                amounts[k] = p;
                total += p;
            }
            Total = total;
        }

        static void Add(Dictionary<long, float> d, long k, float v)
        {
            d.TryGetValue(k, out float p);
            d[k] = p + v;
        }

        /// <summary>All the pollution in the air goes (testing); what was ever made stays.</summary>
        public void Clear()
        {
            amounts.Clear();
            Total = 0f;
        }

        /// <summary>For a save: each square's x, y and amount, and all ever made.</summary>
        public void Save(List<int> xs, List<int> ys, List<float> values, out double produced)
        {
            foreach (var pair in amounts)
            {
                xs.Add(KeyX(pair.Key));
                ys.Add(KeyY(pair.Key));
                values.Add(pair.Value);
            }
            produced = Produced;
        }

        /// <summary>Puts back a saved game's pollution.</summary>
        public void Load(IReadOnlyList<int> xs, IReadOnlyList<int> ys, IReadOnlyList<float> values, double produced)
        {
            amounts.Clear();
            Total = 0f;
            if (xs != null && ys != null && values != null)
                for (int i = 0; i < xs.Count && i < ys.Count && i < values.Count; i++)
                {
                    if (!(values[i] > 0.01f)) continue;
                    amounts[Key(xs[i], ys[i])] = values[i];
                    Total += values[i];
                }
            Produced = Math.Max(0.0, produced);
        }
    }
}
