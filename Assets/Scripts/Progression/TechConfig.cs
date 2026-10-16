using KingdomGame.Building;
using UnityEngine;

namespace KingdomGame.Progression
{
    /// <summary>
    /// One thing to research: what it costs (taken from storage when the research
    /// starts), how many real hours it takes, what has to be researched first, how
    /// many villagers the kingdom needs, and what it lets you build. The last one of
    /// each age moves the kingdom on to the next age.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Progression/Tech", fileName = "Tech")]
    public sealed class TechConfig : ScriptableObject
    {
        public string displayName = "Tech";
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Real hours it takes (it keeps going while the game is closed). The Progression Config's Research Speed divides this.")]
        [Min(0)] public float researchHours = 1f;
        [Min(0)] public int woodCost;
        [Min(0)] public int stoneCost;
        [Min(0)] public int foodCost;
        [Tooltip("Ore from the mines.")]
        [Min(0)] public int copperCost;
        [Min(0)] public int ironCost;
        [Min(0)] public int goldCost;
        [Tooltip("Anything else it takes from storage: the age's Craftables, for the research into the next age.")]
        public Economy.ResourceAmount[] extraCosts = new Economy.ResourceAmount[0];
        [Tooltip("The kingdom needs at least this many villagers before it can start (beds give homes to more).")]
        [Min(0)] public int minVillagers;
        [Tooltip("Researched first (in this age or an earlier one).")]
        public TechConfig[] requires = new TechConfig[0];
        [Tooltip("What it lets you build.")]
        public BuildingConfig[] unlocks = new BuildingConfig[0];
        [Tooltip("Researching it moves the kingdom into the next age.")]
        public bool advancesAge;

        /// <summary>What it costs of one resource.</summary>
        public int Cost(Economy.ResourceType type)
        {
            int extra = 0;
            if (extraCosts != null) foreach (var e in extraCosts) if (e.type == type) extra += e.amount;
            switch (type)
            {
                case Economy.ResourceType.Wood: return woodCost + extra;
                case Economy.ResourceType.Stone: return stoneCost + extra;
                case Economy.ResourceType.Food: return foodCost + extra;
                case Economy.ResourceType.Copper: return copperCost + extra;
                case Economy.ResourceType.Iron: return ironCost + extra;
                case Economy.ResourceType.Gold: return goldCost + extra;
                default: return extra;
            }
        }
    }
}
