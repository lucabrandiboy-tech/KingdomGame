using System;

namespace KingdomGame.World
{
    /// <summary>The seasons, in order (a year goes round them).</summary>
    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>What the sky is doing. Snow only falls in winter; storms come in summer.</summary>
    public enum Weather { Clear, Rain, Storm, Fog, Snow }

    /// <summary>
    /// The seasons and the weather, worked out from the day and hour alone (and the world's seed), so every
    /// computer in an online game has the same, and a saved game needs nothing extra. Plain C# with no Unity
    /// code, so it's unit-tested (tools/tests).
    /// </summary>
    public static class SeasonMath
    {
        public const int SeasonCount = 4;
        public static readonly int WeatherCount = Enum.GetValues(typeof(Weather)).Length;

        /// <summary>The season on day <paramref name="day"/> (1 = the first day: spring).</summary>
        public static Season SeasonOf(int day, int daysPerSeason) =>
            (Season)(((Math.Max(1, day) - 1) / Math.Max(1, daysPerSeason)) % SeasonCount);

        /// <summary>Whole days left of the season after today (0 on its last day).</summary>
        public static int DaysLeft(int day, int daysPerSeason)
        {
            int per = Math.Max(1, daysPerSeason);
            return per - 1 - (Math.Max(1, day) - 1) % per;
        }

        /// <summary>How far through the year it is, 0..1 (spring starts at 0).</summary>
        public static double YearFraction(int day, float hour, int daysPerSeason) =>
            ((Math.Max(1, day) - 1) + Clamp(hour, 0f, 24f) / 24.0) / (Math.Max(1, daysPerSeason) * SeasonCount) % 1.0;

        /// <summary>
        /// Hours the day is longer than usual at each end: <paramref name="most"/> at midsummer (the sun rises that
        /// much earlier and sets that much later), none in the middle of spring and autumn, minus that at midwinter.
        /// It changes a little every day, never in jumps.
        /// </summary>
        public static float DayLengthShift(int day, float hour, int daysPerSeason, float most)
        {
            double t = YearFraction(day, hour, daysPerSeason);
            return (float)(most * Math.Cos(2.0 * Math.PI * (t - 0.375)));   // 0.375: the middle of summer
        }

        /// <summary>
        /// Snow on the ground, 0..1: none until winter, settling over its first day, lying all winter, melting
        /// over the first day of spring.
        /// </summary>
        public static float SnowCover(int day, float hour, int daysPerSeason)
        {
            int per = Math.Max(1, daysPerSeason);
            var season = SeasonOf(day, per);
            float intoSeason = (Math.Max(1, day) - 1) % per + Clamp(hour, 0f, 24f) / 24f;   // days into it
            if (season == Season.Winter) return Smooth(Clamp(intoSeason, 0f, 1f));
            if (season == Season.Spring && day > per * SeasonCount) return 1f - Smooth(Clamp(intoSeason, 0f, 1f));
            return 0f;
        }

        /// <summary>
        /// The weather at a moment. Time goes in spells of <paramref name="spellHours"/> game hours; each spell's
        /// weather is picked from its season's chances (<paramref name="chances"/>[season][weather]) by the seed, so
        /// it's the same everywhere. <paramref name="strength"/> is how heavy it is (0..1): each spell has its own
        /// (light or heavy rain), and it fades in and out where the weather changes. Snow only in winter.
        /// </summary>
        public static Weather WeatherAt(int seed, int day, float hour, int daysPerSeason, float[][] chances, float spellHours, out float strength)
        {
            double hours = (Math.Max(1, day) - 1) * 24.0 + Clamp(hour, 0f, 24f);
            double span = Math.Max(0.5f, spellHours);
            long spell = (long)Math.Floor(hours / span);
            double into = (hours - spell * span) / span;   // 0..1 through this spell
            var now = SpellWeather(seed, spell, span, daysPerSeason, chances, out float heavy);
            strength = 0f;
            if (now == Weather.Clear) return now;
            // Near the start and end: where the spell before or after is the same weather, the heaviness
            // blends into its own; where it's different, this weather fades in or out.
            const double Fade = 0.15;
            strength = heavy;
            if (into < Fade)
            {
                var before = SpellWeather(seed, spell - 1, span, daysPerSeason, chances, out float heavyBefore);
                float k = (float)(into / Fade);
                strength = before == now ? Lerp((heavyBefore + heavy) * 0.5f, heavy, k) : heavy * k;
            }
            else if (into > 1 - Fade)
            {
                var after = SpellWeather(seed, spell + 1, span, daysPerSeason, chances, out float heavyAfter);
                float k = (float)((into - (1 - Fade)) / Fade);
                strength = after == now ? Lerp(heavy, (heavy + heavyAfter) * 0.5f, k) : heavy * (1f - k);
            }
            return now;
        }

        // One spell's weather and how heavy it is, from the seed and the spell's number.
        static Weather SpellWeather(int seed, long spell, double span, int daysPerSeason, float[][] chances, out float heavy)
        {
            heavy = 0f;
            if (spell < 0) return Weather.Clear;
            int day = (int)(spell * span / 24.0) + 1;
            var season = SeasonOf(day, daysPerSeason);
            var weights = chances != null && (int)season < chances.Length ? chances[(int)season] : null;
            uint h = Hash((uint)seed, (uint)spell, 0x5EA50u);
            heavy = 0.45f + 0.55f * ((Hash(h, 7u, 1u) & 0xFFFF) / 65535f);
            if (weights == null) return Weather.Clear;
            float total = 0f;
            for (int w = 0; w < weights.Length && w < WeatherCount; w++)
                if (Allowed((Weather)w, season)) total += Math.Max(0f, weights[w]);
            if (total <= 0f) return Weather.Clear;
            float pick = (h & 0xFFFFFF) / (float)0x1000000 * total;
            for (int w = 0; w < weights.Length && w < WeatherCount; w++)
            {
                if (!Allowed((Weather)w, season)) continue;
                pick -= Math.Max(0f, weights[w]);
                if (pick < 0f) return (Weather)w;
            }
            return Weather.Clear;
        }

        /// <summary>Snow only in winter (rain and storms not then: it snows instead).</summary>
        public static bool Allowed(Weather w, Season s) =>
            w == Weather.Snow ? s == Season.Winter : (w != Weather.Rain && w != Weather.Storm) || s != Season.Winter;

        /// <summary>How fast plants grow (and berries grow back) in a season and weather: rain helps.</summary>
        public static float GrowthRate(Season s, Weather w, float strength, float[] bySeason, float rainBoost)
        {
            float rate = bySeason != null && (int)s < bySeason.Length ? Math.Max(0f, bySeason[(int)s]) : 1f;
            if (rate > 0f && (w == Weather.Rain || w == Weather.Storm)) rate *= 1f + Math.Max(0f, rainBoost) * Clamp(strength, 0f, 1f);
            return rate;
        }

        static uint Hash(uint a, uint b, uint c)
        {
            uint h = a * 0x9E3779B1u ^ b * 0x85EBCA77u ^ c * 0xC2B2AE3Du;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Clamp(float x, float lo, float hi) => x < lo ? lo : x > hi ? hi : x;
    }
}
