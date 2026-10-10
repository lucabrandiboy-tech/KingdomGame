using System;
using UnityEditor;
using UnityEngine;

namespace KingdomGame.EditorTools
{
    /// <summary>
    /// Gives every new image under Assets/Art/ the project's import settings:
    /// sprite, 64 pixels per unit, smooth (bilinear) filtering with mipmaps (so
    /// art stays smooth when it turns or the throne view zooms far out), no
    /// compression. Without this, Unity imports art at 100 pixels per unit.
    ///
    /// It only runs the first time an image is imported (when it has no .meta
    /// file yet), so you can still change one sprite's settings by hand later.
    /// The Phase 0 Setup Check window can re-apply it to everything in Assets/Art.
    /// (The name is from Phase 0, when the game used crisp pixel art.)
    ///
    /// The baked art sheets in Assets/Art/Generated (<see cref="ArtBaker"/>) are
    /// different: they always get <see cref="ApplyBaked"/>, because the game cuts
    /// its sprites out of them in code.
    /// </summary>
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/Art/";
        public const string BakedFolder = ArtBaker.Folder + "/";

        // Big enough that a sprite sheet is never silently shrunk (Unity's default
        // limit is 2048).
        const int MaxTextureSize = 8192;

        void OnPreprocessTexture()
        {
            if (IsBaked(assetPath))
            {
                ApplyBaked((TextureImporter)assetImporter, assetPath);
                return;
            }
            if (!IsInArtFolder(assetPath) || !assetImporter.importSettingsMissing)
                return;

            Apply((TextureImporter)assetImporter);
        }

        public static bool IsInArtFolder(string path) =>
            path.StartsWith(ArtFolder, StringComparison.OrdinalIgnoreCase);

        public static bool IsBaked(string path) =>
            path.StartsWith(BakedFolder, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// A baked sheet: a plain texture (not a sprite) with mipmaps and smooth
        /// blending between them (so it stays sharp but never shimmers as the view
        /// zooms), compressed in high quality to save video memory. Sheets ending
        /// in _n are normal maps (which way each pixel faces, for the lights).
        /// Ground sheets repeat across the map.
        /// </summary>
        public static void ApplyBaked(TextureImporter importer, string path)
        {
            bool normals = path.EndsWith("_n.png", StringComparison.OrdinalIgnoreCase);
            string sheet = System.IO.Path.GetFileNameWithoutExtension(path);
            if (normals) sheet = sheet.Substring(0, sheet.Length - 2);
            importer.textureType = normals ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normals;
            importer.alphaIsTransparency = !normals;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = Art.ArtCatalog.Repeats(sheet) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.maxTextureSize = MaxTextureSize;
        }

        public static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            if (importer.spriteImportMode == SpriteImportMode.None)
                importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelArt.PixelsPerUnit;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            if (importer.maxTextureSize < MaxTextureSize)
                importer.maxTextureSize = MaxTextureSize;
        }

        /// <summary>The first setting that differs from <see cref="Apply"/>, or null if all match.</summary>
        public static string FirstMismatch(TextureImporter importer)
        {
            if (importer.textureType != TextureImporterType.Sprite) return "Texture Type is not Sprite";
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, PixelArt.PixelsPerUnit))
                return $"Pixels Per Unit is {importer.spritePixelsPerUnit}, not {PixelArt.PixelsPerUnit}";
            if (importer.filterMode != FilterMode.Bilinear) return "Filter Mode is not Bilinear (edges will look jagged when it turns)";
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                return "Compression is on (it smears colors)";
            if (!importer.mipmapEnabled) return "Mipmaps are off (it will shimmer when zoomed out)";
            return null;
        }
    }
}
