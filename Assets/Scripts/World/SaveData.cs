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
        /// <summary>Goes up when the layout changes (2: nature is saved as changes to the map).</summary>
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public string mode;
        public int seed;
        public int mapSize;
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
        public bool hasSurvival;
        public SurvivalSave survival = new SurvivalSave();
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

        public static string PathFor(GameMode mode) => Path.Combine(Folder, (mode == GameMode.Survival ? "survival" : "freebuild") + ".json");

        public static bool Exists(GameMode mode) => File.Exists(PathFor(mode));

        public static void Write(GameMode mode, SaveData data)
        {
            Directory.CreateDirectory(Folder);
            string path = PathFor(mode), fresh = path + ".new";
            File.WriteAllText(fresh, JsonUtility.ToJson(data));
            // Swaps the new file in, keeping the old one as .bak, in one step.
            if (File.Exists(path)) File.Replace(fresh, path, path + ".bak");
            else File.Move(fresh, path);
        }

        /// <summary>
        /// The saved game for a mode, or null (with why in <paramref name="problem"/>) if there's none or it
        /// can't be read. If the save itself is broken, the copy before it (.bak) is tried.
        /// </summary>
        public static SaveData Read(GameMode mode, out string problem)
        {
            string path = PathFor(mode);
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
        public static string Describe(GameMode mode)
        {
            var data = Read(mode, out _);
            if (data == null) return null;
            string where = data.hasSurvival && data.survival.wave > 0 ? $"Day {data.day}, wave {data.survival.wave}" : $"Day {data.day}";
            return string.IsNullOrEmpty(data.savedAt) ? where : $"{where}. Saved {data.savedAt}";
        }
    }
}
