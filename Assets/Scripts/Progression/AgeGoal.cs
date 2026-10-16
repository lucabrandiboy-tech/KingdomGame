using System;
using KingdomGame.Building;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Progression
{
    /// <summary>What a goal asks for (see <see cref="AgeGoal"/>).</summary>
    public enum GoalKind
    {
        /// <summary>Have this many villagers.</summary>
        Villagers,
        /// <summary>Have this much of <see cref="AgeGoal.resource"/> in storage.</summary>
        Stock,
        /// <summary>Have this many of <see cref="AgeGoal.building"/> built (none set: anything built).</summary>
        Built,
        /// <summary>Have this many tiles of wall.</summary>
        WallTiles,
        /// <summary>Have this many rooms (walled in, with a door).</summary>
        Rooms,
        /// <summary>Have this many towers.</summary>
        Towers,
        /// <summary>Kill this many zombies (all game long).</summary>
        Kills,
        /// <summary>Kill this many bosses (Warlords).</summary>
        Bosses,
        /// <summary>Live through this many nights.</summary>
        Nights,
        /// <summary>Survive this many waves (survival only).</summary>
        Waves,
        /// <summary>Search this many chests and crates in ruins.</summary>
        Ruins,
        /// <summary>Hunt this many deer (you or your hunters).</summary>
        Deer,
    }

    /// <summary>One goal of an age, and what it gives when it's done.</summary>
    [Serializable]
    public sealed class AgeGoal
    {
        [Tooltip("What it says on screen, like \"Have 6 villagers\".")]
        public string text = "";
        public GoalKind kind;
        [Min(1)] public int amount = 1;
        [Tooltip("Stock: which resource.")]
        public ResourceType resource;
        [Tooltip("Built: what has to be built (none: anything).")]
        public BuildingConfig building;

        [Header("Reward")]
        [Min(0)] public int wood;
        [Min(0)] public int stone;
        [Min(0)] public int food;
        [Tooltip("And this much of another resource...")]
        public ResourceType extra = ResourceType.Iron;
        [Min(0)] public int extraAmount;
        [Tooltip("...and experience (villager upgrades: V).")]
        [Min(0)] public int xp;

        public AgeGoal() { }

        public AgeGoal(string text, GoalKind kind, int amount, int wood = 0, int stone = 0, int food = 0, int xp = 0,
            ResourceType extra = ResourceType.Iron, int extraAmount = 0, ResourceType resource = ResourceType.Wood)
        {
            this.text = text; this.kind = kind; this.amount = amount; this.resource = resource;
            this.wood = wood; this.stone = stone; this.food = food; this.xp = xp; this.extra = extra; this.extraAmount = extraAmount;
        }

        /// <summary>The reward, like "100 stone, 30 XP".</summary>
        public string Reward()
        {
            var parts = new System.Collections.Generic.List<string>(5);
            if (wood > 0) parts.Add($"{wood} wood");
            if (stone > 0) parts.Add($"{stone} stone");
            if (food > 0) parts.Add($"{food} food");
            if (extraAmount > 0) parts.Add($"{extraAmount} {ResourceText.Name(extra)}");
            if (xp > 0) parts.Add($"{xp} XP");
            return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
        }
    }
}
