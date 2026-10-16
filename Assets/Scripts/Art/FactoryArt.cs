using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Art
{
    // The factory's pictures (Building/Factory.cs): belts for each age (straight and turning left or right, for each
    // way they can face), arms (a base, and the part that swings round), furnaces, assemblers, drills (and their bits),
    // and the items they move.
    public static partial class FurnitureArt
    {
        // ------------------------------------------------------------- belts --

        /// <summary>The belt kinds, one for each age that has one: log chute, sledge track, roller track, canvas belt,
        /// conveyor belt, fast belt, express belt.</summary>
        public const int BeltStyles = 7;

        /// <summary>A belt's picture key (its turned pictures, and its curves: <c>/CurveL</c>, <c>/CurveR</c>).</summary>
        public static string Belt(int style) => $"Factory/Belt{style}";

        struct BeltLook
        {
            public Color32 Rail, Top, Slat, Arrow;
            public Surface RailSurface, TopSurface;
            public int Slats;      // 0 none (a plank chute), 1 seams, 2 logs or rollers
            public bool Arrows;
        }

        static BeltLook BeltLookOf(int style)
        {
            switch (style)
            {
                case 0: return new BeltLook { Rail = PixelImage.Rgb(80, 58, 38), Top = PixelImage.Rgb(146, 108, 70), RailSurface = Surface.Wood, TopSurface = Surface.Wood };
                case 1: return new BeltLook { Rail = PixelImage.Rgb(96, 70, 44), Top = PixelImage.Rgb(86, 70, 50), Slat = PixelImage.Rgb(132, 96, 60), RailSurface = Surface.Wood, TopSurface = Surface.Stone, Slats = 2 };
                case 2: return new BeltLook { Rail = PixelImage.Rgb(104, 74, 48), Top = PixelImage.Rgb(54, 44, 34), Slat = PixelImage.Rgb(156, 118, 76), RailSurface = Surface.Wood, TopSurface = Surface.Plain, Slats = 2 };
                case 3: return new BeltLook { Rail = PixelImage.Rgb(110, 80, 50), Top = PixelImage.Rgb(176, 160, 124), Slat = PixelImage.Rgb(140, 124, 94), RailSurface = Surface.Wood, TopSurface = Surface.Cloth, Slats = 1 };
                case 4: return new BeltLook { Rail = PixelImage.Rgb(88, 90, 94), Top = PixelImage.Rgb(38, 38, 40), Slat = PixelImage.Rgb(70, 70, 74), RailSurface = Surface.Metal, TopSurface = Surface.Plain, Slats = 1 };
                case 5: return new BeltLook { Rail = PixelImage.Rgb(156, 64, 46), Top = PixelImage.Rgb(38, 38, 40), Slat = PixelImage.Rgb(70, 70, 74), RailSurface = Surface.Metal, TopSurface = Surface.Plain, Slats = 1, Arrows = true, Arrow = PixelImage.Rgb(214, 206, 196) };
                default: return new BeltLook { Rail = PixelImage.Rgb(58, 92, 156), Top = PixelImage.Rgb(34, 34, 36), Slat = PixelImage.Rgb(66, 66, 70), RailSurface = Surface.Metal, TopSurface = Surface.Plain, Slats = 1, Arrows = true, Arrow = PixelImage.Rgb(232, 192, 64) };
            }
        }

        /// <summary>
        /// A tile of belt carrying things to the right, turned to <paramref name="rotation"/>: straight
        /// (<paramref name="turn"/> 0), or a curve coming in from the top (1, turning left) or the bottom (2, turning right).
        /// Rails along both sides, the moving top between, its slats, seams or rollers across, and arrows on the fast ones.
        /// </summary>
        public static PixelImage BeltPiece(int ppu, int style, int turn, int rotation)
        {
            float t = ppu;
            var look = BeltLookOf(style);
            var img = Canvas(1f, 1f, t);
            uint seed = 4100u + (uint)style * 13u;
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float px = (x + 0.5f) / t, py = (y + 0.5f) / t;
                    float u, v;   // along the way things go (tiles), and across it (0..1)
                    if (turn == 0)
                    {
                        u = px;
                        v = py;
                    }
                    else
                    {
                        // Round the corner it turns about: the top-right one (turning left) or the bottom-right one.
                        float dx = px - 1f, dy = py - (turn == 1 ? 1f : 0f);
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = turn == 1 ? Mathf.Atan2(-dy, -dx) : Mathf.Atan2(dy, -dx);
                        if (a < -0.02f || a > Mathf.PI * 0.5f + 0.02f) continue;
                        u = a * 0.5f;
                        v = r;
                    }
                    float edge = Mathf.Min(v - 0.06f, 0.94f - v);
                    float coverage = Mathf.Clamp01(edge * t + 0.5f);
                    if (coverage <= 0f) continue;
                    Color32 c;
                    float h;
                    if (v < 0.16f || v > 0.84f)
                    {
                        // A rail, rounded on top.
                        float across = v < 0.16f ? (v - 0.06f) / 0.1f : (0.94f - v) / 0.1f;
                        c = PixelImage.Scale(look.Rail, Texture(look.RailSurface, u, v, seed, true));
                        h = 0.07f + 0.03f * Mathf.Sin(Mathf.Clamp01(across) * Mathf.PI);
                    }
                    else
                    {
                        c = PixelImage.Scale(look.Top, Texture(look.TopSurface, u, v * 0.6f, seed + 1u, true));
                        h = 0.055f;
                        float k = u * 4f;
                        float d = Mathf.Abs(k - Mathf.Floor(k) - 0.5f);   // 0 between slats, 0.5 at one
                        if (look.Slats == 2)
                        {
                            // Logs or rollers across it: rounded, with dark gaps between.
                            float round = Mathf.Cos(d * Mathf.PI);
                            c = PixelImage.Scale(look.Slat, 0.55f + 0.55f * round);
                            h = 0.05f + 0.025f * round;
                        }
                        else if (look.Slats == 1 && d > 0.44f)
                        {
                            c = PixelImage.Scale(look.Slat, 0.9f);
                            h = 0.05f;
                        }
                        if (look.Arrows)
                        {
                            // Chevrons pointing the way things go.
                            float w = Mathf.Abs(v - 0.5f);
                            float f = u * 2f + w * 1.3f;   // (the tips lead: they point the way things go)
                            float frac = f - Mathf.Floor(f);
                            if (w < 0.24f && frac < 0.12f)
                            {
                                c = look.Arrow;
                                h = 0.058f;
                            }
                        }
                    }
                    img.Blend(x, y, c, coverage);
                    Lift(img, x, y, h * t, coverage);
                }
            return Finish(img, t, rotation, 0.6f, 0.4f);
        }

        // -------------------------------------------------------------- arms --

        /// <summary>How far an arm's hand reaches from its middle, in tiles (to near the middle of the next tile).</summary>
        public const float ArmReach = 0.62f;

        /// <summary>The arm kinds: wooden (burner), iron (burner), electric, fast.</summary>
        public const int ArmStyles = 4;
        public static string ArmBase(int style) => $"Factory/Arm{style}";

        static void ArmColors(int style, out Color32 frame, out Color32 limb, out Surface surface)
        {
            switch (style)
            {
                case 0: frame = PixelImage.Rgb(110, 80, 52); limb = PixelImage.Rgb(140, 104, 66); surface = Surface.Wood; break;
                case 1: frame = PixelImage.Rgb(70, 70, 76); limb = PixelImage.Rgb(104, 104, 110); surface = Surface.Metal; break;
                case 2: frame = PixelImage.Rgb(84, 84, 88); limb = PixelImage.Rgb(214, 176, 52); surface = Surface.Metal; break;
                default: frame = PixelImage.Rgb(80, 84, 92); limb = PixelImage.Rgb(70, 120, 196); surface = Surface.Metal; break;
            }
        }

        /// <summary>An arm's base, facing right (the way it puts things): a round turntable on a plate, and a mark at the front.</summary>
        public static PixelImage ArmBasePiece(int ppu, int style, int rotation)
        {
            float t = ppu;
            ArmColors(style, out var frame, out var limb, out var surface);
            var img = Canvas(1f, 1f, t);
            uint s = 4300u + (uint)style * 7u;
            Block(img, t, 0.18f, 0.18f, 0.82f, 0.82f, PixelImage.Scale(frame, 0.85f), 0.06f, 0.03f, surface, s);
            foreach (var (bx, by) in new[] { (0.25f, 0.25f), (0.75f, 0.25f), (0.25f, 0.75f), (0.75f, 0.75f) }) Stud(img, t, bx, by, 0.022f, Iron, 0.07f);
            Round(img, t, 0.5f, 0.5f, 0.2f, frame, 0.1f, 0.02f, surface, s + 1);
            // The way it puts things: a notch and an arrow at the front.
            Rod(img, t, 0.72f, 0.4f, 0.9f, 0.5f, 0.02f, limb, 0.08f, 0.08f);
            Rod(img, t, 0.72f, 0.6f, 0.9f, 0.5f, 0.02f, limb, 0.08f, 0.08f);
            if (style <= 1)
            {
                // A burner's little firebox at the back.
                Block(img, t, 0.06f, 0.36f, 0.24f, 0.64f, PixelImage.Rgb(52, 46, 42), 0.12f, 0.02f, Surface.Metal, s + 2);
                Block(img, t, 0.09f, 0.44f, 0.15f, 0.56f, PixelImage.Rgb(220, 120, 40), 0.125f, 0.005f, Surface.Plain, s + 3);
            }
            return Finish(img, t, rotation);
        }

        /// <summary>
        /// An arm's swinging part, pointing right from its shoulder (the picture's pivot): an upper and a lower arm with
        /// an elbow, and a gripper at <see cref="ArmReach"/> tiles. The game turns it as it swings.
        /// </summary>
        public static PixelImage ArmSwingPiece(int ppu, int style)
        {
            float t = ppu;
            ArmColors(style, out var frame, out var limb, out _);
            const float shoulder = 0.14f, w = 0.92f, h = 0.32f, mid = 0.16f;
            float reach = shoulder + ArmReach;
            var img = Canvas(w, h, t);
            img.Pivot = new Vector2(shoulder / w, mid / h);
            Round(img, t, shoulder, mid, 0.075f, frame, 0.3f, 0.03f, Surface.Metal, 4400u);
            float elbow = shoulder + ArmReach * 0.5f;
            Rod(img, t, shoulder, mid, elbow, mid + 0.02f, 0.028f, limb, 0.32f, 0.34f);
            Rod(img, t, elbow, mid + 0.02f, reach - 0.04f, mid, 0.024f, PixelImage.Scale(limb, 0.92f), 0.34f, 0.33f);
            Stud(img, t, elbow, mid + 0.02f, 0.02f, Iron, 0.36f);
            // The gripper: two fingers.
            Rod(img, t, reach - 0.05f, mid, reach + 0.04f, mid + 0.05f, 0.011f, Iron, 0.33f, 0.32f);
            Rod(img, t, reach - 0.05f, mid, reach + 0.04f, mid - 0.05f, 0.011f, Iron, 0.33f, 0.32f);
            return Finish(img, t, 0);
        }

        // ---------------------------------------------------------- furnaces --

        /// <summary>The furnace kinds: stone (burner), brick (burner, faster), electric.</summary>
        public const int FurnaceStyles = 3;
        public static string FurnaceArt(int style) => $"Factory/Furnace{style}";

        /// <summary>A 2x2 furnace seen from above, its fire glowing inside.</summary>
        public static PixelImage FurnacePiece(int ppu, int style)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            uint s = 4500u + (uint)style * 11u;
            var glow = PixelImage.Rgb(236, 128, 44);
            switch (style)
            {
                case 0:
                    // Stacked stones round a fire pit, its mouth at the front.
                    Round(img, t, 1f, 1f, 0.86f, Granite, 0.55f, 0.08f, Surface.Stone, s);
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i * Mathf.PI * 2f / 14f;
                        Round(img, t, 1f + Mathf.Cos(a) * 0.68f, 1f + Mathf.Sin(a) * 0.68f, 0.17f, PixelImage.Scale(Granite, 0.85f + 0.3f * Noise.Hash01(s, i, 1)), 0.62f, 0.04f, Surface.Stone, s + (uint)i);
                    }
                    Round(img, t, 1f, 1f, 0.46f, PixelImage.Rgb(34, 28, 24), 0.3f, 0f, Surface.Stone, s + 20u);
                    Round(img, t, 1f, 1f, 0.3f, glow, 0.32f, 0.02f, Surface.Plain, s + 21u);
                    Round(img, t, 1f, 1f, 0.14f, PixelImage.Rgb(255, 206, 120), 0.34f, 0.01f, Surface.Plain, s + 22u);
                    Block(img, t, 0.86f, 0.06f, 1.14f, 0.4f, PixelImage.Rgb(30, 24, 22), 0.2f, 0.02f, Surface.Stone, s + 23u);
                    break;
                case 1:
                    // A square brick oven with a chimney and a glowing door.
                    Block(img, t, 0.08f, 0.08f, 1.92f, 1.92f, PixelImage.Rgb(150, 70, 52), 0.75f, 0.06f, Surface.Stone, s, true, 0.12f);
                    Block(img, t, 0.3f, 0.3f, 1.7f, 1.7f, PixelImage.Rgb(122, 58, 44), 0.8f, 0.03f, Surface.Stone, s + 1u, false, 0.12f);
                    Round(img, t, 1.45f, 1.45f, 0.24f, PixelImage.Rgb(60, 54, 50), 1.05f, 0f, Surface.Stone, s + 2u);
                    Round(img, t, 1.45f, 1.45f, 0.14f, PixelImage.Rgb(20, 18, 18), 1.0f, 0f, Surface.Plain, s + 3u);
                    Block(img, t, 0.6f, 0.08f, 1.4f, 0.3f, PixelImage.Rgb(40, 34, 30), 0.5f, 0.02f, Surface.Metal, s + 4u);
                    Block(img, t, 0.68f, 0.1f, 1.32f, 0.22f, glow, 0.52f, 0.01f, Surface.Plain, s + 5u);
                    break;
                default:
                    // A steel box with vents and glowing heating coils behind a grille.
                    Block(img, t, 0.08f, 0.08f, 1.92f, 1.92f, PixelImage.Rgb(108, 112, 118), 0.7f, 0.06f, Surface.Metal, s);
                    Block(img, t, 0.4f, 0.4f, 1.6f, 1.6f, PixelImage.Rgb(40, 40, 44), 0.6f, 0.02f, Surface.Metal, s + 1u);
                    for (int i = 0; i < 4; i++)
                        Rod(img, t, 0.5f, 0.58f + i * 0.28f, 1.5f, 0.58f + i * 0.28f, 0.035f, glow, 0.62f, 0.62f);
                    for (int i = 0; i < 6; i++)
                        Rod(img, t, 0.48f + i * 0.21f, 0.42f, 0.48f + i * 0.21f, 1.58f, 0.012f, PixelImage.Rgb(90, 92, 96), 0.66f, 0.66f);
                    foreach (var (bx, by) in new[] { (0.22f, 0.22f), (1.78f, 0.22f), (0.22f, 1.78f), (1.78f, 1.78f) }) Stud(img, t, bx, by, 0.04f, Iron, 0.72f);
                    break;
            }
            return Finish(img, t, 0);
        }

        // -------------------------------------------------------- assemblers --

        /// <summary>The assembler kinds: workshop (burner), clockwork (burner), assembling machine, assembly robot (electric).</summary>
        public const int AssemblerStyles = 4;
        public static string AssemblerArt(int style) => $"Factory/Assembler{style}";

        // A cog: a ring with teeth round it.
        static void Cog(PixelImage img, float t, float cx, float cy, float r, int teeth, Color32 color, float top, uint seed)
        {
            for (int i = 0; i < teeth; i++)
            {
                float a = i * Mathf.PI * 2f / teeth;
                Rod(img, t, cx + Mathf.Cos(a) * r * 0.8f, cy + Mathf.Sin(a) * r * 0.8f, cx + Mathf.Cos(a) * r * 1.12f, cy + Mathf.Sin(a) * r * 1.12f, r * 0.12f, color, top, top);
            }
            Ring(img, t, cx, cy, r * 0.35f, r * 0.9f, color, top, Surface.Metal, seed);
            Stud(img, t, cx, cy, r * 0.22f, PixelImage.Scale(color, 0.7f), top + 0.01f);
        }

        /// <summary>A 3x3 assembler seen from above: where things are put together, and how it's driven.</summary>
        public static PixelImage AssemblerPiece(int ppu, int style)
        {
            float t = ppu;
            var img = Canvas(3f, 3f, t);
            uint s = 4700u + (uint)style * 13u;
            switch (style)
            {
                case 0:
                {
                    // A timber workshop frame: a worktable with tools, a treadwheel driving it, and a firebox.
                    Block(img, t, 0.1f, 0.1f, 2.9f, 2.9f, PixelImage.Rgb(120, 88, 56), 0.12f, 0.03f, Surface.Wood, s, true, 0.25f);
                    Block(img, t, 0.9f, 0.7f, 2.6f, 1.7f, Oak, 0.45f, 0.03f, Surface.Wood, s + 1u, true, 0.2f);
                    Ring(img, t, 0.7f, 2.1f, 0.42f, 0.56f, DarkWood, 0.8f, Surface.Wood, s + 2u);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        Rod(img, t, 0.7f, 2.1f, 0.7f + Mathf.Cos(a) * 0.45f, 2.1f + Mathf.Sin(a) * 0.45f, 0.025f, Oak, 0.78f, 0.78f);
                    }
                    Block(img, t, 2.2f, 2.1f, 2.75f, 2.75f, PixelImage.Rgb(60, 52, 46), 0.4f, 0.03f, Surface.Stone, s + 3u);
                    Block(img, t, 2.32f, 2.22f, 2.63f, 2.4f, PixelImage.Rgb(226, 124, 42), 0.41f, 0.01f, Surface.Plain, s + 4u);
                    Rod(img, t, 1.2f, 1.2f, 1.6f, 1.3f, 0.02f, Iron, 0.47f, 0.47f);
                    Block(img, t, 1.9f, 0.95f, 2.3f, 1.25f, PixelImage.Rgb(176, 120, 60), 0.5f, 0.02f, Surface.Metal, s + 5u);
                    break;
                }
                case 1:
                {
                    // A brass clockwork: cogs meshing on a plate.
                    var brass = PixelImage.Rgb(184, 146, 70);
                    Block(img, t, 0.1f, 0.1f, 2.9f, 2.9f, PixelImage.Rgb(96, 70, 46), 0.15f, 0.04f, Surface.Wood, s, true, 0.3f);
                    Block(img, t, 0.3f, 0.3f, 2.7f, 2.7f, PixelImage.Scale(brass, 0.7f), 0.25f, 0.04f, Surface.Metal, s + 1u);
                    Cog(img, t, 1.0f, 1.9f, 0.55f, 12, brass, 0.4f, s + 2u);
                    Cog(img, t, 1.95f, 1.6f, 0.45f, 10, PixelImage.Scale(brass, 0.9f), 0.42f, s + 3u);
                    Cog(img, t, 1.3f, 0.85f, 0.38f, 9, PixelImage.Scale(brass, 1.08f), 0.44f, s + 4u);
                    Cog(img, t, 2.2f, 0.75f, 0.28f, 8, brass, 0.46f, s + 5u);
                    break;
                }
                case 2:
                {
                    // A grey steel machine: a recessed work bed, a jointed arm over it, hazard stripes round the edge.
                    var steel = PixelImage.Rgb(116, 120, 126);
                    Block(img, t, 0.06f, 0.06f, 2.94f, 2.94f, steel, 0.6f, 0.08f, Surface.Metal, s);
                    for (int i = 0; i < 12; i++)
                    {
                        float x = 0.2f + i * 0.22f;
                        Block(img, t, x, 0.1f, x + 0.1f, 0.24f, PixelImage.Rgb(220, 180, 40), 0.61f, 0.005f, Surface.Plain, s + 1u);
                    }
                    Block(img, t, 0.45f, 0.45f, 2.55f, 2.55f, PixelImage.Rgb(46, 48, 52), 0.4f, 0.03f, Surface.Metal, s + 2u);
                    Round(img, t, 1.5f, 1.5f, 0.3f, PixelImage.Rgb(90, 94, 100), 0.65f, 0.05f, Surface.Metal, s + 3u);
                    Rod(img, t, 1.5f, 1.5f, 2.1f, 1.95f, 0.07f, PixelImage.Rgb(214, 176, 52), 0.75f, 0.72f);
                    Rod(img, t, 2.1f, 1.95f, 1.75f, 2.3f, 0.055f, PixelImage.Rgb(200, 164, 48), 0.72f, 0.7f);
                    foreach (var (bx, by) in new[] { (0.25f, 0.25f), (2.75f, 0.25f), (0.25f, 2.75f), (2.75f, 2.75f) }) Stud(img, t, bx, by, 0.05f, Iron, 0.65f);
                    break;
                }
                default:
                {
                    // A dark modern cell: two robot arms over a small belt, and a green light.
                    var casing = PixelImage.Rgb(66, 74, 70);
                    Block(img, t, 0.06f, 0.06f, 2.94f, 2.94f, casing, 0.65f, 0.1f, Surface.Metal, s);
                    Block(img, t, 0.35f, 1.25f, 2.65f, 1.75f, PixelImage.Rgb(34, 34, 36), 0.45f, 0.02f, Surface.Plain, s + 1u);
                    for (int i = 0; i < 9; i++) Rod(img, t, 0.4f + i * 0.28f, 1.27f, 0.4f + i * 0.28f, 1.73f, 0.01f, PixelImage.Rgb(70, 70, 74), 0.46f, 0.46f);
                    foreach (var (bx, by, ex, ey) in new[] { (0.9f, 2.4f, 1.3f, 1.6f), (2.1f, 0.6f, 1.7f, 1.4f) })
                    {
                        Round(img, t, bx, by, 0.24f, PixelImage.Rgb(200, 200, 194), 0.75f, 0.05f, Surface.Metal, s + 2u);
                        Rod(img, t, bx, by, ex, ey, 0.06f, PixelImage.Rgb(210, 104, 40), 0.85f, 0.8f);
                        Stud(img, t, ex, ey, 0.05f, Iron, 0.82f);
                    }
                    Stud(img, t, 2.65f, 2.65f, 0.08f, PixelImage.Rgb(90, 220, 110), 0.7f);
                    break;
                }
            }
            return Finish(img, t, 0);
        }

        // ------------------------------------------------------------ drills --

        /// <summary>The drill kinds: digging frame (2x2, burner), burner drill (2x2), electric mining drill (3x3), big mining drill (3x3).</summary>
        public const int DrillStyles = 4;
        public static string DrillArt(int style) => $"Factory/Drill{style}";
        /// <summary>How many tiles across a drill of each kind is.</summary>
        public static int DrillSize(int style) => style <= 1 ? 2 : 3;
        static float DrillBitRadius(int style) => style == 0 ? 0.42f : style == 1 ? 0.4f : style == 2 ? 0.72f : 0.86f;

        /// <summary>
        /// A drill seen from above with its chute facing right (on its right side, in the middle row: the lower one on a 2x2,
        /// where Automation/Drill.OutputOf puts what it digs), turned to <paramref name="rotation"/>. Its bit turns in the hole
        /// in its middle (a picture of its own, <see cref="DrillBitPiece"/>).
        /// </summary>
        public static PixelImage DrillPiece(int ppu, int style, int rotation)
        {
            float t = ppu;
            int size = DrillSize(style);
            var img = Canvas(size, size, t);
            uint s = 5100u + (uint)style * 17u;
            float c = size / 2f, chute = size == 2 ? 0.5f : 1.5f, bit = DrillBitRadius(style);
            var glow = PixelImage.Rgb(236, 128, 44);
            switch (style)
            {
                case 0:
                {
                    // A timber frame round a pit, a little fire at the back to drive it, a plank chute at the front.
                    Block(img, t, 0.12f, 0.12f, 1.88f, 1.88f, PixelImage.Rgb(74, 60, 44), 0.04f, 0.02f, Surface.Stone, s);
                    foreach (var (x0, y0, x1, y1) in new[] { (0.2f, 0.2f, 1.8f, 0.2f), (0.2f, 1.8f, 1.8f, 1.8f), (0.2f, 0.2f, 0.2f, 1.8f), (1.8f, 0.2f, 1.8f, 1.8f) })
                        Rod(img, t, x0, y0, x1, y1, 0.075f, Oak, 0.18f, 0.18f);
                    foreach (var (bx, by) in new[] { (0.2f, 0.2f), (1.8f, 0.2f), (0.2f, 1.8f), (1.8f, 1.8f) })
                        Round(img, t, bx, by, 0.13f, DarkWood, 0.32f, 0.03f, Surface.Wood, s + 1u);
                    Block(img, t, 0.3f, 1.28f, 0.7f, 1.68f, Granite, 0.22f, 0.04f, Surface.Stone, s + 2u);
                    Block(img, t, 0.38f, 1.38f, 0.62f, 1.58f, glow, 0.23f, 0.01f, Surface.Plain, s + 3u);
                    Block(img, t, 1.5f, chute - 0.2f, 2f, chute + 0.2f, Oak, 0.2f, 0.02f, Surface.Wood, s + 4u, true, 0.1f);
                    Block(img, t, 1.55f, chute - 0.12f, 1.98f, chute + 0.12f, PixelImage.Scale(Oak, 0.7f), 0.21f, 0.01f, Surface.Wood, s + 5u);
                    break;
                }
                case 1:
                {
                    // A riveted iron box, its firebox glowing at the back, a stubby chimney, an iron chute at the front.
                    var plate = PixelImage.Rgb(86, 84, 82);
                    Block(img, t, 0.08f, 0.08f, 1.92f, 1.92f, plate, 0.42f, 0.05f, Surface.Metal, s);
                    for (int i = 0; i < 7; i++)
                    {
                        float a = 0.2f + i * 0.267f;
                        foreach (var (bx, by) in new[] { (a, 0.17f), (a, 1.83f), (0.17f, a), (1.83f, a) }) Stud(img, t, bx, by, 0.022f, Iron, 0.45f);
                    }
                    Block(img, t, 0.2f, 0.62f, 0.5f, 1.38f, PixelImage.Rgb(46, 42, 40), 0.44f, 0.02f, Surface.Metal, s + 1u);
                    Block(img, t, 0.24f, 0.78f, 0.4f, 1.22f, glow, 0.445f, 0.01f, Surface.Plain, s + 2u);
                    Round(img, t, 0.45f, 1.55f, 0.15f, PixelImage.Rgb(60, 56, 54), 0.62f, 0f, Surface.Metal, s + 3u);
                    Round(img, t, 0.45f, 1.55f, 0.09f, PixelImage.Rgb(16, 14, 14), 0.6f, 0f, Surface.Plain, s + 4u);
                    Block(img, t, 1.5f, chute - 0.22f, 2f, chute + 0.22f, PixelImage.Rgb(70, 70, 74), 0.32f, 0.03f, Surface.Metal, s + 5u);
                    Block(img, t, 1.55f, chute - 0.13f, 1.98f, chute + 0.13f, PixelImage.Rgb(40, 40, 44), 0.33f, 0.01f, Surface.Metal, s + 6u);
                    break;
                }
                case 2:
                {
                    // A grey steel housing on four feet, a motor with cooling fins at the back, yellow and black stripes, a chute.
                    var steel = PixelImage.Rgb(112, 116, 122);
                    foreach (var (bx, by) in new[] { (0.22f, 0.22f), (2.78f, 0.22f), (0.22f, 2.78f), (2.78f, 2.78f) })
                        Block(img, t, bx - 0.18f, by - 0.18f, bx + 0.18f, by + 0.18f, PixelImage.Rgb(70, 72, 76), 0.3f, 0.03f, Surface.Metal, s + 1u);
                    Block(img, t, 0.16f, 0.16f, 2.84f, 2.84f, steel, 0.55f, 0.08f, Surface.Metal, s);
                    for (int i = 0; i < 10; i++)
                    {
                        float x = 0.3f + i * 0.24f;
                        Block(img, t, x, 0.2f, x + 0.12f, 0.32f, PixelImage.Rgb(220, 180, 40), 0.56f, 0.005f, Surface.Plain, s + 2u);
                    }
                    Block(img, t, 0.3f, 2.1f, 1.1f, 2.7f, PixelImage.Rgb(58, 62, 70), 0.75f, 0.05f, Surface.Metal, s + 3u);
                    for (int i = 0; i < 6; i++) Rod(img, t, 0.38f + i * 0.13f, 2.16f, 0.38f + i * 0.13f, 2.64f, 0.018f, PixelImage.Rgb(96, 100, 108), 0.79f, 0.79f);
                    Block(img, t, 2.4f, chute - 0.3f, 3f, chute + 0.3f, PixelImage.Rgb(80, 84, 90), 0.42f, 0.04f, Surface.Metal, s + 4u);
                    Block(img, t, 2.46f, chute - 0.18f, 2.98f, chute + 0.18f, PixelImage.Rgb(36, 36, 40), 0.43f, 0.01f, Surface.Metal, s + 5u);
                    Stud(img, t, 2.6f, 2.6f, 0.06f, PixelImage.Rgb(90, 220, 110), 0.6f);
                    break;
                }
                default:
                {
                    // A big olive-green machine: heavy frame, hazard stripes all round, twin motors, a wide chute.
                    var olive = PixelImage.Rgb(84, 96, 74);
                    Block(img, t, 0.04f, 0.04f, 2.96f, 2.96f, PixelImage.Rgb(52, 56, 50), 0.4f, 0.05f, Surface.Metal, s);
                    Block(img, t, 0.2f, 0.2f, 2.8f, 2.8f, olive, 0.62f, 0.08f, Surface.Metal, s + 1u);
                    for (int i = 0; i < 12; i++)
                    {
                        float a = 0.12f + i * 0.235f;
                        Block(img, t, a, 0.06f, a + 0.11f, 0.17f, PixelImage.Rgb(222, 184, 44), 0.42f, 0.005f, Surface.Plain, s + 2u);
                        Block(img, t, a, 2.83f, a + 0.11f, 2.94f, PixelImage.Rgb(222, 184, 44), 0.42f, 0.005f, Surface.Plain, s + 2u);
                    }
                    foreach (float my in new[] { 0.55f, 2.45f })
                    {
                        Round(img, t, 0.55f, my, 0.26f, PixelImage.Rgb(60, 64, 60), 0.82f, 0.04f, Surface.Metal, s + 3u);
                        Ring(img, t, 0.55f, my, 0.12f, 0.2f, PixelImage.Rgb(110, 116, 106), 0.88f, Surface.Metal, s + 4u);
                    }
                    Block(img, t, 2.3f, chute - 0.42f, 3f, chute + 0.42f, PixelImage.Rgb(70, 76, 66), 0.5f, 0.05f, Surface.Metal, s + 5u);
                    Block(img, t, 2.38f, chute - 0.28f, 2.98f, chute + 0.28f, PixelImage.Rgb(30, 32, 30), 0.51f, 0.01f, Surface.Metal, s + 6u);
                    Stud(img, t, 2.6f, 0.45f, 0.06f, PixelImage.Rgb(240, 90, 60), 0.7f);
                    break;
                }
            }
            // The hole its bit turns in, with a collar round it.
            Round(img, t, c, c, bit + 0.05f, PixelImage.Rgb(26, 22, 20), 0.05f, 0f, Surface.Stone, s + 9u);
            Ring(img, t, c, c, bit + 0.04f, bit + 0.12f, style == 0 ? DarkWood : PixelImage.Rgb(72, 74, 78), style == 0 ? 0.24f : 0.6f, style == 0 ? Surface.Wood : Surface.Metal, s + 10u);
            return Finish(img, t, rotation);
        }

        /// <summary>A drill's bit seen from above, centred: it turns while the drill digs (Building/Factory.cs).</summary>
        public static PixelImage DrillBitPiece(int ppu, int style)
        {
            float t = ppu;
            float r = DrillBitRadius(style);
            var img = Canvas(r * 2f + 0.04f, r * 2f + 0.04f, t);
            float c = r + 0.02f;
            uint s = 5300u + (uint)style * 7u;
            switch (style)
            {
                case 0:
                    // A wooden cross with stone-tipped blades.
                    Round(img, t, c, c, r * 0.3f, Oak, 0.3f, 0.04f, Surface.Wood, s);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * 0.5f;
                        float ex = c + Mathf.Cos(a) * r * 0.82f, ey = c + Mathf.Sin(a) * r * 0.82f;
                        Rod(img, t, c, c, ex, ey, 0.05f, Oak, 0.28f, 0.24f);
                        Round(img, t, ex, ey, 0.08f, Granite, 0.26f, 0.03f, Surface.Stone, s + (uint)i);
                    }
                    Stud(img, t, c, c, 0.06f, Iron, 0.34f);
                    break;
                default:
                {
                    // A steel cutting head: a disc with curved blades and teeth round the edge.
                    var metal = style == 1 ? PixelImage.Rgb(96, 96, 100) : style == 2 ? PixelImage.Rgb(150, 154, 160) : PixelImage.Rgb(130, 136, 128);
                    Round(img, t, c, c, r * 0.92f, PixelImage.Scale(metal, 0.7f), 0.2f, 0.02f, Surface.Metal, s);
                    int blades = style == 1 ? 3 : 4;
                    for (int b = 0; b < blades; b++)
                    {
                        float a0 = b * Mathf.PI * 2f / blades;
                        for (int k = 0; k < 6; k++)
                        {
                            float f0 = k / 6f, f1 = (k + 1) / 6f;
                            float r0 = r * (0.2f + 0.7f * f0), r1 = r * (0.2f + 0.7f * f1);
                            float b0 = a0 + f0 * 1.1f, b1 = a0 + f1 * 1.1f;
                            Rod(img, t, c + Mathf.Cos(b0) * r0, c + Mathf.Sin(b0) * r0, c + Mathf.Cos(b1) * r1, c + Mathf.Sin(b1) * r1, r * 0.07f, metal, 0.3f, 0.3f);
                        }
                    }
                    int teeth = style == 1 ? 8 : 12;
                    for (int i = 0; i < teeth; i++)
                    {
                        float a = i * Mathf.PI * 2f / teeth;
                        Stud(img, t, c + Mathf.Cos(a) * r * 0.86f, c + Mathf.Sin(a) * r * 0.86f, r * 0.07f, PixelImage.Rgb(200, 196, 180), 0.32f);
                    }
                    Round(img, t, c, c, r * 0.22f, PixelImage.Scale(metal, 1.1f), 0.36f, 0.04f, Surface.Metal, s + 1u);
                    break;
                }
            }
            return Finish(img, t, 0);
        }

        // ------------------------------------------------------------- items --

        /// <summary>An item as it rides a belt (about a third of a tile), seen from above.</summary>
        public static PixelImage ItemPiece(int ppu, ResourceType type)
        {
            float t = ppu;
            const float size = 0.34f, c = size / 2f;
            var img = Canvas(size, size, t);
            uint s = 4900u + (uint)type * 7u;
            Color32 Ore(int r, int g, int b) => PixelImage.Rgb(r, g, b);
            switch (type)
            {
                case ResourceType.Wood:
                    Rod(img, t, 0.05f, c - 0.03f, 0.29f, c + 0.03f, 0.055f, PixelImage.Rgb(122, 86, 52), 0.1f, 0.1f);
                    Round(img, t, 0.29f, c + 0.03f, 0.05f, PixelImage.Rgb(196, 160, 112), 0.11f, 0f, Surface.Wood, s);
                    break;
                case ResourceType.Stone:
                    Round(img, t, c, c, 0.12f, Granite, 0.1f, 0.04f, Surface.Stone, s);
                    Round(img, t, c + 0.04f, c + 0.03f, 0.06f, PixelImage.Scale(Granite, 1.15f), 0.13f, 0.02f, Surface.Stone, s + 1u);
                    break;
                case ResourceType.Food:
                    foreach (var (bx, by) in new[] { (c - 0.05f, c - 0.03f), (c + 0.05f, c - 0.03f), (c, c + 0.06f) })
                        Round(img, t, bx, by, 0.055f, PixelImage.Rgb(176, 40, 52), 0.08f, 0.03f, Surface.Wax, s);
                    Rod(img, t, c, c + 0.06f, c + 0.04f, c + 0.13f, 0.01f, PixelImage.Rgb(70, 110, 50), 0.12f, 0.12f);
                    break;
                case ResourceType.Copper:
                    Block(img, t, 0.06f, 0.06f, 0.28f, 0.28f, PixelImage.Rgb(196, 112, 62), 0.05f, 0.015f, Surface.Metal, s);
                    break;
                case ResourceType.Iron:
                    Block(img, t, 0.06f, 0.06f, 0.28f, 0.28f, PixelImage.Rgb(150, 154, 162), 0.05f, 0.015f, Surface.Metal, s);
                    break;
                case ResourceType.Gold:
                    Round(img, t, c, c, 0.09f, PixelImage.Rgb(232, 192, 64), 0.08f, 0.03f, Surface.Metal, s);
                    break;
                case ResourceType.Steel:
                    Block(img, t, 0.04f, 0.11f, 0.3f, 0.23f, PixelImage.Rgb(178, 186, 198), 0.07f, 0.02f, Surface.Metal, s);
                    break;
                case ResourceType.Coal:
                    Round(img, t, c, c, 0.11f, PixelImage.Rgb(34, 32, 34), 0.09f, 0.04f, Surface.Stone, s);
                    Round(img, t, c - 0.03f, c + 0.04f, 0.05f, PixelImage.Rgb(52, 50, 54), 0.12f, 0.02f, Surface.Stone, s + 1u);
                    break;
                case ResourceType.Rope:
                    Ring(img, t, c, c, 0.05f, 0.12f, PixelImage.Rgb(176, 146, 94), 0.07f, Surface.Cloth, s);
                    break;
                case ResourceType.BronzeBar:
                case ResourceType.IronBar:
                {
                    var bar = type == ResourceType.BronzeBar ? PixelImage.Rgb(194, 132, 62) : PixelImage.Rgb(116, 118, 126);
                    Block(img, t, 0.05f, 0.1f, 0.29f, 0.24f, PixelImage.Scale(bar, 0.85f), 0.05f, 0.02f, Surface.Metal, s);
                    Block(img, t, 0.08f, 0.13f, 0.26f, 0.21f, bar, 0.07f, 0.02f, Surface.Metal, s + 1u);
                    break;
                }
                case ResourceType.Gear:
                    Cog(img, t, c, c, 0.12f, 8, PixelImage.Rgb(136, 138, 146), 0.08f, s);
                    break;
                case ResourceType.Gunpowder:
                    Round(img, t, c, c, 0.12f, PixelImage.Rgb(120, 84, 52), 0.1f, 0.02f, Surface.Wood, s);
                    Ring(img, t, c, c, 0.09f, 0.11f, Iron, 0.11f, Surface.Metal, s + 1u);
                    Round(img, t, c, c, 0.07f, PixelImage.Rgb(40, 38, 40), 0.105f, 0f, Surface.Plain, s + 2u);
                    break;
                case ResourceType.MachineParts:
                    Block(img, t, 0.06f, 0.08f, 0.28f, 0.26f, PixelImage.Rgb(96, 100, 108), 0.05f, 0.02f, Surface.Metal, s);
                    Cog(img, t, 0.12f, 0.2f, 0.06f, 6, PixelImage.Rgb(170, 140, 70), 0.08f, s + 1u);
                    Stud(img, t, 0.23f, 0.13f, 0.025f, Iron, 0.08f);
                    break;
                case ResourceType.Wire:
                    Round(img, t, c, c, 0.12f, PixelImage.Rgb(90, 64, 40), 0.06f, 0f, Surface.Wood, s);
                    Ring(img, t, c, c, 0.05f, 0.11f, PixelImage.Rgb(206, 118, 64), 0.09f, Surface.Metal, s + 1u);
                    break;
                case ResourceType.Electronics:
                    Block(img, t, 0.05f, 0.07f, 0.29f, 0.27f, PixelImage.Rgb(46, 110, 60), 0.04f, 0.01f, Surface.Plain, s);
                    Block(img, t, 0.12f, 0.13f, 0.22f, 0.21f, PixelImage.Rgb(30, 30, 32), 0.06f, 0.01f, Surface.Plain, s + 1u);
                    foreach (var (bx, by) in new[] { (0.08f, 0.1f), (0.26f, 0.1f), (0.08f, 0.24f), (0.26f, 0.24f) }) Stud(img, t, bx, by, 0.015f, PixelImage.Rgb(220, 180, 70), 0.05f);
                    break;
                case ResourceType.IronOre:
                case ResourceType.CopperOre:
                {
                    // A rough rock with flecks of the metal in it.
                    var fleck = type == ResourceType.IronOre ? Ore(176, 96, 66) : Ore(70, 170, 150);
                    var rock = type == ResourceType.IronOre ? Ore(110, 100, 96) : Ore(112, 96, 82);
                    Round(img, t, c, c, 0.12f, rock, 0.1f, 0.04f, Surface.Stone, s);
                    for (int i = 0; i < 5; i++)
                        Stud(img, t, c + (Noise.Hash01(s, i, 0) - 0.5f) * 0.14f, c + (Noise.Hash01(s, i, 1) - 0.5f) * 0.14f, 0.018f, fleck, 0.13f);
                    break;
                }
                default:
                    Round(img, t, c, c, 0.1f, PixelImage.Rgb(160, 150, 130), 0.08f, 0.03f, Surface.Plain, s);
                    break;
            }
            return Finish(img, t, 0, 0.7f, 0.4f);
        }
    }
}
