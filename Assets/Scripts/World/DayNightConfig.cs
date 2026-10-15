using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>How long days and nights last and how they look.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/World/Day Night Config", fileName = "DayNightConfig")]
    public sealed class DayNightConfig : ScriptableObject
    {
        [Tooltip("Real minutes for one whole day and night.")]
        [Min(0.5f)] public float minutesPerDay = 12f;
        [Tooltip("Hour of the day when the game starts (0-24). Late afternoon: dusk comes after a few minutes.")]
        [Range(0, 24)] public float startHour = 17f;
        [Range(0, 24)] public float sunrise = 6f;
        [Range(0, 24)] public float sunset = 19.5f;
        [Tooltip("Hours that dawn and dusk take.")]
        [Min(0.1f)] public float twilightHours = 2f;

        [Header("Light (colors and brightness as they look on screen)")]
        public Color dayColor = new Color(0.96f, 0.91f, 0.8f);
        public Color duskColor = new Color(1f, 0.62f, 0.42f);
        public Color nightColor = new Color(0.52f, 0.64f, 0.95f);
        [Range(0, 2)] public float dayBrightness = 0.8f;
        [Tooltip("0 = pitch black at night: only fires, lamps and the ruler's own light show anything.")]
        [Range(0, 2)] public float nightBrightness = 0f;

        [Header("The ruler's light at night")]
        public Color torchColor = new Color(1f, 0.82f, 0.55f);
        [Range(0, 3)] public float torchBrightness = 1.1f;
        [Tooltip("Radius in tiles.")]
        [Min(0.5f)] public float torchRadius = 5.5f;

        [Header("Fires (braziers, torches, lanterns)")]
        public Color fireColor = new Color(1f, 0.68f, 0.38f);
        [Range(0, 4)] public float fireBrightness = 1.3f;
        [Tooltip("Radius in tiles.")]
        [Min(0.5f)] public float fireRadius = 10f;
        [Tooltip("How strong fires are in daylight compared to night (they're always burning).")]
        [Range(0, 1)] public float fireDaytimeShare = 0.35f;
    }
}
