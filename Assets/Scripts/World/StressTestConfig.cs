using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>Settings for the destruction stress test scene.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Phase 1/Stress Test Config", fileName = "StressTestConfig")]
    public sealed class StressTestConfig : ScriptableObject
    {
        [Header("Walls")]
        [Min(1)] public int rows = 10;
        [Min(1)] public int columns = 6;
        [Tooltip("Length of each wall, in tiles.")]
        [Min(1)] public int wallLength = 5;
        [Tooltip("Empty tiles between walls in a row.")]
        [Min(0)] public int gap = 1;
        [Tooltip("Tiles from one row of walls to the next.")]
        [Min(1)] public int rowSpacing = 2;
        [Tooltip("Rows take turns using these materials.")]
        public StructureMaterial[] materials = new StructureMaterial[0];

        [Header("Explosions")]
        public DamageType explosion;
        [Tooltip("Random explosions per second while the automatic test runs.")]
        [Min(0)] public float explosionsPerSecond = 15f;
        [Tooltip("Explosions per wall when you press X (everything at once).")]
        [Min(1)] public int blastAllPerWall = 2;

        [Header("Report")]
        [Tooltip("How often a summary line is written to the Console, in seconds.")]
        [Min(1)] public float reportEverySeconds = 5f;
    }
}
