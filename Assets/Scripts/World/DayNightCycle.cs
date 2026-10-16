using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KingdomGame.World
{
    /// <summary>
    /// Moves the clock forward and lights the world: the global 2D light changes
    /// color and brightness through dawn, day, dusk and night, and lights like
    /// the ruler's torch fade in when it gets dark.
    /// </summary>
    public sealed class DayNightCycle : MonoBehaviour
    {
        [SerializeField] DayNightConfig config;
        [SerializeField] Light2D sun;

        readonly List<Light2D> nightLights = new List<Light2D>();
        float hour;
        bool timeSet;

        public DayNightConfig Config => config;
        /// <summary>Day number, starting at 1.</summary>
        public int Day { get; private set; } = 1;
        /// <summary>Hour of the day, 0 to 24.</summary>
        public float Hour => hour;
        /// <summary>1 in full daylight, 0 at night.</summary>
        public float Daylight { get; private set; } = 1f;
        public bool IsNight => Daylight < 0.25f;
        /// <summary>Hours the day is longer at each end (the seasons: more in summer, less, below 0, in winter).</summary>
        public float DayLengthShift { get; set; }
        /// <summary>How much clouds darken the day, 0..1 (rain, storms, fog).</summary>
        public float Overcast { get; set; }
        /// <summary>A lightning flash lighting everything, 0..1 (fades by itself).</summary>
        public float Flash { get; set; }
        /// <summary>When the sun rises and sets today (the season moves them).</summary>
        public float Sunrise => config != null ? config.sunrise - DayLengthShift : 6f;
        public float Sunset => config != null ? config.sunset + DayLengthShift : 19.5f;

        public void Setup(DayNightConfig dayNightConfig, Light2D globalLight)
        {
            config = dayNightConfig;
            sun = globalLight;
        }

        /// <summary>A light that switches on as it gets dark (its color, radius and brightness come from the config).</summary>
        public void AddNightLight(Light2D light)
        {
            if (light == null) return;
            FireLight.UseNormalMaps(light, 1f);
            nightLights.Add(light);
            Apply();
        }

        void Start()
        {
            if (config == null) config = ScriptableObject.CreateInstance<DayNightConfig>();
            if (!timeSet) hour = config.startHour;
            Apply();
        }

        /// <summary>Real seconds until it's night (<see cref="IsNight"/>), 0 if it is.</summary>
        public float SecondsUntilNight()
        {
            if (config == null || IsNight) return 0f;
            // Daylight drops below a quarter a little after sunset (see Apply).
            float nightStarts = Sunset + 0.174f * config.twilightHours;
            float hours = Mathf.Repeat(nightStarts - hour, 24f);
            return hours * config.minutesPerDay * 60f / 24f;
        }

        /// <summary>Sets the day and the time (loading a saved game).</summary>
        public void SetTime(int day, float hourOfDay)
        {
            timeSet = true;
            Day = Mathf.Max(1, day);
            hour = Mathf.Repeat(hourOfDay, 24f);
            Apply();
        }

        void Update()
        {
            if (config == null) return;
            hour += Time.deltaTime * 24f / (config.minutesPerDay * 60f);
            if (hour >= 24f)
            {
                hour -= 24f;
                Day++;
            }
            Flash = Mathf.Max(0f, Flash - Time.deltaTime * 5f);
            Apply();
        }

        void Apply()
        {
            if (config == null) return;
            float t = config.twilightHours;
            float dawn = Mathf.Clamp01((hour - Sunrise) / t + 0.5f);
            float dusk = Mathf.Clamp01((Sunset - hour) / t + 0.5f);
            Daylight = Smooth(Mathf.Min(dawn, dusk));

            if (sun != null)
            {
                // Night -> warm twilight -> day.
                var color = Daylight < 0.5f
                    ? Color.Lerp(config.nightColor, config.duskColor, Daylight * 2f)
                    : Color.Lerp(config.duskColor, config.dayColor, (Daylight - 0.5f) * 2f);
                var light = color * Mathf.Lerp(config.nightBrightness, config.dayBrightness, Daylight);
                // Clouds dull and grey the day; lightning lights everything up for a moment.
                float cloud = Mathf.Clamp01(Overcast);
                if (cloud > 0f) light = Color.Lerp(light, new Color(0.55f, 0.58f, 0.64f) * light.maxColorComponent, cloud * 0.5f) * (1f - cloud);
                if (Flash > 0f) light = Color.Lerp(light, new Color(0.9f, 0.93f, 1f) * Mathf.Max(1f, config.dayBrightness), Mathf.Clamp01(Flash));
                light.a = 1f;
                // The config's colors and brightness are as they look on screen. URP uses the
                // global 2D light's color as a linear light value without converting it (the
                // other lights' colors are converted), so convert it here; otherwise night
                // would look far brighter than set.
                sun.color = QualitySettings.activeColorSpace == ColorSpace.Linear ? light.linear : light;
                sun.intensity = 1f;
            }

            float dark = 1f - Daylight;
            FireLight.Darkness = dark;
            FireLight.DaytimeShare = config.fireDaytimeShare;
            foreach (var light in nightLights)
            {
                if (light == null) continue;
                light.color = config.torchColor;
                light.intensity = config.torchBrightness * Smooth(Mathf.Clamp01(dark * 1.5f - 0.2f));
                light.pointLightOuterRadius = config.torchRadius;
                light.pointLightInnerRadius = config.torchRadius * 0.25f;
                light.enabled = light.intensity > 0.01f;
            }
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        /// <summary>The time as text, like "Day 2  19:30".</summary>
        public string ClockText()
        {
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f / 10f) * 10;
            return $"Day {Day}  {h:00}:{m:00}";
        }
    }
}
