using System;
using KingdomGame.World;

// Tests for the seasons and the weather.
static partial class Tests
{
    static float[][] Chances() => new[]
    {
        new[] { 50f, 35f, 0f, 15f, 0f },
        new[] { 60f, 15f, 18f, 7f, 0f },
        new[] { 40f, 30f, 5f, 25f, 0f },
        new[] { 35f, 0f, 0f, 20f, 45f },
    };

    static void SeasonsGoRoundTheYear()
    {
        Check(SeasonMath.SeasonOf(1, 3) == Season.Spring, "day 1 is spring");
        Check(SeasonMath.SeasonOf(3, 3) == Season.Spring, "day 3 is still spring");
        Check(SeasonMath.SeasonOf(4, 3) == Season.Summer, "day 4 is summer");
        Check(SeasonMath.SeasonOf(10, 3) == Season.Winter, "day 10 is winter");
        Check(SeasonMath.SeasonOf(13, 3) == Season.Spring, "day 13 is spring again");
        Check(SeasonMath.DaysLeft(1, 3) == 2 && SeasonMath.DaysLeft(3, 3) == 0, "days left of the season");
        Check(SeasonMath.SeasonOf(0, 3) == Season.Spring && SeasonMath.SeasonOf(5, 0) == Season.Spring, "odd inputs don't break it");
    }

    static void DaysAreLongestInSummer()
    {
        float summer = SeasonMath.DayLengthShift(5, 12f, 3, 1.5f);   // the middle of summer
        float winter = SeasonMath.DayLengthShift(11, 12f, 3, 1.5f);  // the middle of winter
        float spring = SeasonMath.DayLengthShift(2, 12f, 3, 1.5f);
        Check(summer > 1.4f, $"midsummer days are longest, got {summer}");
        Check(winter < -1.4f, $"midwinter days are shortest, got {winter}");
        Check(Math.Abs(spring) < 0.3f, $"mid spring is about normal, got {spring}");
        // Never a jump from one hour to the next.
        float last = SeasonMath.DayLengthShift(1, 0f, 3, 1.5f);
        for (int h = 1; h < 24 * 12 * 2; h++)
        {
            float now = SeasonMath.DayLengthShift(1 + h / 24, h % 24, 3, 1.5f);
            Check(Math.Abs(now - last) < 0.05f, $"day length jumped at hour {h}");
            last = now;
        }
    }

    static void SnowLiesOnlyInWinter()
    {
        Check(SeasonMath.SnowCover(5, 12f, 3) == 0f, "no snow in summer");
        Check(SeasonMath.SnowCover(10, 0.5f, 3) < 0.1f, "it starts settling as winter comes");
        Check(SeasonMath.SnowCover(11, 12f, 3) == 1f, "deep in winter it lies");
        Check(SeasonMath.SnowCover(1, 12f, 3) == 0f, "no snow in the very first spring");
        float melting = SeasonMath.SnowCover(13, 6f, 3);
        Check(melting > 0f && melting < 1f, $"it melts in the next spring, got {melting}");
        Check(SeasonMath.SnowCover(14, 12f, 3) == 0f, "and is gone after a day");
    }

    static void WeatherIsTheSameEverywhereAndFitsTheSeason()
    {
        var chances = Chances();
        int[] counts = new int[SeasonMath.WeatherCount];
        bool snowOutOfWinter = false, rainInWinter = false;
        for (int day = 1; day <= 120; day++)
            for (float hour = 0f; hour < 24f; hour += 0.5f)
            {
                var a = SeasonMath.WeatherAt(1234, day, hour, 3, chances, 4f, out float sa);
                var b = SeasonMath.WeatherAt(1234, day, hour, 3, chances, 4f, out float sb);
                Check(a == b && sa == sb, "the same moment gives the same weather");
                Check(sa >= 0f && sa <= 1f, $"strength out of range: {sa}");
                Check(a != Weather.Clear || sa == 0f, "clear weather has no strength");
                var season = SeasonMath.SeasonOf(day, 3);
                if (a == Weather.Snow && season != Season.Winter) snowOutOfWinter = true;
                if ((a == Weather.Rain || a == Weather.Storm) && season == Season.Winter) rainInWinter = true;
                counts[(int)a]++;
            }
        Check(!snowOutOfWinter, "snow only falls in winter");
        Check(!rainInWinter, "it doesn't rain in winter (it snows)");
        for (int w = 0; w < counts.Length; w++) Check(counts[w] > 0, $"{(Weather)w} never came in 120 days");
        Check(counts[(int)Weather.Clear] > counts[(int)Weather.Rain], "clear skies are the most common");

        // Another seed: other weather.
        int same = 0, total = 0;
        for (int day = 1; day <= 40; day++)
            for (float hour = 0f; hour < 24f; hour += 4f)
            {
                total++;
                if (SeasonMath.WeatherAt(1, day, hour, 3, chances, 4f, out _) == SeasonMath.WeatherAt(2, day, hour, 3, chances, 4f, out _)) same++;
            }
        Check(same < total, "a different world has different weather");
    }

    static void WeatherFadesInAndOut()
    {
        var chances = Chances();
        // Walk through time in small steps: strength never jumps.
        float last = -1f;
        var lastWeather = Weather.Clear;
        for (int step = 0; step < 24 * 60 * 6; step++)
        {
            float hours = step / 60f;   // a game minute at a time
            int day = 1 + (int)(hours / 24f);
            var w = SeasonMath.WeatherAt(77, day, hours % 24f, 3, chances, 4f, out float s);
            if (last >= 0f && w == lastWeather) Check(Math.Abs(s - last) < 0.05f, $"strength jumped at {hours:0.00} h ({last} -> {s})");
            if (last >= 0f && w != lastWeather) Check(s < 0.05f && last < 0.05f, $"the weather changed while strong at {hours:0.00} h ({lastWeather} {last} -> {w} {s})");
            last = s;
            lastWeather = w;
        }
    }

    static void NothingGrowsInWinter()
    {
        var growth = new[] { 1.25f, 1f, 0.75f, 0f };
        Check(SeasonMath.GrowthRate(Season.Winter, Weather.Snow, 1f, growth, 0.5f) == 0f, "nothing grows in winter");
        Check(Math.Abs(SeasonMath.GrowthRate(Season.Summer, Weather.Clear, 0f, growth, 0.5f) - 1f) < 1e-6f, "summer: normal");
        Check(Math.Abs(SeasonMath.GrowthRate(Season.Summer, Weather.Rain, 1f, growth, 0.5f) - 1.5f) < 1e-6f, "heavy rain: half as fast again");
        Check(SeasonMath.GrowthRate(Season.Spring, Weather.Clear, 0f, growth, 0.5f) > 1f, "spring: faster");
    }
}
