// Compile-check stubs: ONLY the UnityEditor members the KingdomGame editor scripts
// use, with signatures copied from Unity 6.3's C# reference source
// (github.com/Unity-Technologies/UnityCsReference, branch 6000.3).
// Never shipped; used only to compile-check outside Unity.
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor
{
    public enum TextureImporterType { Default = 0, NormalMap = 1, GUI = 2, Sprite = 8, Cursor = 7, Cookie = 4, Lightmap = 6, DirectionalLightmap = 11, Shadowmask = 12, SingleChannel = 10 }
    public enum SpriteImportMode { None = 0, Single = 1, Multiple = 2, Polygon = 3 }
    public enum TextureImporterCompression { Uncompressed = 0, Compressed = 1, CompressedHQ = 2, CompressedLQ = 3 }
    public enum TextureImporterNPOTScale { None = 0, ToNearest = 1, ToLarger = 2, ToSmaller = 3 }
    public enum SerializationMode { Mixed = 0, ForceBinary = 1, ForceText = 2 }
    [Flags] public enum ImportAssetOptions { Default = 0, ForceUpdate = 1, ForceSynchronousImport = 8, ImportRecursive = 256, DontDownloadFromCacheServer = 8192, ForceUncompressedImport = 16384 }

    public class AssetImporter : Object
    {
        public string assetPath => null;
        public bool importSettingsMissing => false;
        public void SaveAndReimport() { }
        public static AssetImporter GetAtPath(string path) => null;
    }

    public sealed class TextureImporter : AssetImporter
    {
        public TextureImporterType textureType { get; set; }
        public SpriteImportMode spriteImportMode { get; set; }
        public float spritePixelsPerUnit { get; set; }
        public FilterMode filterMode { get; set; }
        public TextureImporterCompression textureCompression { get; set; }
        public bool mipmapEnabled { get; set; }
        public TextureWrapMode wrapMode { get; set; }
        public TextureImporterNPOTScale npotScale { get; set; }
        public int maxTextureSize { get; set; }
        public bool isReadable { get; set; }
        public bool sRGBTexture { get; set; }
        public bool alphaIsTransparency { get; set; }
    }

    public class AssetPostprocessor
    {
        public string assetPath { get; set; }
        public AssetImporter assetImporter => AssetImporter.GetAtPath(assetPath);
    }

    public sealed class MenuItem : Attribute { public MenuItem(string itemName) { } }

    public class EditorWindow : ScriptableObject
    {
        public static T GetWindow<T>(string title) where T : EditorWindow => null;
        public Vector2 minSize { get; set; }
        public void Repaint() { }
    }

    public sealed class EditorStyles
    {
        public static GUIStyle boldLabel => null;
        public static GUIStyle wordWrappedLabel => null;
        public static GUIStyle helpBox => null;
    }

    public sealed class EditorGUILayout
    {
        public static void LabelField(string label, GUIStyle style, params GUILayoutOption[] options) { }
        public static Rect BeginHorizontal(params GUILayoutOption[] options) => default;
        public static void EndHorizontal() { }
        public static Rect BeginVertical(GUIStyle style, params GUILayoutOption[] options) => default;
        public static void EndVertical() { }
        public static void Space() { }
        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options) => default;
        public static void EndScrollView() { }
    }

    public class EditorGUI
    {
        public struct DisabledScope : IDisposable
        {
            public DisabledScope(bool disabled) { }
            public void Dispose() { }
        }
    }

    public sealed class EditorApplication
    {
        public delegate void CallbackFunction();
        public static CallbackFunction delayCall;
        public static bool isPlaying { get; set; }
    }

    public class Undo
    {
        public static void RegisterCreatedObjectUndo(Object objectToUndo, string name) { }
        public static T AddComponent<T>(GameObject gameObject) where T : Component => null;
        public static void RecordObject(Object objectToUndo, string name) { }
    }

    public sealed class EditorUtility
    {
        public static void SetDirty(Object target) { }
        public static void DisplayProgressBar(string title, string info, float progress) { }
        public static void ClearProgressBar() { }
    }

    public sealed class AssetDatabase
    {
        public static string CreateFolder(string parentFolder, string newFolderName) => null;
        public static void StartAssetEditing() { }
        public static void StopAssetEditing() { }
        public static void ImportAsset(string path, ImportAssetOptions options) { }
        public static bool IsValidFolder(string path) => false;
        public static string GetAssetPath(Object assetObject) => null;
        public static T LoadAssetAtPath<T>(string assetPath) where T : Object => null;
        public static string GUIDToAssetPath(string guid) => null;
        public static void SaveAssets() { }
        public static string[] FindAssets(string filter, string[] searchInFolders) => null;
        public static void CreateAsset(Object asset, string path) { }
        public static void AddObjectToAsset(Object objectToAdd, Object assetObject) { }
        public static bool DeleteAsset(string path) => false;
        public static void Refresh() { }
    }

    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { }
        public string path { get; set; }
    }

    public sealed class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; }
    }

    public sealed class Selection
    {
        public static GameObject activeGameObject { get; set; }
    }

    public sealed class EditorSettings : Object
    {
        public static SerializationMode serializationMode { get; set; }
    }

    public sealed class VersionControlSettings : Object
    {
        public static string mode { get; set; }
    }

    public class SerializedObject : IDisposable
    {
        public SerializedObject(Object obj) { }
        public SerializedProperty FindProperty(string propertyPath) => null;
        public void Dispose() { }
    }

    public class SerializedProperty : IDisposable
    {
        public int intValue { get; set; }
        public bool isArray => false;
        public int arraySize { get; set; }
        public Object objectReferenceValue { get; set; }
        public SerializedProperty GetArrayElementAtIndex(int index) => null;
        public void Dispose() { }
    }
}

namespace UnityEditor.SceneManagement
{
    using UnityEngine.SceneManagement;

    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }

    public sealed class EditorSceneManager
    {
        public static bool MarkSceneDirty(Scene scene) => false;
        public static bool SaveScene(Scene scene) => false;
        public static bool SaveScene(Scene scene, string dstScenePath) => false;
        public static Scene NewScene(NewSceneSetup setup) => default;
        public static Scene OpenScene(string scenePath) => default;
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => false;
    }
}
