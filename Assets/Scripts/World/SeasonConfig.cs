using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>How likely each weather is in a season (they're shares of the whole: they needn't add up to anything).</summary>
    [System.Serializable]
    public struct WeatherChances
    {
        public float clear, rain, storm, fog, snow;

        public WeatherChances(float clearSky, float rainy, float stormy, float foggy, float snowy)
        {
            clear = clearSky; rain = rainy; storm = stormy; fog = foggy; snow = snowy;
        }

        /// <summary>In <see cref="Weather"/> order.</summary>
        public float[] ToArray() => new[] { clear, rain, storm, fog, snow };
    }

    /// <summary>The seasons and the weather: how long a season is and what each one does.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/World/Season Config", fileName = "SeasonConfig")]
    public sealed class SeasonConfig : ScriptableObject
    {
        [Tooltip("Off: always summer, always clear.")]
        public bool seasons = true;
        [Tooltip("Game days each season lasts (a day is the Day Night Config's Minutes Per Day of real time).")]
        [Min(1)] public int daysPerSeason = 3;
        [Tooltip("Hours longer the day is at each end at midsummer (and shorter at midwinter: longer nights).")]
        [Range(0, 4)] public float dayLengthShift = 1.5f;

        [Header("Plants (planted trees, berries growing back)")]
        [Tooltip("How fast plants grow in spring, summer, autumn and winter (1 = normal, 0 = not at all).")]
        public float[] growth = { 1.25f, 1f, 0.75f, 0f };
        [Tooltip("Rain makes them grow this much faster (0.5 = 50%, in heavy rain).")]
        [Min(0)] public float rainGrowth = 0.5f;

        [Header("Winter")]
        [Tooltip("Firewood each villager burns a day in winter, taken from storage. Without it they're cold.")]
        [Min(0)] public float firewoodPerVillager = 2f;
        [Tooltip("How fast cold villagers work and walk (1 = normal).")]
        [Range(0.1f, 1f)] public float coldSpeed = 0.75f;
        [Tooltip("How fast everyone walks in deep snow (the ruler, villagers and zombies).")]
        [Range(0.1f, 1f)] public float snowSpeed = 0.88f;

        [Header("Weather")]
        [Tooltip("Game hours each spell of weather lasts.")]
        [Min(0.5f)] public float spellHours = 4f;
        public WeatherChances spring = new WeatherChances(50f, 35f, 0f, 15f, 0f);
        public WeatherChances summer = new WeatherChances(60f, 15f, 18f, 7f, 0f);
        public WeatherChances autumn = new WeatherChances(40f, 30f, 5f, 25f, 0f);
        public WeatherChances winter = new WeatherChances(35f, 0f, 0f, 20f, 45f);
        [Tooltip("In fog, towers see (and shoot) only this share of their range.")]
        [Range(0.2f, 1f)] public float fogTowerRange = 0.65f;
        [Tooltip("Lightning strikes a minute in a heavy storm, about (a strike can set wood on fire).")]
        [Min(0)] public float lightningPerMinute = 3f;
        [Tooltip("How much darker cloudy weather makes the day (0.3 = 30% darker in heavy rain).")]
        [Range(0, 0.8f)] public float overcast = 0.3f;

        /// <summary>[season][weather] chances for <see cref="SeasonMath.WeatherAt"/>.</summary>
        public float[][] Chances() => new[] { spring.ToArray(), summer.ToArray(), autumn.ToArray(), winter.ToArray() };
    }
}
