using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame
{
    /// <summary>
    /// Makes textures and sprites in code. Smooth (bilinear) filtering with
    /// mipmaps, so the art stays smooth when it turns and when the throne view
    /// zooms far out.
    /// </summary>
    public static class PixelTextures
    {
        /// <summary>A transparent RGBA texture: smooth filtering, mipmaps, no tiling.</summary>
        public static Texture2D NewTexture(int width, int height, string name, bool mipmaps = true)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, mipmaps)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        /// <summary>
        /// A sprite covering the whole texture at the project's pixel density.
        /// The pivot is 0..1 (0,0 = bottom-left corner).
        /// </summary>
        public static Sprite NewSprite(Texture2D texture, Vector2 pivot)
        {
            var sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                pivot,
                PixelArt.PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return sprite;
        }

        /// <summary>Turns a picture drawn in code into a sprite (its own texture, pivot from the picture).</summary>
        public static Sprite ToSprite(Art.PixelImage image, string name, bool repeat = false)
        {
            image.BleedEdges();
            var texture = NewTexture(image.Width, image.Height, name);
            if (repeat) texture.wrapMode = TextureWrapMode.Repeat;
            texture.SetPixels32(image.Pixels);
            texture.Apply(true, true);
            return NewSprite(texture, image.Pivot);
        }
    }

    /// <summary>
    /// Packs many code-drawn pictures into one texture. Sprites that share a
    /// texture can be drawn together in one batch, which matters for the
    /// thousand-odd trees, rocks and bushes on the map.
    /// </summary>
    public sealed class SpriteAtlasBuilder
    {
        const int Padding = 4;
        readonly List<Art.PixelImage> images = new List<Art.PixelImage>();
        readonly List<string> names = new List<string>();

        /// <summary>Adds a picture and returns its number in the array <see cref="Build"/> returns.</summary>
        public int Add(Art.PixelImage image, string name)
        {
            images.Add(image);
            names.Add(name);
            return images.Count - 1;
        }

        public Sprite[] Build(string textureName)
        {
            // Shelf packing: tallest first, left to right, then a new row.
            var order = new List<int>();
            for (int i = 0; i < images.Count; i++) order.Add(i);
            order.Sort((a, b) => images[b].Height.CompareTo(images[a].Height));

            int size = 256;
            var positions = new Vector2Int[images.Count];
            while (!Fits(order, size, positions)) size *= 2;

            var atlas = new Art.PixelImage(size, size);
            for (int i = 0; i < images.Count; i++) atlas.Stamp(images[i], positions[i].x, positions[i].y);
            atlas.BleedEdges(Padding);

            var texture = PixelTextures.NewTexture(size, size, textureName);
            texture.SetPixels32(atlas.Pixels);
            texture.Apply(true, true);

            var sprites = new Sprite[images.Count];
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                sprites[i] = Sprite.Create(texture, new Rect(positions[i].x, positions[i].y, img.Width, img.Height),
                    img.Pivot, PixelArt.PixelsPerUnit, 0, SpriteMeshType.FullRect);
                sprites[i].name = names[i];
            }
            return sprites;
        }

        bool Fits(List<int> order, int size, Vector2Int[] positions)
        {
            int x = Padding, y = Padding, rowHeight = 0;
            foreach (int i in order)
            {
                var img = images[i];
                if (img.Width + Padding * 2 > size) return false;
                if (x + img.Width + Padding > size)
                {
                    x = Padding;
                    y += rowHeight + Padding;
                    rowHeight = 0;
                }
                if (y + img.Height + Padding > size) return false;
                positions[i] = new Vector2Int(x, y);
                x += img.Width + Padding;
                rowHeight = Mathf.Max(rowHeight, img.Height);
            }
            return true;
        }
    }
}
