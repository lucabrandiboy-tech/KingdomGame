using System;
using System.Collections.Generic;

namespace KingdomGame.Zombies
{
    /// <summary>
    /// How survival waves grow: how many zombies come, how tough they are, and
    /// which kinds. Plain C# with no Unity code, so it's unit-tested (tools/tests).
    /// </summary>
    public static class WaveMath
    {
        /// <summary>
        /// How many zombies wave number <paramref name="wave"/> (1, 2, 3...) sends:
        /// <paramref name="first"/> in the first wave, <paramref name="extraPerWave"/>
        /// more each wave after, and all of that times <paramref name="growth"/> per
        /// wave, so late waves keep getting harder without end.
        /// </summary>
        public static int Count(int wave, int first, int extraPerWave, float growth)
        {
            int n = Math.Max(1, wave) - 1;
            double count = (first + (double)extraPerWave * n) * Math.Pow(Math.Max(1f, growth), n);
            return (int)Math.Min(int.MaxValue / 2, Math.Round(count));
        }

        /// <summary>Zombies' health multiplier in wave <paramref name="wave"/>: 1 in the first, plus <paramref name="perWave"/> each wave.</summary>
        public static float Toughness(int wave, float perWave) => 1f + Math.Max(0f, perWave) * (Math.Max(1, wave) - 1);

        /// <summary>
        /// How likely each kind of zombie is in wave <paramref name="wave"/>: its weight,
        /// from its first wave on, growing to full weight over three waves so a new
        /// kind starts with a few. Kinds whose first wave hasn't come yet are 0.
        /// </summary>
        public static float Weight(int wave, int firstWave, float weight)
        {
            if (wave < firstWave || weight <= 0f) return 0f;
            return weight * Math.Min(1f, (wave - firstWave + 1) / 3f);
        }

        /// <summary>
        /// Picks a kind of zombie for wave <paramref name="wave"/>, given each kind's first
        /// wave and weight and a random number from 0 up to (not including) 1. Returns -1
        /// if no kind can come yet.
        /// </summary>
        public static int Pick(int wave, IReadOnlyList<int> firstWaves, IReadOnlyList<float> weights, double roll)
        {
            float total = 0f;
            for (int i = 0; i < firstWaves.Count; i++) total += Weight(wave, firstWaves[i], weights[i]);
            if (total <= 0f) return -1;
            double target = Math.Max(0.0, Math.Min(0.999999, roll)) * total;
            int last = -1;
            for (int i = 0; i < firstWaves.Count; i++)
            {
                float w = Weight(wave, firstWaves[i], weights[i]);
                if (w <= 0f) continue;
                last = i;
                if (target < w) return i;
                target -= w;
            }
            return last;
        }
    }
}
