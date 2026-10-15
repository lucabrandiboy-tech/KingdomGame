using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.Progression;
using KingdomGame.Villagers;
using KingdomGame.Zombies;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// Saves the game (pause menu, every few minutes, going to the main menu, quitting) and
    /// puts a saved game back after the map is made (see <see cref="SaveData"/> for what's kept).
    /// A new game only saves over the old one once it has been played for a minute, so
    /// starting one by mistake and quitting doesn't lose your kingdom.
    /// </summary>
    public sealed class SaveSystem : MonoBehaviour
    {
        const float NewGameGrace = 60f;

        GameContent content;
        GameMode mode;
        int seed;
        Kingdom kingdom;
        RulerController ruler;
        Research research;
        DayNightCycle dayNight;
        SurvivalDirector waves;
        ZombieDirector zombies;
        PixelWorld world;
        bool loaded;
        bool hosted;
        // The saved game couldn't be (fully) loaded: it's only saved over when the player saves on purpose.
        bool keepOldSave;
        float started, nextAutosave;
        string message;
        float messageUntil;
        GUIStyle style;

        public GameMode Mode => mode;
        /// <summary>Whether this mode has a saved game to load.</summary>
        public bool HasSave => Upload == null && SaveFiles.Exists(mode, hosted);
        /// <summary>A game you host (saved in its own file).</summary>
        public bool Hosted { get => hosted; set => hosted = value; }
        /// <summary>The game being hosted (its players are saved too), or null.</summary>
        public Networking.NetHost Host { get; set; }
        OthersSave loadedOthers;
        /// <summary>The zombie bases (survival), saved with the game, or null.</summary>
        public Zombies.ZombieBases Bases { get; set; }
        /// <summary>
        /// Online, in someone else's game: saving sends this kingdom to the host, who keeps it for next time (no file here).
        /// </summary>
        public System.Action<byte[]> Upload { get; set; }
        /// <summary>Why the game can't be saved yet (online: your kingdom from last time is still on its way), or null.</summary>
        public string Waiting { get; set; }

        /// <summary>A saved game as compressed bytes (to send), and back (null if they aren't one).</summary>
        public static byte[] Pack(SaveData data)
        {
            var json = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
            using (var output = new System.IO.MemoryStream())
            {
                using (var zip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionLevel.Optimal)) zip.Write(json, 0, json.Length);
                return output.ToArray();
            }
        }

        public static SaveData Unpack(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                using (var input = new System.IO.MemoryStream(bytes))
                using (var zip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress))
                using (var reader = new System.IO.StreamReader(zip, System.Text.Encoding.UTF8))
                    return JsonUtility.FromJson<SaveData>(reader.ReadToEnd());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Couldn't read the kingdom the server kept: {e.Message}");
                return null;
            }
        }

        public void Setup(GameContent gameContent, GameMode gameMode, int mapSeed, Kingdom owner, RulerController rulerController,
            Research tech, DayNightCycle clock, SurvivalDirector survival, ZombieDirector zombieDirector, PixelWorld pixelWorld)
        {
            content = gameContent;
            mode = gameMode;
            seed = mapSeed;
            kingdom = owner;
            ruler = rulerController;
            research = tech;
            dayNight = clock;
            waves = survival;
            zombies = zombieDirector;
            world = pixelWorld;
            started = Time.unscaledTime;
            ScheduleAutosave();
        }

        void ScheduleAutosave()
        {
            float minutes = content != null && content.world != null ? content.world.autosaveMinutes : 3f;
            nextAutosave = minutes > 0f ? Time.unscaledTime + minutes * 60f : float.PositiveInfinity;
        }

        void Update()
        {
            if (Time.unscaledTime < nextAutosave) return;
            ScheduleAutosave();
            if (SafeToOverwrite()) Save(true);
        }

        void OnApplicationQuit()
        {
            if (SafeToOverwrite()) Save(true);
        }

        /// <summary>
        /// Whether saving now on its own (autosave, going to the menu, quitting) is safe: not over a fallen
        /// game, not a brand-new game over an old save, and not after a save that couldn't be loaded.
        /// </summary>
        public bool SafeToOverwrite() =>
            !keepOldSave && CanSave(out _) && (loaded || !HasSave || Time.unscaledTime - started >= NewGameGrace);

        /// <summary>
        /// Continuing a saved game failed (<paramref name="why"/>): this game won't save over it on its
        /// own, only if the player saves on purpose.
        /// </summary>
        public void KeepOldSave(string why)
        {
            keepOldSave = true;
            Say($"{why}. Your saved game is kept: this game only saves over it if you save (Esc).", 10f);
        }

        /// <summary>Whether the game can be saved right now (not once the ruler has fallen).</summary>
        public bool CanSave(out string why)
        {
            why = null;
            if (kingdom == null || ruler == null) why = "Nothing to save";
            else if (waves != null && waves.Now == SurvivalDirector.Phase.Over) why = "You have fallen: there's nothing to save";
            else if (ruler.Dead) why = "Wait until you're back on your feet";
            else if (Waiting != null) why = Waiting;
            return why == null;
        }

        /// <summary>Saves now. Returns a message for the player.</summary>
        public string Save(bool quietly = false)
        {
            if (!CanSave(out string why)) return Say(why);
            try
            {
                if (Upload != null)
                {
                    Upload(Pack(Collect()));
                    loaded = true;
                    keepOldSave = false;
                    return Say("Kingdom saved on the host", 2f);
                }
                SaveFiles.Write(mode, Collect(), hosted);
                loaded = true;   // this game is the saved one now
                keepOldSave = false;
                return quietly ? Say("Game saved", 2f) : Say("Game saved");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Saving the game failed: {e}");
                return Say("Saving failed (see the Console)");
            }
        }

        string Say(string text, float seconds = 3f)
        {
            message = text;
            messageUntil = Time.unscaledTime + seconds;
            return text;
        }

        void OnGUI()
        {
            if (message == null || Time.unscaledTime > messageUntil) return;
            int font = Mathf.Max(12, Screen.height / 50);
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, richText = true };
            style.fontSize = font;
            GUI.Label(new Rect(0, 0, Screen.width - font, Screen.height - font * 4), $"<color=#e8dcc0>{message}</color>", style);
        }

        // ------------------------------------------------------------ saving --

        SaveData Collect()
        {
            var data = new SaveData
            {
                mode = mode.ToString(),
                seed = seed,
                landsSize = kingdom.Nav != null ? kingdom.Nav.Cells.width : 0,
                savedAt = System.DateTime.Now.ToString("d MMM HH:mm"),
            };
            if (dayNight != null)
            {
                data.day = dayNight.Day;
                data.hour = dayNight.Hour;
            }

            // Nature: every tree, rock and bush that isn't as the map made it (cut, planted, gone).
            var nature = NatureMap.Current;
            if (nature != null)
                foreach (var c in nature.Changes())
                    data.nodes.Add(new NodeSave
                    {
                        cell = c.Cell,
                        config = c.Kind != null ? c.Kind.name : "",
                        planted = c.Planted,
                        variant = c.Variant,
                        left = c.Left,
                        grown = c.Grown,
                        growSeconds = c.GrowSeconds,
                        regrowLeft = c.RegrowLeft,
                    });

            // Walls: each stretch once.
            if (world != null)
            {
                var seen = new HashSet<DestructibleStructure>();
                foreach (var cell in world.ChunkCells)
                {
                    var wall = world.ChunkAt(cell)?.Owner;
                    if (wall == null || wall.Collapsed || wall.Remote || wall.Material == null || !seen.Add(wall)) continue;
                    var save = new WallSave { material = wall.Material.name, cells = wall.Cells, battered = wall.Battered };
                    foreach (var c in wall.Cells.allPositionsWithin)
                        if (world.ChunkAt(c)?.Owner != wall) save.gone.Add(c);
                    data.walls.Add(save);
                }
            }

            foreach (var thing in kingdom.AllBuilt())
            {
                if (thing.Config == null) continue;
                var save = new BuiltSave { config = thing.Config.name, cells = thing.Cells, rotation = thing.Rotation };
                var health = (thing as Component) != null ? ((Component)thing).GetComponent<Health>() : null;
                if (health != null && health.Max > 0f) save.health = health.Current;
                switch (thing)
                {
                    case House house:
                        save.slot = house.Slot;
                        save.level = house.Level;
                        if (house.Upgrade != null && !house.Upgrade.Cancelled)
                        {
                            save.upgrading = true;
                            save.upgradeDelivered = Delivered(house.Upgrade);
                            save.upgradeWork = house.Upgrade.Work;
                        }
                        break;
                    case MineShaft mine:
                        save.slot = mine.Slot;
                        if (mine.Level != null)
                        {
                            save.dug = mine.Level.DugText();
                            save.marked = mine.Level.MarkedText();
                        }
                        break;
                    case Storage storage when storage.Stock != null:
                        save.stock = new int[Inventory.TypeCount];
                        for (int i = 0; i < save.stock.Length; i++) save.stock[i] = storage.Stock.Get((ResourceType)i);
                        break;
                }
                data.built.Add(save);
            }

            foreach (var b in kingdom.Blueprints)
            {
                if (b == null || b.Cancelled || b.Upgrades != null || b.Config == null) continue;
                data.blueprints.Add(new BlueprintSave { config = b.Config.name, cells = b.Cells, rotation = b.Rotation, delivered = Delivered(b), work = b.Work });
            }

            foreach (var v in kingdom.Villagers)
            {
                if (v == null || v.Health == null || v.Health.Dead) continue;
                data.villagers.Add(new VillagerSave
                {
                    position = v.Position,
                    job = (int)v.Job,
                    carryType = (int)v.CarryType,
                    carryAmount = v.CarryAmount,
                    health = v.Health.Current,
                });
            }

            foreach (var bag in LootBag.All)
            {
                if (bag == null || bag.Total <= 0) continue;
                var amounts = new int[Inventory.TypeCount];
                for (int i = 0; i < amounts.Length; i++) amounts[i] = bag.Amount((ResourceType)i);
                data.bags.Add(new BagSave { position = bag.Position, amounts = amounts });
            }

            // On the throne the ruler sits inside it: they come back standing in front of it.
            data.ruler.position = ruler.Seated && kingdom.Throne != null ? kingdom.Throne.Front : (Vector2)ruler.transform.position;
            data.ruler.onWalls = ruler.OnWalls;
            data.ruler.health = ruler.Health != null ? ruler.Health.Current : 1f;
            data.ruler.backpack = new int[Inventory.TypeCount];
            if (ruler.Backpack != null)
                for (int i = 0; i < data.ruler.backpack.Length; i++) data.ruler.backpack[i] = ruler.Backpack.Get((ResourceType)i);

            if (research != null) data.research = research.Save();
            if (kingdom.Armory != null) data.armory = kingdom.Armory.Save();
            // The players who joined (their kingdoms and where they are). If hosting didn't start this time,
            // the ones from the save are kept as they were.
            data.others = Host != null && Host.Core != null ? OthersSave.From(Host.Core) : loadedOthers ?? new OthersSave();
            // Experience and the villagers' upgrades; the zombie bases.
            data.xp = kingdom.Xp;
            data.upgrades = kingdom.UpgradeLevels();
            if (Bases != null)
                foreach (var (at, health) in Bases.Saved()) data.zombieBases.Add(new ZombieBaseSave { position = at, health = health });
            if (waves != null)
            {
                data.hasSurvival = true;
                data.survival = new SurvivalSave
                {
                    phase = (int)waves.Now,
                    wave = waves.Wave,
                    timeLeft = waves.TimeLeft,
                    secondsSurvived = waves.SecondsSurvived,
                    kills = waves.Kills,
                };
            }
            return data;
        }

        static int[] Delivered(Blueprint b)
        {
            var delivered = new int[Inventory.TypeCount];
            for (int i = 0; i < delivered.Length; i++) delivered[i] = b.Delivered((ResourceType)i);
            return delivered;
        }

        // ----------------------------------------------------------- loading --

        /// <summary>
        /// Puts a saved game back. Call once everything is set up (the map made from the saved
        /// seed, the kingdom with only its throne, research, the zombies). Returns how many
        /// things couldn't be put back (their asset is gone or renamed).
        /// </summary>
        public int Restore(SaveData data)
        {
            if (data == null) return 0;
            loadedOthers = data.others;
            int missing;
            try
            {
                missing = RestoreAll(data);
            }
            catch (System.Exception e)
            {
                // Half put back: playing on is fine, but this must never be saved over the real save on its own.
                Debug.LogError($"Loading the saved game failed partway: {e}");
                KeepOldSave("Loading the saved game failed partway (see the Console)");
                return -1;
            }
            loaded = true;
            if (missing > 0) KeepOldSave($"{missing} saved things couldn't be put back (their assets are gone or renamed)");
            else Say(Upload != null ? "Your kingdom is back" : "Game loaded");
            return missing;
        }

        int RestoreAll(SaveData data)
        {
            int missing = 0;
            if (data.version < 3)
                Debug.LogWarning("This game was saved before worlds were endless: what you built comes back, but the trees and ground round it are new.");

            if (dayNight != null && Upload == null) dayNight.SetTime(data.day, data.hour);   // (online the host's clock is the time)
            if (research != null && data.research != null) research.Load(data.research);
            if (kingdom.Armory != null && data.armory != null) kingdom.Armory.Load(data.armory);

            // Building configs and wall materials by name.
            var configs = new Dictionary<string, BuildingConfig>();
            void AddConfig(BuildingConfig b)
            {
                if (b != null && !configs.ContainsKey(b.name)) configs[b.name] = b;
            }
            AddConfig(content.storehouse);
            if (content.buildMenu != null) foreach (var b in content.buildMenu) AddConfig(b);
            var materials = new Dictionary<string, StructureMaterial>();
            foreach (var m in content.WallMaterials())
                if (m != null && !materials.ContainsKey(m.name)) materials[m.name] = m;
            var nodeKinds = new Dictionary<string, ResourceNodeConfig>();
            void AddKind(ResourceNodeConfig k)
            {
                if (k != null && !nodeKinds.ContainsKey(k.name)) nodeKinds[k.name] = k;
            }
            AddKind(content.tree);
            AddKind(content.rock);
            AddKind(content.bush);
            foreach (var b in configs.Values) AddKind(b.plant);

            // Nature first (buildings went up where trees were cut down).
            var nature = NatureMap.Current;
            if (nature != null)
                foreach (var n in data.nodes)
                {
                    ResourceNodeConfig kind = null;
                    if (!string.IsNullOrEmpty(n.config) && !nodeKinds.TryGetValue(n.config, out kind)) { missing++; continue; }
                    nature.Apply(new NatureMap.Change
                    {
                        Cell = n.cell, Kind = kind, Planted = n.planted, Variant = n.variant, Left = n.left,
                        Grown = n.grown, GrowSeconds = n.growSeconds, RegrowLeft = n.regrowLeft,
                    });
                }

            // Walls (a stretch with pieces broken off comes back as the pieces that are left).
            foreach (var w in data.walls)
            {
                if (w.material == null || !materials.TryGetValue(w.material, out var material)) { missing++; continue; }
                if (w.gone == null || w.gone.Count == 0)
                {
                    Wall(material, w.cells).RestoreBattered(w.battered);
                    continue;
                }
                int total = w.cells.width * w.cells.height;
                foreach (var c in w.cells.allPositionsWithin)
                    if (!w.gone.Contains(c)) Wall(material, new RectInt(c, Vector2Int.one)).RestoreBattered(w.battered / Mathf.Max(1, total));
            }

            // Everything built (houses and mines come first in the save).
            foreach (var b in data.built)
            {
                if (b.config == null || !configs.TryGetValue(b.config, out var config)) { missing++; continue; }
                var made = kingdom.Restore(config, b.cells, b.rotation, b.slot);
                if (made == null) { missing++; continue; }
                switch (made)
                {
                    case House house:
                        if (b.level > 0) house.SetLevel(b.level);
                        if (b.upgrading)
                        {
                            var upgrade = kingdom.RestoreUpgrade(house);
                            if (upgrade != null) Fill(upgrade, b.upgradeDelivered, b.upgradeWork);
                        }
                        break;
                    case MineShaft mine:
                        if (mine.Level != null) mine.Level.Restore(b.dug, b.marked);
                        break;
                    case Storage storage when b.stock != null:
                        for (int i = 0; i < b.stock.Length && i < Inventory.TypeCount; i++)
                            if (b.stock[i] > 0) storage.Deposit((ResourceType)i, b.stock[i]);
                        break;
                }
                if (b.health > 0f)
                {
                    var health = made.GetComponent<Health>();
                    if (health != null) health.SetCurrent(b.health);
                }
            }

            foreach (var b in data.blueprints)
            {
                if (b.config == null || !configs.TryGetValue(b.config, out var config)) { missing++; continue; }
                var blueprint = kingdom.RestoreBlueprint(config, b.cells, b.rotation);
                if (blueprint != null) Fill(blueprint, b.delivered, b.work);
            }

            foreach (var v in data.villagers)
            {
                var at = Reachable(v.position);
                var villager = kingdom.AddVillager(at, (Villagers.Job)v.job);
                villager.Restore(kingdom.PlaceAt(at), (ResourceType)Mathf.Clamp(v.carryType, 0, Inventory.TypeCount - 1), v.carryAmount, v.health);
            }

            foreach (var bag in data.bags) LootBag.Drop(bag.position, bag.amounts);

            var rulerAt = Reachable(data.ruler.position, true);
            ruler.Restore(rulerAt, kingdom.PlaceAt(rulerAt), data.ruler.onWalls, data.ruler.health, data.ruler.backpack);

            kingdom.RestoreUpgrades(data.xp, data.upgrades);
            if (Bases != null && data.zombieBases != null)
                foreach (var b in data.zombieBases) Bases.Place(b.position, b.health);
            if (waves != null && data.hasSurvival)
            {
                var s = data.survival;
                waves.Restore((SurvivalDirector.Phase)Mathf.Clamp(s.phase, 0, (int)SurvivalDirector.Phase.Calm), s.wave, s.timeLeft, s.secondsSurvived, s.kills);
            }
            else if (zombies != null && data.hasSurvival)
            {
                zombies.RestoreKills(data.survival.kills);
            }

            if (missing > 0) Debug.LogWarning($"{missing} saved things couldn't be put back: their assets are gone or renamed.");
            return missing;
        }

        // Somewhere they can be: where they were, unless that was inside a house or mine that couldn't
        // be put back (far off the map, in nothing): then by the throne. Villagers stay on the kingdom's
        // lands; the ruler can have been anywhere in the world (`anywhere`).
        Vector2 Reachable(Vector2 position, bool anywhere = false)
        {
            var cell = NavGrid.CellAt(position);
            if (kingdom.Nav.Cells.Contains(cell) || (anywhere && NatureMap.Inside(cell)) || kingdom.PlaceAt(position) != null) return position;
            return kingdom.Throne != null ? kingdom.Throne.Front : Vector2.zero;
        }

        DestructibleStructure Wall(StructureMaterial material, RectInt cells)
        {
            var nature = NatureMap.Current;
            if (nature != null) foreach (var c in cells.allPositionsWithin) nature.Clear(c);
            var go = new GameObject($"{material.name} wall");
            go.transform.SetParent(kingdom.transform, false);
            go.transform.position = new Vector3(cells.x, cells.y, 0f);
            var wall = go.AddComponent<DestructibleStructure>();
            wall.Build(world, material, cells.size);
            return wall;
        }

        static void Fill(Blueprint blueprint, int[] delivered, float work)
        {
            if (delivered != null)
                for (int i = 0; i < delivered.Length && i < Inventory.TypeCount; i++)
                    if (delivered[i] > 0) blueprint.Deliver((ResourceType)i, delivered[i]);
            if (work > 0f) blueprint.AddWork(work);
        }
    }
}
