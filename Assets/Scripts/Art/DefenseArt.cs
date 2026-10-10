using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Traps and towers, seen from above, in the furniture's style (same toolkit, lit
    /// from the top-left). Traps have a second picture once they've gone off
    /// (<see cref="ArtKeys.Sprung"/>). Towers that turn to aim have a head drawn for
    /// <see cref="HeadDirections"/> directions, each lit for the way it faces
    /// (<see cref="ArtKeys.Head"/>); the game turns it the rest of the way.
    /// </summary>
    public static partial class FurnitureArt
    {
        public const string SpikePit = "Defense/SpikePit", StakeField = "Defense/StakeField", BearTrap = "Defense/BearTrap",
            BarbedWire = "Defense/BarbedWire", Landmine = "Defense/Landmine",
            ArcherTower = "Defense/ArcherTower", BallistaTower = "Defense/BallistaTower", CannonTower = "Defense/CannonTower",
            GunNest = "Defense/GunNest";

        /// <summary>Directions a tower's head is drawn for (the rest of the way it's turned).</summary>
        public const int HeadDirections = 16;

        /// <summary>A trap or tower's pictures: its base, the base once sprung (traps), and a turning head (towers).</summary>
        public struct DefensePiece
        {
            public string Art;
            public System.Func<int, PixelImage> Draw;
            /// <summary>Drawn once it's gone off (a sprung trap), or null.</summary>
            public System.Func<int, PixelImage> Sprung;
            /// <summary>A head that turns to aim, drawn facing an angle (degrees), or null.</summary>
            public System.Func<int, float, PixelImage> Head;
            /// <summary>Lies on the ground: no shadow.</summary>
            public bool Flat;
        }

        public static readonly DefensePiece[] Defenses =
        {
            new DefensePiece { Art = SpikePit, Draw = p => DrawSpikePit(p, false), Sprung = p => DrawSpikePit(p, true), Flat = true },
            new DefensePiece { Art = StakeField, Draw = DrawStakeField },
            new DefensePiece { Art = BearTrap, Draw = p => DrawBearTrap(p, false), Sprung = p => DrawBearTrap(p, true), Flat = true },
            new DefensePiece { Art = BarbedWire, Draw = DrawBarbedWire },
            new DefensePiece { Art = Landmine, Draw = DrawLandmine, Sprung = DrawCrater, Flat = true },
            new DefensePiece { Art = ArcherTower, Draw = DrawArcherTower },
            new DefensePiece { Art = BallistaTower, Draw = DrawBallistaTower, Head = DrawBallista },
            new DefensePiece { Art = CannonTower, Draw = DrawCannonTower, Head = DrawCannon },
            new DefensePiece { Art = GunNest, Draw = DrawGunNest, Head = DrawMachineGun },
            // The rest, age by age (DefenseArt2.cs).
            new DefensePiece { Art = FirePit, Draw = DrawFirePit, Flat = true },
            new DefensePiece { Art = SlingPost, Draw = DrawSlingPost },
            new DefensePiece { Art = SpikePlate, Draw = p => DrawSpikePlate(p, false), Sprung = p => DrawSpikePlate(p, true), Flat = true },
            new DefensePiece { Art = TarPit, Draw = DrawTarPit, Flat = true },
            new DefensePiece { Art = ThornHedge, Draw = DrawThornHedge },
            new DefensePiece { Art = Caltrops, Draw = DrawCaltrops, Flat = true },
            new DefensePiece { Art = Abatis, Draw = DrawAbatis },
            new DefensePiece { Art = Catapult, Draw = DrawCatapultBase, Head = DrawCatapult },
            new DefensePiece { Art = BoilingOil, Draw = DrawBoilingOil },
            new DefensePiece { Art = CrossbowTower, Draw = DrawCrossbowTower },
            new DefensePiece { Art = Trebuchet, Draw = DrawTrebuchetBase, Head = DrawTrebuchet },
            new DefensePiece { Art = PowderKeg, Draw = DrawPowderKeg, Sprung = DrawCrater },
            new DefensePiece { Art = ChevalDeFrise, Draw = DrawChevalDeFrise },
            new DefensePiece { Art = MusketTower, Draw = DrawMusketTower },
            new DefensePiece { Art = SpringSnare, Draw = p => DrawSpringSnare(p, false), Sprung = p => DrawSpringSnare(p, true), Flat = true },
            new DefensePiece { Art = Dynamite, Draw = DrawDynamite, Sprung = DrawCrater, Flat = true },
            new DefensePiece { Art = Gatling, Draw = DrawGatlingBase, Head = DrawGatling },
            new DefensePiece { Art = GasTrap, Draw = p => DrawGasTrap(p, false), Sprung = p => DrawGasTrap(p, true), Flat = true },
            new DefensePiece { Art = SandbagWall, Draw = DrawSandbagWall },
            new DefensePiece { Art = MortarPit, Draw = DrawMortarPit, Head = DrawMortar },
            new DefensePiece { Art = BouncingMine, Draw = DrawBouncingMine, Sprung = DrawCrater, Flat = true },
            new DefensePiece { Art = NapalmTrap, Draw = p => DrawNapalmTrap(p, false), Sprung = p => DrawNapalmTrap(p, true), Flat = true },
            new DefensePiece { Art = Hedgehog, Draw = DrawHedgehog },
            new DefensePiece { Art = FlameBunker, Draw = DrawFlameBunker, Head = DrawFlamethrower },
            // Machines (MachineArt.cs).
            new DefensePiece { Art = Smelter, Draw = DrawSmelter },
            new DefensePiece { Art = Generator, Draw = DrawGenerator },
            new DefensePiece { Art = Sawmill, Draw = DrawSawmill },
            new DefensePiece { Art = Quarry, Draw = DrawQuarry },
            new DefensePiece { Art = Conveyor, Draw = DrawConveyor, Flat = true },
            new DefensePiece { Art = Howitzer, Draw = DrawHowitzerBase, Head = DrawHowitzer },
        };

        static readonly Color32 Earth = PixelImage.Rgb(78, 62, 44);
        static readonly Color32 Sand = PixelImage.Rgb(146, 128, 94);
        static readonly Color32 PaleWood = PixelImage.Rgb(150, 118, 78);

        // ------------------------------------------------------------- traps --

        // A pit dug in the ground: covered with sticks and leaves until something falls in, then open with its stakes showing.
        static PixelImage DrawSpikePit(int ppu, bool sprung)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            float c = 0.5f;
            Ring(img, t, c, c, 0.34f, 0.47f, Earth, 0.03f, Surface.Stone, 901u);   // the dug-up earth round it
            var dice = new Dice(sprung ? 902u : 903u);
            if (sprung)
            {
                Round(img, t, c, c, 0.35f, PixelImage.Rgb(24, 18, 13), 0f, 0f, Surface.Stone, 904u);
                for (int i = 0; i < 9; i++)
                {
                    float a = dice.Range(0f, Mathf.PI * 2f), r = dice.Range(0.05f, 0.27f);
                    float x = c + Mathf.Cos(a) * r, y = c + Mathf.Sin(a) * r;
                    Rod(img, t, x - 0.02f, y - 0.03f, x, y, 0.016f, PaleWood, 0.005f, 0.05f);
                    Stud(img, t, x, y, 0.012f, PixelImage.Rgb(196, 170, 128), 0.05f);   // sharpened points
                }
                // Something fell in: a dark stain.
                Round(img, t, c + 0.05f, c - 0.03f, 0.12f, PixelImage.Rgb(60, 10, 12, 180), 0.001f, 0f, Surface.Plain, 905u);
            }
            else
            {
                Round(img, t, c, c, 0.35f, PixelImage.Rgb(58, 46, 30), 0.01f, 0f, Surface.Stone, 906u);
                // Thin branches across it, then leaves on top: it looks like ground.
                for (int i = 0; i < 9; i++)
                {
                    float a = dice.Range(0f, Mathf.PI), off = dice.Range(-0.25f, 0.25f);
                    float dx = Mathf.Cos(a) * 0.36f, dy = Mathf.Sin(a) * 0.36f;
                    Rod(img, t, c - dx - dy * off, c - dy + dx * off, c + dx - dy * off, c + dy + dx * off, 0.008f, DarkWood, 0.02f, 0.02f);
                }
                for (int i = 0; i < 90; i++)
                {
                    float a = dice.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(dice.Next()) * 0.36f;
                    var leaf = PixelImage.Mix(PixelImage.Rgb(92, 76, 38), PixelImage.Rgb(62, 74, 36), dice.Next());
                    Round(img, t, c + Mathf.Cos(a) * r, c + Mathf.Sin(a) * r, dice.Range(0.015f, 0.035f), leaf, 0.025f, 0.004f, Surface.Cloth, 907u + (uint)i);
                }
            }
            return Finish(img, t, 0, 0.7f, 0.6f);
        }

        // Sharpened stakes driven into the ground at angles, points outwards.
        static PixelImage DrawStakeField(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var dice = new Dice(911u);
            for (int i = 0; i < 40; i++)
            {
                float x = dice.Range(0.08f, 0.92f), y = dice.Range(0.08f, 0.92f);
                Round(img, t, x, y, dice.Range(0.03f, 0.07f), PixelImage.Scale(Earth, dice.Range(0.8f, 1.1f)), 0.004f, 0.002f, Surface.Stone, 912u + (uint)i);
            }
            for (int i = 0; i < 7; i++)
            {
                float x = dice.Range(0.15f, 0.85f), y = dice.Range(0.15f, 0.85f);
                float a = dice.Range(0f, Mathf.PI * 2f), len = dice.Range(0.16f, 0.24f);
                float tx = x + Mathf.Cos(a) * len, ty = y + Mathf.Sin(a) * len;
                Rod(img, t, x, y, tx, ty, 0.03f, PixelImage.Scale(Oak, dice.Range(0.85f, 1.1f)), 0.02f, 0.28f);
                Rod(img, t, tx - Mathf.Cos(a) * 0.04f, ty - Mathf.Sin(a) * 0.04f, tx, ty, 0.016f, PaleWood, 0.27f, 0.3f);   // the sharpened end
            }
            return Finish(img, t, 0);
        }

        // Steel jaws with teeth, open on a plate (or snapped shut), chained to a stake.
        static PixelImage DrawBearTrap(int ppu, bool sprung)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            float c = 0.5f;
            Rod(img, t, c + 0.2f, c - 0.05f, 0.85f, 0.2f, 0.01f, PixelImage.Scale(Iron, 1.2f), 0.02f, 0.02f);   // chain
            Round(img, t, 0.86f, 0.19f, 0.04f, DarkWood, 0.05f, 0.01f, Surface.Wood, 921u);                       // its stake
            if (!sprung)
            {
                Ring(img, t, c, c, 0.2f, 0.25f, Iron, 0.03f, Surface.Metal, 922u);
                Round(img, t, c, c, 0.09f, PixelImage.Scale(Iron, 0.8f), 0.02f, 0f, Surface.Metal, 923u);   // the plate it's stepped on
                for (int i = 0; i < 18; i++)
                {
                    float a = i * Mathf.PI * 2f / 18f;
                    Rod(img, t, c + Mathf.Cos(a) * 0.2f, c + Mathf.Sin(a) * 0.2f, c + Mathf.Cos(a) * 0.15f, c + Mathf.Sin(a) * 0.15f,
                        0.01f, PixelImage.Scale(Iron, 1.35f), 0.035f, 0.03f);   // teeth
                }
            }
            else
            {
                // Snapped shut: the two jaws meet in a line, teeth locked.
                Rod(img, t, c - 0.24f, c, c + 0.24f, c, 0.05f, Iron, 0.07f, 0.07f);
                for (int i = 0; i < 9; i++)
                {
                    float x = c - 0.2f + i * 0.05f;
                    Stud(img, t, x, c + 0.02f * (i % 2 == 0 ? 1 : -1), 0.012f, PixelImage.Scale(Iron, 1.4f), 0.09f);
                }
                Round(img, t, c + 0.04f, c + 0.06f, 0.1f, PixelImage.Rgb(70, 12, 14, 170), 0.001f, 0f, Surface.Plain, 924u);
            }
            return Finish(img, t, 0);
        }

        // Coils of barbed wire on two posts.
        static PixelImage DrawBarbedWire(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var wire = PixelImage.Rgb(92, 90, 88);
            foreach (float x in new[] { 0.12f, 0.88f })
                Round(img, t, x, 0.5f, 0.05f, DarkWood, 0.4f, 0.01f, Surface.Wood, 931u);
            var dice = new Dice(932u);
            // A long coil: loops along the line between the posts.
            for (int i = 0; i < 9; i++)
            {
                float x = 0.14f + i * 0.09f;
                Ring(img, t, x, 0.5f + dice.Range(-0.03f, 0.03f), 0.13f, 0.142f, wire, 0.22f, Surface.Metal, 933u + (uint)i);
            }
            foreach (float y in new[] { 0.36f, 0.5f, 0.64f })
                Rod(img, t, 0.12f, y, 0.88f, y + dice.Range(-0.03f, 0.03f), 0.006f, wire, 0.3f, 0.3f);
            for (int i = 0; i < 40; i++)
            {
                float x = dice.Range(0.12f, 0.88f), y = 0.5f + dice.Range(-0.16f, 0.16f);
                Rod(img, t, x - 0.012f, y - 0.012f, x + 0.012f, y + 0.012f, 0.004f, PixelImage.Scale(wire, 1.2f), 0.3f, 0.3f);   // barbs
            }
            return Finish(img, t, 0);
        }

        // A mine just under the ground: a metal lid with its prongs poking out of loose earth.
        static PixelImage DrawLandmine(int ppu)
        {
            float t = ppu;
            var img = Canvas(0.5f, 0.5f, t);
            float c = 0.25f;
            Round(img, t, c, c, 0.2f, PixelImage.Scale(Earth, 0.9f), 0.01f, 0.01f, Surface.Stone, 941u);
            Round(img, t, c, c, 0.12f, PixelImage.Rgb(70, 74, 58), 0.025f, 0.01f, Surface.Metal, 942u);
            Ring(img, t, c, c, 0.11f, 0.13f, PixelImage.Rgb(56, 60, 46), 0.03f, Surface.Metal, 943u);
            for (int i = 0; i < 3; i++)
            {
                float a = 0.4f + i * Mathf.PI * 2f / 3f;
                Stud(img, t, c + Mathf.Cos(a) * 0.04f, c + Mathf.Sin(a) * 0.04f, 0.012f, PixelImage.Rgb(150, 140, 110), 0.04f);
            }
            return Finish(img, t, 0);
        }

        // Where a mine went off.
        static PixelImage DrawCrater(int ppu)
        {
            float t = ppu;
            var img = Canvas(0.9f, 0.9f, t);
            float c = 0.45f;
            Ring(img, t, c, c, 0.22f, 0.42f, PixelImage.Rgb(52, 42, 32), 0.02f, Surface.Stone, 951u);
            Round(img, t, c, c, 0.24f, PixelImage.Rgb(24, 20, 17), 0f, 0f, Surface.Stone, 952u);
            return Finish(img, t, 0, 0.7f, 0.7f);
        }

        // ------------------------------------------------------------ towers --

        // A wooden lookout two tiles across: a plank floor on four log posts with a fence of sharpened logs (the guard stands on it).
        static PixelImage DrawArcherTower(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Block(img, t, 0.14f, 0.14f, 1.86f, 1.86f, Oak, 0.8f, 0.02f, Surface.Wood, 961u, true, 0.17f);
            Block(img, t, 0.8f, 0.2f, 1.2f, 0.5f, PixelImage.Rgb(30, 22, 16), 0.79f, 0.01f, Surface.Wood, 962u);   // the hatch the ladder comes up through
            // The fence: a ring of log ends round the edge.
            for (int side = 0; side < 4; side++)
                for (int i = 0; i < 10; i++)
                {
                    float s = 0.17f + i * 0.185f;
                    float x = side == 0 ? s : side == 1 ? 1.83f : side == 2 ? 2f - s : 0.17f;
                    float y = side == 0 ? 0.17f : side == 1 ? s : side == 2 ? 1.83f : 2f - s;
                    Round(img, t, x, y, 0.085f, PixelImage.Scale(DarkWood, 1f + (i % 3) * 0.08f), 0.95f, 0.03f, Surface.Wood, 963u + (uint)(side * 10 + i));
                    Stud(img, t, x, y, 0.03f, PaleWood, 1.0f);   // the sharpened top
                }
            foreach (var (x, y) in new[] { (0.2f, 0.2f), (1.8f, 0.2f), (0.2f, 1.8f), (1.8f, 1.8f) })
                Round(img, t, x, y, 0.13f, DarkWood, 1.05f, 0.03f, Surface.Wood, 970u);
            return Finish(img, t, 0);
        }

        // A stone platform with battlements; the ballista turns on top.
        static PixelImage DrawBallistaTower(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            StonePlatform(img, t, 971u);
            return Finish(img, t, 0);
        }

        // A round stone tower with a parapet; the cannon turns on top.
        static PixelImage DrawCannonTower(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Round(img, t, 1f, 1f, 0.95f, Granite, 0.9f, 0f, Surface.Stone, 981u);
            Round(img, t, 1f, 1f, 0.72f, PixelImage.Scale(Granite, 0.8f), 0.85f, 0f, Surface.Stone, 982u);   // the floor inside
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                if (i % 2 == 0)
                    Round(img, t, 1f + Mathf.Cos(a) * 0.84f, 1f + Mathf.Sin(a) * 0.84f, 0.12f, PixelImage.Scale(Granite, 1.05f), 1.05f, 0.02f, Surface.Stone, 983u + (uint)i);
            }
            Ring(img, t, 1f, 1f, 0.72f, 0.96f, PixelImage.Scale(Granite, 0.95f), 0.95f, Surface.Stone, 990u);
            return Finish(img, t, 0);
        }

        // A ring of sandbags round a dug-in gun position.
        static PixelImage DrawGunNest(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Round(img, t, 1f, 1f, 0.75f, PixelImage.Scale(Earth, 0.8f), 0.01f, 0f, Surface.Stone, 991u);
            var dice = new Dice(992u);
            for (int ring = 0; ring < 2; ring++)
            {
                int bags = ring == 0 ? 16 : 13;
                float r = ring == 0 ? 0.82f : 0.66f, offset = ring * 0.2f;
                for (int i = 0; i < bags; i++)
                {
                    float a = offset + i * Mathf.PI * 2f / bags;
                    float x = 1f + Mathf.Cos(a) * r, y = 1f + Mathf.Sin(a) * r;
                    float along = 0.16f, across = 0.09f;
                    // Each bag lies along the ring.
                    var dir = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                    Rod(img, t, x - dir.x * along * 0.5f, y - dir.y * along * 0.5f, x + dir.x * along * 0.5f, y + dir.y * along * 0.5f,
                        across, PixelImage.Scale(Sand, dice.Range(0.85f, 1.05f)), 0.3f + ring * 0.14f, 0.3f + ring * 0.14f);
                }
            }
            return Finish(img, t, 0);
        }

        static void StonePlatform(PixelImage img, float t, uint seed)
        {
            Block(img, t, 0.06f, 0.06f, 1.94f, 1.94f, Granite, 0.85f, 0.03f, Surface.Stone, seed);
            Block(img, t, 0.3f, 0.3f, 1.7f, 1.7f, PixelImage.Scale(Granite, 0.82f), 0.82f, 0.01f, Surface.Stone, seed + 1u);
            for (int side = 0; side < 4; side++)
                for (int i = 0; i < 4; i++)
                {
                    float s = 0.24f + i * 0.51f;
                    float x = side == 0 || side == 2 ? s : side == 1 ? 1.82f : 0.18f;
                    float y = side == 1 || side == 3 ? s : side == 0 ? 0.18f : 1.82f;
                    Block(img, t, x - 0.13f, y - 0.13f, x + 0.13f, y + 0.13f, PixelImage.Scale(Granite, 1.06f), 1.05f, 0.02f, Surface.Stone, seed + 2u + (uint)(side * 4 + i));
                }
        }

        // --------------------------------------------------------------- heads --
        // Each head is drawn turned to `degrees` (0 = facing right), then lit, so the light is right whichever way it faces.

        static PixelImage DrawBallista(int ppu, float degrees)
        {
            float t = ppu;
            var img = Canvas(1.4f, 1.4f, t);
            var h = new Head(0.7f, 0.7f, degrees);
            Round(img, t, 0.7f, 0.7f, 0.16f, DarkWood, 0.95f, 0.02f, Surface.Wood, 1001u);       // the swivel
            h.Rod(img, t, -0.25f, 0f, 0.42f, 0f, 0.05f, Oak, 1.1f, 1.1f);                          // the stock
            h.Rod(img, t, 0.3f, 0f, 0.12f, 0.42f, 0.035f, DarkWood, 1.12f, 1.12f);                 // the bow's arms
            h.Rod(img, t, 0.3f, 0f, 0.12f, -0.42f, 0.035f, DarkWood, 1.12f, 1.12f);
            h.Rod(img, t, 0.12f, 0.42f, -0.12f, 0f, 0.006f, PixelImage.Rgb(200, 190, 160), 1.13f, 1.13f);   // the string, drawn back
            h.Rod(img, t, 0.12f, -0.42f, -0.12f, 0f, 0.006f, PixelImage.Rgb(200, 190, 160), 1.13f, 1.13f);
            h.Rod(img, t, -0.12f, 0f, 0.55f, 0f, 0.012f, PaleWood, 1.16f, 1.16f);                  // the bolt
            h.Stud(img, t, 0.56f, 0f, 0.02f, Iron, 1.17f);
            return Finish(img, t, 0);
        }

        static PixelImage DrawCannon(int ppu, float degrees)
        {
            float t = ppu;
            var img = Canvas(1.4f, 1.4f, t);
            var h = new Head(0.7f, 0.7f, degrees);
            h.Block(img, t, -0.32f, -0.2f, 0.18f, 0.2f, DarkWood, 1.0f);                           // the carriage
            foreach (float side in new[] { -0.22f, 0.22f })
                h.Rod(img, t, -0.12f, side, 0.06f, side, 0.06f, PixelImage.Scale(DarkWood, 0.8f), 0.98f, 0.98f);   // its wheels, seen edge on
            h.Rod(img, t, -0.3f, 0f, 0.58f, 0f, 0.1f, PixelImage.Rgb(46, 46, 50), 1.15f, 1.12f);    // the barrel
            h.Rod(img, t, 0.5f, 0f, 0.6f, 0f, 0.12f, PixelImage.Rgb(54, 54, 58), 1.14f, 1.14f);     // the muzzle ring
            h.Round(img, t, 0.61f, 0f, 0.06f, PixelImage.Rgb(10, 10, 12), 1.16f);                  // the bore
            h.Round(img, t, -0.33f, 0f, 0.07f, PixelImage.Rgb(50, 50, 54), 1.15f);                 // the knob at the back
            return Finish(img, t, 0);
        }

        static PixelImage DrawMachineGun(int ppu, float degrees)
        {
            float t = ppu;
            var img = Canvas(1.4f, 1.4f, t);
            var h = new Head(0.7f, 0.7f, degrees);
            var steel = PixelImage.Rgb(58, 62, 54);
            foreach (float a in new[] { 140f, 220f, 10f })
            {
                var leg = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * 0.38f;
                h.Rod(img, t, 0f, 0f, leg.x, leg.y, 0.028f, PixelImage.Rgb(52, 54, 50), 0.75f, 0.45f);   // the tripod
            }
            h.Rod(img, t, -0.2f, 0f, 0.32f, 0f, 0.09f, steel, 0.95f, 0.95f);                       // the body (water jacket)
            for (int i = 0; i < 5; i++) h.Rod(img, t, -0.1f + i * 0.08f, -0.09f, -0.1f + i * 0.08f, 0.09f, 0.008f, PixelImage.Scale(steel, 0.75f), 0.99f, 0.99f);
            h.Rod(img, t, 0.32f, 0f, 0.62f, 0f, 0.03f, PixelImage.Rgb(40, 40, 42), 0.97f, 0.97f);   // the barrel
            h.Round(img, t, 0.63f, 0f, 0.035f, PixelImage.Rgb(30, 30, 32), 0.98f);                 // the muzzle
            h.Block(img, t, 0.1f, -0.3f, 0.16f, 0.3f, PixelImage.Rgb(64, 68, 58), 1.02f);            // the shield
            h.Rod(img, t, -0.06f, 0.16f, 0.08f, 0.16f, 0.07f, PixelImage.Rgb(96, 82, 52), 0.9f, 0.9f); // the ammo box
            h.Rod(img, t, 0.0f, 0.1f, 0.04f, 0.04f, 0.015f, PixelImage.Rgb(180, 150, 70), 0.97f, 0.97f); // the belt
            h.Rod(img, t, -0.3f, -0.07f, -0.2f, 0f, 0.022f, PixelImage.Rgb(40, 40, 42), 0.95f, 0.95f);   // the grips
            h.Rod(img, t, -0.3f, 0.07f, -0.2f, 0f, 0.022f, PixelImage.Rgb(40, 40, 42), 0.95f, 0.95f);
            return Finish(img, t, 0);
        }

        // Draws a head's parts in its own frame: x forwards (where it aims), y to its left.
        readonly struct Head
        {
            readonly float cx, cy;
            readonly Vector2 forward, left;

            public Head(float x, float y, float degrees)
            {
                cx = x;
                cy = y;
                float a = degrees * Mathf.Deg2Rad;
                forward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                left = new Vector2(-forward.y, forward.x);
            }

            Vector2 P(float x, float y) => new Vector2(cx, cy) + forward * x + left * y;

            public void Rod(PixelImage img, float t, float x0, float y0, float x1, float y1, float r, Color32 color, float h0, float h1)
            {
                var a = P(x0, y0);
                var b = P(x1, y1);
                FurnitureArt.Rod(img, t, a.x, a.y, b.x, b.y, r, color, h0, h1);
            }

            public void Stud(PixelImage img, float t, float x, float y, float r, Color32 color, float top)
            {
                var a = P(x, y);
                FurnitureArt.Stud(img, t, a.x, a.y, r, color, top);
            }

            public void Round(PixelImage img, float t, float x, float y, float r, Color32 color, float top)
            {
                var a = P(x, y);
                FurnitureArt.Round(img, t, a.x, a.y, r, color, top, 0.01f, Surface.Metal, 1009u);
            }

            // A block in the head's frame: drawn as thick rods side by side (it's turned).
            public void Block(PixelImage img, float t, float x0, float y0, float x1, float y1, Color32 color, float top)
            {
                float r = Mathf.Min(x1 - x0, y1 - y0) * 0.5f;
                if (x1 - x0 >= y1 - y0) Rod(img, t, x0 + r, (y0 + y1) * 0.5f, x1 - r, (y0 + y1) * 0.5f, r, color, top, top);
                else Rod(img, t, (x0 + x1) * 0.5f, y0 + r, (x0 + x1) * 0.5f, y1 - r, r, color, top, top);
            }
        }
    }
}
