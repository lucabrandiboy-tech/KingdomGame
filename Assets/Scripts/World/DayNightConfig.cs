using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>How long days and nights last and how they look.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/World/Day Night Config", fileName = "DayNightConfig")]
    public sealed class DayNightConfig : ScriptableObject
    {
        [Tooltip("Real minutes for one whole day and night.")]
        [Min(0.5f)] public float minutesPerDay = 12f;
        [Tooltip("Hour of the day when the game starts (0-24).")]
        [Range(0, 24)] public float startHour = 7f;
        [Range(0, 24)] public float sunrise = 6f;
        [Range(0, 24)] public float sunset = 19.5f;
        [Tooltip("Hours that dawn and dusk take.")]
        [Min(0.1f)] public float twilightHours = 1.5f;

        [Header("Light")]
        public Color dayColor = new Color(1f, 0.97f, 0.92f);
        public Color duskColor = new Color(1f, 0.68f, 0.45f);
        public Color nightColor = new Color(0.34f, 0.42f, 0.72f);
        [Range(0, 2)] public float dayBrightness = 1f;
        [Range(0, 2)] public float nightBrightness = 0.2f;

        [Header("The ruler's light at night")]
        public Color torchColor = new Color(1f, 0.82f, 0.55f);
        [Range(0, 3)] public float torchBrightness = 1.1f;
        [Tooltip("Radius in tiles.")]
        [Min(0.5f)] public float torchRadius = 4f;
    }
}
