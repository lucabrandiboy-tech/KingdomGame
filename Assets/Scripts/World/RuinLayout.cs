using System.Collections.Generic;
using KingdomGame.Art;
using UnityEngine;

namespace KingdomGame.World
{
    public enum RuinKind { Village, Caravan, Tower }

    /// <summary>How a ruin's picture is drawn: under everything (old floors), low (rubble), or standing (walls, wagons, chests: with a shadow).</summary>
    public enum RuinLayer { Floor, Low, Standing }

    /// <summary>One picture of a ruin: its art name and way round (<see cref="ArtKeys.Turned"/>), where its middle is, and how it's drawn.</summary>
    public struct RuinPiece
    {
        public string Art;
        public int Rotation;
        public Vector2 At;
        public RuinLayer Layer;
        /// <summary>How high it stands, in tiles (for its shadow).</summary>
        public float Height;
    }

    /// <summary>Something to search in a ruin (a chest or a crate): where, and whether a lost blueprint is in it.</summary>
    public struct RuinCache
    {
        public Vector2 At;
        public string Art;
        public int Rotation;
        public bool Blueprint;
    }

    /// <summary>A ruin: what's left of a village, a caravan or a watchtower, the things in it to search, and its zombie guards.</summary>
    public sealed class RuinSite
    {
        /// <summary>Its region of the world ("x,y"): what saves remember it by.</summary>
        public string Id;
        public Vector2Int Region;
        public RuinKind Kind;
        public Vector2 Center;
        public readonly List<RuinPiece> Pieces = new List<RuinPiece>();
        public readonly List<RuinCache> Caches = new List<RuinCache>();
        /// <summary>Cells it covers: trees and rocks there are cleared away when it's first seen.</summary>
        public readonly List<Vector2Int> Footprint = new List<Vector2Int>();
        /// <summary>Zombies guarding it.</summary>
        public int Guards;
        /// <summary>How far it reaches from its middle, in tiles.</summary>
        public float Radius;

        public string Name => Kind == RuinKind.Village ? "an abandoned village" : Kind == RuinKind.Caravan ? "a wrecked caravan" : "a fallen watchtower";
    }

    /// <summary>
    /// Where the ruins are in the endless world and what each looks like, worked out from the seed alone (the same
    /// for everyone, every time): the world is cut into squares of <see cref="Region"/> tiles, and about a third of
    /// them have a ruin somewhere inside. Plain maths (no game objects), so the art preview can draw them too.
    /// </summary>
    public static class RuinLayout
    {
        public const int Region = 96;
        /// <summary>Share of the squares with a ruin.</summary>
        public const float Chance = 0.32f;

        public static Vector2Int RegionOf(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / Region), Mathf.FloorToInt(p.y / Region));

        static uint Hash(uint seed, int x, int y)
        {
            uint h = seed * 0x9E3779B1u ^ (uint)x * 0x85EBCA77u ^ (uint)y * 0xC2B2AE3Du;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            return h;
        }

        /// <summary>The ruin in a region of the world, or null if it has none.</summary>
        public static RuinSite At(uint seed, Vector2Int region)
        {
            var dice = new Dice(Hash(seed ^ 0x5EEDu, region.x, region.y));
            if (dice.Next() >= Chance) return null;
            var site = new RuinSite
            {
                Region = region,
                Id = $"{region.x},{region.y}",
                Center = new Vector2(region.x * Region + 16 + Mathf.Floor(dice.Range(0f, Region - 32)), region.y * Region + 16 + Mathf.Floor(dice.Range(0f, Region - 32))),
            };
            float kind = dice.Next();
            site.Kind = kind < 0.45f ? RuinKind.Village : kind < 0.8f ? RuinKind.Caravan : RuinKind.Tower;
            var c = new Vector2Int(Mathf.FloorToInt(site.Center.x), Mathf.FloorToInt(site.Center.y));
            switch (site.Kind)
            {
                case RuinKind.Village: Village(site, c, ref dice); break;
                case RuinKind.Caravan: Caravan(site, c, ref dice); break;
                default: Tower(site, c, ref dice); break;
            }
            float far = 0f;
            foreach (var cell in site.Footprint) far = Mathf.Max(far, ((Vector2)cell + new Vector2(0.5f, 0.5f) - site.Center).magnitude);
            site.Radius = far + 1f;
            return site;
        }

