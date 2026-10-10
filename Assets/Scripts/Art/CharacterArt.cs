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
        public const int ZombieDirections = 8;
        /// <summary>A zombie's pictures: walking frames 0-3, then 4 (winding up a blow) and 5 (striking).</summary>
        public const int ZombieFrames = 6;
        /// <summary>The fallen are drawn for this many directions (turned the rest of the way like the living).</summary>
        public const int DeadDirections = 4;
        /// <summary>The kinds of zombie (each has its own pictures; ArtKeys.Zombie).</summary>
        public static readonly string[] ZombieKinds = { "Walker", "Runner", "Brute" };
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

        /// <summary>Picture width of each kind of zombie, in tiles.</summary>
        public static float ZombieSize(string kind) => kind == "Brute" ? 0.9f : kind == "Runner" ? 0.62f : 0.64f;

        /// <summary>
        /// One picture of a zombie (<see cref="ZombieKinds"/>): walking frames 0-3, 4 winding
        /// up a blow, 5 striking. Facing right and lit as if turned to <paramref name="facingDegrees"/>.
        /// </summary>
        public static PixelImage ZombieFrame(int ppu, string kind, float facingDegrees, int frame) =>
            Zombie(ppu, ArtLight.For(facingDegrees), kind, frame);

        /// <summary>How big each kind of zombie is drawn, compared to a walker.</summary>
        public static float ZombieScale(string kind) => ZombieLook(kind).size;

        /// <summary>
        /// Where a zombie's eyes are in frame <paramref name="frame"/>, in tiles from its middle,
        /// facing right (at a walker's size: times <see cref="ZombieScale"/>).
        /// </summary>
        public static Vector2 ZombieEyesAt(string kind, int frame)
        {
            float hunch = (kind == "Runner" ? 0.08f : kind == "Brute" ? 0.03f : 0.05f) + (frame == 5 ? 0.05f : 0f);
            float headR = kind == "Brute" ? 0.062f : 0.07f;
            return new Vector2(hunch + 0.07f + headR * 0.62f, 0f);
        }

        /// <summary>
        /// A zombie's two pale eyes, which glow faintly in the dark so a horde can be seen
        /// coming at night (drawn unlit over the zombie, turned with it). The middle between
        /// the eyes is the pivot.
        /// </summary>
        public static PixelImage ZombieEyes(int ppu)
        {
            int w = Mathf.Max(4, Mathf.CeilToInt(0.05f * ppu)), h = Mathf.Max(6, Mathf.CeilToInt(0.1f * ppu));
            var img = new PixelImage(w, h);
            float r = Mathf.Max(1.2f, 0.012f * ppu), glow = r * 2.2f;
            foreach (float side in new[] { -1f, 1f })
            {
                float cx = w / 2f, cy = h / 2f + side * 0.03f * ppu;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        if (d >= glow) continue;
                        float core = Mathf.Clamp01((r - d) + 0.5f);
                        float a = Mathf.Max(core, Mathf.Pow(1f - d / glow, 2f) * 0.45f);
                        var c = PixelImage.Mix(PixelImage.Rgb(170, 190, 110), PixelImage.Rgb(236, 240, 190), core);
                        c.a = (byte)Mathf.RoundToInt(a * 230f);
                        img.Blend(x, y, c);
                    }
            }
            return img;
        }

        /// <summary>A dead zombie lying on its back, head to the right, lit as if turned to <paramref name="facingDegrees"/>.</summary>
        public static PixelImage ZombieDead(int ppu, string kind, float facingDegrees)
        {
            var (skin, cloth, k) = ZombieLook(kind);
            return Lying(ppu, ArtLight.For(facingDegrees), skin, cloth, PixelImage.Rgb(34, 28, 24), Mat.Rot, Mat.Rag, k, null, PixelImage.Rgb(58, 50, 42));
        }

        /// <summary>A villager who didn't make it, lying on their back.</summary>
        public static PixelImage VillagerDead(int ppu, float facingDegrees) =>
            Lying(ppu, ArtLight.For(facingDegrees), Skin, PixelImage.Rgb(84, 64, 42), PixelImage.Rgb(46, 32, 22), Mat.Skin, Mat.Tunic, 0.9f, null, PixelImage.Rgb(70, 48, 30));

        /// <summary>The fallen ruler, cape spread out under them.</summary>
        public static PixelImage RulerDead(int ppu, float facingDegrees) =>
            Lying(ppu, ArtLight.For(facingDegrees), Skin, PixelImage.Rgb(34, 44, 92), PixelImage.Rgb(40, 28, 20), Mat.Skin, Mat.Tunic, 1f, PixelImage.Rgb(104, 18, 28), PixelImage.Rgb(62, 40, 24));

        // Skin and clothes colours and body size of each kind of zombie.
        static (Color32 skin, Color32 cloth, float size) ZombieLook(string kind)
        {
            switch (kind)
            {
                case "Brute": return (PixelImage.Rgb(104, 98, 112), PixelImage.Rgb(48, 40, 36), 1.32f);
                case "Runner": return (PixelImage.Rgb(122, 128, 104), PixelImage.Rgb(74, 58, 44), 0.92f);
                default: return (PixelImage.Rgb(112, 124, 98), PixelImage.Rgb(52, 58, 72), 1f);
            }
        }

        static PixelImage Zombie(int ppu, Vector3 light, string kind, int frame)
        {
            var (skin, cloth, k) = ZombieLook(kind);
            bool brute = kind == "Brute", runner = kind == "Runner";
            float step = frame < WalkFrames ? Stride(frame) : 0f;
            float hunch = (runner ? 0.08f : brute ? 0.03f : 0.05f) + (frame == 5 ? 0.05f : 0f);
            float stride = runner ? 0.1f : 0.075f;
            var boot = PixelImage.Rgb(36, 30, 26);
            var wound = PixelImage.Rgb(86, 20, 18);
            var parts = new System.Collections.Generic.List<Part>();
            // Shuffling feet.
            parts.Add(Limb((0.02f + step * stride) * k, 0.065f * k, 0.05f, (0.1f + step * stride) * k, 0.072f * k, 0.05f, 0.03f * k, Mat.Boot, boot));
            parts.Add(Limb((0.02f - step * stride) * k, -0.065f * k, 0.05f, (0.1f - step * stride) * k, -0.06f * k, 0.05f, 0.03f * k, Mat.Boot, boot));
            // A hunched body in rags, with a wound or two.
            parts.Add(Body(hunch * k, 0f, 1.17f, (brute ? 0.12f : 0.09f) * k, (brute ? 0.2f : 0.15f) * k, 0.19f, Mat.Rag, cloth));
            parts.Add(Ball((hunch - 0.02f) * k, 0.07f * k, 1.31f, 0.03f * k, Mat.Wound, wound));
            if (brute) parts.Add(Ball((hunch + 0.03f) * k, -0.11f * k, 1.29f, 0.035f * k, Mat.Wound, wound));

            // Arms reaching out in front; swinging a little while walking; drawn back, then thrust out, to attack.
            float arm = (brute ? 0.055f : 0.04f) * k, hand = (brute ? 0.045f : 0.03f) * k;
            for (int side = -1; side <= 1; side += 2)
            {
                float sway = frame < WalkFrames ? side * step * 0.045f : 0f;
                Vector3 shoulder = new Vector3(hunch * k, side * (brute ? 0.19f : 0.14f) * k, 1.29f);
                Vector3 elbow, wrist;
                if (frame == 4)
                {
                    elbow = new Vector3(0.04f * k, side * 0.21f * k, 1.38f);
                    wrist = new Vector3(0.12f * k, side * 0.23f * k, 1.48f);
                }
                else if (frame == 5)
                {
                    elbow = new Vector3((hunch + 0.2f) * k, side * 0.12f * k, 1.24f);
                    wrist = new Vector3((hunch + 0.4f) * k, side * 0.07f * k, 1.2f);
                }
                else
                {
                    elbow = new Vector3((0.16f + sway) * k, side * 0.15f * k, 1.23f);
                    wrist = new Vector3((0.31f + sway * 1.6f) * k, side * 0.1f * k, 1.2f);
                }
                parts.Add(Limb(shoulder.x, shoulder.y, shoulder.z, elbow.x, elbow.y, elbow.z, arm, Mat.Rag, cloth));
                parts.Add(Limb(elbow.x, elbow.y, elbow.z, wrist.x, wrist.y, wrist.z, arm * 0.85f, Mat.Rot, skin));
                parts.Add(Ball(wrist.x + 0.02f * k, wrist.y, wrist.z, hand, Mat.Rot, skin));
            }
            // The head, pushed forward; patchy hair.
            float headR = (brute ? 0.062f : 0.07f) * k;
            parts.Add(Ball((hunch + 0.07f) * k, 0f, 1.47f, headR, Mat.Rot, skin));
            parts.Add(Ball((hunch + 0.045f) * k, 0.015f * k, 1.49f, headR * 0.62f, Mat.Hair, PixelImage.Rgb(58, 50, 42)));
            // The arms reach far out in front when it strikes, so the picture reaches further forward than back.
            return Figure(ppu, 0.17f * k, 0.62f * k, 0.3f * k, light, parts);
        }

        // Someone lying on their back seen from above: head to the right, one arm flung out,
        // the other at their side, legs a little apart.
        static PixelImage Lying(int ppu, Vector3 light, Color32 skin, Color32 cloth, Color32 boot, Mat skinMat, Mat clothMat, float k, Color32? cape,
            Color32 hair)
        {
            var parts = new System.Collections.Generic.List<Part>();
            if (cape.HasValue) parts.Add(Body(-0.02f * k, 0.03f * k, 0.02f, 0.3f * k, 0.25f * k, 0.035f, Mat.Cape, cape.Value));
            var legs = PixelImage.Scale(cloth, 0.8f);
            // Legs: thigh and shin, boots pointing up.
            foreach (var (hipY, kneeY, ankleY, footX) in new[] { (0.06f, 0.1f, 0.13f, -0.53f), (-0.06f, -0.07f, -0.05f, -0.5f) })
            {
                parts.Add(Limb(-0.1f * k, hipY * k, 0.1f, -0.29f * k, kneeY * k, 0.09f, 0.052f * k, clothMat, legs));
                parts.Add(Limb(-0.29f * k, kneeY * k, 0.09f, (footX + 0.06f) * k, ankleY * k, 0.07f, 0.045f * k, clothMat, legs));
                parts.Add(Limb((footX + 0.05f) * k, ankleY * k, 0.07f, footX * k, ankleY * k, 0.1f, 0.04f * k, Mat.Boot, boot));
            }
            parts.Add(Body(-0.07f * k, 0f, 0.1f, 0.09f * k, 0.12f * k, 0.07f, clothMat, PixelImage.Scale(cloth, 0.9f)));   // hips
            parts.Add(Body(0.07f * k, 0f, 0.12f, 0.15f * k, 0.15f * k, 0.09f, clothMat, cloth));                           // belly and chest
            parts.Add(Body(0.16f * k, 0f, 0.13f, 0.07f * k, 0.18f * k, 0.08f, clothMat, cloth));                           // shoulders
            // One arm flung up past the head, the other down at the side.
            parts.Add(Limb(0.17f * k, 0.15f * k, 0.12f, 0.27f * k, 0.3f * k, 0.08f, 0.043f * k, clothMat, cloth));
            parts.Add(Limb(0.27f * k, 0.3f * k, 0.08f, 0.38f * k, 0.39f * k, 0.06f, 0.036f * k, skinMat, skin));
            parts.Add(Ball(0.41f * k, 0.41f * k, 0.06f, 0.032f * k, skinMat, skin));
            parts.Add(Limb(0.17f * k, -0.15f * k, 0.12f, 0.04f * k, -0.28f * k, 0.08f, 0.043f * k, clothMat, cloth));
            parts.Add(Limb(0.04f * k, -0.28f * k, 0.08f, -0.09f * k, -0.33f * k, 0.06f, 0.036f * k, skinMat, skin));
            parts.Add(Ball(-0.12f * k, -0.34f * k, 0.06f, 0.032f * k, skinMat, skin));
            // Neck, head (face up) and hair around the back of it.
            parts.Add(Limb(0.21f * k, 0f, 0.12f, 0.28f * k, 0.005f * k, 0.11f, 0.035f * k, skinMat, skin));
            parts.Add(Ball(0.36f * k, 0.012f * k, 0.09f, 0.071f * k, Mat.Hair, hair));
            parts.Add(Ball(0.33f * k, 0.01f * k, 0.12f, 0.066f * k, skinMat, skin));
            return Figure(ppu, 0.6f * k, 0.5f * k, 0.48f * k, light, parts);
        }

        // The materials people are made of.
        enum Mat { Cape, Hem, Tunic, Gold, Steel, Fur, Hair, Skin, Glove, Boot, Sack, Rope, Cap, Rot, Rag, Wound }

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
            return Figure(ppu, size, size, size / 2f, size / 2f, light, parts, royal);
        }

        /// <summary>A figure whose parts reach <paramref name="back"/> tiles behind its middle, <paramref name="front"/> in front and <paramref name="side"/> to each side (the middle is the picture's pivot).</summary>
        static PixelImage Figure(int ppu, float back, float front, float side, Vector3 light, System.Collections.Generic.List<Part> parts) =>
            Figure(ppu, Mathf.CeilToInt((back + front) * ppu), Mathf.CeilToInt(side * 2f * ppu), back * ppu, side * ppu, light, parts, false);

        // The figure on a canvas of cw x ch pixels, its middle at (cx, cy) pixels.
        static PixelImage Figure(int ppu, int cw, int ch, float cx, float cy, Vector3 light, System.Collections.Generic.List<Part> parts, bool royal)
        {
            var img = new PixelImage(cw, ch) { Pivot = new Vector2(cx / cw, cy / ch) };
            float px1 = 1f / ppu;
            var height = new float[cw * ch];
            var which = new int[cw * ch];
            var cover = new float[cw * ch];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    float wx = (x + 0.5f - cx) / ppu, wy = (y + 0.5f - cy) / ppu;
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
                    int idx = y * cw + x;
                    height[idx] = best;
                    which[idx] = top;
                    cover[idx] = top == -1 ? 0f : coverage;
                }

            var blurred = PixelImage.BlurClamped(height, cw, ch, Mathf.Max(1, Mathf.RoundToInt(0.02f * ppu)));
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    int idx = y * cw + x;
                    if (which[idx] == -1) continue;
                    float hx = (H(height, which, cw, ch, x + 1, y, height[idx]) - H(height, which, cw, ch, x - 1, y, height[idx])) * 0.5f * ppu;
                    float hy = (H(height, which, cw, ch, x, y + 1, height[idx]) - H(height, which, cw, ch, x, y - 1, height[idx])) * 0.5f * ppu;
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

                    float wx = (x + 0.5f - cx) / ppu, wy = (y + 0.5f - cy) / ppu;
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
                            // Strands running out from the top of the head.
                            var head = parts[which[idx]].A;
                            float a = Mathf.Atan2(wy - head.y, wx - head.x);
                            float r = Mathf.Sqrt((wx - head.x) * (wx - head.x) + (wy - head.y) * (wy - head.y));
                            texture = (Noise.Value(a * 40f, r * 200f, 59u) - 0.5f) * 0.3f;
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
                        case Mat.Rot:
                        {
                            // Grey-green rotting skin: bruised blotches, dark veins, a clammy sheen.
                            float blotch = Noise.Value(wx * 120f, wy * 120f, 63u);
                            float vein = Mathf.Abs(Noise.Value(wx * 420f, wy * 420f, 64u) - 0.5f);
                            texture = (blotch - 0.5f) * 0.22f - (vein < 0.025f ? 0.14f : 0f);
                            baseColor = PixelImage.Mix(baseColor, PixelImage.Rgb(82, 60, 58), PixelImage.Smooth(0.62f, 0.82f, blotch) * 0.55f);
                            shine = 0.16f; shininess = 14f;
                            break;
                        }
                        case Mat.Rag:
                        {
                            // Torn, filthy clothes: dark tears, old bloodstains.
                            float tear = Noise.Fractal(wx * 55f, wy * 55f, 65u, 2);
                            texture = Mathf.Sin(wx * 2400f) * Mathf.Sin(wy * 2400f) * 0.03f + (Noise.Value(wx * 260f, wy * 260f, 66u) - 0.5f) * 0.12f;
                            if (tear > 0.7f) baseColor = PixelImage.Mix(baseColor, PixelImage.Rgb(28, 22, 20), PixelImage.Smooth(0.7f, 0.74f, tear));
                            else if (tear < 0.3f) baseColor = PixelImage.Mix(baseColor, PixelImage.Rgb(74, 18, 16), PixelImage.Smooth(0.3f, 0.26f, tear) * 0.65f);
                            break;
                        }
                        case Mat.Wound:
                            texture = (Noise.Value(wx * 700f, wy * 700f, 67u) - 0.5f) * 0.25f;
                            shine = 0.4f; shininess = 20f;
                            break;
                    }
                    float shade = (0.32f + diffuse * 0.95f) * (1f + cavity * 0.6f) + texture;
                    var col = PixelImage.Scale(baseColor, shade);
                    if (shine > 0f) col = PixelImage.Mix(col, PixelImage.Rgb(255, 244, 214), Mathf.Pow(Mathf.Max(0f, reflect), shininess) * shine);
                    col.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(cover[idx]) * 255f);
                    img.Pixels[idx] = col;
                }

            if (royal) Crown(img, ppu, light, cx, cy);
            return img;
        }

        static float H(float[] height, int[] which, int cw, int ch, int x, int y, float fallback)
        {
            if (x < 0 || y < 0 || x >= cw || y >= ch) return fallback - 0.05f;
            int i = y * cw + x;
            return which[i] == -1 ? fallback - 0.05f : height[i];
        }

        // A gold crown on the ruler's head: a ring with five points and jewels.
        static void Crown(PixelImage img, int ppu, Vector3 light, float cx, float cy)
        {
            float hx = cx + 0.022f * ppu, hy = cy;
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
