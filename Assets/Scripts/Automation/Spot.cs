using System;

namespace KingdomGame.Automation
{
    /// <summary>A grid cell (plain C#, so the factory can be unit-tested without Unity).</summary>
    public readonly struct Spot : IEquatable<Spot>
    {
        public readonly int X, Y;

        public Spot(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Spot operator +(Spot a, Spot b) => new Spot(a.X + b.X, a.Y + b.Y);
        public static Spot operator -(Spot a, Spot b) => new Spot(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Spot a, Spot b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Spot a, Spot b) => !(a == b);
        public bool Equals(Spot other) => this == other;
        public override bool Equals(object obj) => obj is Spot s && this == s;
        public override int GetHashCode() => unchecked(X * 73856093 ^ Y * 19349663);
        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>
    /// The four ways a factory piece can face, like a building's rotation: 0 right, 1 up, 2 left, 3 down
    /// (quarter turns anticlockwise).
    /// </summary>
    public static class Dirs
    {
        static readonly Spot[] Steps = { new Spot(1, 0), new Spot(0, 1), new Spot(-1, 0), new Spot(0, -1) };

        public static int Wrap(int dir) => ((dir % 4) + 4) % 4;
        /// <summary>One cell in direction <paramref name="dir"/>.</summary>
        public static Spot Step(int dir) => Steps[Wrap(dir)];
        public static int Opposite(int dir) => Wrap(dir + 2);
        /// <summary>The direction from one cell to a cell next to it (-1 if they don't touch side to side).</summary>
        public static int Between(Spot from, Spot to)
        {
            var d = to - from;
            for (int i = 0; i < 4; i++) if (Steps[i] == d) return i;
            return -1;
        }
    }
}
