using UnityEngine;

namespace KingdomGame
{
    /// <summary>
    /// Placeholder sprites drawn in code for Phase 1 (the ruler, the gun and
    /// projectiles). Real pixel art replaces them later.
    /// </summary>
    public static class SpriteFactory
    {
        /// <summary>The ruler seen from above: crown, head, shoulders and cape. 16 x 16 pixels.</summary>
        public static Sprite Ruler() => PixelTextures.FromRows("Ruler", new[]
        {
            "................",
            "......y.y.y.....",
            ".....yyyyyyy....",
            ".....yrrrrry....",
            "....ssssssss....",
            "....sssssssss...",
            "...bbssssssbbb..",
            "..bbbbbbbbbbbbb.",
            "..bbbbBBBBbbbbb.",
            "..bbbBBBBBBbbbb.",
            "..bbbBBBBBBbbbb.",
            "..bbbbBBBBbbbbb.",
            "..bbbbbbbbbbbbb.",
            "...bbbbbbbbbbb..",
            "....ddddddddd...",
            "................",
        }, "yrsbBd", new[]
        {
            new Color32(232, 190, 58, 255),  // y gold crown
            new Color32(196, 52, 60, 255),   // r ruby
            new Color32(224, 172, 132, 255), // s skin
            new Color32(54, 74, 158, 255),   // b royal blue cape
            new Color32(78, 102, 196, 255),  // B lighter cape
            new Color32(32, 40, 92, 255),    // d cape hem
        }, new Vector2(0.5f, 0.5f));

        /// <summary>A rifle pointing right; the sprite turns around its handle.</summary>
        public static Sprite Gun() => PixelTextures.FromRows("Gun", new[]
        {
            ".hhhhhhhhhhhh",
            "kkkkkkkkkkkkk",
            "kkkw.........",
        }, "hkw", new[]
        {
            new Color32(120, 124, 136, 255), // h highlight
            new Color32(52, 54, 64, 255),    // k body
            new Color32(110, 76, 48, 255),   // w wooden grip
        }, new Vector2(0.1f, 0.5f));

        /// <summary>A projectile pointing right, <paramref name="length"/> pixels long.</summary>
        public static Sprite Projectile(string name, int length, int thickness, Color32 color)
        {
            length = Mathf.Max(1, length);
            thickness = Mathf.Max(1, thickness);
            var texture = PixelTextures.NewTexture(length, thickness, name);
            var pixels = new Color32[length * thickness];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            // Brighter tip so you can tell which way it flies.
            for (int y = 0; y < thickness; y++)
                pixels[y * length + length - 1] = new Color32(255, 255, 230, 255);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return PixelTextures.NewSprite(texture, new Vector2(0.5f, 0.5f));
        }
    }
}
