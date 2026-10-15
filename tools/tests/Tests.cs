using System;
using System.Collections.Generic;
using System.Linq;
using KingdomGame.Destruction;

// Small self-contained test runner: no test framework needed.
static partial class Tests
{
    static int failures;

    static int Main()
    {
        Run(nameof(FloorDivRoundsDown), FloorDivRoundsDown);
        Run(nameof(HashIsStableAndSpreads), HashIsStableAndSpreads);
        Run(nameof(SmoothDamageIsAPerfectDisc), SmoothDamageIsAPerfectDisc);
        Run(nameof(DamageFallsOffFromCenter), DamageFallsOffFromCenter);
        Run(nameof(JaggedDamageIsDeterministicAndInsideRadius), JaggedDamageIsDeterministicAndInsideRadius);
        Run(nameof(MaxJaggednessKeepsCenter), MaxJaggednessKeepsCenter);
        Run(nameof(BlockyNoiseWorksAtNegativeCoordinates), BlockyNoiseWorksAtNegativeCoordinates);
        Run(nameof(RectanglesCoverExactlyTheSolidPixels), RectanglesCoverExactlyTheSolidPixels);
        Run(nameof(FullAndEmptyMasks), FullAndEmptyMasks);
        Run(nameof(IslandsSplitCorrectly), IslandsSplitCorrectly);
        Run(nameof(IslandsMatchBruteForceOnRandomMasks), IslandsMatchBruteForceOnRandomMasks);
        Run(nameof(RayHitsFirstSolidPixel), RayHitsFirstSolidPixel);
        Run(nameof(RayPassesThroughHole), RayPassesThroughHole);
        Run(nameof(RayStartingInsideWallHitsImmediately), RayStartingInsideWallHitsImmediately);
        Run(nameof(RayWorksWithNegativeAndDiagonalDirections), RayWorksWithNegativeAndDiagonalDirections);
        Run(nameof(RayMatchesBruteForceSampling), RayMatchesBruteForceSampling);

        Run(nameof(PathStraightAndDiagonal), PathStraightAndDiagonal);
        Run(nameof(PathGoesAroundAWall), PathGoesAroundAWall);
        Run(nameof(PathNeverCutsCorners), PathNeverCutsCorners);
        Run(nameof(PathFailsWhenEnclosed), PathFailsWhenEnclosed);
        Run(nameof(PathEndsNextToABuilding), PathEndsNextToABuilding);
        Run(nameof(PathFromGoalIsEmpty), PathFromGoalIsEmpty);
        Run(nameof(PathIsShortestOnRandomGrids), PathIsShortestOnRandomGrids);
        Run(nameof(SmoothedPathsStayClear), SmoothedPathsStayClear);

        Run(nameof(FieldLeadsStraightToTarget), FieldLeadsStraightToTarget);
        Run(nameof(FieldGoesRoundAShortWall), FieldGoesRoundAShortWall);
        Run(nameof(FieldPrefersTheDoor), FieldPrefersTheDoor);
        Run(nameof(FieldBreaksInWhenThereIsNoDoor), FieldBreaksInWhenThereIsNoDoor);
        Run(nameof(FieldWalksTheLongWayRatherThanBreak), FieldWalksTheLongWayRatherThanBreak);
        Run(nameof(FieldCantCrossTheImpossible), FieldCantCrossTheImpossible);
        Run(nameof(FieldHeadStartMakesTargetsLessAttractive), FieldHeadStartMakesTargetsLessAttractive);
        Run(nameof(FieldMatchesDijkstraOnRandomGrids), FieldMatchesDijkstraOnRandomGrids);
        Run(nameof(WavesGrowAndBringNewKinds), WavesGrowAndBringNewKinds);

        Run(nameof(RoomsAreClosedAreas), RoomsAreClosedAreas);
        Run(nameof(RoomWithAGapIsNotARoom), RoomWithAGapIsNotARoom);
        Run(nameof(CornersCloseARoom), CornersCloseARoom);
        Run(nameof(HugeEnclosuresAreNotRooms), HugeEnclosuresAreNotRooms);

        Run(nameof(HungerEmptiesOverADay), HungerEmptiesOverADay);
        Run(nameof(BedsRestFasterThanTheGround), BedsRestFasterThanTheGround);
        Run(nameof(HungryAndTiredWorkSlower), HungryAndTiredWorkSlower);
        Run(nameof(VillagersSleepAtNightGuardsByDay), VillagersSleepAtNightGuardsByDay);
        Run(nameof(KitchenMealsTakeLessFood), KitchenMealsTakeLessFood);

        Run(nameof(EverySoundIsMadeCleanly), EverySoundIsMadeCleanly);
        Run(nameof(GroansAreAllDifferent), GroansAreAllDifferent);
        Run(nameof(LoopsJoinUpSeamlessly), LoopsJoinUpSeamlessly);

        Console.WriteLine(failures == 0 ? "All tests passed." : $"{failures} test(s) FAILED.");
        return failures == 0 ? 0 : 1;
    }

