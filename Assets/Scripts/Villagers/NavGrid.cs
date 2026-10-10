using System;
using System.Collections.Generic;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Villagers
{
    /// <summary>
    /// Which grid cells villagers can walk through, kept up to date as walls go
    /// up and get destroyed, buildings are placed and trees and rocks are used
    /// up. Finds paths with <see cref="GridPathfinder"/>.
    ///
    /// Each cell counts the things blocking it (a wall, a building), so removing
    /// one of two blockers keeps the cell blocked. Trees, rocks and bushes are
    /// read straight from the <see cref="NatureMap"/> (most aren't real objects).
    /// </summary>
    public sealed class NavGrid
    {
        /// <summary>How wide walkers are (from their middle), in tiles, for cutting corners on straight stretches.</summary>
        public const float WalkerRadius = 0.22f;

        public readonly RectInt Cells;
        /// <summary>Cells walkers may cross but shouldn't stop on (planned buildings), or null.</summary>
        public Func<Vector2Int, bool> NoStopping { get; set; }
        readonly byte[] blockers;
        readonly GridPathfinder finder;
        readonly PixelWorld world;
        readonly NatureMap nature;
        readonly List<GridPoint> found = new List<GridPoint>();
        readonly Func<int, int, bool> walkable;

        /// <summary>
        /// A grid over <paramref name="cells"/>. <paramref name="pixelWorld"/>'s walls and <paramref name="natureMap"/>'s
        /// trees block it (either may be null: a house's inside has neither).
        /// </summary>
        public NavGrid(RectInt cells, PixelWorld pixelWorld, NatureMap natureMap = null)
        {
            Cells = cells;
            blockers = new byte[cells.width * cells.height];
            finder = new GridPathfinder(cells.width, cells.height);
            nature = natureMap;
            walkable = (x, y) => x >= 0 && y >= 0 && x < Cells.width && y < Cells.height && blockers[y * Cells.width + x] == 0 &&
                                 (nature == null || !nature.Blocks(new Vector2Int(Cells.x + x, Cells.y + y)));

            world = pixelWorld;
            if (world != null)
            {
                foreach (var cell in world.ChunkCells) Change(cell, +1);
                world.CellChanged += OnWallCellChanged;
            }
        }

        /// <summary>Stops listening for changes. Call when the kingdom goes away.</summary>
        public void Dispose()
        {
            if (world != null) world.CellChanged -= OnWallCellChanged;
        }

        public bool Inside(Vector2Int cell) => Cells.Contains(cell);

        public bool Walkable(Vector2Int cell) => Inside(cell) && blockers[Index(cell)] == 0 && (nature == null || !nature.Blocks(cell));

        public bool Walkable(Vector2 position) => Walkable(CellAt(position));

        public static Vector2Int CellAt(Vector2 position) => new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));

        /// <summary>Marks a rectangle of cells as blocked by one more thing (a building).</summary>
        public void Block(RectInt rect)
        {
            foreach (var cell in rect.allPositionsWithin) Change(cell, +1);
        }

        public void Unblock(RectInt rect)
        {
            foreach (var cell in rect.allPositionsWithin) Change(cell, -1);
        }

        /// <summary>
        /// Finds a path from <paramref name="from"/> to a free cell right beside
        /// <paramref name="target"/> (touching it, diagonals included), or into it if
        /// <paramref name="beside"/> is false. Fills <paramref name="waypoints"/> with
        /// world positions to walk to, in order. Returns false if there's no way there.
        /// </summary>
        public bool FindPath(Vector2 from, RectInt target, bool beside, List<Vector2> waypoints)
        {
            waypoints.Clear();
            var start = CellAt(from) - Cells.min;
            var inner = new RectInt(target.min - Cells.min, target.size);
            var outer = beside ? new RectInt(inner.min - Vector2Int.one, inner.size + Vector2Int.one * 2) : inner;
            Func<int, int, bool> isGoal = (x, y) =>
                x >= outer.xMin && x < outer.xMax && y >= outer.yMin && y < outer.yMax &&
                (!beside || x < inner.xMin || x >= inner.xMax || y < inner.yMin || y >= inner.yMax) &&
                walkable(x, y) &&
                (NoStopping == null || !NoStopping(new Vector2Int(x + Cells.xMin, y + Cells.yMin)));

            if (!finder.FindPath(walkable, start.x, start.y, isGoal, outer.xMin, outer.yMin, outer.xMax - 1, outer.yMax - 1, found))
                return false;

            var local = from - (Vector2)Cells.min;
            GridPathfinder.Smooth(walkable, local.x, local.y, found, WalkerRadius);
            foreach (var p in found) waypoints.Add(new Vector2(Cells.xMin + p.X + 0.5f, Cells.yMin + p.Y + 0.5f));
            return true;
        }

        /// <summary>The nearest walkable cell's middle to <paramref name="position"/> that is outside <paramref name="avoid"/>, searching outwards.</summary>
        public bool NearestFreeSpot(Vector2 position, RectInt avoid, out Vector2 spot)
        {
            var center = CellAt(position);
            for (int radius = 1; radius <= 6; radius++)
            {
                float best = float.MaxValue;
                spot = default;
                for (int y = -radius; y <= radius; y++)
                    for (int x = -radius; x <= radius; x++)
                    {
                        var cell = center + new Vector2Int(x, y);
                        if (!Walkable(cell) || avoid.Contains(cell) || (NoStopping != null && NoStopping(cell))) continue;
                        var middle = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                        float d = (middle - position).sqrMagnitude;
                        if (d < best) { best = d; spot = middle; }
                    }
                if (best < float.MaxValue) return true;
            }
            spot = position;
            return false;
        }

        void OnWallCellChanged(Vector2Int cell, bool added) => Change(cell, added ? +1 : -1);

        void Change(Vector2Int cell, int amount)
        {
            if (!Inside(cell)) return;
            int i = Index(cell);
            blockers[i] = (byte)Mathf.Clamp(blockers[i] + amount, 0, 255);
        }

        int Index(Vector2Int cell) => (cell.y - Cells.yMin) * Cells.width + (cell.x - Cells.xMin);
    }
}
