using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// A saved game, written as JSON (Unity's JsonUtility). The map isn't saved, only its
    /// seed: loading makes the same map again and then puts back what changed. Things are
    /// saved by their asset names (a Building Config, a Wall Material), so renaming an
    /// asset loses that kind of thing from old saves.
    /// Not saved: zombies that are about, bullet holes in walls (walls come back whole,
    /// with their zombie damage), bodies, and what villagers were halfway through doing.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>
        /// Goes up when the layout changes (2: nature is saved as changes to the map; 3: endless worlds,
        /// the kingdom's lands, and the other players on a hosted world).
        /// </summary>
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public string mode;
        public int seed;
        /// <summary>Tiles across the kingdom's lands (round the throne); 0 in saves from before endless worlds.</summary>
        public int landsSize;
        /// <summary>When it was saved, as local time text (for the menu).</summary>
        public string savedAt;
        public int day = 1;
        public float hour;

        /// <summary>Trees, rocks and bushes that aren't as the map made them.</summary>
        public List<NodeSave> nodes = new List<NodeSave>();
        public List<WallSave> walls = new List<WallSave>();
        public List<BuiltSave> built = new List<BuiltSave>();
        public List<BlueprintSave> blueprints = new List<BlueprintSave>();
        public List<VillagerSave> villagers = new List<VillagerSave>();
        public List<BagSave> bags = new List<BagSave>();
        public RulerSave ruler = new RulerSave();
        public Progression.Research.Saved research = new Progression.Research.Saved();
        public Combat.Armory.Saved armory = new Combat.Armory.Saved();
        public bool hasSurvival;
        public SurvivalSave survival = new SurvivalSave();
        /// <summary>A hosted game: the players who joined it (their kingdoms, and where they were).</summary>
        public OthersSave others = new OthersSave();
        /// <summary>Experience (from zombie bases) and the villagers' upgrade levels (speed, gathering, crafting).</summary>
        public int xp;
        public int[] upgrades = new int[0];
        public List<ZombieBaseSave> zombieBases = new List<ZombieBaseSave>();
        /// <summary>The ruins out in the world that have been found, searched or fought over.</summary>
        public List<Ruins.State> ruins = new List<Ruins.State>();
        /// <summary>The goals of the ages done so far, and the counts they need.</summary>
        public Progression.Goals.Saved goals;
    }

    [Serializable]
    public sealed class ZombieBaseSave
    {
        public Vector2 position;
        public float health;
    }

    /// <summary>The players who joined a hosted world or a server: their kingdoms' spots, and each player.</summary>
    [Serializable]
    public sealed class OthersSave
    {
        public List<KingdomSpotSave> kingdoms = new List<KingdomSpotSave>();
        public List<PlayerSpotSave> players = new List<PlayerSpotSave>();
        /// <summary>What each of their kingdoms has built (for the others to see) and each one's saved kingdom.</summary>
        public List<KingdomBuiltSave> built = new List<KingdomBuiltSave>();

        public static OthersSave From(Networking.ServerCore core)
        {
            var save = new OthersSave();
            if (core == null) return save;
            var (kingdoms, players) = core.Remembered();
            foreach (var k in kingdoms) save.kingdoms.Add(new KingdomSpotSave { id = k.Id, owner = k.Owner, x = k.X, y = k.Y });
            foreach (var p in players)
                save.players.Add(new PlayerSpotSave { key = p.Key, name = p.Name, kingdom = p.KingdomId, placed = p.HasPosition, x = p.X, y = p.Y });
            foreach (var m in core.Memories())
            {
                var b = new KingdomBuiltSave { kingdom = m.KingdomId, save = m.Save != null ? Convert.ToBase64String(m.Save) : "" };
                foreach (var p in m.Pieces)
                    b.pieces.Add(new PieceSave { wall = p.Wall, name = p.Name, x = p.X, y = p.Y, w = p.W, h = p.H, rotation = p.Rotation, level = p.Level });
                save.built.Add(b);
            }
            return save;
        }

        /// <summary>Hands what was saved to a server that's just been made (before anyone joins).</summary>
        public void Into(Networking.ServerCore core)
        {
            if (core == null) return;
            var k = new List<Networking.KingdomInfo>();
            foreach (var s in kingdoms) if (s != null) k.Add(new Networking.KingdomInfo { Id = s.id, Owner = s.owner, X = s.x, Y = s.y });
            var p = new List<Networking.PlayerRecord>();
            foreach (var s in players)
                if (s != null) p.Add(new Networking.PlayerRecord { Key = s.key, Name = s.name, KingdomId = s.kingdom, HasPosition = s.placed, X = s.x, Y = s.y });
            core.Remember(k, p);
            var memories = new List<Networking.KingdomMemory>();
            if (built != null)
                foreach (var b in built)
                {
                    if (b == null) continue;
                    var m = new Networking.KingdomMemory { KingdomId = b.kingdom };
                    if (!string.IsNullOrEmpty(b.save))
                    {
                        try { m.Save = Convert.FromBase64String(b.save); }
                        catch (FormatException) { Debug.LogWarning($"Kingdom {b.kingdom}'s saved kingdom is broken: its player starts afresh."); }
                    }
                    if (b.pieces != null)
                        foreach (var s in b.pieces)
                            if (s != null)
                                m.Pieces.Add(new Networking.BuiltPiece
                                {
                                    Wall = s.wall, Name = s.name, X = s.x, Y = s.y, W = s.w, H = s.h,
                                    Rotation = (byte)Mathf.Clamp(s.rotation, 0, 255), Level = (byte)Mathf.Clamp(s.level, 0, 255),
                                });
                    memories.Add(m);
                }
            core.RememberBuilt(memories);
        }
    }

    /// <summary>What one other player's kingdom has built, and their saved kingdom (compressed, as text).</summary>
    [Serializable]
    public sealed class KingdomBuiltSave
    {
        public int kingdom;
        public List<PieceSave> pieces = new List<PieceSave>();
        public string save;
    }

    /// <summary>One wall tile or building of another player's kingdom (see Networking.BuiltPiece).</summary>
    [Serializable]
    public sealed class PieceSave
    {
        public bool wall;
        public string name;
        public int x, y, w, h, rotation, level;
    }

    [Serializable]
    public sealed class KingdomSpotSave
    {
        public int id;
        public string owner;
        /// <summary>The throne's bottom-left cell.</summary>
        public int x, y;
    }

    [Serializable]
    public sealed class PlayerSpotSave
    {
        /// <summary>Who they are (the code their game keeps, and their name).</summary>
        public string key;
        public string name;
        public int kingdom;
        /// <summary>Whether x, y is where they were when they left (else they start at their throne).</summary>
        public bool placed;
        public float x, y;
    }

    /// <summary>A dedicated server's world, kept between runs: its seed, the time, and everyone's kingdoms.</summary>
    [Serializable]
    public sealed class ServerWorldSave
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public int seed;
        public int day = 1;
        public float hour;
        public string savedAt;
        public OthersSave others = new OthersSave();
    }

    /// <summary>A tree, rock or bush that isn't as the map made it (planted, partly used, growing back, gone).</summary>
    [Serializable]
    public sealed class NodeSave
    {
        public Vector2Int cell;
        /// <summary>What stands there now (its asset's name), or empty if nothing does any more.</summary>
        public string config;
        public bool planted;
        public int variant;
        public int left;
        /// <summary>0..1 while a young one grows, -1 otherwise.</summary>
        public float grown = -1f;
        public float growSeconds;
        public float regrowLeft = -1f;
    }

    /// <summary>One wall (a stretch built in one go): its material, cells and zombie damage.</summary>
    [Serializable]
    public sealed class WallSave
    {
        public string material;
        public RectInt cells;
        /// <summary>Cells of it that are gone (broken off); empty if it's all there.</summary>
        public List<Vector2Int> gone = new List<Vector2Int>();
        public float battered;
    }

    /// <summary>Something finished: a house, mine, storehouse, door, piece of furniture, floor or roof tile.</summary>
    [Serializable]
    public sealed class BuiltSave
    {
        public string config;
        public RectInt cells;
        public int rotation;
        /// <summary>Hit points, or -1 for things without any.</summary>
        public float health = -1f;
        /// <summary>Houses and mines: which inside square they have (things inside are saved where they are).</summary>
        public int slot = -1;
        public int level;
        /// <summary>Storehouses and chests: how much of each resource.</summary>
        public int[] stock;
        /// <summary>Mines: what's dug and what's marked (see MineLevel.DugText).</summary>
        public string dug;
        public string marked;
        /// <summary>Houses being upgraded: what's been brought and how much work is done.</summary>
        public bool upgrading;
        public int[] upgradeDelivered;
        public float upgradeWork;
    }

    [Serializable]
    public sealed class BlueprintSave
    {
        public string config;
        public RectInt cells;
        public int rotation;
        public int[] delivered;
        public float work;
    }

    [Serializable]
    public sealed class VillagerSave
    {
        public Vector2 position;
        public int job;
        public int carryType;
        public int carryAmount;
        public float health;
        /// <summary>How fed and rested (1 = fully; older saves: fully).</summary>
        public float fed = 1f;
        public float rested = 1f;
        /// <summary>A survivor's name (empty for the first villagers).</summary>
        public string name;
        /// <summary>Bitten: seconds until it turns into a zombie (-1: it isn't).</summary>
        public float turnsIn = -1f;
        /// <summary>A soldier's weapon and armor (their names in the Armory Config; empty: none) and experience.</summary>
        public string weapon;
        public string armor;
        public float xp;
    }

    [Serializable]
    public sealed class BagSave
    {
        public Vector2 position;
        public int[] amounts;
    }

    [Serializable]
    public sealed class RulerSave
    {
        public Vector2 position;
        public bool onWalls;
        public float health;
        public int[] backpack;
    }

    [Serializable]
    public sealed class SurvivalSave
    {
        public int phase;
        public int wave;
        public float timeLeft;
        public float secondsSurvived;
        public int kills;
    }

    /// <summary>
    /// Where saved games are kept: one per mode, in the game's own folder (on Windows
    /// C:\Users\you\AppData\LocalLow\(company)\(product)\saves). Writing goes to a new
    /// file first and the last save is kept as a .bak, so a crash while saving can't
    /// lose both.
    /// </summary>
    public static class SaveFiles
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "saves");

        /// <summary>Writes <paramref name="json"/> to a new file, then swaps it in (keeping the old one as .bak).</summary>
        internal static void WriteSafely(string path, string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string fresh = path + ".new";
            File.WriteAllText(fresh, json);
            if (File.Exists(path)) File.Replace(fresh, path, path + ".bak");
            else File.Move(fresh, path);
        }

        /// <summary>A mode's save file; games you host (open to others) have their own.</summary>
        public static string PathFor(GameMode mode, bool hosted = false) =>
            Path.Combine(Folder, (mode == GameMode.Survival ? "survival" : "freebuild") + (hosted ? "-hosted" : "") + ".json");

        public static bool Exists(GameMode mode, bool hosted = false) => File.Exists(PathFor(mode, hosted));

        public static void Write(GameMode mode, SaveData data, bool hosted = false) => WriteSafely(PathFor(mode, hosted), JsonUtility.ToJson(data));

        /// <summary>
        /// The saved game for a mode, or null (with why in <paramref name="problem"/>) if there's none or it
        /// can't be read. If the save itself is broken, the copy before it (.bak) is tried.
        /// </summary>
        public static SaveData Read(GameMode mode, out string problem, bool hosted = false)
        {
            string path = PathFor(mode, hosted);
            var data = ReadFile(path, out problem);
            if (data != null || !File.Exists(path)) return data;
            var older = ReadFile(path + ".bak", out string olderProblem);
            if (older == null) return null;
            Debug.LogWarning($"{problem}: loading the save before it instead ({path}.bak).");
            problem = null;
            return older;
        }

        static SaveData ReadFile(string path, out string problem)
        {
            problem = null;
            try
            {
                if (!File.Exists(path))
                {
                    problem = "There's no saved game";
                    return null;
                }
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                if (data == null) problem = "The saved game is empty";
                else if (data.version > SaveData.CurrentVersion) problem = "The saved game is from a newer version of the game";
                return problem == null ? data : null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't read the saved game {path}: {e.Message}");
                problem = "The saved game couldn't be read";
                return null;
            }
        }

        /// <summary>A line about a mode's saved game for the main menu (like "Day 3, wave 5. Saved 10 Oct 14:32"), or null.</summary>
        public static string Describe(GameMode mode, bool hosted = false)
        {
            var data = Read(mode, out _, hosted);
            if (data == null) return null;
            string where = data.hasSurvival && data.survival.wave > 0 ? $"Day {data.day}, wave {data.survival.wave}" : $"Day {data.day}";
            return string.IsNullOrEmpty(data.savedAt) ? where : $"{where}. Saved {data.savedAt}";
        }
    }

    /// <summary>
    /// The dedicated server's world file (saves/server-world.json, or another name with
    /// -world name): read when the server starts, written every minute and when it stops.
    /// </summary>
    public static class ServerWorldFile
    {
        public static string PathFor(string name = null)
        {
            string clean = "";
            foreach (char c in name ?? "") if (char.IsLetterOrDigit(c) || c == '-' || c == '_') clean += c;
            return Path.Combine(SaveFiles.Folder, (clean.Length > 0 ? "server-" + clean : "server-world") + ".json");
        }

        public static void Write(ServerWorldSave data, string name = null)
        {
            data.version = ServerWorldSave.CurrentVersion;
            data.savedAt = DateTime.Now.ToString("d MMM HH:mm");
            SaveFiles.WriteSafely(PathFor(name), JsonUtility.ToJson(data, true));
        }

        /// <summary>The server's world from last time, or null: a new world (<paramref name="problem"/> says why if the file was there but broken).</summary>
        public static ServerWorldSave Read(out string problem, string name = null)
        {
            problem = null;
            string path = PathFor(name);
            if (!File.Exists(path)) return null;
            foreach (var file in new[] { path, path + ".bak" })
            {
                try
                {
                    if (!File.Exists(file)) continue;
                    var data = JsonUtility.FromJson<ServerWorldSave>(File.ReadAllText(file));
                    if (data != null && data.seed != 0 && data.version <= ServerWorldSave.CurrentVersion)
                    {
                        if (file != path) Debug.LogWarning($"[Server] {path} couldn't be read: using the copy before it ({file}).");
                        return data;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Server] Couldn't read {file}: {e.Message}");
                }
            }
            problem = $"The server's world file ({path}) couldn't be read, so this is a new world. The old file is kept as {path}.broken";
            try { File.Copy(path, path + ".broken", true); } catch (Exception) { }
            return null;
        }
    }
}
