using System;
using System.Collections.Generic;

namespace KingdomGame.Networking
{
    /// <summary>
    /// Picks where each new player's kingdom goes on the map: at least <see cref="Spacing"/>
    /// tiles from every other kingdom, away from the map's edge, on a spot the game says is
    /// good (<see cref="IsGood"/>: open ground). The first goes near the middle; later ones go
    /// as close to the others as the spacing allows, so neighbours aren't across the map.
    /// If the map fills up the spacing shrinks, so everyone still gets a spot.
    /// </summary>
    public sealed class KingdomPlanner
    {
        readonly int half, margin;
        readonly Random random;
        readonly List<(int X, int Y)> spots = new List<(int X, int Y)>();

        /// <summary>Tiles between kingdoms (throne to throne).</summary>
        public int Spacing { get; }
        /// <summary>Whether a throne can go at a cell (open ground near it). Null: anywhere.</summary>
        public Func<int, int, bool> IsGood { get; set; }
        public IReadOnlyList<(int X, int Y)> Spots => spots;

        /// <summary>For a map <paramref name="mapSize"/> tiles across, centered on 0,0.</summary>
        public KingdomPlanner(int mapSize, int spacing, int seed)
        {
            half = Math.Max(8, mapSize / 2);
            Spacing = Math.Max(8, spacing);
            margin = Math.Min(Math.Max(16, Spacing / 3), half - 4);
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
            int lo = -half + margin, hi = half - margin;
            if (spots.Count == 0)
            {
                // The middle, or the nearest good spot to it.
                for (int r = 0; r <= hi; r += 2)
                    for (int i = 0; i < Math.Max(1, r * 4); i++)
                    {
                        double a = i * Math.PI * 2 / Math.Max(1, r * 4);
                        int x = (int)Math.Round(Math.Cos(a) * r), y = (int)Math.Round(Math.Sin(a) * r);
                        if (Good(x, y)) return (x, y);
                    }
                return (0, 0);
            }
            for (double spacing = Spacing; spacing >= 6; spacing *= 0.75)
            {
                (int X, int Y) best = default;
                double bestNear = double.MaxValue;
                for (int attempt = 0; attempt < 600; attempt++)
                {
                    int x = random.Next(lo, hi + 1), y = random.Next(lo, hi + 1);
                    double near = Nearest(x, y);
                    if (near < spacing || near >= bestNear || !Good(x, y)) continue;
                    bestNear = near;
                    best = (x, y);
                }
                if (bestNear < double.MaxValue) return best;
            }
            // Nowhere at all: anywhere on the map.
            return (random.Next(lo, hi + 1), random.Next(lo, hi + 1));
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
