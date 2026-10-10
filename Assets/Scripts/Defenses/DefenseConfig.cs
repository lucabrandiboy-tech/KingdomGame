using KingdomGame.Combat;
using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Defenses
{
    public enum DefenseKind
    {
        /// <summary>Goes off when a zombie steps on it (hurts, and can hold it), then sets itself again.</summary>
        Trap,
        /// <summary>Slows and wears down every zombie walking through it (stakes, barbed wire).</summary>
        Area,
        /// <summary>Blows up once when a zombie comes close; builders set a new one.</summary>
        Mine,
        /// <summary>Shoots zombies in range: with a guard on it, or by itself.</summary>
        Tower,
    }

    /// <summary>
    /// What a trap or tower does. A building (on the Traps or Towers tab) gets one of
    /// these as its Defense; the numbers here are what it does to zombies.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Defenses/Defense", fileName = "Defense")]
    public sealed class DefenseConfig : ScriptableObject
    {
        public DefenseKind kind = DefenseKind.Trap;

        [Header("Traps")]
        [Tooltip("Damage when it goes off.")]
        [Min(0)] public float damage = 40f;
        [Tooltip("Seconds the zombie it catches can't move or hit anything.")]
        [Min(0)] public float holdSeconds = 5f;
        [Tooltip("Catches only the first zombie (a pit, a bear trap). Off: hits every zombie on it.")]
        public bool catchesOne = true;
        [Tooltip("Seconds until it's ready again after going off.")]
        [Min(0.1f)] public float rearmSeconds = 15f;

        [Header("Areas (stakes, wire)")]
        [Tooltip("Share of their speed zombies keep while in it (0.5 = half speed).")]
        [Range(0.05f, 1f)] public float slowTo = 0.5f;
        [Tooltip("Damage per second to every zombie in it.")]
        [Min(0)] public float damagePerSecond = 5f;

        [Header("Mines")]
        [Tooltip("The explosion (its Creature Damage and Blast Radius hurt zombies, and it can break walls).")]
        public DamageType blast;
        [Tooltip("How close a zombie must come to set it off, in tiles from its middle.")]
        [Min(0.05f)] public float triggerRadius = 0.45f;

        [Header("Towers")]
        [Tooltip("What it shoots (its Hit Damage, speed and look; explosive shots blow up where they land).")]
        public WeaponConfig weapon;
        [Tooltip("How far it shoots, in tiles.")]
        [Min(0.5f)] public float range = 7f;
        [Tooltip("It can't hit zombies closer than this (cannons, mortars), in tiles.")]
        [Min(0)] public float minRange;
        [Tooltip("Shots per second.")]
        [Min(0.05f)] public float shotsPerSecond = 1f;
        [Tooltip("Zombies one shot goes through before it stops (ballista bolts).")]
        [Min(0)] public int pierce;
        [Tooltip("The shot arcs over everything and lands where it's aimed (cannonballs, mortar shells).")]
        public bool lobbed;
        [Tooltip("Only shoots while a villager with the Guard job is on it. Off: it shoots by itself.")]
        public bool needsGuard = true;
        [Tooltip("How fast it turns to aim, in degrees per second.")]
        [Min(1)] public float turnSpeed = 240f;
    }
}
