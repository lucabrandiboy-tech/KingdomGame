using System;
using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Economy;
using UnityEngine;

// Plain asserts, like tools/tests.
static class Tests
{
    static int failed;

    static int Main()
    {
        Run(nameof(SameSeedSameWorldEverywhere), SameSeedSameWorldEverywhere);
        Run(nameof(TheEdgeIsSolid), TheEdgeIsSolid);
        Run(nameof(OldStumpsAreGoneWhenLoaded), OldStumpsAreGoneWhenLoaded);
        Run(nameof(ChangesSurviveSavingAndLoading), ChangesSurviveSavingAndLoading);
        Run(nameof(ForgettingKeepsChangesAndShownSquares), ForgettingKeepsChangesAndShownSquares);
        Run(nameof(SavingWhileListenersLookElsewhere), SavingWhileListenersLookElsewhere);
        Run(nameof(GrowingBackFinishesWhileNobodyIsNear), GrowingBackFinishesWhileNobodyIsNear);
        Run(nameof(FindNearestAcrossSquares), FindNearestAcrossSquares);
        Run(nameof(GroundSquaresJoinUp), GroundSquaresJoinUp);
        Run(nameof(GroundTintJoinsUp), GroundTintJoinsUp);
        Run(nameof(AGroundSquareIsQuickToMake), AGroundSquareIsQuickToMake);
        Console.WriteLine(failed == 0 ? "All world tests passed." : $"{failed} world test(s) FAILED.");
        return failed == 0 ? 0 : 1;
    }

    static void Run(string name, Action test)
    {
        try
        {
            Time.time = 0f;
            Time.frameCount = 0;
            test();
            Console.WriteLine($"  ok    {name}");
        }
        catch (Exception e)
        {
            failed++;
            Console.WriteLine($"  FAIL  {name}: {e.Message}");
        }
    }

    static void Check(bool condition, string what)
    {
        if (!condition) throw new Exception(what);
    }

    // ------------------------------------------------------------ helpers --

    static readonly ResourceNodeConfig Tree = new ResourceNodeConfig { name = "Tree", look = NodeLook.Tree, resource = ResourceType.Wood, amount = 60 };
    static readonly ResourceNodeConfig Rock = new ResourceNodeConfig { name = "Rock", look = NodeLook.Rock, resource = ResourceType.Stone, amount = 40, regrowSeconds = 0 };
    static readonly ResourceNodeConfig Bush = new ResourceNodeConfig { name = "Bush", look = NodeLook.BerryBush, resource = ResourceType.Food, amount = 20, regrowSeconds = 120 };

    // A made-up world: about one tile in 9 has something, the same every time for the same seed.
    static ResourceNodeConfig Generate(uint seed, int x, int y, out int variant)
    {
        uint h = KingdomGame.Destruction.DamageShape.Hash(seed, x, y);
        variant = (int)(h % 4);
        switch (h % 27)
        {
            case 0: case 1: return Tree;
            case 2: return Rock;
            case 3: return Bush;
            default: return null;
        }
    }

    static NatureMap NewMap(uint seed = 7) => new NatureMap(seed, (int x, int y, out int v) => Generate(seed, x, y, out v));

    // ------------------------------------------------------------- nature --

    // Every cell is what the generator says, whichever square it falls in (negative ones too), and a
    // second map from the same seed agrees, read in another order.
    static void SameSeedSameWorldEverywhere()
    {
        var a = NewMap();
        var b = NewMap();
        var cells = new List<Vector2Int>();
        foreach (int x in new[] { -32001 + 2, -1000, -65, -64, -33, -32, -31, -1, 0, 1, 31, 32, 33, 63, 64, 999, 31998 })
            foreach (int y in new[] { -40, -33, -32, -1, 0, 31, 32, 500 })
                cells.Add(new Vector2Int(x, y));
        foreach (var c in cells)
        {
            var want = Generate(7, c.x, c.y, out int variant);
            Check(a.KindAt(c) == want, $"{c}: {a.KindAt(c)?.name ?? "nothing"}, the generator says {want?.name ?? "nothing"}");
            if (want != null) Check(a.VariantAt(c) == variant, $"{c}: wrong variant");
            Check(a.Blocks(c) == (want != null), $"{c}: blocks is wrong");
        }
        cells.Reverse();
        foreach (var c in cells) Check(b.KindAt(c) == a.KindAt(c), $"{c}: the second map differs");
    }

