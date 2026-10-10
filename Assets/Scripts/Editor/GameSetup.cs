using System;
using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.UI;
using KingdomGame.Villagers;
using KingdomGame.World;
using KingdomGame.Zombies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace KingdomGame.EditorTools
{
    /// <summary>
    /// Menu: KingdomGame > Build Game Scenes.
    /// Makes the settings assets in Assets/Data (kept if they already exist, so
    /// your tuning survives; settings added later are filled in) and rebuilds
    /// three scenes: MainMenu (first in the build), Kingdom (the game) and
    /// StressTest. Also bakes the art (<see cref="ArtBaker"/>, only when the art
    /// changed) and removes the old Phase 1 test scenes and assets.
    /// </summary>
    public static class GameSetup
    {
        const string DataRoot = "Assets/Data";

        /// <summary>
        /// Bumped when settings made by an older version of this menu should get
        /// new values (it normally keeps them, so your tuning survives).
        /// 1: the high-detail look (evening start, fires, softer night, wall colors, color grading).
        /// 2: the castle and survival update (furniture on the build bar, zombie numbers for walls and weapons).
        /// 3: houses (a house you go into, bigger inside, upgraded age by age).
        /// </summary>
        const int SettingsVersion = 3;

        public const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        public const string KingdomScenePath = "Assets/Scenes/Kingdom.unity";
        public const string StressScenePath = "Assets/Scenes/StressTest.unity";

        // Made by the Phase 1 menu; replaced by the assets and scenes below.
        static readonly string[] Obsolete =
        {
            "Assets/Data/Phase1",
            "Assets/Scenes/Phase1_Destruction.unity",
            "Assets/Scenes/Phase1_StressTest.unity",
        };


        [MenuItem("KingdomGame/Build Game Scenes")]
        static void BuildGameScenes()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play mode first, then run KingdomGame > Build Game Scenes again.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Folder("Assets", "Data");
            foreach (var area in new[] { "Destruction", "Combat", "Character", "Economy", "World", "Building", "Villagers", "Look", "Zombies" })
                Folder(DataRoot, area);
            foreach (var area in new[] { "Doors", "Floors", "Furniture", "Lights", "Storage", "Walls", "Houses" })
                Folder(DataRoot + "/Building", area);
            Folder("Assets", "Scenes");
            Folder("Assets", "Art");

            // Settings made by an older version of this menu get the new look
            // (only how things look: gameplay numbers are kept).
            var oldContent = AssetDatabase.LoadAssetAtPath<GameContent>(DataRoot + "/GameContent.asset");
            bool newLook = oldContent != null && oldContent.settingsVersion < 1;
            // Settings from before the castle and survival update get its new numbers (and keep the rest).
            bool castleUpdate = oldContent != null && oldContent.settingsVersion < 2;
            // ...and from before houses.
            bool housesUpdate = oldContent != null && oldContent.settingsVersion < 3;

            // ---- Destruction: 64 pixels = 1 tile. Stone shrugs off bullets (Rust-style).
            var settings = Asset<DestructionSettings>("Destruction/DestructionSettings", s => { });
            var bullet = Asset<DamageType>("Destruction/Bullet", d =>
            {
                d.category = DamageCategory.Bullet;
                d.radiusPixels = 4; d.centerDamage = 160; d.edgeDamage = 40; d.jaggedness = 120; d.noiseBlock = 1;
                d.debrisChance = 0.5f; d.debrisSpeed = new Vector2(1f, 3f); d.debrisLift = new Vector2(1f, 3f);
                d.sparks = 4; d.sparkColor = new Color32(255, 230, 140, 255); d.screenShake = 0.01f;
            });
            var rocketBlast = Asset<DamageType>("Destruction/RocketExplosion", d =>
            {
                d.category = DamageCategory.Explosive;
                d.radiusPixels = 40; d.centerDamage = 360; d.edgeDamage = 100; d.jaggedness = 110; d.noiseBlock = 3;
                d.debrisChance = 0.12f; d.debrisSpeed = new Vector2(3f, 9f); d.debrisLift = new Vector2(2f, 7f);
                d.sparks = 80; d.sparkColor = new Color32(255, 150, 60, 255); d.screenShake = 0.25f;
                RocketBlast(d);
            });
            var c4Blast = Asset<DamageType>("Destruction/C4Explosion", d =>
            {
                d.category = DamageCategory.Explosive;
                d.radiusPixels = 64; d.centerDamage = 900; d.edgeDamage = 160; d.jaggedness = 100; d.noiseBlock = 4;
                d.debrisChance = 0.08f; d.debrisSpeed = new Vector2(3f, 10f); d.debrisLift = new Vector2(2f, 8f);
                d.sparks = 120; d.sparkColor = new Color32(255, 170, 70, 255); d.screenShake = 0.45f;
                C4Blast(d);
            });
            var smash = Asset<DamageType>("Destruction/BruteSmash", d =>
            {
                d.category = DamageCategory.Melee;
                d.radiusPixels = 20; d.centerDamage = 400; d.edgeDamage = 150; d.jaggedness = 170; d.noiseBlock = 6;
                d.debrisChance = 0.25f; d.debrisSpeed = new Vector2(1.5f, 5f); d.debrisLift = new Vector2(1.5f, 5f);
                d.screenShake = 0.12f;
            });
            // A zombie's claws on a wall: wood splinters, stone only cracks (the wall wears down by the zombie's Wall Damage).
            var claw = Asset<DamageType>("Destruction/ZombieClaw", d =>
            {
                d.category = DamageCategory.Melee;
                d.radiusPixels = 6; d.centerDamage = 120; d.edgeDamage = 30; d.jaggedness = 170; d.noiseBlock = 2;
                d.debrisChance = 0.5f; d.debrisSpeed = new Vector2(0.8f, 2.5f); d.debrisLift = new Vector2(1f, 2.5f);
                d.sparks = 3; d.sparkColor = new Color32(140, 128, 110, 255); d.screenShake = 0f;
            });

            var stone = Asset<StructureMaterial>("Destruction/StoneWall", m =>
            {
                m.pixelHealth = 255; m.bulletPercent = 3; m.meleePercent = 8; m.explosivePercent = 100; m.siegeHealth = 600f;
                StoneLook(m);
            });
            var wood = Asset<StructureMaterial>("Destruction/WoodPalisade", m =>
            {
                m.pixelHealth = 140; m.bulletPercent = 8; m.meleePercent = 35; m.explosivePercent = 140; m.siegeHealth = 200f;
                WoodLook(m);
            });

            // ---- Combat
            var rifle = Asset<WeaponConfig>("Combat/Rifle", w =>
            {
                w.damage = bullet; w.fireRate = 8f; w.automatic = true; w.projectileSpeed = 30f; w.range = 14f;
                w.spreadDegrees = 2f; w.projectileLength = 8; w.projectileThickness = 2;
                w.projectileColor = new Color32(255, 214, 102, 255); w.recoilShake = 0.008f;
                w.hitDamage = 25f; w.overWalls = 1.6f;
            });
            var rockets = Asset<WeaponConfig>("Combat/RocketLauncher", w =>
            {
                w.damage = rocketBlast; w.fireRate = 0.8f; w.automatic = false; w.projectileSpeed = 12f; w.range = 14f;
                w.spreadDegrees = 0.5f; w.explodeAtEndOfRange = true; w.projectileLength = 14; w.projectileThickness = 4;
                w.projectileColor = new Color32(232, 96, 52, 255); w.recoilShake = 0.06f;
                w.hitDamage = 80f; w.overWalls = 0f;
            });
            var c4 = Asset<ExplosiveConfig>("Combat/C4", e =>
            {
                e.damage = c4Blast; e.fuseSeconds = 4f; e.throwRange = 4f; e.throwSpeed = 9f; e.cooldown = 1.5f;
            });

            // ---- Character and economy
            var player = Asset<PlayerConfig>("Character/PlayerConfig", p => { });
            var tree = Asset<ResourceNodeConfig>("Economy/Tree", n =>
            {
                n.look = NodeLook.Tree; n.resource = ResourceType.Wood; n.amount = 60; n.perHit = 5; n.secondsPerHit = 0.5f;
                n.regrowSeconds = 0f; n.blockRadius = 0.18f; n.reach = 0.6f; n.chipColor = new Color32(150, 108, 66, 255);
            });
            var rock = Asset<ResourceNodeConfig>("Economy/Rock", n =>
            {
                n.look = NodeLook.Rock; n.resource = ResourceType.Stone; n.amount = 80; n.perHit = 4; n.secondsPerHit = 0.6f;
                n.regrowSeconds = 0f; n.blockRadius = 0.36f; n.reach = 0.5f; n.chipColor = new Color32(150, 148, 150, 255);
            });
            var bush = Asset<ResourceNodeConfig>("Economy/BerryBush", n =>
            {
                n.look = NodeLook.BerryBush; n.resource = ResourceType.Food; n.amount = 20; n.perHit = 4; n.secondsPerHit = 0.4f;
                n.regrowSeconds = 120f; n.blockRadius = 0.25f; n.reach = 0.5f; n.chipColor = new Color32(210, 50, 70, 255);
            });

            // ---- World
            var worldConfig = Asset<WorldConfig>("World/WorldConfig", w => { });
            var dayNight = Asset<DayNightConfig>("World/DayNightConfig", d => { });
            if (newLook)
            {
                StoneLook(stone);
                WoodLook(wood);
                NewDayNightLook(dayNight);
                EditorUtility.SetDirty(stone);
                EditorUtility.SetDirty(wood);
                EditorUtility.SetDirty(dayNight);
                Debug.Log("Updated your older settings to the new look (day and night light, fires, wall colors, color grading). Gameplay numbers were kept.");
            }
            var stressConfig = Asset<StressTestConfig>("World/StressTestConfig", c =>
            {
                c.explosion = c4Blast;
                c.materials = new[] { stone, wood };
            });
            // ---- Look: camera, glow and color grading
            var glow = GlowMaterial();
            var view = Asset<ViewConfig>("Look/ViewConfig", v => v.glowMaterial = glow);
            if (view.glowMaterial == null && glow != null)
            {
                view.glowMaterial = glow;
                EditorUtility.SetDirty(view);
            }
            if (newLook && view.walkZoomMin > 2f)
            {
                view.walkZoomMin = 2f;   // zoom in close enough to see the detail
                EditorUtility.SetDirty(view);
            }
            var postProcessing = PostProcessingProfile(newLook);

            // ---- Kingdom: buildings, villagers
            var stoneWallBuilding = Asset<BuildingConfig>("Building/StoneWall", b =>
            {
                b.displayName = "Stone wall"; b.kind = BuildingKind.Wall; b.category = BuildCategory.Walls; b.size = Vector2Int.one;
                b.stoneCost = 10; b.buildSeconds = 4f; b.wallMaterial = stone; b.dragToPlaceLines = true; b.roomTag = RoomTag.None;
            });
            var woodWallBuilding = Asset<BuildingConfig>("Building/WoodWall", b =>
            {
                b.displayName = "Wood wall"; b.kind = BuildingKind.Wall; b.category = BuildCategory.Walls; b.size = Vector2Int.one;
                b.woodCost = 8; b.buildSeconds = 3f; b.wallMaterial = wood; b.dragToPlaceLines = true; b.roomTag = RoomTag.None;
            });
            var storehouse = Asset<BuildingConfig>("Building/Storehouse", b =>
            {
                b.displayName = "Storehouse"; b.kind = BuildingKind.Storage; b.category = BuildCategory.Storage; b.size = new Vector2Int(2, 2);
                b.woodCost = 60; b.stoneCost = 20; b.buildSeconds = 15f; b.storageCapacity = 1000; b.dragToPlaceLines = false;
                b.roomTag = RoomTag.Storage; b.height = 0.3f; b.health = 1200f;
            });
            var castle = CastleBuildings();
            var house = House(stone, wood);
            var kingdomConfig = Asset<KingdomConfig>("Building/KingdomConfig", k => { });
            var villagerConfig = Asset<VillagerConfig>("Villagers/VillagerConfig", v => { });

            // ---- Zombies and survival
            var walker = Asset<ZombieConfig>("Zombies/Walker", z =>
            {
                z.displayName = "Walker"; z.art = "Walker"; z.health = 65f; z.moveSpeed = 1f; z.radius = 0.22f;
                z.damage = 10f; z.attackSeconds = 1.3f; z.reach = 0.25f; z.structureDamage = 10f; z.wallDamage = 10f; z.wallScratch = claw;
                z.firstWave = 1; z.weight = 1f; z.lootChance = 0.25f; z.lootAmount = 8;
            });
            var runner = Asset<ZombieConfig>("Zombies/Runner", z =>
            {
                z.displayName = "Runner"; z.art = "Runner"; z.health = 30f; z.moveSpeed = 2.1f; z.radius = 0.2f;
                z.damage = 7f; z.attackSeconds = 0.9f; z.reach = 0.22f; z.structureDamage = 6f; z.wallDamage = 5f; z.wallScratch = claw;
                z.firstWave = 3; z.weight = 0.6f; z.lootChance = 0.2f; z.lootAmount = 6;
            });
            var brute = Asset<ZombieConfig>("Zombies/Brute", z =>
            {
                z.displayName = "Brute"; z.art = "Brute"; z.health = 280f; z.moveSpeed = 0.8f; z.radius = 0.32f;
                z.damage = 28f; z.attackSeconds = 1.8f; z.reach = 0.3f; z.structureDamage = 45f; z.wallDamage = 60f; z.wallScratch = smash;
                z.firstWave = 5; z.weight = 0.25f; z.lootChance = 0.6f; z.lootAmount = 20;
            });
            var survival = Asset<SurvivalConfig>("Zombies/SurvivalConfig", s => s.zombies = new[] { walker, runner, brute });

            var content = Asset<GameContent>("GameContent", c =>
            {
                c.player = player; c.rifle = rifle; c.rocketLauncher = rockets; c.c4 = c4;
                c.stone = stone; c.wood = wood; c.tree = tree; c.rock = rock; c.bush = bush;
                c.world = worldConfig; c.dayNight = dayNight;
            });
            // A Game Content made by an older version of this menu doesn't know the newer settings yet.
            if (content.view == null) content.view = view;
            if (content.kingdom == null) content.kingdom = kingdomConfig;
            if (content.villager == null) content.villager = villagerConfig;
            if (content.storehouse == null) content.storehouse = storehouse;
            if (content.survival == null) content.survival = survival;
            // Everything on the build bar, in order (each item shows on its category's tab).
            var menu = new List<BuildingConfig> { stoneWallBuilding, woodWallBuilding };
            menu.AddRange(castle);
            int firstStorage = castle.FindIndex(b => b != null && b.category == BuildCategory.Storage);
            menu.Insert(firstStorage >= 0 ? firstStorage + 2 : menu.Count, storehouse);
            menu.Add(house);
            if (content.buildMenu == null || content.buildMenu.Length == 0)
                content.buildMenu = menu.ToArray();
            if (castleUpdate)
            {
                // Settings from before the castle update: the new things join the build bar (after yours),
                // and walls, weapons and explosions get their numbers for zombies.
                var list = new List<BuildingConfig>(content.buildMenu);
                foreach (var b in menu)
                    if (!list.Contains(b)) list.Add(b);
                content.buildMenu = list.ToArray();
                storehouse.category = BuildCategory.Storage;
                storehouse.roomTag = RoomTag.Storage;
                storehouse.health = 1200f;
                stoneWallBuilding.category = woodWallBuilding.category = BuildCategory.Walls;
                stoneWallBuilding.roomTag = woodWallBuilding.roomTag = RoomTag.None;
                stone.siegeHealth = 600f;
                wood.siegeHealth = 200f;
                rifle.hitDamage = 25f;
                rifle.overWalls = 1.6f;
                rockets.hitDamage = 80f;
                rockets.overWalls = 0f;
                RocketBlast(rocketBlast);
                C4Blast(c4Blast);
                foreach (var changed in new UnityEngine.Object[] { storehouse, stoneWallBuilding, woodWallBuilding, stone, wood, rifle, rockets, rocketBlast, c4Blast })
                    EditorUtility.SetDirty(changed);
                Debug.Log("Added the castle furniture to your build bar and set up the zombies (survival). Your other settings were kept.");
            }

            if (housesUpdate && Array.IndexOf(content.buildMenu, house) < 0)
            {
                var list = new List<BuildingConfig>(content.buildMenu) { house };
                content.buildMenu = list.ToArray();
                Debug.Log("Added houses to your build bar (the Houses tab). Your other settings were kept.");
            }

            // The pictures, at full detail (the first time takes a few minutes).
            try
            {
                content.art = ArtBaker.Bake(content, false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Debug.LogError("Baking the art failed (the error is above), so the game draws its pictures in less detail when you press Play. Please send Claude that error.");
            }
            content.settingsVersion = SettingsVersion;
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();

            var damageTypes = new[] { bullet, rocketBlast, c4Blast, smash, claw };

            // Scene 1: the main menu.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var menuCamera = MakeCamera(view);
            menuCamera.backgroundColor = new Color(0.035f, 0.04f, 0.035f);
            new GameObject("Main Menu").AddComponent<MainMenu>();
            EditorSceneManager.SaveScene(scene, MenuScenePath);

            // Scene 2: the game.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var camera = MakeCamera(view);
            var sun = MakeGlobalLight();
            MakeVolume(postProcessing);
            MakeWorld(settings, damageTypes);
            var cycle = new GameObject("Day and Night").AddComponent<DayNightCycle>();
            cycle.Setup(dayNight, sun);
            var bootstrap = new GameObject("Kingdom").AddComponent<KingdomBootstrap>();
            bootstrap.Content = content;
            bootstrap.DayNight = cycle;
            bootstrap.DefaultMode = GameMode.Survival;
            EditorSceneManager.SaveScene(scene, KingdomScenePath);

            // Scene 3: stress test, zoomed out far enough to show every wall.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            camera = MakeCamera(view);
            camera.orthographicSize = 12f;
            MakeGlobalLight();
            MakeVolume(postProcessing);
            MakeWorld(settings, damageTypes);
            new GameObject("Stress Test").AddComponent<StressTest>().Config = stressConfig;
            EditorSceneManager.SaveScene(scene, StressScenePath);

            RemoveObsolete();
            SetBuildScenes(MenuScenePath, KingdomScenePath, StressScenePath);
            EditorSceneManager.OpenScene(KingdomScenePath);
            Debug.Log("Game scenes are ready: Assets/Scenes/Kingdom (open now: Play starts survival), Assets/Scenes/MainMenu " +
                      "(pick survival or free build) and Assets/Scenes/StressTest. Press Play.");
        }

        // Explosions hurt creatures, doors and furniture too (zombies, and you if you're too close).
        static void RocketBlast(DamageType d)
        {
            d.creatureDamage = 120f;
            d.blastRadius = 2.2f;
            d.friendlyShare = 0.35f;
        }

        static void C4Blast(DamageType d)
        {
            d.creatureDamage = 300f;
            d.blastRadius = 3.2f;
            d.friendlyShare = 0.35f;
        }

        // Doors, floors, furniture, lights and storage for building a castle (the art is in FurnitureArt).
        static List<BuildingConfig> CastleBuildings()
        {
            var list = new List<BuildingConfig>
            {
                Door("WoodenDoor", "Wooden door", FurnitureArt.WoodDoor, 20, 0, 5f, 250f),
                Door("HeavyDoor", "Heavy door", FurnitureArt.HeavyDoor, 30, 20, 8f, 700f),
                Climb("Ladder", "Ladder", FurnitureArt.Ladder, 6, 0, 2f, 60f),
                Climb("StoneStairs", "Stone stairs", FurnitureArt.StoneStairs, 0, 20, 6f, 400f),
                Asset<BuildingConfig>("Building/Floors/WoodFloor", b =>
                {
                    b.displayName = "Wood floor"; b.kind = BuildingKind.Floor; b.category = BuildCategory.Floors; b.art = FurnitureArt.WoodFloor;
                    b.woodCost = 2; b.buildSeconds = 1f; b.dragToPlaceLines = true; b.blocksMovement = false; b.health = 0f; b.height = 0f;
                    b.roomTag = RoomTag.None;
                }),
                Asset<BuildingConfig>("Building/Floors/StoneFloor", b =>
                {
                    b.displayName = "Stone floor"; b.kind = BuildingKind.Floor; b.category = BuildCategory.Floors; b.art = FurnitureArt.StoneFloor;
                    b.stoneCost = 3; b.buildSeconds = 1.5f; b.dragToPlaceLines = true; b.blocksMovement = false; b.health = 0f; b.height = 0f;
                    b.roomTag = RoomTag.None;
                }),
                Furnish("Floors/Rug", "Rug", FurnitureArt.Rug, BuildCategory.Floors, new Vector2Int(2, 3), 2, 0, 0, 15, 4f, RoomTag.Decor, false, 0f, 0f, flat: true),

                Furnish("Furniture/Chair", "Chair", FurnitureArt.Chair, BuildCategory.Furniture, Vector2Int.one, 4, 5, 0, 0, 2f, RoomTag.Seat, false, 40f, 0.25f),
                Furnish("Furniture/Table", "Table", FurnitureArt.Table, BuildCategory.Furniture, Vector2Int.one, 1, 10, 0, 0, 3f, RoomTag.Table, true, 80f, 0.3f),
                Furnish("Furniture/LongTable", "Long table", FurnitureArt.LongTable, BuildCategory.Furniture, new Vector2Int(2, 1), 2, 20, 0, 0, 5f, RoomTag.Table, true, 150f, 0.3f),
                Furnish("Furniture/Bench", "Bench", FurnitureArt.Bench, BuildCategory.Furniture, new Vector2Int(2, 1), 2, 8, 0, 0, 3f, RoomTag.Seat, false, 60f, 0.15f),
                Furnish("Furniture/Bed", "Bed", FurnitureArt.Bed, BuildCategory.Furniture, new Vector2Int(1, 2), 4, 15, 0, 10, 6f, RoomTag.Bed, true, 100f, 0.25f, sleeps: 1),
                Furnish("Furniture/Bookshelf", "Bookshelf", FurnitureArt.Bookshelf, BuildCategory.Furniture, new Vector2Int(2, 1), 4, 25, 0, 0, 6f, RoomTag.Books, true, 120f, 0.6f),
                Furnish("Furniture/WeaponRack", "Weapon rack", FurnitureArt.WeaponRack, BuildCategory.Furniture, new Vector2Int(2, 1), 4, 15, 10, 0, 5f, RoomTag.Weapons, true, 120f, 0.45f),
                Furnish("Furniture/ArmorStand", "Armor stand", FurnitureArt.ArmorStand, BuildCategory.Furniture, Vector2Int.one, 4, 5, 15, 0, 4f, RoomTag.Weapons, true, 100f, 0.7f),
                Furnish("Furniture/Cauldron", "Cauldron", FurnitureArt.Cauldron, BuildCategory.Furniture, Vector2Int.one, 1, 0, 15, 0, 4f, RoomTag.Cooking, true, 150f, 0.35f, light: 0.45f),
                Furnish("Furniture/Anvil", "Anvil", FurnitureArt.Anvil, BuildCategory.Furniture, Vector2Int.one, 2, 5, 20, 0, 4f, RoomTag.Workshop, true, 200f, 0.4f),
                Furnish("Furniture/Banner", "Banner", FurnitureArt.Banner, BuildCategory.Furniture, Vector2Int.one, 4, 5, 0, 10, 3f, RoomTag.Decor, false, 30f, 0.9f),
                Furnish("Furniture/Well", "Well", FurnitureArt.Well, BuildCategory.Furniture, new Vector2Int(2, 2), 1, 10, 40, 0, 10f, RoomTag.Decor, true, 400f, 0.5f),

                Furnish("Lights/Torch", "Torch", FurnitureArt.Torch, BuildCategory.Lights, Vector2Int.one, 1, 5, 0, 0, 2f, RoomTag.Decor, false, 30f, 0.55f,
                    light: 0.75f, flames: new[] { Vector2.zero }, flameSize: 1f),
                Furnish("Lights/Candelabra", "Candelabra", FurnitureArt.Candelabra, BuildCategory.Lights, Vector2Int.one, 1, 0, 6, 4, 3f, RoomTag.Decor, false, 30f, 0.4f,
                    light: 0.6f, flames: FurnitureArt.CandelabraFlames, flameSize: 0.45f),
                Furnish("Lights/Brazier", "Brazier", FurnitureArt.Brazier, BuildCategory.Lights, Vector2Int.one, 1, 0, 12, 0, 3f, RoomTag.Decor, true, 120f, 0.45f,
                    light: 1f, flames: new[] { Vector2.zero }, flameSize: 1.8f),
                Furnish("Lights/Fireplace", "Fireplace", FurnitureArt.Fireplace, BuildCategory.Lights, new Vector2Int(2, 1), 4, 0, 40, 0, 8f, RoomTag.Decor, true, 400f, 0.7f,
                    light: 1.2f, flames: new[] { FurnitureArt.FireplaceFlame }, flameSize: 2.4f),

                Store("Chest", "Chest", FurnitureArt.Chest, 4, 20, 4f, 200, 0.3f, 200f),
                Store("Barrel", "Barrel", FurnitureArt.Barrel, 1, 15, 3f, 150, 0.36f, 150f),
                Store("Crates", "Crates", FurnitureArt.Crates, 1, 15, 3f, 250, 0.3f, 180f),
            };
            return list;
        }

        // A house you go into: bigger inside than out, and a level for each age (a hut up to a concrete house).
        static BuildingConfig House(StructureMaterial stone, StructureMaterial wood)
        {
            var levels = Asset<HouseConfig>("Building/Houses/HouseLevels", h =>
            {
                var brick = new Color32(140, 72, 52, 255);
                var grey = new Color32(118, 118, 114, 255);
                var fieldstone = new Color32(112, 108, 100, 255);
                h.levels = new[]
                {
                    Level("Hut", 0, 5, 4, 600f, RoofStyle.Thatch, wood, FurnitureArt.WoodFloor, FurnitureArt.WoodDoor, fieldstone, 0, 0, 0, 0f),
                    Level("Mudbrick house", 1, 7, 5, 900f, RoofStyle.WoodShingles, stone, FurnitureArt.WoodFloor, FurnitureArt.WoodDoor, fieldstone, 60, 40, 10, 60f),
                    Level("Timber house", 2, 9, 6, 1300f, RoofStyle.ClayTiles, wood, FurnitureArt.WoodFloor, FurnitureArt.WoodDoor, fieldstone, 100, 80, 20, 90f),
                    Level("Stone house", 3, 11, 7, 1800f, RoofStyle.Slate, stone, FurnitureArt.StoneFloor, FurnitureArt.HeavyDoor, fieldstone, 120, 160, 30, 120f),
                    Level("Manor", 4, 13, 8, 2400f, RoofStyle.Copper, stone, FurnitureArt.WoodFloor, FurnitureArt.HeavyDoor, brick, 160, 240, 40, 150f),
                    Level("Brick townhouse", 5, 15, 9, 3000f, RoofStyle.CorrugatedIron, stone, FurnitureArt.WoodFloor, FurnitureArt.HeavyDoor, brick, 200, 320, 50, 180f),
                    Level("Concrete house", 6, 17, 10, 3800f, RoofStyle.TarFelt, stone, FurnitureArt.StoneFloor, FurnitureArt.HeavyDoor, grey, 220, 420, 60, 210f),
                    Level("Bunker house", 7, 19, 11, 4800f, RoofStyle.Concrete, stone, FurnitureArt.StoneFloor, FurnitureArt.HeavyDoor, grey, 260, 560, 80, 240f),
                };
            });
            return Asset<BuildingConfig>("Building/Houses/House", b =>
            {
                b.displayName = "House"; b.kind = BuildingKind.House; b.category = BuildCategory.Houses; b.house = levels;
                b.size = new Vector2Int(3, 3); b.rotations = 4; b.woodCost = 40; b.stoneCost = 10; b.foodCost = 5; b.buildSeconds = 25f;
                b.health = 600f; b.height = 0.6f; b.blocksMovement = true; b.dragToPlaceLines = false; b.roomTag = RoomTag.None;
            });
        }

        static HouseLevel Level(string displayName, int age, int width, int height, float health, RoofStyle roof, StructureMaterial walls,
            string floor, string door, Color32 chimney, int wood, int stone, int food, float seconds) => new HouseLevel
        {
            displayName = displayName, age = age, inside = new Vector2Int(width, height), health = health, roof = roof, walls = walls,
            floorArt = floor, doorArt = door, chimney = chimney, woodCost = wood, stoneCost = stone, foodCost = food,
            buildSeconds = Mathf.Max(0.1f, seconds),
        };

        static BuildingConfig Door(string file, string displayName, string art, int wood, int stone, float seconds, float health) =>
            Asset<BuildingConfig>("Building/Doors/" + file, b =>
            {
                b.displayName = displayName; b.kind = BuildingKind.Door; b.category = BuildCategory.Walls; b.art = art;
                b.size = Vector2Int.one; b.rotations = 2; b.woodCost = wood; b.stoneCost = stone; b.buildSeconds = seconds;
                b.health = health; b.height = 0.2f; b.blocksMovement = true; b.dragToPlaceLines = false; b.roomTag = RoomTag.None;
            });

        // A ladder or stairs: leans on the wall it faces; the ruler climbs onto the wall there (E).
        static BuildingConfig Climb(string file, string displayName, string art, int wood, int stone, float seconds, float health) =>
            Asset<BuildingConfig>("Building/Walls/" + file, b =>
            {
                b.displayName = displayName; b.kind = BuildingKind.Furniture; b.category = BuildCategory.Walls; b.art = art;
                b.size = Vector2Int.one; b.rotations = 4; b.woodCost = wood; b.stoneCost = stone; b.buildSeconds = seconds;
                b.climb = true; b.blocksMovement = false; b.health = health; b.height = 0.35f; b.roomTag = RoomTag.None;
                b.dragToPlaceLines = false;
            });

        static BuildingConfig Furnish(string file, string displayName, string art, BuildCategory category, Vector2Int size, int rotations,
            int wood, int stone, int food, float seconds, RoomTag tag, bool blocks, float health, float height,
            float light = 0f, Vector2[] flames = null, float flameSize = 1f, int sleeps = 0, bool flat = false) =>
            Asset<BuildingConfig>("Building/" + file, b =>
            {
                b.displayName = displayName; b.kind = BuildingKind.Furniture; b.category = category; b.art = art;
                b.size = size; b.rotations = rotations; b.woodCost = wood; b.stoneCost = stone; b.foodCost = food; b.buildSeconds = seconds;
                b.roomTag = tag; b.blocksMovement = blocks; b.health = health; b.height = height; b.flat = flat; b.sleeps = sleeps;
                b.lightSize = light; b.flames = flames != null ? (Vector2[])flames.Clone() : new Vector2[0]; b.flameSize = flameSize;
                b.dragToPlaceLines = false;
            });

        static BuildingConfig Store(string file, string displayName, string art, int rotations, int wood, float seconds, int capacity, float height, float health) =>
            Asset<BuildingConfig>("Building/Storage/" + file, b =>
            {
                b.displayName = displayName; b.kind = BuildingKind.Storage; b.category = BuildCategory.Storage; b.art = art;
                b.size = Vector2Int.one; b.rotations = rotations; b.woodCost = wood; b.buildSeconds = seconds; b.storageCapacity = capacity;
                b.height = height; b.roomTag = RoomTag.Storage; b.dragToPlaceLines = false; b.health = health;
            });

        // Loads the asset if it already exists (keeping your changes), otherwise
        // creates it with these starting values.
        static T Asset<T>(string name, Action<T> startingValues) where T : ScriptableObject
        {
            string path = $"{DataRoot}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            startingValues(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void Folder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
                AssetDatabase.CreateFolder(parent, name);
        }

        static Camera MakeCamera(ViewConfig view)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0, 0, -10);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = view.walkZoom;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = view.backgroundColor;
            // Post-processing (vignette, bloom, color grading) only shows on cameras that ask for it.
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            go.AddComponent<AudioListener>();
            go.AddComponent<CameraRig>().Setup(view);
            return camera;
        }

        // A material that ignores lighting, for things that glow (fire, sparks, shots).
        static Material GlowMaterial()
        {
            const string path = DataRoot + "/Look/Glow.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                Debug.LogWarning("Couldn't find URP's Sprite-Unlit-Default shader, so fire and sparks won't glow at night. Everything else works.");
                return null;
            }
            var material = new Material(shader) { name = "Glow" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void StoneLook(StructureMaterial m)
        {
            m.pattern = WallPattern.Stone;
            m.gapColor = new Color32(46, 44, 42, 255);
            m.colors = new[]
            {
                new Color32(112, 110, 104, 255), new Color32(100, 98, 94, 255),
                new Color32(122, 118, 110, 255), new Color32(92, 92, 94, 255),
            };
        }

        static void WoodLook(StructureMaterial m)
        {
            m.pattern = WallPattern.Logs;
            m.gapColor = new Color32(30, 22, 16, 255);
            m.colors = new[]
            {
                new Color32(150, 112, 72, 255), new Color32(138, 102, 66, 255),
                new Color32(162, 122, 80, 255), new Color32(128, 94, 60, 255),
            };
        }

        // The light settings of a new DayNightConfig (the times of sunrise, sunset and day length are kept).
        static void NewDayNightLook(DayNightConfig d)
        {
            var fresh = ScriptableObject.CreateInstance<DayNightConfig>();
            d.startHour = fresh.startHour;
            d.twilightHours = fresh.twilightHours;
            d.dayColor = fresh.dayColor;
            d.duskColor = fresh.duskColor;
            d.nightColor = fresh.nightColor;
            d.dayBrightness = fresh.dayBrightness;
            d.nightBrightness = fresh.nightBrightness;
            d.torchColor = fresh.torchColor;
            d.torchBrightness = fresh.torchBrightness;
            d.torchRadius = fresh.torchRadius;
            d.fireColor = fresh.fireColor;
            d.fireBrightness = fresh.fireBrightness;
            d.fireRadius = fresh.fireRadius;
            d.fireDaytimeShare = fresh.fireDaytimeShare;
            UnityEngine.Object.DestroyImmediate(fresh);
        }

        // The dark, gritty look: dark corners, glow around bright things, more
        // contrast, less color, a little film grain. Made again when remake is true.
        static VolumeProfile PostProcessingProfile(bool remake)
        {
            const string path = DataRoot + "/Look/PostProcessing.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null && !remake) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(path);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.4f);
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);
            // Gentle contrast: URP's contrast turns the darkest shades pure black, and this game is dark.
            var grading = profile.Add<ColorAdjustments>();
            grading.contrast.Override(5f);
            grading.saturation.Override(-25f);
            grading.postExposure.Override(-0.1f);
            var grain = profile.Add<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.12f);
            grain.response.Override(0.8f);
            // The effects are saved inside the profile file.
            foreach (var effect in profile.components)
            {
                effect.name = effect.GetType().Name;
                AssetDatabase.AddObjectToAsset(effect, profile);
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        static void MakeVolume(VolumeProfile profile)
        {
            if (profile == null) return;
            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static Light2D MakeGlobalLight()
        {
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            var layers = SortingLayer.layers;
            var ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
            light.targetSortingLayers = ids;
            return light;
        }

        static void MakeWorld(DestructionSettings settings, DamageType[] damageTypes)
        {
            new GameObject("Pixel World").AddComponent<PixelWorld>().Setup(settings, damageTypes);
        }

        static void RemoveObsolete()
        {
            foreach (var path in Obsolete)
            {
                bool exists = AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null;
                if (exists && AssetDatabase.DeleteAsset(path))
                    Debug.Log($"Removed {path} (old Phase 1 test content, replaced by the new game scenes).");
            }
        }

        // These scenes first, in this order (the first is the one a built game starts with), then any others you added.
        static void SetBuildScenes(params string[] paths)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var path in paths) scenes.Add(new EditorBuildSettingsScene(path, true));
            foreach (var existing in EditorBuildSettings.scenes)
                if (Array.IndexOf(Obsolete, existing.path) < 0 && Array.IndexOf(paths, existing.path) < 0) scenes.Add(existing);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
