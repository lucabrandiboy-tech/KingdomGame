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
            var unlocking = !survival ? Research.Unlocking.Everything
                : content.progression != null && content.progression.survivalAgesByWaves ? Research.Unlocking.ByAge : Research.Unlocking.ByResearch;
            var (research, builder) = KingdomTools(kingdom, rulerController, rig, parts, unlocking);

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

            // Survivors who come to the throne asking to join (after each wave, or each morning in free build).
            if (content.survival != null)
            {
                var gate = new GameObject("Survivors").AddComponent<Villagers.Survivors>();
                gate.transform.SetParent(transform, false);
                gate.Setup(content.survival, kingdom, dayNight, survival);
            }
            AddSeasons(seed);
            var hud = gameObject.AddComponent<GameHud>();
            hud.Setup(rulerController, dayNight, kingdom, builder, rig, waves);
            gameObject.AddComponent<Audio.Ambience>().Setup(dayNight);   // wind, birds by day, crickets at night
            // Ruins out in the wild, with things to search and zombies guarding them.
            var ruins = new GameObject("Ruins").AddComponent<Ruins>();
            ruins.transform.SetParent(transform, false);
            ruins.Setup(seed, kingdom, zombies, waves);
            // Each age's goals, with rewards.
            var goals = gameObject.AddComponent<Progression.Goals>();
            goals.Setup(content.progression, kingdom, research, dayNight, zombies, waves);
            var saves = gameObject.AddComponent<SaveSystem>();
            saves.Hosted = hosting;
            saves.Bases = zombieBases;
            saves.Ruins = ruins;
            saves.Goals = goals;
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
                // The others' kingdoms shown here, and yours sent to them.
                var (others, mirror) = KingdomSharing(world, kingdom, ruler.transform);
                host = gameObject.AddComponent<NetHost>();
                host.Setup(Settings(network, seed), kingdom, rulerController, remotes, chat, parts.Nature, dayNight, saved != null ? saved.others : null,
                    others, mirror, survival);
                saves.Host = host;
            }
            gameObject.AddComponent<PauseMenu>().Setup(builder, waves, saves, host, null);
            AddDebug();
            // The cheat menu (F10), for testing: not in online games, and only while the Progression Config allows cheats.
            if (!hosting && (content.progression == null || content.progression.allowCheats))
                gameObject.AddComponent<CheatMenu>().Setup(kingdom, rulerController, research, dayNight, waves, zombies,
                    content.survival != null ? content.survival.zombies : null);
        }

        // What a ruler needs to run their own kingdom: the ages and research, the panels (villagers, armory,
        // storage) and building from the throne. The same alone, hosting, or in someone else's game.
        (Research Research, BuildController Builder) KingdomTools(Kingdom kingdom, RulerController rulerController, CameraRig rig, WorldParts parts,
            Research.Unlocking unlocking)
        {
            var research = gameObject.AddComponent<Research>();
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
            return (research, builder);
        }

        // Online: the other players' kingdoms as shown here, and the one that sends yours to them.
        (RemoteKingdoms Others, KingdomMirror Mirror) KingdomSharing(PixelWorld world, Kingdom kingdom, Transform ruler)
        {
            var others = new GameObject("Other kingdoms").AddComponent<RemoteKingdoms>();
            others.transform.SetParent(transform, false);
            others.Setup(content, kingdom, world, ruler);
            var mirror = kingdom.gameObject.AddComponent<KingdomMirror>();
            mirror.Setup(kingdom, world);
            return (others, mirror);
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

            // Your own kingdom, run on this computer. The first time on this server it starts like a new game;
            // after that the host hands back what you had (just after this), so it starts with only its throne.
            var mine = welcome.Kingdoms.Find(k => k.Id == welcome.YourKingdom);
            var throneCell = mine != null ? new Vector2Int(mine.X, mine.Y) : Vector2Int.zero;
            var survival = welcome.Survival ? content.survival : null;
            var kingdom = MakeKingdom(world, survival, false, LandsSize(), throneCell, true);
            if (!welcome.HasSave) StartKingdom(kingdom, survival);
            // Back where you left off last time on this server, or by your throne the first time.
            var start = kingdom.Throne != null ? kingdom.Throne.Front : Vector2.zero;
            if (welcome.HasSpawn && NatureMap.Inside(new Vector2Int(Mathf.FloorToInt(welcome.SpawnX), Mathf.FloorToInt(welcome.SpawnY)))) start = new Vector2(welcome.SpawnX, welcome.SpawnY);
            var ruler = SpawnRuler(start);
            var rulerController = ruler.GetComponent<RulerController>();
            var rig = Follow(parts, kingdom, ruler);

            // The ages as the host's game has them: researched in survival, all open in free build.
            // (No zombies here yet: they come with a later online stage.)
            var unlocking = welcome.Survival ? Research.Unlocking.ByResearch : Research.Unlocking.Everything;
            var (research, builder) = KingdomTools(kingdom, rulerController, rig, parts, unlocking);

            var remotes = new GameObject("Other players").AddComponent<RemotePlayers>();
            remotes.transform.SetParent(transform, false);
            remotes.Setup(content.player);
            foreach (var k in welcome.Kingdoms) if (k.Id != welcome.YourKingdom) remotes.AddKingdom(k);
            var chat = gameObject.AddComponent<ChatBox>();
            chat.Ruler = rulerController;
            var (others, mirror) = KingdomSharing(world, kingdom, ruler.transform);

            AddSeasons(welcome.Seed);   // (the same seasons and weather as the host: from the seed and the host's clock)
            var hud = gameObject.AddComponent<GameHud>();
            hud.Setup(rulerController, dayNight, kingdom, builder, rig, null);
            gameObject.AddComponent<Audio.Ambience>().Setup(dayNight);
            var goals = gameObject.AddComponent<Progression.Goals>();
            goals.Setup(content.progression, kingdom, research, dayNight, null, null);
            // Saving sends your kingdom to the host (it keeps it for you), not to a file here.
            var saves = gameObject.AddComponent<SaveSystem>();
            saves.Goals = goals;
            saves.Setup(content, Mode, welcome.Seed, kingdom, rulerController, research, dayNight, null, null, world);
            parts.Streamer.ShowNow();
            rulerController.ShowWeapon();
            // If the host's copy of your kingdom can't be read: start a new one (it isn't saved over unless you save).
            client.SaveUnreadable = () => StartKingdom(kingdom, survival);
            client.Enter(welcome, rulerController, remotes, chat, dayNight, parts.Nature, others, mirror, saves);
            gameObject.AddComponent<PauseMenu>().Setup(builder, null, saves, null, client);
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

        // The seasons and the weather (from the world's seed and the clock), and fire (it burns this kingdom).
        void AddSeasons(int seed)
        {
            var seasons = new GameObject("Seasons and weather").AddComponent<Seasons>();
            seasons.transform.SetParent(transform, false);
            seasons.Setup(content.seasons, dayNight, seed);
            var fires = new GameObject("Fires").AddComponent<Fires>();
            fires.transform.SetParent(transform, false);
            fires.Setup(content.fire, Kingdom.Current, PixelWorld.Current);
            // Deer and wolves round the ruler (each computer its own).
            if (content.wildlife != null)
            {
                var wild = new GameObject("Wildlife").AddComponent<Wildlife>();
                wild.transform.SetParent(transform, false);
                wild.Setup(content.wildlife, Kingdom.Current, dayNight);
            }
        }

        void AddDebug()
        {
            var debug = gameObject.AddComponent<DebugHud>();
            debug.visible = false;
            debug.toggleKey = KeyCode.F3;
        }

        int LandsSize() => content.kingdom != null ? content.kingdom.landsSize : 512;

        // `fresh`: a new game, so it gets its storehouse and villagers (a loaded one puts back its own). `lands`: tiles
        // across the kingdom's lands, a square round the throne. `throneCell`: where the throne goes (null: the Kingdom
        // Config's), the storehouse next to it the same way. `villagers`: false makes none.
        Kingdom MakeKingdom(PixelWorld world, SurvivalConfig survival, bool fresh, int lands, Vector2Int? throneCell, bool villagers)
        {
            var go = new GameObject("Kingdom");
            go.transform.SetParent(transform, false);
            var kingdom = go.AddComponent<Kingdom>();
            var at = throneCell ?? (content.kingdom != null ? content.kingdom.throneCell : new Vector2Int(-1, 2));
            lands = Mathf.Clamp(lands, 128, 2048);
            var landsCells = new RectInt(at.x - lands / 2, at.y - lands / 2, lands, lands);
            kingdom.Setup(content.kingdom, content.villager, content.buildMenu, world, landsCells, Storehouse());
            kingdom.Clock = dayNight;
            kingdom.PlaceThrone(at);
            if (fresh) StartKingdom(kingdom, survival, villagers);
            return kingdom;
        }

        BuildingConfig Storehouse()
        {
            if (content.storehouse != null) return content.storehouse;
            if (content.buildMenu != null)
                foreach (var b in content.buildMenu)
                    if (b != null && b.kind == BuildingKind.Storage) return b;
            return null;
        }

        // A new kingdom's start: the storehouse next to the throne with the starting wood, stone and food, and the
        // first villagers (unless `villagers` is false).
        void StartKingdom(Kingdom kingdom, SurvivalConfig survival, bool villagers = true)
        {
            var config = kingdom.Config;
            var throne = kingdom.Throne;
            if (config == null || throne == null) return;
            var at = throne.Cells.position;
            var storehouse = Storehouse();
            if (storehouse != null)
            {
                var storage = kingdom.AddStorage(storehouse, at + (config.storageCell - config.throneCell));
                if (!villagers) return;
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
                // Two builders to start with, then one picking berries (they eat), the rest cutting wood (V changes it).
                kingdom.AddVillager(spot, i < 2 ? Villagers.Job.Builder : i == 2 ? Villagers.Job.Forager : Villagers.Job.Woodcutter);
            }
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
