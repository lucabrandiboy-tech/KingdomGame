using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Zombies
{
    /// <summary>What a kind of zombie can do besides clawing (see <see cref="ZombieConfig.ability"/>).</summary>
    public enum ZombieAbility { None, Spit, Burst, Scream, Burn, Ram }

    /// <summary>
    /// One kind of zombie: how it looks, how tough and fast it is, how hard it
    /// hits people, doors and walls, which wave it first comes in and what it drops.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Zombies/Zombie", fileName = "Zombie")]
    public sealed class ZombieConfig : ScriptableObject
    {
        public string displayName = "Walker";
        [Tooltip("Which pictures it uses: Walker, Runner, Brute, Crawler, Spitter, Bloater, Screamer, Armored, Giant or Warlord (see CharacterArt.ZombieKinds).")]
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

        [Tooltip("Armor: taken off every hit it takes (it always takes at least a fifth of a hit). Small quick shots do little; big shots and blasts go through.")]
        [Min(0)] public float armor;

        [Header("Special")]
        [Tooltip("Spit: spits acid at people up to Ability Range away (it lands where they stood, so they can dodge), even at guards up on walls and towers. " +
                 "Burst: blows up when it reaches a wall, door or person, or when it's killed. " +
                 "Scream: when it sees someone, it screams and the zombies round it speed up. " +
                 "Burn: it's on fire: its blows set wooden things alight, and so does it when it falls. " +
                 "Ram: it makes straight for the throne, smashing through walls, doors and anything else in the way instead of going round.")]
        public ZombieAbility ability;
        [Tooltip("Spit: how far it spits. Scream: how far away it sees someone to scream at.")]
        [Min(0)] public float abilityRange = 6f;
        [Tooltip("Seconds between spits or screams.")]
        [Min(0.5f)] public float abilitySeconds = 4f;
        [Tooltip("Spit: damage to anyone where the acid lands.")]
        [Min(0)] public float spitDamage = 12f;
        [Tooltip("Burst: the blast (it breaks walls and hurts people and other zombies).")]
        public DamageType burstBlast;
        [Tooltip("Burst: wear on the wall or door it bursts against, like a blow's Wall Damage.")]
        [Min(0)] public float burstWallDamage = 200f;
        [Tooltip("Scream: zombies within this many tiles speed up...")]
        [Min(0)] public float screamRadius = 9f;
        [Tooltip("...to this many times their speed...")]
        [Min(1)] public float screamSpeed = 1.5f;
        [Tooltip("...for this many seconds.")]
        [Min(0)] public float screamSeconds = 5f;

        [Header("Boss")]
        [Tooltip("A boss: it doesn't come with the waves by itself (the Survival Config's Boss comes every Boss Every waves), has a health bar across the top of the screen, " +
                 "and drops the Survival Config's boss loot.")]
        public bool boss;

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
