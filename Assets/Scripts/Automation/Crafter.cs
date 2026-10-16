using System;
using System.Collections.Generic;
using KingdomGame.Economy;

namespace KingdomGame.Automation
{
    /// <summary>
    /// A furnace or an assembler: arms put ingredients (and, in a burner one, fuel) in; it makes its recipe, a batch at
    /// a time, into its output, which arms take out. A furnace picks its recipe by what's put in it (iron ore makes iron,
    /// copper ore copper, iron steel); an assembler makes the recipe you choose. It holds ingredients for two batches.
    /// Plain C# (unit-tested); Building/Factory.cs runs it.
    /// </summary>
    public sealed class Crafter : IItemPort, ITicking, IPowerUser
    {
        public const int MaxFuel = 5;

        readonly FuelTable fuels;
        readonly int[] inputs = new int[Inventory.TypeCount];

        public readonly bool Furnace;
        /// <summary>The recipes a furnace can pick from (by what's put in it); the game keeps it to the kingdom's age.</summary>
        public IReadOnlyList<Recipe> FurnaceRecipes = Array.Empty<Recipe>();
        public Recipe Recipe { get; private set; }
        /// <summary>Crafting speed (1 = a recipe's own time).</summary>
        public float Speed = 1f;
        /// <summary>Burns fuel (no electricity).</summary>
        public bool Burner;
        /// <summary>MW while working (a burner's fuel).</summary>
        public float EnergyUse = 0.09f;
        /// <summary>A burner's energy left (MJ), and its fuel waiting to burn.</summary>
        public float Energy;
        public int FuelCount { get; private set; }
        public ResourceType FuelType { get; private set; }
        /// <summary>An electric one's share of the power it needs (0-1).</summary>
        public float Power = 1f;
        public int Output { get; private set; }
        public ResourceType OutputType { get; private set; }
        /// <summary>Seconds into the batch it's making (at speed 1).</summary>
        public float Progress { get; private set; }
        public bool Crafting { get; private set; }
        /// <summary>Making something right now (for drawing it lit, and its pollution).</summary>
        public bool Working { get; private set; }
        /// <summary>Why it isn't working (null: it is).</summary>
        public string Problem { get; private set; }

        public Crafter(bool furnace, FuelTable fuels)
        {
            Furnace = furnace;
            this.fuels = fuels ?? new FuelTable(null);
        }

        public int Input(ResourceType t) => inputs[(int)t];
        /// <summary>MW an electric one wants: its use while it makes a batch, a thirtieth of that while it waits.</summary>
        public float PowerDemand() => Burner ? 0f : Crafting ? EnergyUse : EnergyUse * PowerGrid.IdleShare;
        public void SetPower(float share) => Power = share;
        /// <summary>The most finished goods it keeps before it stops.</summary>
        public int OutputCap => Recipe != null ? Math.Max(Recipe.amount * 2, 10) : 10;

        bool Empty()
        {
            if (Crafting || Output > 0) return false;
            foreach (int n in inputs) if (n > 0) return false;
            return true;
        }

        /// <summary>
        /// Sets an assembler's recipe. What's in it (ingredients, the batch being made, finished goods) goes into
        /// <paramref name="spill"/> (counts by type) to be put back in storage; its fuel stays.
        /// </summary>
        public void SetRecipe(Recipe recipe, int[] spill)
        {
            if (recipe == Recipe) return;
            TakeAll(spill, false);
            Recipe = recipe;
        }

