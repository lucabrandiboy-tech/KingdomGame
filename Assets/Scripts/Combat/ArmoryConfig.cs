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

    /// <summary>The ruler's weapons through the ages, made at an anvil by crafter villagers.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Combat/Armory Config", fileName = "ArmoryConfig")]
    public sealed class ArmoryConfig : ScriptableObject
    {
        public ArmsItem[] items = new ArmsItem[0];
    }
}
