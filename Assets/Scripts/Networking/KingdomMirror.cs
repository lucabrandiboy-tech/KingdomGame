using System;
using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Networking
{
    /// <summary>
    /// Online: tells the others what this computer's kingdom is like. Twice a second it lists what's built (each wall
    /// tile and each building, as <see cref="BuiltPiece"/>s) and sends what changed since last time (the first time,
    /// all of it); five times a second, where its villagers are. Things inside houses and mines (far off the map) and
    /// roofs aren't sent: the others see the kingdom from outside.
    /// </summary>
    public sealed class KingdomMirror : MonoBehaviour
    {
        const float BuiltSeconds = 0.5f, FolkSeconds = 0.2f;

        Kingdom kingdom;
        PixelWorld world;
        HashSet<BuiltPiece> sent = new HashSet<BuiltPiece>(), now = new HashSet<BuiltPiece>();
        readonly List<BuiltPiece> removed = new List<BuiltPiece>(), added = new List<BuiltPiece>();
        readonly List<(int Index, Motion Motion)> folk = new List<(int, Motion)>();
        bool sentAll;
        float builtAt, folkAt;

        /// <summary>Sends a kingdom's changes: replace all, what went, what's new.</summary>
        public Action<bool, IReadOnlyList<BuiltPiece>, IReadOnlyList<BuiltPiece>> SendBuilt { get; set; }
        /// <summary>Sends where the villagers are.</summary>
        public Action<IReadOnlyList<(int Index, Motion Motion)>> SendFolk { get; set; }

        public void Setup(Kingdom owner, PixelWorld pixelWorld)
        {
            kingdom = owner;
            world = pixelWorld;
        }

        /// <summary>Sends the whole kingdom again next time (once it's been loaded, or after joining again).</summary>
        public void SendAllAgain() => sentAll = false;

        void Update()
        {
            if (kingdom == null) return;
            float t = Time.unscaledTime;
            if (t >= builtAt && SendBuilt != null)
            {
                builtAt = t + BuiltSeconds;
                Collect(now);
                if (!sentAll)
                {
                    added.Clear();
                    added.AddRange(now);
                    removed.Clear();
                    SendBuilt(true, removed, added);
                    sentAll = true;
                }
                else
                {
                    BuiltChanges.Diff(sent, now, removed, added);
                    if (removed.Count > 0 || added.Count > 0) SendBuilt(false, removed, added);
                }
                var was = sent;
                sent = now;
                now = was;
            }
            if (t >= folkAt && SendFolk != null)
            {
                folkAt = t + FolkSeconds;
                folk.Clear();
                var villagers = kingdom.Villagers;
                for (int i = 0; i < villagers.Count && folk.Count < 255 * Protocol.FolkPerMessage; i++)
                {
                    var v = villagers[i];
                    if (v == null || v.Health == null || v.Health.Dead) continue;
                    var at = v.Position;
                    if (!NatureMap.Inside(new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y)))) continue;   // inside a house or mine
                    var flags = MotionFlags.None;
                    if (v.Moving) flags |= MotionFlags.Walking;
                    if (v.CarryAmount > 0) flags |= MotionFlags.Carrying;
                    folk.Add((i, new Motion { X = at.x, Y = at.y, Aim = v.Facing, Flags = flags }));
                }
                if (folk.Count > 0) SendFolk(folk);
            }
        }

        // What's built now: each standing wall tile of this kingdom (not others' shown here), and each building.
        void Collect(HashSet<BuiltPiece> into)
        {
            into.Clear();
            if (world != null)
                foreach (var cell in world.ChunkCells)
                {
                    var wall = world.ChunkAt(cell)?.Owner;
                    if (wall == null || wall.Remote || wall.Collapsed || wall.Material == null || !NatureMap.Inside(cell)) continue;
                    into.Add(new BuiltPiece { Wall = true, Name = wall.Material.name, X = cell.x, Y = cell.y, W = 1, H = 1 });
                }
            foreach (var thing in kingdom.AllBuilt())
            {
                var config = thing.Config;
                if (config == null || config.kind == BuildingKind.Roof) continue;
                var cells = thing.Cells;
                if (!NatureMap.Inside(cells.position)) continue;
                into.Add(new BuiltPiece
                {
                    Name = config.name, X = cells.x, Y = cells.y, W = cells.width, H = cells.height,
                    Rotation = (byte)Mathf.Clamp(thing.Rotation, 0, 3), Level = (byte)(thing is House house ? Mathf.Clamp(house.Level, 0, 255) : 0),
                });
            }
        }
    }
}
