using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.Networking;
using KingdomGame.Progression;
using KingdomGame.UI;
using KingdomGame.Zombies;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KingdomGame.World
{
    /// <summary>
    /// Builds the game when you press Play: the map, your kingdom (throne,
    /// storehouse, builder villagers), the ruler with their gear, day and night,
    /// and the on-screen info. In survival the zombies too; in free build the
    /// test outpost instead. The mode comes from the main menu, or is
    /// <see cref="DefaultMode"/> when this scene is played on its own.
    /// </summary>
    public sealed class KingdomBootstrap : MonoBehaviour
    {
        [SerializeField] GameContent content;
        [SerializeField] DayNightCycle dayNight;
        [Tooltip("The mode when you press Play in this scene directly (the main menu chooses otherwise).")]
        [SerializeField] GameMode defaultMode = GameMode.Survival;

        public GameContent Content { get => content; set => content = value; }
        public DayNightCycle DayNight { get => dayNight; set => dayNight = value; }
        public GameMode DefaultMode { get => defaultMode; set => defaultMode = value; }
        /// <summary>The mode this game is in.</summary>
        public GameMode Mode { get; private set; }

        void Start()
        {
            var world = PixelWorld.Current;
            if (world == null || content == null || content.world == null)
            {
                Debug.LogError("The game scene needs a PixelWorld and its Content set. Run KingdomGame > Build Game Scenes again.", this);
                return;
            }
            Look.Configure(content.view);
            var walls = new System.Collections.Generic.List<Art.WallLook>();
            foreach (var material in content.WallMaterials()) walls.Add(WallTiles.LookOf(material));
            ArtSprites.Configure(content.art, walls);
            if (content.dayNight != null)
                FireLight.Configure(content.dayNight.fireColor, content.dayNight.fireRadius, content.dayNight.fireBrightness, content.dayNight.fireDaytimeShare);
            Mode = GameSession.Chosen ?? defaultMode;

            switch (GameSession.Role)
            {
                case NetRole.Client: StartJoining(world); break;
                case NetRole.Server: StartDedicated(); break;
                default: StartPlaying(world, GameSession.Role == NetRole.Host); break;
            }
        }

        // Playing your own game: alone, or hosting it (open to others: they join over the network).
        void StartPlaying(PixelWorld world, bool hosting)
        {
            bool survival = Mode == GameMode.Survival;
            if (survival && content.survival == null)
            {
                Debug.LogWarning("The Game Content has no Survival Config, so this game has no zombies. Run KingdomGame > Build Game Scenes again.", this);
                survival = false;
            }
            var network = content.network;
            if (hosting)
            {
                Application.runInBackground = true;   // keeps going for the others while this window isn't in front
                if (network == null) Debug.LogWarning("The Game Content has no Network Config: hosting with the defaults. Run KingdomGame > Build Game Scenes again.", this);
            }

            // Continuing a saved game: the same world again (from its seed), then what was built on it.
            SaveData saved = null;
            string loadProblem = null;
            if (GameSession.LoadSave)
            {
                GameSession.LoadSave = false;
                saved = SaveFiles.Read(Mode, out loadProblem, hosting);
                if (saved == null) Debug.LogWarning($"{loadProblem}: starting a new game instead.");
            }

            int configSeed = hosting && network != null && network.seed != 0 ? network.seed : content.world.seed;
            int seed = saved != null ? saved.seed : configSeed != 0 ? configSeed : Random.Range(1, int.MaxValue);
            Debug.Log($"World seed {seed}. Put it in WorldConfig's Seed to get the same world again.");
            var parts = WorldGenerator.Generate(content, transform, seed, true);
            // The test outpost is for trying weapons on walls; survival (and hosted games) start on open ground.
            // (A saved game has its own walls: the outpost's are among them if they still stand.)
            if (content.world.testOutpost && !survival && !hosting) BuildOutpost(world, saved == null);
            new GameObject("Remains").AddComponent<Remains>().transform.SetParent(transform, false);

            int lands = saved != null && saved.landsSize > 0 ? saved.landsSize : LandsSize();
            var kingdom = MakeKingdom(world, survival ? content.survival : null, saved == null, lands, null, true);
            var ruler = SpawnRuler(kingdom.Throne != null ? kingdom.Throne.Front : Vector2.zero);
            var rulerController = ruler.GetComponent<RulerController>();
            var rig = Follow(parts, kingdom, ruler);

            // The ages: survival researches them (days of real time each), free build has them all.
            var research = gameObject.AddComponent<Research>();
            var unlocking = !survival ? Research.Unlocking.Everything
                : content.progression != null && content.progression.survivalAgesByWaves ? Research.Unlocking.ByAge : Research.Unlocking.ByResearch;
            research.Setup(content.progression, kingdom, unlocking);
            gameObject.AddComponent<TechScreen>().Setup(research);
            gameObject.AddComponent<VillagersPanel>().Setup(kingdom);

            // The ruler's weapons: a club to start with, the rest made at an anvil (free build has them all).
            if (content.armory != null)
            {
                var armory = kingdom.gameObject.AddComponent<Armory>();
                armory.Setup(content.armory, kingdom, unlocking == Research.Unlocking.Everything);
                kingdom.Armory = armory;
                rulerController.Armory = armory;
                var armoryPanel = gameObject.AddComponent<ArmoryPanel>();
                armoryPanel.Setup(armory, kingdom);
                rulerController.OpenArmory = armoryPanel.Open;
            }
            else Debug.LogWarning("The Game Content has no Armory Config, so the ruler has the old rifle. Run KingdomGame > Build Game Scenes again.", this);
            var storagePanel = gameObject.AddComponent<StoragePanel>();
            storagePanel.Setup(rulerController);
            rulerController.OpenStorage = storagePanel.Open;

            var builder = kingdom.gameObject.AddComponent<BuildController>();
            builder.Setup(kingdom, rulerController, rig, parts.Canopies, content.view);

            // Survival: the zombies, and the waves they come in. Falling ends the game.
            SurvivalDirector waves = null;
            rulerController.RespawnOnDeath = !survival;
            // Zombies: waves in survival; in free build only the test horde (F9) for trying traps and towers.
            ZombieDirector zombies = null;
            if (content.survival != null)
            {
                zombies = new GameObject("Zombies").AddComponent<ZombieDirector>();
                zombies.transform.SetParent(transform, false);
                zombies.Setup(content.survival, world, kingdom, rulerController, dayNight, kingdom.Nav.Cells);
                zombies.TestHordeKey = !survival;
            }
            if (survival)
            {
                waves = gameObject.AddComponent<SurvivalDirector>();
                waves.Setup(content.survival, zombies, kingdom, dayNight);
                rulerController.Health.Died += _ => waves.RulerFell();
            }
            // Zombie bases out in the wild (survival): camps, sieges, and XP when they're destroyed.
            ZombieBases zombieBases = null;
            if (survival && zombies != null)
            {
                zombieBases = new GameObject("Zombie bases").AddComponent<ZombieBases>();
                zombieBases.transform.SetParent(transform, false);
                zombieBases.Setup(content.survival, zombies, kingdom, dayNight, waves);
            }

            var hud = gameObject.AddComponent<GameHud>();
            hud.Setup(rulerController, dayNight, kingdom, builder, rig, waves);
            var saves = gameObject.AddComponent<SaveSystem>();
            saves.Hosted = hosting;
            saves.Bases = zombieBases;
            saves.Setup(content, Mode, seed, kingdom, rulerController, research, dayNight, waves, zombies, world);
            if (saved != null) saves.Restore(saved);
            // Continuing failed: this new game mustn't save over the save on its own.
            else if (loadProblem != null) saves.KeepOldSave(loadProblem + ": started a new game instead");
            parts.Streamer.ShowNow();
            rulerController.ShowWeapon();

            NetHost host = null;
            if (hosting)
            {
                var remotes = new GameObject("Other players").AddComponent<RemotePlayers>();
                remotes.transform.SetParent(transform, false);
                remotes.Setup(content.player);
                var chat = gameObject.AddComponent<ChatBox>();
                chat.Ruler = rulerController;
                host = gameObject.AddComponent<NetHost>();
                host.Setup(Settings(network, seed), kingdom, rulerController, remotes, chat, parts.Nature, dayNight, saved != null ? saved.others : null);
                saves.Host = host;
            }
            gameObject.AddComponent<PauseMenu>().Setup(builder, waves, saves, host, null);
            AddDebug();
        }

        // Playing in someone else's game: connect first; the world is made once the host says what it is.
        void StartJoining(PixelWorld world)
        {
            Application.runInBackground = true;
            var client = gameObject.AddComponent<NetClient>();
            client.WorldReady = welcome => EnterJoinedWorld(world, client, welcome);
            client.Connect(GameSession.JoinHost, GameSession.JoinPort, content.network != null ? content.network.sendRate : 15f);
        }

        void EnterJoinedWorld(PixelWorld world, NetClient client, WelcomeInfo welcome)
        {
            var parts = WorldGenerator.Generate(content, transform, welcome.Seed, true);
            new GameObject("Remains").AddComponent<Remains>().transform.SetParent(transform, false);
            // Every kingdom's ground is cleared the same way the host cleared it.
            foreach (var k in welcome.Kingdoms) KingdomGround.Clear(parts.Nature, k);

            // Your kingdom (just its throne and storehouse for now: building online comes next), others as markers.
            var mine = welcome.Kingdoms.Find(k => k.Id == welcome.YourKingdom);
            var throneCell = mine != null ? new Vector2Int(mine.X, mine.Y) : Vector2Int.zero;
            var kingdom = MakeKingdom(world, null, true, LandsSize(), throneCell, false);
            // Back where you left off last time on this server, or by your throne the first time.
            var start = kingdom.Throne != null ? kingdom.Throne.Front : Vector2.zero;
            if (welcome.HasSpawn && NatureMap.Inside(new Vector2Int(Mathf.FloorToInt(welcome.SpawnX), Mathf.FloorToInt(welcome.SpawnY)))) start = new Vector2(welcome.SpawnX, welcome.SpawnY);
            var ruler = SpawnRuler(start);
            var rulerController = ruler.GetComponent<RulerController>();
            rulerController.Visiting = true;
            var rig = Follow(parts, kingdom, ruler);

            var remotes = new GameObject("Other players").AddComponent<RemotePlayers>();
            remotes.transform.SetParent(transform, false);
            remotes.Setup(content.player);
            foreach (var k in welcome.Kingdoms) if (k.Id != welcome.YourKingdom) remotes.AddKingdom(k);
            var chat = gameObject.AddComponent<ChatBox>();
            chat.Ruler = rulerController;

            var hud = gameObject.AddComponent<GameHud>();
            hud.Setup(rulerController, dayNight, kingdom, null, rig, null);
            parts.Streamer.ShowNow();
            client.Enter(welcome, rulerController, remotes, chat, dayNight, parts.Nature);
            gameObject.AddComponent<PauseMenu>().Setup(null, null, null, null, client);
            AddDebug();
        }

        // A dedicated server: the world for everyone, no player and nothing to draw (started with -server).
        void StartDedicated()
        {
            Application.runInBackground = true;
            var network = content.network;
            Application.targetFrameRate = network != null ? network.serverFrameRate : 30;
            QualitySettings.vSyncCount = 0;
            // The world this server had last time (its seed, kingdoms and players), unless told to start a new one.
            string worldName = CommandLine.Value("-world");
            string problem = null;
            var memory = CommandLine.Has("-newworld") ? null : ServerWorldFile.Read(out problem, worldName);
            if (memory == null && problem != null) Debug.LogWarning("[Server] " + problem);
            int seed = memory != null ? memory.seed : network != null && network.seed != 0 ? network.seed : Random.Range(1, int.MaxValue);
            var parts = WorldGenerator.Generate(content, transform, seed, false);
            if (memory != null && dayNight != null) dayNight.SetTime(memory.day, memory.hour);
            var settings = Settings(network, seed);
            settings.Port = CommandLine.Int("-port", settings.Port);
            settings.MaxPlayers = CommandLine.Int("-maxplayers", settings.MaxPlayers);
            settings.Name = CommandLine.Value("-servername") ?? settings.Name;
            gameObject.AddComponent<NetServer>().Setup(settings, parts.Nature, dayNight, memory, worldName);
        }

        static ServerSettings Settings(NetworkConfig network, int seed)
        {
            var s = new ServerSettings { Seed = seed, WorldLimit = NatureMap.Limit };
            if (network == null) return s;
            s.Port = network.port;
            s.MaxPlayers = network.maxPlayers;
            s.Name = string.IsNullOrWhiteSpace(network.serverName) ? s.Name : network.serverName;
            s.KingdomSpacing = network.kingdomSpacing;
            s.InterestRadius = network.interestRadius;
            s.SnapshotRate = network.snapshotRate;
            return s;
        }

        // The ruler's surroundings: trees near them, the leaves that fade, the camera that follows them.
        CameraRig Follow(WorldParts parts, Kingdom kingdom, GameObject ruler)
        {
            kingdom.Ruler = ruler.transform;
            kingdom.RulerRadius = content.player != null ? content.player.bodyRadius : 0.15f;
            parts.Canopies.Focus = ruler.transform;
            kingdom.Canopies = parts.Canopies;
            if (content.view != null) parts.Canopies.NearAlpha = content.view.canopyFadeAlpha;
            // Trees, rocks and bushes are real objects round the ruler, the camera's view and the kingdom.
            parts.Streamer.Followed.Add(ruler.transform);
            parts.Streamer.View = Camera.main;
            parts.Streamer.Areas.Add(kingdom.ActiveAreas);
            kingdom.Streamer = parts.Streamer;

            CameraRig rig = null;
            var view = Camera.main;
            if (view != null)
            {
                rig = view.GetComponent<CameraRig>();
                if (rig == null) rig = view.gameObject.AddComponent<CameraRig>();
                rig.Setup(content.view);
                rig.Follow(ruler.transform);
                const int half = NatureMap.Limit;
                rig.Bounds = new Rect(-half, -half, half * 2, half * 2);
                if (content.view != null) view.backgroundColor = content.view.backgroundColor;
            }
            return rig;
        }

        void AddDebug()
        {
            var debug = gameObject.AddComponent<DebugHud>();
            debug.visible = false;
            debug.toggleKey = KeyCode.F3;
        }

        int LandsSize() => content.kingdom != null ? content.kingdom.landsSize : 512;

        // `fresh`: a new game (a loaded one puts back its own storehouses and villagers). `lands`: tiles across the
        // kingdom's lands, a square round the throne. `throneCell`: where the throne goes (null: the Kingdom Config's),
        // the storehouse next to it the same way. `villagers`: false makes none.
        Kingdom MakeKingdom(PixelWorld world, SurvivalConfig survival, bool fresh, int lands, Vector2Int? throneCell, bool villagers)
        {
            var go = new GameObject("Kingdom");
            go.transform.SetParent(transform, false);
            var kingdom = go.AddComponent<Kingdom>();
            var storehouse = content.storehouse;
            if (storehouse == null && content.buildMenu != null)
                foreach (var b in content.buildMenu)
                    if (b != null && b.kind == BuildingKind.Storage) { storehouse = b; break; }
            var at = throneCell ?? (content.kingdom != null ? content.kingdom.throneCell : new Vector2Int(-1, 2));
            lands = Mathf.Clamp(lands, 128, 2048);
            var landsCells = new RectInt(at.x - lands / 2, at.y - lands / 2, lands, lands);
            kingdom.Setup(content.kingdom, content.villager, content.buildMenu, world, landsCells, storehouse);

            var config = kingdom.Config;
            var throne = kingdom.PlaceThrone(at);
            if (!fresh) return kingdom;
            if (storehouse != null)
            {
                var storage = kingdom.AddStorage(storehouse, at + (config.storageCell - config.throneCell));
                if (!villagers) return kingdom;
                storage.Deposit(ResourceType.Wood, survival != null ? survival.startWood : config.startWood);
                storage.Deposit(ResourceType.Stone, survival != null ? survival.startStone : config.startStone);
                storage.Deposit(ResourceType.Food, survival != null ? survival.startFood : config.startFood);
            }
            else
            {
                Debug.LogWarning("No storehouse building in the Game Content, so the kingdom starts without storage. Run KingdomGame > Build Game Scenes again.", this);
            }

            // Villagers start in a row in front of the throne.
            for (int i = 0; villagers && i < config.startVillagers; i++)
            {
                float x = (i - (config.startVillagers - 1) * 0.5f) * 0.8f;
                var spot = throne.Front + new Vector2(x, -1.1f);
                if (!kingdom.Nav.Walkable(spot)) kingdom.Nav.NearestFreeSpot(spot, new RectInt(0, 0, 0, 0), out spot);
                // Two builders to start with, the rest cutting wood (V changes it).
                kingdom.AddVillager(spot, i < 2 ? Villagers.Job.Builder : Villagers.Job.Woodcutter);
            }
            return kingdom;
        }

        void OnDestroy()
        {
            ArtSprites.Release();
            Economy.NatureMap.Current = null;
        }

        // A small walled yard east of the start: stone on two sides, wood on one,
        // and a stone tower, for trying bullets, rockets and C4. Torches in the corners.
        void BuildOutpost(PixelWorld world, bool walls)
        {
            if (walls)
            {
                Wall(world, "Outpost north wall", content.stone, 5, 4, 9, 1);
                Wall(world, "Outpost south wall", content.stone, 5, -4, 9, 1);
                Wall(world, "Outpost east palisade", content.wood, 13, -3, 1, 7);
                Wall(world, "Outpost tower", content.stone, 8, -1, 2, 2);
            }
            var fire = ArtSprites.Get(Art.ArtKeys.Brazier);
            foreach (var spot in new[] { new Vector2(5.5f, 3.5f), new Vector2(12.5f, 3.5f), new Vector2(5.5f, -3f), new Vector2(12.5f, -3f) })
            {
                var torch = new GameObject("Torch");
                torch.transform.SetParent(transform, false);
                torch.transform.localPosition = spot;
                torch.AddComponent<Building.Brazier>().Setup(fire, 0.75f, 0.9f);
            }
        }

        void Wall(PixelWorld world, string wallName, StructureMaterial material, int x, int y, int width, int height)
        {
            if (material == null) return;
            var go = new GameObject(wallName);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(x, y, 0);
            go.AddComponent<DestructibleStructure>().Build(world, material, new Vector2Int(width, height));
        }

        GameObject SpawnRuler(Vector2 at)
        {
            var shots = new GameObject("Projectiles").AddComponent<Projectiles>();
            var charges = new GameObject("Explosives").AddComponent<Explosives>();

            var ruler = new GameObject("Ruler");
            ruler.transform.position = at;

            var body = ruler.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Seen from above, the ruler's position is the middle of their body.
            var feet = ruler.AddComponent<CircleCollider2D>();
            feet.radius = content.player != null ? content.player.bodyRadius : 0.15f;

            var look = ruler.AddComponent<RulerLook>();
            var gun = look.Build();

            // A warm light that comes on at night.
            var torch = new GameObject("Night light").AddComponent<Light2D>();
            torch.transform.SetParent(ruler.transform, false);
            torch.lightType = Light2D.LightType.Point;
            if (dayNight != null) dayNight.AddNightLight(torch);
            else torch.enabled = false;

            ruler.AddComponent<RulerController>().Setup(
                content.player, content.rifle, content.rocketLauncher, content.c4, gun, shots, charges);
            return ruler;
        }
    }
}
