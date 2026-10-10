using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// The ruler's weapons, one or two for each age, seen from above and pointing right
    /// like the rifle: drawn small, then scaled up smoothly with the grip in the middle
    /// of the picture (where the ruler's hand is). Lit from the top-left.
    /// </summary>
    public static class WeaponArt
    {
        public const string Club = "Club", Sling = "Sling", Bow = "Bow", Crossbow = "Crossbow", HandCannon = "HandCannon",
            Musket = "Musket", LeverRifle = "LeverRifle", BoltRifle = "BoltRifle", Smg = "Smg", Bazooka = "Bazooka";

        /// <summary>Every weapon picture's name.</summary>
        public static readonly string[] All = { Club, Sling, Bow, Crossbow, HandCannon, Musket, LeverRifle, BoltRifle, Smg, Bazooka };

        public static PixelImage Draw(string weapon, int ppu)
        {
            switch (weapon)
            {
                case Club: return Held(ClubSmall(), 4, ppu);
                case Sling: return Held(SlingSmall(), 3, ppu);
                case Bow: return Held(BowSmall(), 8, ppu);
                case Crossbow: return Held(CrossbowSmall(), 8, ppu);
                case HandCannon: return Held(HandCannonSmall(), 10, ppu);
                case Musket: return Held(MusketSmall(), 14, ppu);
                case LeverRifle: return Held(LeverRifleSmall(), 12, ppu);
                case BoltRifle: return Held(BoltRifleSmall(), 13, ppu);
                case Smg: return Held(SmgSmall(), 11, ppu);
                case Bazooka: return Held(BazookaSmall(), 16, ppu);
                default: return Held(ClubSmall(), 4, ppu);
            }
        }

        // Scales a small drawing up smoothly (like CharacterArt.Rifle), padded so x = gripX is in the middle.
        static PixelImage Held(PixelImage small, int gripX, int ppu)
        {
            float s = ppu / 64f * CharacterArt.K;
            int halfWidth = Mathf.CeilToInt(Mathf.Max(gripX, small.Width - gripX) * s) + 2;
            int w = halfWidth * 2, h = Mathf.CeilToInt(small.Height * s) + 2;
            var img = new PixelImage(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sx = (x + 0.5f - halfWidth) / s + gripX, sy = (y + 0.5f - h / 2f) / s + small.Height / 2f;
                    img.Set(x, y, CharacterArt.Bilinear(small, sx - 0.5f, sy - 0.5f));
                }
            return img;
        }

        static readonly Color32 Wood = PixelImage.Rgb(104, 70, 42), DarkWood = PixelImage.Rgb(70, 46, 28), Metal = PixelImage.Rgb(54, 56, 64),
            Bronze = PixelImage.Rgb(166, 112, 52), Leather = PixelImage.Rgb(92, 62, 40), Olive = PixelImage.Rgb(82, 92, 58);

        // A rod along x from x0 to x1 at row y, `thick` rows, lit from above (the top row light, the bottom dark).
        static void Rod(PixelImage img, int x0, int x1, int y, int thick, Color32 c)
        {
            for (int x = x0; x <= x1; x++)
                for (int k = 0; k < thick; k++)
                {
                    float shade = thick == 1 ? 1f : Mathf.Lerp(0.75f, 1.3f, k / (float)(thick - 1));
                    img.Set(x, y + k, PixelImage.Scale(c, shade));
                }
        }

        static PixelImage ClubSmall()
        {
            var img = new PixelImage(25, 11);
            Rod(img, 1, 10, 4, 3, Wood);                                  // handle
            for (int x = 1; x <= 3; x++) img.Set(x, 5, PixelImage.Scale(Leather, 1.1f));   // grip wrap
            for (int x = 8; x <= 23; x++)                                  // the head: swelling from the handle, rounded at the end
            {
                float u = (x + 0.5f - 8f) / 16f;
                float r = Mathf.Lerp(1.4f, 4.6f, PixelImage.Smooth(0f, 0.7f, u));
                if (u > 0.7f) r *= Mathf.Sqrt(Mathf.Max(0f, 1f - (u - 0.7f) / 0.3f * ((u - 0.7f) / 0.3f)));
                for (int y = 0; y < 11; y++)
                {
                    float d = Mathf.Abs(y + 0.5f - 5.5f);
                    float cover = Mathf.Clamp01(r - d + 0.5f);
                    if (cover <= 0f) continue;
                    float knot = Noise.Hash01(91u, x, y) < 0.12f ? 0.7f : 1f;
                    img.Blend(x, y, PixelImage.Scale(Wood, (0.85f + (y + 0.5f - 5.5f) / Mathf.Max(r, 0.5f) * 0.3f) * knot), cover);
                }
            }
            return img;
        }

        static PixelImage SlingSmall()
        {
            var img = new PixelImage(18, 9);
            for (int x = 1; x <= 12; x++)                                  // the two cords, apart towards the pouch
            {
                int spread = Mathf.RoundToInt((x - 1) / 11f * 2f);
                img.Set(x, 4 - spread, PixelImage.Scale(Leather, 1.15f));
                img.Set(x, 4 + spread, PixelImage.Scale(Leather, 0.9f));
            }
            img.Ellipse(14.5f, 4.5f, 2.6f, 3.2f, Leather);                 // the pouch, with a stone in it
            img.Ellipse(14.5f, 4.8f, 1.6f, 1.6f, PixelImage.Rgb(140, 134, 122));
            img.Set(14, 5, PixelImage.Rgb(186, 180, 168));
            return img;
        }

        static PixelImage BowSmall()
        {
            var img = new PixelImage(26, 23);
            for (int y = 1; y <= 21; y++)                                  // the bow, bending forward in the middle
            {
                float t = (y - 11f) / 10f;
                int x = Mathf.RoundToInt(13f - t * t * 5f);
                img.Set(x, y, PixelImage.Scale(Wood, 1.15f));
                img.Set(x - 1, y, PixelImage.Scale(Wood, 0.8f));
                img.Set(x, y, y >= 10 && y <= 12 ? PixelImage.Scale(Leather, 1.1f) : img.Get(x, y));
            }
            for (int y = 1; y <= 21; y++) img.Blend(Mathf.RoundToInt(Mathf.Lerp(8f, 7f, Mathf.Abs(y - 11f) / 10f)), y, PixelImage.Rgb(210, 200, 170), 0.8f);   // string
            Rod(img, 4, 24, 11, 1, PixelImage.Rgb(150, 116, 74));          // the arrow
            img.Set(24, 11, Metal); img.Set(25, 11, PixelImage.Scale(Metal, 0.8f));
            img.Set(4, 10, PixelImage.Rgb(200, 60, 50)); img.Set(4, 12, PixelImage.Rgb(200, 60, 50)); img.Set(5, 10, PixelImage.Rgb(220, 220, 210)); img.Set(5, 12, PixelImage.Rgb(220, 220, 210));
            return img;
        }

        static PixelImage CrossbowSmall()
        {
            var img = new PixelImage(28, 19);
            Rod(img, 1, 20, 8, 3, Wood);                                   // the stock
            for (int y = 1; y <= 17; y++)                                  // the bow across the front
            {
                float t = (y - 9f) / 8f;
                int x = Mathf.RoundToInt(21f + (1f - t * t) * 2f);
                img.Set(x, y, PixelImage.Scale(Metal, 1.2f));
                img.Set(x - 1, y, PixelImage.Scale(Metal, 0.85f));
            }
            for (int y = 2; y <= 16; y++) img.Blend(Mathf.RoundToInt(Mathf.Lerp(14f, 20f, Mathf.Abs(y - 9f) / 7f)), y, PixelImage.Rgb(210, 200, 170), 0.7f);
            Rod(img, 10, 26, 9, 1, PixelImage.Rgb(140, 110, 72));          // the bolt
            img.Set(26, 9, Metal); img.Set(27, 9, Metal);
            return img;
        }

        static PixelImage HandCannonSmall()
        {
            var img = new PixelImage(30, 9);
            Rod(img, 1, 18, 3, 3, DarkWood);                               // the pole
            Rod(img, 17, 28, 2, 5, Bronze);                                // the bronze tube, wide at the mouth
            for (int y = 1; y <= 7; y++) img.Set(28, y, PixelImage.Scale(Bronze, y < 4 ? 1.3f : 0.75f));
            img.Set(28, 4, PixelImage.Rgb(30, 26, 22));
            img.Set(19, 6, PixelImage.Rgb(255, 140, 60));                   // the glowing touch-hole match
            return img;
        }

        static PixelImage MusketSmall()
        {
            var img = new PixelImage(44, 9);
            for (int x = 1; x <= 14; x++)                                  // the stock, wider at the butt
                Rod(img, x, x, x < 6 ? 2 : 3, x < 6 ? 5 : 3, Wood);
            Rod(img, 15, 30, 3, 3, Wood);                                  // the fore-end
            Rod(img, 15, 42, 4, 1, Metal);                                 // the long barrel
            img.Set(16, 5, PixelImage.Scale(Metal, 1.3f)); img.Set(17, 5, PixelImage.Scale(Metal, 1.3f));   // the lock
            return img;
        }

        static PixelImage LeverRifleSmall()
        {
            var img = new PixelImage(38, 11);
            for (int x = 1; x <= 11; x++) Rod(img, x, x, x < 5 ? 3 : 4, x < 5 ? 5 : 3, Wood);
            Rod(img, 12, 19, 4, 3, PixelImage.Rgb(170, 140, 70));          // the brass receiver
            for (int x = 12; x <= 16; x++) img.Set(x, 2, PixelImage.Scale(Metal, 0.9f));    // the lever loop
            img.Set(12, 3, Metal); img.Set(16, 3, Metal);
            Rod(img, 20, 27, 4, 3, Wood);
            Rod(img, 20, 36, 5, 1, Metal);                                 // the barrel
            Rod(img, 20, 33, 4, 1, PixelImage.Scale(Metal, 0.75f));        // the magazine tube under it
            return img;
        }

        static PixelImage BoltRifleSmall()
        {
            var img = new PixelImage(42, 11);
            for (int x = 1; x <= 12; x++) Rod(img, x, x, x < 5 ? 3 : 4, x < 5 ? 5 : 3, DarkWood);
            Rod(img, 13, 32, 4, 3, DarkWood);                              // the long wooden fore-end
            Rod(img, 13, 20, 5, 1, Metal);                                 // the receiver
            img.Set(16, 6, Metal); img.Set(16, 7, Metal); img.Ellipse(16.5f, 8.2f, 1f, 1f, PixelImage.Scale(Metal, 1.3f));   // the bolt handle
            Rod(img, 21, 40, 5, 1, Metal);                                 // the barrel
            return img;
        }

        static PixelImage SmgSmall()
        {
            var img = new PixelImage(32, 15);
            for (int x = 1; x <= 9; x++) Rod(img, x, x, 6, 3, Wood);       // the stock
            Rod(img, 10, 22, 6, 3, Metal);                                 // the receiver
            img.Ellipse(15.5f, 11f, 3.4f, 3.4f, PixelImage.Scale(Metal, 0.85f));   // the drum magazine, out to the side
            img.Ellipse(15f, 11.6f, 1.4f, 1.4f, PixelImage.Scale(Metal, 1.25f));
            Rod(img, 13, 16, 3, 2, Wood);                                  // the front grip
            Rod(img, 23, 30, 7, 1, PixelImage.Scale(Metal, 1.1f));         // the barrel, with fins
            for (int x = 23; x <= 28; x += 2) { img.Set(x, 6, PixelImage.Scale(Metal, 0.8f)); img.Set(x, 8, PixelImage.Scale(Metal, 1.2f)); }
            return img;
        }

        static PixelImage BazookaSmall()
        {
            var img = new PixelImage(40, 11);
            Rod(img, 1, 38, 3, 5, Olive);                                  // the tube
            for (int y = 2; y <= 8; y++) { img.Set(38, y, PixelImage.Scale(Olive, y < 5 ? 1.3f : 0.7f)); img.Set(1, y, PixelImage.Scale(Olive, 0.8f)); }
            img.Set(38, 5, PixelImage.Rgb(24, 26, 20));
            Rod(img, 14, 17, 8, 2, DarkWood);                              // the grip and shoulder rest
            Rod(img, 20, 22, 1, 2, Metal);                                 // the sight
            return img;
        }
    }
}
