using UnityEngine;

namespace KingdomGame
{
    /// <summary>Makes textures and sprites in code with crisp pixel-art settings.</summary>
    public static class PixelTextures
    {
        /// <summary>A transparent RGBA texture: no blur, no mipmaps, no tiling.</summary>
        public static Texture2D NewTexture(int width, int height, string name)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        /// <summary>
        /// A sprite covering the whole texture at the project's pixel density.
        /// The pivot is 0..1 (0,0 = bottom-left corner).
        /// </summary>
        public static Sprite NewSprite(Texture2D texture, Vector2 pivot)
        {
            var sprite = UnityEngine.Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                pivot,
                PixelArt.PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return sprite;
        }

        /// <summary>A texture and sprite from rows of text, for tiny code-drawn sprites.</summary>
        /// <param name="rows">One string per pixel row, top row first. Each character
        /// is looked up in <paramref name="palette"/>; '.' is transparent.</param>
        public static Sprite FromRows(string name, string[] rows, string paletteKeys, Color32[] palette, Vector2 pivot)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var pixels = new Color32[width * height];
            for (int row = 0; row < height; row++)
            {
                for (int x = 0; x < width; x++)
                {
                    char key = x < rows[row].Length ? rows[row][x] : '.';
                    int index = paletteKeys.IndexOf(key);
                    pixels[(height - 1 - row) * width + x] = index >= 0 ? palette[index] : new Color32(0, 0, 0, 0);
                }
            }
            var texture = NewTexture(width, height, name);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return NewSprite(texture, pivot);
        }
    }
}
