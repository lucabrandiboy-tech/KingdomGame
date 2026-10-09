using System;
using System.Collections.Generic;
using KingdomGame.Villagers;

// Tests for the villagers' path search (GridPathfinder).
static partial class Tests
{
    // A grid from rows of text, top row first: '#' blocked, anything else free.
    static Func<int, int, bool> PathGrid(string[] rows, out int width, out int height)
    {
        int w = rows[0].Length, h = rows.Length;
        width = w;
        height = h;
        return (x, y) => x >= 0 && y >= 0 && x < w && y < h && rows[h - 1 - y][x] != '#';
    }

    static Func<int, int, bool> At(int gx, int gy) => (x, y) => x == gx && y == gy;

    static void CheckWalk(Func<int, int, bool> walkable, int sx, int sy, List<GridPoint> path)
    {
        int x = sx, y = sy;
        foreach (var p in path)
        {
            int dx = p.X - x, dy = p.Y - y;
            Check(Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && (dx != 0 || dy != 0), $"step {x},{y} -> {p} is not to a neighbor");
            Check(walkable(p.X, p.Y), $"step onto blocked cell {p}");
            if (dx != 0 && dy != 0) Check(walkable(x + dx, y) && walkable(x, y + dy), $"diagonal step {x},{y} -> {p} cuts a corner");
            x = p.X;
            y = p.Y;
        }
    }

    static void PathStraightAndDiagonal()
    {
        var walk = PathGrid(new[] { "......", "......", "......", "......" }, out int w, out int h);
        var finder = new GridPathfinder(w, h);
        var path = new List<GridPoint>();
        Check(finder.FindPath(walk, 0, 0, At(5, 0), 5, 0, 5, 0, path), "straight path found");
        Check(path.Count == 5 && path[4].Equals(new GridPoint(5, 0)), $"straight path has 5 steps ending at the goal, got {path.Count}");
        Check(finder.FindPath(walk, 0, 0, At(3, 3), 3, 3, 3, 3, path), "diagonal path found");
        Check(path.Count == 3, $"diagonal path has 3 steps, got {path.Count}");
        CheckWalk(walk, 0, 0, path);
    }

    static void PathGoesAroundAWall()
    {
        var rows = new[]
        {
            ".......",
            "..#....",
            "..#....",
            "..#....",
            "..#....",
        };
        var walk = PathGrid(rows, out int w, out int h);
        var finder = new GridPathfinder(w, h);
        var path = new List<GridPoint>();
        Check(finder.FindPath(walk, 0, 0, At(4, 0), 4, 0, 4, 0, path), "path found");
        CheckWalk(walk, 0, 0, path);
        Check(path[path.Count - 1].Equals(new GridPoint(4, 0)), "ends at the goal");
        // Up the left side, over the top of the wall (row 4) and back down: 2 diagonal steps and 8 straight ones.
        // (No diagonal step can touch the wall's top corner, so it can't be cut short.)
        Check(Math.Abs(GridPathfinder.Length(0, 0, path) - (2 * 1.41421356f + 8f)) < 0.01f, $"shortest length, got {GridPathfinder.Length(0, 0, path)}");
    }

    static void PathNeverCutsCorners()
    {
        // The only way from bottom-left to top-right is squeezing diagonally between two blocks.
        var walk = PathGrid(new[] { "#.", ".#" }, out int w, out int h);
        var path = new List<GridPoint>();
        Check(!new GridPathfinder(w, h).FindPath(walk, 0, 0, At(1, 1), 1, 1, 1, 1, path), "no squeezing between corners");
    }

    static void PathFailsWhenEnclosed()
    {
        var rows = new[]
        {
            ".....",
            ".###.",
            ".#.#.",
            ".###.",
            ".....",
        };
        var walk = PathGrid(rows, out int w, out int h);
        var path = new List<GridPoint>();
        Check(!new GridPathfinder(w, h).FindPath(walk, 0, 0, At(2, 2), 2, 2, 2, 2, path), "can't reach the inside of a closed ring");
    }

    static void PathEndsNextToABuilding()
    {
        // A 2 x 2 building at (4..5, 4..5): the walker should stop on any free cell touching it.
        var rows = new string[9];
        for (int i = 0; i < rows.Length; i++) rows[i] = ".........";
        rows[9 - 1 - 4] = "....##...";
        rows[9 - 1 - 5] = "....##...";
        var walk = PathGrid(rows, out int w, out int h);
        var finder = new GridPathfinder(w, h);
        var path = new List<GridPoint>();
        Func<int, int, bool> nextToIt = (x, y) => x >= 3 && x <= 6 && y >= 3 && y <= 6 && !(x >= 4 && x <= 5 && y >= 4 && y <= 5);
        Check(finder.FindPath(walk, 0, 0, nextToIt, 3, 3, 6, 6, path), "path found");
        CheckWalk(walk, 0, 0, path);
        var end = path[path.Count - 1];
        Check(end.Equals(new GridPoint(3, 3)), $"stops at the nearest corner (3,3), got {end}");
    }

