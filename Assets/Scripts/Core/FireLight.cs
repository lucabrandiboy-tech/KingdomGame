using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KingdomGame
{
    /// <summary>
    /// A warm flickering light from a fire (brazier, torch, lantern). It's always
    /// lit, but much stronger at night: <see cref="Darkness"/> (set by the day and
    /// night cycle) scales it. Its light uses the sprites' normal maps, so it shows
    /// the bumps of walls, ground and bark.
    /// </summary>
    public sealed class FireLight : MonoBehaviour
    {
        /// <summary>0 in full daylight, 1 at night. Set by the day and night cycle.</summary>
        public static float Darkness { get; set; }
        /// <summary>How strong fires are in daylight compared to night (0..1).</summary>
        public static float DaytimeShare { get; set; } = 0.35f;
        /// <summary>Color, radius (tiles) and brightness of a standard fire (from the Day Night Config).</summary>
        public static Color DefaultColor { get; private set; } = new Color(1f, 0.68f, 0.38f);
        public static float DefaultRadius { get; private set; } = 5f;
        public static float DefaultIntensity { get; private set; } = 1.3f;

        public static void Configure(Color color, float radius, float intensity, float daytimeShare)
        {
            DefaultColor = color;
            DefaultRadius = radius;
            DefaultIntensity = intensity;
            DaytimeShare = daytimeShare;
        }

        /// <summary>A standard fire, <paramref name="size"/> times as big and bright.</summary>
        public static FireLight Create(Transform parent, Vector2 localPosition, float size = 1f) =>
            Create(parent, localPosition, DefaultColor, DefaultRadius * size, DefaultIntensity * Mathf.Sqrt(size));

        Light2D fireLight;
        float baseIntensity;
        float seed;

        /// <summary>Makes a fire light as a child of <paramref name="parent"/>.</summary>
        public static FireLight Create(Transform parent, Vector2 localPosition, Color color, float radius, float intensity)
        {
            var go = new GameObject("Fire light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.pointLightInnerRadius = radius * 0.12f;
            light.pointLightOuterRadius = radius;
            UseNormalMaps(light, 1f);
            var fire = go.AddComponent<FireLight>();
            fire.fireLight = light;
            fire.baseIntensity = intensity;
            fire.seed = Random.value * 100f;
            return fire;
        }

        /// <summary>
        /// Switches a 2D light's normal-map lighting on. URP only lets you set this in
        /// the Inspector (the property has no setter), so this sets the saved field
        /// directly. If a future URP renames it, lights simply light without normal maps.
        /// </summary>
        public static void UseNormalMaps(Light2D light, float distance)
        {
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var quality = typeof(Light2D).GetField("m_NormalMapQuality", flags);
                var height = typeof(Light2D).GetField("m_NormalMapDistance", flags);
                if (quality == null || height == null) return;
                quality.SetValue(light, System.Enum.ToObject(quality.FieldType, 1));   // Accurate
                height.SetValue(light, distance);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Couldn't switch on normal maps for 2D lights ({e.Message}). Everything still works, just flatter.");
            }
        }

        void Update()
        {
            float t = Time.time * 6f + seed;
            float flicker = 0.82f + 0.18f * Mathf.PerlinNoise(t, seed) + 0.05f * Mathf.Sin(t * 3.1f);
            float share = Mathf.Lerp(DaytimeShare, 1f, Mathf.Clamp01(Darkness));
            fireLight.intensity = baseIntensity * share * flicker;
        }
    }
}
