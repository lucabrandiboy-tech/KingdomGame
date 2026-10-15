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

        /// <summary>The picture key of an age machine (see <see cref="MachinePictures"/>).</summary>
        public static string AgeMachine(string name) => "Age/M/" + name;

        /// <summary>Each age machine's picture: its name (see <see cref="AgeMachine"/>), whether it lies flat (belts), and how it's drawn.</summary>
        static readonly (string Name, bool Flat, System.Func<int, PixelImage> Draw)[] MachinePictures =
        {
            // Stone Age
            ("WoodcutterCamp", false, p => DrawHarvester(p, 0)),
            ("RockPile", false, p => DrawDigger(p, 0, PixelImage.Rgb(130, 124, 116))),
            ("FishTrap", false, p => DrawFarm(p, 0, FarmKind.Fish)),
            ("SaplingBed", false, p => DrawFarm(p, 0, FarmKind.Saplings)),
            ("LogChute", true, p => DrawBelt(p, 0)),
            // Bronze Age
            ("CopperSluice", false, p => DrawDigger(p, 1, PixelImage.Rgb(196, 112, 58))),
            ("BerryOrchard", false, p => DrawFarm(p, 1, FarmKind.Bushes)),
            ("BronzeSawPit", false, p => DrawHarvester(p, 1)),
            ("SledgeTrack", true, p => DrawBelt(p, 1)),
            // Iron Age
            ("BogIronPit", false, p => DrawDigger(p, 2, PixelImage.Rgb(150, 96, 84))),
            ("GranaryFarm", false, p => DrawFarm(p, 2, FarmKind.Grain)),
            // Medieval
            ("CharcoalKiln", false, p => DrawKiln(p)),
            ("Windmill", false, p => DrawMill(p, 3, false)),
            ("WindSawmill", false, p => DrawHarvester(p, 3)),
            ("CoalPit", false, p => DrawDigger(p, 3, PixelImage.Rgb(34, 32, 32))),
            // Renaissance
            ("WaterMill", false, p => DrawMill(p, 4, true)),
            ("ClockworkCrafter", false, p => DrawAutoCrafter(p, 4)),
            ("WaterQuarry", false, p => DrawDigger(p, 4, PixelImage.Rgb(140, 134, 126))),
            ("StampMill", false, p => DrawDigger(p, 4, PixelImage.Rgb(150, 96, 84))),
            ("CanvasBelt", true, p => DrawBelt(p, 4)),
            // Industrial
            ("SteamDrill", false, p => DrawDigger(p, 5, PixelImage.Rgb(34, 32, 32))),
            // World War 1
            ("DieselGenerator", false, p => DrawEngine(p, 6)),
            ("ElectricSawmill", false, p => DrawHarvester(p, 6)),
            ("OreDrill", false, p => DrawDigger(p, 6, PixelImage.Rgb(236, 190, 64))),
            ("TractorFarm", false, p => DrawFarm(p, 6, FarmKind.Grain)),
            ("FastBelt", true, p => DrawBelt(p, 6)),
            // World War 2
            ("PowerPlant", false, p => DrawEngine(p, 7)),
            ("AssemblyLine", false, p => DrawAutoCrafter(p, 7)),
            ("Excavator", false, p => DrawDigger(p, 7, PixelImage.Rgb(130, 124, 116))),
            ("MechanizedFarm", false, p => DrawFarm(p, 7, FarmKind.Greenhouse)),
            ("ExpressBelt", true, p => DrawBelt(p, 7)),
        };

        /// <summary>Every picture of the ages' newer things (see the class summary).</summary>
        public static IEnumerable<DefensePiece> AgePieces()
        {
            for (int age = 0; age < 8; age++)
            {
                int a = age;
                yield return new DefensePiece { Art = Workbench(a), Draw = p => DrawWorkbench(p, a) };
            }
            foreach (var m in MachinePictures) yield return new DefensePiece { Art = AgeMachine(m.Name), Draw = m.Draw, Flat = m.Flat };
            for (int age = 0; age < 8; age++)
                for (int k = 0; k < FurnitureKinds; k++)
                {
                    int a = age, kind = k;
                    yield return new DefensePiece { Art = AgeFurniture(a, kind), Draw = p => DrawAgeFurniture(p, a, kind) };
                }
        }

        // ----------------------------------------------------------- furniture --

        /// <summary>Each age's six pieces of furniture: bed, table, shelf, hearth, desk and decoration.</summary>
        public const int FurnitureKinds = 6;
        public static string AgeFurniture(int age, int kind) => $"Age/F/{age}/{kind}";
        /// <summary>Tiles each kind takes (bed 1x2, table and shelf 2x1, the rest 1x1).</summary>
        public static Vector2Int FurnitureSize(int kind) => kind == 0 ? new Vector2Int(1, 2) : kind == 1 || kind == 2 ? new Vector2Int(2, 1) : Vector2Int.one;

        static PixelImage DrawAgeFurniture(int ppu, int age, int kind)
        {
            float t = ppu;
            var look = LookOf(age);
            var size = FurnitureSize(kind);
            var img = Canvas(size.x, size.y, t);
            uint s = 3000u + (uint)(age * 10 + kind) * 13u;
            bool modern = age >= 5;
            var frame = modern ? look.Metal : look.Wood;
            var frameSurface = modern ? Surface.Metal : Surface.Wood;
            switch (kind)
            {
                case 0:   // a bed: frame, mattress, blanket, pillow (posts and a canopy edge for the grand ones)
                    if (age == 0)
                    {
                        Round(img, t, 0.5f, 1f, 0.45f, PixelImage.Rgb(196, 170, 100), 0.12f, 0.08f, Surface.Cloth, s);   // a heap of straw
                        Block(img, t, 0.2f, 0.35f, 0.8f, 1.2f, look.Cloth, 0.16f, 0.08f, Surface.Cloth, s + 1);         // a hide
                        break;
                    }
                    Block(img, t, 0.06f, 0.06f, 0.94f, 1.94f, frame, 0.3f, 0.03f, frameSurface, s, false, 0.1f);
                    Block(img, t, 0.13f, 0.13f, 0.87f, 1.87f, PixelImage.Rgb(214, 206, 186), 0.38f, 0.06f, Surface.Cloth, s + 1);   // the mattress
                    Block(img, t, 0.13f, 0.13f, 0.87f, 1.3f, look.Cloth, 0.42f, 0.08f, Surface.Cloth, s + 2);                         // the blanket
                    Block(img, t, 0.22f, 1.5f, 0.78f, 1.8f, PixelImage.Rgb(232, 226, 210), 0.45f, 0.1f, Surface.Cloth, s + 3);         // the pillow
                    if (age == 3 || age == 4)
                        foreach (var (x, y) in new[] { (0.08f, 0.08f), (0.92f, 0.08f), (0.08f, 1.92f), (0.92f, 1.92f) })
                            Round(img, t, x, y, 0.07f, look.Trim, 1.2f, 0.02f, Surface.Wood, s + 4);
                    if (age >= 6) Block(img, t, 0.06f, 1.88f, 0.94f, 1.96f, look.Metal, 0.7f, 0.01f, Surface.Metal, s + 5);   // a bunk rail
                    break;

                case 1:   // a table with the age's things on it (bowls, a map, plans)
                    Block(img, t, 0.05f, 0.08f, 1.95f, 0.92f, age == 0 ? look.Stone : frame, 0.45f, 0.03f, age == 0 ? Surface.Stone : frameSurface, s, true, 0.25f);
                    if (age >= 4)
                    {
                        Block(img, t, 0.4f, 0.25f, 1.4f, 0.75f, PixelImage.Rgb(222, 210, 180), 0.47f, 0.005f, Surface.Plain, s + 1);   // a map / plans
                        Rod(img, t, 0.6f, 0.4f, 1.2f, 0.6f, 0.01f, PixelImage.Rgb(120, 60, 50), 0.48f, 0.48f);
                    }
                    else
                        for (int i = 0; i < 3; i++)
                        {
                            Round(img, t, 0.45f + i * 0.55f, 0.5f, 0.13f, age == 1 ? look.Metal : PixelImage.Rgb(150, 110, 80), 0.5f, 0.04f, Surface.Wood, s + 2 + (uint)i);
                            Round(img, t, 0.45f + i * 0.55f, 0.5f, 0.08f, PixelImage.Rgb(170, 120, 60), 0.51f, 0f, Surface.Plain, s + 5);
                        }
                    if (age == 3) Block(img, t, 0.05f, 0.4f, 1.95f, 0.6f, look.Cloth, 0.46f, 0.005f, Surface.Cloth, s + 6);   // a runner
                    break;

                case 2:   // shelves (or racks and lockers) with the age's goods
                    Block(img, t, 0.04f, 0.5f, 1.96f, 0.96f, frame, 0.9f, 0.03f, frameSurface, s, true, 0.2f);
                    for (int i = 0; i < 4; i++)
                    {
                        float x = 0.25f + i * 0.47f;
                        var goods = age >= 6 ? PixelImage.Rgb(90, 100, 70) : age == 0 ? PixelImage.Rgb(196, 186, 160) : i % 2 == 0 ? look.Metal : look.Cloth;
                        if (age == 0) Rod(img, t, x - 0.15f, 0.65f, x + 0.15f, 0.85f, 0.03f, goods, 0.95f, 0.95f);   // antlers and bones
                        else Block(img, t, x - 0.16f, 0.58f, x + 0.16f, 0.9f, goods, 0.95f, 0.02f, Surface.Wood, s + 1 + (uint)i);
                    }
                    break;

                case 3:   // a hearth or stove (its fire is drawn by the game)
                    if (age < 5)
                    {
                        Round(img, t, 0.5f, 0.5f, 0.42f, age == 4 ? PixelImage.Rgb(200, 190, 170) : look.Stone, 0.5f, 0.1f, Surface.Stone, s);
                        Round(img, t, 0.5f, 0.5f, 0.24f, PixelImage.Rgb(40, 30, 26), 0.2f, 0f, Surface.Plain, s + 1);
                        if (age == 1) Round(img, t, 0.5f, 0.5f, 0.24f, PixelImage.Rgb(140, 90, 60), 0.55f, 0.15f, Surface.Stone, s + 2);   // a bread oven dome
                    }
                    else
                    {
                        Block(img, t, 0.1f, 0.1f, 0.9f, 0.9f, look.Metal, 0.6f, 0.03f, Surface.Metal, s);
                        for (int i = 0; i < 2; i++) Ring(img, t, 0.32f + i * 0.36f, 0.62f, 0.07f, 0.12f, PixelImage.Rgb(40, 40, 40), 0.62f, Surface.Metal, s + 1);
                        Round(img, t, 0.75f, 0.2f, 0.08f, PixelImage.Rgb(40, 40, 40), 1.1f, 0f, Surface.Metal, s + 2);   // the chimney pipe
                    }
                    break;

                case 4:   // a desk with the age's knowledge: tally stones, clay tablets, scrolls, books, a globe, a telegraph, a radio
                    Block(img, t, 0.06f, 0.15f, 0.94f, 0.85f, age == 0 ? look.Stone : frame, 0.45f, 0.03f, age == 0 ? Surface.Stone : frameSurface, s, true, 0.2f);
                    switch (age)
                    {
                        case 0: for (int i = 0; i < 5; i++) Stud(img, t, 0.25f + i * 0.12f, 0.5f, 0.04f, PixelImage.Rgb(200, 196, 186), 0.5f); break;
                        case 1: Block(img, t, 0.25f, 0.3f, 0.6f, 0.7f, PixelImage.Rgb(170, 130, 90), 0.5f, 0.02f, Surface.Stone, s + 1); break;
                        case 2: case 3: Rod(img, t, 0.25f, 0.45f, 0.75f, 0.55f, 0.08f, PixelImage.Rgb(222, 210, 180), 0.5f, 0.5f); break;
                        case 4: Round(img, t, 0.5f, 0.5f, 0.24f, PixelImage.Rgb(70, 110, 150), 0.8f, 0.2f, Surface.Plain, s + 2); Ring(img, t, 0.5f, 0.5f, 0.22f, 0.27f, look.Trim, 0.82f, Surface.Metal, s + 3); break;
                        default:
                            Block(img, t, 0.25f, 0.3f, 0.75f, 0.7f, PixelImage.Rgb(60, 64, 58), 0.6f, 0.02f, Surface.Metal, s + 4);
                            Round(img, t, 0.4f, 0.5f, 0.06f, age >= 6 ? PixelImage.Rgb(240, 200, 120) : look.Trim, 0.62f, 0f, Surface.Plain, s + 5);
                            break;
                    }
                    break;

                default:  // a decoration: totem, idol, banner, tapestry, bust, clock, flag, poster
                    switch (age)
                    {
                        case 0:
                            Round(img, t, 0.5f, 0.5f, 0.28f, look.Wood, 1.4f, 0.05f, Surface.Wood, s);
                            Ring(img, t, 0.5f, 0.5f, 0.15f, 0.22f, PixelImage.Rgb(170, 60, 40), 1.42f, Surface.Plain, s + 1);
                            break;
                        case 1: case 4:
                            Block(img, t, 0.25f, 0.25f, 0.75f, 0.75f, look.Stone, 0.4f, 0.04f, Surface.Stone, s);   // the plinth
                            Round(img, t, 0.5f, 0.5f, 0.17f, age == 1 ? look.Metal : PixelImage.Rgb(226, 222, 214), 1.1f, 0.12f, Surface.Metal, s + 1);
                            break;
                        case 5:
                            Block(img, t, 0.25f, 0.2f, 0.75f, 0.8f, look.Wood, 1.6f, 0.03f, Surface.Wood, s);   // the clock case
                            Round(img, t, 0.5f, 0.6f, 0.13f, PixelImage.Rgb(230, 224, 200), 1.62f, 0f, Surface.Plain, s + 1);
                            break;
                        default:
                            Round(img, t, 0.5f, 0.5f, 0.06f, look.Wood, 1.5f, 0.02f, Surface.Wood, s);   // a pole with a banner, flag or poster board
                            Block(img, t, 0.5f, 0.4f, 0.95f, 0.65f, look.Cloth, 1.45f, 0.01f, Surface.Cloth, s + 1);
                            Block(img, t, 0.55f, 0.48f, 0.9f, 0.57f, look.Trim, 1.46f, 0f, Surface.Cloth, s + 2);
                            break;
                    }
                    break;
            }
            return Finish(img, t, 0);
        }

        // ------------------------------------------------------------ machines --

        internal enum FarmKind { Fish, Saplings, Bushes, Grain, Greenhouse }

        // Something that cuts trees by itself: a platform with a log pile and the age's cutter (axe, saw, sails, motor).
        static PixelImage DrawHarvester(int ppu, int age)
        {
            float t = ppu;
            var look = LookOf(age);
            var img = Canvas(2f, 2f, t);
            uint s = 1700u + (uint)age * 30u;
            if (age == 0) Round(img, t, 1f, 1f, 0.85f, PixelImage.Rgb(80, 66, 48), 0.03f, 0.02f, Surface.Stone, s);   // trampled earth
            else Block(img, t, 0.1f, 0.1f, 1.9f, 1.9f, age >= 5 ? look.Stone : look.Wood, 0.12f, 0.03f, age >= 5 ? Surface.Stone : Surface.Wood, s, true, 0.25f);
            // The log pile.
            for (int i = 0; i < 4; i++)
                Rod(img, t, 0.25f, 0.35f + i * 0.13f, 1.0f, 0.35f + i * 0.13f, 0.06f, PixelImage.Scale(PaleWood, 0.85f + 0.05f * i), 0.35f, 0.35f);
            if (age == 0)
            {
                Round(img, t, 1.45f, 1.35f, 0.28f, PixelImage.Scale(Oak, 0.8f), 0.4f, 0.04f, Surface.Wood, s + 1);   // a chopping stump
                Rod(img, t, 1.25f, 1.6f, 1.6f, 1.2f, 0.035f, Oak, 0.5f, 0.5f);                                          // the axe handle
                Block(img, t, 1.55f, 1.12f, 1.72f, 1.3f, look.Metal, 0.52f, 0.02f, Surface.Stone, s + 2);
            }
            else
            {
                Round(img, t, 1.4f, 1.3f, 0.32f, PixelImage.Rgb(176, 178, 174), 0.4f, 0f, Surface.Metal, s + 1);   // the blade
                Round(img, t, 1.4f, 1.3f, 0.07f, look.Metal, 0.45f, 0f, Surface.Metal, s + 2);
                if (age == 3) Sails(img, t, 1.4f, 1.3f, 0.55f, look.Cloth, s + 3);
                if (age >= 5) Block(img, t, 1.0f, 1.55f, 1.85f, 1.88f, look.Metal, 0.7f, 0.03f, Surface.Metal, s + 4);   // the motor
                if (age >= 6) Round(img, t, 1.7f, 1.72f, 0.06f, PixelImage.Rgb(240, 200, 80), 0.75f, 0f, Surface.Plain, s + 5);
            }
            return Finish(img, t, 0);
        }

        // Something that digs by itself: a pit with what it digs in it, under the age's frame (wood, treadwheel, steel rig).
        static PixelImage DrawDigger(int ppu, int age, Color32 ore)
        {
            float t = ppu;
            var look = LookOf(age);
            var img = Canvas(2f, 2f, t);
            uint s = 1900u + (uint)age * 30u + ore.r;
            Block(img, t, 0.1f, 0.1f, 1.9f, 1.9f, look.Stone, 0.08f, 0.08f, Surface.Stone, s);
            Round(img, t, 1f, 1f, 0.62f, PixelImage.Scale(look.Stone, 0.5f), 0.01f, 0f, Surface.Stone, s + 1);   // the pit
            var dice = new Dice(s + 2);
            for (int i = 0; i < 9; i++)
            {
                float a = dice.Range(0f, Mathf.PI * 2f), r = dice.Range(0.05f, 0.45f);
                Round(img, t, 1f + Mathf.Cos(a) * r, 1f + Mathf.Sin(a) * r, dice.Range(0.05f, 0.1f), PixelImage.Scale(ore, dice.Range(0.8f, 1.1f)), 0.06f, 0.03f, Surface.Stone, s + 3 + (uint)i);
            }
            var frame = age >= 5 ? look.Metal : look.Wood;
            Rod(img, t, 0.3f, 0.3f, 1.7f, 1.7f, 0.05f, frame, 0.9f, 0.9f);
            Rod(img, t, 0.3f, 1.7f, 1.7f, 0.3f, 0.05f, frame, 0.9f, 0.9f);
            if (age == 2) Ring(img, t, 1.6f, 0.5f, 0.24f, 0.32f, look.Wood, 0.8f, Surface.Wood, s + 20);          // treadwheel
            if (age == 4) Ring(img, t, 0.4f, 1.6f, 0.2f, 0.3f, look.Wood, 0.8f, Surface.Wood, s + 21);           // waterwheel
            Round(img, t, 1f, 1f, 0.14f, age >= 5 ? PixelImage.Rgb(120, 70, 44) : look.Metal, 1f, 0.04f, Surface.Metal, s + 22);   // the bucket / drill head
            if (age >= 5) Block(img, t, 1.35f, 1.5f, 1.85f, 1.85f, look.Metal, 0.75f, 0.03f, Surface.Metal, s + 23);   // the engine
            return Finish(img, t, 0);
        }

        // Something that grows food (or saplings) by itself.
        static PixelImage DrawFarm(int ppu, int age, FarmKind kind)
        {
            float t = ppu;
            var look = LookOf(age);
            var img = Canvas(2f, 2f, t);
            uint s = 2100u + (uint)age * 30u + (uint)kind * 7u;
            if (kind == FarmKind.Fish)
            {
                Round(img, t, 1f, 1f, 0.85f, PixelImage.Rgb(46, 70, 84), 0.02f, 0f, Surface.Plain, s);   // the pond
                Ring(img, t, 1f, 1f, 0.82f, 0.92f, look.Stone, 0.12f, Surface.Stone, s + 1);
                Round(img, t, 1.1f, 0.95f, 0.3f, look.Cloth, 0.2f, 0.1f, Surface.Cloth, s + 2);          // the woven trap
                Ring(img, t, 1.1f, 0.95f, 0.22f, 0.3f, PixelImage.Scale(look.Cloth, 0.7f), 0.22f, Surface.Cloth, s + 3);
                return Finish(img, t, 0);
            }
            var soil = kind == FarmKind.Greenhouse ? PixelImage.Rgb(70, 60, 48) : PixelImage.Rgb(84, 62, 40);
            Block(img, t, 0.08f, 0.08f, 1.92f, 1.92f, soil, 0.03f, 0.02f, Surface.Stone, s);
            var green = PixelImage.Rgb(78, 120, 54);
            for (int row = 0; row < 4; row++)
                for (int i = 0; i < 4; i++)
                {
                    float x = 0.3f + i * 0.45f, y = 0.3f + row * 0.45f;
                    switch (kind)
                    {
                        case FarmKind.Saplings: Round(img, t, x, y, 0.1f, green, 0.25f, 0.06f, Surface.Plain, s + (uint)(row * 4 + i)); break;
                        case FarmKind.Bushes:
                            Round(img, t, x, y, 0.17f, PixelImage.Scale(green, 0.85f), 0.25f, 0.08f, Surface.Plain, s + (uint)(row * 4 + i));
                            Stud(img, t, x + 0.05f, y + 0.04f, 0.03f, PixelImage.Rgb(200, 40, 60), 0.3f);
                            break;
                        default:
                            Block(img, t, x - 0.18f, y - 0.06f, x + 0.18f, y + 0.06f, PixelImage.Rgb(196, 168, 90), 0.15f, 0.01f, Surface.Plain, s + (uint)(row * 4 + i));
                            break;
                    }
                }
            if (kind == FarmKind.Greenhouse)
            {
                // Glass panes over it and a steel frame.
                for (int i = 0; i <= 4; i++) Rod(img, t, 0.1f + i * 0.45f, 0.1f, 0.1f + i * 0.45f, 1.9f, 0.025f, look.Metal, 0.9f, 0.9f);
                Rod(img, t, 0.1f, 1f, 1.9f, 1f, 0.025f, look.Metal, 0.9f, 0.9f);
            }
            if (age >= 6) Block(img, t, 1.45f, 0.1f, 1.9f, 0.45f, PixelImage.Rgb(140, 40, 34), 0.5f, 0.04f, Surface.Metal, s + 40);   // a tractor
            if (age == 2) Round(img, t, 1.7f, 1.7f, 0.2f, look.Wood, 0.6f, 0.1f, Surface.Wood, s + 41);                            // the granary
            return Finish(img, t, 0);
        }

        // A stretch of the age's conveyor, 1 tile, lying flat: a log trough, sledge rails, rollers, canvas, rubber with arrows.
        static PixelImage DrawBelt(int ppu, int age)
        {
            float t = ppu;
            var look = LookOf(age);
            var img = Canvas(1f, 1f, t);
            uint s = 2300u + (uint)age * 10u;
            switch (age)
            {
                case 0:
                    Block(img, t, 0.12f, 0.05f, 0.88f, 0.95f, PixelImage.Scale(look.Wood, 0.8f), 0.08f, 0.1f, Surface.Wood, s, false, 0f);
                    Block(img, t, 0.24f, 0.05f, 0.76f, 0.95f, PixelImage.Scale(look.Wood, 0.5f), 0.04f, 0.02f, Surface.Wood, s + 1, false);
                    break;
                case 1:
                    Block(img, t, 0.15f, 0.05f, 0.27f, 0.95f, look.Wood, 0.06f, 0.01f, Surface.Wood, s, false);
                    Block(img, t, 0.73f, 0.05f, 0.85f, 0.95f, look.Wood, 0.06f, 0.01f, Surface.Wood, s + 1, false);
                    for (int i = 0; i < 4; i++) Block(img, t, 0.1f, 0.1f + i * 0.24f, 0.9f, 0.18f + i * 0.24f, PixelImage.Scale(look.Wood, 0.8f), 0.05f, 0.01f, Surface.Wood, s + 2);
                    break;
                default:
                    var frame = age >= 5 ? look.Metal : look.Wood;
                    Block(img, t, 0.05f, 0.05f, 0.95f, 0.95f, frame, 0.06f, 0.02f, age >= 5 ? Surface.Metal : Surface.Wood, s);
                    var belt = age == 4 ? look.Cloth : PixelImage.Rgb(34, 34, 36);
                    Block(img, t, 0.14f, 0.05f, 0.86f, 0.95f, belt, 0.08f, 0.01f, age == 4 ? Surface.Cloth : Surface.Plain, s + 1, false);
                    for (int i = 0; i < 4; i++)
                        Rod(img, t, 0.16f, 0.15f + i * 0.23f, 0.84f, 0.15f + i * 0.23f, 0.012f, PixelImage.Scale(belt, 1.6f), 0.09f, 0.09f);
                    if (age >= 6)
                        for (int i = 0; i < 2; i++)
                        {
                            // Arrows: the faster belts.
                            float y = 0.3f + i * 0.4f;
                            var c = age == 7 ? PixelImage.Rgb(230, 190, 60) : PixelImage.Rgb(200, 200, 200);
                            Rod(img, t, 0.35f, y, 0.5f, y + 0.12f, 0.02f, c, 0.1f, 0.1f);
                            Rod(img, t, 0.65f, y, 0.5f, y + 0.12f, 0.02f, c, 0.1f, 0.1f);
                        }
                    break;
            }
            return Finish(img, t, 0);
        }

        // A mill: a round mill house with sails (wind) or a waterwheel and a race of water beside it.
        static PixelImage DrawMill(int ppu, int age, bool water)
        {
            float t = ppu;
            var look = LookOf(age);
            var img = Canvas(2f, 2f, t);
            uint s = 2400u + (uint)age * 10u + (water ? 5u : 0u);
            if (water) Block(img, t, 0.05f, 0.05f, 0.5f, 1.95f, PixelImage.Rgb(46, 70, 84), 0.01f, 0f, Surface.Plain, s);
            float cx = water ? 1.2f : 1f;
            Round(img, t, cx, 1f, 0.6f, look.Stone, 0.9f, 0.15f, Surface.Stone, s + 1);
            Round(img, t, cx, 1f, 0.48f, PixelImage.Scale(look.Wood, 0.9f), 1.1f, 0.25f, Surface.Wood, s + 2);   // the roof
            if (water) Ring(img, t, 0.45f, 1f, 0.3f, 0.4f, look.Wood, 0.6f, Surface.Wood, s + 3);
            else Sails(img, t, cx, 1f, 0.82f, look.Cloth, s + 4);
            return Finish(img, t, 0);
        }

        static void Sails(PixelImage img, float t, float cx, float cy, float r, Color32 cloth, uint seed)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                float ex = cx + Mathf.Cos(a) * r, ey = cy + Mathf.Sin(a) * r;
                Rod(img, t, cx, cy, ex, ey, 0.03f, Oak, 1.25f, 1.25f);
                Rod(img, t, cx + Mathf.Cos(a) * r * 0.35f + Mathf.Cos(a + 1.57f) * 0.08f, cy + Mathf.Sin(a) * r * 0.35f + Mathf.Sin(a + 1.57f) * 0.08f,
                    ex + Mathf.Cos(a + 1.57f) * 0.08f, ey + Mathf.Sin(a + 1.57f) * 0.08f, 0.07f, cloth, 1.24f, 1.24f);
            }
            Round(img, t, cx, cy, 0.07f, Oak, 1.3f, 0.03f, Surface.Wood, seed);
        }

        // A charcoal kiln: a domed earth mound with a smoking vent and a stack of logs waiting.
        static PixelImage DrawKiln(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Round(img, t, 1f, 1.05f, 0.75f, PixelImage.Rgb(86, 70, 54), 0.7f, 0.35f, Surface.Stone, 2501u);
            Round(img, t, 1f, 1.05f, 0.14f, PixelImage.Rgb(30, 26, 24), 0.72f, 0f, Surface.Plain, 2502u);
            Round(img, t, 1f, 1.05f, 0.07f, PixelImage.Rgb(240, 120, 40), 0.73f, 0f, Surface.Plain, 2503u);
            for (int i = 0; i < 3; i++) Rod(img, t, 0.2f, 0.18f + i * 0.1f, 0.9f, 0.18f + i * 0.1f, 0.045f, PaleWood, 0.25f, 0.25f);
            return Finish(img, t, 0);
        }

        // An engine house: a generator (diesel engine with exhausts, or a power plant with cooling and pylons).
        static PixelImage DrawEngine(int ppu, int age)
        {
            float t = ppu;
            var look = LookOf(age);
            var img = Canvas(2f, 2f, t);
            uint s = 2600u + (uint)age * 10u;
            Block(img, t, 0.08f, 0.08f, 1.92f, 1.92f, look.Stone, 0.15f, 0.03f, Surface.Stone, s);
            if (age == 6)
            {
                Block(img, t, 0.3f, 0.5f, 1.5f, 1.3f, look.Metal, 0.8f, 0.05f, Surface.Metal, s + 1);
                for (int i = 0; i < 4; i++) Round(img, t, 0.5f + i * 0.28f, 0.9f, 0.08f, PixelImage.Scale(look.Metal, 1.3f), 0.85f, 0.02f, Surface.Metal, s + 2);
                Round(img, t, 1.7f, 1.6f, 0.12f, PixelImage.Rgb(40, 40, 40), 1.2f, 0f, Surface.Metal, s + 3);   // the exhaust
                Block(img, t, 1.55f, 0.3f, 1.85f, 0.7f, PixelImage.Rgb(140, 40, 34), 0.6f, 0.03f, Surface.Metal, s + 4);   // the fuel tank
            }
            else
            {
                Round(img, t, 0.65f, 1.3f, 0.45f, PixelImage.Scale(look.Stone, 1.15f), 1.3f, 0.2f, Surface.Stone, s + 1);   // cooling tower
                Round(img, t, 0.65f, 1.3f, 0.3f, PixelImage.Rgb(50, 52, 50), 1.31f, 0f, Surface.Plain, s + 2);
                Block(img, t, 1.1f, 0.3f, 1.8f, 1.0f, look.Metal, 0.9f, 0.04f, Surface.Metal, s + 3);
                Rod(img, t, 1.45f, 1.1f, 1.45f, 1.8f, 0.03f, look.Metal, 1.4f, 1.4f);   // a pylon
                Rod(img, t, 1.25f, 1.6f, 1.65f, 1.6f, 0.02f, look.Metal, 1.4f, 1.4f);
            }
            return Finish(img, t, 0);
        }

        // An auto-crafter: the age's part made with no crafter. Clockwork (gears under glass) or an assembly line (belt and arms).
        static PixelImage DrawAutoCrafter(int ppu, int age)
        {
            float t = ppu;
            var look = LookOf(age);
            var img = Canvas(2f, 2f, t);
            uint s = 2700u + (uint)age * 10u;
            Block(img, t, 0.1f, 0.1f, 1.9f, 1.9f, age >= 5 ? look.Metal : look.Wood, 0.4f, 0.04f, age >= 5 ? Surface.Metal : Surface.Wood, s, true, 0.3f);
            if (age < 5)
            {
                Gear(img, t, 0.7f, 0.7f, 0.3f, look.Trim, s + 1);
                Gear(img, t, 1.25f, 1.15f, 0.22f, look.Metal, s + 2);
                Gear(img, t, 0.7f, 1.45f, 0.17f, look.Trim, s + 3);
            }
            else
            {
                Block(img, t, 0.2f, 0.85f, 1.8f, 1.15f, PixelImage.Rgb(34, 34, 36), 0.5f, 0.01f, Surface.Plain, s + 1);   // the belt
                for (int i = 0; i < 3; i++)
                {
                    float x = 0.5f + i * 0.5f;
                    Rod(img, t, x, 1.5f, x, 1.05f, 0.04f, look.Trim, 0.9f, 0.7f);   // robot arms
                    Round(img, t, x, 1.55f, 0.08f, look.Metal, 0.9f, 0.03f, Surface.Metal, s + 2 + (uint)i);
                    Block(img, t, x - 0.08f, 0.92f, x + 0.08f, 1.08f, PixelImage.Rgb(90, 110, 80), 0.55f, 0.01f, Surface.Metal, s + 5);
                }
            }
            return Finish(img, t, 0);
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
