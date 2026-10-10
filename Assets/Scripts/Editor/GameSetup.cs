using System;
using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.Villagers;
using KingdomGame.World;
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
    /// two scenes: Kingdom (the game) and StressTest. Also bakes the art
    /// (<see cref="ArtBaker"/>, only when the art changed) and removes the old
    /// Phase 1 test scenes and assets.
    /// </summary>
    public static class GameSetup
    {
        const string DataRoot = "Assets/Data";

        /// <summary>
        /// Bumped when settings made by an older version of this menu should get
        /// new values (it normally keeps them, so your tuning survives).
        /// 1: the high-detail look (evening start, fires, softer night, wall colors, color grading).
        /// </summary>
        const int SettingsVersion = 1;

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
            foreach (var area in new[] { "Destruction", "Combat", "Character", "Economy", "World", "Building", "Villagers", "Look" })
                Folder(DataRoot, area);
            Folder("Assets", "Scenes");
            Folder("Assets", "Art");

            // Settings made by an older version of this menu get the new look
            // (only how things look: gameplay numbers are kept).
            var oldContent = AssetDatabase.LoadAssetAtPath<GameContent>(DataRoot + "/GameContent.asset");
            bool newLook = oldContent != null && oldContent.settingsVersion < SettingsVersion;

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
                StoneLook(m);
            });
            var wood = Asset<StructureMaterial>("Destruction/WoodPalisade", m =>
            {
                m.pixelHealth = 140; m.bulletPercent = 8; m.meleePercent = 35; m.explosivePercent = 140;
                WoodLook(m);
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
                b.displayName = "Stone wall"; b.kind = BuildingKind.Wall; b.size = Vector2Int.one;
                b.stoneCost = 10; b.buildSeconds = 4f; b.wallMaterial = stone; b.dragToPlaceLines = true;
            });
            var woodWallBuilding = Asset<BuildingConfig>("Building/WoodWall", b =>
            {
                b.displayName = "Wood wall"; b.kind = BuildingKind.Wall; b.size = Vector2Int.one;
                b.woodCost = 8; b.buildSeconds = 3f; b.wallMaterial = wood; b.dragToPlaceLines = true;
            });
            var storehouse = Asset<BuildingConfig>("Building/Storehouse", b =>
            {
                b.displayName = "Storehouse"; b.kind = BuildingKind.Storage; b.size = new Vector2Int(2, 2);
                b.woodCost = 60; b.stoneCost = 20; b.buildSeconds = 15f; b.storageCapacity = 1000; b.dragToPlaceLines = false;
            });
            var kingdomConfig = Asset<KingdomConfig>("Building/KingdomConfig", k => { });
            var villagerConfig = Asset<VillagerConfig>("Villagers/VillagerConfig", v => { });

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
            if (content.buildMenu == null || content.buildMenu.Length == 0)
                content.buildMenu = new[] { stoneWallBuilding, woodWallBuilding, storehouse };

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

            var damageTypes = new[] { bullet, rocketBlast, c4Blast, smash };

            // Scene 1: the game.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            var camera = MakeCamera(view);
            var sun = MakeGlobalLight();
            MakeVolume(postProcessing);
            MakeWorld(settings, damageTypes);
            var cycle = new GameObject("Day and Night").AddComponent<DayNightCycle>();
            cycle.Setup(dayNight, sun);
            var bootstrap = new GameObject("Kingdom").AddComponent<KingdomBootstrap>();
            bootstrap.Content = content;
            bootstrap.DayNight = cycle;
            EditorSceneManager.SaveScene(scene, KingdomScenePath);

            // Scene 2: stress test, zoomed out far enough to show every wall.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            camera = MakeCamera(view);
            camera.orthographicSize = 12f;
            MakeGlobalLight();
            MakeVolume(postProcessing);
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
