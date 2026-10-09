using System;
using System.Collections.Generic;

namespace KingdomGame.Villagers
{
    /// <summary>A grid cell, counted from the grid's bottom-left corner.</summary>
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public readonly int X, Y;
        public GridPoint(int x, int y) { X = x; Y = y; }
        public bool Equals(GridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
        public override int GetHashCode() => X * 73856093 ^ Y * 19349663;
        public override string ToString() => $"({X}, {Y})";
    }

    /// <summary>
    /// Finds the shortest walk between grid cells (A* search): 8 directions,
    /// diagonal steps cost about 1.41, and a diagonal step is only allowed if
    /// both cells beside it are free, so walkers never squeeze between two
    /// walls that touch at a corner.
    ///
    /// Plain C# with no Unity code, so it is unit-tested (tools/tests). One
    /// instance per grid; it reuses its memory between searches.
    /// </summary>
    public sealed class GridPathfinder
    {
        const float Diagonal = 1.41421356f;
        static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] StepY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        public readonly int Width, Height;

        readonly float[] cost;
        readonly int[] parent;
        readonly int[] seen;     // == search number when cost/parent are valid for this search
        readonly int[] closed;   // == search number when the cell is finished
        int search;

        // Binary heap of open cells, ordered by estimated total cost.
        float[] heapKey = new float[256];
        int[] heapCell = new int[256];
        int heapCount;

        /// <summary>Gives up after looking at this many cells (keeps a hopeless search from taking long).</summary>
        public int MaxSearched { get; set; } = 40000;

        public GridPathfinder(int width, int height)
        {
            Width = width;
            Height = height;
            cost = new float[width * height];
            parent = new int[width * height];
            seen = new int[width * height];
            closed = new int[width * height];
        }

        /// <summary>
        /// Finds the shortest path from the start cell to any walkable cell where
        /// <paramref name="isGoal"/> is true. Every goal cell must lie inside the
        /// target rectangle (min and max included); it steers the search.
        /// On success <paramref name="path"/> holds the cells to walk through, in
        /// order, ending with the goal (not the start). The start cell itself
        /// doesn't have to be walkable.
        /// </summary>
        public bool FindPath(Func<int, int, bool> walkable, int startX, int startY, Func<int, int, bool> isGoal,
            int targetMinX, int targetMinY, int targetMaxX, int targetMaxY, List<GridPoint> path)
        {
            path.Clear();
            if (!Inside(startX, startY)) return false;
            if (isGoal(startX, startY)) return true;

            if (++search == int.MaxValue)
            {
                Array.Clear(seen, 0, seen.Length);
                Array.Clear(closed, 0, closed.Length);
                search = 1;
            }
            heapCount = 0;

            int start = startY * Width + startX;
            cost[start] = 0f;
            parent[start] = -1;
            seen[start] = search;
            Push(Estimate(startX, startY, targetMinX, targetMinY, targetMaxX, targetMaxY), start);

            int searched = 0;
            while (heapCount > 0)
            {
                int current = Pop();
                if (closed[current] == search) continue;
                closed[current] = search;
                int x = current % Width, y = current / Width;
                if (current != start && isGoal(x, y))
                {
                    for (int c = current; c != start; c = parent[c]) path.Add(new GridPoint(c % Width, c / Width));
                    path.Reverse();
                    return true;
                }
                if (++searched > MaxSearched) return false;

                for (int k = 0; k < 8; k++)
                {
                    int nx = x + StepX[k], ny = y + StepY[k];
                    if (!Inside(nx, ny) || !walkable(nx, ny)) continue;
                    bool diagonal = k >= 4;
                    if (diagonal && (!walkable(x + StepX[k], y) || !walkable(x, y + StepY[k]))) continue;
                    int next = ny * Width + nx;
                    if (closed[next] == search) continue;
                    float g = cost[current] + (diagonal ? Diagonal : 1f);
                    if (seen[next] == search && g >= cost[next] - 1e-5f) continue;
                    seen[next] = search;
                    cost[next] = g;
                    parent[next] = current;
                    Push(g + Estimate(nx, ny, targetMinX, targetMinY, targetMaxX, targetMaxY), next);
                }
            }
            return false;
        }

