using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// The ruler, the rifle, C4 and projectiles, drawn in code at 64 pixels per
    /// tile. Placeholder art until real pixel art is made, but detailed enough to
    /// play with.
    /// </summary>
    public static class CharacterArt
    {
        // Palette. Lowercase = base color, uppercase = its shadow.
        const string Keys = "oyYwrbsSefhHcCdkFtTugpPnN";
        static readonly Color32[] Palette =
        {
            PixelImage.Rgb(34, 26, 40),    // o outline
            PixelImage.Rgb(240, 196, 64),  // y gold
            PixelImage.Rgb(186, 136, 40),  // Y gold shadow
            PixelImage.Rgb(255, 238, 160), // w gold shine
            PixelImage.Rgb(214, 48, 60),   // r ruby
            PixelImage.Rgb(80, 140, 236),  // b sapphire
            PixelImage.Rgb(238, 190, 150), // s skin
            PixelImage.Rgb(206, 148, 112), // S skin shadow
            PixelImage.Rgb(36, 28, 44),    // e eye
            PixelImage.Rgb(244, 240, 232), // f ermine fur / eye shine
            PixelImage.Rgb(150, 100, 62),  // h hair and beard
            PixelImage.Rgb(106, 68, 42),   // H hair shadow
            PixelImage.Rgb(178, 40, 54),   // c cape
            PixelImage.Rgb(124, 26, 42),   // C cape shadow
            PixelImage.Rgb(214, 70, 78),   // d cape shine
            PixelImage.Rgb(44, 40, 50),    // k ermine spot
            PixelImage.Rgb(198, 194, 188), // F fur shadow
            PixelImage.Rgb(66, 90, 186),   // t tunic
            PixelImage.Rgb(42, 58, 130),   // T tunic shadow
            PixelImage.Rgb(104, 130, 220), // u tunic shine
            PixelImage.Rgb(126, 84, 44),   // g belt
            PixelImage.Rgb(78, 66, 92),    // p trousers
            PixelImage.Rgb(54, 46, 68),    // P trousers shadow
            PixelImage.Rgb(110, 72, 44),   // n boots
            PixelImage.Rgb(70, 46, 30),    // N boots shadow
        };

        // Left half of the ruler, seen from the front. Mirrored to make the whole body.
        static readonly string[] RulerHalf =
        {
            "...........",
            ".....o....o",
            "....oyo..oy",
            "....oyyooyr",
            "....oywyyyr",
            "....oyyyyyy",
            "....oYbYYYY",
            "...ohhsssss",
            "...ohssssss",
            "...ohsefsss",
            "...ohseesss",
            "...ohsssssS",
            "...ohhsssss",
            "....ohhhhhh",
            "....ohhhhhh",
            ".....ohhhhh",
            "...offkfffF",
            "..offffffff",
            "..ocouttttt",
            "..ocoutttty",
            "..ocouttyyy",
            "..ocoutttty",
            "..ocouttttt",
            "..ocogggggy",
            "..ocsuttttt",
            "..ocouttttt",
            "..odcoutttt",
            "..odcoutttt",
            "..odcooTTTT",
            "..odcco....",
            "..odcco....",
            "...odco....",
            "...odco....",
            "....oo.....",
            "...........",
            "...........",
            "...........",
            "...........",
            "...........",
        };

        public const int RulerWidth = 22;

        /// <summary>
        /// Four walking frames: standing, left foot forward, standing, right foot
        /// forward. The pivot is between the feet.
        /// </summary>
        public static PixelImage[] RulerFrames()
        {
            return new[] { Ruler(4, 4), Ruler(5, 3), Ruler(4, 4), Ruler(3, 5) };
        }

        static PixelImage Ruler(int leftLeg, int rightLeg)
        {
            int height = RulerHalf.Length;
            var rows = new char[height][];
            for (int r = 0; r < height; r++)
            {
                string half = RulerHalf[r];
                rows[r] = new char[RulerWidth];
                for (int i = 0; i < half.Length; i++)
                {
                    rows[r][i] = half[i];
                    rows[r][RulerWidth - 1 - i] = half[i];
                }
            }
            Leg(rows, 6, leftLeg);
            Leg(rows, 11, rightLeg);
            ShadeRightEdge(rows);

            var text = new string[height];
            for (int r = 0; r < height; r++) text[r] = new string(rows[r]);
            var image = new PixelImage(RulerWidth, height) { Pivot = new Vector2(0.5f, 2f / height) };
            image.DrawRows(text, 0, height - 1, Keys, Palette);
            return image;
        }

        // A 3-pixel trouser leg with a boot, starting under the tunic.
        static void Leg(char[][] rows, int x0, int length)
        {
            const int top = 29;
            for (int i = 0; i < length; i++)
                Paint(rows, x0, top + i, "oppPo");
            int y = top + length;
            Paint(rows, x0, y, "onnno");
            Paint(rows, x0 - 1, y + 1, "oNNNNo");
            Paint(rows, x0 - 1, y + 2, "oooooo");
        }

        static void Paint(char[][] rows, int x, int y, string pixels)
        {
            if (y < 0 || y >= rows.Length) return;
            for (int i = 0; i < pixels.Length; i++)
                if (x + i >= 0 && x + i < rows[y].Length) rows[y][x + i] = pixels[i];
        }

        // Light comes from the top-left, so the right edge of each row is a shade darker.
        static void ShadeRightEdge(char[][] rows)
        {
            const string from = "tucdsywhfpn";
            const string to = "TtCcSYyHFPN";
            foreach (var row in rows)
            {
                int right = -1;
                for (int i = 0; i < row.Length; i++) if (row[i] != '.') right = i;
                for (int i = RulerWidth / 2 + 1; i < right; i++)
                {
                    int k = from.IndexOf(row[i]);
                    if (k >= 0 && i >= right - 3) row[i] = to[k];
                }
            }
        }

        /// <summary>A rifle pointing right. The pivot is the grip, so it turns around the ruler's hands.</summary>
        public static PixelImage Rifle()
        {
            var image = new PixelImage(30, 9) { Pivot = new Vector2(10f / 30f, 4f / 9f) };
            var wood = PixelImage.Rgb(128, 82, 46);
            var woodDark = PixelImage.Rgb(92, 58, 34);
            var metal = PixelImage.Rgb(70, 74, 86);
            var metalLight = PixelImage.Rgb(122, 128, 144);
            var metalDark = PixelImage.Rgb(44, 46, 56);

            for (int x = 1; x <= 8; x++)                       // stock, thicker at the back
                for (int y = 3; y <= (x < 5 ? 6 : 5); y++)
                    image.Set(x, y, y == 3 ? woodDark : wood);
            for (int x = 9; x <= 18; x++)                      // receiver
                for (int y = 3; y <= 6; y++)
                    image.Set(x, y, y == 6 ? metalLight : y == 3 ? metalDark : metal);
            for (int x = 19; x <= 28; x++)                     // barrel
            {
                image.Set(x, 5, metalLight);
                image.Set(x, 4, metal);
            }
            image.Set(27, 6, metalDark);                       // front sight
            image.Set(12, 7, metalDark);                       // rear sight
            image.Set(13, 7, metalDark);
            for (int y = 1; y <= 2; y++)                       // magazine
                for (int x = 14; x <= 16; x++)
                    image.Set(x, y, metalDark);
            image.Set(10, 2, woodDark);                        // grip
            image.Set(10, 1, woodDark);
            image.Set(11, 2, wood);
            image.Outline(PixelImage.Rgb(26, 22, 30));
            return image;
        }

        /// <summary>A C4 charge: a block with tape and a light that is on or off.</summary>
        public static PixelImage C4(bool lightOn)
        {
            var image = new PixelImage(16, 12) { Pivot = new Vector2(0.5f, 0.5f) };
            var block = PixelImage.Rgb(196, 182, 136);
            var blockDark = PixelImage.Rgb(156, 142, 100);
            var tape = PixelImage.Rgb(54, 54, 60);
            for (int y = 1; y <= 9; y++)
                for (int x = 1; x <= 14; x++)
                    image.Set(x, y, y <= 2 || x >= 13 ? blockDark : block);
            for (int y = 1; y <= 9; y++)
            {
                image.Set(4, y, tape);
                image.Set(11, y, tape);
            }
            var light = lightOn ? PixelImage.Rgb(255, 60, 50) : PixelImage.Rgb(110, 30, 30);
            image.Set(7, 7, light);
            image.Set(8, 7, light);
            image.Set(7, 6, light);
            image.Set(8, 6, light);
            image.Set(6, 10, PixelImage.Rgb(200, 40, 40)); // wires
            image.Set(9, 10, PixelImage.Rgb(60, 110, 200));
            image.Outline(PixelImage.Rgb(30, 28, 26));
            return image;
        }

        /// <summary>A projectile pointing right with a bright tip.</summary>
        public static PixelImage Projectile(int length, int thickness, Color32 color)
        {
            length = Mathf.Max(1, length);
            thickness = Mathf.Max(1, thickness);
            var image = new PixelImage(length, thickness) { Pivot = new Vector2(0.5f, 0.5f) };
            for (int y = 0; y < thickness; y++)
                for (int x = 0; x < length; x++)
                    image.Set(x, y, x == length - 1 ? PixelImage.Rgb(255, 255, 230) : x < length / 3 ? PixelImage.Shade(color, -40) : color);
            return image;
        }
    }
}