        // ------------------------------------------------------------ villages --

        // Two to four houses fallen in round a middle: broken walls with a way in, old floors, rubble, a chest or two,
        // sometimes the old well. Guarded by four to seven zombies.
        static void Village(RuinSite site, Vector2Int c, ref Dice dice)
        {
            var slots = new List<Vector2Int> { new Vector2Int(-9, -6), new Vector2Int(3, -7), new Vector2Int(-8, 3), new Vector2Int(3, 2) };
            for (int i = slots.Count - 1; i > 0; i--)
            {
                int j = (int)(dice.Next() * (i + 1));
                var swap = slots[i]; slots[i] = slots[j]; slots[j] = swap;
            }
            int houses = 2 + (int)(dice.Next() * 3f);
            var used = new HashSet<Vector2Int>();
            int chests = 0;
            for (int h = 0; h < houses; h++)
            {
                int w = 4 + (int)(dice.Next() * 3f), d = 4 + (int)(dice.Next() * 2f);
                var o = c + slots[h] + new Vector2Int(Mathf.FloorToInt(dice.Range(-1f, 2f)), Mathf.FloorToInt(dice.Range(-1f, 2f)));
                int doorX = o.x + 1 + (int)(dice.Next() * (w - 2));
                for (int y = o.y; y < o.y + d; y++)
                    for (int x = o.x; x < o.x + w; x++)
                    {
                        var cell = new Vector2Int(x, y);
                        used.Add(cell);
                        site.Footprint.Add(cell);
                        var middle = (Vector2)cell + new Vector2(0.5f, 0.5f);
                        bool edgeX = x == o.x || x == o.x + w - 1, edgeY = y == o.y || y == o.y + d - 1;
                        if (!edgeX && !edgeY)
                        {
                            if (dice.Next() < 0.94f) Add(site, FurnitureArt.RuinFloor + (int)(dice.Next() * FurnitureArt.RuinFloors), 0, middle, RuinLayer.Floor, 0f);
                            continue;
                        }
                        if (y == o.y && x == doorX) continue;   // the way in
                        float roll = dice.Next();
                        if (roll < 0.2f) continue;                                // fallen away
                        if (roll < 0.32f) Add(site, FurnitureArt.RuinRubble + (int)(dice.Next() * FurnitureArt.RuinRubbles), 0, middle, RuinLayer.Low, 0.12f);
                        else
                        {
                            int variant = dice.Next() < 0.25f ? 3 : (int)(dice.Next() * 3f);
                            Add(site, FurnitureArt.RuinWall + variant, edgeY ? 0 : 1, middle, RuinLayer.Standing, variant == 3 ? 0.12f : 0.4f);
                        }
                    }
                // A chest in the first house, sometimes one in another.
                if (w > 2 && d > 2 && (chests == 0 || dice.Next() < 0.35f))
                {
                    var spot = new Vector2Int(o.x + 1 + (int)(dice.Next() * (w - 2)), o.y + 1 + (int)(dice.Next() * (d - 2)));
                    site.Caches.Add(new RuinCache { At = (Vector2)spot + new Vector2(0.5f, 0.5f), Art = FurnitureArt.Chest, Rotation = (int)(dice.Next() * 4f), Blueprint = dice.Next() < 0.25f });
                    chests++;
                }
            }
            // The old well in the middle.
            if (dice.Next() < 0.4f)
            {
                Add(site, FurnitureArt.Well, 0, (Vector2)c + new Vector2(0.5f, 0.5f), RuinLayer.Standing, 0.4f);
                for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) site.Footprint.Add(c + new Vector2Int(x, y));
            }
            Scatter(site, c, ref dice, 4 + (int)(dice.Next() * 4f), 11f, used);
            site.Guards = 4 + (int)(dice.Next() * 4f);
        }

        // ------------------------------------------------------------ caravans --

        // One or two wagons wrecked on the road, their crates thrown about. Two to four zombies.
        static void Caravan(RuinSite site, Vector2Int c, ref Dice dice)
        {
            var used = new HashSet<Vector2Int>();
            int wagons = dice.Next() < 0.5f ? 2 : 1;
            for (int i = 0; i < wagons; i++)
            {
                int rotation = (int)(dice.Next() * 2f);
                var at = (Vector2)c + new Vector2(0.5f, 0.5f) + (i == 0 ? Vector2.zero : new Vector2(3.4f, 1.6f));
                Add(site, FurnitureArt.Wagon, rotation, at, RuinLayer.Standing, 0.35f);
                float hw = rotation == 0 ? 1.2f : 0.8f, hh = rotation == 0 ? 0.8f : 1.2f;
                for (int y = Mathf.FloorToInt(at.y - hh); y <= Mathf.FloorToInt(at.y + hh); y++)
                    for (int x = Mathf.FloorToInt(at.x - hw); x <= Mathf.FloorToInt(at.x + hw); x++)
                    {
                        used.Add(new Vector2Int(x, y));
                        site.Footprint.Add(new Vector2Int(x, y));
                    }
            }
            var spots = new[] { new Vector2Int(-3, 2), new Vector2Int(2, -3), new Vector2Int(-2, -3), new Vector2Int(5, -1) };
            int crates = 2 + (int)(dice.Next() * 2f);
            for (int i = 0; i < crates; i++)
            {
                var cell = c + spots[i];
                if (!used.Add(cell)) continue;
                site.Footprint.Add(cell);
                site.Caches.Add(new RuinCache { At = (Vector2)cell + new Vector2(0.5f, 0.5f), Art = FurnitureArt.Crates, Rotation = 0, Blueprint = dice.Next() < 0.15f });
            }
            Scatter(site, c, ref dice, 2 + (int)(dice.Next() * 3f), 5f, used);
            site.Guards = 2 + (int)(dice.Next() * 3f);
        }

        // ---------------------------------------------------------- watchtowers --

        // A round stone tower fallen in, with stumps of its outer wall, and a chest inside with a lost blueprint. Five to eight zombies.
        static void Tower(RuinSite site, Vector2Int c, ref Dice dice)
        {
            var used = new HashSet<Vector2Int>();
            var middle = (Vector2)c + new Vector2(0.5f, 0.5f);
            Add(site, FurnitureArt.RuinTower, 0, middle, RuinLayer.Standing, 0.6f);
            for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                {
                    used.Add(c + new Vector2Int(x, y));
                    site.Footprint.Add(c + new Vector2Int(x, y));
                }
            site.Caches.Add(new RuinCache { At = middle, Art = FurnitureArt.Chest, Rotation = (int)(dice.Next() * 4f), Blueprint = true });
            // Stumps of the outer wall: a stretch east and one south.
            for (int i = 0; i < 4; i++)
            {
                var east = c + new Vector2Int(4 + i, -1);
                var south = c + new Vector2Int(-1, -4 - i);
                foreach (var (cell, rotation) in new[] { (east, 0), (south, 1) })
                {
                    if (dice.Next() < 0.25f) continue;
                    used.Add(cell);
                    site.Footprint.Add(cell);
                    int variant = i == 3 ? 3 : (int)(dice.Next() * 3f);
                    Add(site, FurnitureArt.RuinWall + variant, rotation, (Vector2)cell + new Vector2(0.5f, 0.5f), RuinLayer.Standing, variant == 3 ? 0.12f : 0.4f);
                }
            }
            Scatter(site, c, ref dice, 4 + (int)(dice.Next() * 3f), 6f, used);
            site.Guards = 5 + (int)(dice.Next() * 4f);
        }

        // Rubble on free cells round the middle.
        static void Scatter(RuinSite site, Vector2Int c, ref Dice dice, int count, float radius, HashSet<Vector2Int> used)
        {
            for (int i = 0, tries = 0; i < count && tries < count * 6; tries++)
            {
                float a = dice.Range(0f, Mathf.PI * 2f), d = dice.Range(2f, radius);
                var cell = c + new Vector2Int(Mathf.RoundToInt(Mathf.Cos(a) * d), Mathf.RoundToInt(Mathf.Sin(a) * d));
                if (!used.Add(cell)) continue;
                site.Footprint.Add(cell);
                Add(site, FurnitureArt.RuinRubble + (int)(dice.Next() * FurnitureArt.RuinRubbles), 0, (Vector2)cell + new Vector2(0.5f, 0.5f), RuinLayer.Low, 0.12f);
                i++;
            }
        }

        static void Add(RuinSite site, string art, int rotation, Vector2 at, RuinLayer layer, float height) =>
            site.Pieces.Add(new RuinPiece { Art = art, Rotation = rotation, At = at, Layer = layer, Height = height });
    }
}
