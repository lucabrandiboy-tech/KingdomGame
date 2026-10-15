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

        [Header("Zombie bases")]
        [Tooltip("A new zombie base appears every this many days (at dawn), up to Max Bases.")]
        [Min(1)] public int baseEveryDays = 2;
        [Min(0)] public int maxBases = 3;
        [Tooltip("How far from the throne bases go (tiles): never near where you started.")]
        [Min(20)] public float baseMinDistance = 70f;
        [Min(20)] public float baseMaxDistance = 140f;
        [Min(1)] public float baseHealth = 1500f;
        [Tooltip("Zombies camping round each base (more come back when they're killed).")]
        [Min(0)] public int baseCampers = 6;
        [Tooltip("Seconds between new campers.")]
        [Min(1)] public float baseCamperSeconds = 40f;
        [Tooltip("Chance each night that a base besieges the kingdom: a crowd camps just outside, going for anyone who comes out.")]
        [Range(0, 1)] public float siegeChance = 0.35f;
        [Min(1)] public int siegeZombies = 14;
        [Tooltip("Seconds a siege lasts before the crowd goes home.")]
        [Min(10)] public float siegeSeconds = 240f;
        [Tooltip("Experience for destroying a base (spent on villager upgrades: V).")]
        [Min(0)] public int baseXp = 100;

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
        [Tooltip("Seconds of calm after a horde is beaten before the next one comes (N calls it early).")]
        [Min(0)] public float breakSeconds = 120f;
        [Tooltip("Hordes only come at night: the first a few seconds after dark, then another Break Seconds after each is beaten, until dawn. Days are for building (zombies still out keep fighting). Off: waves come day and night.")]
        public bool hordesAtNight = true;
        [Tooltip("Seconds after dark before the night's first horde.")]
        [Min(0)] public float nightfallDelay = 5f;
        [Tooltip("How many zombies come out of the dark per second while a wave arrives.")]
        [Min(0.1f)] public float spawnPerSecond = 2f;
        [Tooltip("Most zombies alive at once; the rest of the wave waits its turn. Lower it if the game slows down.")]
        [Min(1)] public int maxAlive = 120;
        [Tooltip("Zombies come from a ring round your kingdom (just outside its outermost walls), at least this many tiles from you, your villagers and your throne.")]
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
