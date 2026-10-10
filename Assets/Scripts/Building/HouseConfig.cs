using System;
using KingdomGame.Art;
using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>One level of a house: what it's called, how big it is inside, how it looks, and what upgrading to it costs.</summary>
    [Serializable]
    public sealed class HouseLevel
    {
        [Tooltip("Like \"Hut\" or \"Manor\".")]
        public string displayName = "Hut";
        [Tooltip("The age this level belongs to (0 = the first). A house can only be upgraded to it once the kingdom has reached that age.")]
        [Min(0)] public int age;
        [Tooltip("Size of the room inside, in tiles. The house on the map stays the same size: only the inside grows. Odd widths keep the door in the middle.")]
        public Vector2Int inside = new Vector2Int(5, 4);
        [Tooltip("Hit points before zombies (or your explosives) break the house.")]
        [Min(1)] public float health = 600f;

        [Header("Look")]
        public RoofStyle roof = RoofStyle.Thatch;
        [Tooltip("What the walls inside are made of (only their look is used).")]
        public StructureMaterial walls;
        [Tooltip("The floor inside: a floor's art name (Floor/Wood or Floor/Stone).")]
        public string floorArt = "Floor/Wood";
        [Tooltip("The door, inside and out: a door's art name (Door/Wood or Door/Heavy).")]
        public string doorArt = "Door/Wood";
        [Tooltip("The chimney on the roof.")]
        public Color32 chimney = new Color32(112, 108, 100, 255);

        [Header("Upgrading to this level (from the level before)")]
        [Min(0)] public int woodCost;
        [Min(0)] public int stoneCost;
        [Min(0)] public int foodCost;
        [Tooltip("Seconds of work for one builder once the resources are there.")]
        [Min(0.1f)] public float buildSeconds = 30f;

        /// <summary>What upgrading to this level costs, like "40 wood, 60 stone".</summary>
        public string CostText()
        {
            var parts = new System.Collections.Generic.List<string>(3);
            if (woodCost > 0) parts.Add($"{woodCost} wood");
            if (stoneCost > 0) parts.Add($"{stoneCost} stone");
            if (foodCost > 0) parts.Add($"{foodCost} cloth (food)");
            return parts.Count > 0 ? string.Join(", ", parts) : "free";
        }
    }

    /// <summary>
    /// A house's levels, one per age: a hut in the Stone Age up to a concrete house in
    /// World War 2. Upgrading a house (once the kingdom has reached the next age) makes
    /// the room inside bigger and gives the house the new age's look; the house on the
    /// map stays the same size. Builders do the upgrade, like any other building.
    /// The first level is what the house's own building costs build.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Building/House Config", fileName = "HouseConfig")]
    public sealed class HouseConfig : ScriptableObject
    {
        public HouseLevel[] levels = new HouseLevel[0];

        public int Count => levels != null ? levels.Length : 0;

        /// <summary>The level at <paramref name="index"/> (clamped), or a plain hut if there are none.</summary>
        public HouseLevel Level(int index)
        {
            if (Count == 0) return new HouseLevel();
            return levels[Mathf.Clamp(index, 0, Count - 1)] ?? new HouseLevel();
        }
    }
}
