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
    }

    public static class ResourceText
    {
        /// <summary>Like "120 wood, 4 iron" for everything there's some of, or "nothing".</summary>
        public static string Describe(Inventory inventory)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int i = 0; i < Inventory.TypeCount; i++)
            {
                int n = inventory.Get((ResourceType)i);
                if (n > 0 || i < 3) parts.Add($"{n} {((ResourceType)i).ToString().ToLowerInvariant()}");
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
