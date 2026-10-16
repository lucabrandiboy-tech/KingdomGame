using System;
using KingdomGame.Economy;

namespace KingdomGame.Automation
{
    /// <summary>
    /// What a furnace or assembler makes: ingredients in, a product out, taking <see cref="seconds"/> at crafting
    /// speed 1. Furnaces pick their recipe by what's put in them; assemblers make the one you choose.
    /// </summary>
    [Serializable]
    public sealed class Recipe
    {
        public string name = "Gears";
        public ResourceAmount[] inputs = new ResourceAmount[0];
        public ResourceType output = ResourceType.Gear;
        public int amount = 1;
        public float seconds = 2f;
        /// <summary>Made in furnaces (smelting), not assemblers.</summary>
        public bool furnace;
        /// <summary>The age it can be made from (0 = Stone Age).</summary>
        public int age;

        /// <summary>How many of <paramref name="type"/> one batch takes (0: none).</summary>
        public int Need(ResourceType type)
        {
            int n = 0;
            if (inputs != null) foreach (var i in inputs) if (i.type == type) n += i.amount;
            return n;
        }

        public static Recipe Make(string name, ResourceType output, int amount, float seconds, int age, bool furnace, params ResourceAmount[] inputs) =>
            new Recipe { name = name, output = output, amount = amount, seconds = seconds, age = age, furnace = furnace, inputs = inputs };
    }

    /// <summary>Something that burns in a burner machine (an arm, a furnace, a drill), and how much energy it holds (MJ).</summary>
    [Serializable]
    public struct Fuel
    {
        public ResourceType type;
        public float energy;

        public Fuel(ResourceType t, float megajoules)
        {
            type = t;
            energy = megajoules;
        }
    }

    /// <summary>The fuels and their energy, shared by everything in a factory.</summary>
    public sealed class FuelTable
    {
        readonly float[] energy = new float[Inventory.TypeCount];

        public FuelTable(Fuel[] fuels)
        {
            if (fuels != null) foreach (var f in fuels) if ((int)f.type < energy.Length) energy[(int)f.type] = Math.Max(0f, f.energy);
        }

        public bool IsFuel(ResourceType t) => (int)t < energy.Length && energy[(int)t] > 0f;
        public float Energy(ResourceType t) => (int)t < energy.Length ? energy[(int)t] : 0f;
    }
}

namespace KingdomGame.Automation
{
    /// <summary>The recipes a new game starts with (Build Game Scenes puts them in the Factory Config).</summary>
    public static class RecipeBook
    {
        static ResourceAmount In(ResourceType t, int n) => new ResourceAmount(t, n);

        public static Recipe[] Defaults() => new[]
        {
            // Furnaces: they smelt whatever is put in.
            Recipe.Make("Iron", ResourceType.Iron, 1, 3.2f, 0, true, In(ResourceType.IronOre, 1)),
            Recipe.Make("Copper", ResourceType.Copper, 1, 3.2f, 0, true, In(ResourceType.CopperOre, 1)),
            Recipe.Make("Steel", ResourceType.Steel, 1, 16f, 3, true, In(ResourceType.Iron, 5)),
            // Assemblers: you choose. Each age's part (what its buildings are made of), and the factory's own parts.
            Recipe.Make("Rope", ResourceType.Rope, 2, 4f, 0, false, In(ResourceType.Food, 2), In(ResourceType.Wood, 1)),
            Recipe.Make("Bronze bars", ResourceType.BronzeBar, 2, 6f, 1, false, In(ResourceType.Copper, 3), In(ResourceType.Coal, 1)),
            Recipe.Make("Iron bars", ResourceType.IronBar, 2, 6f, 2, false, In(ResourceType.Iron, 3), In(ResourceType.Coal, 2)),
            Recipe.Make("Gears", ResourceType.Gear, 1, 1f, 2, false, In(ResourceType.Iron, 2)),
            Recipe.Make("Gunpowder", ResourceType.Gunpowder, 2, 5f, 4, false, In(ResourceType.Coal, 2), In(ResourceType.Stone, 1), In(ResourceType.Food, 1)),
            Recipe.Make("Machine parts", ResourceType.MachineParts, 2, 6f, 5, false, In(ResourceType.Steel, 2), In(ResourceType.Gear, 1), In(ResourceType.Coal, 1)),
            Recipe.Make("Wire", ResourceType.Wire, 2, 0.5f, 5, false, In(ResourceType.Copper, 1)),
            Recipe.Make("Electronics", ResourceType.Electronics, 1, 1f, 6, false, In(ResourceType.Iron, 1), In(ResourceType.Wire, 3)),
        };

        public static Fuel[] DefaultFuels() => new[] { new Fuel(ResourceType.Wood, 2f), new Fuel(ResourceType.Coal, 4f) };
    }
}
