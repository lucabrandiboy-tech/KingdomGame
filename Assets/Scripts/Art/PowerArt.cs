using UnityEngine;

namespace KingdomGame.Art
{
    // Electricity's pictures (Automation/PowerGrid.cs): power poles of each age (their wires are drawn by the game between
    // their middles), the accumulator and the solar panel. Seen from straight above, lit from the top-left.
    public static partial class FurnitureArt
    {
        /// <summary>The pole kinds: wooden (1x1), medium (steel, 1x1), big (a lattice tower, 2x2), substation (2x2).</summary>
        public const int PoleStyles = 4;
        public static string PoleArt(int style) => $"Factory/Pole{style}";
        public static int PoleSize(int style) => style >= 2 ? 2 : 1;
        public const string AccumulatorArt = "Factory/Accumulator";
        public const string SolarArt = "Factory/Solar";

        static readonly Color32 Porcelain = PixelImage.Rgb(214, 210, 198);
        static readonly Color32 GlassGreen = PixelImage.Rgb(96, 150, 120);

        /// <summary>A power pole from above: its post or tower, the crossarm and the insulators the wires hang from.</summary>
        public static PixelImage PolePiece(int ppu, int style)
        {
            float t = ppu;
            int size = PoleSize(style);
            var img = Canvas(size, size, t);
            float c = size / 2f;
            uint s = 5500u + (uint)style * 13u;
            switch (style)
            {
                case 0:
                {
                    // A wooden post with a crossarm, braces and three green glass insulators.
                    Round(img, t, c, c, 0.2f, PixelImage.Rgb(96, 70, 46), 0.9f, 0.05f, Surface.Wood, s);
                    Ring(img, t, c, c, 0.12f, 0.2f, PixelImage.Rgb(76, 54, 36), 0.92f, Surface.Wood, s + 3u);
                    Block(img, t, 0.06f, c - 0.075f, 0.94f, c + 0.075f, Oak, 1.05f, 0.03f, Surface.Wood, s + 1u);
                    Rod(img, t, c, c - 0.08f, 0.24f, c - 0.3f, 0.028f, DarkWood, 0.98f, 0.85f);
                    Rod(img, t, c, c - 0.08f, 0.76f, c - 0.3f, 0.028f, DarkWood, 0.98f, 0.85f);
                    foreach (float x in new[] { 0.14f, 0.86f }) Round(img, t, x, c, 0.085f, GlassGreen, 1.12f, 0.05f, Surface.Wax, s + 2u);
                    Round(img, t, c, c + 0.05f, 0.08f, GlassGreen, 1.14f, 0.05f, Surface.Wax, s + 4u);
                    break;
                }
                case 1:
                {
                    // A steel pole: a square lattice mast, a steel crossarm and three white insulators.
                    var steel = PixelImage.Rgb(122, 126, 132);
                    Block(img, t, c - 0.22f, c - 0.22f, c + 0.22f, c + 0.22f, PixelImage.Rgb(70, 72, 78), 0.95f, 0.03f, Surface.Metal, s);
                    Rod(img, t, c - 0.2f, c - 0.2f, c + 0.2f, c + 0.2f, 0.022f, steel, 1f, 1f);
                    Rod(img, t, c - 0.2f, c + 0.2f, c + 0.2f, c - 0.2f, 0.022f, steel, 1f, 1f);
                    Block(img, t, 0.04f, c - 0.065f, 0.96f, c + 0.065f, steel, 1.1f, 0.03f, Surface.Metal, s + 1u);
                    foreach (float x in new[] { 0.12f, 0.5f, 0.88f }) Round(img, t, x, c, 0.085f, Porcelain, 1.2f, 0.05f, Surface.Wax, s + 2u);
                    break;
                }
                case 2:
                {
                    // A lattice tower: four legs splaying out, cross-braced, a wide arm with four insulators at the top.
                    var steel = PixelImage.Rgb(116, 120, 124);
                    foreach (var (lx, ly) in new[] { (0.2f, 0.2f), (1.8f, 0.2f), (0.2f, 1.8f), (1.8f, 1.8f) })
                    {
                        Block(img, t, lx - 0.1f, ly - 0.1f, lx + 0.1f, ly + 0.1f, PixelImage.Rgb(132, 128, 120), 0.08f, 0.02f, Surface.Stone, s);
                        Rod(img, t, lx, ly, c + (lx - c) * 0.3f, c + (ly - c) * 0.3f, 0.03f, steel, 0.1f, 1.3f);
                    }
                    foreach (var (ax, ay, bx, by) in new[] { (0.2f, 0.2f, 1.8f, 1.8f), (0.2f, 1.8f, 1.8f, 0.2f), (0.2f, 0.2f, 1.8f, 0.2f), (0.2f, 1.8f, 1.8f, 1.8f), (0.2f, 0.2f, 0.2f, 1.8f), (1.8f, 0.2f, 1.8f, 1.8f) })
                        Rod(img, t, ax, ay, bx, by, 0.015f, PixelImage.Scale(steel, 0.85f), 0.3f, 0.3f);
                    Block(img, t, c - 0.26f, c - 0.26f, c + 0.26f, c + 0.26f, PixelImage.Rgb(80, 84, 88), 1.35f, 0.03f, Surface.Metal, s + 1u);
                    Block(img, t, 0.1f, c - 0.07f, 1.9f, c + 0.07f, steel, 1.5f, 0.03f, Surface.Metal, s + 2u);
                    foreach (float x in new[] { 0.2f, 0.7f, 1.3f, 1.8f }) Round(img, t, x, c, 0.075f, Porcelain, 1.6f, 0.04f, Surface.Wax, s + 3u);
                    break;
                }
                default:
                {
                    // A substation: a concrete pad, a finned transformer, three bushings, a warning stripe.
                    Block(img, t, 0.06f, 0.06f, 1.94f, 1.94f, PixelImage.Rgb(126, 124, 118), 0.08f, 0.03f, Surface.Stone, s);
                    for (int i = 0; i < 9; i++)
                    {
                        float x = 0.12f + i * 0.2f;
                        Block(img, t, x, 0.1f, x + 0.1f, 0.2f, PixelImage.Rgb(220, 180, 40), 0.09f, 0.005f, Surface.Plain, s + 1u);
                    }
                    Block(img, t, 0.35f, 0.4f, 1.65f, 1.45f, PixelImage.Rgb(84, 98, 90), 0.8f, 0.05f, Surface.Metal, s + 2u);
                    for (int i = 0; i < 8; i++) Rod(img, t, 0.42f + i * 0.17f, 0.32f, 0.42f + i * 0.17f, 0.42f, 0.02f, PixelImage.Rgb(70, 82, 76), 0.6f, 0.6f);
                    foreach (float x in new[] { 0.65f, 1f, 1.35f })
                    {
                        Round(img, t, x, 1.62f, 0.09f, Porcelain, 1.05f, 0.04f, Surface.Wax, s + 3u);
                        Stud(img, t, x, 1.62f, 0.03f, PixelImage.Rgb(180, 120, 70), 1.1f);
                    }
                    Stud(img, t, 1.5f, 0.6f, 0.05f, PixelImage.Rgb(90, 220, 110), 0.85f);
                    break;
                }
            }
            return Finish(img, t, 0);
        }

