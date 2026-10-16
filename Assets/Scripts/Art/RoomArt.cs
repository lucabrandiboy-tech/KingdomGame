using UnityEngine;

namespace KingdomGame.Art
{
    // Furniture that makes a room a training room, an infirmary or a crafting room (Building/RoomTypes.cs).
    public static partial class FurnitureArt
    {
        public const string TrainingDummy = "Furniture/TrainingDummy", MedicineTable = "Furniture/MedicineTable", ToolBench = "Furniture/ToolBench";

        // A carpenter's tool bench: a thick top of boards, a rack along the back with a saw, a hammer and chisels on it,
        // a board being planed (the plane on it, shavings curled round it), a mallet, and an iron vise at one end.
        static PixelImage DrawToolBench(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1.9f, 0.9f, t);
            var top = PixelImage.Rgb(140, 104, 64);
            var steel = PixelImage.Rgb(168, 170, 176);
            Block(img, t, 0.04f, 0.06f, 1.86f, 0.84f, top, 0.36f, 0.018f, Surface.Wood, 1601u, true, 0.2f);
            // The rack along the back, and the tools on it.
            Block(img, t, 0.06f, 0.74f, 1.84f, 0.84f, DarkWood, 0.42f, 0.01f, Surface.Wood, 1602u);
            Block(img, t, 0.24f, 0.58f, 0.66f, 0.72f, steel, 0.44f, 0.004f, Surface.Metal, 1603u);              // saw blade
            for (int i = 0; i < 14; i++)                                                                          // its teeth
                Rod(img, t, 0.25f + i * 0.03f, 0.58f, 0.26f + i * 0.03f, 0.565f, 0.004f, PixelImage.Scale(steel, 0.8f), 0.44f, 0.44f);
            Block(img, t, 0.12f, 0.58f, 0.25f, 0.76f, Oak, 0.46f, 0.02f, Surface.Wood, 1604u);                   // its handle
            Rod(img, t, 0.8f, 0.66f, 1.04f, 0.66f, 0.014f, Oak, 0.45f, 0.45f);                                    // hammer
            Block(img, t, 1.03f, 0.6f, 1.08f, 0.73f, Iron, 0.47f, 0.008f, Surface.Metal, 1605u);
            for (int i = 0; i < 3; i++)                                                                           // chisels
            {
                float cx = 1.2f + i * 0.08f;
                Rod(img, t, cx, 0.7f, cx, 0.63f, 0.014f, Oak, 0.45f, 0.45f);
                Rod(img, t, cx, 0.63f, cx, 0.56f, 0.007f, steel, 0.44f, 0.44f);
            }
            // A board being planed, the plane on it and shavings.
            Block(img, t, 0.48f, 0.2f, 1.32f, 0.38f, PixelImage.Rgb(190, 154, 104), 0.4f, 0.01f, Surface.Wood, 1606u);
            Block(img, t, 0.9f, 0.22f, 1.08f, 0.36f, DarkWood, 0.46f, 0.02f, Surface.Wood, 1607u);
            Stud(img, t, 0.97f, 0.29f, 0.018f, steel, 0.47f);
            Rod(img, t, 1.04f, 0.29f, 1.1f, 0.29f, 0.016f, Oak, 0.48f, 0.48f);
            foreach (var (sx, sy, r) in new[] { (1.18f, 0.16f, 0.025f), (1.26f, 0.42f, 0.02f), (1.4f, 0.24f, 0.03f), (1.36f, 0.12f, 0.018f), (0.4f, 0.14f, 0.02f) })
                Ring(img, t, sx, sy, r * 0.55f, r, PixelImage.Rgb(222, 192, 140), 0.38f, Surface.Wood, 1610u);
            // A mallet.
            Rod(img, t, 0.16f, 0.2f, 0.34f, 0.3f, 0.013f, Oak, 0.38f, 0.38f);
            Round(img, t, 0.14f, 0.19f, 0.05f, DarkWood, 0.42f, 0.02f, Surface.Wood, 1611u);
            // The vise at the end: iron jaws and its screw handle.
            Block(img, t, 1.6f, 0.08f, 1.84f, 0.3f, Iron, 0.43f, 0.015f, Surface.Metal, 1612u);
            Block(img, t, 1.62f, 0.18f, 1.82f, 0.2f, PixelImage.Scale(Iron, 0.6f), 0.44f, 0.002f, Surface.Metal, 1613u);
            Rod(img, t, 1.72f, 0.08f, 1.72f, 0.02f, 0.012f, Iron, 0.4f, 0.4f);
            Rod(img, t, 1.63f, 0.025f, 1.81f, 0.025f, 0.009f, PixelImage.Rgb(120, 122, 130), 0.41f, 0.41f);
            return Finish(img, t, rotation);
        }

