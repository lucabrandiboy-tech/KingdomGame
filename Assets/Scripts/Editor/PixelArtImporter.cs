using System;
using UnityEditor;
using UnityEngine;

namespace KingdomGame.EditorTools
{
    /// <summary>
    /// Gives every new image under Assets/Art/ crisp pixel-art import settings:
    /// sprite, 32 pixels per unit, no blur (Point filter), no compression, no
    /// mipmaps. Without this, Unity imports art blurry and at 100 pixels per unit.
    ///
    /// It only runs the first time an image is imported (when it has no .meta
    /// file yet), so you can still change one sprite's settings by hand later.
    /// The Phase 0 Setup Check window can re-apply it to everything in Assets/Art.
    /// </summary>
    public sealed class PixelArtImporter : AssetPostprocessor
    {
        public const string ArtFolder = "Assets/Art/";

        // Big enough that a sprite sheet is never silently shrunk (Unity's default
        // limit is 2048, and shrinking pixel art ruins it).
        const int MaxTextureSize = 8192;

        void OnPreprocessTexture()
        {
            if (!IsInArtFolder(assetPath) || !assetImporter.importSettingsMissing)
                return;

            Apply((TextureImporter)assetImporter);
        }

        public static bool IsInArtFolder(string path) =>
            path.StartsWith(ArtFolder, StringComparison.OrdinalIgnoreCase);

        public static void Apply(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            if (importer.spriteImportMode == SpriteImportMode.None)
                importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelArt.PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
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
            if (importer.filterMode != FilterMode.Point) return "Filter Mode is not Point (it will look blurry)";
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                return "Compression is on (it smears pixel colors)";
            if (importer.mipmapEnabled) return "Mipmaps are on";
            return null;
        }
    }
}
