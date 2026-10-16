using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Art
{
    // The ruins out in the wild (World/Ruins.cs): crumbled walls, rubble, old floors, a wrecked wagon and a broken
    // watchtower, drawn with the furniture toolkit (unlit heights and colours, then turned and lit from the top-left).
    public static partial class FurnitureArt
    {
        public const string RuinWall = "Ruin/Wall", RuinRubble = "Ruin/Rubble", RuinFloor = "Ruin/Floor", Wagon = "Ruin/Wagon", RuinTower = "Ruin/Tower";
        /// <summary>Kinds of crumbled wall, rubble and old floor (their art names end in the number: "Ruin/Wall2").</summary>
        public const int RuinWalls = 4, RuinRubbles = 3, RuinFloors = 4;

        /// <summary>The ruins' pictures (each has its own art name; walls and the wagon lie either way: rotation 0 or 1).</summary>
        public static readonly Piece[] RuinPieces = MakeRuinPieces();

        static Piece[] MakeRuinPieces()
        {
            var list = new List<Piece>();
            for (int v = 0; v < RuinWalls; v++)
            {
                int variant = v;
                list.Add(new Piece { Art = RuinWall + v, Rotations = 2, Draw = (p, r) => DrawRuinWall(p, r, variant) });
            }
            for (int v = 0; v < RuinRubbles; v++)
            {
                int variant = v;
                list.Add(new Piece { Art = RuinRubble + v, Rotations = 1, Draw = (p, r) => DrawRubble(p, variant) });
            }
            for (int v = 0; v < RuinFloors; v++)
            {
                int variant = v;
                list.Add(new Piece { Art = RuinFloor + v, Rotations = 1, Draw = (p, r) => DrawRuinFloor(p, variant), Flat = true });
            }
            list.Add(new Piece { Art = Wagon, Rotations = 2, Draw = DrawWagon });
            list.Add(new Piece { Art = RuinTower, Rotations = 1, Draw = (p, r) => DrawRuinTower(p) });
            return list.ToArray();
        }

        static readonly Color32 OldStone = PixelImage.Rgb(108, 104, 96);
        static readonly Color32 Moss = PixelImage.Rgb(70, 88, 50);

        // A tile of crumbled stone wall running left to right: two courses of blocks, broken down to different heights,
        // some missing, a few fallen beside it, moss in the cracks. Variant 3 has mostly fallen.
        static PixelImage DrawRuinWall(int ppu, int rotation, int variant)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var dice = new Dice(900u + (uint)variant * 31u);
            bool fallen = variant == 3;
            for (int course = 0; course < 2; course++)
            {
                float y0 = course == 0 ? 0.3f : 0.5f, y1 = course == 0 ? 0.5f : 0.7f;
                float x = course == 0 ? 0f : -dice.Range(0.05f, 0.15f);   // staggered joints
                for (int b = 0; x < 1f && b < 8; b++)
                {
                    float w = dice.Range(0.18f, 0.3f);
                    float x0 = Mathf.Max(0f, x), x1 = Mathf.Min(1f, x + w);
                    x += w + 0.012f;
                    if (dice.Next() < (fallen ? 0.55f : 0.18f)) continue;   // a gap where blocks fell
                    float height = fallen ? dice.Range(0.08f, 0.22f) : dice.Range(0.3f, 0.72f);
                    var tone = PixelImage.Scale(OldStone, dice.Range(0.8f, 1.12f));
                    Block(img, t, x0 + 0.008f, y0 + 0.006f, x1 - 0.008f, y1 - 0.006f, tone, height, 0.035f, Surface.Stone, 901u + (uint)(variant * 16 + course * 8 + b));
                }
            }
            // Fallen stones on both sides.
            int loose = fallen ? 6 : 3;
            for (int i = 0; i < loose; i++)
            {
                float sx = dice.Range(0.08f, 0.92f), sy = dice.Next() < 0.5f ? dice.Range(0.06f, 0.26f) : dice.Range(0.74f, 0.94f);
                Round(img, t, sx, sy, dice.Range(0.04f, 0.08f), PixelImage.Scale(OldStone, dice.Range(0.75f, 1.05f)), dice.Range(0.04f, 0.1f), 0.03f, Surface.Stone, 940u + (uint)(variant * 8 + i));
            }
            // Moss where the stones meet.
            for (int i = 0; i < 5; i++)
                Round(img, t, dice.Range(0.05f, 0.95f), dice.Range(0.32f, 0.68f), dice.Range(0.025f, 0.05f), PixelImage.Scale(Moss, dice.Range(0.8f, 1.1f)), 0.06f, 0f, Surface.Cloth, 960u + (uint)i);
            return Finish(img, t, rotation);
        }

        // A heap of broken stone and a snapped beam.
        static PixelImage DrawRubble(int ppu, int variant)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var dice = new Dice(1000u + (uint)variant * 17u);
            if (variant != 1)
                Rod(img, t, dice.Range(0.1f, 0.3f), dice.Range(0.2f, 0.8f), dice.Range(0.6f, 0.9f), dice.Range(0.2f, 0.8f), 0.045f, PixelImage.Rgb(78, 56, 38), 0.12f, 0.2f);
            int stones = 8 + variant * 2;
            for (int i = 0; i < stones; i++)
            {
                float a = dice.Range(0f, Mathf.PI * 2f), d = Mathf.Sqrt(dice.Next()) * 0.34f;
                float r = dice.Range(0.05f, 0.12f) * (1f - d);
                Round(img, t, 0.5f + Mathf.Cos(a) * d, 0.5f + Mathf.Sin(a) * d, Mathf.Max(0.035f, r), PixelImage.Scale(OldStone, dice.Range(0.72f, 1.1f)),
                    (0.3f - d * 0.6f) + dice.Range(0f, 0.08f), r * 0.6f, Surface.Stone, 1010u + (uint)(variant * 20 + i));
            }
            return Finish(img, t, 0);
        }

        // An old flagstone floor, cracked, with slabs missing and earth and weeds coming through.
        static PixelImage DrawRuinFloor(int ppu, int variant)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var dice = new Dice(1100u + (uint)variant * 13u);
            var hs = img.EnsureHeights();
            var tones = new Color32[4];
            bool[] missing = new bool[4];
            for (int i = 0; i < 4; i++)
            {
                tones[i] = PixelImage.Scale(PixelImage.Rgb(92, 88, 80), dice.Range(0.8f, 1.1f));
                missing[i] = dice.Next() < 0.07f;
            }
            float crackA = dice.Range(0f, 6.28f);
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float px = (x + 0.5f) / t, py = (y + 0.5f) / t;
                    int slab = (py > 0.5f ? 2 : 0) + (px > 0.5f ? 1 : 0);
                    float edge = Mathf.Min(Mathf.Min(Mathf.Abs(px - 0.5f), Mathf.Abs(py - 0.5f)), Mathf.Min(Mathf.Min(px, 1f - px), Mathf.Min(py, 1f - py)));
                    edge += (Noise.Value(px * 50f, py * 50f, 1120u + (uint)variant) - 0.5f) * 0.02f;
                    float dirt = Noise.Fractal(px * 5f, py * 5f, 1130u + (uint)variant, 3);
                    int i = y * img.Width + x;
                    if (missing[slab] || dirt > 0.72f)
                    {
                        // Bare earth (left see-through at the edges, so it blends into the ground).
                        float a = Mathf.Clamp01((dirt - 0.5f) * 3f) * (missing[slab] ? 0.6f : 1f);
                        if (a <= 0.02f) continue;
                        var earth = PixelImage.Mix(PixelImage.Rgb(58, 48, 36), Moss, Noise.Value(px * 30f, py * 30f, 1140u));
                        img.Blend(x, y, earth, a);
                        hs[i] = 0.004f * t;
                        continue;
                    }
                    if (edge < 0.014f)
                    {
                        img.Set(x, y, PixelImage.Scale(PixelImage.Rgb(46, 42, 36), 0.8f + Noise.Value(px * 200f, py * 200f, 1150u) * 0.3f));
                        hs[i] = 0f;
                        continue;
                    }
                    // A crack across the slab.
                    float u = Mathf.Cos(crackA) * (px - 0.5f) + Mathf.Sin(crackA) * (py - 0.5f);
                    float crack = Mathf.Abs(u + (Noise.Value(px * 12f, py * 12f, 1160u) - 0.5f) * 0.12f);
                    float shade = Texture(Surface.Stone, px + slab * 5f, py, 1170u + (uint)variant, true) * (crack < 0.008f ? 0.55f : 1f);
                    img.Set(x, y, PixelImage.Scale(tones[slab], shade));
                    hs[i] = (0.01f + Mathf.Min(edge, 0.03f) * 0.3f) * t;
                }
            img.BakeLight(0.6f, 0.6f, Mathf.Max(1, Mathf.RoundToInt(0.01f * t)));
            return img;
        }

        // A wagon wrecked on the road: its bed broken open (boards missing), one wheel still on, the other lying in the
        // grass beside it, the shafts snapped, and what it carried spilled out (sacks, a split crate).
        static PixelImage DrawWagon(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(2.4f, 1.6f, t);
            var dice = new Dice(1200u);
            var oak = PixelImage.Rgb(104, 74, 46);
            var dark = PixelImage.Rgb(62, 44, 28);
            // The wheel lying in the grass: rim, spokes, hub.
            Ring(img, t, 1.95f, 1.22f, 0.24f, 0.3f, dark, 0.06f, Surface.Wood, 1201u);
            for (int s = 0; s < 8; s++)
            {
                float a = s * Mathf.PI / 4f + 0.2f;
                Rod(img, t, 1.95f, 1.22f, 1.95f + Mathf.Cos(a) * 0.25f, 1.22f + Mathf.Sin(a) * 0.25f, 0.018f, dark, 0.05f, 0.05f);
            }
            Round(img, t, 1.95f, 1.22f, 0.06f, Iron, 0.08f, 0.02f, Surface.Metal, 1202u);
            // The bed: boards across, a third of them gone, the side rails.
            for (int b = 0; b < 7; b++)
            {
                if (b == 2 || b == 5 || dice.Next() < 0.12f) continue;
                float x0 = 0.32f + b * 0.2f;
                Block(img, t, x0, 0.42f, x0 + 0.18f, 1.06f, PixelImage.Scale(oak, dice.Range(0.82f, 1.08f)), 0.32f + dice.Range(-0.03f, 0.02f), 0.01f, Surface.Wood, 1210u + (uint)b, false);
            }
            Block(img, t, 0.28f, 0.36f, 1.76f, 0.44f, dark, 0.4f, 0.012f, Surface.Wood, 1220u);
            Block(img, t, 0.28f, 1.04f, 1.2f, 1.12f, dark, 0.4f, 0.012f, Surface.Wood, 1221u);   // (the far end of this rail broke off)
            Rod(img, t, 1.24f, 1.1f, 1.62f, 1.3f, 0.035f, dark, 0.1f, 0.06f);                     // ...and lies on the ground
            // The wheel still on, seen edge-on under the near rail, and the axle.
            Block(img, t, 0.7f, 0.22f, 1.02f, 0.32f, dark, 0.5f, 0.02f, Surface.Wood, 1222u);
            Rod(img, t, 0.86f, 0.3f, 0.86f, 1.1f, 0.03f, Iron, 0.18f, 0.18f);
            // The shafts, one snapped short.
            Rod(img, t, 1.76f, 0.56f, 2.32f, 0.5f, 0.03f, oak, 0.3f, 0.2f);
            Rod(img, t, 1.76f, 0.92f, 2.02f, 0.98f, 0.03f, oak, 0.3f, 0.24f);
            // What it carried, spilled out.
            Fabric(img, t, 0.06f, 1.06f, 0.42f, 1.42f, PixelImage.Rgb(138, 118, 84), 0.06f, 0.1f, 0.1f, 1230u);
            Fabric(img, t, 0.4f, 1.22f, 0.7f, 1.5f, PixelImage.Rgb(124, 106, 76), 0.05f, 0.09f, 0.1f, 1231u);
            CrateBox(img, t, 0.04f, 0.08f, 0.3f, 0.22f, 1232u);
            Rod(img, t, 0.4f, 0.12f, 0.62f, 0.06f, 0.02f, PixelImage.Rgb(126, 92, 58), 0.05f, 0.04f);   // a board off the crate
            return Finish(img, t, rotation);
        }

        // What's left of a round stone watchtower: a ring of wall broken down to stumps, one side fallen in completely,
        // the fallen stones inside and out.
        static PixelImage DrawRuinTower(int ppu)
        {
            float t = ppu;
            var img = Canvas(3.2f, 3.2f, t);
            var dice = new Dice(1300u);
            const float c = 1.6f, radius = 1.18f;
            // The floor inside: old flagstones half buried.
            Round(img, t, c, c, radius - 0.1f, PixelImage.Rgb(78, 74, 66), 0.01f, 0f, Surface.Stone, 1301u);
            // The wall: two courses of dressed stones round the ring (staggered), broken down to different heights
            // in stretches; a stretch of it fallen in.
            const int blocks = 30;
            int gap = 3 + (int)(dice.Next() * 5f);
            float stretch = dice.Range(0.6f, 1.1f);
            for (int course = 0; course < 2; course++)
                for (int i = 0; i < blocks; i++)
                {
                    float a = (i + 0.5f * course + 0.5f) / blocks * Mathf.PI * 2f;
                    int fromGap = Mathf.Min(Mathf.Abs(i - gap), blocks - Mathf.Abs(i - gap));
                    if (fromGap <= 2 || (fromGap == 3 && course == 1)) continue;
                    // Higher and lower stretches, and each stone a little different.
                    float wave = 0.5f + 0.5f * Mathf.Sin(a * 3f + stretch * 4f);
                    float height = fromGap <= 4 ? dice.Range(0.1f, 0.25f) : 0.3f + wave * 0.65f + dice.Range(-0.08f, 0.08f);
                    var tone = PixelImage.Scale(OldStone, dice.Range(0.78f, 1.1f));
                    float rr = course == 0 ? radius + 0.12f : radius - 0.14f;
                    float bx = c + Mathf.Cos(a) * rr, by = c + Mathf.Sin(a) * rr;
                    Round(img, t, bx, by, course == 0 ? 0.15f : 0.13f, tone, height, 0.02f, Surface.Stone, 1310u + (uint)(course * blocks + i));
                }
            // Fallen stones: in through the gap, and scattered round the outside.
            float gapAngle = (gap + 0.5f) / blocks * Mathf.PI * 2f;
            for (int i = 0; i < 16; i++)
            {
                bool inside = i < 7;
                float a = inside ? gapAngle + dice.Range(-0.7f, 0.7f) : dice.Range(0f, Mathf.PI * 2f);
                float d = inside ? dice.Range(0.2f, 0.95f) : dice.Range(1.42f, 1.55f);
                float r = dice.Range(0.06f, 0.13f);
                Round(img, t, c + Mathf.Cos(a) * d, c + Mathf.Sin(a) * d, r, PixelImage.Scale(OldStone, dice.Range(0.72f, 1.05f)), dice.Range(0.05f, 0.16f), r * 0.6f, Surface.Stone, 1340u + (uint)i);
            }
            // A charred beam from the floor above.
            Rod(img, t, c - 0.6f, c - 0.2f, c + 0.5f, c + 0.35f, 0.06f, PixelImage.Rgb(40, 32, 26), 0.1f, 0.18f);
            for (int i = 0; i < 6; i++)
                Round(img, t, c + dice.Range(-1.3f, 1.3f), c + dice.Range(-1.3f, 1.3f), dice.Range(0.03f, 0.06f), PixelImage.Scale(Moss, dice.Range(0.8f, 1.1f)), 0.05f, 0f, Surface.Cloth, 1370u + (uint)i);
            return Finish(img, t, 0);
        }
    }
}
