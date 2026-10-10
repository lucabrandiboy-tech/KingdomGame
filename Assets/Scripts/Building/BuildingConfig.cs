using KingdomGame.Art;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Building
{
    public enum BuildingKind
    {
        /// <summary>A destructible wall tile (stone or wood).</summary>
        Wall,
        /// <summary>Holds resources for the kingdom; builders fetch from it (a storehouse, a chest).</summary>
        Storage,
        /// <summary>A floor tile: drawn under everything, walked on, furniture goes on top.</summary>
        Floor,
        /// <summary>Opens for you and your villagers, not for zombies (they break it down).</summary>
        Door,
        /// <summary>Tables, chairs, beds, torches and the rest.</summary>
        Furniture,
        /// <summary>A roof over a cell: placed by dragging an area, drawn as one roof over the whole house.</summary>
        Roof,
        /// <summary>A house you go into: a room inside, bigger than it looks, that grows when it's upgraded.</summary>
        House,
        /// <summary>A young tree or bush you plant; it grows, and woodcutters or foragers gather from it.</summary>
        Plant,
        /// <summary>A mine's entrance: you go down to an underground level of rock to dig out.</summary>
        Mine,
        /// <summary>Marks rock in a mine to be dug out (not a building: miners dig it).</summary>
        Dig,
    }

    /// <summary>The tabs of the build bar (new ones go at the end: assets store the number).</summary>
    public enum BuildCategory
    {
        Walls,
        Floors,
        Furniture,
        Lights,
        Storage,
        Traps,
        Towers,
        Houses,
        Farming,
        Mining,
    }

    /// <summary>What a piece of furniture counts as, for naming the room it's in.</summary>
    public enum RoomTag
    {
        None,
        Bed,
        Table,
        Seat,
        Storage,
        Books,
        Weapons,
        Cooking,
        Workshop,
        Decor,
    }

    /// <summary>
    /// Something you can build from the throne: its size, what it costs, how long
    /// it takes and how it behaves. Placed as a blueprint that builder villagers put up.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Building/Building", fileName = "Building")]
    public sealed class BuildingConfig : ScriptableObject
    {
        [Tooltip("Shown on the build bar.")]
        public string displayName = "Building";
        public BuildingKind kind = BuildingKind.Wall;
        [Tooltip("Which tab of the build bar it's on.")]
        public BuildCategory category = BuildCategory.Walls;
        [Tooltip("Size in grid tiles, facing its first direction (turning it with R swaps width and height).")]
        public Vector2Int size = Vector2Int.one;
        [Tooltip("The name of its picture in the baked art (see ArtKeys); empty for walls and the storehouse.")]
        public string art = "";
        [Tooltip("How many ways it can face (1, 2 or 4); R turns it while placing.")]
        [Range(1, 4)] public int rotations = 1;

        [Header("Cost (taken from storage by the builders)")]
        [Min(0)] public int woodCost;
        [Min(0)] public int stoneCost;
        [Tooltip("Food stands in for cloth and wool for now (rugs, beds, banners).")]
        [Min(0)] public int foodCost;
        [Tooltip("Seconds of work for one builder once all the resources are there. Two builders take half as long.")]
        [Min(0.1f)] public float buildSeconds = 4f;

        [Header("Walls")]
        [Tooltip("What the wall is made of (how tough it is and how it looks).")]
        public StructureMaterial wallMaterial;
        [Tooltip("Hold the left mouse button and drag to place a whole line (walls) or an area (floors).")]
        public bool dragToPlaceLines = true;

        [Header("Doors and furniture")]
        [Tooltip("Nobody can walk through it (tables, beds, shelves). Chairs, rugs and benches can be walked over.")]
        public bool blocksMovement = true;
        [Tooltip("Hit points before zombies (or your explosives) break it. 0 = can't be broken.")]
        [Min(0)] public float health = 150f;
        [Tooltip("How tall it is, in tiles (how far its shadow falls).")]
        [Min(0)] public float height = 0.15f;
        [Tooltip("Lies flat on the floor (rugs): drawn under people and furniture, no shadow.")]
        public bool flat;
        [Tooltip("What it counts as when the game names the room it's in.")]
        public RoomTag roomTag = RoomTag.Decor;
        [Tooltip("Beds: how many villagers it gives a home (survival: more survivors can join).")]
        [Min(0)] public int sleeps;
        [Tooltip("A ladder or stairs: it leans on the wall it faces, and the ruler climbs up there with E.")]
        public bool climb;

        [Header("Light (torches, candles, fireplaces)")]
        [Tooltip("0 = no light. 1 = as far and bright as a brazier.")]
        [Min(0)] public float lightSize;
        [Tooltip("Where the flames are, in tiles from its middle, facing its first direction.")]
        public Vector2[] flames = new Vector2[0];
        [Tooltip("Size of each flame picture (1 = a torch's).")]
        [Min(0)] public float flameSize = 1f;

        [Header("Roofs")]
        [Tooltip("What the roof is covered with (each age has its own).")]
        public RoofStyle roofStyle = RoofStyle.Thatch;

        [Header("Traps and towers")]
        [Tooltip("What it does to zombies (a trap, an area, a mine or a tower), or empty for ordinary furniture.")]
        public Defenses.DefenseConfig defense;

        [Header("Mines")]
        [Tooltip("How the mine underground works (Mine only).")]
        public MineConfig mine;

        [Header("Houses")]
        [Tooltip("A house's levels, one per age (Houses only).")]
        public HouseConfig house;

        [Header("Planting (tree farms, berry patches)")]
        [Tooltip("What grows here (a tree or bush kind).")]
        public ResourceNodeConfig plant;
        [Tooltip("Seconds until it's fully grown and can be gathered.")]
        [Min(1)] public float growSeconds = 240f;

        [Header("Storage")]
        [Tooltip("How much it holds, all resources together.")]
        [Min(1)] public int storageCapacity = 1000;

        /// <summary>Its size facing direction <paramref name="rotation"/> (0..3).</summary>
        public Vector2Int SizeFacing(int rotation) => rotation % 2 == 0 ? size : new Vector2Int(size.y, size.x);

        /// <summary>A valid direction for this building (R cycles through them).</summary>
        public int ClampRotation(int rotation)
        {
            int count = Mathf.Clamp(rotations, 1, 4);
            return ((rotation % count) + count) % count;
        }

        public int Cost(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return woodCost;
                case ResourceType.Stone: return stoneCost;
                case ResourceType.Food: return foodCost;
                default: return 0;
            }
        }

        /// <summary>Like "10 stone" or "60 wood, 20 stone".</summary>
        public string CostText(int count = 1)
        {
            var parts = new System.Collections.Generic.List<string>(3);
            if (woodCost > 0) parts.Add($"{woodCost * count} wood");
            if (stoneCost > 0) parts.Add($"{stoneCost * count} stone");
            if (foodCost > 0) parts.Add($"{foodCost * count} cloth (food)");
            return parts.Count > 0 ? string.Join(", ", parts) : "free";
        }
    }
}
