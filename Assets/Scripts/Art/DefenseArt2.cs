using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>The rest of the traps, barriers and towers, one set per age (see <see cref="Defenses"/>).</summary>
    public static partial class FurnitureArt
    {
        public const string FirePit = "Defense/FirePit", SlingPost = "Defense/SlingPost",
            SpikePlate = "Defense/SpikePlate", TarPit = "Defense/TarPit", ThornHedge = "Defense/ThornHedge",
            Caltrops = "Defense/Caltrops", Abatis = "Defense/Abatis", Catapult = "Defense/Catapult",
            BoilingOil = "Defense/BoilingOil", CrossbowTower = "Defense/CrossbowTower", Trebuchet = "Defense/Trebuchet",
            PowderKeg = "Defense/PowderKeg", ChevalDeFrise = "Defense/ChevalDeFrise", MusketTower = "Defense/MusketTower",
            SpringSnare = "Defense/SpringSnare", Dynamite = "Defense/Dynamite", Gatling = "Defense/Gatling",
            GasTrap = "Defense/GasTrap", SandbagWall = "Defense/SandbagWall", MortarPit = "Defense/MortarPit",
            BouncingMine = "Defense/BouncingMine", NapalmTrap = "Defense/NapalmTrap", Hedgehog = "Defense/Hedgehog",
            FlameBunker = "Defense/FlameBunker", Howitzer = "Defense/Howitzer";

        static readonly Color32 Concrete = PixelImage.Rgb(122, 120, 112);
        static readonly Color32 Brick = PixelImage.Rgb(132, 70, 52);
        static readonly Color32 Steel = PixelImage.Rgb(66, 70, 64);

        // ------------------------------------------------------- Stone Age --

        // A ring of stones round burning logs: zombies walking through it burn (the flames are drawn by the game).
        static PixelImage DrawFirePit(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            float c = 0.5f;
            Round(img, t, c, c, 0.36f, PixelImage.Rgb(34, 26, 20), 0.01f, 0f, Surface.Stone, 1101u);   // ash
            var dice = new Dice(1102u);
            for (int i = 0; i < 11; i++)
            {
                float a = i * Mathf.PI * 2f / 11f + dice.Range(-0.1f, 0.1f);
                Round(img, t, c + Mathf.Cos(a) * 0.38f, c + Mathf.Sin(a) * 0.38f, dice.Range(0.06f, 0.09f), PixelImage.Scale(Granite, dice.Range(0.8f, 1.1f)), 0.07f, 0.03f, Surface.Stone, 1103u + (uint)i);
            }
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f + 0.3f;
                Rod(img, t, c + Mathf.Cos(a) * 0.08f, c + Mathf.Sin(a) * 0.08f, c + Mathf.Cos(a) * 0.3f, c + Mathf.Sin(a) * 0.3f, 0.04f, PixelImage.Rgb(40, 28, 20), 0.08f, 0.04f);
            }
            for (int i = 0; i < 40; i++)
            {
                float a = dice.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(dice.Next()) * 0.24f;
                Stud(img, t, c + Mathf.Cos(a) * r, c + Mathf.Sin(a) * r, dice.Range(0.012f, 0.025f),
                    dice.Chance(0.55f) ? PixelImage.Rgb(255, 120, 40) : PixelImage.Rgb(60, 34, 24), 0.06f);   // embers
            }
            return Finish(img, t, 0);
        }

        // A stump with a stone pile: the guard stands on it and slings stones.
        static PixelImage DrawSlingPost(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            Round(img, t, 0.5f, 0.5f, 0.42f, PaleWood, 0.45f, 0.02f, Surface.Wood, 1111u);
            Ring(img, t, 0.5f, 0.5f, 0.36f, 0.43f, Oak, 0.46f, Surface.Wood, 1112u);   // bark
            var dice = new Dice(1113u);
            for (int i = 0; i < 9; i++)
                Round(img, t, 0.72f + dice.Range(-0.08f, 0.08f), 0.28f + dice.Range(-0.08f, 0.08f), dice.Range(0.03f, 0.05f), Granite, 0.5f, 0.02f, Surface.Stone, 1114u + (uint)i);
            return Finish(img, t, 0);
        }

        // ------------------------------------------------------ Bronze Age --

        // A plate of bronze spikes hidden under a board: everything on it gets stabbed when it springs.
        static PixelImage DrawSpikePlate(int ppu, bool sprung)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var bronze = PixelImage.Rgb(150, 104, 52);
            Block(img, t, 0.08f, 0.08f, 0.92f, 0.92f, PixelImage.Scale(Earth, 0.9f), 0.01f, 0.01f, Surface.Stone, 1121u);
            if (!sprung)
                Block(img, t, 0.12f, 0.12f, 0.88f, 0.88f, PixelImage.Scale(Oak, 0.9f), 0.03f, 0.01f, Surface.Wood, 1122u, true, 0.19f);
            else
                for (int y = 0; y < 5; y++)
                    for (int x = 0; x < 5; x++)
                        Stud(img, t, 0.18f + x * 0.16f, 0.18f + y * 0.16f, 0.035f, bronze, 0.12f);
            return Finish(img, t, 0);
        }

        // A pit of sticky black tar: zombies wade through it very slowly.
        static PixelImage DrawTarPit(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            float c = 0.5f;
            Ring(img, t, c, c, 0.36f, 0.47f, Earth, 0.02f, Surface.Stone, 1131u);
            Round(img, t, c, c, 0.39f, PixelImage.Rgb(16, 14, 12), 0.01f, 0.004f, Surface.Wax, 1132u);
            var dice = new Dice(1133u);
            for (int i = 0; i < 8; i++)
            {
                float a = dice.Range(0f, Mathf.PI * 2f), r = dice.Range(0f, 0.28f);
                Ring(img, t, c + Mathf.Cos(a) * r, c + Mathf.Sin(a) * r, 0.02f, 0.035f, PixelImage.Rgb(36, 32, 28), 0.016f, Surface.Wax, 1134u + (uint)i);   // bubbles
            }
            return Finish(img, t, 0, 0.9f, 0.3f);
        }

        // A thick hedge of thorny bushes: zombies must tear through it, and get torn.
        static PixelImage DrawThornHedge(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var dice = new Dice(1141u);
            for (int i = 0; i < 26; i++)
            {
                float x = dice.Range(0.12f, 0.88f), y = dice.Range(0.12f, 0.88f);
                var green = PixelImage.Mix(PixelImage.Rgb(44, 62, 30), PixelImage.Rgb(70, 76, 36), dice.Next());
                Round(img, t, x, y, dice.Range(0.1f, 0.16f), green, 0.35f, 0.12f, Surface.Cloth, 1142u + (uint)i);
            }
            for (int i = 0; i < 60; i++)
            {
                float x = dice.Range(0.08f, 0.92f), y = dice.Range(0.08f, 0.92f), a = dice.Range(0f, Mathf.PI * 2f);
                Rod(img, t, x, y, x + Mathf.Cos(a) * 0.05f, y + Mathf.Sin(a) * 0.05f, 0.006f, PixelImage.Rgb(120, 96, 64), 0.5f, 0.52f);   // thorns
            }
            return Finish(img, t, 0);
        }

        // -------------------------------------------------------- Iron Age --

        static PixelImage DrawCaltrops(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var dice = new Dice(1151u);
            for (int i = 0; i < 22; i++)
            {
                float x = dice.Range(0.1f, 0.9f), y = dice.Range(0.1f, 0.9f), a = dice.Range(0f, Mathf.PI * 2f);
                for (int k = 0; k < 3; k++)
                {
                    float b = a + k * Mathf.PI * 2f / 3f;
                    Rod(img, t, x, y, x + Mathf.Cos(b) * 0.04f, y + Mathf.Sin(b) * 0.04f, 0.008f, PixelImage.Scale(Iron, 1.3f), 0.02f, 0.01f);
                }
                Stud(img, t, x, y, 0.01f, PixelImage.Scale(Iron, 1.5f), 0.04f);   // the spike pointing up
            }
            return Finish(img, t, 0);
        }

        // Felled trees with sharpened branches, laid with their points outwards.
        static PixelImage DrawAbatis(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            Rod(img, t, 0.1f, 0.5f, 0.9f, 0.5f, 0.09f, Oak, 0.18f, 0.18f);
            var dice = new Dice(1161u);
            for (int i = 0; i < 9; i++)
            {
                float x = 0.15f + i * 0.09f, side = i % 2 == 0 ? 1f : -1f;
                float tx = x + dice.Range(-0.06f, 0.06f), ty = 0.5f + side * dice.Range(0.28f, 0.4f);
                Rod(img, t, x, 0.5f, tx, ty, 0.025f, PixelImage.Scale(DarkWood, 1.1f), 0.22f, 0.32f);
                Stud(img, t, tx, ty, 0.016f, PaleWood, 0.33f);
            }
            return Finish(img, t, 0);
        }

        // A wooden platform with a throwing arm on a swivel.
        static PixelImage DrawCatapultBase(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Block(img, t, 0.12f, 0.12f, 1.88f, 1.88f, Oak, 0.35f, 0.02f, Surface.Wood, 1171u, true, 0.22f);
            foreach (var (x, y) in new[] { (0.2f, 0.2f), (1.8f, 0.2f), (0.2f, 1.8f), (1.8f, 1.8f) })
                Round(img, t, x, y, 0.1f, DarkWood, 0.42f, 0.02f, Surface.Wood, 1172u);
            return Finish(img, t, 0);
        }

        static PixelImage DrawCatapult(int ppu, float degrees)
        {
            float t = ppu;
            var img = Canvas(1.6f, 1.6f, t);
            var h = new Head(0.8f, 0.8f, degrees);
            h.Rod(img, t, -0.35f, 0.25f, 0.35f, 0.25f, 0.05f, DarkWood, 0.5f, 0.5f);    // the frame
            h.Rod(img, t, -0.35f, -0.25f, 0.35f, -0.25f, 0.05f, DarkWood, 0.5f, 0.5f);
            h.Rod(img, t, 0.2f, 0.28f, 0.2f, -0.28f, 0.05f, DarkWood, 0.7f, 0.7f);      // the crossbar it hits
            h.Rod(img, t, -0.3f, 0f, 0.4f, 0f, 0.04f, Oak, 0.55f, 0.75f);              // the arm
            h.Round(img, t, -0.32f, 0f, 0.1f, PixelImage.Rgb(90, 70, 46), 0.6f);        // the bucket
            h.Round(img, t, -0.32f, 0f, 0.06f, Granite, 0.66f);                         // a stone in it
            return Finish(img, t, 0);
        }

        // -------------------------------------------------------- Medieval --

        // A cauldron of boiling oil on a stone hearth: the guard pours it on zombies close by.
        static PixelImage DrawBoilingOil(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            Block(img, t, 0.06f, 0.06f, 0.94f, 0.94f, Granite, 0.3f, 0.03f, Surface.Stone, 1181u);
            Round(img, t, 0.5f, 0.5f, 0.3f, PixelImage.Rgb(36, 36, 40), 0.6f, 0f, Surface.Metal, 1182u);
            Round(img, t, 0.5f, 0.5f, 0.24f, PixelImage.Rgb(92, 66, 22), 0.58f, 0f, Surface.Wax, 1183u);   // the oil
            Ring(img, t, 0.5f, 0.5f, 0.24f, 0.31f, Iron, 0.65f, Surface.Metal, 1184u);
            return Finish(img, t, 0);
        }

        // A tall square stone tower: a crossbowman shoots from the top.
        static PixelImage DrawCrossbowTower(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            StonePlatform(img, t, 1191u);
            Block(img, t, 0.85f, 0.85f, 1.15f, 1.15f, PixelImage.Rgb(30, 26, 22), 0.83f, 0.01f, Surface.Wood, 1195u);   // the stair hatch
            return Finish(img, t, 0);
        }

        // A big wooden frame; its long arm turns to aim.
        static PixelImage DrawTrebuchetBase(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Block(img, t, 0.2f, 0.2f, 1.8f, 1.8f, PixelImage.Scale(Earth, 0.95f), 0.02f, 0.02f, Surface.Stone, 1201u);
            foreach (var (x0, y0, x1, y1) in new[] { (0.25f, 0.3f, 1.75f, 0.3f), (0.25f, 1.7f, 1.75f, 1.7f), (0.3f, 0.25f, 0.3f, 1.75f), (1.7f, 0.25f, 1.7f, 1.75f) })
                Rod(img, t, x0, y0, x1, y1, 0.07f, DarkWood, 0.2f, 0.2f);
            return Finish(img, t, 0);
        }

        static PixelImage DrawTrebuchet(int ppu, float degrees)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            var h = new Head(1f, 1f, degrees);
            h.Rod(img, t, -0.1f, 0.35f, -0.1f, -0.35f, 0.06f, DarkWood, 1.0f, 1.0f);   // the axle on its posts
            h.Rod(img, t, -0.55f, 0f, 0.8f, 0f, 0.05f, Oak, 1.05f, 1.1f);               // the long arm
            h.Block(img, t, -0.75f, -0.2f, -0.45f, 0.2f, PixelImage.Rgb(80, 62, 44), 0.9f);   // the counterweight box
            h.Rod(img, t, 0.8f, 0f, 0.6f, 0.15f, 0.008f, PixelImage.Rgb(170, 150, 110), 1.1f, 1.05f);   // the sling
            h.Round(img, t, 0.58f, 0.16f, 0.06f, Granite, 1.06f);
            return Finish(img, t, 0);
        }

        // ----------------------------------------------------- Renaissance --

        static PixelImage DrawPowderKeg(int ppu)
        {
            float t = ppu;
            var img = Canvas(0.7f, 0.7f, t);
            float c = 0.35f;
            Round(img, t, c, c, 0.25f, PixelImage.Rgb(108, 74, 44), 0.35f, 0.02f, Surface.Wood, 1211u);
            Ring(img, t, c, c, 0.21f, 0.255f, Iron, 0.37f, Surface.Metal, 1212u);
            Ring(img, t, c, c, 0.1f, 0.13f, Iron, 0.37f, Surface.Metal, 1213u);
            Rod(img, t, c, c, c + 0.25f, c - 0.2f, 0.01f, PixelImage.Rgb(40, 34, 28), 0.38f, 0.02f);   // the fuse
            return Finish(img, t, 0);
        }

        // Crossed sharpened stakes through a log: a barrier zombies hurt themselves on.
        static PixelImage DrawChevalDeFrise(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            Rod(img, t, 0.08f, 0.5f, 0.92f, 0.5f, 0.07f, DarkWood, 0.3f, 0.3f);
            for (int i = 0; i < 6; i++)
            {
                float x = 0.16f + i * 0.136f;
                Rod(img, t, x - 0.2f, 0.12f, x + 0.2f, 0.88f, 0.025f, Oak, 0.1f, 0.5f);
                Rod(img, t, x + 0.2f, 0.12f, x - 0.2f, 0.88f, 0.025f, Oak, 0.5f, 0.1f);
                Stud(img, t, x + 0.2f, 0.88f, 0.014f, Iron, 0.5f);
                Stud(img, t, x - 0.2f, 0.88f, 0.014f, Iron, 0.5f);
            }
            return Finish(img, t, 0);
        }

        // A round brick tower with loopholes: a musketeer shoots from the top.
        static PixelImage DrawMusketTower(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Round(img, t, 1f, 1f, 0.94f, Brick, 0.9f, 0f, Surface.Stone, 1221u);
            Round(img, t, 1f, 1f, 0.74f, PixelImage.Scale(Oak, 0.85f), 0.86f, 0f, Surface.Wood, 1222u);   // the wooden floor up top
            Ring(img, t, 1f, 1f, 0.74f, 0.95f, PixelImage.Scale(Brick, 1.08f), 0.97f, Surface.Stone, 1223u);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Block(img, t, 1f + Mathf.Cos(a) * 0.85f - 0.04f, 1f + Mathf.Sin(a) * 0.85f - 0.04f, 1f + Mathf.Cos(a) * 0.85f + 0.04f,
                    1f + Mathf.Sin(a) * 0.85f + 0.04f, PixelImage.Rgb(20, 16, 14), 0.95f, 0.005f, Surface.Plain, 1224u);   // loopholes
            }
            return Finish(img, t, 0);
        }

        // ------------------------------------------------------ Industrial --

        static PixelImage DrawSpringSnare(int ppu, bool sprung)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var wire = PixelImage.Rgb(110, 108, 100);
            Round(img, t, 0.5f, 0.5f, 0.08f, Steel, 0.04f, 0.01f, Surface.Metal, 1231u);   // the spring box
            if (!sprung) Ring(img, t, 0.5f, 0.5f, 0.3f, 0.315f, wire, 0.01f, Surface.Metal, 1232u);   // the open noose
            else
            {
                Ring(img, t, 0.5f, 0.5f, 0.1f, 0.115f, wire, 0.06f, Surface.Metal, 1233u);   // pulled tight
                Round(img, t, 0.55f, 0.48f, 0.1f, PixelImage.Rgb(70, 12, 14, 170), 0.001f, 0f, Surface.Plain, 1234u);
            }
            Rod(img, t, 0.5f, 0.5f, 0.9f, 0.85f, 0.008f, wire, 0.04f, 0.02f);
            Round(img, t, 0.9f, 0.86f, 0.035f, DarkWood, 0.06f, 0.01f, Surface.Wood, 1235u);
            return Finish(img, t, 0);
        }

        static PixelImage DrawDynamite(int ppu)
        {
            float t = ppu;
            var img = Canvas(0.7f, 0.7f, t);
            Round(img, t, 0.35f, 0.35f, 0.24f, PixelImage.Scale(Earth, 0.9f), 0.01f, 0.01f, Surface.Stone, 1241u);
            for (int i = 0; i < 4; i++)
                Rod(img, t, 0.2f, 0.26f + i * 0.06f, 0.48f, 0.26f + i * 0.06f, 0.03f, PixelImage.Rgb(170, 52, 40), 0.06f, 0.06f);
            Rod(img, t, 0.5f, 0.35f, 0.66f, 0.62f, 0.005f, PixelImage.Rgb(150, 140, 120), 0.08f, 0.02f);   // the tripwire
            return Finish(img, t, 0);
        }

        // A concrete pad; the many-barrelled gun turns on it by itself.
        static PixelImage DrawGatlingBase(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Block(img, t, 0.2f, 0.2f, 1.8f, 1.8f, Concrete, 0.3f, 0.04f, Surface.Stone, 1251u);
            Round(img, t, 1f, 1f, 0.45f, Steel, 0.36f, 0.02f, Surface.Metal, 1252u);
            return Finish(img, t, 0);
        }

        static PixelImage DrawGatling(int ppu, float degrees)
        {
            float t = ppu;
            var img = Canvas(1.4f, 1.4f, t);
            var h = new Head(0.7f, 0.7f, degrees);
            h.Block(img, t, -0.25f, -0.18f, 0.1f, 0.18f, Steel, 0.6f);
            for (int i = 0; i < 6; i++)
            {
                float y = -0.08f + i * 0.032f;
                h.Rod(img, t, 0.05f, y, 0.62f, y, 0.014f, PixelImage.Rgb(40, 40, 42), 0.64f, 0.64f);
            }
            h.Round(img, t, 0.58f, 0f, 0.1f, PixelImage.Rgb(54, 54, 58), 0.66f);
            h.Rod(img, t, -0.32f, 0f, -0.25f, 0f, 0.05f, Brass, 0.62f, 0.62f);   // the crank
            return Finish(img, t, 0);
        }

        // ---------------------------------------------------- World War 1 --

        static PixelImage DrawGasTrap(int ppu, bool sprung)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            Round(img, t, 0.5f, 0.5f, 0.2f, PixelImage.Scale(Earth, 0.85f), 0.01f, 0.01f, Surface.Stone, 1261u);
            Rod(img, t, 0.38f, 0.5f, 0.62f, 0.5f, 0.09f, PixelImage.Rgb(84, 98, 72), 0.12f, 0.12f);   // the gas cylinder
            Round(img, t, 0.64f, 0.5f, 0.05f, sprung ? PixelImage.Rgb(30, 30, 30) : Brass, 0.15f, 0.01f, Surface.Metal, 1262u);
            return Finish(img, t, 0);
        }

        static PixelImage DrawSandbagWall(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var dice = new Dice(1271u);
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 3; i++)
                {
                    float x = 0.2f + i * 0.3f + (row % 2) * 0.15f - 0.075f, y = 0.2f + row * 0.3f;
                    Rod(img, t, x - 0.09f, y, x + 0.09f, y, 0.12f, PixelImage.Scale(Sand, dice.Range(0.85f, 1.05f)), 0.3f + row * 0.05f, 0.3f + row * 0.05f);
                }
            return Finish(img, t, 0);
        }

        static PixelImage DrawMortarPit(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Round(img, t, 1f, 1f, 0.7f, PixelImage.Scale(Earth, 0.75f), 0f, 0f, Surface.Stone, 1281u);
            var dice = new Dice(1282u);
            for (int i = 0; i < 14; i++)
            {
                float a = i * Mathf.PI * 2f / 14f;
                var dir = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                float x = 1f + Mathf.Cos(a) * 0.8f, y = 1f + Mathf.Sin(a) * 0.8f;
                Rod(img, t, x - dir.x * 0.08f, y - dir.y * 0.08f, x + dir.x * 0.08f, y + dir.y * 0.08f, 0.1f,
                    PixelImage.Scale(Sand, dice.Range(0.85f, 1.05f)), 0.3f, 0.3f);
            }
            return Finish(img, t, 0);
        }

        static PixelImage DrawMortar(int ppu, float degrees)
        {
            float t = ppu;
            var img = Canvas(1.2f, 1.2f, t);
            var h = new Head(0.6f, 0.6f, degrees);
            h.Round(img, t, -0.15f, 0f, 0.16f, Steel, 0.1f);                                  // the base plate
            h.Rod(img, t, 0.05f, 0.12f, 0.2f, 0.18f, 0.012f, Steel, 0.15f, 0.02f);            // the bipod
            h.Rod(img, t, 0.05f, -0.12f, 0.2f, -0.18f, 0.012f, Steel, 0.15f, 0.02f);
            h.Rod(img, t, -0.15f, 0f, 0.22f, 0f, 0.05f, PixelImage.Rgb(70, 76, 62), 0.12f, 0.5f);   // the tube, pointing up and out
            h.Round(img, t, 0.23f, 0f, 0.04f, PixelImage.Rgb(14, 14, 14), 0.52f);
            return Finish(img, t, 0);
        }

        // ---------------------------------------------------- World War 2 --

        static PixelImage DrawBouncingMine(int ppu)
        {
            float t = ppu;
            var img = Canvas(0.5f, 0.5f, t);
            float c = 0.25f;
            Round(img, t, c, c, 0.2f, PixelImage.Scale(Earth, 0.85f), 0.01f, 0.01f, Surface.Stone, 1291u);
            Round(img, t, c, c, 0.1f, PixelImage.Rgb(84, 86, 70), 0.03f, 0.01f, Surface.Metal, 1292u);
            Stud(img, t, c, c, 0.03f, PixelImage.Rgb(130, 130, 120), 0.05f);
            for (int i = 0; i < 3; i++)
            {
                float a = 1f + i * 2.1f;
                Rod(img, t, c, c, c + Mathf.Cos(a) * 0.08f, c + Mathf.Sin(a) * 0.08f, 0.004f, PixelImage.Rgb(140, 130, 110), 0.05f, 0.06f);
            }
            return Finish(img, t, 0);
        }

        static PixelImage DrawNapalmTrap(int ppu, bool sprung)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            Round(img, t, 0.5f, 0.5f, 0.32f, sprung ? PixelImage.Rgb(30, 22, 16) : PixelImage.Scale(Earth, 0.85f), 0.01f, 0.01f, Surface.Stone, 1301u);
            foreach (float x in new[] { 0.36f, 0.64f })
            {
                Rod(img, t, x, 0.34f, x, 0.66f, 0.08f, PixelImage.Rgb(130, 44, 32), 0.1f, 0.1f);   // the drums
                Round(img, t, x, 0.68f, 0.05f, Steel, 0.12f, 0.01f, Surface.Metal, 1302u);
            }
            return Finish(img, t, 0);
        }

        // Three steel beams welded into a star: zombies have to tear through it.
        static PixelImage DrawHedgehog(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            Rod(img, t, 0.12f, 0.2f, 0.88f, 0.8f, 0.05f, Steel, 0.05f, 0.5f);
            Rod(img, t, 0.12f, 0.8f, 0.88f, 0.2f, 0.05f, PixelImage.Scale(Steel, 1.1f), 0.5f, 0.05f);
            Rod(img, t, 0.5f, 0.08f, 0.5f, 0.92f, 0.05f, PixelImage.Scale(Steel, 0.9f), 0.3f, 0.3f);
            Stud(img, t, 0.5f, 0.5f, 0.06f, PixelImage.Scale(Steel, 1.2f), 0.5f);
            return Finish(img, t, 0);
        }

        // A low concrete pillbox; the flamethrower nozzle turns in its slot.
        static PixelImage DrawFlameBunker(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Round(img, t, 1f, 1f, 0.92f, Concrete, 0.55f, 0.15f, Surface.Stone, 1311u);
            Ring(img, t, 1f, 1f, 0.42f, 0.55f, PixelImage.Rgb(30, 28, 26), 0.6f, Surface.Plain, 1312u);   // the firing slot all round
            Round(img, t, 1f, 1f, 0.42f, PixelImage.Scale(Concrete, 1.08f), 0.75f, 0.05f, Surface.Stone, 1313u);
            return Finish(img, t, 0);
        }

        static PixelImage DrawFlamethrower(int ppu, float degrees)
        {
            float t = ppu;
            var img = Canvas(1.4f, 1.4f, t);
            var h = new Head(0.7f, 0.7f, degrees);
            h.Round(img, t, 0f, 0f, 0.2f, PixelImage.Scale(Steel, 0.9f), 0.7f);           // the turret
            h.Rod(img, t, -0.1f, 0.12f, -0.1f, -0.12f, 0.06f, PixelImage.Rgb(96, 90, 60), 0.76f, 0.76f);   // the fuel tanks
            h.Rod(img, t, 0.1f, 0f, 0.62f, 0f, 0.045f, Steel, 0.74f, 0.74f);              // the nozzle
            h.Rod(img, t, 0.5f, 0f, 0.64f, 0f, 0.06f, PixelImage.Scale(Steel, 0.8f), 0.75f, 0.75f);
            h.Round(img, t, 0.66f, 0f, 0.035f, PixelImage.Rgb(255, 140, 40), 0.77f);       // the pilot flame
            return Finish(img, t, 0);
        }

        // A heavy gun on a concrete pad; its long barrel turns to aim.
        static PixelImage DrawHowitzerBase(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Block(img, t, 0.1f, 0.1f, 1.9f, 1.9f, Concrete, 0.2f, 0.04f, Surface.Stone, 1321u);
            Ring(img, t, 1f, 1f, 0.55f, 0.62f, Steel, 0.24f, Surface.Metal, 1322u);   // the turntable rail
            return Finish(img, t, 0);
        }

        static PixelImage DrawHowitzer(int ppu, float degrees)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            var h = new Head(1f, 1f, degrees);
            h.Block(img, t, -0.55f, -0.32f, 0.15f, 0.32f, PixelImage.Rgb(70, 76, 60), 0.45f);   // the carriage and shield
            h.Rod(img, t, -0.3f, 0f, 0.9f, 0f, 0.07f, PixelImage.Rgb(60, 66, 54), 0.6f, 0.62f);   // the barrel
            h.Rod(img, t, 0.82f, 0f, 0.95f, 0f, 0.09f, PixelImage.Rgb(52, 56, 48), 0.62f, 0.62f); // the muzzle brake
            h.Rod(img, t, -0.55f, 0.3f, -0.95f, 0.5f, 0.04f, PixelImage.Rgb(62, 66, 54), 0.3f, 0.1f);   // the trails
            h.Rod(img, t, -0.55f, -0.3f, -0.95f, -0.5f, 0.04f, PixelImage.Rgb(62, 66, 54), 0.3f, 0.1f);
            return Finish(img, t, 0);
        }
    }
}
