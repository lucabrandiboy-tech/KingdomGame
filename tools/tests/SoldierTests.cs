using System;
using KingdomGame.Villagers;

// Tests for soldiers: ranks from experience, their health and how fast they attack.
static partial class Tests
{
    static readonly float[] RankMarks = { 20f, 60f, 140f, 300f };

    static void RanksComeWithExperience()
    {
        Check(SoldierMath.Rank(0f, RankMarks) == 0, "a new soldier is a recruit");
        Check(SoldierMath.Rank(19.9f, RankMarks) == 0, "just short of the first mark: still a recruit");
        Check(SoldierMath.Rank(20f, RankMarks) == 1, "the first mark makes a private");
        Check(SoldierMath.Rank(139f, RankMarks) == 2, "a corporal below the sergeant's mark");
        Check(SoldierMath.Rank(1e6f, RankMarks) == SoldierMath.TopRank, "never above captain");
        Check(SoldierMath.Rank(1e6f, new[] { 1f, 2f, 3f, 4f, 5f, 6f }) == SoldierMath.TopRank, "extra marks don't make ranks that don't exist");
        Check(SoldierMath.Rank(50f, null) == 0, "no marks: everyone stays a recruit");
        Check(SoldierMath.RankName(1) == "Private" && SoldierMath.RankName(99) == "Captain" && SoldierMath.RankName(-3) == "Recruit", "rank names");
        Check(Math.Abs(SoldierMath.ToNext(50f, RankMarks) - 10f) < 1e-4f, "10 more to corporal");
        Check(SoldierMath.ToNext(400f, RankMarks) < 0f, "nothing after captain");
    }

    static void SoldiersGetTougherAndFaster()
    {
        Check(Math.Abs(SoldierMath.MaxHealth(60f, 40f, 0f, 0, 8f) - 100f) < 1e-4f, "a recruit without armor: 100");
        Check(Math.Abs(SoldierMath.MaxHealth(60f, 40f, 50f, 4, 8f) - 182f) < 1e-4f, "a captain in plate: 182");
        float recruit = SoldierMath.AttackGap(2f, 0, 0.1f), captain = SoldierMath.AttackGap(2f, 4, 0.1f);
        Check(Math.Abs(recruit - 0.5f) < 1e-5f, "2 attacks a second: half a second apart");
        Check(captain < recruit && Math.Abs(captain - 0.5f / 1.4f) < 1e-5f, "a captain attacks 40% faster");
        Check(SoldierMath.AttackGap(0f, 0, 0.1f) <= 10f, "a weapon with no rate still attacks now and then");
    }
}