        /// <summary>Empties it into <paramref name="spill"/> (counts by type): ingredients, the batch being made, finished goods, and fuel if asked.</summary>
        public void TakeAll(int[] spill, bool fuelToo)
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                if (spill != null && i < spill.Length) spill[i] += inputs[i];
                inputs[i] = 0;
            }
            if (Crafting && Recipe != null && Recipe.inputs != null && spill != null)
                foreach (var i in Recipe.inputs) if ((int)i.type < spill.Length) spill[(int)i.type] += i.amount;
            if (Output > 0 && spill != null && (int)OutputType < spill.Length) spill[(int)OutputType] += Output;
            Output = 0;
            Crafting = false;
            Progress = 0f;
            if (fuelToo)
            {
                if (FuelCount > 0 && spill != null && (int)FuelType < spill.Length) spill[(int)FuelType] += FuelCount;
                FuelCount = 0;
            }
        }

        /// <summary>Puts back what a saved game says.</summary>
        public void Restore(Recipe recipe, int[] savedInputs, ResourceType outputType, int output, bool crafting, float progress,
            ResourceType fuelType, int fuelCount, float energy)
        {
            Recipe = recipe;
            Array.Clear(inputs, 0, inputs.Length);
            if (savedInputs != null) for (int i = 0; i < savedInputs.Length && i < inputs.Length; i++) inputs[i] = Math.Max(0, savedInputs[i]);
            OutputType = outputType;
            Output = Math.Max(0, output);
            Crafting = crafting && recipe != null;
            Progress = Crafting ? Math.Max(0f, progress) : 0f;
            FuelType = fuelType;
            FuelCount = Math.Max(0, Math.Min(MaxFuel, fuelCount));
            Energy = energy;
        }

        // The furnace recipe that takes <item> in (one ingredient).
        Recipe FurnaceRecipeFor(ResourceType item)
        {
            foreach (var r in FurnaceRecipes)
                if (r != null && r.inputs != null && r.inputs.Length == 1 && r.inputs[0].type == item) return r;
            return null;
        }

        // The recipe <item> would go into as an ingredient now (null: it wouldn't).
        Recipe IngredientFor(ResourceType item)
        {
            if (Recipe != null && Recipe.Need(item) > 0) return inputs[(int)item] < Recipe.Need(item) * 2 ? Recipe : null;
            if (Furnace && Empty()) return FurnaceRecipeFor(item);   // an empty furnace takes up whatever it can smelt
            return null;
        }

        bool FuelFits(ResourceType item) =>
            Burner && fuels.IsFuel(item) && FuelCount < MaxFuel && (FuelCount == 0 || FuelType == item);

        public bool Accepts(ResourceType item) => IngredientFor(item) != null || FuelFits(item);

        public bool Insert(ResourceType item)
        {
            var r = IngredientFor(item);
            if (r != null)
            {
                Recipe = r;
                inputs[(int)item]++;
                return true;
            }
            if (!FuelFits(item)) return false;
            FuelType = item;
            FuelCount++;
            return true;
        }

        public bool Offer(Func<ResourceType, bool> want, out ResourceType item)
        {
            item = OutputType;
            return Output > 0 && want(OutputType);
        }

        public bool Extract(ResourceType item)
        {
            if (Output <= 0 || item != OutputType) return false;
            Output--;
            return true;
        }

        public void Tick(float dt)
        {
            Working = false;
            if (!Crafting)
            {
                if (Recipe == null) { Problem = Furnace ? "nothing to smelt" : "no recipe: E to pick one"; return; }
                if (Output > 0 && OutputType != Recipe.output) { Problem = "full: take its goods out"; return; }
                if (Output + Recipe.amount > OutputCap) { Problem = "full: take its goods out"; return; }
                if (Recipe.inputs != null)
                    foreach (var i in Recipe.inputs)
                        if (inputs[(int)i.type] < i.amount) { Problem = "waiting for ingredients"; return; }
                if (Recipe.inputs != null) foreach (var i in Recipe.inputs) inputs[(int)i.type] -= i.amount;
                Crafting = true;
                Progress = 0f;
            }
            float rate;
            if (Burner)
            {
                if (Energy <= 0f && FuelCount > 0)
                {
                    FuelCount--;
                    Energy += fuels.Energy(FuelType);
                }
                if (Energy <= 0f) { Problem = "no fuel: put wood or coal in"; return; }
                rate = Speed;
                Energy -= EnergyUse * dt;
            }
            else
            {
                if (Power <= 0f) { Problem = "no power"; return; }
                rate = Speed * Math.Min(1f, Power);
            }
            Working = true;
            Problem = null;
            Progress += dt * rate;
            if (Progress < Recipe.seconds) return;
            Output += Recipe.amount;
            OutputType = Recipe.output;
            Crafting = false;
            Progress = 0f;
        }
    }
}
