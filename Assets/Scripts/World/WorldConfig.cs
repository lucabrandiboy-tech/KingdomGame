using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>How the map is generated. A new random map every play unless Seed is set.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/World/World Config", fileName = "WorldConfig")]
    public sealed class WorldConfig : ScriptableObject
    {
        [Tooltip("Map width and height in tiles. The ruler starts in the middle. Big maps take longer to start and use more memory (the ground's patches get a little less detailed past 128).")]
        [Range(32, 512)] public int mapSize = 256;
        [Tooltip("0 = a new random map every time. Any other number always makes the same map.")]
        public int seed = 0;

        [Header("Nature")]
        [Tooltip("Share of the map covered by forest (0-1).")]
        [Range(0, 1)] public float forestCover = 0.35f;
        [Tooltip("How packed trees are inside a forest (0-1).")]
        [Range(0, 1)] public float treeDensity = 0.3f;
        [Tooltip("Chance of a rock on any open tile.")]
        [Range(0, 0.2f)] public float rockChance = 0.012f;
        [Tooltip("Chance of a berry bush on any tile near a forest's edge.")]
        [Range(0, 0.5f)] public float bushChance = 0.05f;
        [Tooltip("Tiles around the start kept clear of trees and rocks.")]
        [Min(0)] public int clearRadius = 7;

        [Header("Test outpost")]
        [Tooltip("Stone and wood walls near the start, for trying out guns and explosives.")]
        public bool testOutpost = true;

        [Header("Saving")]
        [Tooltip("Minutes between autosaves (0 = only when you save, leave or quit). The game also saves when you go to the main menu or quit.")]
        [Min(0)] public float autosaveMinutes = 3f;
    }
}