    static void Run(string name, Action test)
    {
        try { test(); Console.WriteLine($"  ok    {name}"); }
        catch (Exception e) { failures++; Console.WriteLine($"  FAIL  {name}: {e.Message}"); }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    // ------------------------------------------------------------ damage --

    static void FloorDivRoundsDown()
    {
        Check(DamageShape.FloorDiv(0, 32) == 0, "0/32");
        Check(DamageShape.FloorDiv(31, 32) == 0, "31/32");
        Check(DamageShape.FloorDiv(32, 32) == 1, "32/32");
        Check(DamageShape.FloorDiv(-1, 32) == -1, "-1/32");
        Check(DamageShape.FloorDiv(-32, 32) == -1, "-32/32");
        Check(DamageShape.FloorDiv(-33, 32) == -2, "-33/32");
    }

    static void HashIsStableAndSpreads()
    {
        Check(DamageShape.Hash(7, 3, -4) == DamageShape.Hash(7, 3, -4), "same input, same hash");
        var low = new HashSet<uint>();
        for (int i = 0; i < 1000; i++) low.Add(DamageShape.Hash(1, i, 0) & 255);
        Check(low.Count > 200, $"low byte should spread over 0..255, got {low.Count} values");
    }

    static DamageShapeParams Shape(int radius, int center = 200, int edge = 50, int jag = 0, int block = 1) =>
        new DamageShapeParams { RadiusPx = radius, CenterDamage = center, EdgeDamage = edge, Jaggedness = jag, NoiseBlock = block };

    static void SmoothDamageIsAPerfectDisc()
    {
        var output = new List<PixelDamage>();
        DamageShape.Compute(100, -50, Shape(5), 1, output);
        int expected = 0;
        for (int y = -5; y <= 5; y++)
            for (int x = -5; x <= 5; x++)
                if (x * x + y * y <= 25) expected++;
        Check(output.Count == expected, $"expected {expected} pixels, got {output.Count}");
        Check(output.All(p => (p.X - 100) * (p.X - 100) + (p.Y + 50) * (p.Y + 50) <= 25), "pixel outside radius");
        Check(output.Select(p => (p.X, p.Y)).Distinct().Count() == output.Count, "duplicate pixels");
    }

    static void DamageFallsOffFromCenter()
    {
        var output = new List<PixelDamage>();
        DamageShape.Compute(0, 0, Shape(10, 200, 50), 1, output);
        var center = output.Single(p => p.X == 0 && p.Y == 0);
        Check(center.Damage == 200, $"center damage {center.Damage}");
        var edge = output.Single(p => p.X == 10 && p.Y == 0);
        Check(edge.Damage == 50, $"edge damage {edge.Damage}");
        var mid = output.Single(p => p.X == 5 && p.Y == 0);
        Check(mid.Damage < 200 && mid.Damage > 50, $"middle damage {mid.Damage}");
    }

    static void JaggedDamageIsDeterministicAndInsideRadius()
    {
        var a = new List<PixelDamage>();
        var b = new List<PixelDamage>();
        var c = new List<PixelDamage>();
        DamageShape.Compute(40, 40, Shape(12, jag: 120), 1234, a);
        DamageShape.Compute(40, 40, Shape(12, jag: 120), 1234, b);
        DamageShape.Compute(40, 40, Shape(12, jag: 120), 999, c);
        Check(a.Count > 0 && a.SequenceEqual(b), "same event must give the same pixels");
        Check(!a.Select(p => (p.X, p.Y)).SequenceEqual(c.Select(p => (p.X, p.Y))), "different seed should change the edge");
        Check(a.All(p => (p.X - 40) * (p.X - 40) + (p.Y - 40) * (p.Y - 40) <= 144), "jagged pixel outside radius");

        var smooth = new List<PixelDamage>();
        DamageShape.Compute(40, 40, Shape(12), 1234, smooth);
        Check(a.Count < smooth.Count, "jagged hole should be smaller than the smooth one");
    }

    static void MaxJaggednessKeepsCenter()
    {
        var output = new List<PixelDamage>();
        DamageShape.Compute(3, 3, Shape(4, jag: 255), 5, output);
        Check(output.Any(p => p.X == 3 && p.Y == 3), "center pixel must always be hit");
        DamageShape.Compute(3, 3, Shape(0), 5, output);
        Check(output.Count == 1 && output[0].Damage == 200, "radius 0 hits only the center at full damage");
    }

    static void BlockyNoiseWorksAtNegativeCoordinates()
    {
        var output = new List<PixelDamage>();
        DamageShape.Compute(-37, -91, Shape(9, jag: 200, block: 4), 77, output);
        Check(output.Count > 0, "no pixels");
        Check(output.All(p => (p.X + 37) * (p.X + 37) + (p.Y + 91) * (p.Y + 91) <= 81), "outside radius");
    }

    // -------------------------------------------------------- rectangles --

    static void RectanglesCoverExactlyTheSolidPixels()
    {
        var random = new Random(42);
        var rects = new List<PixelRect>();
        for (int round = 0; round < 300; round++)
        {
            int w = random.Next(1, 40), h = random.Next(1, 40);
            var mask = new byte[w * h];
            double fill = random.NextDouble();
            for (int i = 0; i < mask.Length; i++) mask[i] = (byte)(random.NextDouble() < fill ? 1 + random.Next(255) : 0);

            PixelAlgorithms.Rectangles(mask, w, h, new bool[w * h], rects);

            var covered = new int[w * h];
            foreach (var r in rects)
            {
                Check(r.Width > 0 && r.Height > 0 && r.X >= 0 && r.Y >= 0 && r.X + r.Width <= w && r.Y + r.Height <= h, "rect out of bounds");
                for (int y = r.Y; y < r.Y + r.Height; y++)
                    for (int x = r.X; x < r.X + r.Width; x++)
                        covered[y * w + x]++;
            }
            for (int i = 0; i < mask.Length; i++)
                Check(covered[i] == (mask[i] != 0 ? 1 : 0), $"round {round}: pixel {i} covered {covered[i]} times, solid={mask[i] != 0}");
        }
    }

    static void FullAndEmptyMasks()
    {
        var rects = new List<PixelRect>();
        var full = Enumerable.Repeat((byte)1, 32 * 32).ToArray();
        PixelAlgorithms.Rectangles(full, 32, 32, new bool[1024], rects);
        Check(rects.Count == 1 && rects[0].Width == 32 && rects[0].Height == 32, "full chunk should be one 32x32 box");
        PixelAlgorithms.Rectangles(new byte[1024], 32, 32, new bool[1024], rects);
        Check(rects.Count == 0, "empty chunk should have no boxes");
    }

    // ----------------------------------------------------------- islands --

    static byte[] Parse(string[] rows, out int width, out int height)
    {
        // Rows are written top-down; the mask is bottom-up like the game.
        height = rows.Length;
        width = rows[0].Length;
        var mask = new byte[width * height];
        for (int r = 0; r < height; r++)
            for (int x = 0; x < width; x++)
                mask[(height - 1 - r) * width + x] = rows[r][x] == '#' ? (byte)1 : (byte)0;
        return mask;
    }

    static void IslandsSplitCorrectly()
    {
        var mask = Parse(new[]
        {
            "##..#",
            "##...",
            "...#.",
            "....#",
        }, out int w, out int h);
        var sizes = new List<int>();
        int count = PixelAlgorithms.Islands(mask, w, h, new int[w * h], new int[w * h], sizes);
        Check(count == 4, $"expected 4 pieces (corner-touching pixels are separate), got {count}");
        Check(sizes.OrderBy(s => s).SequenceEqual(new[] { 1, 1, 1, 4 }), "sizes " + string.Join(",", sizes));
    }

    static void IslandsMatchBruteForceOnRandomMasks()
    {
        var random = new Random(7);
        var sizes = new List<int>();
        for (int round = 0; round < 200; round++)
        {
            int w = random.Next(1, 50), h = random.Next(1, 50);
            var mask = new byte[w * h];
            for (int i = 0; i < mask.Length; i++) mask[i] = (byte)(random.Next(100) < 55 ? 1 : 0);
            var labels = new int[w * h];
            int count = PixelAlgorithms.Islands(mask, w, h, labels, new int[w * h], sizes);

            Check(sizes.Sum() == mask.Count(m => m != 0), "sizes must add up to the solid pixel count");
            for (int i = 0; i < mask.Length; i++)
            {
                Check((labels[i] >= 0) == (mask[i] != 0), "label/solid mismatch");
                int x = i % w, y = i / w;
                // Neighbours that are both solid must share a label.
                if (mask[i] != 0 && x + 1 < w && mask[i + 1] != 0) Check(labels[i] == labels[i + 1], "right neighbour split");
                if (mask[i] != 0 && y + 1 < h && mask[i + w] != 0) Check(labels[i] == labels[i + w], "upper neighbour split");
            }
            for (int island = 0; island < count; island++)
                Check(labels.Count(l => l == island) == sizes[island], "size mismatch");
        }
    }

    // ----------------------------------------------------------- raycast --

    sealed class Grid : IPixelSolid
    {
        readonly HashSet<(int, int)> solid = new HashSet<(int, int)>();
        public void Set(int x, int y) => solid.Add((x, y));
        public void Clear(int x, int y) => solid.Remove((x, y));
        public bool IsSolid(int x, int y) => solid.Contains((x, y));
    }

    static Grid VerticalWall(int x, int fromY, int toY)
    {
        var grid = new Grid();
        for (int y = fromY; y <= toY; y++) grid.Set(x, y);
        return grid;
    }

    static void RayHitsFirstSolidPixel()
    {
        var grid = VerticalWall(10, 0, 20);
        grid.Set(11, 5);
        Check(PixelAlgorithms.Raycast(grid, 0.5, 5.5, 20.5, 5.5, out int x, out int y, out double t), "should hit");
        Check(x == 10 && y == 5, $"hit ({x},{y})");
        Check(Math.Abs(t - 0.475) < 1e-9, $"t = {t}");
    }

    static void RayPassesThroughHole()
    {
        var grid = VerticalWall(10, 0, 20);
        grid.Clear(10, 5);
        Check(!PixelAlgorithms.Raycast(grid, 0.5, 5.5, 20.5, 5.5, out _, out _, out double t), "should fly through the hole");
        Check(t == 1, "t should be 1 on a miss");
        Check(PixelAlgorithms.Raycast(grid, 0.5, 6.5, 20.5, 6.5, out int x, out int y, out _) && x == 10 && y == 6, "row above the hole is still wall");
    }

    static void RayStartingInsideWallHitsImmediately()
    {
        var grid = VerticalWall(3, 0, 3);
        Check(PixelAlgorithms.Raycast(grid, 3.2, 1.7, 9, 1.7, out int x, out int y, out double t) && x == 3 && y == 1 && t == 0, "start pixel");
        Check(!PixelAlgorithms.Raycast(grid, 5.5, 1.5, 5.5, 1.5, out _, out _, out _), "zero-length ray in empty space");
    }

    static void RayWorksWithNegativeAndDiagonalDirections()
    {
        var grid = VerticalWall(-20, -40, 40);
        Check(PixelAlgorithms.Raycast(grid, 0.5, 0.5, -30.5, -10.5, out int x, out _, out _) && x == -20, "leftwards and down");

        var diagonal = new Grid();
        diagonal.Set(5, 5);
        Check(PixelAlgorithms.Raycast(diagonal, 0.5, 0.5, 9.5, 9.5, out x, out int y, out _) && x == 5 && y == 5, "exact diagonal");
    }

    static void RayMatchesBruteForceSampling()
    {
        // Compare with sampling the line very finely: the traversal must find a
        // hit whenever fine sampling does, at the same or an earlier pixel.
        var random = new Random(3);
        for (int round = 0; round < 2000; round++)
        {
            var grid = new Grid();
            for (int i = 0; i < 40; i++) grid.Set(random.Next(-20, 20), random.Next(-20, 20));
            double x0 = random.NextDouble() * 40 - 20, y0 = random.NextDouble() * 40 - 20;
            double x1 = random.NextDouble() * 40 - 20, y1 = random.NextDouble() * 40 - 20;

            bool hit = PixelAlgorithms.Raycast(grid, x0, y0, x1, y1, out int hx, out int hy, out double t);

            double sampledT = -1;
            for (int s = 0; s <= 20000; s++)
            {
                double f = s / 20000.0;
                if (grid.IsSolid((int)Math.Floor(x0 + (x1 - x0) * f), (int)Math.Floor(y0 + (y1 - y0) * f))) { sampledT = f; break; }
            }

            if (sampledT >= 0)
            {
                Check(hit, $"round {round}: sampling found a wall the ray missed");
                Check(t <= sampledT + 1e-6, $"round {round}: ray hit later ({t}) than sampling ({sampledT})");
                Check(grid.IsSolid(hx, hy), "hit pixel must be solid");
            }
            else if (hit)
            {
                // Only allowed when the line grazes a pixel corner the sampling skipped.
                Check(t >= 0 && t <= 1, "t out of range");
            }
        }
    }
}
