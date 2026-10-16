using System;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Combat
{
    /// <summary>One weapon the ruler can have: its age, what it costs to make and how long crafters work on it.</summary>
    [Serializable]
    public sealed class ArmsItem
    {
        public string displayName = "Weapon";
        [TextArea(1, 3)] public string description = "";
        [Tooltip("Its picture in the ruler's hand (a name in WeaponArt).")]
        public string art = "Club";
        [Tooltip("The age it can be made in (0 = Stone Age): the kingdom has to have reached it.")]
        [Range(0, 7)] public int age;
        [Tooltip("Fired with the left mouse button (switch with Q or 1-9).")]
        public WeaponConfig weapon;
        [Tooltip("Instead fired with the right mouse button whatever is in hand (the bazooka).")]
        public bool heavy;
        [Tooltip("Instead thrown with G (dynamite).")]
        public ExplosiveConfig explosive;
        [Tooltip("The ruler has it from the start.")]
        public bool startsOwned;
        [Min(0)] public int woodCost, stoneCost, foodCost, copperCost, ironCost, goldCost;
        [Tooltip("Seconds of a crafter's work at an anvil.")]
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
    }

    /// <summary>
    /// A suit of armor, bought at an armory room's weapon rack or armor stand (E) for the ruler or a soldier: it takes
    /// <see cref="armor"/> off every hit (a hit always does at least a fifth of its damage) and adds to their health.
    /// </summary>
    [Serializable]
    public sealed class ArmorItem
    {
        public string displayName = "Armor";
        [TextArea(1, 3)] public string description = "";
        [Tooltip("The age it can be bought in (0 = Stone Age): the kingdom has to have reached it.")]
        [Range(0, 7)] public int age;
        [Tooltip("How a soldier wearing it looks: 0 a tunic, 1 leather or padding, 2 mail, 3 plate, 4 a modern uniform and helmet.")]
        [Range(0, 4)] public int look = 1;
        [Tooltip("Taken off every hit (a zombie's claw does 6-14, a brute 28, a giant 50).")]
        [Min(0)] public float armor = 1f;
        [Tooltip("Hit points it adds.")]
        [Min(0)] public float health = 15f;
        [Min(0)] public int woodCost, stoneCost, foodCost, copperCost, ironCost, goldCost, steelCost;

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

        public string CostText(int times = 1)
        {
            var parts = new System.Collections.Generic.List<string>();
            for (int k = 0; k < Inventory.TypeCount; k++)
            {
                int n = Cost((ResourceType)k) * times;
                if (n > 0) parts.Add($"{n} {ResourceText.Name((ResourceType)k)}");
            }
            return parts.Count > 0 ? string.Join(", ", parts) : "free";
        }

        static ArmorItem Make(string name, string about, int age, int look, float armor, float health,
            int wood = 0, int food = 0, int copper = 0, int iron = 0, int gold = 0, int steel = 0) => new ArmorItem
        {
            displayName = name, description = about, age = age, look = look, armor = armor, health = health,
            woodCost = wood, foodCost = food, copperCost = copper, ironCost = iron, goldCost = gold, steelCost = steel,
        };

        /// <summary>The armor for each age (Build Game Scenes puts these in the Armory Config; older settings get them too).</summary>
        public static ArmorItem[] Defaults() => new[]
        {
            Make("Padded jerkin", "Layers of hide and cloth: a little softer to hit.", 0, 1, 1f, 15f, wood: 10, food: 40),
            Make("Bronze scales", "Bronze plates sewn on leather.", 1, 2, 2f, 25f, food: 30, copper: 60),
            Make("Chainmail", "Thousands of iron rings: claws slide off.", 2, 2, 3f, 35f, copper: 20, iron: 90),
            Make("Plate armor", "A knight's steel shell, head to toe.", 3, 3, 4f, 50f, iron: 160, gold: 10),
            Make("Breastplate and morion", "A pikeman's steel chest and helmet.", 4, 3, 4f, 60f, iron: 140, gold: 20, steel: 20),
            Make("Steel cuirass", "Polished steel over a wool coat.", 5, 3, 5f, 70f, iron: 100, gold: 30, steel: 60),
            Make("Trench gear", "A steel helmet and a thick greatcoat.", 6, 4, 5f, 85f, iron: 80, gold: 40, steel: 100),
            Make("Flak vest", "Plates and padding round the chest, and a helmet.", 7, 4, 6f, 100f, gold: 50, steel: 160),
        };
    }

    /// <summary>
    /// The ruler's weapons through the ages, made at an anvil by crafter villagers, and the armor you buy at an armory
    /// room for yourself and your soldiers.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Combat/Armory Config", fileName = "ArmoryConfig")]
    public sealed class ArmoryConfig : ScriptableObject
    {
        public ArmsItem[] items = new ArmsItem[0];
        [Tooltip("Armor for the ruler and soldiers, bought at a weapon rack or armor stand in an armory room (a walled room with a door).")]
        public ArmorItem[] armor = ArmorItem.Defaults();
        [Tooltip("A copy of one of your weapons for a soldier costs this share of what it cost to make (bought at an armory room).")]
        [Range(0f, 2f)] public float soldierWeaponCost = 0.5f;
    }
}