        // A straw man on a post, seen from straight above like the people: crossed feet on the floor, a sacking body
        // stuffed with straw (a painted target on its front, facing down), a crossbar through its shoulders for arms with
        // straw sticking out of the ends, and its sack head on top, tied at the neck.
        static PixelImage DrawTrainingDummy(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var straw = PixelImage.Rgb(186, 156, 88);
            var sacking = PixelImage.Rgb(150, 124, 84);
            var rope = PixelImage.Rgb(96, 74, 46);
            // The feet: two planks crossed under it.
            Rod(img, t, 0.2f, 0.2f, 0.8f, 0.8f, 0.032f, DarkWood, 0.04f, 0.04f);
            Rod(img, t, 0.2f, 0.8f, 0.8f, 0.2f, 0.032f, DarkWood, 0.05f, 0.05f);
            // The body, wider across the shoulders, and the crossbar arms with straw out of the ends.
            Fabric(img, t, 0.34f, 0.41f, 0.66f, 0.59f, sacking, 1.15f, 0.08f, 0.08f, 1401u, 0.006f);
            Rod(img, t, 0.17f, 0.5f, 0.83f, 0.5f, 0.026f, Oak, 1.24f, 1.24f);
            foreach (float end in new[] { 0.17f, 0.83f })
                for (int i = 0; i < 5; i++)
                {
                    float a = (i - 2) * 0.35f + (end < 0.5f ? Mathf.PI : 0f);
                    Rod(img, t, end, 0.5f, end + Mathf.Cos(a) * 0.055f, 0.5f + Mathf.Sin(a) * 0.055f, 0.007f, straw, 1.23f, 1.2f);
                }
            // A painted target on its front.
            for (int ring = 3; ring >= 1; ring--)
                Round(img, t, 0.5f, 0.405f, ring * 0.017f, ring % 2 == 1 ? PixelImage.Rgb(150, 40, 34) : PixelImage.Rgb(220, 206, 170), 1.2f + (3 - ring) * 0.002f, 0f, Surface.Cloth, 1410u + (uint)ring);
            // The head on top, tied at the neck.
            Ring(img, t, 0.5f, 0.5f, 0.072f, 0.084f, rope, 1.3f, Surface.Cloth, 1419u);
            Round(img, t, 0.5f, 0.5f, 0.07f, sacking, 1.45f, 0.06f, Surface.Cloth, 1420u);
            return Finish(img, t, rotation);
        }

        // A healer's table: bottles of medicine, a mortar and pestle, bundles of drying herbs, rolled bandages and a
        // cloth with a red cross on it.
        static PixelImage DrawMedicineTable(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1.9f, 0.9f, t);
            Block(img, t, 0.05f, 0.08f, 1.85f, 0.82f, Oak, 0.42f, 0.015f, Surface.Wood, 1501u, true, 0.18f);
            // The cloth with the cross.
            Fabric(img, t, 0.12f, 0.16f, 0.62f, 0.72f, PixelImage.Rgb(222, 214, 196), 0.43f, 0.01f, 0.03f, 1502u, 0.003f);
            Block(img, t, 0.33f, 0.24f, 0.41f, 0.64f, PixelImage.Rgb(170, 34, 32), 0.445f, 0.004f, Surface.Cloth, 1503u);
            Block(img, t, 0.17f, 0.4f, 0.57f, 0.48f, PixelImage.Rgb(170, 34, 32), 0.446f, 0.004f, Surface.Cloth, 1504u);
            // Bottles: green, brown and blue glass with corks.
            var glass = new[] { PixelImage.Rgb(70, 130, 80), PixelImage.Rgb(120, 76, 40), PixelImage.Rgb(70, 96, 150), PixelImage.Rgb(70, 130, 80) };
            for (int i = 0; i < 4; i++)
            {
                float bx = 0.78f + i * 0.13f, by = i % 2 == 0 ? 0.62f : 0.5f;
                Round(img, t, bx, by, 0.05f, glass[i], 0.62f, 0.02f, Surface.Wax, 1510u + (uint)i);
                Stud(img, t, bx, by, 0.022f, PixelImage.Rgb(160, 120, 80), 0.66f);
            }
            // A mortar and pestle.
            Ring(img, t, 1.42f, 0.56f, 0.06f, 0.1f, Granite, 0.55f, Surface.Stone, 1520u);
            Round(img, t, 1.42f, 0.56f, 0.06f, PixelImage.Rgb(80, 110, 60), 0.5f, 0f, Surface.Cloth, 1521u);
            Rod(img, t, 1.4f, 0.54f, 1.56f, 0.68f, 0.018f, Granite, 0.6f, 0.62f);
            // Herbs drying in bundles, and rolled bandages.
            for (int i = 0; i < 2; i++)
                Fabric(img, t, 1.58f + i * 0.12f, 0.2f, 1.68f + i * 0.12f, 0.46f, PixelImage.Rgb(84, 116, 60), 0.46f, 0.04f, 0.04f, 1530u + (uint)i, 0.01f, true);
            for (int i = 0; i < 3; i++)
                Round(img, t, 0.86f + i * 0.13f, 0.24f, 0.05f, PixelImage.Rgb(226, 220, 204), 0.5f, 0.02f, Surface.Cloth, 1540u + (uint)i);
            return Finish(img, t, rotation);
        }
    }
}
