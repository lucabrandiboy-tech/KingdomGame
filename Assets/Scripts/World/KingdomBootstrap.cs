using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.Economy;
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
            bool survival = Mode == GameMode.Survival;
            if (survival && content.survival == null)
            {
                Debug.LogWarning("The Game Content has no Survival Config, so this game has no zombies. Run KingdomGame > Build Game Scenes again.", this);
                survival = false;
            }

            int seed = content.world.seed != 0 ? content.world.seed : Random.Range(1, int.MaxValue);
            Debug.Log($"Map seed {seed}. Put this number in WorldConfig's Seed to get the same map again.");
            var canopies = WorldGenerator.Generate(content, transform, seed);
            // The test outpost is for trying weapons on walls; survival starts on open ground.
            if (content.world.testOutpost && !survival) BuildOutpost(world);
            new GameObject("Remains").AddComponent<Remains>().transform.SetParent(transform, false);

            var kingdom = MakeKingdom(world, survival ? content.survival : null);
            var ruler = SpawnRuler(kingdom.Throne != null ? kingdom.Throne.Front : Vector2.zero);
            var rulerController = ruler.GetComponent<RulerController>();
            kingdom.Ruler = ruler.transform;
            kingdom.RulerRadius = content.player != null ? content.player.bodyRadius : 0.15f;
            canopies.Focus = ruler.transform;
            if (content.view != null) canopies.NearAlpha = content.view.canopyFadeAlpha;

            CameraRig rig = null;
            var view = Camera.main;
            if (view != null)
            {
                rig = view.GetComponent<CameraRig>();
                if (rig == null) rig = view.gameObject.AddComponent<CameraRig>();
                rig.Setup(content.view);
                rig.Follow(ruler.transform);
                int half = content.world.mapSize / 2;
                rig.Bounds = new Rect(-half, -half, half * 2, half * 2);
                if (content.view != null) view.backgroundColor = content.view.backgroundColor;
            }

            var builder = kingdom.gameObject.AddComponent<BuildController>();
            builder.Setup(kingdom, rulerController, rig, canopies, content.view);

            // Survival: the zombies, and the waves they come in. Falling ends the game.
            SurvivalDirector waves = null;
            rulerController.RespawnOnDeath = !survival;
            if (survival)
            {
                var zombies = new GameObject("Zombies").AddComponent<ZombieDirector>();
                zombies.transform.SetParent(transform, false);
                zombies.Setup(content.survival, world, kingdom, rulerController, dayNight, kingdom.Nav.Cells);
                waves = gameObject.AddComponent<SurvivalDirector>();
                waves.Setup(content.survival, zombies, kingdom);
                rulerController.Health.Died += _ => waves.RulerFell();
            }

            var hud = gameObject.AddComponent<GameHud>();
            hud.Setup(rulerController, dayNight, kingdom, builder, rig, waves);
            gameObject.AddComponent<PauseMenu>().Setup(builder, waves);
            var debug = gameObject.AddComponent<DebugHud>();
            debug.visible = false;
            debug.toggleKey = KeyCode.F3;
        }

        Kingdom MakeKingdom(PixelWorld world, SurvivalConfig survival)
        {
            var go = new GameObject("Kingdom");
            go.transform.SetParent(transform, false);
            var kingdom = go.AddComponent<Kingdom>();
            int size = content.world.mapSize, half = size / 2;
            var storehouse = content.storehouse;
            if (storehouse == null && content.buildMenu != null)
                foreach (var b in content.buildMenu)
                    if (b != null && b.kind == BuildingKind.Storage) { storehouse = b; break; }
            kingdom.Setup(content.kingdom, content.villager, content.buildMenu, world, new RectInt(-half, -half, size, size), storehouse);

            var config = kingdom.Config;
            var throne = kingdom.PlaceThrone(config.throneCell);
            if (storehouse != null)
            {
                var storage = kingdom.AddStorage(storehouse, config.storageCell);
                storage.Deposit(ResourceType.Wood, survival != null ? survival.startWood : config.startWood);
                storage.Deposit(ResourceType.Stone, survival != null ? survival.startStone : config.startStone);
                storage.Deposit(ResourceType.Food, survival != null ? survival.startFood : config.startFood);
            }
            else
            {
                Debug.LogWarning("No storehouse building in the Game Content, so the kingdom starts without storage. Run KingdomGame > Build Game Scenes again.", this);
            }

            // Villagers start in a row in front of the throne.
            for (int i = 0; i < config.startVillagers; i++)
            {
                float x = (i - (config.startVillagers - 1) * 0.5f) * 0.8f;
                var spot = throne.Front + new Vector2(x, -1.1f);
                if (!kingdom.Nav.Walkable(spot)) kingdom.Nav.NearestFreeSpot(spot, new RectInt(0, 0, 0, 0), out spot);
                kingdom.AddVillager(spot);
            }
            return kingdom;
        }

        void OnDestroy() => ArtSprites.Release();

        // A small walled yard east of the start: stone on two sides, wood on one,
        // and a stone tower, for trying bullets, rockets and C4. Torches in the corners.
        void BuildOutpost(PixelWorld world)
        {
            Wall(world, "Outpost north wall", content.stone, 5, 4, 9, 1);
            Wall(world, "Outpost south wall", content.stone, 5, -4, 9, 1);
            Wall(world, "Outpost east palisade", content.wood, 13, -3, 1, 7);
            Wall(world, "Outpost tower", content.stone, 8, -1, 2, 2);
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