    static void PathFromGoalIsEmpty()
    {
        var walk = PathGrid(new[] { "..." }, out int w, out int h);
        var path = new List<GridPoint> { new GridPoint(9, 9) };
        Check(new GridPathfinder(w, h).FindPath(walk, 1, 0, At(1, 0), 1, 0, 1, 0, path), "found");
        Check(path.Count == 0, "already there: no steps");
    }

    // Compares the path length with a plain Dijkstra search on many random grids.
    static void PathIsShortestOnRandomGrids()
    {
        var random = new Random(1234);
        const int w = 24, h = 18;
        var finder = new GridPathfinder(w, h);
        var path = new List<GridPoint>();
        int found = 0;
        for (int round = 0; round < 300; round++)
        {
            var blocked = new bool[w * h];
            for (int i = 0; i < blocked.Length; i++) blocked[i] = random.NextDouble() < 0.28;
            int sx = random.Next(w), sy = random.Next(h), gx = random.Next(w), gy = random.Next(h);
            blocked[sy * w + sx] = false;
            blocked[gy * w + gx] = false;
            Func<int, int, bool> walk = (x, y) => x >= 0 && y >= 0 && x < w && y < h && !blocked[y * w + x];

            bool ok = finder.FindPath(walk, sx, sy, At(gx, gy), gx, gy, gx, gy, path);
            float best = Dijkstra(walk, w, h, sx, sy, gx, gy);
            Check(ok == !float.IsPositiveInfinity(best), $"round {round}: found={ok} but Dijkstra says {best}");
            if (!ok) continue;
            found++;
            CheckWalk(walk, sx, sy, path);
            Check(Math.Abs(GridPathfinder.Length(sx, sy, path) - best) < 0.01f, $"round {round}: length {GridPathfinder.Length(sx, sy, path)} vs shortest {best}");
        }
        Check(found > 100, $"enough solvable grids to mean something ({found})");
    }

    static float Dijkstra(Func<int, int, bool> walk, int w, int h, int sx, int sy, int gx, int gy)
    {
        var dist = new float[w * h];
        var done = new bool[w * h];
        for (int i = 0; i < dist.Length; i++) dist[i] = float.PositiveInfinity;
        dist[sy * w + sx] = 0;
        while (true)
        {
            int best = -1;
            for (int i = 0; i < dist.Length; i++)
                if (!done[i] && !float.IsPositiveInfinity(dist[i]) && (best < 0 || dist[i] < dist[best])) best = i;
            if (best < 0) return float.PositiveInfinity;
            if (best == gy * w + gx) return dist[best];
            done[best] = true;
            int x = best % w, y = best / w;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (!walk(nx, ny)) continue;
                    if (dx != 0 && dy != 0 && (!walk(x + dx, y) || !walk(x, y + dy))) continue;
                    float d = dist[best] + (dx != 0 && dy != 0 ? 1.41421356f : 1f);
                    if (d < dist[ny * w + nx]) dist[ny * w + nx] = d;
                }
        }
    }

    static void SmoothedPathsStayClear()
    {
        var random = new Random(99);
        const int w = 30, h = 20;
        var finder = new GridPathfinder(w, h);
        var path = new List<GridPoint>();
        for (int round = 0; round < 200; round++)
        {
            var blocked = new bool[w * h];
            for (int i = 0; i < blocked.Length; i++) blocked[i] = random.NextDouble() < 0.18;
            int sx = random.Next(w), sy = random.Next(h), gx = random.Next(w), gy = random.Next(h);
            blocked[sy * w + sx] = false;
            blocked[gy * w + gx] = false;
            Func<int, int, bool> walk = (x, y) => x >= 0 && y >= 0 && x < w && y < h && !blocked[y * w + x];
            if (!finder.FindPath(walk, sx, sy, At(gx, gy), gx, gy, gx, gy, path)) continue;
            int before = path.Count;
            GridPathfinder.Smooth(walk, sx + 0.5f, sy + 0.5f, path, 0.2f);
            Check(path.Count <= before && path.Count >= 1, "smoothing keeps at least the goal and never adds points");
            Check(path[path.Count - 1].Equals(new GridPoint(gx, gy)), "smoothing keeps the goal");
            float ax = sx + 0.5f, ay = sy + 0.5f;
            foreach (var p in path)
            {
                Check(GridPathfinder.LineIsClear(walk, ax, ay, p.X + 0.5f, p.Y + 0.5f, 0.2f), $"round {round}: shortcut to {p} crosses a blocked cell");
                ax = p.X + 0.5f;
                ay = p.Y + 0.5f;
            }
        }
        // In an empty field the whole path becomes one straight line.
        var open = PathGrid(new[] { "........", "........", "........", "........" }, out int ow, out int oh);
        var straight = new List<GridPoint>();
        Check(new GridPathfinder(ow, oh).FindPath(open, 0, 0, At(7, 3), 7, 3, 7, 3, straight), "found");
        GridPathfinder.Smooth(open, 0.5f, 0.5f, straight, 0.2f);
        Check(straight.Count == 1, $"open field: one straight line, got {straight.Count} points");
    }
}