    // A save from before stumps were taken away: a cut-down wild tree comes back as nothing (not a solid stump).
    static void OldStumpsAreGoneWhenLoaded()
    {
        var map = NewMap();
        var cell = FirstWith(map, Tree, 40);
        map.Apply(new NatureMap.Change { Cell = cell, Kind = Tree, Left = 0, Grown = -1f, RegrowLeft = -1f });
        Check(map.KindAt(cell) == null && !map.Blocks(cell), "the old stump is still there");
        var changes = map.Changes();
        Check(changes.Count == 1 && changes[0].Kind == null, "the stump's removal isn't saved");
    }

    static void TheEdgeIsSolid()
    {
        var map = NewMap();
        int edge = NatureMap.Limit;
        Check(NatureMap.Inside(new Vector2Int(edge - 1, -(edge - 1))), "the last cell in isn't inside");
        Check(!NatureMap.Inside(new Vector2Int(edge, 0)) && !NatureMap.Inside(new Vector2Int(0, -edge)), "the edge cell is inside");
        Check(map.Blocks(new Vector2Int(edge, 5)) && map.Blocks(new Vector2Int(-edge - 10, 0)), "past the edge isn't solid");
        Check(map.KindAt(new Vector2Int(edge + 3, 0)) == null && !map.Standing(new Vector2Int(edge + 3, 0), out _), "something stands past the edge");
        map.Clear(new Vector2Int(edge + 3, 0));   // (does nothing, doesn't throw)
        Check(map.Plant(new Vector2Int(edge + 3, 0), Tree, 0, 10f) == null && map.Changes().Count == 0, "planted past the edge");
    }

    static Vector2Int FirstWith(NatureMap map, ResourceNodeConfig kind, int fromX, int y = 3)
    {
        for (int x = fromX; ; x++)
            if (map.KindAt(new Vector2Int(x, y)) == kind) return new Vector2Int(x, y);
    }

    static Vector2Int FirstEmpty(NatureMap map, int fromX, int y = 3)
    {
        for (int x = fromX; ; x++)
            if (map.KindAt(new Vector2Int(x, y)) == null) return new Vector2Int(x, y);
    }

    // A tree half cut, a rock cleared for a building, a young tree planted, a used-up bush growing back:
    // saved as changes, and a fresh map from the same seed with them put back is the same.
    static void ChangesSurviveSavingAndLoading()
    {
        var map = NewMap();
        Time.time = 100f;
        var cut = FirstWith(map, Tree, -300);
        var cleared = FirstWith(map, Rock, -200, -50);
        var planted = FirstEmpty(map, 4000, 4000);
        var bush = FirstWith(map, Bush, 77, -900);

        var node = new ResourceNode { Config = Tree };
        node.Bind(map, cut);
        node.Left = 25;                                  // gathered from while shown
        map.Clear(cleared);
        map.Plant(planted, Tree, 3, 50f);
        map.Record(bush, 0, false, 0f, 160f, true);      // used up: grows back at 160
        map.SetBlocks(bush, true);

        Time.time = 110f;
        var changes = map.Changes();                     // (asks the shown tree for its state first)
        Check(changes.Count == 4, $"{changes.Count} changes, not 4");

        var loaded = NewMap();
        Time.time = 0f;                                  // a new game's clock starts again
        foreach (var c in changes) loaded.Apply(c);
        Check(loaded.KindAt(cut) == Tree && loaded.Standing(cut, out _), "the cut tree is gone");
        loaded.StateOf(cut, out int left, out _, out _, out _);
        Check(left == 25, $"the cut tree has {left} left, not 25");
        Check(loaded.KindAt(cleared) == null && !loaded.Blocks(cleared), "the cleared rock came back");
        Check(loaded.KindAt(planted) == Tree && loaded.IsPlanted(planted) && loaded.VariantAt(planted) == 3, "the planted tree isn't there");
        loaded.StateOf(planted, out int young, out bool growing, out float start, out float end);
        Check(growing && young == 0 && Math.Abs((end - start) - 50f) < 1e-3 && Math.Abs(-start - 10f) < 1e-3,
            $"the young tree isn't 10 of 50 seconds grown (start {start}, end {end})");
        loaded.StateOf(bush, out int berries, out bool bushGrowing, out _, out float back);
        Check(berries == 0 && !bushGrowing && Math.Abs(back - 50f) < 1e-3, $"the bush should grow back in 50 s (at {back})");
        // Untouched cells are as the world made them.
        var other = FirstWith(loaded, Tree, 9000);
        Check(!loaded.IsPlanted(other) && loaded.Standing(other, out _), "an untouched tree changed");
        // And saving the loaded one again gives the same four.
        Check(loaded.Changes().Count == 4, "saving the loaded world again gives different changes");
    }

