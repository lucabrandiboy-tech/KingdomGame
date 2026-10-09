using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// People and gear seen from straight above, facing right (+X). The game turns
    /// the sprites to face where they aim or walk. Soft shading with light from
    /// the top-left; placeholder art until real art replaces it.
    /// </summary>
    public static class CharacterArt
    {
        static readonly Vector3 Light = new Vector3(-0.55f, 0.6f, 0.58f);

        static readonly Color32 Skin = PixelImage.Rgb(214, 168, 132);
        static readonly Color32 Boot = PixelImage.Rgb(58, 40, 30);

        // How big people are drawn: 1 = the original sizes below.
        const float K = 1.4f;

        /// <summary>Hand position (where the gun's grip goes) relative to the middle of a ruler frame, in pixels.</summary>
        public static readonly Vector2 RulerHand = new Vector2(12f * K, -1.5f * K);

        /// <summary>Four walking frames of the ruler: feet together, left forward, together, right forward.</summary>
        public static PixelImage[] RulerFrames() => new[] { Ruler(0, 0), Ruler(5, -4), Ruler(0, 0), Ruler(-4, 5) };

        static PixelImage Ruler(int leftFoot, int rightFoot)
        {
            var img = new PixelImage(64, 64);
            const float cx = 32f, cy = 32f;
            Feet(img, cx, cy, leftFoot * K, rightFoot * K, 4.6f * K, 1.25f * K);

            // Cape: hangs from the shoulders down the back, flaring a little, with folds and a gold hem.
            var cape = PixelImage.Rgb(108, 24, 32);
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float back = (cx + 1f * K - (x + 0.5f)) / K;       // 0 at the shoulders, growing towards the back
                    float across = (y + 0.5f - cy) / K;
                    if (back < 0f || back > 14f) continue;
                    float halfWidth = 10.5f + back * 0.18f;
                    float roundEnd = 14f - Mathf.Max(0f, (across * across) / (halfWidth * halfWidth) * 3.2f);
                    float edge = Mathf.Min(halfWidth - Mathf.Abs(across), roundEnd - back) * K;
                    if (edge <= -0.5f) continue;
                    float fold = 0.84f + 0.16f * Mathf.Sin(across * 1.15f + back * 0.12f);
                    float light = 0.95f + across * 0.012f - back * 0.018f;      // shoulders higher than the hem
                    var c = PixelImage.Scale(cape, fold * light);
                    if (roundEnd - back < 1.4f) c = PixelImage.Rgb(170, 132, 56);          // gold hem
                    img.Blend(x, y, c, edge + 0.5f);
                }

            var tunic = PixelImage.Rgb(46, 60, 120);
            Arm(img, cx + 1f * K, cy + 8.5f * K, cx + 9.5f * K, cy + 2.5f * K, tunic);     // left arm reaches across to the barrel
            Arm(img, cx + 1f * K, cy - 8.5f * K, cx + 11f * K, cy - 2.5f * K, tunic);     // right hand on the grip
            Shaded(img, cx, cy, 6f * K, 10.5f * K, tunic, 0.5f);                          // shoulders and chest
            Shaded(img, cx - 0.5f * K, cy + 9f * K, 3.2f * K, 3f * K, PixelImage.Rgb(180, 140, 60), 0.5f);  // gold pauldrons
            Shaded(img, cx - 0.5f * K, cy - 9f * K, 3.2f * K, 3f * K, PixelImage.Rgb(180, 140, 60), 0.5f);

            // Ermine collar.
            var fur = PixelImage.Rgb(222, 218, 208);
            Shaded(img, cx, cy, 5.2f * K, 7f * K, fur, 0.35f);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + 0.4f;
                img.Ellipse(cx + Mathf.Cos(a) * 4.3f * K, cy + Mathf.Sin(a) * 5.8f * K, 0.55f * K, 0.55f * K, PixelImage.Rgb(30, 28, 34));
            }

            // Head: hair seen from above, with a gold crown on it.
            Shaded(img, cx + 1f * K, cy, 4.6f * K, 4.4f * K, PixelImage.Rgb(84, 56, 36), 0.55f);
            var gold = PixelImage.Rgb(214, 172, 66);
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                float shade = 0.85f + 0.3f * (-Mathf.Cos(a) * 0.6f + Mathf.Sin(a) * 0.7f);
                img.Ellipse(cx + 1f * K + Mathf.Cos(a) * 3.6f * K, cy + Mathf.Sin(a) * 3.5f * K, 0.75f * K, 0.75f * K, PixelImage.Scale(gold, shade));
            }
            for (int i = 0; i < 5; i++)                                                     // the crown's points
            {
                float a = i / 5f * Mathf.PI * 2f + 0.3f;
                img.Ellipse(cx + 1f * K + Mathf.Cos(a) * 3.7f * K, cy + Mathf.Sin(a) * 3.6f * K, 0.85f * K, 0.85f * K, PixelImage.Rgb(255, 230, 150));
            }
            img.Ellipse(cx + 4.6f * K, cy, 0.8f * K, 1f * K, PixelImage.Rgb(200, 36, 52));     // ruby at the front
            return img;
        }

        /// <summary>A villager (builder) facing right; with <paramref name="carrying"/> a sack on their back.</summary>
        public static PixelImage[] VillagerFrames(bool carrying) => new[]
        {
            Villager(0, 0, carrying), Villager(4, -3, carrying), Villager(0, 0, carrying), Villager(-3, 4, carrying),
        };

        static PixelImage Villager(int leftFoot, int rightFoot, bool carrying)
        {
            var img = new PixelImage(52, 52);
            const float cx = 26f, cy = 26f;
            Feet(img, cx, cy, leftFoot * K, rightFoot * K, 4f * K, 1.1f * K);
            var tunic = PixelImage.Rgb(92, 70, 46);
            if (carrying) Shaded(img, cx - 6.5f * K, cy, 5f * K, 6.5f * K, PixelImage.Rgb(150, 132, 96), 0.55f);  // sack on the back
            Arm(img, cx + 0.5f * K, cy + 7f * K, cx + 6.5f * K, cy + 5f * K, tunic);
            Arm(img, cx + 0.5f * K, cy - 7f * K, cx + 6.5f * K, cy - 5f * K, tunic);
            Shaded(img, cx, cy, 5f * K, 8.5f * K, tunic, 0.5f);
            Shaded(img, cx + 0.5f * K, cy, 4f * K, 4f * K, PixelImage.Rgb(66, 46, 30), 0.6f);             // leather cap
            img.Ellipse(cx + 3.6f * K, cy, 1.2f * K, 2.8f * K, PixelImage.Rgb(84, 60, 40));                // cap brim
            return img;
        }

        static void Feet(PixelImage img, float cx, float cy, float left, float right, float apart, float size)
        {
            img.Ellipse(cx + 1.5f * size + left, cy + apart, 3.2f * size, 2.1f * size, Boot);
            img.Ellipse(cx + 1.5f * size + right, cy - apart, 3.2f * size, 2.1f * size, Boot);
        }

        static void Arm(PixelImage img, float x0, float y0, float x1, float y1, Color32 sleeve)
        {
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                float r = Mathf.Lerp(2.7f, 2.1f, t) * K;
                Shaded(img, Mathf.Lerp(x0, x1, t), Mathf.Lerp(y0, y1, t), r, r, sleeve, 0.35f);
            }
            Shaded(img, x1 + 1.3f * K, y1, 1.9f * K, 1.9f * K, Skin, 0.35f);
        }

        // An ellipse lit like a rounded bump (bright towards the top-left).
        static void Shaded(PixelImage img, float cx, float cy, float rx, float ry, Color32 color, float strength)
        {
            var light = Light.normalized;
            for (int y = Mathf.FloorToInt(cy - ry - 1); y <= cy + ry + 1; y++)
                for (int x = Mathf.FloorToInt(cx - rx - 1); x <= cx + rx + 1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d2 = dx * dx + dy * dy;
                    float edge = (1f - Mathf.Sqrt(d2)) * Mathf.Min(rx, ry) + 0.5f;
                    if (edge <= 0f) continue;
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d2));
                    float lit = dx * light.x + dy * light.y + nz * light.z;   // -1..1
                    float factor = 1f + (lit - 0.55f) * strength * 1.6f;
                    img.Blend(x, y, PixelImage.Scale(color, factor), edge);
                }
        }

        /// <summary>A rifle seen from above, pointing right. The pivot is the grip.</summary>
        public static PixelImage Rifle()
        {
            var small = RifleSmall();
            // Drawn small and scaled up, so it matches the bigger people.
            int w = Mathf.RoundToInt(small.Width * K), h = Mathf.RoundToInt(small.Height * K);
            var img = new PixelImage(w, h) { Pivot = small.Pivot };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    img.Set(x, y, small.Get(Mathf.FloorToInt((x + 0.5f) / K), Mathf.FloorToInt((y + 0.5f) / K)));
            return img;
        }

        static PixelImage RifleSmall()
        {
            var img = new PixelImage(38, 9) { Pivot = new Vector2(12f / 38f, 0.5f) };
            var wood = PixelImage.Rgb(104, 70, 42);
            var metal = PixelImage.Rgb(54, 56, 64);
            for (int x = 1; x <= 10; x++)                          // stock
                for (int y = 2; y <= 6; y++)
                    img.Blend(x, y, PixelImage.Scale(wood, y >= 5 ? 1.2f : y <= 2 ? 0.75f : 1f), x == 1 ? 0.6f : 1f);
            for (int x = 11; x <= 22; x++)                         // receiver
                for (int y = 3; y <= 5; y++)
                    img.Set(x, y, PixelImage.Scale(metal, y == 5 ? 1.35f : y == 3 ? 0.8f : 1f));
            for (int x = 23; x <= 36; x++)                         // barrel
            {
                img.Set(x, 4, PixelImage.Scale(metal, 1.2f));
                img.Blend(x, 5, PixelImage.Scale(metal, 0.8f), 0.7f);
            }
            for (int x = 13; x <= 19; x++)                         // scope on top
            {
                img.Set(x, 5, PixelImage.Rgb(34, 36, 42));
                img.Set(x, 6, PixelImage.Rgb(70, 74, 86));
            }
            img.Set(13, 6, PixelImage.Rgb(120, 170, 210));          // lens glint
            return img;
        }

        /// <summary>A C4 charge seen from above, with its light on or off.</summary>
        public static PixelImage C4(bool lightOn)
        {
            var img = new PixelImage(18, 14);
            var block = PixelImage.Rgb(170, 156, 116);
            for (int y = 2; y <= 11; y++)
                for (int x = 2; x <= 15; x++)
                {
                    float shade = 1f + (y - 6.5f) * 0.03f - (x - 8.5f) * 0.02f;
                    img.Set(x, y, PixelImage.Scale(block, shade));
                }
            for (int y = 2; y <= 11; y++)
            {
                img.Set(5, y, PixelImage.Rgb(46, 46, 52));
                img.Set(12, y, PixelImage.Rgb(46, 46, 52));
            }
            img.Ellipse(8.5f, 8.5f, 1.6f, 1.6f, lightOn ? PixelImage.Rgb(255, 70, 50) : PixelImage.Rgb(90, 26, 26));
            if (lightOn) img.Ellipse(8.5f, 8.5f, 3f, 3f, PixelImage.Rgb(255, 80, 60, 70));
            return img;
        }

        /// <summary>A glowing bullet or rocket pointing right.</summary>
        public static PixelImage Projectile(int length, int thickness, Color32 color)
        {
            length = Mathf.Max(2, length);
            thickness = Mathf.Max(1, thickness);
            var img = new PixelImage(length + 2, thickness + 2);
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float along = x / (float)(img.Width - 1);              // 0 = tail, 1 = tip
                    float across = Mathf.Abs(y + 0.5f - img.Height / 2f) / (img.Height / 2f);
                    float alpha = Mathf.Clamp01(1.2f - across) * Mathf.Clamp01(along * 1.6f + 0.15f);
                    var c = PixelImage.Mix(color, PixelImage.Rgb(255, 250, 220), along * (1f - across));
                    img.Blend(x, y, c, alpha);
                }
            return img;
        }
    }
}
