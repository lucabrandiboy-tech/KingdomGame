using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// The pictures for each age's newer things (crafting tables, machines, furniture, towers), drawn by a
    /// few painters that take the age's look: its wood, metal, cloth and trim. They're baked at a quarter
    /// of the art detail (<see cref="ArtCatalog"/>): there are a lot of them, and video memory is limited.
    /// </summary>
    public static partial class FurnitureArt
    {
        /// <summary>The materials of an age (0 = Stone Age ... 7 = World War 2).</summary>
        internal struct AgeLook
        {
            public Color32 Wood, Metal, Trim, Cloth, Stone;
            public Surface MetalSurface;
        }

        internal static AgeLook LookOf(int age)
        {
            switch (Mathf.Clamp(age, 0, 7))
            {
                case 0: return new AgeLook { Wood = PixelImage.Rgb(120, 92, 62), Metal = PixelImage.Rgb(110, 104, 96), Trim = PixelImage.Rgb(170, 150, 110), Cloth = PixelImage.Rgb(150, 120, 80), Stone = PixelImage.Rgb(120, 114, 104), MetalSurface = Surface.Stone };
                case 1: return new AgeLook { Wood = PixelImage.Rgb(126, 88, 56), Metal = PixelImage.Rgb(176, 120, 60), Trim = PixelImage.Rgb(200, 150, 80), Cloth = PixelImage.Rgb(170, 110, 70), Stone = PixelImage.Rgb(140, 120, 96), MetalSurface = Surface.Metal };
                case 2: return new AgeLook { Wood = PixelImage.Rgb(110, 78, 50), Metal = PixelImage.Rgb(96, 96, 100), Trim = PixelImage.Rgb(130, 130, 136), Cloth = PixelImage.Rgb(120, 70, 50), Stone = PixelImage.Rgb(116, 112, 108), MetalSurface = Surface.Metal };
                case 3: return new AgeLook { Wood = PixelImage.Rgb(96, 64, 40), Metal = PixelImage.Rgb(82, 84, 90), Trim = PixelImage.Rgb(190, 160, 70), Cloth = PixelImage.Rgb(130, 40, 40), Stone = PixelImage.Rgb(128, 124, 118), MetalSurface = Surface.Metal };
                case 4: return new AgeLook { Wood = PixelImage.Rgb(132, 86, 50), Metal = PixelImage.Rgb(160, 130, 70), Trim = PixelImage.Rgb(220, 190, 110), Cloth = PixelImage.Rgb(60, 70, 130), Stone = PixelImage.Rgb(170, 160, 140), MetalSurface = Surface.Metal };
                case 5: return new AgeLook { Wood = PixelImage.Rgb(90, 60, 42), Metal = PixelImage.Rgb(70, 72, 70), Trim = PixelImage.Rgb(150, 70, 50), Cloth = PixelImage.Rgb(80, 60, 50), Stone = PixelImage.Rgb(132, 74, 56), MetalSurface = Surface.Metal };
                case 6: return new AgeLook { Wood = PixelImage.Rgb(104, 92, 70), Metal = PixelImage.Rgb(84, 92, 78), Trim = PixelImage.Rgb(170, 150, 90), Cloth = PixelImage.Rgb(100, 104, 74), Stone = PixelImage.Rgb(120, 118, 110), MetalSurface = Surface.Metal };
                default: return new AgeLook { Wood = PixelImage.Rgb(98, 88, 70), Metal = PixelImage.Rgb(78, 86, 80), Trim = PixelImage.Rgb(200, 200, 190), Cloth = PixelImage.Rgb(84, 96, 72), Stone = PixelImage.Rgb(126, 126, 120), MetalSurface = Surface.Metal };
            }
        }

        public static string Workbench(int age) => $"Age/Workbench{age}";

        /// <summary>Every picture of the ages' newer things (see the class summary).</summary>
        public static IEnumerable<DefensePiece> AgePieces()
        {
            for (int age = 0; age < 8; age++)
            {
                int a = age;
                yield return new DefensePiece { Art = Workbench(a), Draw = p => DrawWorkbench(p, a) };
            }
        }

        // ---------------------------------------------------- crafting tables --

        // A crafting table, 2 tiles by 1: a top on legs with the age's tools and what it makes lying on it.
        static PixelImage DrawWorkbench(int ppu, int age)
        {
            float t = ppu;
            var look = LookOf(age);
            var img = Canvas(2f, 1f, t);
            uint s = 1600u + (uint)age * 20u;
            bool stone = age == 0;
            // Legs at the corners, then the top.
            foreach (var (x, y) in new[] { (0.12f, 0.16f), (1.88f, 0.16f), (0.12f, 0.84f), (1.88f, 0.84f) })
                Round(img, t, x, y, 0.07f, PixelImage.Scale(look.Wood, 0.7f), 0.3f, 0.02f, Surface.Wood, s);
            if (stone) Block(img, t, 0.06f, 0.1f, 1.94f, 0.9f, look.Stone, 0.42f, 0.06f, Surface.Stone, s + 1);
            else Block(img, t, 0.06f, 0.1f, 1.94f, 0.9f, look.Wood, 0.45f, 0.03f, Surface.Wood, s + 1, true, 0.2f);
            if (age >= 5) Block(img, t, 0.06f, 0.1f, 1.94f, 0.22f, look.Metal, 0.47f, 0.01f, look.MetalSurface, s + 2);   // a steel edge
            switch (age)
            {
                case 0:   // coils of rope and a stone knife
                    Ring(img, t, 0.55f, 0.5f, 0.12f, 0.2f, look.Cloth, 0.5f, Surface.Cloth, s + 3);
                    Ring(img, t, 0.95f, 0.55f, 0.08f, 0.15f, PixelImage.Scale(look.Cloth, 0.85f), 0.5f, Surface.Cloth, s + 4);
                    Rod(img, t, 1.35f, 0.35f, 1.7f, 0.6f, 0.04f, PixelImage.Rgb(70, 66, 62), 0.5f, 0.5f);
                    break;
                case 1:   // a crucible and bronze bars
                    Round(img, t, 0.5f, 0.5f, 0.17f, PixelImage.Rgb(90, 80, 70), 0.6f, 0.05f, Surface.Stone, s + 3);
                    Round(img, t, 0.5f, 0.5f, 0.1f, PixelImage.Rgb(240, 150, 50), 0.62f, 0f, Surface.Plain, s + 4);
                    for (int i = 0; i < 3; i++) Block(img, t, 1.0f + i * 0.22f, 0.36f, 1.16f + i * 0.22f, 0.64f, look.Metal, 0.52f, 0.02f, Surface.Metal, s + 5);
                    break;
                case 2:   // a small anvil and iron bars
                    Block(img, t, 0.3f, 0.35f, 0.8f, 0.65f, look.Metal, 0.65f, 0.04f, Surface.Metal, s + 3);
                    Round(img, t, 0.86f, 0.5f, 0.1f, look.Metal, 0.64f, 0.03f, Surface.Metal, s + 4);
                    for (int i = 0; i < 3; i++) Block(img, t, 1.15f, 0.3f + i * 0.16f, 1.75f, 0.4f + i * 0.16f, PixelImage.Scale(look.Metal, 0.85f), 0.5f, 0.02f, Surface.Metal, s + 5);
                    break;
                case 3:   // gears and a vice
                    Gear(img, t, 0.55f, 0.5f, 0.22f, look.Trim, s + 3);
                    Gear(img, t, 1.0f, 0.42f, 0.14f, look.Metal, s + 4);
                    Block(img, t, 1.4f, 0.35f, 1.8f, 0.65f, look.Metal, 0.6f, 0.03f, Surface.Metal, s + 5);
                    break;
                case 4:   // a powder keg and a mortar and pestle
                    Round(img, t, 0.5f, 0.5f, 0.22f, look.Wood, 0.7f, 0.08f, Surface.Wood, s + 3);
                    Ring(img, t, 0.5f, 0.5f, 0.18f, 0.22f, look.Metal, 0.71f, Surface.Metal, s + 4);
                    Round(img, t, 1.2f, 0.5f, 0.16f, look.Stone, 0.55f, 0.05f, Surface.Stone, s + 5);
                    Round(img, t, 1.2f, 0.5f, 0.09f, PixelImage.Rgb(40, 38, 36), 0.56f, 0f, Surface.Plain, s + 6);
                    break;
                case 5:   // a vice, a wrench and machine parts
                    Block(img, t, 0.25f, 0.32f, 0.65f, 0.68f, look.Metal, 0.65f, 0.03f, Surface.Metal, s + 3);
                    Rod(img, t, 0.9f, 0.3f, 1.3f, 0.7f, 0.04f, PixelImage.Scale(look.Metal, 1.3f), 0.55f, 0.55f);
                    Gear(img, t, 1.6f, 0.5f, 0.17f, look.Trim, s + 4);
                    break;
                case 6:   // spools of copper wire
                    for (int i = 0; i < 3; i++)
                    {
                        float x = 0.45f + i * 0.5f;
                        Round(img, t, x, 0.5f, 0.17f, PixelImage.Rgb(196, 112, 58), 0.6f, 0.04f, Surface.Metal, s + 3 + (uint)i);
                        Round(img, t, x, 0.5f, 0.06f, look.Wood, 0.62f, 0f, Surface.Wood, s + 6);
                    }
                    break;
                default:  // a radio set, valves and a soldering iron
                    Block(img, t, 0.2f, 0.25f, 0.95f, 0.75f, PixelImage.Rgb(60, 66, 58), 0.7f, 0.03f, Surface.Metal, s + 3);
                    for (int i = 0; i < 3; i++) Round(img, t, 0.35f + i * 0.2f, 0.55f, 0.05f, PixelImage.Rgb(240, 200, 120), 0.72f, 0f, Surface.Plain, s + 4);
                    Rod(img, t, 1.2f, 0.4f, 1.75f, 0.6f, 0.03f, PixelImage.Rgb(40, 40, 40), 0.5f, 0.5f);
                    Round(img, t, 1.5f, 0.3f, 0.06f, look.Trim, 0.5f, 0f, Surface.Metal, s + 5);
                    break;
            }
            return Finish(img, t, 0);
        }

        // A cogwheel seen from above: a ring with teeth and a hub.
        static void Gear(PixelImage img, float t, float cx, float cy, float r, Color32 color, uint seed)
        {
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                Round(img, t, cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, r * 0.18f, color, 0.6f, 0.01f, Surface.Metal, seed + (uint)i);
            }
            Ring(img, t, cx, cy, r * 0.45f, r * 0.95f, color, 0.6f, Surface.Metal, seed + 20u);
            Round(img, t, cx, cy, r * 0.2f, PixelImage.Scale(color, 0.7f), 0.62f, 0.01f, Surface.Metal, seed + 21u);
        }
    }
}