    // Walking far keeps making new squares; old ones are forgotten, but never one with a change or a shown tree.
    static void ForgettingKeepsChangesAndShownSquares()
    {
        var map = NewMap();
        var cut = FirstWith(map, Tree, 0);
        map.Clear(cut);
        var shown = FirstWith(map, Rock, 64);
        var node = new ResourceNode { Config = Rock };
        node.Bind(map, shown);
        for (int i = 0; i < 9000; i++)
        {
            Time.frameCount = i;
            // A new square each time (rows of 1000, all well inside the world).
            map.Blocks(new Vector2Int(-20000 + (i % 1000) * NatureMap.ChunkTiles, 12345 + (i / 1000) * NatureMap.ChunkTiles));
        }
        Check(map.Squares <= 6001 && map.Squares > 1000, $"{map.Squares} squares kept: they aren't forgotten");
        Check(map.KindAt(cut) == null, "the cleared tree came back after its square was forgotten");
        Check(map.Live(shown) == node, "the shown rock's square was forgotten");
        node.Left = 7;
        node.WriteBack();
        map.StateOf(shown, out int left, out _, out _, out _);
        Check(left == 7, "the shown rock's state went to a forgotten square");
    }

    // Saving settles trees that finished growing back, which tells listeners (zombie paths); a listener
    // that looks at another square mustn't break the save.
    static void SavingWhileListenersLookElsewhere()
    {
        var map = NewMap();
        var bush = FirstWith(map, Bush, 0);
        map.Record(bush, 0, false, 0f, 5f, true);
        map.SetBlocks(bush, false);
        int far = 0;
        map.BlockingChanged += c => map.Blocks(new Vector2Int(c.x + 5000 + far++ * 40, c.y));
        Time.time = 10f;
        var changes = map.Changes();
        Check(changes.Count == 1 && changes[0].Left == Bush.amount && changes[0].RegrowLeft < 0f, "the bush didn't grow back when saved");
        Check(map.Blocks(bush), "the grown bush isn't solid again");
    }

    static void GrowingBackFinishesWhileNobodyIsNear()
    {
        var map = NewMap();
        Time.time = 0f;
        var young = FirstEmpty(map, 10, 10);
        map.Plant(young, Tree, 1, 30f);
        Check(!map.Standing(young, out _), "a young tree can be gathered");
        Check(map.Blocks(young), "a young tree isn't in the way");
        Time.time = 31f;
        Check(map.Standing(young, out var look) && look == NodeLook.Tree, "the tree didn't finish growing");
        map.StateOf(young, out int left, out bool growing, out _, out _);
        Check(left == Tree.amount && !growing, "the grown tree isn't full");
    }

