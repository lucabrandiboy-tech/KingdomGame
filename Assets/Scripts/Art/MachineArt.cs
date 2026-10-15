using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>The smelter (Medieval) and the Industrial machines: generator, sawmill, quarry and conveyor belt.</summary>
    public static partial class FurnitureArt
    {
        public const string Smelter = "Machine/Smelter", Generator = "Machine/Generator", Sawmill = "Machine/Sawmill",
            Quarry = "Machine/Quarry", Conveyor = "Machine/Conveyor", ElectricLamp = "Machine/ElectricLamp", Floodlight = "Machine/Floodlight", TreeFarm = "Machine/TreeFarm", Lever = "Machine/Lever";

        static readonly Color32 Soot = PixelImage.Rgb(38, 34, 32);
        static readonly Color32 Rust = PixelImage.Rgb(120, 70, 44);

        // A round brick furnace with glowing ore inside and a chimney.
        static PixelImage DrawSmelter(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Round(img, t, 1f, 1f, 0.8f, Brick, 0.7f, 0.2f, Surface.Stone, 1401u);
            Round(img, t, 1f, 1f, 0.42f, Soot, 0.5f, 0f, Surface.Stone, 1402u);
            Round(img, t, 1f, 1f, 0.28f, PixelImage.Rgb(255, 130, 40), 0.52f, 0f, Surface.Plain, 1403u);   // molten metal
            Round(img, t, 1.55f, 1.5f, 0.2f, Soot, 1.2f, 0f, Surface.Stone, 1404u);                         // chimney
            Block(img, t, 0.2f, 0.85f, 0.45f, 1.15f, Steel, 0.3f, 0.02f, Surface.Metal, 1405u);             // the pouring spout
            return Finish(img, t, 0);
        }

        // A boiler with a firebox and a flywheel: burns wood to power machines nearby.
        static PixelImage DrawGenerator(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Block(img, t, 0.1f, 0.1f, 1.9f, 1.9f, Concrete, 0.2f, 0.04f, Surface.Stone, 1411u);
            Rod(img, t, 0.4f, 0.75f, 1.3f, 0.75f, 0.32f, Rust, 0.75f, 0.75f);                              // the boiler
            Round(img, t, 1.45f, 1.35f, 0.32f, Steel, 0.6f, 0.05f, Surface.Metal, 1412u);                   // flywheel
            Round(img, t, 1.45f, 1.35f, 0.08f, Brass, 0.65f, 0f, Surface.Metal, 1413u);
            Round(img, t, 0.45f, 1.45f, 0.17f, Soot, 1.3f, 0f, Surface.Stone, 1414u);                       // smokestack
            Block(img, t, 1.25f, 0.4f, 1.7f, 0.65f, PixelImage.Rgb(240, 120, 40), 0.25f, 0.01f, Surface.Plain, 1415u);   // firebox glow
            return Finish(img, t, 0);
        }

        // A steam saw: a bench with a big round blade and a log on it.
        static PixelImage DrawSawmill(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Block(img, t, 0.15f, 0.3f, 1.85f, 1.7f, Oak, 0.35f, 0.03f, Surface.Wood, 1421u, true, 0.2f);
            Rod(img, t, 0.2f, 1.2f, 1.8f, 1.2f, 0.17f, PaleWood, 0.6f, 0.6f);                               // the log
            Round(img, t, 1f, 0.75f, 0.36f, PixelImage.Rgb(170, 172, 168), 0.45f, 0f, Surface.Metal, 1422u); // the blade
            Round(img, t, 1f, 0.75f, 0.07f, Steel, 0.5f, 0f, Surface.Metal, 1423u);
            Block(img, t, 1.55f, 0.35f, 1.85f, 0.65f, Steel, 0.55f, 0.02f, Surface.Metal, 1424u);           // the engine
            return Finish(img, t, 0);
        }

        // A steam drill in a stone pit.
        static PixelImage DrawQuarry(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Block(img, t, 0.1f, 0.1f, 1.9f, 1.9f, Granite, 0.1f, 0.1f, Surface.Stone, 1431u);
            Round(img, t, 1f, 1f, 0.6f, PixelImage.Scale(Granite, 0.6f), 0.02f, 0f, Surface.Stone, 1432u);  // the pit
            Rod(img, t, 0.35f, 0.35f, 1.65f, 1.65f, 0.06f, Steel, 0.9f, 0.9f);                               // the frame
            Rod(img, t, 0.35f, 1.65f, 1.65f, 0.35f, 0.06f, Steel, 0.9f, 0.9f);
            Round(img, t, 1f, 1f, 0.16f, Rust, 1f, 0.05f, Surface.Metal, 1433u);                            // the drill head
            return Finish(img, t, 0);
        }

        // A lamp post seen from above: an iron base and a round glass shade.
        static PixelImage DrawElectricLamp(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            Round(img, t, 0.5f, 0.5f, 0.2f, Steel, 0.15f, 0.03f, Surface.Metal, 1451u);
            Round(img, t, 0.5f, 0.5f, 0.15f, PixelImage.Rgb(226, 230, 214), 1.4f, 0.08f, Surface.Plain, 1452u);   // the glass
            Ring(img, t, 0.5f, 0.5f, 0.14f, 0.17f, Iron, 1.42f, Surface.Metal, 1453u);
            return Finish(img, t, 0);
        }

        // A floodlight on a steel stand: a big square lamp with a lens.
        static PixelImage DrawFloodlight(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            Rod(img, t, 0.2f, 0.2f, 0.8f, 0.8f, 0.03f, Steel, 0.6f, 0.6f);
            Rod(img, t, 0.2f, 0.8f, 0.8f, 0.2f, 0.03f, Steel, 0.6f, 0.6f);
            Block(img, t, 0.26f, 0.26f, 0.74f, 0.74f, PixelImage.Rgb(52, 56, 52), 1.5f, 0.03f, Surface.Metal, 1461u);
            Round(img, t, 0.5f, 0.5f, 0.18f, PixelImage.Rgb(232, 236, 222), 1.55f, 0.04f, Surface.Plain, 1462u);   // the lens
            return Finish(img, t, 0);
        }

        // A tree nursery: a fenced bed of earth with saplings in rows and a water trough.
        static PixelImage DrawTreeFarm(int ppu)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Block(img, t, 0.12f, 0.12f, 1.88f, 1.88f, PixelImage.Rgb(66, 50, 34), 0.03f, 0.02f, Surface.Stone, 1471u);   // tilled earth
            foreach (float v in new[] { 0.1f, 1.84f })
            {
                Block(img, t, 0.06f, v, 1.94f, v + 0.06f, PaleWood, 0.25f, 0.01f, Surface.Wood, 1472u);   // fence rails
                Block(img, t, v, 0.06f, v + 0.06f, 1.94f, PaleWood, 0.25f, 0.01f, Surface.Wood, 1473u, false);
            }
            var leaf = PixelImage.Rgb(70, 112, 52);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    float x = 0.45f + i * 0.45f, y = 0.55f + j * 0.45f;
                    Round(img, t, x, y, 0.13f, PixelImage.Scale(leaf, 0.85f + 0.1f * ((i + j) % 2)), 0.3f, 0.08f, Surface.Plain, 1474u + (uint)(i * 3 + j));
                }
            Block(img, t, 1.3f, 0.22f, 1.8f, 0.4f, Oak, 0.2f, 0.01f, Surface.Wood, 1483u);
            Block(img, t, 1.34f, 0.26f, 1.76f, 0.36f, PixelImage.Rgb(60, 90, 120), 0.18f, 0f, Surface.Plain, 1484u);   // water
            return Finish(img, t, 0);
        }

        // A gate lever: an iron base plate with a long handle and a red knob, chains running off to the gate.
        static PixelImage DrawLever(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            Block(img, t, 0.3f, 0.3f, 0.7f, 0.7f, Steel, 0.12f, 0.02f, Surface.Metal, 1491u);
            Rod(img, t, 0.5f, 0.5f, 0.78f, 0.78f, 0.035f, PixelImage.Rgb(80, 80, 84), 0.2f, 0.6f);
            Round(img, t, 0.79f, 0.79f, 0.07f, PixelImage.Rgb(170, 40, 34), 0.62f, 0.03f, Surface.Plain, 1492u);
            for (int i = 0; i < 5; i++) Stud(img, t, 0.32f - i * 0.06f, 0.5f, 0.018f, Iron, 0.1f);   // the chain
            return Finish(img, t, 0);
        }

        // A short stretch of rubber belt between rollers.
        static PixelImage DrawConveyor(int ppu)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            Block(img, t, 0.05f, 0.05f, 0.95f, 0.95f, Steel, 0.06f, 0.02f, Surface.Metal, 1441u);
            Block(img, t, 0.14f, 0.14f, 0.86f, 0.86f, PixelImage.Rgb(34, 34, 36), 0.08f, 0.01f, Surface.Plain, 1442u);
            for (int i = 0; i < 4; i++)
            {
                float y = 0.22f + i * 0.19f;
                Rod(img, t, 0.16f, y, 0.84f, y, 0.012f, PixelImage.Rgb(58, 58, 60), 0.09f, 0.09f);
            }
            return Finish(img, t, 0);
        }
    }
}
