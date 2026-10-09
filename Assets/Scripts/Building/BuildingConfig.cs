using KingdomGame.Destruction;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Building
{
    public enum BuildingKind
    {
        /// <summary>A destructible wall tile (stone or wood).</summary>
        Wall,
        /// <summary>Holds resources for the kingdom; builders fetch from it.</summary>
        Storage,
    }

    /// <summary>
    /// Something you can build from the throne: its size, what it costs and how
    /// long it takes. Placed as a blueprint that builder villagers put up.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Building/Building", fileName = "Building")]
    public sealed class BuildingConfig : ScriptableObject
    {
        [Tooltip("Shown on the build bar.")]
        public string displayName = "Building";
        public BuildingKind kind = BuildingKind.Wall;
        [Tooltip("Size in grid tiles.")]
        public Vector2Int size = Vector2Int.one;

        [Header("Cost (taken from storage by the builders)")]
        [Min(0)] public int woodCost;
        [Min(0)] public int stoneCost;
        [Tooltip("Seconds of work for one builder once all the resources are there. Two builders take half as long.")]
        [Min(0.1f)] public float buildSeconds = 4f;

        [Header("Walls")]
        [Tooltip("What the wall is made of (how tough it is and how it looks).")]
        public StructureMaterial wallMaterial;
        [Tooltip("Hold the left mouse button and drag to place a whole line.")]
        public bool dragToPlaceLines = true;

        [Header("Storage")]
        [Tooltip("How much it holds, all resources together.")]
        [Min(1)] public int storageCapacity = 1000;

        public int Cost(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return woodCost;
                case ResourceType.Stone: return stoneCost;
                default: return 0;
            }
        }

        /// <summary>Like "10 stone" or "60 wood, 20 stone".</summary>
        public string CostText()
        {
            if (woodCost > 0 && stoneCost > 0) return $"{woodCost} wood, {stoneCost} stone";
            if (woodCost > 0) return $"{woodCost} wood";
            if (stoneCost > 0) return $"{stoneCost} stone";
            return "free";
        }
    }
}
