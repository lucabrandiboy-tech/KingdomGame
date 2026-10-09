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

        public DayNightConfig Config => config;
        /// <summary>Day number, starting at 1.</summary>
        public int Day { get; private set; } = 1;
        /// <summary>Hour of the day, 0 to 24.</summary>
        public float Hour => hour;
        /// <summary>1 in full daylight, 0 at night.</summary>
        public float Daylight { get; private set; } = 1f;
        public bool IsNight => Daylight < 0.25f;

        public void Setup(DayNightConfig dayNightConfig, Light2D globalLight)
        {
            config = dayNightConfig;
            sun = globalLight;
        }

        /// <summary>A light that switches on as it gets dark (its color, radius and brightness come from the config).</summary>
        public void AddNightLight(Light2D light)
        {
            if (light == null) return;
            nightLights.Add(light);
            Apply();
        }

        void Start()
        {
            if (config == null) config = ScriptableObject.CreateInstance<DayNightConfig>();
            hour = config.startHour;
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
            Apply();
        }

        void Apply()
        {
            if (config == null) return;
            float t = config.twilightHours;
            float dawn = Mathf.Clamp01((hour - config.sunrise) / t + 0.5f);
            float dusk = Mathf.Clamp01((config.sunset - hour) / t + 0.5f);
            Daylight = Smooth(Mathf.Min(dawn, dusk));

            if (sun != null)
            {
                // Night -> warm twilight -> day.
                var color = Daylight < 0.5f
                    ? Color.Lerp(config.nightColor, config.duskColor, Daylight * 2f)
                    : Color.Lerp(config.duskColor, config.dayColor, (Daylight - 0.5f) * 2f);
                sun.color = color;
                sun.intensity = Mathf.Lerp(config.nightBrightness, config.dayBrightness, Daylight);
            }

            float dark = 1f - Daylight;
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
