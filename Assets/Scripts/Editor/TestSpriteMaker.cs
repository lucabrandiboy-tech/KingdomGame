using UnityEngine;

namespace KingdomGame.EditorTools
{
    /// <summary>
    /// Draws a 64 x 64 stone-brick wall (2 x 2 grid tiles) in code, for testing
    /// that pixel art imports and renders crisply. It is also the first picture
    /// committed to the repo, so it doubles as the Git LFS test.
    /// </summary>
    public static class TestSpriteMaker
    {
        public const int Size = PixelArt.PixelsPerUnit * 2;

        const int BrickWidth = 16; // includes the 1-pixel mortar line
        const int BrickHeight = 8;

        static readonly Color32 Mortar = new Color32(52, 46, 56, 255);
        static readonly Color32[] Bricks =
        {
            new Color32(150, 82, 62, 255),
            new Color32(136, 72, 56, 255),
            new Color32(164, 94, 70, 255),
            new Color32(124, 68, 58, 255),
        };

        /// <summary>
        /// Pixels in Texture2D order: index 0 is the bottom-left corner, rows go up.
        /// </summary>
        public static Color32[] Pixels()
        {
            var pixels = new Color32[Size * Size];
            for (int row = 0; row < Size; row++) // row 0 = top of the picture
            {
                int course = row / BrickHeight;
                int yInBrick = row % BrickHeight;
                int shift = (course % 2) * (BrickWidth / 2); // stagger every other course

                for (int x = 0; x < Size; x++)
                {
                    int xs = x + shift;
                    int xInBrick = xs % BrickWidth;
                    int brick = xs / BrickWidth;

                    Color32 c;
                    if (yInBrick == BrickHeight - 1 || xInBrick == BrickWidth - 1)
                    {
                        c = Mortar;
                    }
                    else
                    {
                        c = Bricks[Hash(brick, course) % Bricks.Length];
                        if (yInBrick == 0 || xInBrick == 0) c = Shade(c, 26); // lit top/left edge
                        else if (yInBrick == BrickHeight - 2 || xInBrick == BrickWidth - 2) c = Shade(c, -24); // shadow
                        else if (Hash(x, row) % 9 == 0) c = Shade(c, -12); // speckle
                    }

                    pixels[(Size - 1 - row) * Size + x] = c;
                }
            }
            return pixels;
        }

        public static byte[] Png()
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(Pixels());
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        static Color32 Shade(Color32 c, int amount) => new Color32(
            (byte)Mathf.Clamp(c.r + amount, 0, 255),
            (byte)Mathf.Clamp(c.g + amount, 0, 255),
            (byte)Mathf.Clamp(c.b + amount, 0, 255),
            c.a);

        // Small deterministic hash so the wall looks the same every time.
        static int Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (int)(h & 0x7fffffff);
            }
        }
    }
}
