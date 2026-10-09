using UnityEngine;

namespace KingdomGame
{
    /// <summary>
    /// Top-down drawing order: things lower on the screen are in front. Trees,
    /// rocks, the ruler and (later) villagers and zombies all use this, so you
    /// walk behind a tree's leaves but in front of its trunk. Walls are drawn
    /// below everything that stands (order 0); flying pixels above (20000).
    /// </summary>
    public static class YSort
    {
        public const int Center = 5000;

        /// <summary>Drawing order for something whose feet are at height <paramref name="y"/> (in tiles).</summary>
        public static int Order(float y) => Mathf.Clamp(Center - Mathf.RoundToInt(y * 16f), 1000, 9000);
    }
}
