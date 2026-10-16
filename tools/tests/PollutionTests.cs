using System;
using System.Collections.Generic;
using System.Linq;
using KingdomGame.Automation;
using KingdomGame.Zombies;

// Tests for pollution (Assets/Scripts/Automation/PollutionMap.cs) and what it does to the hordes (Zombies/WaveMath.cs).
static partial class Tests
{
    static void RunPollution(PollutionMap map, float seconds, Action each = null)
    {
        for (float t = 0f; t < seconds; t += 1f)
        {
            each?.Invoke();
            map.Tick(1f);
        }
    }

    // A factory's cloud spreads out and stops growing where what the ground absorbs matches what's made.
    static void PollutionCloudsSpreadAndSettle()
    {
        var map = new PollutionMap { Absorb = 0.05f };
        RunPollution(map, 1500f, () => map.Emit(10f, 10f, 2f));
        float settled = map.Total;
        int chunks = map.ChunkCount;
        RunPollution(map, 300f, () => map.Emit(10f, 10f, 2f));
        Check(Math.Abs(map.Total - settled) < settled * 0.1f, $"it stops growing ({settled:0} then {map.Total:0})");
        Check(chunks >= 15 && chunks <= 60, $"2 a second against 0.05 a square settles over about 40 squares (got {chunks})");
        float middle = map.At(0, 0);
        Check(map.Chunks().All(c => c.Amount <= middle + 1e-3f), "the factory's own square has the most");
        Check(map.At(1, 0) > 0f && map.At(-1, 0) > 0f && map.At(0, 1) > 0f && map.At(0, -1) > 0f, "it spread every way");
        Check(Math.Abs(map.Produced - 2.0 * 1800) < 1.0, "it counts all that was ever made");
        // Stopping the factory: the cloud goes in time.
        RunPollution(map, 3000f);
        Check(map.Total < 1f && map.ChunkCount == 0, $"with nothing made, it's all absorbed (left {map.Total:0.0})");
    }

    // Spreading only moves pollution about (nothing made or lost), and thin pollution doesn't spread.
    static void PollutionSpreadsOnlyWhenThick()
    {
        var map = new PollutionMap { Absorb = 0f };
        map.Emit(5f, 5f, 1000f);
        RunPollution(map, 200f);
        Check(Math.Abs(map.Total - 1000f) < 0.5f && Math.Abs(map.Chunks().Sum(c => c.Amount) - 1000f) < 0.5f, "spreading neither makes nor loses any");
        Check(map.ChunkCount > 9, "1000 in one square spreads over many");
        Check(map.Chunks().All(c => c.Amount <= map.At(0, 0) + 1e-3f), "still most where it started");
        var thin = new PollutionMap { Absorb = 0f };
        thin.Emit(0f, 0f, 10f);
        RunPollution(thin, 100f);
        Check(thin.ChunkCount == 1 && Math.Abs(thin.At(0, 0) - 10f) < 1e-3f, "10 (below the 15 it takes to spread) stays put");
    }

    // Trees absorb pollution: a cloud reaches less far over a forest. Bases breathe it in.
    static void TreesAndBasesTakePollution()
    {
        var open = new PollutionMap { Absorb = 0.05f };
        var forest = new PollutionMap { Absorb = 0.05f, ExtraAbsorb = (cx, cy) => cx > 0 ? 0.5f : 0f };
        RunPollution(open, 1500f, () => open.Emit(16f, 16f, 2f));
        RunPollution(forest, 1500f, () => forest.Emit(16f, 16f, 2f));
        float openEast = open.Chunks().Where(c => c.X > 0).Sum(c => c.Amount), forestEast = forest.Chunks().Where(c => c.X > 0).Sum(c => c.Amount);
        Check(forestEast < openEast * 0.5f, $"the forest to the east takes in most of what reaches it ({forestEast:0} against {openEast:0} over open ground)");
        Check(forest.Total < open.Total, "the whole cloud is smaller");
        float before = open.At(0, 0);
        float taken = open.Take(0, 0, 30f);
        Check(Math.Abs(taken - 30f) < 1e-3f && Math.Abs(open.At(0, 0) - (before - 30f)) < 1e-2f, "a base takes what it breathes in");
        Check(open.Take(40, 40, 30f) == 0f, "nothing to take where there's none");
        var small = new PollutionMap();
        small.Emit(0f, 0f, 5f);
        Check(Math.Abs(small.Take(0, 0, 30f) - 5f) < 1e-3f && small.ChunkCount == 0, "it takes no more than there is");
    }

