using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Zombies
{
    /// <summary>
    /// One kind of zombie: how it looks, how tough and fast it is, how hard it
    /// hits people, doors and walls, which wave it first comes in and what it drops.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Zombies/Zombie", fileName = "Zombie")]
    public sealed class ZombieConfig : ScriptableObject
    {
        public string displayName = "Walker";
        [Tooltip("Which pictures it uses: Walker, Runner or Brute (see CharacterArt.ZombieKinds).")]
        public string art = "Walker";

        [Header("Body")]
        [Min(1)] public float health = 60f;
        [Tooltip("Walking speed, in tiles per second.")]
        [Min(0.1f)] public float moveSpeed = 1.1f;
        [Tooltip("Size of its body, in tiles from its middle (for shots, blasts and crowding).")]
        [Range(0.1f, 0.6f)] public float radius = 0.22f;

        [Header("Attacking")]
        [Tooltip("Damage to the ruler or a villager per blow.")]
        [Min(0)] public float damage = 10f;
        [Tooltip("Seconds per blow: winding up takes the first half.")]
        [Min(0.2f)] public float attackSeconds = 1.2f;
        [Tooltip("How far its blows reach past its body, in tiles.")]
        [Min(0)] public float reach = 0.25f;
        [Tooltip("Damage per blow to doors and furniture (a wooden door has 250).")]
        [Min(0)] public float structureDamage = 10f;
        [Tooltip("Damage per blow to walls. A wall tile caves in after its material's Siege Health (stone 600, wood 200).")]
        [Min(0)] public float wallDamage = 10f;
        [Tooltip("The scratches its blows leave on walls (just for looks; the wall wears down by Wall Damage).")]
        public DamageType wallScratch;

        [Header("Waves (survival)")]
        [Tooltip("The first wave it comes in.")]
        [Min(1)] public int firstWave = 1;
        [Tooltip("How common it is compared to the other kinds (once it has been around for 3 waves).")]
        [Min(0)] public float weight = 1f;

        [Header("Loot")]
        [Tooltip("Chance (0-1) that it drops a bag of resources when it dies.")]
        [Range(0, 1)] public float lootChance = 0.25f;
        [Tooltip("Resources in the bag: a random mix of wood, stone and food.")]
        [Min(0)] public int lootAmount = 8;
    }
}
