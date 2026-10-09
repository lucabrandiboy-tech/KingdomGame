using System;
using System.Collections.Generic;
using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KingdomGame.EditorTools
{
    /// <summary>
    /// Menu: KingdomGame > Build Game Scenes.
    /// Makes the settings assets in Assets/Data (kept if they already exist, so
    /// your tuning survives) and rebuilds two scenes: Kingdom (the game) and
    /// StressTest. Also removes the old Phase 1 test scenes and assets.
    /// </summary>
    public static class GameSetup
    {
        const string DataRoot = "Assets/Data";
        public const string KingdomScenePath = "Assets/Scenes/Kingdom.unity";
        public const string StressScenePath = "Assets/Scenes/StressTest.unity";

        // Made by the Phase 1 menu; replaced by the assets and scenes below.
        static readonly string[] Obsolete =
        {
            "Assets/Data/Phase1",
            "Assets/Scenes/Phase1_Destruction.unity",
            "Assets/Scenes/Phase1_StressTest.unity",
        };

        static readonly Color GroundColor = new Color32(58, 88, 48, 255);

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
            foreach (var area in new[] { "Destruction", "Combat", "Character", "Economy", "World" })
                Folder(DataRoot, area);
            Folder("Assets", "Scenes");

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
            });
            var c4Blast = Asset<DamageType>("Destruction/C4Explosion", d =>
            {
                d.category = DamageCategory.Explosive;
                d.radiusPixels = 64; d.centerDamage = 900; d.edgeDamage = 160; d.jaggedness = 100; d.noiseBlock = 4;
                d.debrisChance = 0.08f; d.debrisSpeed = new Vector2(3f, 10f); d.debrisLift = new Vector2(2f, 8f);
                d.sparks = 120; d.sparkColor = new Color32(255, 170, 70, 255); d.screenShake = 0.45f;
            });
            var smash = Asset<DamageType>("Destruction/BruteSmash", d =>
            {
                d.category = DamageCategory.Melee;
                d.radiusPixels = 20; d.centerDamage = 400; d.edgeDamage = 150; d.jaggedness = 170; d.noiseBlock = 6;
                d.debrisChance = 0.25f; d.debrisSpeed = new Vector2(1.5f, 5f); d.debrisLift = new Vector2(1.5f, 5f);
                d.screenShake = 0.12f;
            });

            var stone = Asset<StructureMaterial>("Destruction/StoneWall", m =>
            {
                m.pixelHealth = 255; m.bulletPercent = 3; m.meleePercent = 8; m.explosivePercent = 100;
                m.pattern = WallPattern.Bricks;
                m.gapColor = new Color32(54, 52, 58, 255);
                m.colors = new[]
                {
                    new Color32(128, 126, 122, 255), new Color32(114, 112, 110, 255),
                    new Color32(140, 136, 128, 255), new Color32(104, 104, 108, 255),
                };
            });
            var wood = Asset<StructureMaterial>("Destruction/WoodPalisade", m =>
            {
                m.pixelHealth = 140; m.bulletPercent = 8; m.meleePercent = 35; m.explosivePercent = 140;
                m.pattern = WallPattern.Planks;
                m.gapColor = new Color32(48, 32, 22, 255);
                m.colors = new[]
                {
                    new Color32(142, 96, 58, 255), new Color32(126, 84, 50, 255),
                    new Color32(156, 108, 66, 255), new Color32(118, 80, 48, 255),
                };
            });

            // ---- Combat
            var rifle = Asset<WeaponConfig>("Combat/Rifle", w =>
            {
                w.damage = bullet; w.fireRate = 8f; w.automatic = true; w.projectileSpeed = 30f; w.range = 14f;
                w.spreadDegrees = 2f; w.projectileLength = 8; w.projectileThickness = 2;
                w.projectileColor = new Color32(255, 214, 102, 255); w.recoilShake = 0.008f;
            });
            var rockets = Asset<WeaponConfig>("Combat/RocketLauncher", w =>
            {
                w.damage = rocketBlast; w.fireRate = 0.8f; w.automatic = false; w.projectileSpeed = 12f; w.range = 14f;
                w.spreadDegrees = 0.5f; w.explodeAtEndOfRange = true; w.projectileLength = 14; w.projectileThickness = 4;
                w.projectileColor = new Color32(232, 96, 52, 255); w.recoilShake = 0.06f;
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
            var stressConfig = Asset<StressTestConfig>("World/StressTestConfig", c =>
            {
                c.explosion = c4Blast;
                c.materials = new[] { stone, wood };
            });
            var content = Asset<GameContent>("GameContent", c =>
            {
                c.player = player; c.rifle = rifle; c.rocketLauncher = rockets; c.c4 = c4;
                c.stone = stone; c.wood = wood; c.tree = tree; c.rock = rock; c.bush = bush;
                c.world = worldConfig; c.dayNight = dayNight;
            });
            AssetDatabase.SaveAssets();

            var damageTypes = new[] { bullet, rocketBlast, c4Blast, smash };

            // Scene 1: the game.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var camera = MakeCamera();
            PixelCameraSetup.Configure(camera);
            var sun = MakeGlobalLight();
            MakeWorld(settings, damageTypes);
            var cycle = new GameObject("Day and Night").AddComponent<DayNightCycle>();
            cycle.Setup(dayNight, sun);
            var bootstrap = new GameObject("Kingdom").AddComponent<KingdomBootstrap>();
            bootstrap.Content = content;
            bootstrap.DayNight = cycle;
            EditorSceneManager.SaveScene(scene, KingdomScenePath);

            // Scene 2: stress test. No pixel-perfect camera here: it zooms out
            // far enough to show every wall, which matters more for a benchmark.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            camera = MakeCamera();
            camera.orthographicSize = 12f;
            MakeGlobalLight();
            MakeWorld(settings, damageTypes);
            new GameObject("Stress Test").AddComponent<StressTest>().Config = stressConfig;
            EditorSceneManager.SaveScene(scene, StressScenePath);

            RemoveObsolete();
            SetBuildScenes(KingdomScenePath, StressScenePath);
            EditorSceneManager.OpenScene(KingdomScenePath);
            Debug.Log("Game scenes are ready: Assets/Scenes/Kingdom (open now) and Assets/Scenes/StressTest. Press Play.");
        }

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

        static Camera MakeCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0, 0, -10);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = PixelArt.ReferenceHeight * 0.5f / PixelArt.PixelsPerUnit;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = GroundColor;
            go.AddComponent<AudioListener>();
            go.AddComponent<CameraRig>();
            return camera;
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

        static void SetBuildScenes(params string[] paths)
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var existing in EditorBuildSettings.scenes)
                if (Array.IndexOf(Obsolete, existing.path) < 0) scenes.Add(existing);
            foreach (var path in paths)
                if (!scenes.Exists(s => s.path == path)) scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
