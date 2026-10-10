using System;
using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Zombies;

// Tests for the zombies' flow field, survival waves and room finding.
static partial class Tests
{
    // A field from rows of text, top row first:
    //   '.' open ground, '#' impossible (a tree, the map edge), 'W' a wall (cost 30),
    //   'D' a door (cost 6), 'T' a target.
    static FlowField Field(string[] rows, out List<int> targets)
    {
        int w = rows[0].Length, h = rows.Length;
        var field = new FlowField(w, h);
        targets = new List<int>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = rows[h - 1 - y][x];
                int i = y * w + x;
                field.Cost[i] = c == '#' ? 0f : c == 'W' ? 30f : c == 'D' ? 6f : 1f;
                if (c == 'T') targets.Add(i);
            }
        return field;
    }

    // Follows the field from (x, y), breaking through whatever it says to; returns the cells stepped into.
    static List<(int x, int y)> Follow(FlowField field, int x, int y, int maxSteps = 500)
    {
        var steps = new List<(int, int)>();
        for (int i = 0; i < maxSteps && field.Next(x, y, out int nx, out int ny); i++)
        {
            int dx = nx - x, dy = ny - y;
            Check(Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && (dx != 0 || dy != 0), $"step {x},{y} -> {nx},{ny} isn't to a neighbour");
            if (dx != 0 && dy != 0) Check(field.Open(x + dx, y) && field.Open(x, y + dy) && field.Open(nx, ny), $"diagonal step {x},{y} -> {nx},{ny} cuts a corner or breaks in");
            x = nx;
            y = ny;
            steps.Add((x, y));
        }
        Check(steps.Count < maxSteps, "the walk ends");
        return steps;
    }

    static void FieldLeadsStraightToTarget()
    {
        var field = Field(new[]
        {
            ".........T",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
        }, out var targets);
        field.Compute(targets, null);
        Check(Math.Abs(field.Distance(0, 0) - 9 * 1.41421356f) < 0.01f, $"diagonal distance, got {field.Distance(0, 0)}");
        var walk = Follow(field, 0, 0);
        Check(walk.Count == 9 && walk[8] == (9, 9), $"9 diagonal steps to the target, got {walk.Count}");
    }

    static void FieldGoesRoundAShortWall()
    {
        // A short wall with open ground round it: walking round is cheaper than breaking in.
        var field = Field(new[]
        {
            "....T....",
            ".........",
            "..WWWWW..",
            ".........",
            "....z....",
        }, out var targets);
        field.Compute(targets, null);
        foreach (var (x, y) in Follow(field, 4, 0))
            Check(field.Cost[y * field.Width + x] == 1f, $"walks round the wall instead of breaking it (stepped into {x},{y})");
    }

    static void FieldPrefersTheDoor()
    {
        // A walled yard with a door on the far side and a target inside.
        var field = Field(new[]
        {
            "#########",
            "#.......#",
            "#.WWDWW.#",
            "#.W...W.#",
            "#.W.T.W.#",
            "#.WWWWW.#",
            "#.......#",
            "#.......#",
            "#########",
        }, out var targets);
        field.Compute(targets, null);
        var walk = Follow(field, 4, 1);
        bool door = false;
        foreach (var (x, y) in walk)
        {
            float cost = field.Cost[y * field.Width + x];
            Check(cost != 30f, $"breaks a wall at {x},{y} instead of using the door");
            if (cost == 6f) door = true;
        }
        Check(door, "goes in through the door");
        Check(walk[walk.Count - 1] == (4, 4), "ends at the target");
    }

    static void FieldBreaksInWhenThereIsNoDoor()
    {
        var field = Field(new[]
        {
            ".......",
            ".WWWWW.",
            ".W...W.",
            ".W.T.W.",
            ".W...W.",
            ".WWWWW.",
            ".......",
        }, out var targets);
        field.Compute(targets, null);
        Check(!float.IsPositiveInfinity(field.Distance(0, 0)), "a closed room can still be reached by breaking in");
        var walk = Follow(field, 3, 0);
        int walls = 0;
        foreach (var (x, y) in walk) if (field.Cost[y * field.Width + x] == 30f) walls++;
        Check(walls == 1, $"breaks through exactly one wall, got {walls}");
    }

    static void FieldCantCrossTheImpossible()
    {
        var field = Field(new[]
        {
            ".......",
            ".#####.",
            ".#...#.",
            ".#.T.#.",
            ".#...#.",
            ".#####.",
            ".......",
        }, out var targets);
        field.Compute(targets, null);
        Check(float.IsPositiveInfinity(field.Distance(0, 0)), "no way through trees and rocks");
        Check(!field.Next(0, 0, out _, out _), "no step to take");
    }

    static void FieldHeadStartMakesTargetsLessAttractive()
    {
        // Two targets: the left one is nearer (4 steps against 6).
        var field = Field(new[] { "T...z.....T" }, out var targets);
        field.Compute(targets, new[] { 0f, 0f });
        var walk = Follow(field, 4, 0);
        Check(walk[walk.Count - 1] == (0, 0), $"with no head start, the nearer one, ended at {walk[walk.Count - 1]}");
        // A head start of 10 makes the near one worth 14, more than the far one's 6.
        field.Compute(targets, new[] { 10f, 0f });
        walk = Follow(field, 4, 0);
        Check(walk[walk.Count - 1] == (10, 0), $"goes for the more attractive one, ended at {walk[walk.Count - 1]}");
    }

    static void FieldMatchesDijkstraOnRandomGrids()
    {
        var random = new Random(12345);
        for (int round = 0; round < 200; round++)
        {
            int w = random.Next(3, 14), h = random.Next(3, 14);
            var field = new FlowField(w, h);
            for (int i = 0; i < w * h; i++)
            {
                double r = random.NextDouble();
                field.Cost[i] = r < 0.15 ? 0f : r < 0.25 ? 5f + random.Next(20) : 1f;
            }
            int target = random.Next(w * h);
            field.Cost[target] = 1f;
            field.Compute(new[] { target }, null);

            // Following the field from anywhere reachable costs exactly the distance it promises.
            for (int start = 0; start < w * h; start++)
            {
                int x = start % w, y = start / w;
                float promised = field.Distance(x, y);
                if (float.IsPositiveInfinity(promised) || field.Cost[start] <= 0f) continue;
                float paid = 0f;
                foreach (var (sx, sy) in Follow(field, x, y))
                {
                    bool diagonal = Math.Abs(sx - x) == 1 && Math.Abs(sy - y) == 1;
                    paid += field.Cost[sy * w + sx] * (diagonal ? 1.41421356f : 1f);
                    x = sx;
                    y = sy;
                }
                Check(x + y * w == target, $"round {round}: the walk from {start} ends at the target");
                Check(Math.Abs(paid - promised) < 0.01f, $"round {round}: paid {paid} but promised {promised}");
            }
        }
    }

    static void WavesGrowAndBringNewKinds()
    {
        int last = 0;
        for (int wave = 1; wave <= 40; wave++)
        {
            int count = WaveMath.Count(wave, 6, 3, 1.05f);
            Check(count > last, $"wave {wave} sends more than wave {wave - 1} ({count} vs {last})");
            last = count;
        }
        Check(WaveMath.Count(1, 6, 3, 1.05f) == 6, "the first wave sends the first count");
        Check(Math.Abs(WaveMath.Toughness(1, 0.06f) - 1f) < 1e-6f && WaveMath.Toughness(11, 0.06f) > 1.59f, "toughness grows");

        var first = new[] { 1, 3, 5 };
        var weights = new[] { 10f, 4f, 1f };
        for (int i = 0; i < 1000; i++)
            Check(WaveMath.Pick(2, first, weights, i / 1000.0) == 0, "only walkers in wave 2");
        var counts = new int[3];
        for (int i = 0; i < 3000; i++) counts[WaveMath.Pick(20, first, weights, i / 3000.0)]++;
        Check(counts[0] > counts[1] && counts[1] > counts[2] && counts[2] > 0, $"all three kinds by wave 20, walkers most ({counts[0]}, {counts[1]}, {counts[2]})");
        Check(WaveMath.Pick(1, new[] { 2 }, new[] { 1f }, 0.5) == -1, "nothing can come yet");
    }

    // ------------------------------------------------------------- rooms --

    // Walls from rows of text, top row first: '#' wall or door, anything else open.
    static bool[] Walls(string[] rows, out int width, out int height)
    {
        width = rows[0].Length;
        height = rows.Length;
        var walls = new bool[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                walls[y * width + x] = rows[height - 1 - y][x] == '#';
        return walls;
    }

    static void RoomsAreClosedAreas()
    {
        var walls = Walls(new[]
        {
            "...........",
            ".#####.....",
            ".#...#.###.",
            ".#...#.#.#.",
            ".#...#.###.",
            ".#####.....",
            "...........",
        }, out int w, out int h);
        var rooms = RoomFinder.Find(walls, w, h, 400);
        Check(rooms.Count == 2, $"two rooms, got {rooms.Count}");
        var sizes = new List<int> { rooms[0].Count, rooms[1].Count };
        sizes.Sort();
        Check(sizes[0] == 1 && sizes[1] == 9, $"a 1-tile and a 9-tile room, got {sizes[0]} and {sizes[1]}");
    }

    static void RoomWithAGapIsNotARoom()
    {
        var walls = Walls(new[]
        {
            ".......",
            ".##.##.",
            ".#...#.",
            ".#...#.",
            ".#####.",
            ".......",
        }, out int w, out int h);
        Check(RoomFinder.Find(walls, w, h, 400).Count == 0, "a gap lets the outside in");
    }

    static void CornersCloseARoom()
    {
        // Walls that only touch at a corner still close the room (nobody squeezes between corners).
        var walls = Walls(new[]
        {
            ".......",
            "..###..",
            ".#...#.",
            ".#...#.",
            "..###..",
            ".......",
        }, out int w, out int h);
        var rooms = RoomFinder.Find(walls, w, h, 400);
        Check(rooms.Count == 1 && rooms[0].Count == 6, $"one 6-tile room, got {rooms.Count}");
    }

    static void HugeEnclosuresAreNotRooms()
    {
        var walls = Walls(new[]
        {
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#######",
        }, out int w, out int h);
        Check(RoomFinder.Find(walls, w, h, 10).Count == 0, "15 tiles is more than the 10 allowed");
        Check(RoomFinder.Find(walls, w, h, 15).Count == 1, "15 tiles is fine with 15 allowed");
    }
}
