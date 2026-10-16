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
    }
}
