using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace KingdomGame.EditorTools
{
    /// <summary>
    /// Menu: KingdomGame > Phase 0 Setup Check.
    /// Checks everything Phase 0 is supposed to set up and offers a Fix button
    /// for each problem it can fix by itself.
    /// </summary>
    public sealed class SetupCheckWindow : EditorWindow
    {
        enum Status { Pass, Warn, Fail }

        sealed class Check
        {
            public string Title;
            public Status Status;
            public string Detail;
            public string FixLabel;
            public Action Fix;
        }

        const string TestSpriteFolder = "Assets/Art/Test";
        const string TestSpritePath = TestSpriteFolder + "/TestBrickWall.png";
        const string TestWallName = "Test Brick Wall";
        const string VisibleMetaFiles = "Visible Meta Files";

        readonly List<Check> checks = new List<Check>();
        Vector2 scroll;
        GUIStyle textStyle;

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [MenuItem("KingdomGame/Phase 0 Setup Check")]
        static void Open()
        {
            var window = GetWindow<SetupCheckWindow>("Setup Check");
            window.minSize = new Vector2(420, 360);
            window.Run();
        }

        // Deferred so the checks never touch the AssetDatabase while Unity is
        // still importing after a script reload.
        void OnEnable() => Later(null);
        void OnFocus() => Run();

        void Run()
        {
            checks.Clear();
            checks.Add(CheckUnityVersion());
            checks.Add(CheckRepoFiles());
            checks.Add(CheckGitLfs());
            checks.Add(CheckTextSerialization());
            checks.Add(CheckVisibleMetaFiles());

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            checks.Add(CheckUrp(urp));
            if (urp != null)
            {
                checks.Add(Check2DRenderer(urp));
                checks.Add(CheckMsaa(urp));
            }

            checks.Add(CheckInputSystem());
            checks.Add(CheckPixelCamera());
            checks.Add(CheckTestWall());
            checks.Add(CheckArtImport());
            Repaint();
        }

        // ---------------------------------------------------------------- UI --

        void OnGUI()
        {
            if (textStyle == null)
                textStyle = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = true };

            EditorGUILayout.LabelField("Phase 0 setup check", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "<color=#4caf50>PASS</color> done   <color=#e0b030>WARN</color> works, but worth fixing   " +
                "<color=#e05050>FAIL</color> fix before Phase 1", textStyle);

            // Changes made in Play mode are thrown away when it stops.
            bool canFix = !EditorApplication.isPlaying;
            if (!canFix)
                EditorGUILayout.LabelField("<b>Stop Play mode to use the Fix buttons.</b>", textStyle);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Run checks again")) Later(null);
            using (new EditorGUI.DisabledScope(!canFix || !checks.Exists(c => c.Fix != null)))
            {
                if (GUILayout.Button("Fix everything I can")) Later(FixAll);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var check in checks)
                DrawCheck(check, canFix);
            EditorGUILayout.EndScrollView();

            int fails = checks.FindAll(c => c.Status == Status.Fail).Count;
            int warns = checks.FindAll(c => c.Status == Status.Warn).Count;
            string summary = fails == 0 && warns == 0
                ? "<b>Everything passed. Phase 0 is set up.</b> Commit and push in GitHub Desktop."
                : $"<b>{fails} to fix, {warns} warning(s).</b> See docs/PHASE-0-SETUP.md if a fix isn't clear.";
            EditorGUILayout.LabelField(summary, textStyle);
        }

        void DrawCheck(Check check, bool canFix)
        {
            string label;
            switch (check.Status)
            {
                case Status.Pass: label = "<color=#4caf50><b>PASS</b></color>"; break;
                case Status.Warn: label = "<color=#e0b030><b>WARN</b></color>"; break;
                default: label = "<color=#e05050><b>FAIL</b></color>"; break;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"{label}  <b>{check.Title}</b>", textStyle);
            if (!string.IsNullOrEmpty(check.Detail))
                EditorGUILayout.LabelField(check.Detail, textStyle);
            if (check.Fix != null)
            {
                using (new EditorGUI.DisabledScope(!canFix))
                {
                    if (GUILayout.Button(check.FixLabel ?? "Fix", GUILayout.ExpandWidth(false)))
                        Later(check.Fix);
                }
            }
            EditorGUILayout.EndVertical();
        }

        // Fixes change assets and scenes, and re-running the checks changes what
        // the window draws. Neither may happen halfway through drawing the window,
        // so both run right after this frame.
        void Later(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                try { action?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                if (this != null) Run();
            };
        }

        void FixAll()
        {
            foreach (var check in checks.ToArray())
            {
                if (check.Fix == null) continue;
                try { check.Fix(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        static Check Pass(string title, string detail = null) =>
            new Check { Title = title, Status = Status.Pass, Detail = detail };

        static Check Problem(Status status, string title, string detail, string fixLabel = null, Action fix = null) =>
            new Check { Title = title, Status = status, Detail = detail, FixLabel = fixLabel, Fix = fix };

        // ------------------------------------------------------------ checks --

        static Check CheckUnityVersion()
        {
            const string title = "Unity 6.3 LTS";
            string version = Application.unityVersion; // for example "6000.3.5f1"
            string[] parts = version.Split('.');
            if (parts.Length < 2 || !int.TryParse(parts[0], out int major) || !int.TryParse(parts[1], out int minor))
                return Problem(Status.Warn, title, $"Couldn't read the Unity version \"{version}\".");

            if (major > 6000 || (major == 6000 && minor >= 3))
                return Pass(title, $"Running Unity {version}.");
            if (major == 6000)
                return Problem(Status.Warn, title,
                    $"Running Unity {version}. It works, but the project targets Unity 6.3 LTS " +
                    "(longer support). Install it in Unity Hub > Installs.");
            return Problem(Status.Fail, title,
                $"Running Unity {version}. This project needs Unity 6.3 LTS (6000.3.x). Install it in Unity Hub > Installs.");
        }

        static Check CheckRepoFiles()
        {
            const string title = "Project is inside the KingdomGame Git repo";
            string root = ProjectRoot;
            bool hasGit = Directory.Exists(Path.Combine(root, ".git")) || File.Exists(Path.Combine(root, ".git"));
            if (!hasGit)
                return Problem(Status.Fail, title,
                    $"There's no .git folder in {root}. Open the folder GitHub Desktop cloned instead " +
                    "(Unity Hub > Add > Add project from disk). See setup step 4.");

            string ignore = ReadOrEmpty(Path.Combine(root, ".gitignore"));
            string attributes = ReadOrEmpty(Path.Combine(root, ".gitattributes"));
            if (!ignore.Contains("[Ll]ibrary"))
                return Problem(Status.Fail, title,
                    ".gitignore is missing from the project folder, so Git would upload Unity's huge Library folder.");
            if (!attributes.Contains("filter=lfs"))
                return Problem(Status.Fail, title,
                    ".gitattributes is missing from the project folder, so art and audio wouldn't use Git LFS.");
            return Pass(title, root);
        }

        static Check CheckGitLfs()
        {
            const string title = "Git LFS is switched on for this repo";
            string hook = Path.Combine(ProjectRoot, ".git", "hooks", "pre-push");
            if (ReadOrEmpty(hook).Contains("git lfs"))
                return Pass(title, "Pictures and sounds will upload to Git LFS when you push.");
            return Problem(Status.Warn, title,
                "Couldn't confirm it from here. If GitHub Desktop offers \"Initialize Git LFS\", click it. " +
                "After your first push, open Assets/Art/Test/TestBrickWall.png on github.com: it should say " +
                "\"Stored with Git LFS\".");
        }

        static Check CheckTextSerialization()
        {
            const string title = "Unity saves scenes and assets as text";
            if (EditorSettings.serializationMode == SerializationMode.ForceText)
                return Pass(title);
            return Problem(Status.Fail, title,
                "Binary scenes can't be compared or merged in Git.",
                "Switch to Force Text", () => EditorSettings.serializationMode = SerializationMode.ForceText);
        }

        static Check CheckVisibleMetaFiles()
        {
            const string title = "Visible .meta files";
            if (VersionControlSettings.mode == VisibleMetaFiles)
                return Pass(title, "Every asset's .meta file must be committed with it.");
            return Problem(Status.Fail, title,
                $"Version control mode is \"{VersionControlSettings.mode}\". Git needs the .meta files to be visible.",
                "Switch to Visible Meta Files", () => VersionControlSettings.mode = VisibleMetaFiles);
        }

        static Check CheckUrp(UniversalRenderPipelineAsset urp)
        {
            const string title = "Universal Render Pipeline (URP) is active";
            if (urp != null)
                return Pass(title, AssetDatabase.GetAssetPath(urp));
            return Problem(Status.Fail, title,
                "This project doesn't use URP. Create it from Unity Hub's \"Universal 2D\" template (setup step 3).");
        }

        static Check Check2DRenderer(UniversalRenderPipelineAsset urp)
        {
            const string title = "URP uses the 2D Renderer (2D lights and shadows)";
            var asset = new SerializedObject(urp);
            var list = asset.FindProperty("m_RendererDataList");
            var indexProperty = asset.FindProperty("m_DefaultRendererIndex");
            int index = indexProperty != null ? indexProperty.intValue : 0;
            UnityEngine.Object renderer = list != null && list.isArray && index >= 0 && index < list.arraySize
                ? list.GetArrayElementAtIndex(index).objectReferenceValue
                : null;

            if (renderer is Renderer2DData)
                return Pass(title, AssetDatabase.GetAssetPath(renderer));
            string found = renderer != null ? renderer.GetType().Name : "nothing";
            return Problem(Status.Fail, title,
                $"The default renderer is {found}. Create the project from the \"Universal 2D\" template (setup step 3).");
        }

        static Check CheckMsaa(UniversalRenderPipelineAsset urp)
        {
            const string title = "Anti-aliasing (MSAA) is off";
            if (urp.msaaSampleCount <= 1)
                return Pass(title);
            return Problem(Status.Warn, title,
                $"MSAA is {urp.msaaSampleCount}x. It does nothing useful for pixel art and costs performance.",
                "Turn MSAA off", () =>
                {
                    Undo.RecordObject(urp, "Turn MSAA off");
                    urp.msaaSampleCount = 1;
                    EditorUtility.SetDirty(urp);
                    AssetDatabase.SaveAssets();
                });
        }

        static Check CheckInputSystem()
        {
            const string title = "Input System package is active";
            bool installed = Type.GetType("UnityEngine.InputSystem.InputSystem, Unity.InputSystem") != null;
#if ENABLE_INPUT_SYSTEM
            bool active = true;
#else
            bool active = false;
#endif
            if (installed && active)
                return Pass(title, "Phase 1's keyboard and mouse controls use it.");
            if (!installed)
                return Problem(Status.Fail, title,
                    "Install it: Window > Package Manager > Unity Registry > Input System > Install.");
            return Problem(Status.Fail, title,
                "Edit > Project Settings > Player > Other Settings > Active Input Handling: choose " +
                "\"Input System Package (New)\". Unity restarts.");
        }

        static Check CheckPixelCamera()
        {
            const string title = "Main Camera has a Pixel Perfect Camera";
            if (!SavedSceneIsOpen())
                return Problem(Status.Fail, title, OpenSampleScene);

            var camera = Camera.main;
            if (camera == null)
                return Problem(Status.Fail, title,
                    "No camera tagged MainCamera in the open scene. Open Assets/Scenes/SampleScene and run the checks again.");

            var pixel = camera.GetComponent<PixelPerfectCamera>();
            string problem =
                pixel == null ? "There's no Pixel Perfect Camera component." :
                !camera.orthographic ? "The camera isn't orthographic." :
                pixel.assetsPPU != PixelArt.PixelsPerUnit ? $"Assets PPU is {pixel.assetsPPU}, not {PixelArt.PixelsPerUnit}." :
                pixel.refResolutionX != PixelArt.ReferenceWidth || pixel.refResolutionY != PixelArt.ReferenceHeight
                    ? $"Reference Resolution is {pixel.refResolutionX} x {pixel.refResolutionY}, not {PixelArt.ReferenceWidth} x {PixelArt.ReferenceHeight}." :
                pixel.gridSnapping != PixelPerfectCamera.GridSnapping.UpscaleRenderTexture
                    ? "Grid Snapping isn't Upscale Render Texture." :
                null;

            if (problem == null)
                return Pass(title,
                    $"{PixelArt.PixelsPerUnit} pixels per unit, {PixelArt.ReferenceWidth} x {PixelArt.ReferenceHeight} reference resolution.");
            return Problem(Status.Fail, title, problem, "Set up Main Camera (saves the scene)", () => ConfigureCamera(camera));
        }

        static Check CheckTestWall()
        {
            const string title = "Test brick wall is in the scene";
            if (!SavedSceneIsOpen())
                return Problem(Status.Fail, title, OpenSampleScene);
            if (FindTestWall() != null)
                return Pass(title, "Press Play: it should look sharp, with square pixels, not blurry.");
            return Problem(Status.Warn, title,
                "A small picture drawn in code, to test that pixel art shows up crisp.",
                "Place test wall (saves the scene)", PlaceTestWall);
        }

        static Check CheckArtImport()
        {
            const string title = "Pixel art import settings (Assets/Art)";
            if (!AssetDatabase.IsValidFolder("Assets/Art"))
                return Problem(Status.Warn, title,
                    "The Assets/Art folder is missing. Placing the test wall creates it.");

            var wrong = new List<TextureImporter>();
            string firstProblem = null;
            int total = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                total++;
                string mismatch = PixelArtImporter.FirstMismatch(importer);
                if (mismatch == null) continue;
                wrong.Add(importer);
                if (firstProblem == null) firstProblem = $"{path}: {mismatch}";
            }

            if (total == 0)
                return Problem(Status.Warn, title, "No images in Assets/Art yet. Placing the test wall creates one.");
            if (wrong.Count == 0)
                return Pass(title, $"{total} image(s) OK.");
            return Problem(Status.Fail, title,
                $"{wrong.Count} of {total} image(s) have the wrong settings. First one: {firstProblem}",
                "Fix import settings", () => Reimport(wrong));
        }

        // ------------------------------------------------------------- fixes --

        static void ConfigureCamera(Camera camera)
        {
            if (camera == null) return;
            Undo.RecordObject(camera, "Set up pixel camera");
            camera.orthographic = true;

            var pixel = camera.GetComponent<PixelPerfectCamera>();
            if (pixel == null)
                pixel = Undo.AddComponent<PixelPerfectCamera>(camera.gameObject);
            else
                Undo.RecordObject(pixel, "Set up pixel camera");

            pixel.assetsPPU = PixelArt.PixelsPerUnit;
            pixel.refResolutionX = PixelArt.ReferenceWidth;
            pixel.refResolutionY = PixelArt.ReferenceHeight;
            pixel.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            pixel.cropFrame = PixelPerfectCamera.CropFrame.None;
            EditorUtility.SetDirty(pixel);

            SaveScene(camera.gameObject.scene);
        }

        static void Reimport(List<TextureImporter> importers)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var importer in importers)
                {
                    PixelArtImporter.Apply(importer);
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        static void PlaceTestWall()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(TestSpritePath) == null)
                CreateTestSprite();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TestSpritePath);
            if (sprite == null)
            {
                Debug.LogError($"Unity didn't import {TestSpritePath} as a sprite. " +
                               "Select it in the Project window and check its Texture Type in the Inspector.");
                return;
            }
            if (!SavedSceneIsOpen() || FindTestWall() != null)
                return;

            var wall = new GameObject(TestWallName);
            wall.AddComponent<SpriteRenderer>().sprite = sprite;
            Undo.RegisterCreatedObjectUndo(wall, "Place test wall");
            Selection.activeGameObject = wall;
            SaveScene(wall.scene);
        }

        // Writes the PNG and lets Unity import it. PixelArtImporter gives it the
        // pixel-art settings because it is new (it has no .meta file yet).
        static void CreateTestSprite()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Art"))
                AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder(TestSpriteFolder))
                AssetDatabase.CreateFolder("Assets/Art", "Test");

            File.WriteAllBytes(Path.Combine(ProjectRoot, TestSpritePath), TestSpriteMaker.Png());
            AssetDatabase.ImportAsset(TestSpritePath, ImportAssetOptions.ForceUpdate);
        }

        const string OpenSampleScene =
            "Open a saved scene first: in the Project window, Assets > Scenes > double-click SampleScene. " +
            "Then run the checks again.";

        static bool SavedSceneIsOpen() => !string.IsNullOrEmpty(SceneManager.GetActiveScene().path);

        static GameObject FindTestWall()
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == TestWallName)
                    return root;
            return null;
        }

        static void SaveScene(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            if (!string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene);
        }

        static string ReadOrEmpty(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