    static void FindNearestAcrossSquares()
    {
        var map = NewMap();
        var from = new Vector2(-32.5f, -32.5f);   // a square's corner
        Check(map.FindNearest(from, ResourceType.Stone, 64, null, out var found), "no rock found");
        float best = float.MaxValue;
        for (int y = -100; y < 40; y++)
            for (int x = -100; x < 40; x++)
            {
                var c = new Vector2Int(x, y);
                if (map.KindAt(c) != Rock) continue;
                best = Math.Min(best, (new Vector2(x + 0.5f, y + 0.5f) - from).sqrMagnitude);
            }
        float d = (new Vector2(found.x + 0.5f, found.y + 0.5f) - from).sqrMagnitude;
        Check(Math.Abs(d - best) < 1e-3, $"found a rock {Math.Sqrt(d):0.0} away, the nearest is {Math.Sqrt(best):0.0}");
        Check(!map.FindNearest(from, ResourceType.Stone, 64, c => true, out _), "skipping everything still found one");
    }

    // ------------------------------------------------------------- ground --

    // Two squares side by side are the same, pixel for pixel, as one square twice as wide.
    static void GroundSquaresJoinUp()
    {
        const int detail = 8, tiles = 16;
        foreach (var (ox, oy) in new[] { (0, 0), (-64, -128), (30000, -31000) })
        {
            int n = tiles * detail, big = n * 2;
            var dryBig = new byte[big * big];
            var mudBig = new byte[big * big];
            GroundArt.PatchMasksAt(ox, oy, tiles * 2, detail, 99u, dryBig, mudBig);
            for (int py = 0; py < 2; py++)
                for (int px = 0; px < 2; px++)
                {
                    var dry = new byte[n * n];
                    var mud = new byte[n * n];
                    GroundArt.PatchMasksAt(ox + px * tiles, oy + py * tiles, tiles, detail, 99u, dry, mud);
                    int differ = 0;
                    for (int y = 0; y < n; y++)
                        for (int x = 0; x < n; x++)
                        {
                            int i = (py * n + y) * big + px * n + x;
                            if (Math.Abs(dry[y * n + x] - dryBig[i]) > 1 || Math.Abs(mud[y * n + x] - mudBig[i]) > 1) differ++;
                        }
                    Check(differ == 0, $"square {px},{py} at {ox},{oy}: {differ} mask pixels differ from the big one");
                }
            int some = 0;
            foreach (var b in dryBig) if (b > 128) some++;
            Check(some > 0 && some < dryBig.Length, $"at {ox},{oy} the dry grass mask is all one thing");
        }
    }

    static void GroundTintJoinsUp()
    {
        const int detail = 2, tiles = 8;
        Func<float, float, float> forest = (x, y) => Noise.Fractal(x / 14f, y / 14f, 5u, 3);
        int n = tiles * detail, big = n * 2;
        var whole = new Color32[big * big];
        GroundArt.TintAt(-8, -8, tiles * 2, detail, 5u, forest, 0.6f, 0.5f, whole);
        var part = new Color32[n * n];
        GroundArt.TintAt(0, -8, tiles, detail, 5u, forest, 0.6f, 0.5f, part);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                var a = part[y * n + x];
                var b = whole[y * big + n + x];
                Check(a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a, $"tint pixel {x},{y} differs");
            }
    }

    // GroundStreamer's square (64 tiles, masks at 8 pixels a tile, tint at 2), on one thread.
    static void AGroundSquareIsQuickToMake()
    {
        Func<float, float, float> forest = (x, y) => Noise.Fractal(x / 14f, y / 14f, 5u, 3);
        var dry = new byte[512 * 512];
        var mud = new byte[512 * 512];
        var tint = new Color32[128 * 128];
        GroundArt.PatchMasksAt(0, 0, 64, 8, 5u, dry, mud);   // (warm up)
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 1; i <= 4; i++)
        {
            GroundArt.PatchMasksAt(i * 64, -i * 64, 64, 8, 5u, dry, mud);
            GroundArt.TintAt(i * 64, -i * 64, 64, 2, 5u, forest, 0.6f, 0.5f, tint);
        }
        double ms = watch.Elapsed.TotalMilliseconds / 4;
        Console.WriteLine($"        (one ground square takes {ms:0} ms)");
        Check(ms < 400, $"one ground square takes {ms:0} ms");
    }
}