        /// <summary>An accumulator from above: four big battery cells on a base, with their terminals and a cable.</summary>
        public static PixelImage AccumulatorPiece(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            uint s = 5600u;
            Block(img, t, 0.06f, 0.06f, 1.94f, 1.94f, PixelImage.Rgb(58, 60, 64), 0.15f, 0.03f, Surface.Metal, s);
            foreach (var (cx, cy) in new[] { (0.55f, 0.55f), (1.45f, 0.55f), (0.55f, 1.45f), (1.45f, 1.45f) })
            {
                Round(img, t, cx, cy, 0.38f, PixelImage.Rgb(96, 110, 128), 0.7f, 0.04f, Surface.Metal, s + 1u);
                Ring(img, t, cx, cy, 0.3f, 0.36f, PixelImage.Rgb(140, 150, 160), 0.72f, Surface.Metal, s + 2u);
                Stud(img, t, cx - 0.1f, cy + 0.08f, 0.05f, PixelImage.Rgb(200, 60, 50), 0.78f);
                Stud(img, t, cx + 0.1f, cy + 0.08f, 0.05f, PixelImage.Rgb(60, 60, 64), 0.78f);
            }
            Rod(img, t, 0.45f, 0.63f, 1.55f, 0.63f, 0.025f, PixelImage.Rgb(40, 40, 42), 0.8f, 0.8f);
            Rod(img, t, 0.45f, 1.53f, 1.55f, 1.53f, 0.025f, PixelImage.Rgb(40, 40, 42), 0.8f, 0.8f);
            Rod(img, t, 1f, 0.63f, 1f, 1.53f, 0.025f, PixelImage.Rgb(40, 40, 42), 0.8f, 0.8f);
            return Finish(img, t, 0);
        }

        /// <summary>A solar panel from above: a 3x3 frame of dark blue cells with silver lines between them.</summary>
        public static PixelImage SolarPiece(int ppu)
        {
            float t = ppu;
            var img = Canvas(3f, 3f, t);
            uint s = 5700u;
            Block(img, t, 0.04f, 0.04f, 2.96f, 2.96f, PixelImage.Rgb(170, 174, 180), 0.22f, 0.03f, Surface.Metal, s);
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                {
                    float x0 = 0.12f + x * 0.93f, y0 = 0.12f + y * 0.93f;
                    Block(img, t, x0, y0, x0 + 0.85f, y0 + 0.85f, PixelImage.Rgb(30, 44, 86), 0.24f, 0.01f, Surface.Plain, s + 1u);
                    // The cells' grid: thin silver lines.
                    for (int k = 1; k < 4; k++)
                    {
                        Rod(img, t, x0 + k * 0.2125f, y0 + 0.02f, x0 + k * 0.2125f, y0 + 0.83f, 0.004f, PixelImage.Rgb(120, 130, 150), 0.245f, 0.245f);
                        Rod(img, t, x0 + 0.02f, y0 + k * 0.2125f, x0 + 0.83f, y0 + k * 0.2125f, 0.004f, PixelImage.Rgb(120, 130, 150), 0.245f, 0.245f);
                    }
                    // A sheen across the glass.
                    Rod(img, t, x0 + 0.08f, y0 + 0.7f, x0 + 0.35f, y0 + 0.78f, 0.02f, PixelImage.Rgb(90, 110, 160), 0.246f, 0.246f);
                }
            return Finish(img, t, 0, 0.6f, 0.4f);
        }
    }
}
