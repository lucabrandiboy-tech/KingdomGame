namespace KingdomGame.Economy
{
    /// <summary>
    /// The clock plants grow by (planted trees and bushes, berries and planted trees growing back): the game's own
    /// clock, but slower or stopped by the season (nothing grows in winter) and faster in the rain. The seasons move
    /// it on (<see cref="Advance"/>); with no seasons it's the game's clock.
    /// </summary>
    public static class NatureClock
    {
        // How far behind the game's clock it is (seconds): it grows while plants grow slower than normal.
        static float lag;

        /// <summary>Now, in seconds, on the plants' clock (compare growing and regrowing times with this, not Time.time).</summary>
        public static float Now => UnityEngine.Time.time - lag;

        /// <summary><paramref name="seconds"/> of the game went by, with plants growing at <paramref name="rate"/> (1 normal, 0 not at all).</summary>
        public static void Advance(float seconds, float rate)
        {
            if (seconds <= 0f) return;
            if (rate < 0f) rate = 0f;
            lag += seconds * (1f - rate);
        }
    }
}
