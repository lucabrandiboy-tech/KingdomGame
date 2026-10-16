using System;

namespace KingdomGame.Villagers
{
    /// <summary>
    /// Soldiers' ranks, health and attack speed. Plain C# with no Unity code, so it's unit-tested (tools/tests).
    /// </summary>
    public static class SoldierMath
    {
        public static readonly string[] RankNames = { "Recruit", "Private", "Corporal", "Sergeant", "Captain" };
        public static int TopRank => RankNames.Length - 1;

        /// <summary>
        /// The rank (0 = Recruit) for <paramref name="xp"/> experience: one more for each mark in <paramref name="rankXp"/>
        /// (the experience for Private, Corporal, ...) reached, in order.
        /// </summary>
        public static int Rank(float xp, float[] rankXp)
        {
            int rank = 0;
            if (rankXp == null) return 0;
            for (int i = 0; i < rankXp.Length && rank < TopRank; i++)
            {
                if (xp < rankXp[i]) break;
                rank = i + 1;
            }
            return rank;
        }

        public static string RankName(int rank) => RankNames[Math.Max(0, Math.Min(TopRank, rank))];

        /// <summary>Experience still needed for the next rank, or -1 at the top.</summary>
        public static float ToNext(float xp, float[] rankXp)
        {
            int rank = Rank(xp, rankXp);
            if (rankXp == null || rank >= rankXp.Length || rank >= TopRank) return -1f;
            return rankXp[rank] - xp;
        }

        /// <summary>A soldier's hit points: a villager's, a soldier's extra, its armor's and each rank's.</summary>
        public static float MaxHealth(float villager, float soldier, float armor, int rank, float perRank) =>
            villager + soldier + armor + Math.Max(0, rank) * perRank;

        /// <summary>Seconds between a soldier's attacks: its weapon's rate (attacks a second), faster for each rank.</summary>
        public static float AttackGap(float fireRate, int rank, float perRank) =>
            1f / (Math.Max(0.1f, fireRate) * (1f + Math.Max(0, rank) * perRank));
    }
}
