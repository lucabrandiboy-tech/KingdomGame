using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// People and gear seen from straight above, drawn facing right (+X). The
    /// game turns them to face where they aim or walk. So that the light still
    /// comes from the top-left after turning, people are drawn once per facing
    /// direction (<see cref="RulerDirections"/>, <see cref="VillagerDirections"/>)
    /// with the light turned the other way; the game picks the nearest one and
    /// turns it the last few degrees. Sizes scale with <c>ppu</c> (pixels per tile).
    /// </summary>
    public static class CharacterArt
    {
        public const int RulerDirections = 16;
        public const int VillagerDirections = 8;
        public const int WalkFrames = 4;

        /// <summary>Picture width of the ruler and a villager, in tiles.</summary>
        public const float RulerSize = 0.72f, VillagerSize = 0.58f;

        static readonly Color32 Skin = PixelImage.Rgb(200, 156, 122);

        // The rifle was designed on a 64-pixels-per-tile grid; K scales it.
        const float K = 1.4f;

        /// <summary>Hand position (where the gun's grip goes) relative to the middle of the ruler, in tiles.</summary>
        public static Vector2 RulerHand => new Vector2(0.16f, -0.1f);

        /// <summary>
        /// One of the ruler's four walking frames (0 feet together, 1 left foot forward,
        /// 2 together, 3 right foot forward), facing right and lit as if turned to
        /// <paramref name="facingDegrees"/> (so after turning, the light is top-left).
        /// </summary>
        public static PixelImage RulerFrame(int ppu, float facingDegrees, int frame) =>
            Ruler(ppu, ArtLight.For(facingDegrees), Stride(frame));

        /// <summary>One walking frame of a villager (builder), like <see cref="RulerFrame"/>; with <paramref name="carrying"/> a sack on their back.</summary>
        public static PixelImage VillagerFrame(int ppu, bool carrying, float facingDegrees, int frame) =>
            Villager(ppu, ArtLight.For(facingDegrees), Stride(frame), carrying);

        static float Stride(int frame) => frame == 1 ? 1f : frame == 3 ? -1f : 0f;

        // The materials people are made of.
        enum Mat { Cape, Hem, Tunic, Gold, Steel, Fur, Hair, Skin, Glove, Boot, Sack, Rope, Cap }

        // One body part: a capsule (A to B, radius R; A = B for a ball) or an ellipsoid (A = middle,
        // B = radii), seen from above. Heights (z) are in tiles: the head is about 1.5 tiles up.
        // Positions are in tiles from the figure's middle, facing +X.
        struct Part
        {
            public bool Ellipsoid;
            public Vector3 A, B;
            public float R;
            public Mat Material;
            public Color32 Tint;
        }

        static Part Ball(float x, float y, float z, float r, Mat m, Color32 tint) =>
            new Part { A = new Vector3(x, y, z), B = new Vector3(x, y, z), R = r, Material = m, Tint = tint };

        static Part Limb(float x0, float y0, float z0, float x1, float y1, float z1, float r, Mat m, Color32 tint) =>
            new Part { A = new Vector3(x0, y0, z0), B = new Vector3(x1, y1, z1), R = r, Material = m, Tint = tint };

        static Part Body(float x, float y, float z, float rx, float ry, float rz, Mat m, Color32 tint) =>
            new Part { Ellipsoid = true, A = new Vector3(x, y, z), B = new Vector3(rx, ry, rz), Material = m, Tint = tint };

        static PixelImage Ruler(int ppu, Vector3 light, float step)
        {
            var parts = new System.Collections.Generic.List<Part>();
            var boot = PixelImage.Rgb(40, 28, 20);
            var tunic = PixelImage.Rgb(34, 44, 92);
            var gold = PixelImage.Rgb(196, 148, 60);
            var glove = PixelImage.Rgb(74, 50, 32);
            // Feet: one forward, one back while walking.
            parts.Add(Limb(0.02f + step * 0.07f, 0.065f, 0.05f, 0.1f + step * 0.07f, 0.065f, 0.05f, 0.03f, Mat.Boot, boot));
            parts.Add(Limb(0.02f - step * 0.07f, -0.065f, 0.05f, 0.1f - step * 0.07f, -0.065f, 0.05f, 0.03f, Mat.Boot, boot));
            parts.Add(Body(0f, 0f, 1.22f, 0.095f, 0.165f, 0.2f, Mat.Tunic, tunic));                         // chest and shoulders
            parts.Add(Body(-0.01f, 0.148f, 1.3f, 0.055f, 0.048f, 0.07f, Mat.Steel, PixelImage.Rgb(78, 80, 88)));   // steel pauldrons
            parts.Add(Body(-0.01f, -0.148f, 1.3f, 0.055f, 0.048f, 0.07f, Mat.Steel, PixelImage.Rgb(78, 80, 88)));
            // Arms holding the rifle against the right shoulder: right hand on the grip, left hand under the barrel.
            parts.Add(Limb(0.0f, -0.16f, 1.3f, 0.05f, -0.2f, 1.16f, 0.043f, Mat.Tunic, tunic));
            parts.Add(Limb(0.05f, -0.2f, 1.16f, 0.15f, -0.115f, 1.24f, 0.036f, Mat.Tunic, tunic));
            parts.Add(Ball(0.16f, -0.1f, 1.25f, 0.03f, Mat.Glove, glove));
            parts.Add(Limb(0.0f, 0.16f, 1.3f, 0.17f, 0.09f, 1.17f, 0.043f, Mat.Tunic, tunic));
            parts.Add(Limb(0.17f, 0.09f, 1.17f, 0.35f, -0.085f, 1.24f, 0.036f, Mat.Tunic, tunic));
            parts.Add(Ball(0.362f, -0.095f, 1.25f, 0.029f, Mat.Glove, glove));
            parts.Add(Ball(0.012f, 0f, 1.43f, 0.088f, Mat.Fur, PixelImage.Rgb(204, 200, 190)));            // ermine collar
            parts.Add(Ball(0.022f, 0f, 1.53f, 0.072f, Mat.Hair, PixelImage.Rgb(62, 40, 24)));              // head
            return Figure(ppu, RulerSize, light, parts, true);
        }

        static PixelImage Villager(int ppu, Vector3 light, float step, bool carrying)
        {
            var parts = new System.Collections.Generic.List<Part>();
            var boot = PixelImage.Rgb(46, 32, 22);
            var tunic = PixelImage.Rgb(84, 64, 42);
            parts.Add(Limb(0.02f + step * 0.06f, 0.055f, 0.05f, 0.09f + step * 0.06f, 0.055f, 0.05f, 0.026f, Mat.Boot, boot));
            parts.Add(Limb(0.02f - step * 0.06f, -0.055f, 0.05f, 0.09f - step * 0.06f, -0.055f, 0.05f, 0.026f, Mat.Boot, boot));
            parts.Add(Body(0f, 0f, 1.15f, 0.085f, 0.14f, 0.18f, Mat.Tunic, tunic));
            float swing = step * 0.03f;
            parts.Add(Limb(0.0f, -0.135f, 1.25f, 0.08f + swing, -0.125f, 1.1f, 0.036f, Mat.Tunic, tunic));
            parts.Add(Limb(0.08f + swing, -0.125f, 1.1f, 0.15f + swing, -0.09f, 1.05f, 0.031f, Mat.Tunic, tunic));
            parts.Add(Ball(0.165f + swing, -0.085f, 1.05f, 0.026f, Mat.Skin, Skin));
            parts.Add(Limb(0.0f, 0.135f, 1.25f, 0.08f - swing, 0.125f, 1.1f, 0.036f, Mat.Tunic, tunic));
            parts.Add(Limb(0.08f - swing, 0.125f, 1.1f, 0.15f - swing, 0.09f, 1.05f, 0.031f, Mat.Tunic, tunic));
            parts.Add(Ball(0.165f - swing, 0.085f, 1.05f, 0.026f, Mat.Skin, Skin));
            if (carrying)
            {
                parts.Add(Body(-0.1f, 0f, 1.32f, 0.085f, 0.115f, 0.11f, Mat.Sack, PixelImage.Rgb(138, 120, 86)));   // sack on the back
                parts.Add(Limb(-0.02f, 0.13f, 1.34f, -0.02f, -0.13f, 1.34f, 0.012f, Mat.Rope, PixelImage.Rgb(66, 50, 32)));
            }
            parts.Add(Ball(0.02f, 0f, 1.44f, 0.064f, Mat.Cap, PixelImage.Rgb(60, 42, 28)));                // leather cap
            parts.Add(Body(0.075f, 0f, 1.43f, 0.02f, 0.06f, 0.02f, Mat.Cap, PixelImage.Rgb(74, 52, 34)));    // its brim
            return Figure(ppu, VillagerSize, light, parts, false);
        }

        /// <summary>
        /// Draws a figure made of rounded parts as seen from above: each pixel shows
        /// the highest part there, shaded by the surface's slope (so the whole figure
        /// is lit as one 3D shape), with cloth, fur, hair and metal surfaces.
        /// </summary>
        static PixelImage Figure(int ppu, float sizeTiles, Vector3 light, System.Collections.Generic.List<Part> parts, bool royal)
        {
            int size = Mathf.CeilToInt(sizeTiles * ppu);
            var img = new PixelImage(size, size);
            float c = size / 2f;
            float px1 = 1f / ppu;
            var height = new float[size * size];
            var which = new int[size * size];
            var cover = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float wx = (x + 0.5f - c) / ppu, wy = (y + 0.5f - c) / ppu;
                    float best = -1f, coverage = 0f;
                    int top = -1;
                    for (int i = 0; i < parts.Count; i++)
                    {
                        var p = parts[i];
                        float h, inside;
                        if (!p.Ellipsoid)
                        {
                            // Capsule: distance to the segment, and the height along it.
                            float abx = p.B.x - p.A.x, aby = p.B.y - p.A.y;
                            float len2 = abx * abx + aby * aby;
                            float t = len2 > 1e-8f ? Mathf.Clamp01(((wx - p.A.x) * abx + (wy - p.A.y) * aby) / len2) : 0f;
                            float dx = wx - (p.A.x + abx * t), dy = wy - (p.A.y + aby * t);
                            float d2 = dx * dx + dy * dy;
                            inside = p.R - Mathf.Sqrt(d2);
                            if (inside <= -px1) continue;
                            h = p.A.z + (p.B.z - p.A.z) * t + Mathf.Sqrt(Mathf.Max(0f, p.R * p.R - d2));
                        }
                        else
                        {
                            float dx = (wx - p.A.x) / p.B.x, dy = (wy - p.A.y) / p.B.y;
                            float d2 = dx * dx + dy * dy;
                            inside = (1f - Mathf.Sqrt(d2)) * Mathf.Min(p.B.x, p.B.y);
                            if (inside <= -px1) continue;
                            h = p.A.z + p.B.z * Mathf.Sqrt(Mathf.Max(0f, 1f - d2));
                        }
                        coverage = Mathf.Max(coverage, Mathf.Clamp01(inside * ppu + 0.5f));
                        if (h > best) { best = h; top = i; }
                    }
                    // The ruler's cape: hangs down the back from the shoulders, with folds.
                    if (royal)
                    {
                        float back = 0.03f - wx;                                 // tiles behind the cape's front edge (over the shoulders)
                        float halfWidth = 0.185f + Mathf.Max(0f, back) * 0.25f;
                        float length = 0.27f - (wy * wy) / (halfWidth * halfWidth) * 0.06f;
                        float edge = Mathf.Min(Mathf.Min(halfWidth - Mathf.Abs(wy), length - back), back + 0.01f);
                        if (edge > -px1)
                        {
                            // Folds fanning out towards the hem, deeper further down.
                            float fan = wy / (halfWidth + 0.001f);
                            float depth = 0.004f + Mathf.Max(0f, back) * 0.09f;
                            float fold = Mathf.Sin(fan * 9.5f + back * 4f) * depth + Mathf.Sin(fan * 21f + 1.3f) * depth * 0.35f;
                            float h = 1.45f - Mathf.Max(0f, back) * 2.2f - (wy * wy) * 1.2f + fold;
                            if (h > best || top < 0) { best = h; top = edge < 0.022f ? -3 : -2; }
                            coverage = Mathf.Max(coverage, Mathf.Clamp01(edge * ppu + 0.5f));
                        }
                    }
                    int idx = y * size + x;
                    height[idx] = best;
                    which[idx] = top;
                    cover[idx] = top == -1 ? 0f : coverage;
                }

            var blurred = PixelImage.BlurClamped(height, size, size, Mathf.Max(1, Mathf.RoundToInt(0.02f * ppu)));
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    if (which[idx] == -1) continue;
                    float hx = (H(height, which, size, x + 1, y, height[idx]) - H(height, which, size, x - 1, y, height[idx])) * 0.5f * ppu;
                    float hy = (H(height, which, size, x, y + 1, height[idx]) - H(height, which, size, x, y - 1, height[idx])) * 0.5f * ppu;
                    float nx = -hx, ny = -hy, nz = 1f;
                    float len = Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
                    nx /= len; ny /= len; nz /= len;
                    float ndl = nx * light.x + ny * light.y + nz * light.z;
                    float diffuse = Mathf.Max(0f, ndl);
                    float cavity = Mathf.Clamp((height[idx] - blurred[idx]) * 18f, -1f, 0.3f);
                    float reflect = 2f * ndl * nz - light.z;                    // mirror of the light, seen from above

                    Mat m;
                    Color32 baseColor;
                    if (which[idx] == -2) { m = Mat.Cape; baseColor = PixelImage.Rgb(104, 18, 28); }
                    else if (which[idx] == -3) { m = Mat.Hem; baseColor = PixelImage.Rgb(176, 132, 52); }
                    else { m = parts[which[idx]].Material; baseColor = parts[which[idx]].Tint; }

                    float wx = (x + 0.5f - c) / ppu, wy = (y + 0.5f - c) / ppu;
                    float shine = 0f, shininess = 12f, texture = 0f;
                    switch (m)
                    {
                        case Mat.Cape:
                            texture = (Noise.Value(wx * 900f, wy * 900f, 51u) - 0.5f) * 0.08f;
                            shine = 0.12f; shininess = 6f;                                    // velvet sheen
                            break;
                        case Mat.Hem:
                        case Mat.Gold:
                            shine = 0.7f; shininess = 22f;
                            texture = (Noise.Value(wx * 600f, wy * 600f, 53u) - 0.5f) * 0.06f;
                            break;
                        case Mat.Steel:
                            shine = 0.45f; shininess = 16f;
                            texture = (Noise.Value(wx * 800f, wy * 800f, 54u) - 0.5f) * 0.08f;
                            break;
                        case Mat.Tunic:
                        case Mat.Sack:
                            texture = Mathf.Sin(wx * 2400f) * Mathf.Sin(wy * 2400f) * 0.03f + (Noise.Value(wx * 300f, wy * 300f, 55u) - 0.5f) * 0.12f;
                            break;
                        case Mat.Fur:
                        {
                            texture = (Noise.Value(wx * 1400f, wy * 1400f, 57u) - 0.5f) * 0.18f;
                            // Ermine: black tail tips around the collar.
                            float a = Mathf.Atan2(wy, wx - 0.012f);
                            float r = Mathf.Sqrt((wx - 0.012f) * (wx - 0.012f) + wy * wy);
                            if (Mathf.Abs(Mathf.Sin(a * 3.5f)) < 0.16f && r > 0.075f && r < 0.098f) baseColor = PixelImage.Rgb(26, 24, 28);
                            break;
                        }
                        case Mat.Hair:
                        {
                            float a = Mathf.Atan2(wy, wx - 0.022f);
                            texture = (Noise.Value(a * 40f, Mathf.Sqrt((wx - 0.022f) * (wx - 0.022f) + wy * wy) * 200f, 59u) - 0.5f) * 0.3f;
                            shine = 0.15f; shininess = 10f;
                            break;
                        }
                        case Mat.Cap:
                        case Mat.Glove:
                        case Mat.Boot:
                            texture = (Noise.Value(wx * 500f, wy * 500f, 61u) - 0.5f) * 0.1f;
                            shine = 0.12f; shininess = 8f;
                            break;
                        case Mat.Skin:
                            shine = 0.08f;
                            break;
                    }
                    float shade = (0.32f + diffuse * 0.95f) * (1f + cavity * 0.6f) + texture;
                    var col = PixelImage.Scale(baseColor, shade);
                    if (shine > 0f) col = PixelImage.Mix(col, PixelImage.Rgb(255, 244, 214), Mathf.Pow(Mathf.Max(0f, reflect), shininess) * shine);
                    col.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(cover[idx]) * 255f);
                    img.Pixels[idx] = col;
                }

            if (royal) Crown(img, ppu, light, c);
            return img;
        }

        static float H(float[] height, int[] which, int size, int x, int y, float fallback)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return fallback - 0.05f;
            int i = y * size + x;
            return which[i] == -1 ? fallback - 0.05f : height[i];
        }

        // A gold crown on the ruler's head: a ring with five points and jewels.
        static void Crown(PixelImage img, int ppu, Vector3 light, float c)
        {
            float hx = c + 0.022f * ppu, hy = c;
            float ringR = 0.058f * ppu, tube = Mathf.Max(0.8f, 0.012f * ppu);
            var gold = PixelImage.Rgb(212, 168, 70);
            for (int y = Mathf.FloorToInt(hy - ringR - tube - 1); y <= hy + ringR + tube + 1; y++)
                for (int x = Mathf.FloorToInt(hx - ringR - tube - 1); x <= hx + ringR + tube + 1; x++)
                {
                    float dx = x + 0.5f - hx, dy = y + 0.5f - hy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float across = (r - ringR) / tube;
                    if (Mathf.Abs(across) > 1.1f || r < 0.001f) continue;
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - across * across));
                    float nx = dx / r * across, ny = dy / r * across;
                    float lit = Mathf.Max(0f, nx * light.x + ny * light.y + nz * light.z);
                    float reflect = 2f * lit * nz - light.z;
                    var col = PixelImage.Scale(gold, 0.35f + lit * 0.9f);
                    col = PixelImage.Mix(col, PixelImage.Rgb(255, 246, 210), Mathf.Pow(Mathf.Max(0f, reflect), 22f) * 0.9f);
                    img.Blend(x, y, col, Mathf.Clamp01((1.1f - Mathf.Abs(across)) * tube));
                }
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                float px = hx + Mathf.Cos(a) * ringR, py = hy + Mathf.Sin(a) * ringR;
                Jewel(img, px, py, tube * 1.7f, PixelImage.Rgb(236, 196, 96), light);
                Jewel(img, px, py, tube * 0.8f, i % 2 == 0 ? PixelImage.Rgb(40, 80, 190) : PixelImage.Rgb(170, 24, 40), light);
            }
        }

        static void Jewel(PixelImage img, float x, float y, float r, Color32 color, Vector3 light)
        {
            for (int py = Mathf.FloorToInt(y - r - 1); py <= y + r + 1; py++)
                for (int px = Mathf.FloorToInt(x - r - 1); px <= x + r + 1; px++)
                {
                    float dx = (px + 0.5f - x) / r, dy = (py + 0.5f - y) / r;
                    float d2 = dx * dx + dy * dy;
                    float edge = (1f - Mathf.Sqrt(d2)) * r + 0.5f;
                    if (edge <= 0f) continue;
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d2));
                    float lit = Mathf.Max(0f, dx * light.x + dy * light.y + nz * light.z);
                    float reflect = 2f * lit * nz - light.z;
                    var col = PixelImage.Scale(color, 0.4f + lit * 0.9f);
                    col = PixelImage.Mix(col, PixelImage.Rgb(255, 250, 230), Mathf.Pow(Mathf.Max(0f, reflect), 30f));
                    img.Blend(px, py, col, edge);
                }
        }

        /// <summary>A rifle seen from above, pointing right. The grip is in the middle of the picture (the pivot).</summary>
        public static PixelImage Rifle(int ppu)
        {
            var small = RifleSmall();
            float s = ppu / 64f * K;
            // Drawn small and scaled up smoothly, padded so the grip (x = 12 of 38) is in the middle.
            int halfWidth = Mathf.CeilToInt((38 - 12) * s) + 2;
            int w = halfWidth * 2, h = Mathf.CeilToInt(small.Height * s) + 2;
            var img = new PixelImage(w, h);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sx = (x + 0.5f - halfWidth) / s + 12f, sy = (y + 0.5f - h / 2f) / s + small.Height / 2f;
                    img.Set(x, y, Bilinear(small, sx - 0.5f, sy - 0.5f));
                }
            return img;
        }

        static Color32 Bilinear(PixelImage img, float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            float r = 0, g = 0, b = 0, a = 0;
            for (int k = 0; k < 4; k++)
            {
                int sx = x0 + (k & 1), sy = y0 + (k >> 1);
                float w = ((k & 1) != 0 ? fx : 1 - fx) * ((k >> 1) != 0 ? fy : 1 - fy);
                var c = img.Get(sx, sy);
                float ca = c.a / 255f * w;
                r += c.r * ca; g += c.g * ca; b += c.b * ca; a += ca;
            }
            if (a <= 0.001f) return PixelImage.Clear;
            return new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(r / a), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(g / a), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(b / a), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
        }

        static PixelImage RifleSmall()
        {
            var img = new PixelImage(38, 9);
            var wood = PixelImage.Rgb(96, 64, 38);
            var metal = PixelImage.Rgb(50, 52, 60);
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
                img.Set(x, 5, PixelImage.Rgb(30, 32, 38));
                img.Set(x, 6, PixelImage.Rgb(66, 70, 82));
            }
            img.Set(13, 6, PixelImage.Rgb(120, 170, 210));          // lens glint
            return img;
        }

        /// <summary>A C4 charge seen from above, with its light on or off (small, made when the game starts).</summary>
        public static PixelImage C4(bool lightOn)
        {
            var img = new PixelImage(18, 14);
            var block = PixelImage.Rgb(160, 146, 108);
            for (int y = 2; y <= 11; y++)
                for (int x = 2; x <= 15; x++)
                {
                    float shade = 1f + (y - 6.5f) * 0.03f - (x - 8.5f) * 0.02f;
                    img.Set(x, y, PixelImage.Scale(block, shade));
                }
            for (int y = 2; y <= 11; y++)
            {
                img.Set(5, y, PixelImage.Rgb(42, 42, 48));
                img.Set(12, y, PixelImage.Rgb(42, 42, 48));
            }
            img.Ellipse(8.5f, 8.5f, 1.6f, 1.6f, lightOn ? PixelImage.Rgb(255, 70, 50) : PixelImage.Rgb(90, 26, 26));
            if (lightOn) img.Ellipse(8.5f, 8.5f, 3f, 3f, PixelImage.Rgb(255, 80, 60, 70));
            return img;
        }

        /// <summary>A glowing bullet or rocket pointing right (small, made when the game starts).</summary>
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