        /// <summary>The length of a path that <see cref="FindPath"/> returned, counting from the start cell.</summary>
        public static float Length(int startX, int startY, List<GridPoint> path)
        {
            float length = 0f;
            int x = startX, y = startY;
            foreach (var p in path)
            {
                length += (p.X != x && p.Y != y) ? Diagonal : 1f;
                x = p.X;
                y = p.Y;
            }
            return length;
        }

        /// <summary>
        /// Drops waypoints that can be skipped by walking in a straight line, so
        /// walkers don't zig-zag from cell middle to cell middle. A shortcut is
        /// only taken if a walker <paramref name="radius"/> cells wide (from its
        /// middle) stays on walkable cells all along it. Positions are in cells
        /// (a cell's middle is x + 0.5).
        /// </summary>
        public static void Smooth(Func<int, int, bool> walkable, float startX, float startY, List<GridPoint> path, float radius)
        {
            if (path.Count < 2) return;
            var kept = new List<GridPoint>(path.Count);
            float ax = startX, ay = startY;
            int i = 0;
            while (i < path.Count)
            {
                int furthest = i;
                for (int j = path.Count - 1; j > i; j--)
                {
                    if (LineIsClear(walkable, ax, ay, path[j].X + 0.5f, path[j].Y + 0.5f, radius))
                    {
                        furthest = j;
                        break;
                    }
                }
                kept.Add(path[furthest]);
                ax = path[furthest].X + 0.5f;
                ay = path[furthest].Y + 0.5f;
                i = furthest + 1;
            }
            path.Clear();
            path.AddRange(kept);
        }

        /// <summary>True if a walker <paramref name="radius"/> cells wide can go straight from a to b on walkable cells.</summary>
        public static bool LineIsClear(Func<int, int, bool> walkable, float ax, float ay, float bx, float by, float radius)
        {
            float dx = bx - ax, dy = by - ay;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            int steps = Math.Max(1, (int)Math.Ceiling(length / 0.2f));
            for (int s = 0; s <= steps; s++)
            {
                float t = s / (float)steps;
                float x = ax + dx * t, y = ay + dy * t;
                if (!walkable(Floor(x - radius), Floor(y - radius)) || !walkable(Floor(x + radius), Floor(y - radius)) ||
                    !walkable(Floor(x - radius), Floor(y + radius)) || !walkable(Floor(x + radius), Floor(y + radius)))
                    return false;
            }
            return true;
        }

        static int Floor(float v) => (int)Math.Floor(v);

        bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        // Shortest possible walk to the target rectangle with no obstacles (never more than the real one).
        static float Estimate(int x, int y, int minX, int minY, int maxX, int maxY)
        {
            int dx = Math.Max(0, Math.Max(minX - x, x - maxX));
            int dy = Math.Max(0, Math.Max(minY - y, y - maxY));
            int straight = Math.Abs(dx - dy), diagonal = Math.Min(dx, dy);
            return straight + diagonal * Diagonal;
        }

        void Push(float key, int cell)
        {
            if (heapCount == heapKey.Length)
            {
                Array.Resize(ref heapKey, heapKey.Length * 2);
                Array.Resize(ref heapCell, heapCell.Length * 2);
            }
            int i = heapCount++;
            while (i > 0)
            {
                int up = (i - 1) / 2;
                if (heapKey[up] <= key) break;
                heapKey[i] = heapKey[up];
                heapCell[i] = heapCell[up];
                i = up;
            }
            heapKey[i] = key;
            heapCell[i] = cell;
        }

        int Pop()
        {
            int top = heapCell[0];
            float key = heapKey[--heapCount];
            int cell = heapCell[heapCount];
            int i = 0;
            while (true)
            {
                int child = i * 2 + 1;
                if (child >= heapCount) break;
                if (child + 1 < heapCount && heapKey[child + 1] < heapKey[child]) child++;
                if (heapKey[child] >= key) break;
                heapKey[i] = heapKey[child];
                heapCell[i] = heapCell[child];
                i = child;
            }
            heapKey[i] = key;
            heapCell[i] = cell;
            return top;
        }
    }
}
