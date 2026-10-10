using UnityEngine;

namespace KingdomGame.Zombies
{
    /// <summary>
    /// The survival game: what you start with, how the endless waves of zombies
    /// grow, how much worse night is, and who comes to join you between waves.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Zombies/Survival Config", fileName = "SurvivalConfig")]
    public sealed class SurvivalConfig : ScriptableObject
    {
        [Header("Start")]
        [Tooltip("Seconds to build before the first wave comes (N calls it early).")]
        [Min(0)] public float prepSeconds = 120f;
        [Min(0)] public int startWood = 250;
        [Min(0)] public int startStone = 200;
        [Min(0)] public int startFood = 60;

        [Header("Waves")]
        [Tooltip("The kinds of zombie that come.")]
        public ZombieConfig[] zombies = new ZombieConfig[0];
        [Min(1)] public int firstWaveZombies = 6;
        [Tooltip("How many more zombies each wave brings than the one before...")]
        [Min(0)] public int extraZombiesPerWave = 3;
        [Tooltip("...and on top of that, each wave is this many times bigger, so it never stops getting harder.")]
        [Min(1)] public float growth = 1.05f;
        [Tooltip("Zombies have this much more health each wave (0.06 = 6% more).")]
        [Min(0)] public float toughnessPerWave = 0.06f;
        [Tooltip("Seconds of calm between waves (N calls the next one early).")]
        [Min(0)] public float breakSeconds = 45f;
        [Tooltip("How many zombies come out of the dark per second while a wave arrives.")]
        [Min(0.1f)] public float spawnPerSecond = 2f;
        [Tooltip("Most zombies alive at once; the rest of the wave waits its turn. Lower it if the game slows down.")]
        [Min(1)] public int maxAlive = 120;
        [Tooltip("Zombies come from the edge of the map, at least this many tiles from you and your throne.")]
        [Min(4)] public float spawnDistance = 24f;

        [Header("Night")]
        [Tooltip("At night zombies are this many times as fast...")]
        [Min(0.1f)] public float nightSpeed = 1.2f;
        [Tooltip("...and hit this many times as hard.")]
        [Min(0.1f)] public float nightDamage = 1.25f;

        [Header("Survivors")]
        [Tooltip("Survivors who come to join you after each wave, if you have room: each bed is a home for one more villager.")]
        [Min(0)] public int survivorsPerWave = 2;
    }
}
