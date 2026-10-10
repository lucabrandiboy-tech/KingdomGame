using System;
using System.Collections.Generic;

namespace KingdomGame.Networking
{
    /// <summary>
    /// Picks where each new player's kingdom goes in the world: at least <see cref="Spacing"/>
    /// tiles from every other kingdom, on a spot the game says is good (<see cref="IsGood"/>:
    /// open ground). The first goes near the middle; later ones go as close to the others as
    /// the spacing allows (it looks in a square round the kingdoms there are, a little bigger
    /// than them), so neighbours aren't miles apart. The world is endless up to its edge
    /// (<see cref="Limit"/> tiles from the middle), so there's always room further out.
    /// </summary>
    public sealed class KingdomPlanner
    {
        /// <summary>The furthest from the middle the first kingdom looks for open ground.</summary>
        const int FirstSearch = 4096;

        readonly int limit, margin;
        readonly Random random;
        readonly List<(int X, int Y)> spots = new List<(int X, int Y)>();

        /// <summary>Tiles between kingdoms (throne to throne).</summary>
        public int Spacing { get; }
        /// <summary>Tiles from the middle to the world's edge.</summary>
        public int Limit => limit;
        /// <summary>Whether a throne can go at a cell (open ground near it). Null: anywhere.</summary>
        public Func<int, int, bool> IsGood { get; set; }
        public IReadOnlyList<(int X, int Y)> Spots => spots;

        /// <summary>For a world that goes <paramref name="worldLimit"/> tiles from 0,0 each way.</summary>
        public KingdomPlanner(int worldLimit, int spacing, int seed)
        {
            limit = Math.Max(64, worldLimit);
            Spacing = Math.Max(8, spacing);
            margin = Math.Max(32, Spacing);
            random = new Random(seed);
        }

        /// <summary>A spot that's already taken (a kingdom from before).</summary>
        public void Add(int x, int y) => spots.Add((x, y));

        /// <summary>Finds and takes the next spot.</summary>
        public (int X, int Y) Next()
        {
            var spot = Find();
            spots.Add(spot);
            return spot;
        }

        (int X, int Y) Find()
        {
            int edge = limit - margin;
            if (spots.Count == 0)
            {
                // The middle, or the nearest good spot to it.
                for (int r = 0; r <= Math.Min(FirstSearch, edge); r += 2)
                    for (int i = 0; i < Math.Max(1, r * 4); i++)
                    {
                        double a = i * Math.PI * 2 / Math.Max(1, r * 4);
                        int x = (int)Math.Round(Math.Cos(a) * r), y = (int)Math.Round(Math.Sin(a) * r);
                        if (Good(x, y)) return (x, y);
                    }
                return (0, 0);
            }

            // A square round the kingdoms there are, with room for a ring of new ones round them.
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var s in spots)
            {
                x0 = Math.Min(x0, s.X); y0 = Math.Min(y0, s.Y);
                x1 = Math.Max(x1, s.X); y1 = Math.Max(y1, s.Y);
            }
            for (int grow = 2; grow <= 64; grow *= 2)
            {
                int lo = Math.Max(-edge, Math.Min(x0, y0) - Spacing * grow), hi = Math.Min(edge, Math.Max(x1, y1) + Spacing * grow);
                (int X, int Y) best = default;
                double bestNear = double.MaxValue;
                for (int attempt = 0; attempt < 600; attempt++)
                {
                    int x = random.Next(lo, hi + 1), y = random.Next(lo, hi + 1);
                    double near = Nearest(x, y);
                    if (near < Spacing || near >= bestNear || !Good(x, y)) continue;
                    bestNear = near;
                    best = (x, y);
                }
                if (bestNear < double.MaxValue) return best;
                if (lo == -edge && hi == edge) break;   // the whole world was searched
            }
            // Nowhere at all that's open and far enough from the others: anywhere far enough.
            for (int attempt = 0; attempt < 10000; attempt++)
            {
                int x = random.Next(-edge, edge + 1), y = random.Next(-edge, edge + 1);
                if (Nearest(x, y) >= Spacing) return (x, y);
            }
            return (random.Next(-edge, edge + 1), random.Next(-edge, edge + 1));
        }

        bool Good(int x, int y) => IsGood == null || IsGood(x, y);

        double Nearest(int x, int y)
        {
            double best = double.MaxValue;
            foreach (var s in spots)
            {
                double dx = s.X - x, dy = s.Y - y;
                best = Math.Min(best, Math.Sqrt(dx * dx + dy * dy));
            }
            return best;
        }
    }
}
