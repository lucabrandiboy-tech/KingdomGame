using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KingdomGame.Art;
using KingdomGame.Destruction;
using KingdomGame.World;
using UnityEditor;
using UnityEngine;

namespace KingdomGame.EditorTools
{
    /// <summary>
    /// Bakes the code-drawn art (<see cref="ArtCatalog"/>) at full detail (the View
    /// Config's Art Detail, in pixels per tile) into texture sheets in
    /// Assets/Art/Generated, with normal maps, and records where every picture sits
    /// in Assets/Data/Look/ArtLibrary. Run by Build Game Scenes; it only bakes again
    /// when the art code (its version), the detail or the wall colors changed.
    /// The baked files aren't saved to Git (they're made again from the code).
    /// </summary>
    public static class ArtBaker
    {
        public const string Folder = "Assets/Art/Generated";
        const string LibraryPath = "Assets/Data/Look/ArtLibrary.asset";

        [MenuItem("KingdomGame/Bake Art Again")]
        static void BakeAgainMenu()
        {
            var content = AssetDatabase.LoadAssetAtPath<GameContent>("Assets/Data/GameContent.asset");
            if (content == null)
            {
                Debug.LogWarning("Run KingdomGame > Build Game Scenes first.");
                return;
            }
            content.art = Bake(content, true);
            EditorUtility.SetDirty(content);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Bakes the art if needed and returns the library.</summary>
        public static ArtLibrary Bake(GameContent content, bool force)
        {
            var walls = new List<WallLook>();
            foreach (var material in content.WallMaterials()) walls.Add(WallTiles.LookOf(material));
            int ppu = content.view != null ? Mathf.Clamp(content.view.artDetail, 128, 1024) : ArtCatalog.DefaultPixelsPerTile;
            string stamp = Stamp(walls, ppu);

            var library = AssetDatabase.LoadAssetAtPath<ArtLibrary>(LibraryPath);
            if (!force && library != null && library.stamp == stamp && Complete(library))
                return library;

            if (!AssetDatabase.IsValidFolder("Assets/Art")) AssetDatabase.CreateFolder("Assets", "Art");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Art", "Generated");

            var started = DateTime.Now;
            var baked = new List<(ArtSheet sheet, string albedo, string normals)>();
            float progress = 0f;
            try
            {
                // Draw and save one sheet at a time (each is let go once it's saved).
                string title = $"Baking the art at {ppu} pixels per tile (a few minutes, only when the art changed)";
                ArtSheet.Pack(ArtCatalog.Items(walls), ppu, sheet =>
                {
                    EditorUtility.DisplayProgressBar(title, $"Saving {sheet.Name}", progress);
                    string albedo = $"{Folder}/{sheet.Name}.png";
                    Save(sheet.Albedo, albedo);
                    string normals = null;
                    if (sheet.Normals != null)
                    {
                        normals = $"{Folder}/{sheet.Name}_n.png";
                        Save(sheet.Normals, normals);
                    }
                    sheet.Albedo = sheet.Normals = null;
                    baked.Add((sheet, albedo, normals));
                    GC.Collect();   // hand the big sheet's memory back before drawing the next one
                }, (key, done) =>
                {
                    progress = done * 0.85f;
                    EditorUtility.DisplayProgressBar(title, key, progress);
                });
                RemoveOldSheets(baked);

                // Unity imports the new files now (PixelArtImporter gives them their settings;
                // compressing the big sheets takes a moment).
                EditorUtility.DisplayProgressBar(title, "Importing the textures", 0.9f);
                AssetDatabase.Refresh();

                if (library == null)
                {
                    library = ScriptableObject.CreateInstance<ArtLibrary>();
                    AssetDatabase.CreateAsset(library, LibraryPath);
                }
                var sheetInfos = new List<ArtLibrary.Sheet>();
                var entries = new List<ArtLibrary.Entry>();
                for (int i = 0; i < baked.Count; i++)
                {
                    var (sheet, albedo, normals) = baked[i];
                    sheetInfos.Add(new ArtLibrary.Sheet
                    {
                        name = sheet.Name,
                        albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedo),
                        normals = normals != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(normals) : null,
                        hasNormals = normals != null,
                    });
                    foreach (var e in sheet.Entries)
                        entries.Add(new ArtLibrary.Entry
                        {
                            key = e.Item.Key,
                            sheet = i,
                            rect = new RectInt(e.X, e.Y, e.Width, e.Height),
                            pivot = e.Pivot,
                            pixelsPerTile = e.PixelsPerTile,
                        });
                }
                library.sheets = sheetInfos.ToArray();
                library.entries = entries.ToArray();
                library.stamp = Complete(library) ? stamp : "";   // if a texture didn't import, bake again next time
                EditorUtility.SetDirty(library);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            if (!Complete(library))
                Debug.LogError($"Some baked art textures in {Folder} didn't import, so those pictures are drawn in less detail. Please send Claude any errors above this one.");
            Debug.Log($"Baked the art: {baked.Count} texture sheets in {Folder}, {(DateTime.Now - started).TotalSeconds:0} seconds.");
            return library;
        }

        // Deletes sheets from an older bake that this one didn't make (for example a renamed wall material).
        static void RemoveOldSheets(List<(ArtSheet sheet, string albedo, string normals)> baked)
        {
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in baked)
            {
                keep.Add(b.albedo);
                if (b.normals != null) keep.Add(b.normals);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!keep.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        static void Save(PixelImage image, string assetPath)
        {
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
            texture.SetPixels32(image.Pixels);
            texture.Apply(false);
            File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static bool Complete(ArtLibrary library)
        {
            if (library.sheets == null || library.sheets.Length == 0) return false;
            foreach (var s in library.sheets)
                if (s == null || s.albedo == null || (s.hasNormals && s.normals == null)) return false;
            return true;
        }

        // Changes whenever the art code's version, the detail or anything about the walls' looks changes.
        static string Stamp(List<WallLook> walls, int ppu)
        {
            var text = new StringBuilder();
            text.Append("v").Append(ArtCatalog.Version).Append(" ppu").Append(ppu);
            foreach (var w in walls)
            {
                text.Append('|').Append(w.Name).Append(w.Logs ? 'L' : 'S').Append(w.Variants).Append(':').Append(Hex(w.Gap));
                if (w.Colors != null) foreach (var c in w.Colors) text.Append(Hex(c));
            }
            return text.ToString();
        }

        static string Hex(Color32 c) => $"{c.r:X2}{c.g:X2}{c.b:X2}";
    }
}
