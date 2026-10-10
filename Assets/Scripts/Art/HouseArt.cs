using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// A house seen from straight above: its roof (in its age's style, see
    /// <see cref="RoofArt"/>) over the whole house, a chimney at the back, and a
    /// stone step with the door at the front. Drawn while playing (one picture per
    /// level and direction, shared by every house that looks the same), at
    /// <c>ppt</c> pixels per tile. The picture is the house plus one tile all
    /// round; its pivot is the middle, so it sits on the middle of the house.
    /// </summary>
    public static class HouseArt
    {
        /// <summary>How far the roof reaches past the house's walls, in tiles.</summary>
        public const float Eave = 0.15f;

        /// <summary>
        /// The house facing <paramref name="rotation"/> (0 = the door at the bottom, then quarter
        /// turns anticlockwise). <paramref name="size"/> is its size facing 0.
        /// </summary>
        public static PixelImage Exterior(Vector2Int size, int rotation, RoofStyle roof, Color32 chimney, Color32 door, int ppt, uint seed)
        {
            bool turned = rotation % 2 == 1;
            int w = turned ? size.y : size.x, h = turned ? size.x : size.y;
            int gw = w + 2, gh = h + 2;
            var cells = new bool[gw * gh];
            for (int y = 1; y <= h; y++)
                for (int x = 1; x <= w; x++)
                    cells[y * gw + x] = true;
            var img = RoofArt.Roof(cells, gw, gh, ppt, roof, seed, Eave);
            img.Pivot = new Vector2(0.5f, 0.5f);
            float t = ppt;
            // Positions are in tiles from the house's middle, facing 0 (door at the bottom), then turned.
            float cx = gw * 0.5f, cy = gh * 0.5f;
            float halfW = size.x * 0.5f, halfH = size.y * 0.5f;
            Vector2 At(float x, float y) { var o = Turn(new Vector2(x, y), rotation); return new Vector2(cx + o.x, cy + o.y); }
            Vector2 Extent(float x, float y) => turned ? new Vector2(y, x) : new Vector2(x, y);

            // The step in front of the door, and the door under the eave.
            var step = At(0f, -halfH - 0.3f);
            Box(img, t, step, Extent(0.42f, 0.16f), PixelImage.Rgb(112, 108, 100), 0.1f, 11u);
            var doorAt = At(0f, -halfH - 0.08f);
            Box(img, t, doorAt, Extent(0.3f, 0.07f), door, 0.15f, 12u);

            // The chimney at the back, on the right, with its shadow falling on the roof.
            var stack = At(halfW - 0.55f, halfH - 0.5f);
            var shadowAt = stack + new Vector2(0.11f, -0.13f);
            Darken(img, t, shadowAt, new Vector2(0.21f, 0.21f), 0.55f);
            Box(img, t, stack, new Vector2(0.21f, 0.21f), chimney, 0.2f, 13u);
            Box(img, t, stack, new Vector2(0.09f, 0.09f), PixelImage.Rgb(20, 18, 16), 0.04f, 14u);   // the soot-black flue
            return img;
        }

        /// <summary>The soft shadow of a house (its roof's outline), at <paramref name="ppt"/> pixels per tile; pivot in the middle.</summary>
        public static PixelImage Shadow(Vector2Int size, int rotation, int ppt)
        {
            bool turned = rotation % 2 == 1;
            int w = turned ? size.y : size.x, h = turned ? size.x : size.y;
            int gw = w + 2, gh = h + 2;
            var mask = new PixelImage(gw * ppt, gh * ppt) { Pivot = new Vector2(0.5f, 0.5f) };
            float e = Eave * ppt;
            for (int y = 0; y < mask.Height; y++)
                for (int x = 0; x < mask.Width; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    if (px >= ppt - e && px <= (gw - 1) * ppt + e && py >= ppt - e && py <= (gh - 1) * ppt + e)
                        mask.Pixels[y * mask.Width + x] = new Color32(0, 0, 0, 255);
                }
            return mask.Shadow(Mathf.Max(1, ppt / 6), 1f);
        }

        static Vector2 Turn(Vector2 o, int rotation)
        {
            switch (((rotation % 4) + 4) % 4)
            {
                case 1: return new Vector2(-o.y, o.x);
                case 2: return new Vector2(-o.x, -o.y);
                case 3: return new Vector2(o.y, -o.x);
                default: return o;
            }
        }

        // A raised block seen from above (tiles): its top, a bright edge towards the light (top-left), a dark one away from it.
        static void Box(PixelImage img, float t, Vector2 center, Vector2 half, Color32 color, float bevel, uint seed)
        {
            int x0 = Mathf.FloorToInt((center.x - half.x) * t), x1 = Mathf.CeilToInt((center.x + half.x) * t);
            int y0 = Mathf.FloorToInt((center.y - half.y) * t), y1 = Mathf.CeilToInt((center.y + half.y) * t);
            float b = Mathf.Max(1.5f, bevel * Mathf.Min(half.x, half.y) * t);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    if (!img.Inside(x, y)) continue;
                    float px = (x + 0.5f) / t, py = (y + 0.5f) / t;
                    float left = (px - (center.x - half.x)) * t, right = ((center.x + half.x) - px) * t;
                    float bottom = (py - (center.y - half.y)) * t, top = ((center.y + half.y) - py) * t;
                    if (Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top)) < 0f) continue;
                    float shade = 1f + (Noise.Value(px * 40f, py * 40f, seed) - 0.5f) * 0.14f;
                    if (left < b || top < b) shade *= 1.18f;          // facing the light
                    else if (right < b || bottom < b) shade *= 0.68f; // facing away
                    var c = PixelImage.Scale(color, shade);
                    c.a = 255;
                    img.Pixels[y * img.Width + x] = c;
                }
        }

        static void Darken(PixelImage img, float t, Vector2 center, Vector2 half, float by)
        {
            int x0 = Mathf.FloorToInt((center.x - half.x) * t), x1 = Mathf.CeilToInt((center.x + half.x) * t);
            int y0 = Mathf.FloorToInt((center.y - half.y) * t), y1 = Mathf.CeilToInt((center.y + half.y) * t);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    if (!img.Inside(x, y) || img.Pixels[y * img.Width + x].a == 0) continue;
                    img.Pixels[y * img.Width + x] = PixelImage.Scale(img.Pixels[y * img.Width + x], by);
                }
        }
    }
}