    // A save puts the cloud back exactly.
    static void PollutionIsSaved()
    {
        var map = new PollutionMap();
        RunPollution(map, 400f, () => map.Emit(-40f, 70f, 3f));
        var xs = new List<int>();
        var ys = new List<int>();
        var values = new List<float>();
        map.Save(xs, ys, values, out double produced);
        var loaded = new PollutionMap();
        loaded.Load(xs, ys, values, produced);
        Check(Math.Abs(loaded.Total - map.Total) < 0.01f && loaded.ChunkCount == map.ChunkCount && loaded.Produced == map.Produced, "the same cloud, and all ever made");
        foreach (var c in map.Chunks()) Check(Math.Abs(loaded.At(c.X, c.Y) - c.Amount) < 1e-3f, $"square {c.X},{c.Y} differs");
        loaded.Load(null, null, null, 0);
        Check(loaded.Total == 0f && loaded.ChunkCount == 0, "an older save has none");
    }

    // More pollution: bigger hordes (up to a limit), zombies further on, and bases send a group for what they breathe in.
    static void PollutionMakesHordesHarder()
    {
        Check(WaveMath.PollutionWaves(0f, 5000f, 4f) == 1f, "no pollution: waves as before");
        Check(Math.Abs(WaveMath.PollutionWaves(5000f, 5000f, 4f) - 2f) < 1e-4f, "5000 in the air: twice as big");
        Check(WaveMath.PollutionWaves(1e9f, 5000f, 4f) == 4f, "never more than four times");
        float last = 0f;
        for (int p = 0; p < 30000; p += 1000)
        {
            float m = WaveMath.PollutionWaves(p, 5000f, 4f);
            Check(m >= last, "more pollution never makes waves smaller");
            last = m;
        }
        Check(WaveMath.Evolution(0, 4000f, 20) == 0 && WaveMath.Evolution(12500, 4000f, 20) == 3 && WaveMath.Evolution(1e12, 4000f, 20) == 20, "evolution: a wave further for each 4000 made, up to 20");
        Check(WaveMath.AttackGroup(250f, 40f) == 6 && WaveMath.AttackGroup(10f, 40f) == 0, "a base sends one zombie for each 40 it breathes in");
        // Craftables pollute four times as much as other recipes.
        var craftables = RecipeBook.Defaults().Where(r => KingdomGame.Economy.ResourceText.IsCraftable(r.output)).ToList();
        Check(craftables.Count == KingdomGame.Economy.ResourceText.Craftables, "a recipe for every Craftable");
        Check(craftables.All(r => r.pollution > 1f) && RecipeBook.Defaults().Where(r => !KingdomGame.Economy.ResourceText.IsCraftable(r.output)).All(r => r.pollution == 1f),
            "Craftables pollute more than anything else");
        for (int age = 1; age <= KingdomGame.Economy.ResourceText.Craftables; age++)
        {
            Check(KingdomGame.Economy.ResourceText.CraftableOf(age, out var t), $"age {age} has a Craftable");
            var r = craftables.First(c => c.output == t);
            Check(r.age == age, $"{r.name} is made from its own age");
            Check(r.inputs.Length >= 3, $"{r.name} takes at least three kinds of thing (it's hard to make)");
        }
        Check(!KingdomGame.Economy.ResourceText.CraftableOf(0, out _) && !KingdomGame.Economy.ResourceText.CraftableOf(7, out _), "no Craftable in the Stone Age or World War 2");
    }
}
