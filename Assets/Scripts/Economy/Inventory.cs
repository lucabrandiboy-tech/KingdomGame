using System;

namespace KingdomGame.Economy
{
    /// <summary>The resources (new ones go at the end: saves store the number). Ores are dug in mines.</summary>
    public enum ResourceType
    {
        Wood,
        Stone,
        Food,
        Copper,
        Iron,
        Gold,
        /// <summary>Smelted from iron at a smelter (Medieval); machines are built from it.</summary>
        Steel,
        /// <summary>Dug in mines (near the shaft); burns in generators three times as long as wood.</summary>
        Coal,
        // The parts made at each age's crafting table (from the Stone Age to World War 2). Building an
        // age's things takes some of its part, and of every earlier age's part too.
        Rope,
        BronzeBar,
        IronBar,
        Gear,
        Gunpowder,
        MachineParts,
        Wire,
        Electronics,
        /// <summary>Dug by drills from iron ore fields on the map; a furnace smelts it into iron.</summary>
        IronOre,
        /// <summary>Dug by drills from copper ore fields; a furnace smelts it into copper.</summary>
        CopperOre,
        // Craftables (like Factorio's science packs): made in assemblers from each age's goods, hard to make, and making
        // them pollutes a lot. Researching the way into the next age takes a pile of the age's own (Bronze Age to World War 1).
        BronzeCraftable,
        IronCraftable,
        MedievalCraftable,
        RenaissanceCraftable,
        IndustrialCraftable,
        ModernCraftable,
    }

    /// <summary>An amount of one resource (extra building costs: an age's parts).</summary>
    [Serializable]
    public struct ResourceAmount
    {
        public ResourceType type;
        public int amount;

        public ResourceAmount(ResourceType t, int n)
        {
            type = t;
            amount = n;
        }
    }

    public static class ResourceText
    {
        /// <summary>The first age part (Rope); the one for age N is FirstPart + N.</summary>
        public const ResourceType FirstPart = ResourceType.Rope;
        public const int Parts = 8;

        /// <summary>The part made at age <paramref name="age"/>'s crafting table (0 = Stone Age).</summary>
        public static ResourceType PartOf(int age) => FirstPart + (age < 0 ? 0 : age >= Parts ? Parts - 1 : age);
        public static bool IsPart(ResourceType type) => type >= FirstPart && type < FirstPart + Parts;

        /// <summary>The first Craftable (the Bronze Age's); age N's is FirstCraftable + N - 1, from the Bronze Age (1) to World War 1 (6).</summary>
        public const ResourceType FirstCraftable = ResourceType.BronzeCraftable;
        public const int Craftables = 6;

        /// <summary>The Craftable made in age <paramref name="age"/> and needed to research the next age (none in the Stone Age or World War 2: false).</summary>
        public static bool CraftableOf(int age, out ResourceType type)
        {
            type = FirstCraftable + Math.Max(0, Math.Min(Craftables - 1, age - 1));
            return age >= 1 && age <= Craftables;
        }

        public static bool IsCraftable(ResourceType type) => type >= FirstCraftable && type < FirstCraftable + Craftables;

        /// <summary>How a resource is called in text, lower case ("wood", "bronze bars", "machine parts").</summary>
        public static string Name(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.BronzeBar: return "bronze bars";
                case ResourceType.IronBar: return "iron bars";
                case ResourceType.Gear: return "gears";
                case ResourceType.MachineParts: return "machine parts";
                case ResourceType.IronOre: return "iron ore";
                case ResourceType.CopperOre: return "copper ore";
                case ResourceType.BronzeCraftable: return "bronze craftables";
                case ResourceType.IronCraftable: return "iron craftables";
                case ResourceType.MedievalCraftable: return "medieval craftables";
                case ResourceType.RenaissanceCraftable: return "renaissance craftables";
                case ResourceType.IndustrialCraftable: return "industrial craftables";
                case ResourceType.ModernCraftable: return "modern craftables";
                default: return type.ToString().ToLowerInvariant();
            }
        }

        /// <summary>Like "120 wood, 4 iron" for everything there's some of, or "nothing".</summary>
        public static string Describe(Inventory inventory)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Inventory.TypeCount; i++)
            {
                int n = inventory.Get((ResourceType)i);
                if (n > 0 || i < 3) parts.Add($"{n} {Name((ResourceType)i)}");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
        }
    }

    /// <summary>
    /// A limited set of resources, like the ruler's backpack or (later) a storage
    /// building. The capacity is shared by all resource types.
    /// </summary>
    [Serializable]
    public sealed class Inventory
    {
        public static readonly int TypeCount = Enum.GetValues(typeof(ResourceType)).Length;

        readonly int[] counts = new int[TypeCount];

        public Inventory(int capacity) => Capacity = capacity;

        public int Capacity { get; set; }
        public int Total { get; private set; }
        public int SpaceLeft => Math.Max(0, Capacity - Total);

        /// <summary>Called whenever an amount changes.</summary>
        public event Action Changed;

        public int Get(ResourceType type) => counts[(int)type];

        /// <summary>Adds as much as fits and returns how much was added.</summary>
        public int Add(ResourceType type, int amount)
        {
            int added = Math.Min(Math.Max(0, amount), SpaceLeft);
            if (added == 0) return 0;
            counts[(int)type] += added;
            Total += added;
            Changed?.Invoke();
            return added;
        }

        /// <summary>Takes the amount out if there's enough, otherwise takes nothing and returns false.</summary>
        public bool TryRemove(ResourceType type, int amount)
        {
            if (amount < 0 || counts[(int)type] < amount) return false;
            counts[(int)type] -= amount;
            Total -= amount;
            Changed?.Invoke();
            return true;
        }
    }
}
