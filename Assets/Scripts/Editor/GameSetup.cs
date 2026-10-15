using System;
using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Defenses;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.Progression;
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
        /// 4: roofs on the build bar (one kind per age).
        /// 5: farming (tree saplings, berry bushes) for the woodcutters and foragers.
        /// 6: traps and towers.
        /// 7: the full set of defences (31, by age) and the ages themselves.
        /// 8: a bigger map (256 tiles across instead of 96).
        /// 9: mines (an entrance on the map, an underground to dig out, ores).
        /// </summary>
        const int SettingsVersion = 16;

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
            foreach (var area in new[] { "Destruction", "Combat", "Character", "Economy", "World", "Building", "Villagers", "Look", "Zombies", "Defenses", "Progression", "Network" })
                Folder(DataRoot, area);
            foreach (var area in new[] { "Doors", "Floors", "Furniture", "Lights", "Storage", "Walls", "Houses", "Farming", "Traps", "Towers", "Mining", "Machines" })
                Folder(DataRoot + "/Building", area);
            Folder(DataRoot + "/Economy", "Machines");
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
            bool roofsUpdate = oldContent != null && oldContent.settingsVersion < 4;
            bool farmingUpdate = oldContent != null && oldContent.settingsVersion < 5;
            bool defensesUpdate = oldContent != null && oldContent.settingsVersion < 7;
            bool minesUpdate = oldContent != null && oldContent.settingsVersion < 9;
            bool hardAges = oldContent != null && oldContent.settingsVersion < 10;
            bool nightHordes = oldContent != null && oldContent.settingsVersion < 11;
            bool endless = oldContent != null && oldContent.settingsVersion < 12;
            bool machinesUpdate = oldContent != null && oldContent.settingsVersion < 16;
            bool costlyAges = oldContent != null && oldContent.settingsVersion < 16;
            bool darkNights = oldContent != null && oldContent.settingsVersion < 16;
            bool gatesUpdate = oldContent != null && oldContent.settingsVersion < 14;

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
            var mineConfig = Asset<MineConfig>("Building/Mining/MineConfig", m => { });
            var mining = new List<BuildingConfig>
            {
                Asset<BuildingConfig>("Building/Mining/MineEntrance", b =>
                {
                    b.displayName = "Mine entrance"; b.kind = BuildingKind.Mine; b.category = BuildCategory.Mining; b.art = FurnitureArt.MineHead;
                    b.mine = mineConfig; b.size = new Vector2Int(2, 2); b.rotations = 1; b.woodCost = 80; b.stoneCost = 20; b.buildSeconds = 40f;
                    b.health = 2500f; b.height = 0.9f; b.blocksMovement = true; b.dragToPlaceLines = false; b.roomTag = RoomTag.None;
                }),
                Asset<BuildingConfig>("Building/Mining/Dig", b =>
                {
                    b.displayName = "Dig"; b.kind = BuildingKind.Dig; b.category = BuildCategory.Mining; b.size = Vector2Int.one;
                    b.rotations = 1; b.buildSeconds = 1f; b.health = 0f; b.blocksMovement = false; b.dragToPlaceLines = true; b.roomTag = RoomTag.None;
                }),
            };
            var blasts = new List<DamageType>();
            var defensesByAge = Defenses(blasts);
            // Planted trees grow back after they're cut (a tree farm); a little less wood than a wild one.
            var plantedTree = Asset<ResourceNodeConfig>("Economy/PlantedTree", n =>
            {
                n.look = NodeLook.Tree; n.resource = ResourceType.Wood; n.amount = 45; n.perHit = 5; n.secondsPerHit = 0.5f;
                n.regrowSeconds = 300f; n.replant = true; n.blockRadius = 0.18f; n.reach = 0.6f; n.chipColor = new Color32(150, 108, 66, 255);
            });
            // Each age's automation (machines), unlocked with the age.
            var machinesByAge = Machines(plantedTree);
            for (int i = 0; i < machinesByAge.Length && i < defensesByAge.Length; i++) defensesByAge[i].AddRange(machinesByAge[i]);
            var defenses = new List<BuildingConfig>();
            foreach (var age in defensesByAge) defenses.AddRange(age);
            var farming = new List<BuildingConfig>
            {
                Asset<BuildingConfig>("Building/Farming/TreeSapling", b =>
                {
                    b.displayName = "Tree sapling"; b.kind = BuildingKind.Plant; b.category = BuildCategory.Farming; b.plant = plantedTree;
                    b.growSeconds = 240f; b.size = Vector2Int.one; b.foodCost = 2; b.buildSeconds = 2f; b.dragToPlaceLines = true;
                    b.blocksMovement = true; b.health = 0f; b.roomTag = RoomTag.None;
                }),
                Asset<BuildingConfig>("Building/Farming/BerryBush", b =>
                {
                    b.displayName = "Berry bush"; b.kind = BuildingKind.Plant; b.category = BuildCategory.Farming; b.plant = bush;
                    b.growSeconds = 150f; b.size = Vector2Int.one; b.woodCost = 1; b.foodCost = 4; b.buildSeconds = 2f; b.dragToPlaceLines = true;
                    b.blocksMovement = true; b.health = 0f; b.roomTag = RoomTag.None;
                }),
            };
            var roofs = Roofs();
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
            if (content.progression == null) content.progression = Ages(defensesByAge, roofs);
            if (content.network == null) content.network = Asset<Networking.NetworkConfig>("Network/NetworkConfig", n => { });
            if (content.armory == null) content.armory = Weapons(bullet, rockets, c4);
            // Everything on the build bar, in order (each item shows on its category's tab).
            var menu = new List<BuildingConfig> { stoneWallBuilding, woodWallBuilding };
            menu.AddRange(castle);
            int firstStorage = castle.FindIndex(b => b != null && b.category == BuildCategory.Storage);
            menu.Insert(firstStorage >= 0 ? firstStorage + 2 : menu.Count, storehouse);
            menu.Add(house);
            menu.AddRange(mining);
            menu.AddRange(farming);
            menu.AddRange(defenses);
            // Roofs go on the Floors & roofs tab after the rugs.
            int afterFloors = menu.FindLastIndex(b => b != null && b.category == BuildCategory.Floors);
            menu.InsertRange(afterFloors >= 0 ? afterFloors + 1 : menu.Count, roofs);
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

            if (minesUpdate)
            {
                var list = new List<BuildingConfig>(content.buildMenu);
                foreach (var m in mining) if (!list.Contains(m)) list.Add(m);
                content.buildMenu = list.ToArray();
                Debug.Log("Added mines to your build bar (the Mining tab). Your other settings were kept.");
            }
            if (defensesUpdate)
            {
                var list = new List<BuildingConfig>(content.buildMenu);
                foreach (var d in defenses) if (!list.Contains(d)) list.Add(d);
                content.buildMenu = list.ToArray();
                Debug.Log("Added traps and towers to your build bar (the Traps and Towers tabs). Your other settings were kept.");
            }
            if (farmingUpdate)
            {
                var list = new List<BuildingConfig>(content.buildMenu);
                foreach (var plant in farming) if (!list.Contains(plant)) list.Add(plant);
                content.buildMenu = list.ToArray();
                Debug.Log("Added farming (tree saplings, berry bushes) to your build bar. Your other settings were kept.");
            }
            if (roofsUpdate)
            {
                var list = new List<BuildingConfig>(content.buildMenu);
                foreach (var roof in roofs) if (!list.Contains(roof)) list.Add(roof);
                content.buildMenu = list.ToArray();
                Debug.Log("Added roofs to your build bar (the Floors & roofs tab). Your other settings were kept.");
            }

            if (hardAges)
            {
                // Each new age takes days of research, ore and villagers now (and survival researches too).
                for (int i = 0; i < AdvanceNames.Length; i++)
                {
                    var tech = AssetDatabase.LoadAssetAtPath<TechConfig>($"{DataRoot}/{AdvancePath(i)}.asset");
                    if (tech == null) continue;
                    AgeTech(tech, i);
                    EditorUtility.SetDirty(tech);
                }
                if (content.progression != null)
                {
                    content.progression.survivalAgesByWaves = false;
                    EditorUtility.SetDirty(content.progression);
                }
                Debug.Log("Ages now take days of research, with ore and villagers (survival too). Your other settings were kept.");
            }

            if (nightHordes && content.survival != null)
            {
                // Survival hordes come at night now, two minutes after each one is beaten.
                content.survival.hordesAtNight = true;
                content.survival.breakSeconds = 120f;
                EditorUtility.SetDirty(content.survival);
                Debug.Log("Survival hordes now come at night, 2 minutes apart. Your other settings were kept.");
            }

            if (costlyAges)
            {
                // Research to each new age costs far more now (10,000 wood and stone to the Bronze Age, then steeper).
                for (int i = 0; i < AdvanceNames.Length; i++)
                {
                    var tech = AssetDatabase.LoadAssetAtPath<TechConfig>($"{DataRoot}/{AdvancePath(i)}.asset");
                    if (tech == null) continue;
                    AgeTech(tech, i);
                    EditorUtility.SetDirty(tech);
                }
                Debug.Log("Research costs and times for each age were updated (a third of the earlier big costs). Your other settings were kept.");
            }
            if (darkNights && content.dayNight != null)
            {
                // Nights are pitch black now except where something gives light, and fires light twice as far.
                content.dayNight.nightBrightness = 0f;
                if (content.dayNight.fireRadius < 9f) content.dayNight.fireRadius *= 2f;
                EditorUtility.SetDirty(content.dayNight);
                Debug.Log("Nights are pitch black now (only lights show anything), and fires and lamps light twice as far.");
            }
            if (gatesUpdate)
            {
                // The big gates join the Walls & doors tab, after the doors.
                var list = new List<BuildingConfig>(content.buildMenu);
                int at = list.FindLastIndex(b => b != null && b.kind == BuildingKind.Door);
                foreach (var g in castle)
                    if (g != null && g.kind == BuildingKind.Door && !list.Contains(g)) list.Insert(at >= 0 ? ++at : list.Count, g);
                content.buildMenu = list.ToArray();
                Debug.Log("Added the wooden gate and castle gate (Walls & doors tab). Your other settings were kept.");
            }
            if (machinesUpdate)
            {
                // The smelter (Medieval) and the machines (Industrial): on the build bar's Industry tab, unlocked with their age.
                var all = new List<BuildingConfig>();
                foreach (var age in machinesByAge) all.AddRange(age);
                var list = new List<BuildingConfig>(content.buildMenu);
                foreach (var m in all) if (!list.Contains(m)) list.Add(m);
                content.buildMenu = list.ToArray();
                var ages = content.progression != null ? content.progression.ages : null;
                void Unlock(int age, List<BuildingConfig> things)
                {
                    if (ages == null || age >= ages.Length || ages[age] == null) return;
                    var u = new List<BuildingConfig>(ages[age].startUnlocks ?? new BuildingConfig[0]);
                    foreach (var m in things) if (!u.Contains(m)) u.Add(m);
                    ages[age].startUnlocks = u.ToArray();
                    EditorUtility.SetDirty(ages[age]);
                }
                for (int i = 0; i < machinesByAge.Length; i++) Unlock(i, machinesByAge[i]);
                Debug.Log("Added each age's machines (Industry tab: tree farm, sawmills, quarries, belts, smelter, generator) and electric lights (Lights tab). Your other settings were kept.");
            }

            if (endless && content.network != null && content.network.kingdomSpacing < 560)
            {
                // Each kingdom has lands 512 tiles across in the endless world: kingdoms go further apart so they don't overlap.
                content.network.kingdomSpacing = 560;
                EditorUtility.SetDirty(content.network);
                Debug.Log("Worlds are endless now, and online kingdoms are 560 tiles apart. Your other settings were kept.");
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

            var damageTypes = new List<DamageType> { bullet, rocketBlast, c4Blast, smash, claw };
            damageTypes.AddRange(blasts);

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
            MakeWorld(settings, damageTypes.ToArray());
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
            MakeWorld(settings, damageTypes.ToArray());
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
                Gate("WoodGate", "Wooden gate (3 wide)", FurnitureArt.WoodGate, 3, 90, 20, 25f, 1200f),
                Gate("CastleGate", "Castle gate (4 wide)", FurnitureArt.CastleGate, 4, 160, 200, 60f, 3000f),
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

        // The ruler's weapons, one for each age (and dynamite and the bazooka): a club to start with, the rest
        // made at an anvil by crafters once the kingdom reaches their age. Later ones cost iron and gold from the mines.
        static Combat.ArmoryConfig Weapons(DamageType bullet, WeaponConfig rockets, ExplosiveConfig c4)
        {
            WeaponConfig Gun(string file, float hit, float rate, bool auto, float speed, float range, float spread, int length, int thick, Color32 color,
                DamageType damage, int pierce = 0, float recoil = 0.006f, float overWalls = 1.2f) =>
                Asset<WeaponConfig>("Combat/Ruler" + file, w =>
                {
                    w.hitDamage = hit; w.fireRate = rate; w.automatic = auto; w.projectileSpeed = speed; w.range = range; w.spreadDegrees = spread;
                    w.projectileLength = length; w.projectileThickness = thick; w.projectileColor = color; w.damage = damage; w.pierce = pierce;
                    w.recoilShake = recoil; w.overWalls = overWalls;
                });
            var stone = new Color32(130, 124, 112, 255);
            var wood = new Color32(176, 140, 92, 255);
            var tracer = new Color32(255, 214, 102, 255);
            var club = Asset<WeaponConfig>("Combat/RulerClub", w =>
            {
                w.melee = true; w.hitDamage = 20f; w.fireRate = 1.8f; w.automatic = true; w.range = 1.15f; w.meleeArc = 110f;
            });
            var sling = Gun("Sling", 14f, 1.3f, false, 14f, 7f, 4f, 5, 5, stone, null, 0, 0f, 0.6f);
            var bow = Gun("Bow", 24f, 1.3f, false, 19f, 9.5f, 2f, 12, 2, wood, null, 0, 0f, 0.8f);
            var crossbow = Gun("Crossbow", 42f, 0.7f, false, 25f, 10.5f, 1.2f, 10, 2, wood, null, 1, 0.004f, 1f);
            var handCannon = Gun("HandCannon", 70f, 0.35f, false, 26f, 9f, 5f, 6, 4, tracer, bullet, 0, 0.03f);
            var musket = Gun("Musket", 65f, 0.5f, false, 32f, 12f, 2.5f, 6, 3, tracer, bullet, 0, 0.02f);
            var lever = Gun("LeverRifle", 40f, 1.8f, false, 34f, 13f, 1.5f, 8, 2, tracer, bullet, 0, 0.01f);
            var bolt = Gun("BoltRifle", 80f, 0.9f, false, 40f, 16f, 0.6f, 9, 2, tracer, bullet, 2, 0.015f);
            var smg = Gun("Smg", 20f, 11f, true, 32f, 11f, 5f, 7, 2, tracer, bullet, 0, 0.006f);

            Combat.ArmsItem Item(string name, string about, string art, int age, WeaponConfig weapon, float seconds,
                int wood = 0, int food = 0, int copper = 0, int iron = 0, int gold = 0) => new Combat.ArmsItem
            {
                displayName = name, description = about, art = art, age = age, weapon = weapon, craftSeconds = seconds,
                woodCost = wood, foodCost = food, copperCost = copper, ironCost = iron, goldCost = gold,
            };
            var items = new[]
            {
                Item("Club", "A heavy wooden club: hits everything in front of you.", Art.WeaponArt.Club, 0, club, 1f),
                Item("Sling", "Throws stones: weak, but it keeps them at a distance.", Art.WeaponArt.Sling, 0, sling, 25f, wood: 30, food: 10),
                Item("Bow", "Arrows, faster and further than stones.", Art.WeaponArt.Bow, 1, bow, 40f, wood: 80, food: 20, copper: 20),
                Item("Crossbow", "Slow to load; its bolts go through the first zombie.", Art.WeaponArt.Crossbow, 2, crossbow, 70f, wood: 120, copper: 60, iron: 40),
                Item("Hand cannon", "A bronze tube on a pole: one huge, wild shot.", Art.WeaponArt.HandCannon, 3, handCannon, 100f, wood: 100, copper: 60, iron: 160),
                Item("Musket", "Hard-hitting and far-reaching, slow to reload.", Art.WeaponArt.Musket, 4, musket, 130f, wood: 140, iron: 220, gold: 30),
                Item("Lever-action rifle", "Quick shots, one after another.", Art.WeaponArt.LeverRifle, 5, lever, 160f, wood: 120, iron: 280, gold: 50),
                Item("Bolt-action rifle", "Long range; each bullet goes through two zombies.", Art.WeaponArt.BoltRifle, 6, bolt, 190f, wood: 140, iron: 360, gold: 70),
                Item("Submachine gun", "Fires as long as you hold the button.", Art.WeaponArt.Smg, 7, smg, 240f, wood: 100, iron: 450, gold: 110),
                Item("Dynamite", "Throw it with G: it blows holes in walls and zombies.", Art.WeaponArt.Club, 5, null, 60f, wood: 80, food: 120, copper: 80),
                Item("Bazooka", "Rockets with the right mouse button, whatever is in your hand.", Art.WeaponArt.Bazooka, 7, rockets, 300f, copper: 60, iron: 600, gold: 160),
            };
            items[0].startsOwned = true;
            items[9].explosive = c4;
            items[10].heavy = true;
            return Asset<Combat.ArmoryConfig>("Combat/ArmoryConfig", a => a.items = items);
        }

        static readonly string[] AdvanceNames = { "Bronze casting", "Iron smelting", "Feudal lords", "Gunpowder", "Steam power", "Total war", "Blitzkrieg" };
        static string AdvancePath(int i) => $"Progression/{i + 1}_{AdvanceNames[i].Replace(" ", "")}";

        // Reaching each new age is meant to be hard: days of real research (1 day for the Bronze Age up to a week for
        // World War 2, about four weeks in all at research speed 1), a lot from storage including ore from the mines
        // (World War 2 alone needs room for about 28 storehouses' worth), and enough villagers (beds give homes).
        static void AgeTech(TechConfig t, int i)
        {
            int[] days = { 1, 2, 3, 4, 5, 6, 7 };
            int[,] cost =
            {
                //  wood  stone  food copper iron gold
                {  10000,  10000,  3000,   500,     0,     0 },   // to the Bronze Age
                {  20000,  20000,  6000,  1500,   500,     0 },
                {  35000,  35000, 10000,  3000,  2000,   100 },
                {  55000,  55000, 15000,  5000,  5000,   400 },
                {  80000,  80000, 22000,  8000, 10000,  1000 },
                { 115000, 115000, 30000, 12000, 18000,  2000 },
                { 160000, 160000, 40000, 18000, 30000,  4000 },
            };
            int[] villagers = { 6, 9, 13, 18, 24, 30, 38 };
            // A third of the table above (going through the ages was made 3 times easier): costs and research time.
            const int Easier = 3;
            t.researchHours = days[i] * 24f / Easier;
            t.woodCost = cost[i, 0] / Easier; t.stoneCost = cost[i, 1] / Easier; t.foodCost = cost[i, 2] / Easier;
            t.copperCost = cost[i, 3] / Easier; t.ironCost = cost[i, 4] / Easier; t.goldCost = cost[i, 5] / Easier;
            t.minVillagers = villagers[i];
        }

        // The eight ages: what each one brings (its defences and roof; houses get its level), and the research that leads on.
        // Everything else (walls, furniture, the first house...) comes with the Stone Age.
        static ProgressionConfig Ages(List<BuildingConfig>[] defensesByAge, List<BuildingConfig> roofs)
        {
            string[] names = { "Stone Age", "Bronze Age", "Iron Age", "Medieval", "Renaissance", "Industrial", "World War 1", "World War 2" };
            string[] about =
            {
                "Pits, stakes and fire. Slings from a stump.",
                "Bronze spikes, tar and thorn hedges. Archers on wooden towers.",
                "Caltrops and abatis. Ballistas and catapults.",
                "Bear traps and boiling oil. Crossbows and trebuchets.",
                "Powder kegs and cheval de frise. Muskets and cannon.",
                "Snares, barbed wire and dynamite. The Gatling turret shoots by itself.",
                "Landmines, gas and sandbags. Machine guns and mortars.",
                "Bouncing mines, napalm and hedgehogs. Flamethrowers and howitzers.",
            };
            var colors = new[]
            {
                new Color(0.72f, 0.62f, 0.48f), new Color(0.8f, 0.58f, 0.32f), new Color(0.6f, 0.62f, 0.66f), new Color(0.62f, 0.5f, 0.78f),
                new Color(0.86f, 0.72f, 0.36f), new Color(0.7f, 0.46f, 0.36f), new Color(0.56f, 0.62f, 0.44f), new Color(0.48f, 0.56f, 0.5f),
            };
            // Research to reach the next age (AgeTech has its days, costs and villagers).
            string[] advances = AdvanceNames;
            var ages = new AgeConfig[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var unlocks = new List<BuildingConfig>(defensesByAge[i]);
                if (i < roofs.Count) unlocks.Add(roofs[i]);
                TechConfig advance = null;
                if (i < advances.Length)
                    advance = Asset<TechConfig>(AdvancePath(i), t =>
                    {
                        t.displayName = advances[index];
                        t.description = $"Leads into the {names[index + 1]}.";
                        t.advancesAge = true;
                        AgeTech(t, index);
                    });
                ages[i] = Asset<AgeConfig>($"Progression/Age{i + 1}_{names[i].Replace(" ", "")}", a =>
                {
                    a.displayName = names[index];
                    a.description = about[index];
                    a.color = colors[index];
                    a.startUnlocks = unlocks.ToArray();
                    a.techs = advance != null ? new[] { advance } : new TechConfig[0];
                });
            }
            return Asset<ProgressionConfig>("Progression/ProgressionConfig", p => p.ages = ages);
        }

        // The smelter (Medieval: iron and wood into steel) and the Industrial machines built with that steel.
        // Each age's machines (index = age: 0 Stone Age ... 7 World War 2).
        static List<BuildingConfig>[] Machines(ResourceNodeConfig plantedTree)
        {
            MachineConfig Machine(string file, MachineKind kind, float seconds, int output, float reach, int woodIn, int ironIn, bool power = true, float belt = 2f,
                ResourceNodeConfig plant = null, float grow = 0f) =>
                Asset<MachineConfig>("Economy/Machines/" + file, m =>
                {
                    m.kind = kind; m.workSeconds = seconds; m.output = output; m.reach = reach; m.holds = 60;
                    m.woodIn = woodIn; m.ironIn = ironIn; m.beltSpeed = belt; m.needsPower = power;
                    m.plant = plant; m.growSeconds = grow;
                });
            BuildingConfig Item(string file, string displayName, string art, int size, int wood, int stone, int steel, float seconds,
                float health, float height, bool flat, MachineConfig machine) =>
                Asset<BuildingConfig>("Building/Machines/" + file, b =>
                {
                    b.displayName = displayName; b.kind = BuildingKind.Furniture; b.category = BuildCategory.Industry; b.art = art;
                    b.size = new Vector2Int(size, size); b.rotations = 1; b.woodCost = wood; b.stoneCost = stone; b.steelCost = steel;
                    b.buildSeconds = seconds; b.health = health; b.height = height; b.blocksMovement = !flat; b.flat = flat;
                    b.dragToPlaceLines = flat; b.roomTag = RoomTag.Workshop; b.machine = machine;
                });
            // Bronze Age: a tree farm that keeps trees planted round it (they grow back when cut, so a sawmill or woodcutters never run out).
            var bronze = new List<BuildingConfig>
            {
                Item("TreeFarm", "Tree farm", FurnitureArt.TreeFarm, 2, 120, 20, 0, 20f, 300f, 0.3f, false,
                    Machine("TreeFarm", MachineKind.TreeFarm, 12f, 16, 5f, 0, 0, false, 2f, plantedTree, 180f)),
            };
            // Iron Age: the first automation, worked by water and treadwheels (no power), slower than the Industrial machines.
            var iron = new List<BuildingConfig>
            {
                Item("WaterSawmill", "Water sawmill", FurnitureArt.Sawmill, 2, 150, 60, 0, 25f, 400f, 0.7f, false, Machine("WaterSawmill", MachineKind.Sawmill, 16f, 4, 6f, 0, 0, false)),
                Item("TreadwheelQuarry", "Treadwheel quarry", FurnitureArt.Quarry, 2, 120, 80, 0, 25f, 500f, 1f, false, Machine("TreadwheelQuarry", MachineKind.Quarry, 20f, 3, 1f, 0, 0, false)),
                Item("RollerTrack", "Roller track", FurnitureArt.Conveyor, 1, 4, 1, 0, 2f, 80f, 0.05f, true, Machine("RollerTrack", MachineKind.Conveyor, 1f, 1, 1f, 0, 0, false, 1f)),
            };
            var medieval = new List<BuildingConfig>
            {
                Item("Smelter", "Smelter", FurnitureArt.Smelter, 2, 80, 150, 0, 30f, 800f, 0.9f, false, Machine("Smelter", MachineKind.Smelter, 20f, 1, 1f, 2, 2)),
            };
            var industrial = new List<BuildingConfig>
            {
                Item("Generator", "Generator", FurnitureArt.Generator, 2, 100, 100, 40, 35f, 700f, 1f, false, Machine("Generator", MachineKind.Generator, 30f, 0, 10f, 2, 0)),
                Item("Sawmill", "Sawmill", FurnitureArt.Sawmill, 2, 80, 20, 30, 30f, 600f, 0.7f, false, Machine("Sawmill", MachineKind.Sawmill, 8f, 5, 8f, 0, 0)),
                Item("Quarry", "Quarry", FurnitureArt.Quarry, 2, 40, 60, 40, 30f, 700f, 1f, false, Machine("Quarry", MachineKind.Quarry, 10f, 4, 1f, 0, 0)),
                Item("Conveyor", "Conveyor belt", FurnitureArt.Conveyor, 1, 0, 2, 1, 2f, 150f, 0.05f, true, Machine("Conveyor", MachineKind.Conveyor, 1f, 1, 1f, 0, 0)),
                // Electric lights (Lights tab): they only shine while a generator's power reaches them.
                Lamp(Item("ElectricLamp", "Electric lamp", FurnitureArt.ElectricLamp, 1, 5, 5, 3, 6f, 150f, 1.4f, false, Machine("ElectricLamp", MachineKind.Lamp, 1f, 0, 1.3f, 0, 0))),
                Lamp(Item("Floodlight", "Floodlight", FurnitureArt.Floodlight, 1, 0, 10, 8, 10f, 250f, 1.5f, false, Machine("Floodlight", MachineKind.Lamp, 1f, 0, 2.6f, 0, 0))),
            };
            return new[] { new List<BuildingConfig>(), bronze, iron, medieval, new List<BuildingConfig>(), industrial, new List<BuildingConfig>(), new List<BuildingConfig>() };

            // Lamps go on the Lights tab and can be put up inside houses.
            static BuildingConfig Lamp(BuildingConfig b)
            {
                if (b.category == BuildCategory.Industry) b.category = BuildCategory.Lights;
                return b;
            }
        }

        // Traps, barriers and towers, four or five for each age; returns them by age (blasts: their explosions, for the PixelWorld).
        static List<BuildingConfig>[] Defenses(List<DamageType> blasts)
        {
            // An explosion: how big a hole it makes in walls (small: they go off among zombies at your walls) and what it does to creatures.
            DamageType Blast(string file, int holePixels, int holeDamage, float creatureDamage, float radius, float friendly, float shake, Color32 sparks)
            {
                var d = Asset<DamageType>("Destruction/" + file, t =>
                {
                    t.category = DamageCategory.Explosive;
                    t.radiusPixels = holePixels; t.centerDamage = holeDamage; t.edgeDamage = holeDamage / 4; t.jaggedness = 110; t.noiseBlock = 3;
                    t.debrisChance = 0.12f; t.debrisSpeed = new Vector2(2f, 8f); t.debrisLift = new Vector2(2f, 6f);
                    t.sparks = 50; t.sparkColor = sparks; t.screenShake = shake;
                    t.creatureDamage = creatureDamage; t.blastRadius = radius; t.friendlyShare = friendly;
                });
                blasts.Add(d);
                return d;
            }
            var fire = new Color32(255, 160, 70, 255);
            var mineBlast = Blast("MineBlast", 30, 300, 200f, 2.2f, 0.35f, 0.2f, fire);
            var cannonBlast = Blast("CannonBlast", 8, 60, 110f, 1.3f, 0.1f, 0.12f, fire);
            var rockImpact = Blast("RockImpact", 6, 50, 70f, 1f, 0.1f, 0.08f, new Color32(170, 160, 140, 255));
            var bigRock = Blast("TrebuchetImpact", 8, 80, 110f, 1.4f, 0.1f, 0.15f, new Color32(170, 160, 140, 255));
            var oilSplash = Blast("OilSplash", 2, 10, 60f, 1.2f, 0.1f, 0.02f, new Color32(255, 200, 90, 255));
            var powderBlast = Blast("PowderKegBlast", 34, 320, 160f, 2.4f, 0.35f, 0.25f, fire);
            var dynamiteBlast = Blast("DynamiteBlast", 36, 360, 190f, 2.6f, 0.35f, 0.3f, fire);
            var mortarBlast = Blast("MortarBlast", 8, 70, 130f, 1.8f, 0.1f, 0.15f, fire);
            var bouncingBlast = Blast("BouncingMineBlast", 12, 120, 260f, 3f, 0.35f, 0.3f, fire);
            var howitzerBlast = Blast("HowitzerBlast", 12, 120, 240f, 2.5f, 0.1f, 0.35f, fire);

            WeaponConfig Shot(string file, float hit, float speed, float range, float spread, int length, int thickness, Color32 color,
                DamageType damage = null, bool explode = false) =>
                Asset<WeaponConfig>("Combat/" + file, w =>
                {
                    w.hitDamage = hit; w.projectileSpeed = speed; w.range = range; w.spreadDegrees = spread; w.fireRate = 1f; w.automatic = false;
                    w.projectileLength = length; w.projectileThickness = thickness; w.projectileColor = color; w.overWalls = 0f;
                    w.damage = damage; w.explodeAtEndOfRange = explode; w.recoilShake = 0f;
                });
            var stone = new Color32(130, 124, 112, 255);
            var slingStone = Shot("SlingStone", 12f, 14f, 6.5f, 4f, 5, 5, stone);
            var arrow = Shot("Arrow", 18f, 18f, 8f, 2.5f, 12, 2, new Color32(176, 140, 92, 255));
            var bolt = Shot("BallistaBolt", 45f, 22f, 10f, 1f, 20, 3, new Color32(150, 116, 74, 255));
            var catapultStone = Shot("CatapultStone", 30f, 10f, 12f, 2f, 9, 9, stone, rockImpact, true);
            var oil = Shot("BoilingOil", 20f, 7f, 4f, 3f, 8, 8, new Color32(220, 170, 60, 255), oilSplash, true);
            var quarrel = Shot("CrossbowBolt", 32f, 24f, 9f, 1.5f, 10, 2, new Color32(140, 110, 72, 255));
            var trebuchetStone = Shot("TrebuchetStone", 50f, 9f, 18f, 2f, 12, 12, stone, bigRock, true);
            var musketBall = Shot("MusketBall", 55f, 30f, 9f, 3f, 6, 3, new Color32(255, 220, 140, 255));
            var cannonball = Shot("Cannonball", 60f, 11f, 14f, 1.5f, 7, 7, new Color32(40, 40, 44, 255), cannonBlast, true);
            var gatlingBullet = Shot("GatlingBullet", 12f, 30f, 9f, 6f, 7, 2, new Color32(255, 214, 102, 255));
            var gunBullet = Shot("GunNestBullet", 14f, 34f, 9.5f, 4f, 8, 2, new Color32(255, 214, 102, 255));
            var shell = Shot("MortarShell", 40f, 9f, 16f, 2f, 7, 4, new Color32(70, 76, 62, 255), mortarBlast, true);
            var flame = Shot("Flame", 6f, 9f, 4.5f, 12f, 10, 6, new Color32(255, 140, 40, 255));
            var bigShell = Shot("HowitzerShell", 60f, 12f, 26f, 1.5f, 9, 5, new Color32(60, 64, 52, 255), howitzerBlast, true);

            DefenseConfig Defense(string file, Action<DefenseConfig> values) => Asset<DefenseConfig>("Defenses/" + file, values);
            DefenseConfig Trap(string file, float damage, float hold, bool one, float rearm) =>
                Defense(file, d => { d.kind = DefenseKind.Trap; d.damage = damage; d.holdSeconds = hold; d.catchesOne = one; d.rearmSeconds = rearm; });
            DefenseConfig Area(string file, float slowTo, float dps) => Defense(file, d => { d.kind = DefenseKind.Area; d.slowTo = slowTo; d.damagePerSecond = dps; });
            DefenseConfig Mine(string file, DamageType blast) => Defense(file, d => { d.kind = DefenseKind.Mine; d.blast = blast; d.triggerRadius = 0.45f; d.damage = blast.creatureDamage; });
            DefenseConfig Barrier(string file, float thorns) => Defense(file, d => { d.kind = DefenseKind.Barrier; d.thorns = thorns; });
            DefenseConfig Cloud(string file, float radius, float seconds, float dps, float rearm, Color color, bool glows) =>
                Defense(file, d => { d.kind = DefenseKind.Cloud; d.cloudRadius = radius; d.cloudSeconds = seconds; d.cloudDamagePerSecond = dps; d.rearmSeconds = rearm; d.cloudColor = color; d.cloudGlows = glows; });
            DefenseConfig Tower(string file, WeaponConfig weapon, float range, float perSecond, bool guard, float turn, int pierce = 0, bool lobbed = false, float minRange = 0f) =>
                Defense(file, d =>
                {
                    d.kind = DefenseKind.Tower; d.weapon = weapon; d.range = range; d.shotsPerSecond = perSecond; d.needsGuard = guard; d.turnSpeed = turn;
                    d.pierce = pierce; d.lobbed = lobbed; d.minRange = minRange;
                });

            BuildingConfig Item(string file, string displayName, string art, BuildCategory category, int size, int wood, int stone, int food,
                float seconds, float health, float height, bool blocks, bool drag, DefenseConfig defense, float light = 0f, float flameSize = 1f) =>
                Asset<BuildingConfig>("Building/" + file, b =>
                {
                    b.displayName = displayName; b.kind = BuildingKind.Furniture; b.category = category; b.art = art;
                    b.size = new Vector2Int(size, size); b.rotations = 1; b.woodCost = wood; b.stoneCost = stone; b.foodCost = food;
                    b.buildSeconds = seconds; b.health = health; b.height = height; b.blocksMovement = blocks; b.flat = false;
                    b.dragToPlaceLines = drag; b.roomTag = RoomTag.None; b.defense = defense;
                    if (light > 0f) { b.lightSize = light; b.flames = new[] { Vector2.zero }; b.flameSize = flameSize; }
                });
            const BuildCategory T = BuildCategory.Traps, W = BuildCategory.Towers;
            var gas = new Color(0.6f, 0.72f, 0.3f, 0.5f);
            var napalm = new Color(1f, 0.5f, 0.15f, 0.55f);
            return new[]
            {
                new List<BuildingConfig>   // Stone Age
                {
                    Item("Traps/SpikePit", "Spike pit", FurnitureArt.SpikePit, T, 1, 10, 0, 0, 6f, 100f, 0f, false, false, Trap("SpikePit", 40f, 6f, true, 20f)),
                    Item("Traps/StakeField", "Stake field", FurnitureArt.StakeField, T, 1, 12, 0, 0, 4f, 100f, 0.15f, false, true, Area("StakeField", 0.5f, 5f)),
                    Item("Traps/FirePit", "Fire pit", FurnitureArt.FirePit, T, 1, 15, 6, 0, 5f, 100f, 0f, false, false, Area("FirePit", 0.8f, 12f), light: 0.8f, flameSize: 2f),
                    Item("Towers/SlingPost", "Sling post", FurnitureArt.SlingPost, W, 1, 25, 10, 0, 8f, 250f, 0.45f, true, false, Tower("SlingPost", slingStone, 6f, 0.7f, true, 360f)),
                },
                new List<BuildingConfig>   // Bronze Age
                {
                    Item("Traps/SpikePlate", "Spike plate", FurnitureArt.SpikePlate, T, 1, 10, 15, 0, 5f, 100f, 0f, false, false, Trap("SpikePlate", 40f, 0f, false, 3f)),
                    Item("Traps/TarPit", "Tar pit", FurnitureArt.TarPit, T, 1, 10, 5, 5, 5f, 100f, 0f, false, true, Area("TarPit", 0.25f, 2f)),
                    Item("Traps/ThornHedge", "Thorn hedge", FurnitureArt.ThornHedge, T, 1, 6, 0, 4, 4f, 300f, 0.4f, true, true, Barrier("ThornHedge", 6f)),
                    Item("Towers/ArcherTower", "Archer tower", FurnitureArt.ArcherTower, W, 2, 80, 10, 0, 20f, 600f, 1.2f, true, false, Tower("ArcherTower", arrow, 7.5f, 1f, true, 360f)),
                },
                new List<BuildingConfig>   // Iron Age
                {
                    Item("Traps/Caltrops", "Caltrops", FurnitureArt.Caltrops, T, 1, 0, 12, 0, 3f, 100f, 0f, false, true, Area("Caltrops", 0.65f, 7f)),
                    Item("Traps/Abatis", "Abatis", FurnitureArt.Abatis, T, 1, 20, 0, 0, 5f, 500f, 0.3f, true, true, Barrier("Abatis", 10f)),
                    Item("Towers/Ballista", "Ballista", FurnitureArt.BallistaTower, W, 2, 120, 80, 0, 30f, 900f, 1.1f, true, false, Tower("Ballista", bolt, 9f, 0.4f, true, 120f, pierce: 3)),
                    Item("Towers/Catapult", "Catapult", FurnitureArt.Catapult, W, 2, 140, 40, 0, 30f, 700f, 0.6f, true, false, Tower("Catapult", catapultStone, 12f, 0.17f, true, 90f, lobbed: true, minRange: 3f)),
                },
                new List<BuildingConfig>   // Medieval
                {
                    Item("Traps/BearTrap", "Bear trap", FurnitureArt.BearTrap, T, 1, 5, 15, 0, 4f, 100f, 0f, false, false, Trap("BearTrap", 50f, 8f, true, 12f)),
                    Item("Towers/BoilingOil", "Boiling oil", FurnitureArt.BoilingOil, W, 1, 20, 40, 10, 12f, 500f, 0.6f, true, false, Tower("BoilingOil", oil, 3.5f, 0.1f, true, 360f, lobbed: true, minRange: 0.8f), light: 0.45f, flameSize: 1.4f),
                    Item("Towers/CrossbowTower", "Crossbow tower", FurnitureArt.CrossbowTower, W, 2, 40, 160, 0, 35f, 1300f, 1.3f, true, false, Tower("CrossbowTower", quarrel, 9f, 0.8f, true, 360f)),
                    Item("Towers/Trebuchet", "Trebuchet", FurnitureArt.Trebuchet, W, 2, 220, 60, 0, 45f, 900f, 1.2f, true, false, Tower("Trebuchet", trebuchetStone, 18f, 0.11f, true, 60f, lobbed: true, minRange: 5f)),
                },
                new List<BuildingConfig>   // Renaissance
                {
                    Item("Traps/PowderKeg", "Powder keg", FurnitureArt.PowderKeg, T, 1, 15, 10, 0, 4f, 100f, 0.35f, false, false, Mine("PowderKeg", powderBlast)),
                    Item("Traps/ChevalDeFrise", "Cheval de frise", FurnitureArt.ChevalDeFrise, T, 1, 25, 5, 0, 5f, 700f, 0.5f, true, true, Barrier("ChevalDeFrise", 14f)),
                    Item("Towers/MusketTower", "Musket tower", FurnitureArt.MusketTower, W, 2, 60, 200, 0, 40f, 1600f, 1.3f, true, false, Tower("MusketTower", musketBall, 9f, 0.45f, true, 360f)),
                    Item("Towers/CannonTower", "Cannon tower", FurnitureArt.CannonTower, W, 2, 40, 300, 0, 45f, 1400f, 1.2f, true, false, Tower("Cannon", cannonball, 13f, 0.2f, true, 90f, lobbed: true, minRange: 3f)),
                },
                new List<BuildingConfig>   // Industrial
                {
                    Item("Traps/SpringSnare", "Spring snare", FurnitureArt.SpringSnare, T, 1, 5, 15, 0, 4f, 100f, 0f, false, false, Trap("SpringSnare", 30f, 10f, true, 8f)),
                    Item("Traps/BarbedWire", "Barbed wire", FurnitureArt.BarbedWire, T, 1, 4, 10, 0, 3f, 100f, 0.2f, false, true, Area("BarbedWire", 0.3f, 6f)),
                    Item("Traps/Dynamite", "Dynamite tripwire", FurnitureArt.Dynamite, T, 1, 5, 20, 0, 4f, 100f, 0f, false, false, Mine("Dynamite", dynamiteBlast)),
                    Item("Towers/Gatling", "Gatling turret", FurnitureArt.Gatling, W, 2, 40, 180, 0, 35f, 1000f, 0.7f, true, false, Tower("Gatling", gatlingBullet, 9f, 6f, false, 220f)),
                },
                new List<BuildingConfig>   // World War 1
                {
                    Item("Traps/Landmine", "Landmine", FurnitureArt.Landmine, T, 1, 0, 20, 0, 3f, 100f, 0f, false, false, Mine("Landmine", mineBlast)),
                    Item("Traps/GasTrap", "Gas trap", FurnitureArt.GasTrap, T, 1, 0, 25, 10, 5f, 100f, 0f, false, false, Cloud("GasTrap", 2f, 8f, 12f, 30f, gas, false)),
                    Item("Traps/SandbagWall", "Sandbag wall", FurnitureArt.SandbagWall, T, 1, 0, 8, 6, 3f, 900f, 0.4f, true, true, Barrier("SandbagWall", 0f)),
                    Item("Towers/GunNest", "Machine-gun nest", FurnitureArt.GunNest, W, 2, 40, 120, 20, 30f, 800f, 0.5f, true, false, Tower("GunNest", gunBullet, 9.5f, 8f, false, 200f)),
                    Item("Towers/MortarPit", "Mortar pit", FurnitureArt.MortarPit, W, 2, 20, 160, 20, 30f, 800f, 0.4f, true, false, Tower("MortarPit", shell, 16f, 0.25f, false, 120f, lobbed: true, minRange: 4f)),
                },
                new List<BuildingConfig>   // World War 2
                {
                    Item("Traps/BouncingMine", "Bouncing mine", FurnitureArt.BouncingMine, T, 1, 0, 30, 0, 3f, 100f, 0f, false, false, Mine("BouncingMine", bouncingBlast)),
                    Item("Traps/NapalmTrap", "Napalm trap", FurnitureArt.NapalmTrap, T, 1, 0, 30, 20, 5f, 100f, 0f, false, false, Cloud("NapalmTrap", 1.6f, 10f, 25f, 40f, napalm, true)),
                    Item("Traps/Hedgehog", "Czech hedgehog", FurnitureArt.Hedgehog, T, 1, 0, 40, 0, 4f, 1500f, 0.5f, true, true, Barrier("Hedgehog", 0f)),
                    Item("Towers/FlameBunker", "Flamethrower bunker", FurnitureArt.FlameBunker, W, 2, 0, 320, 30, 40f, 2500f, 0.7f, true, false, Tower("FlameBunker", flame, 4.5f, 10f, false, 300f, pierce: 4)),
                    Item("Towers/Howitzer", "Howitzer", FurnitureArt.Howitzer, W, 2, 40, 400, 0, 50f, 1800f, 0.8f, true, false, Tower("Howitzer", bigShell, 26f, 0.14f, false, 40f, lobbed: true, minRange: 6f)),
                },
            };
        }

        // Roofs, one kind per age: dragged over a room like a floor, drawn as one roof for its shape.
        static List<BuildingConfig> Roofs()
        {
            BuildingConfig Roof(string file, string displayName, RoofStyle style, int wood, int stone, int food, float seconds) =>
                Asset<BuildingConfig>("Building/Floors/" + file, b =>
                {
                    b.displayName = displayName; b.kind = BuildingKind.Roof; b.category = BuildCategory.Floors; b.roofStyle = style;
                    b.size = Vector2Int.one; b.rotations = 1; b.woodCost = wood; b.stoneCost = stone; b.foodCost = food; b.buildSeconds = seconds;
                    b.dragToPlaceLines = true; b.blocksMovement = false; b.health = 0f; b.height = 0f; b.roomTag = RoomTag.None;
                });
            return new List<BuildingConfig>
            {
                Roof("ThatchRoof", "Thatch roof", RoofStyle.Thatch, 2, 0, 1, 1.5f),
                Roof("ShingleRoof", "Shingle roof", RoofStyle.WoodShingles, 3, 0, 0, 2f),
                Roof("ClayTileRoof", "Clay tile roof", RoofStyle.ClayTiles, 1, 3, 0, 2.5f),
                Roof("SlateRoof", "Slate roof", RoofStyle.Slate, 1, 4, 0, 3f),
                Roof("CopperRoof", "Copper roof", RoofStyle.Copper, 2, 5, 0, 3.5f),
                Roof("TinRoof", "Tin roof", RoofStyle.CorrugatedIron, 2, 4, 0, 2.5f),
                Roof("TarFeltRoof", "Tar felt roof", RoofStyle.TarFelt, 3, 2, 1, 2.5f),
                Roof("ConcreteRoof", "Concrete roof", RoofStyle.Concrete, 0, 7, 0, 4f),
            };
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

        // A big double gate, `width` tiles across, built into a wall like a door.
        static BuildingConfig Gate(string file, string displayName, string art, int width, int wood, int stone, float seconds, float health) =>
            Asset<BuildingConfig>("Building/Doors/" + file, b =>
            {
                b.displayName = displayName; b.kind = BuildingKind.Door; b.category = BuildCategory.Walls; b.art = art;
                b.size = new Vector2Int(width, 1); b.rotations = 2; b.woodCost = wood; b.stoneCost = stone; b.buildSeconds = seconds;
                b.health = health; b.height = 0.25f; b.blocksMovement = true; b.dragToPlaceLines = false; b.roomTag = RoomTag.None;
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
