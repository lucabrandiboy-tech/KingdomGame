using System;
using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KingdomGame.EditorTools
{
    /// <summary>
    /// Menu: KingdomGame > Phase 1 > Create Test Scenes.
    /// Makes the Phase 1 settings assets (in Assets/Data/Phase1) and two scenes:
    /// Phase1_Destruction (walk around and shoot walls) and Phase1_StressTest.
    /// Existing assets are kept, so your tuning survives running it again;
    /// the two scenes are rebuilt.
    /// </summary>
    public static class Phase1Setup
    {
        const string DataFolder = "Assets/Data/Phase1";
        const string SceneFolder = "Assets/Scenes";
        public const string SandboxScenePath = SceneFolder + "/Phase1_Destruction.unity";
        public const string StressScenePath = SceneFolder + "/Phase1_StressTest.unity";

        static readonly Color GroundColor = new Color32(58, 70, 50, 255);

        [MenuItem("KingdomGame/Phase 1/Create Test Scenes")]
        static void CreateTestScenes()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Stop Play mode first, then run KingdomGame > Phase 1 > Create Test Scenes again.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Phase1");
            EnsureFolder("Assets", "Scenes");

            var settings = Asset<DestructionSettings>("DestructionSettings", s => { });

            var bullet = Asset<DamageType>("Bullet", d =>
            {
                d.radiusPixels = 3; d.centerDamage = 160; d.edgeDamage = 40; d.jaggedness = 120; d.noiseBlock = 1;
                d.debrisChance = 0.8f; d.debrisSpeed = new Vector2(1f, 3.5f); d.debrisLift = new Vector2(1f, 3f);
                d.sparks = 3; d.sparkColor = new Color32(255, 230, 140, 255);
                d.screenShake = 0.01f;
            });
            var explosion = Asset<DamageType>("Explosion", d =>
            {
                d.radiusPixels = 22; d.centerDamage = 255; d.edgeDamage = 60; d.jaggedness = 110; d.noiseBlock = 2;
                d.debrisChance = 0.35f; d.debrisSpeed = new Vector2(3f, 9f); d.debrisLift = new Vector2(2f, 7f);
                d.sparks = 60; d.sparkColor = new Color32(255, 150, 60, 255);
                d.screenShake = 0.2f;
            });
            var smash = Asset<DamageType>("BruteSmash", d =>
            {
                d.radiusPixels = 11; d.centerDamage = 220; d.edgeDamage = 90; d.jaggedness = 170; d.noiseBlock = 4;
                d.debrisChance = 0.6f; d.debrisSpeed = new Vector2(1.5f, 5f); d.debrisLift = new Vector2(1.5f, 5f);
                d.screenShake = 0.12f;
            });

            var stone = Asset<StructureMaterial>("StoneWall", m =>
            {
                m.pixelHealth = 120;
                m.pattern = WallPattern.Bricks;
                m.gapColor = new Color32(54, 52, 58, 255);
                m.colors = new[]
                {
                    new Color32(128, 126, 122, 255), new Color32(114, 112, 110, 255),
                    new Color32(140, 136, 128, 255), new Color32(104, 104, 108, 255),
                };
            });
            var wood = Asset<StructureMaterial>("WoodPalisade", m =>
            {
                m.pixelHealth = 50;
                m.pattern = WallPattern.Planks;
                m.gapColor = new Color32(48, 32, 22, 255);
                m.colors = new[]
                {
                    new Color32(142, 96, 58, 255), new Color32(126, 84, 50, 255),
                    new Color32(156, 108, 66, 255), new Color32(118, 80, 48, 255),
                };
            });

            var rifle = Asset<WeaponConfig>("Rifle", w =>
            {
                w.damage = bullet; w.fireRate = 9f; w.automatic = true; w.projectileSpeed = 30f; w.range = 16f;
                w.spreadDegrees = 2.5f; w.projectileLength = 4; w.projectileThickness = 1;
                w.projectileColor = new Color32(255, 214, 102, 255); w.recoilShake = 0.008f;
            });
            var rockets = Asset<WeaponConfig>("RocketLauncher", w =>
            {
                w.damage = explosion; w.fireRate = 1.5f; w.automatic = false; w.projectileSpeed = 14f; w.range = 14f;
                w.spreadDegrees = 0.5f; w.explodeAtEndOfRange = true; w.projectileLength = 6; w.projectileThickness = 2;
                w.projectileColor = new Color32(232, 96, 52, 255); w.recoilShake = 0.05f;
            });

            var player = Asset<PlayerConfig>("PlayerConfig", p => { });
            var content = Asset<Phase1Content>("Phase1Content", c =>
            {
                c.player = player; c.rifle = rifle; c.rocketLauncher = rockets; c.smash = smash; c.stone = stone; c.wood = wood;
            });
            var stress = Asset<StressTestConfig>("StressTestConfig", c =>
            {
                c.explosion = explosion;
                c.materials = new[] { stone, wood };
            });
            AssetDatabase.SaveAssets();

            var damageTypes = new[] { bullet, explosion, smash };

            // Scene 1: walk around and shoot walls.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            MakeCamera(PixelArt.ReferenceWidth, PixelArt.ReferenceHeight);
            MakeGlobalLight();
            MakeWorld(settings, damageTypes);
            new GameObject("Phase 1 Sandbox").AddComponent<DestructionSandbox>().Content = content;
            EditorSceneManager.SaveScene(scene, SandboxScenePath);

            // Scene 2: stress test. The camera shows twice as much (1280 x 720
            // game pixels) so the whole field of walls fits on screen.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            MakeCamera(PixelArt.ReferenceWidth * 2, PixelArt.ReferenceHeight * 2);
            MakeGlobalLight();
            MakeWorld(settings, damageTypes);
            new GameObject("Stress Test").AddComponent<StressTest>().Config = stress;
            EditorSceneManager.SaveScene(scene, StressScenePath);

            AddToBuildSettings(SandboxScenePath, StressScenePath);
            EditorSceneManager.OpenScene(SandboxScenePath);
            Debug.Log("Phase 1 scenes are ready: Assets/Scenes/Phase1_Destruction and Phase1_StressTest. " +
                      "Phase1_Destruction is open now. Press Play.");
        }

        // Loads the asset if it already exists (keeping your changes), otherwise
        // creates it with these starting values.
        static T Asset<T>(string assetName, Action<T> startingValues) where T : ScriptableObject
        {
            string path = $"{DataFolder}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<T>();
            startingValues(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void EnsureFolder(string parent, string folder)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{folder}"))
                AssetDatabase.CreateFolder(parent, folder);
        }

        static void MakeCamera(int referenceWidth, int referenceHeight)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(0, 0, -10);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = referenceHeight * 0.5f / PixelArt.PixelsPerUnit;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = GroundColor;
            go.AddComponent<AudioListener>();
            PixelCameraSetup.Configure(camera, referenceWidth, referenceHeight);
            go.AddComponent<CameraRig>();
        }

        static void MakeGlobalLight()
        {
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            var layers = SortingLayer.layers;
            var ids = new int[layers.Length];
            for (int i = 0; i < layers.Length; i++) ids[i] = layers[i].id;
            light.targetSortingLayers = ids;
        }

        static void MakeWorld(DestructionSettings settings, DamageType[] damageTypes)
        {
            new GameObject("Pixel World").AddComponent<PixelWorld>().Setup(settings, damageTypes);
        }

        static void AddToBuildSettings(params string[] paths)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in paths)
                if (!scenes.Exists(s => s.path == path))
                    scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
