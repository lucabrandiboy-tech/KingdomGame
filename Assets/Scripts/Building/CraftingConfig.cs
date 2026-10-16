using System;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>What a crafting order makes.</summary>
    public enum CraftKind
    {
        /// <summary>Better tools for every villager, for good: they gather and build faster (each set adds to the last).</summary>
        Tools,
        /// <summary>Bandages: C heals you with one; a badly hurt soldier uses one instead of leaving the fight.</summary>
        Bandages,
        /// <summary>A cure for a bite: a bitten villager gets it before turning into a zombie.</summary>
        Cure,
    }

    /// <summary>One thing the crafting room makes (ordered at its tool bench, made there by crafters).</summary>
    [Serializable]
    public sealed class CraftItem
    {
        public string displayName = "Tools";
        [TextArea(1, 3)] public string description = "";
        public CraftKind kind;
        [Tooltip("The age it can be made in (0 = Stone Age): the kingdom has to have reached it.")]
        [Range(0, 7)] public int age;
        [Tooltip("How many one order makes (bandages, cures). Tools: one set.")]
        [Min(1)] public int amount = 1;
        [Tooltip("Tools: how much faster villagers gather (0.08 = 8%).")]
        [Range(0, 1)] public float gathering;
        [Tooltip("Tools: how much faster builders build.")]
        [Range(0, 1)] public float building;
        [Tooltip("Bandages: hit points one heals.")]
        [Min(0)] public float heal;
        [Min(0)] public int woodCost, stoneCost, foodCost, copperCost, ironCost, goldCost, steelCost;
        [Tooltip("Seconds of a crafter's work at a tool bench.")]
        [Min(1)] public float craftSeconds = 30f;

        public int Cost(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Wood: return woodCost;
                case ResourceType.Stone: return stoneCost;
                case ResourceType.Food: return foodCost;
                case ResourceType.Copper: return copperCost;
                case ResourceType.Iron: return ironCost;
                case ResourceType.Gold: return goldCost;
                case ResourceType.Steel: return steelCost;
                default: return 0;
            }
        }

        public string CostText()
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int k = 0; k < Inventory.TypeCount; k++)
            {
                int n = Cost((ResourceType)k);
                if (n > 0) parts.Add($"{n} {ResourceText.Name((ResourceType)k)}");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "free";
        }

        static CraftItem Tools(string name, string about, int age, float gathering, float building, float seconds,
            int wood = 0, int stone = 0, int copper = 0, int iron = 0, int gold = 0, int steel = 0) => new CraftItem
        {
            displayName = name, description = about, kind = CraftKind.Tools, age = age, gathering = gathering, building = building,
            craftSeconds = seconds, woodCost = wood, stoneCost = stone, copperCost = copper, ironCost = iron, goldCost = gold, steelCost = steel,
        };

        /// <summary>What the crafting room makes (Build Game Scenes puts these in the Crafting Config).</summary>
        public static CraftItem[] Defaults() => new[]
        {
            Tools("Flint tools", "Sharp flint axes, picks and knives.", 0, 0.08f, 0.05f, 40f, wood: 60, stone: 80),
            Tools("Bronze tools", "Bronze axes and chisels that keep their edge.", 1, 0.08f, 0.05f, 60f, wood: 60, copper: 80),
            Tools("Iron tools", "Iron saws, picks and hammers.", 2, 0.08f, 0.05f, 80f, wood: 60, iron: 100),
            Tools("Steel tools", "Tempered steel blades and picks.", 3, 0.08f, 0.05f, 100f, iron: 80, steel: 30),
            Tools("Hand drills and planes", "Finer tools for finer work.", 4, 0.08f, 0.05f, 120f, iron: 100, gold: 10, steel: 50),
            Tools("Steam-forged tools", "Factory-made, all the same, never breaking.", 5, 0.08f, 0.05f, 140f, gold: 20, steel: 120),
            Tools("Pneumatic tools", "Air-driven drills and hammers.", 6, 0.08f, 0.05f, 160f, gold: 40, steel: 160),
            Tools("Machine tools", "Precision tools for everything.", 7, 0.08f, 0.05f, 180f, gold: 60, steel: 200),
            new CraftItem
            {
                displayName = "Bandages", description = "C heals you with one. Badly hurt soldiers use one instead of leaving the fight.",
                kind = CraftKind.Bandages, age = 0, amount = 5, heal = 50f, foodCost = 25, woodCost = 5, craftSeconds = 20f,
            },
            new CraftItem
            {
                displayName = "Cure for bites", description = "A bitten villager is given it before they turn, and lives.",
                kind = CraftKind.Cure, age = 1, amount = 1, foodCost = 60, copperCost = 15, craftSeconds = 60f,
            },
        };
    }

    /// <summary>
    /// What a crafting room makes: better tools for each age (villagers gather and build faster for good) and supplies
    /// (bandages, cures for bites). Ordered at a tool bench in a crafting room (a walled room with a door), made there by
    /// crafter villagers.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Building/Crafting Config", fileName = "CraftingConfig")]
    public sealed class CraftingConfig : ScriptableObject
    {
        public CraftItem[] items = CraftItem.Defaults();
    }
}
