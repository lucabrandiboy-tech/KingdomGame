using KingdomGame.Automation;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// The factory's numbers: what furnaces and assemblers can make, what burns and how long, and how fast it all runs
    /// (Building/Factory.cs).
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Building/Factory Config", fileName = "FactoryConfig")]
    public sealed class FactoryConfig : ScriptableObject
    {
        [Tooltip("What furnaces (smelting, picked by what goes in) and assemblers (you choose) make, from which age.")]
        public Recipe[] recipes = RecipeBook.Defaults();
        [Tooltip("What burns in burner machines, and its energy (MJ).")]
        public Fuel[] fuels = RecipeBook.DefaultFuels();
        [Tooltip("How many times a second the factory moves on (more is smoother and costs more).")]
        [Range(10, 60)] public int ticksPerSecond = 30;
        [Tooltip("How fast the older machines (sawmills, quarries, drills) put what they make out onto belts touching them, items a second.")]
        [Min(0.5f)] public float machineOutputRate = 4f;
        [Tooltip("How big items on belts are drawn, in tiles.")]
        [Range(0.1f, 0.6f)] public float itemSize = 0.3f;

        [Header("Pollution (machines make it: Machine Config's Pollution, a minute)")]
        [Tooltip("The share of a square's pollution it gives each of its four neighbours a second (squares are 32 tiles).")]
        [Range(0f, 0.2f)] public float pollutionSpread = 0.02f;
        [Tooltip("A square only spreads pollution when it has more than this.")]
        [Min(0f)] public float pollutionSpreadAbove = 15f;
        [Tooltip("What any square absorbs a second.")]
        [Min(0f)] public float pollutionAbsorb = 0.05f;
        [Tooltip("What each tree absorbs a second (a square of forest has a few hundred).")]
        [Min(0f)] public float pollutionPerTree = 0.002f;
        [Tooltip("Survival: hordes are twice as big with this much pollution in the air, three times with twice it...")]
        [Min(1f)] public float pollutionPerDoubleWave = 2000f;
        [Tooltip("Survival: the most pollution makes the hordes bigger (times).")]
        [Min(1f)] public float pollutionMostWaves = 4f;
        [Tooltip("Survival: for each this much pollution ever made, the zombies are a wave further on (tougher, and later kinds).")]
        [Min(1f)] public float pollutionPerEvolution = 5000f;
        [Tooltip("Survival: the most waves further on pollution takes them.")]
        [Min(0)] public int pollutionMostEvolution = 20;
        [Tooltip("Survival: how much pollution a zombie base breathes in a second, where the cloud reaches it.")]
        [Min(0f)] public float baseBreathes = 1f;
        [Tooltip("Survival: a base sends one zombie at the kingdom for each this much pollution it breathed in...")]
        [Min(1f)] public float pollutionPerAttacker = 40f;
        [Tooltip("...when it has at least this many to send (and never more than Attack Group Most at once).")]
        [Min(1)] public int attackGroupLeast = 5;
        [Min(1)] public int attackGroupMost = 40;
    }
}
