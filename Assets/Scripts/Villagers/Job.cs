using KingdomGame.Economy;

namespace KingdomGame.Villagers
{
    /// <summary>What a villager does all day (set in the villagers panel, V). New ones go at the end: saves store the number.</summary>
    public enum Job
    {
        /// <summary>No job: hangs about until given one.</summary>
        Idle,
        /// <summary>Brings resources to blueprints and builds them.</summary>
        Builder,
        /// <summary>Chops trees and carries the wood to storage.</summary>
        Woodcutter,
        /// <summary>Breaks rocks and carries the stone to storage.</summary>
        Miner,
        /// <summary>Picks berries and carries the food to storage.</summary>
        Forager,
        /// <summary>Stands on a tower and works it (archer towers, ballistas, cannons). Doesn't run from zombies.</summary>
        Guard,
        /// <summary>Makes the weapons the ruler orders, at an anvil.</summary>
        Crafter,
    }

    public static class Jobs
    {
        /// <summary>The jobs in the villagers panel, in order.</summary>
        public static readonly Job[] Listed = { Job.Builder, Job.Woodcutter, Job.Miner, Job.Forager, Job.Guard, Job.Crafter };

        public static string Name(Job job, bool plural = false)
        {
            switch (job)
            {
                case Job.Builder: return plural ? "Builders" : "Builder";
                case Job.Woodcutter: return plural ? "Woodcutters" : "Woodcutter";
                case Job.Miner: return plural ? "Miners" : "Miner";
                case Job.Forager: return plural ? "Foragers" : "Forager";
                case Job.Guard: return plural ? "Guards" : "Guard";
                case Job.Crafter: return plural ? "Crafters" : "Crafter";
                default: return "Idle";
            }
        }

        /// <summary>What a gathering job brings in, or false for the other jobs.</summary>
        public static bool Gathers(Job job, out ResourceType type)
        {
            switch (job)
            {
                case Job.Woodcutter: type = ResourceType.Wood; return true;
                case Job.Miner: type = ResourceType.Stone; return true;
                case Job.Forager: type = ResourceType.Food; return true;
                default: type = default; return false;
            }
        }

        /// <summary>What a gathering job does, for the panel.</summary>
        public static string Describe(Job job)
        {
            switch (job)
            {
                case Job.Builder: return "build what you plan from the throne";
                case Job.Woodcutter: return "chop trees (planted ones first)";
                case Job.Miner: return "break rocks for stone";
                case Job.Forager: return "pick berries for food";
                case Job.Guard: return "man the towers (archer tower, ballista, cannon)";
                case Job.Crafter: return "make the weapons you order at an anvil (E at one)";
                default: return "";
            }
        }
    }
}
