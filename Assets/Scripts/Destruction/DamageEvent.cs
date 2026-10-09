using System;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// Everything needed to reproduce one hit exactly. In multiplayer (Phase 5)
    /// the server sends these instead of pixel data: every computer runs the
    /// same whole-number math (<see cref="DamageShape"/>) and removes the same
    /// pixels. The flying pixels are only for looks and are made on each computer.
    /// </summary>
    [Serializable]
    public struct DamageEvent
    {
        /// <summary>World pixel that was hit: world position x 32, rounded down.</summary>
        public int PixelX, PixelY;
        /// <summary>Hole radius in pixels. 0 = the damage type's own radius.</summary>
        public ushort RadiusPx;
        /// <summary>Index of the damage type in <see cref="PixelWorld"/>'s list.</summary>
        public byte TypeIndex;
        /// <summary>Random seed for the ragged edge. Same seed, same hole.</summary>
        public uint Seed;
    }
}
