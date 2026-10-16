using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Castle furniture, floors, doors and small effects (flames, blood, loot), seen
    /// from straight above. Each piece is drawn unlit (colours and heights) facing
    /// its first direction (its front towards the bottom of the picture), then
    /// turned and lit from the top-left (<see cref="Finish"/>), so every direction
    /// it can face is lit the same way. Sizes are in tiles; <c>ppu</c> is pixels per tile.
    /// </summary>
    public static partial class FurnitureArt
    {
        /// <summary>How each piece of furniture is drawn: its art name, how many directions, and the drawing.</summary>
        public struct Piece
        {
            public string Art;
            public int Rotations;
            public System.Func<int, int, PixelImage> Draw;   // (ppu, rotation)
            /// <summary>Lies flat on the floor (a rug): no shadow.</summary>
            public bool Flat;
        }

        public const string Chair = "Furniture/Chair", Table = "Furniture/Table", LongTable = "Furniture/LongTable",
            Bench = "Furniture/Bench", Bed = "Furniture/Bed", Chest = "Furniture/Chest", Barrel = "Furniture/Barrel",
            Bookshelf = "Furniture/Bookshelf", WeaponRack = "Furniture/WeaponRack", ArmorStand = "Furniture/ArmorStand",
            Cauldron = "Furniture/Cauldron", Fireplace = "Furniture/Fireplace", Rug = "Furniture/Rug",
            Candelabra = "Furniture/Candelabra", Torch = "Furniture/Torch", Brazier = "Furniture/Brazier",
            Anvil = "Furniture/Anvil", Crates = "Furniture/Crates", Banner = "Furniture/Banner", Well = "Furniture/Well",
            Ladder = "Furniture/Ladder", StoneStairs = "Furniture/StoneStairs", MineHead = "Building/MineHead";
        public const string WoodFloor = "Floor/Wood", StoneFloor = "Floor/Stone";
        public const string WoodDoor = "Door/Wood", HeavyDoor = "Door/Heavy", WoodGate = "Door/WoodGate", CastleGate = "Door/CastleGate",
            LeverGate = "Door/LeverGate";

        /// <summary>Every piece of furniture (the game's building configs refer to these by their art name).</summary>
        public static readonly Piece[] Pieces =
        {
            new Piece { Art = Chair, Rotations = 4, Draw = DrawChair },
            new Piece { Art = Table, Rotations = 1, Draw = DrawTable },
            new Piece { Art = LongTable, Rotations = 2, Draw = DrawLongTable },
            new Piece { Art = Bench, Rotations = 2, Draw = DrawBench },
            new Piece { Art = Bed, Rotations = 4, Draw = DrawBed },
            new Piece { Art = Chest, Rotations = 4, Draw = DrawChest },
            new Piece { Art = Barrel, Rotations = 1, Draw = DrawBarrel },
            new Piece { Art = Bookshelf, Rotations = 4, Draw = DrawBookshelf },
            new Piece { Art = WeaponRack, Rotations = 4, Draw = DrawWeaponRack },
            new Piece { Art = ArmorStand, Rotations = 4, Draw = DrawArmorStand },
            new Piece { Art = Cauldron, Rotations = 1, Draw = DrawCauldron },
            new Piece { Art = Fireplace, Rotations = 4, Draw = DrawFireplace },
            new Piece { Art = Rug, Rotations = 2, Draw = DrawRug, Flat = true },
            new Piece { Art = Candelabra, Rotations = 1, Draw = DrawCandelabra },
            new Piece { Art = Torch, Rotations = 1, Draw = DrawTorch },
            new Piece { Art = Brazier, Rotations = 1, Draw = DrawBrazier },
            new Piece { Art = Anvil, Rotations = 2, Draw = DrawAnvil },
            new Piece { Art = Crates, Rotations = 1, Draw = DrawCrates },
            new Piece { Art = Banner, Rotations = 4, Draw = DrawBanner },
            new Piece { Art = Well, Rotations = 1, Draw = DrawWell },
            new Piece { Art = Ladder, Rotations = 4, Draw = DrawLadder },
            new Piece { Art = StoneStairs, Rotations = 4, Draw = DrawStoneStairs },
            new Piece { Art = MineHead, Rotations = 1, Draw = DrawMineHead },
            new Piece { Art = TrainingDummy, Rotations = 1, Draw = DrawTrainingDummy },
            new Piece { Art = MedicineTable, Rotations = 4, Draw = DrawMedicineTable },
        };

        /// <summary>Where the candelabra's three candles are, in tiles from its middle.</summary>
        public static readonly Vector2[] CandelabraFlames = { new Vector2(-0.15f, 0.04f), new Vector2(0f, 0.13f), new Vector2(0.15f, 0.04f) };
        /// <summary>Where the fire is in a fireplace, in tiles from its middle (facing down).</summary>
        public static readonly Vector2 FireplaceFlame = new Vector2(0f, -0.05f);

        static readonly Color32 Oak = PixelImage.Rgb(112, 78, 48);
        static readonly Color32 DarkWood = PixelImage.Rgb(72, 48, 30);
        static readonly Color32 Iron = PixelImage.Rgb(58, 58, 64);
        static readonly Color32 Brass = PixelImage.Rgb(170, 130, 60);
        static readonly Color32 Granite = PixelImage.Rgb(118, 114, 108);

        internal enum Surface { Wood, Stone, Metal, Cloth, Wax, Plain }

        // ------------------------------------------------------------ toolkit --

        static PixelImage Canvas(float widthTiles, float heightTiles, float t) =>
            new PixelImage(Mathf.Max(2, Mathf.CeilToInt(widthTiles * t)), Mathf.Max(2, Mathf.CeilToInt(heightTiles * t))) { Bumpiness = 1f };

        /// <summary>Turns the finished drawing to face <paramref name="rotation"/> and lights it from the top-left.</summary>
        static PixelImage Finish(PixelImage img, float t, int rotation, float light = 0.85f, float occlusion = 0.55f)
        {
            var turned = img.Turned(rotation);
            turned.BakeLight(light, occlusion, Mathf.Max(1, Mathf.RoundToInt(0.012f * t)));
            return turned;
        }

        static void Lift(PixelImage img, int x, int y, float h, float coverage)
        {
            var hs = img.EnsureHeights();
            int i = y * img.Width + x;
            float mixed = h * coverage + hs[i] * (1f - coverage);
            if (mixed > hs[i]) hs[i] = mixed;
        }

        // Brightness variation of a surface at a point (tiles), around 1.
        static float Texture(Surface surface, float x, float y, uint seed, bool grainAlongX)
        {
            switch (surface)
            {
                case Surface.Wood:
                {
                    float along = grainAlongX ? x : y, across = grainAlongX ? y : x;
                    float grain = Noise.Value(along * 6f, across * 90f, seed) - 0.5f;
                    float rings = Mathf.Sin((across * 160f + Noise.Value(along * 3f, across * 10f, seed + 1u) * 6f)) * 0.04f;
                    return 1f + grain * 0.3f + rings;
                }
                case Surface.Stone:
                    return 1f + (Noise.Fractal(x * 14f, y * 14f, seed, 3) - 0.5f) * 0.32f + (Noise.Hash01(seed + 2u, Mathf.FloorToInt(x * 260f), Mathf.FloorToInt(y * 260f)) - 0.5f) * 0.12f;
                case Surface.Metal:
                    return 1f + (Noise.Value(x * 40f, y * 400f, seed) - 0.5f) * 0.12f + (Noise.Fractal(x * 8f, y * 8f, seed + 3u, 2) - 0.5f) * 0.16f;
                case Surface.Cloth:
                    return 1f + Mathf.Sin(x * 1500f) * Mathf.Sin(y * 1500f) * 0.035f + (Noise.Value(x * 40f, y * 40f, seed) - 0.5f) * 0.12f;
                case Surface.Wax:
                    return 1f + (Noise.Value(x * 50f, y * 50f, seed) - 0.5f) * 0.06f;
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// A raised block with slightly rounded edges (a table top, a crate, a stone): fills
        /// the rectangle with a material. <paramref name="plank"/> &gt; 0 cuts it into boards
        /// that wide, running along the grain.
        /// </summary>
        static void Block(PixelImage img, float t, float x0, float y0, float x1, float y1, Color32 color, float top, float bevel,
            Surface surface, uint seed, bool grainAlongX = true, float plank = 0f)
        {
            int ix0 = Mathf.FloorToInt(x0 * t) - 1, ix1 = Mathf.CeilToInt(x1 * t) + 1;
            int iy0 = Mathf.FloorToInt(y0 * t) - 1, iy1 = Mathf.CeilToInt(y1 * t) + 1;
            for (int y = iy0; y <= iy1; y++)
                for (int x = ix0; x <= ix1; x++)
                {
                    if (!img.Inside(x, y)) continue;
                    float px = (x + 0.5f) / t, py = (y + 0.5f) / t;
                    float edge = Mathf.Min(Mathf.Min(px - x0, x1 - px), Mathf.Min(py - y0, y1 - py));
                    float coverage = Mathf.Clamp01(edge * t + 0.5f);
                    if (coverage <= 0f) continue;
                    float slope = Mathf.Clamp01(edge / Mathf.Max(bevel, 0.5f / t));
                    float h = top * (0.6f + 0.4f * Mathf.Sqrt(slope));
                    float shade = Texture(surface, px, py, seed, grainAlongX);
                    if (plank > 0f)
                    {
                        float acrossGrain = grainAlongX ? py - y0 : px - x0;
                        float board = acrossGrain / plank;
                        int index = Mathf.FloorToInt(board);
                        float inBoard = (board - index) * plank;
                        shade += (Noise.Hash01(seed + 5u, index, 3) - 0.5f) * 0.16f;
                        if (inBoard < 0.006f || plank - inBoard < 0.006f)
                        {
                            shade *= 0.55f;
                            h -= 0.008f;
                        }
                    }
                    img.Blend(x, y, PixelImage.Scale(color, shade), coverage);
                    Lift(img, x, y, h * t, coverage);
                }
        }

        /// <summary>A round thing seen from above: flat-topped with a rounded edge, or domed (<paramref name="dome"/> tiles higher in the middle).</summary>
        static void Round(PixelImage img, float t, float cx, float cy, float r, Color32 color, float top, float dome, Surface surface, uint seed)
        {
            int ix0 = Mathf.FloorToInt((cx - r) * t) - 1, ix1 = Mathf.CeilToInt((cx + r) * t) + 1;
            int iy0 = Mathf.FloorToInt((cy - r) * t) - 1, iy1 = Mathf.CeilToInt((cy + r) * t) + 1;
            for (int y = iy0; y <= iy1; y++)
                for (int x = ix0; x <= ix1; x++)
                {
                    if (!img.Inside(x, y)) continue;
                    float px = (x + 0.5f) / t, py = (y + 0.5f) / t;
                    float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    float coverage = Mathf.Clamp01((r - d) * t + 0.5f);
                    if (coverage <= 0f) continue;
                    float inside = Mathf.Clamp01(1f - d / r);
                    float h = top * (0.65f + 0.35f * Mathf.Sqrt(Mathf.Clamp01(inside * 6f))) + dome * Mathf.Sqrt(Mathf.Max(0f, 1f - (d / r) * (d / r)));
                    img.Blend(x, y, PixelImage.Scale(color, Texture(surface, px, py, seed, true)), coverage);
                    Lift(img, x, y, h * t, coverage);
                }
        }

        /// <summary>A ring (a hoop, a rim, a stone circle) with a rounded top.</summary>
        static void Ring(PixelImage img, float t, float cx, float cy, float r0, float r1, Color32 color, float top, Surface surface, uint seed)
        {
            int ix0 = Mathf.FloorToInt((cx - r1) * t) - 1, ix1 = Mathf.CeilToInt((cx + r1) * t) + 1;
            int iy0 = Mathf.FloorToInt((cy - r1) * t) - 1, iy1 = Mathf.CeilToInt((cy + r1) * t) + 1;
            float mid = (r0 + r1) * 0.5f, half = (r1 - r0) * 0.5f;
            for (int y = iy0; y <= iy1; y++)
                for (int x = ix0; x <= ix1; x++)
                {
                    if (!img.Inside(x, y)) continue;
                    float px = (x + 0.5f) / t, py = (y + 0.5f) / t;
                    float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                    float across = (d - mid) / half;
                    float coverage = Mathf.Clamp01((1f - Mathf.Abs(across)) * half * t + 0.5f);
                    if (coverage <= 0f) continue;
                    float h = top * (0.75f + 0.25f * Mathf.Sqrt(Mathf.Max(0f, 1f - across * across)));
                    img.Blend(x, y, PixelImage.Scale(color, Texture(surface, px, py, seed, false)), coverage);
                    Lift(img, x, y, h * t, coverage);
                }
        }

        /// <summary>A rod or bar from one point to another (tiles), radius <paramref name="r"/>.</summary>
        static void Rod(PixelImage img, float t, float x0, float y0, float x1, float y1, float r, Color32 color, float h0, float h1) =>
            img.Capsule(x0 * t, y0 * t, x1 * t, y1 * t, r * t, r * t, color, PixelImage.Scale(color, 0.92f), h0 * t, h1 * t);

        /// <summary>A small glossy knob (a nail head, a stud, a gem).</summary>
        static void Stud(PixelImage img, float t, float x, float y, float r, Color32 color, float top)
        {
            var light = ArtLight.Sun;
            float rp = Mathf.Max(0.7f, r * t);
            float cx = x * t, cy = y * t;
            for (int py = Mathf.FloorToInt(cy - rp - 1); py <= cy + rp + 1; py++)
                for (int px = Mathf.FloorToInt(cx - rp - 1); px <= cx + rp + 1; px++)
                {
                    if (!img.Inside(px, py)) continue;
                    float dx = (px + 0.5f - cx) / rp, dy = (py + 0.5f - cy) / rp;
                    float d2 = dx * dx + dy * dy;
                    float edge = (1f - Mathf.Sqrt(d2)) * rp + 0.5f;
                    if (edge <= 0f) continue;
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - d2));
                    float lit = dx * light.x + dy * light.y + nz * light.z;
                    var c = PixelImage.Mix(PixelImage.Scale(color, 0.6f + lit * 0.6f), PixelImage.Rgb(255, 244, 210), Mathf.Pow(Mathf.Max(0f, lit), 18f) * 0.7f);
                    img.Blend(px, py, c, Mathf.Clamp01(edge));
                    Lift(img, px, py, top * t + nz * rp, Mathf.Clamp01(edge));
                }
        }

        /// <summary>
        /// Soft fabric (a mattress, a blanket, a cushion): rounded corners, puffed up in the
        /// middle, with folds running across <paramref name="foldsAlongY"/>.
        /// </summary>
        static void Fabric(PixelImage img, float t, float x0, float y0, float x1, float y1, Color32 color, float top, float puff, float corner,
            uint seed, float folds = 0f, bool foldsAlongY = false)
        {
            int ix0 = Mathf.FloorToInt(x0 * t) - 1, ix1 = Mathf.CeilToInt(x1 * t) + 1;
            int iy0 = Mathf.FloorToInt(y0 * t) - 1, iy1 = Mathf.CeilToInt(y1 * t) + 1;
            for (int y = iy0; y <= iy1; y++)
                for (int x = ix0; x <= ix1; x++)
                {
                    if (!img.Inside(x, y)) continue;
                    float px = (x + 0.5f) / t, py = (y + 0.5f) / t;
                    // Distance inside a rounded rectangle.
                    float qx = Mathf.Max(0f, Mathf.Max(x0 + corner - px, px - (x1 - corner)));
                    float qy = Mathf.Max(0f, Mathf.Max(y0 + corner - py, py - (y1 - corner)));
                    float edge = corner - Mathf.Sqrt(qx * qx + qy * qy);
                    edge = Mathf.Min(edge, Mathf.Min(Mathf.Min(px - x0, x1 - px), Mathf.Min(py - y0, y1 - py)));
                    float coverage = Mathf.Clamp01(edge * t + 0.5f);
                    if (coverage <= 0f) continue;
                    float swell = Mathf.Sqrt(Mathf.Clamp01(edge / Mathf.Max(0.001f, Mathf.Min(x1 - x0, y1 - y0) * 0.5f)));
                    float fold = folds > 0f
                        ? Mathf.Sin((foldsAlongY ? px : py) * 38f + Noise.Value(px * 6f, py * 6f, seed) * 3f) * folds
                        : 0f;
                    float h = top + puff * swell + fold;
                    float shade = Texture(Surface.Cloth, px, py, seed, true) + fold * 4f;
                    img.Blend(x, y, PixelImage.Scale(color, shade), coverage);
                    Lift(img, x, y, h * t, coverage);
                }
        }

        // ----------------------------------------------------------- furniture --

        static PixelImage DrawChair(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.62f, 0.62f, t);
            Block(img, t, 0.11f, 0.06f, 0.51f, 0.46f, Oak, 0.18f, 0.02f, Surface.Wood, 11u, true, 0.1f);
            Fabric(img, t, 0.15f, 0.1f, 0.47f, 0.4f, PixelImage.Rgb(116, 24, 32), 0.19f, 0.03f, 0.05f, 12u);
            // Back rest along the far side: a rail between two posts.
            Block(img, t, 0.08f, 0.46f, 0.54f, 0.56f, PixelImage.Scale(Oak, 0.92f), 0.42f, 0.015f, Surface.Wood, 13u);
            Round(img, t, 0.11f, 0.51f, 0.048f, PixelImage.Scale(Oak, 0.85f), 0.46f, 0.015f, Surface.Wood, 14u);
            Round(img, t, 0.51f, 0.51f, 0.048f, PixelImage.Scale(Oak, 0.85f), 0.46f, 0.015f, Surface.Wood, 15u);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawTable(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.92f, 0.92f, t);
            Block(img, t, 0.03f, 0.03f, 0.89f, 0.89f, Oak, 0.32f, 0.018f, Surface.Wood, 21u, true, 0.215f);
            foreach (var c in new[] { new Vector2(0.07f, 0.07f), new Vector2(0.85f, 0.07f), new Vector2(0.07f, 0.85f), new Vector2(0.85f, 0.85f) })
                Stud(img, t, c.x, c.y, 0.016f, Iron, 0.32f);
            Plate(img, t, 0.36f, 0.4f, 0.12f);
            Mug(img, t, 0.64f, 0.64f);
            Candle(img, t, 0.68f, 0.3f, 0.32f);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawLongTable(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1.92f, 0.9f, t);
            Block(img, t, 0.03f, 0.04f, 1.89f, 0.86f, Oak, 0.32f, 0.018f, Surface.Wood, 31u, true, 0.205f);
            for (int i = 0; i < 4; i++) Stud(img, t, 0.08f + i * 0.585f, 0.08f, 0.015f, Iron, 0.32f);
            Plate(img, t, 0.38f, 0.26f, 0.11f);
            Plate(img, t, 0.96f, 0.64f, 0.11f);
            Plate(img, t, 1.54f, 0.26f, 0.11f);
            // A loaf of bread and a jug.
            Fabric(img, t, 0.7f, 0.32f, 1.04f, 0.5f, PixelImage.Rgb(150, 98, 52), 0.33f, 0.06f, 0.08f, 33u);
            for (int i = 0; i < 3; i++) Rod(img, t, 0.78f + i * 0.09f, 0.35f, 0.81f + i * 0.09f, 0.47f, 0.006f, PixelImage.Rgb(104, 62, 30), 0.39f, 0.39f);
            Mug(img, t, 1.3f, 0.62f);
            Candle(img, t, 0.2f, 0.66f, 0.32f);
            Candle(img, t, 1.72f, 0.66f, 0.32f);
            return Finish(img, t, rotation);
        }

        static void Plate(PixelImage img, float t, float x, float y, float r)
        {
            var pewter = PixelImage.Rgb(146, 146, 150);
            Round(img, t, x, y, r, pewter, 0.33f, 0f, Surface.Metal, 41u);
            Ring(img, t, x, y, r * 0.72f, r, PixelImage.Scale(pewter, 1.08f), 0.34f, Surface.Metal, 42u);
        }

        static void Mug(PixelImage img, float t, float x, float y)
        {
            Round(img, t, x, y, 0.055f, PixelImage.Rgb(120, 92, 62), 0.42f, 0f, Surface.Wood, 43u);
            Round(img, t, x, y, 0.038f, PixelImage.Rgb(70, 40, 20), 0.4f, 0f, Surface.Plain, 44u);   // ale inside
            Rod(img, t, x + 0.05f, y - 0.02f, x + 0.08f, y + 0.02f, 0.012f, PixelImage.Rgb(110, 84, 56), 0.4f, 0.4f);
        }

        static void Candle(PixelImage img, float t, float x, float y, float on)
        {
            Round(img, t, x, y, 0.04f, Brass, on + 0.02f, 0.005f, Surface.Metal, 45u);
            Round(img, t, x, y, 0.022f, PixelImage.Rgb(222, 212, 186), on + 0.12f, 0f, Surface.Wax, 46u);
            Stud(img, t, x, y, 0.006f, PixelImage.Rgb(40, 30, 20), on + 0.12f);
        }

        static PixelImage DrawBench(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1.82f, 0.44f, t);
            Block(img, t, 0.02f, 0.06f, 0.16f, 0.38f, DarkWood, 0.14f, 0.012f, Surface.Wood, 51u, false);
            Block(img, t, 1.66f, 0.06f, 1.8f, 0.38f, DarkWood, 0.14f, 0.012f, Surface.Wood, 52u, false);
            Block(img, t, 0.06f, 0.09f, 1.76f, 0.35f, Oak, 0.17f, 0.015f, Surface.Wood, 53u, true, 0.13f);
            foreach (float x in new[] { 0.12f, 1.7f })
                foreach (float y in new[] { 0.14f, 0.3f })
                    Stud(img, t, x, y, 0.012f, Iron, 0.17f);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawBed(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.94f, 1.94f, t);
            Block(img, t, 0.02f, 0.02f, 0.92f, 1.92f, DarkWood, 0.16f, 0.015f, Surface.Wood, 61u, false);
            Fabric(img, t, 0.08f, 0.1f, 0.86f, 1.76f, PixelImage.Rgb(196, 184, 156), 0.2f, 0.05f, 0.06f, 62u);   // straw mattress
            Fabric(img, t, 0.17f, 1.4f, 0.77f, 1.7f, PixelImage.Rgb(222, 216, 200), 0.27f, 0.06f, 0.07f, 63u);   // pillow
            var wool = PixelImage.Rgb(46, 74, 60);
            Fabric(img, t, 0.06f, 0.08f, 0.88f, 1.2f, wool, 0.26f, 0.05f, 0.05f, 64u, 0.008f, true);               // blanket
            Fabric(img, t, 0.06f, 1.08f, 0.88f, 1.26f, PixelImage.Scale(wool, 1.25f), 0.3f, 0.03f, 0.04f, 65u);    // folded edge
            for (int i = 0; i < 2; i++)
                Block(img, t, 0.08f, 0.3f + i * 0.5f, 0.86f, 0.34f + i * 0.5f, PixelImage.Rgb(150, 120, 60), 0.3f, 0.01f, Surface.Cloth, 66u);
            // Head board with posts, and a foot board.
            Block(img, t, 0f, 1.78f, 0.94f, 1.94f, PixelImage.Scale(DarkWood, 1.15f), 0.42f, 0.015f, Surface.Wood, 67u);
            Round(img, t, 0.07f, 1.86f, 0.06f, DarkWood, 0.46f, 0.02f, Surface.Wood, 68u);
            Round(img, t, 0.87f, 1.86f, 0.06f, DarkWood, 0.46f, 0.02f, Surface.Wood, 69u);
            Block(img, t, 0f, 0f, 0.94f, 0.08f, PixelImage.Scale(DarkWood, 1.1f), 0.28f, 0.012f, Surface.Wood, 70u);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawChest(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.84f, 0.64f, t);
            Block(img, t, 0.03f, 0.03f, 0.81f, 0.61f, Oak, 0.28f, 0.015f, Surface.Wood, 71u, true, 0.145f);
            // A rounded lid: higher along the middle.
            var hs = img.EnsureHeights();
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float py = (y + 0.5f) / t;
                    if (img.Pixels[y * img.Width + x].a == 0 || py < 0.04f || py > 0.6f) continue;
                    hs[y * img.Width + x] += Mathf.Sin((py - 0.04f) / 0.56f * Mathf.PI) * 0.05f * t;
                }
            foreach (float x in new[] { 0.2f, 0.64f })
                Block(img, t, x - 0.035f, 0.02f, x + 0.035f, 0.62f, Iron, 0.33f, 0.006f, Surface.Metal, 72u, false);
            Block(img, t, 0.35f, 0.02f, 0.49f, 0.13f, PixelImage.Scale(Iron, 1.15f), 0.34f, 0.006f, Surface.Metal, 73u);   // lock plate
            Stud(img, t, 0.42f, 0.07f, 0.014f, PixelImage.Rgb(20, 18, 18), 0.34f);
            foreach (float x in new[] { 0.06f, 0.78f })
                foreach (float y in new[] { 0.06f, 0.58f })
                    Stud(img, t, x, y, 0.018f, Iron, 0.3f);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawBarrel(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.64f, 0.64f, t);
            Round(img, t, 0.32f, 0.32f, 0.3f, Oak, 0.36f, 0f, Surface.Wood, 81u);
            // Lid boards across.
            for (int i = -2; i <= 2; i++)
                Rod(img, t, 0.32f - Mathf.Sqrt(Mathf.Max(0f, 0.065f - i * i * 0.0081f)), 0.32f + i * 0.09f + 0.045f,
                    0.32f + Mathf.Sqrt(Mathf.Max(0f, 0.065f - i * i * 0.0081f)), 0.32f + i * 0.09f + 0.045f, 0.004f, PixelImage.Rgb(40, 26, 16), 0.355f, 0.355f);
            Ring(img, t, 0.32f, 0.32f, 0.255f, 0.3f, Iron, 0.38f, Surface.Metal, 82u);
            Stud(img, t, 0.32f, 0.2f, 0.02f, PixelImage.Rgb(60, 42, 28), 0.36f);   // bung
            return Finish(img, t, rotation);
        }

        static PixelImage DrawBookshelf(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1.88f, 0.52f, t);
            Block(img, t, 0.01f, 0.01f, 1.87f, 0.51f, DarkWood, 0.36f, 0.012f, Surface.Wood, 91u);
            Block(img, t, 0.01f, 0.42f, 1.87f, 0.51f, PixelImage.Scale(DarkWood, 0.9f), 0.62f, 0.01f, Surface.Wood, 92u);   // back
            var dice = new Dice(93u);
            Color32[] covers =
            {
                PixelImage.Rgb(110, 30, 30), PixelImage.Rgb(40, 70, 48), PixelImage.Rgb(40, 52, 96), PixelImage.Rgb(96, 70, 40),
                PixelImage.Rgb(30, 30, 34), PixelImage.Rgb(120, 96, 50), PixelImage.Rgb(78, 34, 70),
            };
            // Book tops in three sections between dividers.
            float x = 0.06f;
            while (x < 1.8f)
            {
                float section = Mathf.Repeat(x, 0.62f);
                if (section < 0.04f) { x += 0.05f; continue; }
                float w = dice.Range(0.035f, 0.07f);
                if (dice.Chance(0.08f)) { x += w; continue; }                       // a gap
                float depth = dice.Range(0.26f, 0.34f);
                var c = covers[dice.Range(0, covers.Length)];
                Block(img, t, x, 0.06f, Mathf.Min(1.82f, x + w - 0.004f), 0.06f + depth, c, dice.Range(0.5f, 0.58f), 0.006f, Surface.Cloth, 94u + (uint)(x * 100));
                Block(img, t, x + 0.004f, 0.06f, Mathf.Min(1.82f, x + w - 0.008f), 0.075f, PixelImage.Rgb(212, 196, 150), 0.5f, 0.003f, Surface.Plain, 95u); // page edges
                x += w;
            }
            foreach (float d in new[] { 0.62f, 1.24f })
                Block(img, t, d - 0.025f, 0.02f, d + 0.025f, 0.5f, DarkWood, 0.62f, 0.008f, Surface.Wood, 96u, false);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawWeaponRack(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1.88f, 0.54f, t);
            var steel = PixelImage.Rgb(150, 154, 162);
            Block(img, t, 0.04f, 0.38f, 1.84f, 0.48f, DarkWood, 0.42f, 0.01f, Surface.Wood, 101u);   // back rail
            Block(img, t, 0.04f, 0.04f, 1.84f, 0.11f, DarkWood, 0.22f, 0.01f, Surface.Wood, 102u);   // front rail
            foreach (float x in new[] { 0.06f, 1.82f })
                Block(img, t, x - 0.035f, 0.02f, x + 0.035f, 0.5f, PixelImage.Scale(DarkWood, 0.85f), 0.45f, 0.008f, Surface.Wood, 103u, false);
            // Two spears, two swords, an axe and a shield.
            foreach (float x in new[] { 0.22f, 1.62f })
            {
                Rod(img, t, x, -0.02f, x, 0.46f, 0.016f, PixelImage.Rgb(116, 84, 52), 0.3f, 0.46f);
                img.Capsule(x * t, 0.46f * t, x * t, 0.54f * t, 0.03f * t, 0.004f * t, steel, PixelImage.Scale(steel, 1.2f), 0.47f * t, 0.48f * t);
            }
            foreach (float x in new[] { 0.48f, 0.72f })
            {
                Rod(img, t, x, 0.12f, x, 0.44f, 0.018f, steel, 0.36f, 0.4f);                       // blade
                Rod(img, t, x - 0.07f, 0.12f, x + 0.07f, 0.12f, 0.012f, Brass, 0.41f, 0.41f);     // cross guard
                Rod(img, t, x, 0.03f, x, 0.11f, 0.014f, PixelImage.Rgb(60, 40, 26), 0.4f, 0.4f); // grip
                Stud(img, t, x, 0.02f, 0.018f, Brass, 0.4f);                                       // pommel
            }
            Rod(img, t, 1.0f, 0.04f, 1.0f, 0.46f, 0.016f, PixelImage.Rgb(108, 78, 48), 0.3f, 0.42f);
            Block(img, t, 0.93f, 0.36f, 1.12f, 0.47f, steel, 0.44f, 0.01f, Surface.Metal, 104u);   // axe head
            Round(img, t, 1.35f, 0.26f, 0.19f, PixelImage.Rgb(96, 40, 32), 0.32f, 0.03f, Surface.Wood, 105u);
            Ring(img, t, 1.35f, 0.26f, 0.165f, 0.19f, Iron, 0.36f, Surface.Metal, 106u);
            Stud(img, t, 1.35f, 0.26f, 0.045f, PixelImage.Scale(steel, 0.9f), 0.36f);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawArmorStand(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.74f, 0.64f, t);
            var steel = PixelImage.Rgb(142, 146, 154);
            Round(img, t, 0.37f, 0.32f, 0.27f, DarkWood, 0.05f, 0f, Surface.Wood, 111u);           // base
            Rod(img, t, 0.08f, 0.38f, 0.66f, 0.38f, 0.02f, DarkWood, 0.5f, 0.5f);                    // shoulder bar
            Round(img, t, 0.37f, 0.3f, 0.17f, steel, 0.5f, 0.07f, Surface.Metal, 112u);             // breastplate
            Round(img, t, 0.16f, 0.38f, 0.08f, PixelImage.Scale(steel, 0.92f), 0.56f, 0.03f, Surface.Metal, 113u);   // pauldrons
            Round(img, t, 0.58f, 0.38f, 0.08f, PixelImage.Scale(steel, 0.92f), 0.56f, 0.03f, Surface.Metal, 114u);
            Round(img, t, 0.37f, 0.4f, 0.1f, PixelImage.Scale(steel, 1.05f), 0.66f, 0.05f, Surface.Metal, 115u);    // helmet
            Rod(img, t, 0.3f, 0.33f, 0.44f, 0.33f, 0.008f, PixelImage.Rgb(20, 20, 24), 0.7f, 0.7f);             // visor slit
            Fabric(img, t, 0.35f, 0.42f, 0.39f, 0.56f, PixelImage.Rgb(140, 24, 30), 0.72f, 0.01f, 0.01f, 116u); // plume
            return Finish(img, t, rotation);
        }

        static PixelImage DrawCauldron(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.82f, 0.82f, t);
            // A ring of fire stones with embers, the black pot, and stew.
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f + 0.3f;
                Round(img, t, 0.41f + Mathf.Cos(a) * 0.33f, 0.41f + Mathf.Sin(a) * 0.33f, 0.07f, PixelImage.Scale(Granite, 0.8f + (i % 3) * 0.1f), 0.07f, 0.02f, Surface.Stone, 121u + (uint)i);
            }
            var dice = new Dice(122u);
            for (int i = 0; i < 40; i++)
            {
                float a = dice.Range(0f, Mathf.PI * 2f), r = dice.Range(0.24f, 0.3f);
                Stud(img, t, 0.41f + Mathf.Cos(a) * r, 0.41f + Mathf.Sin(a) * r, dice.Range(0.006f, 0.014f),
                    dice.Chance(0.5f) ? PixelImage.Rgb(255, 130, 40) : PixelImage.Rgb(80, 30, 16), 0.03f);
            }
            Round(img, t, 0.41f, 0.41f, 0.26f, PixelImage.Rgb(30, 30, 32), 0.3f, 0f, Surface.Metal, 123u);
            Round(img, t, 0.41f, 0.41f, 0.2f, PixelImage.Rgb(88, 74, 40), 0.26f, 0f, Surface.Plain, 124u);   // stew
            for (int i = 0; i < 7; i++)
                Stud(img, t, 0.41f + dice.Range(-0.13f, 0.13f), 0.41f + dice.Range(-0.13f, 0.13f), dice.Range(0.008f, 0.02f), PixelImage.Rgb(120, 104, 60), 0.26f);
            Ring(img, t, 0.41f, 0.41f, 0.2f, 0.26f, PixelImage.Rgb(40, 40, 44), 0.33f, Surface.Metal, 125u);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawFireplace(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1.92f, 0.94f, t);
            // Hearth stones in front, the stone surround (back and sides), logs and coals inside.
            Block(img, t, 0.18f, 0.0f, 1.74f, 0.22f, PixelImage.Scale(Granite, 0.95f), 0.05f, 0.01f, Surface.Stone, 131u, true, 0.11f);
            Block(img, t, 0.38f, 0.2f, 1.54f, 0.64f, PixelImage.Rgb(30, 26, 24), 0.06f, 0.01f, Surface.Stone, 132u);   // sooty firebox floor
            for (int i = 0; i < 6; i++)
                Block(img, t, i * 0.32f, 0.64f, i * 0.32f + 0.32f, 0.94f, PixelImage.Scale(Granite, 0.85f + (i % 3) * 0.08f), 0.72f, 0.012f, Surface.Stone, 133u + (uint)i);
            for (int i = 0; i < 3; i++)
            {
                Block(img, t, 0f, 0.18f + i * 0.155f, 0.4f, 0.33f + i * 0.155f, PixelImage.Scale(Granite, 0.9f + i * 0.05f), 0.6f, 0.012f, Surface.Stone, 140u + (uint)i);
                Block(img, t, 1.52f, 0.18f + i * 0.155f, 1.92f, 0.33f + i * 0.155f, PixelImage.Scale(Granite, 0.95f - i * 0.04f), 0.6f, 0.012f, Surface.Stone, 143u + (uint)i);
            }
            Rod(img, t, 0.6f, 0.3f, 1.3f, 0.52f, 0.05f, PixelImage.Rgb(70, 46, 28), 0.1f, 0.12f);
            Rod(img, t, 0.62f, 0.52f, 1.32f, 0.3f, 0.045f, PixelImage.Rgb(80, 52, 30), 0.14f, 0.12f);
            var dice = new Dice(146u);
            for (int i = 0; i < 60; i++)
                Stud(img, t, dice.Range(0.45f, 1.47f), dice.Range(0.25f, 0.6f), dice.Range(0.006f, 0.016f),
                    dice.Chance(0.55f) ? PixelImage.Rgb(255, 120, 40) : PixelImage.Rgb(60, 26, 16), 0.08f);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawRug(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1.9f, 2.9f, t);
            var red = PixelImage.Rgb(116, 26, 30);
            var blue = PixelImage.Rgb(30, 40, 78);
            var gold = PixelImage.Rgb(176, 136, 60);
            var hs = img.EnsureHeights();
            float x0 = 0.04f, x1 = 1.86f, y0 = 0.12f, y1 = 2.78f;
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float px = (x + 0.5f) / t, py = (y + 0.5f) / t;
                    float edge = Mathf.Min(Mathf.Min(px - x0, x1 - px), Mathf.Min(py - y0, y1 - py));
                    int i = y * img.Width + x;
                    if (edge < 0f)
                    {
                        // Fringe at the short ends.
                        if (px > x0 + 0.03f && px < x1 - 0.03f && (py < y0 && py > y0 - 0.1f || py > y1 && py < y1 + 0.1f) && Mathf.Repeat(px, 0.03f) < 0.012f)
                        {
                            img.Set(x, y, PixelImage.Rgb(190, 172, 130));
                            hs[i] = 0.004f * t;
                        }
                        continue;
                    }
                    Color32 c;
                    float cx = px - 0.95f, cy = py - 1.45f;
                    float diamond = Mathf.Abs(cx) / 0.55f + Mathf.Abs(cy) / 0.85f;
                    if (edge < 0.14f)
                    {
                        // Border: blue with a gold zigzag.
                        float along = edge < 0.07f ? 0f : Mathf.Abs(Mathf.Repeat((px + py) * 6f, 2f) - 1f);
                        c = edge < 0.025f || (edge > 0.115f) ? gold : along < 0.3f && edge > 0.05f ? gold : blue;
                    }
                    else if (diamond < 1f)
                        c = diamond < 0.45f ? gold : diamond < 0.55f ? blue : diamond < 0.9f ? PixelImage.Rgb(140, 40, 38) : gold;
                    else
                        c = Mathf.Abs(Mathf.Repeat(cx * 5f, 1f) - 0.5f) + Mathf.Abs(Mathf.Repeat(cy * 5f, 1f) - 0.5f) < 0.18f ? PixelImage.Scale(gold, 0.8f) : red;
                    float weave = Mathf.Sin(px * 1300f) * Mathf.Sin(py * 1300f) * 0.04f + (Noise.Value(px * 30f, py * 30f, 151u) - 0.5f) * 0.1f;
                    img.Set(x, y, PixelImage.Scale(c, 1f + weave));
                    hs[i] = (0.006f + weave * 0.01f) * t;
                }
            return Finish(img, t, rotation, 0.4f, 0.2f);
        }

        static PixelImage DrawCandelabra(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.6f, 0.6f, t);
            float c = 0.3f;
            Round(img, t, c, c - 0.02f, 0.13f, PixelImage.Scale(Iron, 1.1f), 0.06f, 0.03f, Surface.Metal, 161u);
            foreach (var f in CandelabraFlames)
            {
                Rod(img, t, c, c, c + f.x, c + f.y, 0.014f, Brass, 0.34f, 0.38f);
                Candle(img, t, c + f.x, c + f.y, 0.36f);
            }
            Stud(img, t, c, c, 0.03f, Brass, 0.36f);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawTorch(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.36f, 0.36f, t);
            Round(img, t, 0.18f, 0.18f, 0.12f, PixelImage.Scale(Iron, 0.9f), 0.04f, 0.01f, Surface.Metal, 171u);   // foot
            Round(img, t, 0.18f, 0.18f, 0.06f, DarkWood, 0.45f, 0f, Surface.Wood, 172u);                          // post
            Ring(img, t, 0.18f, 0.18f, 0.055f, 0.085f, Iron, 0.55f, Surface.Metal, 173u);                         // cup
            Round(img, t, 0.18f, 0.18f, 0.055f, PixelImage.Rgb(40, 30, 22), 0.56f, 0.01f, Surface.Cloth, 174u);   // pitch-soaked rags
            return Finish(img, t, rotation);
        }

        static PixelImage DrawBrazier(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.84f, 0.84f, t);
            float c = 0.42f;
            for (int i = 0; i < 3; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI * 2f / 3f;
                Rod(img, t, c, c, c + Mathf.Cos(a) * 0.38f, c + Mathf.Sin(a) * 0.38f, 0.022f, Iron, 0.4f, 0.04f);
            }
            Round(img, t, c, c, 0.28f, PixelImage.Rgb(30, 28, 28), 0.42f, 0f, Surface.Metal, 181u);
            var dice = new Dice(182u);
            for (int i = 0; i < 70; i++)
            {
                float a = dice.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(dice.Next()) * 0.22f;
                Stud(img, t, c + Mathf.Cos(a) * r, c + Mathf.Sin(a) * r, dice.Range(0.01f, 0.025f),
                    dice.Chance(0.45f) ? PixelImage.Rgb(255, 128, 44) : PixelImage.Rgb(46, 32, 26), 0.44f);
            }
            Ring(img, t, c, c, 0.24f, 0.3f, Iron, 0.5f, Surface.Metal, 183u);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawAnvil(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.88f, 0.6f, t);
            Round(img, t, 0.4f, 0.3f, 0.26f, PixelImage.Rgb(96, 66, 40), 0.3f, 0f, Surface.Wood, 191u);   // stump
            var steel = PixelImage.Rgb(86, 88, 96);
            Block(img, t, 0.2f, 0.17f, 0.64f, 0.43f, steel, 0.5f, 0.02f, Surface.Metal, 192u);
            img.Capsule(0.62f * t, 0.3f * t, 0.86f * t, 0.3f * t, 0.08f * t, 0.015f * t, steel, PixelImage.Scale(steel, 1.1f), 0.5f * t, 0.48f * t);   // horn
            Block(img, t, 0.24f, 0.21f, 0.6f, 0.39f, PixelImage.Scale(steel, 1.3f), 0.51f, 0.01f, Surface.Metal, 193u);      // polished face
            Rod(img, t, 0.28f, 0.48f, 0.5f, 0.56f, 0.012f, PixelImage.Rgb(90, 64, 40), 0.33f, 0.33f);                         // hammer handle
            Block(img, t, 0.24f, 0.5f, 0.34f, 0.58f, PixelImage.Scale(steel, 0.9f), 0.36f, 0.008f, Surface.Metal, 194u);      // hammer head
            return Finish(img, t, rotation);
        }

        static PixelImage DrawCrates(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.94f, 0.94f, t);
            CrateBox(img, t, 0.48f, 0.44f, 0.4f, 0.36f, 201u);
            CrateBox(img, t, 0.04f, 0.04f, 0.56f, 0.42f, 202u);
            Fabric(img, t, 0.6f, 0.06f, 0.9f, 0.34f, PixelImage.Rgb(138, 118, 84), 0.08f, 0.1f, 0.1f, 203u);   // a sack
            Stud(img, t, 0.75f, 0.31f, 0.03f, PixelImage.Rgb(110, 92, 62), 0.2f);
            return Finish(img, t, rotation);
        }

        static void CrateBox(PixelImage img, float t, float x0, float y0, float size, float top, uint seed)
        {
            var wood = PixelImage.Rgb(126, 92, 58);
            Block(img, t, x0, y0, x0 + size, y0 + size, wood, top, 0.012f, Surface.Wood, seed, true, size / 3f);
            float f = 0.03f;
            var frame = PixelImage.Scale(wood, 0.82f);
            Block(img, t, x0, y0, x0 + size, y0 + f, frame, top + 0.01f, 0.005f, Surface.Wood, seed + 1u);
            Block(img, t, x0, y0 + size - f, x0 + size, y0 + size, frame, top + 0.01f, 0.005f, Surface.Wood, seed + 2u);
            Rod(img, t, x0 + f, y0 + f, x0 + size - f, y0 + size - f, f * 0.5f, frame, top + 0.012f, top + 0.012f);
            foreach (var c in new[] { new Vector2(x0 + f, y0 + f), new Vector2(x0 + size - f, y0 + f), new Vector2(x0 + f, y0 + size - f), new Vector2(x0 + size - f, y0 + size - f) })
                Stud(img, t, c.x, c.y, 0.01f, Iron, top + 0.015f);
        }

        static PixelImage DrawBanner(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(0.92f, 0.72f, t);
            var red = PixelImage.Rgb(130, 22, 30);
            var gold = PixelImage.Rgb(190, 150, 64);
            // The banner, hanging from its pole and stirring in the wind, then the pole on top.
            Fabric(img, t, 0.16f, 0.12f, 0.88f, 0.6f, red, 0.5f, 0.02f, 0.02f, 211u, 0.012f, true);
            var hs = img.EnsureHeights();
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float px = (x + 0.5f) / t, py = (y + 0.5f) / t;
                    int i = y * img.Width + x;
                    if (img.Pixels[i].a == 0 || px < 0.16f) continue;
                    hs[i] -= (px - 0.16f) * 0.25f * t;   // droops away from the pole
                    float bx = px - 0.52f, by = py - 0.36f;
                    bool border = px < 0.2f || px > 0.84f || py < 0.16f || py > 0.56f;
                    // A gold chevron between three gold roundels.
                    bool chevron = Mathf.Abs(by - (0.07f - Mathf.Abs(bx) * 0.7f)) < 0.032f && Mathf.Abs(bx) < 0.3f;
                    float ux = Mathf.Abs(bx) - 0.17f, uy = by - 0.11f, ly = by + 0.11f;
                    bool roundel = ux * ux + uy * uy < 0.0018f || bx * bx + ly * ly < 0.0018f;
                    if (border || chevron || roundel) img.Set(x, y, PixelImage.Scale(gold, 0.85f + (img.Pixels[i].r - red.r) / 255f));
                }
            Round(img, t, 0.12f, 0.36f, 0.05f, DarkWood, 0.9f, 0f, Surface.Wood, 212u);
            Stud(img, t, 0.12f, 0.36f, 0.035f, Brass, 0.92f);
            return Finish(img, t, rotation);
        }

        static PixelImage DrawWell(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1.9f, 1.9f, t);
            float c = 0.95f;
            // Water, then the stone ring, then the wooden frame with the bucket.
            var hs = img.EnsureHeights();
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float px = (x + 0.5f) / t - c, py = (y + 0.5f) / t - c;
                    float d = Mathf.Sqrt(px * px + py * py);
                    if (d > 0.58f) continue;
                    float ripple = Mathf.Sin(d * 90f + Noise.Value(px * 8f, py * 8f, 221u) * 4f) * 0.05f;
                    var water = PixelImage.Mix(PixelImage.Rgb(12, 26, 30), PixelImage.Rgb(30, 58, 64), Mathf.Clamp01(0.4f + ripple * 4f - d * 0.5f));
                    img.Blend(x, y, water, Mathf.Clamp01((0.58f - d) * t + 0.5f));
                    hs[y * img.Width + x] = (0.02f + ripple * 0.02f) * t;
                }
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                Round(img, t, c + Mathf.Cos(a) * 0.72f, c + Mathf.Sin(a) * 0.72f, 0.19f, PixelImage.Scale(Granite, 0.85f + (i % 4) * 0.07f), 0.42f, 0.04f, Surface.Stone, 222u + (uint)i);
            }
            Round(img, t, 0.2f, c, 0.08f, DarkWood, 0.85f, 0f, Surface.Wood, 240u);
            Round(img, t, 1.7f, c, 0.08f, DarkWood, 0.85f, 0f, Surface.Wood, 241u);
            Block(img, t, 0.16f, c - 0.05f, 1.74f, c + 0.05f, Oak, 0.92f, 0.015f, Surface.Wood, 242u);
            Rod(img, t, c, c, c + 0.02f, c - 0.18f, 0.008f, PixelImage.Rgb(150, 126, 84), 0.9f, 0.6f);   // rope
            Round(img, t, c + 0.02f, c - 0.24f, 0.1f, PixelImage.Rgb(110, 80, 50), 0.58f, 0f, Surface.Wood, 243u);
            Ring(img, t, c + 0.02f, c - 0.24f, 0.08f, 0.1f, Iron, 0.6f, Surface.Metal, 244u);
            return Finish(img, t, rotation);
        }

        // A ladder leaning on the wall at the top of its tile: two rails rising towards the wall, rungs across.
        static PixelImage DrawLadder(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var wood = PixelImage.Rgb(122, 88, 54);
            for (int i = 0; i < 7; i++)
            {
                float y = 0.12f + i * 0.13f, h = 0.06f + y * 0.38f;
                Rod(img, t, 0.3f, y, 0.7f, y, 0.022f, PixelImage.Scale(wood, 0.92f + (i % 2) * 0.06f), h, h);
            }
            foreach (float x in new[] { 0.28f, 0.72f })
                Rod(img, t, x, 0.03f, x + (x < 0.5f ? 0.02f : -0.02f), 0.99f, 0.035f, wood, 0.03f, 0.44f);
            // Rope lashings where the rungs meet the rails.
            for (int i = 0; i < 7; i += 2)
                foreach (float x in new[] { 0.29f, 0.71f })
                    Stud(img, t, x, 0.12f + i * 0.13f, 0.02f, PixelImage.Rgb(150, 126, 84), 0.07f + (0.12f + i * 0.13f) * 0.4f);
            return Finish(img, t, rotation);
        }

        // Stone steps rising towards the wall at the top of the tile, between two low side walls.
        // A mine's entrance from above: a timber headframe over a dark shaft, a winch drum with its rope, the ladder going down.
        static PixelImage DrawMineHead(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(2f, 2f, t);
            Block(img, t, 0.05f, 0.05f, 1.95f, 1.95f, PixelImage.Rgb(84, 70, 54), 0.04f, 0.03f, Surface.Stone, 1401u);   // trodden earth and spoil
            Block(img, t, 0.45f, 0.45f, 1.55f, 1.55f, PixelImage.Rgb(10, 8, 7), 0.01f, 0.02f, Surface.Stone, 1402u);     // the shaft
            for (int i = 0; i < 6; i++)
                Rod(img, t, 0.62f, 0.5f + i * 0.17f, 0.88f, 0.5f + i * 0.17f, 0.018f, PaleWood, 0.03f, 0.03f);            // the ladder's rungs
            Rod(img, t, 0.62f, 0.45f, 0.62f, 1.55f, 0.025f, Oak, 0.04f, 0.04f);
            Rod(img, t, 0.88f, 0.45f, 0.88f, 1.55f, 0.025f, Oak, 0.04f, 0.04f);
            // The frame: a timber square round the shaft and four legs leaning in to a crossbeam.
            foreach (var (x0, y0, x1, y1) in new[] { (0.35f, 0.4f, 1.65f, 0.4f), (0.35f, 1.6f, 1.65f, 1.6f), (0.4f, 0.35f, 0.4f, 1.65f), (1.6f, 0.35f, 1.6f, 1.65f) })
                Rod(img, t, x0, y0, x1, y1, 0.07f, DarkWood, 0.12f, 0.12f);
            foreach (var (x, y) in new[] { (0.25f, 0.25f), (1.75f, 0.25f), (0.25f, 1.75f), (1.75f, 1.75f) })
                Rod(img, t, x, y, 1f + (x - 1f) * 0.35f, 1f + (y - 1f) * 0.35f, 0.06f, Oak, 0.15f, 1.1f);
            Rod(img, t, 0.6f, 1f, 1.4f, 1f, 0.12f, PixelImage.Rgb(96, 72, 46), 1.15f, 1.15f);                                // the winch drum
            Rod(img, t, 1.05f, 1f, 1.05f, 0.7f, 0.012f, PixelImage.Rgb(170, 150, 110), 1.1f, 0.4f);                          // its rope
            Round(img, t, 1.05f, 0.68f, 0.07f, PixelImage.Rgb(80, 64, 44), 0.42f, 0.02f, Surface.Wood, 1403u);               // the bucket
            return Finish(img, t, rotation);
        }

        static PixelImage DrawStoneStairs(int ppu, int rotation)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            for (int i = 0; i < 5; i++)
            {
                float y0 = i * 0.2f, top = 0.06f + i * 0.085f;
                Block(img, t, 0.1f, y0, 0.9f, y0 + 0.2f, PixelImage.Scale(Granite, 0.86f + (i % 2) * 0.08f + i * 0.02f), top, 0.012f, Surface.Stone, 520u + (uint)i);
                // The worn front edge of each step.
                Block(img, t, 0.11f, y0, 0.89f, y0 + 0.025f, PixelImage.Scale(Granite, 0.7f), top + 0.005f, 0.004f, Surface.Stone, 530u + (uint)i);
            }
            foreach (float x in new[] { 0f, 0.9f })
                Block(img, t, x, 0f, x + 0.1f, 1f, PixelImage.Scale(Granite, 0.78f), 0.46f, 0.012f, Surface.Stone, 540u, false);
            return Finish(img, t, rotation);
        }

        // -------------------------------------------------------------- floors --

        /// <summary>One tile of wooden floor: four boards across with joints in different places.</summary>
        public static PixelImage WoodFloorTile(int ppu, int variant)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var dice = new Dice(300u + (uint)variant * 17u);
            var hs = img.EnsureHeights();
            for (int board = 0; board < 4; board++)
            {
                float y0 = board * 0.25f, y1 = y0 + 0.25f;
                float joint = dice.Chance(0.6f) ? dice.Range(0.2f, 0.8f) : -1f;
                var tone = PixelImage.Scale(PixelImage.Rgb(110, 78, 50), dice.Range(0.82f, 1.08f));
                uint seed = 310u + (uint)(variant * 4 + board);
                for (int y = Mathf.FloorToInt(y0 * t); y < Mathf.CeilToInt(y1 * t); y++)
                    for (int x = 0; x < img.Width; x++)
                    {
                        if (!img.Inside(x, y)) continue;
                        float px = (x + 0.5f) / t, py = (y + 0.5f) / t;
                        bool gap = py - y0 < 0.006f || y1 - py < 0.006f || (joint > 0f && Mathf.Abs(px - joint) < 0.004f);
                        float shade = Texture(Surface.Wood, px + board * 3.7f, py, seed, true) * (joint > 0f && px > joint ? 1.06f : 1f);
                        img.Set(x, y, gap ? PixelImage.Rgb(30, 22, 16) : PixelImage.Scale(tone, shade));
                        hs[y * img.Width + x] = gap ? 0f : (0.012f + (shade - 1f) * 0.004f) * t;
                    }
                if (joint > 0f)
                {
                    Stud(img, t, joint - 0.025f, y0 + 0.125f, 0.008f, Iron, 0.012f);
                    Stud(img, t, joint + 0.025f, y0 + 0.125f, 0.008f, Iron, 0.012f);
                }
            }
            img.BakeLight(0.5f, 0.5f, Mathf.Max(1, Mathf.RoundToInt(0.008f * t)));
            return img;
        }

        /// <summary>One tile of stone floor: four uneven flagstones in mortar.</summary>
        public static PixelImage StoneFloorTile(int ppu, int variant)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var dice = new Dice(400u + (uint)variant * 23u);
            var hs = img.EnsureHeights();
            float splitX = dice.Range(0.4f, 0.6f), splitY = dice.Range(0.4f, 0.6f);
            float splitX2 = dice.Range(0.35f, 0.65f);   // the top row splits somewhere else
            var tones = new Color32[4];
            for (int i = 0; i < 4; i++) tones[i] = PixelImage.Scale(PixelImage.Rgb(104, 100, 94), dice.Range(0.82f, 1.12f));
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float px = (x + 0.5f) / t, py = (y + 0.5f) / t;
                    bool top = py > splitY;
                    float sx = top ? splitX2 : splitX;
                    int slab = (top ? 2 : 0) + (px > sx ? 1 : 0);
                    float edge = Mathf.Min(Mathf.Min(Mathf.Abs(px - sx), Mathf.Abs(py - splitY)), Mathf.Min(Mathf.Min(px, 1f - px), Mathf.Min(py, 1f - py)));
                    edge += (Noise.Value(px * 60f, py * 60f, 410u) - 0.5f) * 0.012f;
                    int i = y * img.Width + x;
                    if (edge < 0.012f)
                    {
                        img.Set(x, y, PixelImage.Scale(PixelImage.Rgb(52, 50, 46), 0.85f + Noise.Value(px * 200f, py * 200f, 411u) * 0.3f));
                        hs[i] = 0f;
                        continue;
                    }
                    float shade = Texture(Surface.Stone, px + slab * 5f, py, 412u + (uint)variant, true);
                    img.Set(x, y, PixelImage.Scale(tones[slab], shade));
                    hs[i] = (0.012f + Mathf.Min(edge, 0.03f) * 0.4f + (shade - 1f) * 0.006f) * t;
                }
            img.BakeLight(0.6f, 0.6f, Mathf.Max(1, Mathf.RoundToInt(0.01f * t)));
            return img;
        }

        // --------------------------------------------------------------- doors --

        /// <summary>
        /// A door in a wall, seen from above, closed or swung open. Facing 0 it sits in a wall
        /// running left to right; facing 1, in a wall running up and down.
        /// </summary>
        public static PixelImage Door(int ppu, bool heavy, bool open, int rotation)
        {
            float t = ppu;
            var img = Canvas(1f, 1f, t);
            var post = heavy ? Granite : DarkWood;
            var surface = heavy ? Surface.Stone : Surface.Wood;
            // A worn sill along the doorway, then the posts at both ends.
            Block(img, t, 0.14f, 0.36f, 0.86f, 0.64f, PixelImage.Rgb(78, 74, 68), 0.02f, 0.008f, Surface.Stone, 501u, true, 0.14f);
            Block(img, t, 0f, 0f, 0.15f, 1f, post, 0.6f, 0.015f, surface, 502u, false);
            Block(img, t, 0.85f, 0f, 1f, 1f, post, 0.6f, 0.015f, surface, 503u, false);
            float thick = heavy ? 0.12f : 0.08f;
            var wood = heavy ? PixelImage.Rgb(92, 62, 38) : Oak;
            if (!open)
            {
                Block(img, t, 0.15f, 0.5f - thick, 0.85f, 0.5f + thick, wood, 0.55f, 0.01f, Surface.Wood, 504u, false, 0.1f);
                foreach (float x in new[] { 0.3f, 0.7f })
                    Block(img, t, x - 0.02f, 0.5f - thick - 0.005f, x + 0.02f, 0.5f + thick + 0.005f, Iron, 0.57f, 0.004f, Surface.Metal, 505u, false);
                if (heavy)
                    for (int i = 0; i < 5; i++) Stud(img, t, 0.2f + i * 0.15f, 0.5f, 0.014f, PixelImage.Scale(Iron, 1.3f), 0.57f);
            }
            else
            {
                // Swung in against the left post.
                Block(img, t, 0.15f, 0.5f, 0.15f + thick * 2f, 0.98f, wood, 0.55f, 0.01f, Surface.Wood, 506u, true, 0.1f);
                Block(img, t, 0.15f, 0.62f, 0.15f + thick * 2f, 0.66f, Iron, 0.57f, 0.004f, Surface.Metal, 507u);
            }
            return Finish(img, t, rotation);
        }

        /// <summary>
        /// A big double gate, <paramref name="width"/> tiles across (facing 0: in a wall running left to
        /// right), with a stone post at each end. Closed: two leaves meeting in the middle, held by iron
        /// bands (and studs on the castle gate). Open: both leaves swung in against their posts.
        /// </summary>
        public static PixelImage Gate(int ppu, int width, bool castle, bool open, int rotation)
        {
            float t = ppu, w = width;
            var img = Canvas(w, 1f, t);
            var post = castle ? Granite : DarkWood;
            var surface = castle ? Surface.Stone : Surface.Wood;
            Block(img, t, 0.3f, 0.34f, w - 0.3f, 0.66f, PixelImage.Rgb(78, 74, 68), 0.02f, 0.01f, Surface.Stone, 511u, true, 0.3f);   // the sill
            Block(img, t, 0f, 0f, 0.3f, 1f, post, 0.75f, 0.02f, surface, 512u, false);
            Block(img, t, w - 0.3f, 0f, w, 1f, post, 0.75f, 0.02f, surface, 513u, false);
            float thick = castle ? 0.16f : 0.12f, leaf = (w - 0.6f) / 2f;
            var wood = castle ? PixelImage.Rgb(86, 58, 36) : Oak;
            if (!open)
            {
                for (int side = 0; side < 2; side++)
                {
                    float x0 = 0.3f + side * leaf, x1 = x0 + leaf;
                    Block(img, t, x0, 0.5f - thick, x1 - 0.01f, 0.5f + thick, wood, 0.65f, 0.012f, Surface.Wood, 514u + (uint)side, true, 0.16f);
                    foreach (float f in new[] { 0.2f, 0.8f })
                    {
                        float x = x0 + leaf * f;
                        Block(img, t, x - 0.035f, 0.5f - thick - 0.006f, x + 0.035f, 0.5f + thick + 0.006f, Iron, 0.67f, 0.005f, Surface.Metal, 516u, false);
                    }
                    if (castle)
                        for (float x = x0 + 0.15f; x < x1 - 0.1f; x += 0.2f) Stud(img, t, x, 0.5f, 0.02f, PixelImage.Scale(Iron, 1.3f), 0.67f);
                }
                Block(img, t, w / 2f - 0.5f, 0.5f - 0.04f, w / 2f + 0.5f, 0.5f + 0.04f, Iron, 0.7f, 0.006f, Surface.Metal, 517u);   // the crossbar
            }
            else
            {
                // Each leaf swung in against its post.
                const float depth = 0.48f;   // (folded back within the doorway's tile, like the small door)
                Block(img, t, 0.3f, 0.5f, 0.3f + thick * 2f, 0.5f + depth, wood, 0.65f, 0.012f, Surface.Wood, 518u, false, 0.16f);
                Block(img, t, w - 0.3f - thick * 2f, 0.5f, w - 0.3f, 0.5f + depth, wood, 0.65f, 0.012f, Surface.Wood, 519u, false, 0.16f);
            }
            return Finish(img, t, rotation);
        }

        // ------------------------------------------------------------- effects --

        /// <summary>
        /// A small bright flame seen from above (drawn glowing over torches, candles and fires;
        /// not lit by the scene): a white-hot heart, ragged orange tongues licking out, and a
        /// faint glow round it.
        /// </summary>
        public static PixelImage Flame(int ppu)
        {
            float t = ppu;
            var img = Canvas(0.24f, 0.24f, t);
            float c = 0.12f;
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float px = (x + 0.5f) / t - c, py = (y + 0.5f) / t - c;
                    float a = Mathf.Atan2(py, px);
                    // Uneven tongues: several sizes of wobble round the edge, so it isn't a star.
                    float tongues = 1f + (Noise.Value(Mathf.Cos(a) * 2.2f + 5f, Mathf.Sin(a) * 2.2f + 5f, 601u) - 0.5f) * 0.55f
                                    + (Noise.Value(Mathf.Cos(a) * 6f + 9f, Mathf.Sin(a) * 6f + 9f, 602u) - 0.5f) * 0.3f;
                    float r = Mathf.Sqrt(px * px + py * py);
                    float d = r / (0.085f * tongues);
                    float glow = Mathf.Clamp01(1f - r / 0.12f);
                    if (d >= 1f)
                    {
                        // The faint glow beyond the flames.
                        if (glow <= 0f) continue;
                        var halo = PixelImage.Rgb(255, 120, 40);
                        halo.a = (byte)Mathf.RoundToInt(glow * glow * 70f);
                        img.Set(x, y, halo);
                        continue;
                    }
                    // Brighter streaks running out from the middle.
                    float streak = (Noise.Value(a * 3f, r * 60f, 603u) - 0.5f) * 0.25f;
                    float k = Mathf.Clamp01(d + streak);
                    var col = k < 0.3f ? PixelImage.Mix(PixelImage.Rgb(255, 248, 214), PixelImage.Rgb(255, 214, 120), k / 0.3f)
                        : k < 0.65f ? PixelImage.Mix(PixelImage.Rgb(255, 214, 120), PixelImage.Rgb(255, 128, 40), (k - 0.3f) / 0.35f)
                        : PixelImage.Mix(PixelImage.Rgb(255, 128, 40), PixelImage.Rgb(190, 46, 16), (k - 0.65f) / 0.35f);
                    float edge = Mathf.Clamp01((1f - d) * 2.5f);
                    col.a = (byte)Mathf.RoundToInt(Mathf.Max(edge, glow * glow * 0.27f) * 255f);
                    img.Set(x, y, col);
                }
            return img;
        }

        /// <summary>A splatter of blood on the ground (lit like the ground, no bumps).</summary>
        public static PixelImage Blood(int ppu, int variant)
        {
            float t = ppu;
            var img = Canvas(0.9f, 0.9f, t);
            var dice = new Dice(700u + (uint)variant * 31u);
            float c = 0.45f;
            uint seed = 701u + (uint)variant;
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    float px = (x + 0.5f) / t - c, py = (y + 0.5f) / t - c;
                    float a = Mathf.Atan2(py, px);
                    float reach = 0.16f + (Noise.Value(Mathf.Cos(a) * 2.5f + 9f, Mathf.Sin(a) * 2.5f + 9f, seed) - 0.5f) * 0.14f;
                    float d = Mathf.Sqrt(px * px + py * py);
                    if (d >= reach) continue;
                    float depth = 1f - d / reach;
                    var col = PixelImage.Mix(PixelImage.Rgb(96, 10, 12), PixelImage.Rgb(50, 4, 6), Mathf.Clamp01(depth * 1.4f));
                    col.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(depth * 4f) * 230f);
                    img.Set(x, y, col);
                }
            // Droplets flung out.
            for (int i = 0; i < 18; i++)
            {
                float a = dice.Range(0f, Mathf.PI * 2f), r = dice.Range(0.18f, 0.42f), size = dice.Range(0.008f, 0.03f) * (1f - r);
                img.Ellipse((c + Mathf.Cos(a) * r) * t, (c + Mathf.Sin(a) * r) * t, size * t + 0.6f, size * t * dice.Range(0.6f, 1f) + 0.6f, PixelImage.Rgb(80, 8, 10, 220));
            }
            return img;
        }

        /// <summary>A small sack of loot dropped on the ground.</summary>
        public static PixelImage LootBag(int ppu)
        {
            float t = ppu;
            var img = Canvas(0.42f, 0.42f, t);
            Fabric(img, t, 0.06f, 0.05f, 0.36f, 0.33f, PixelImage.Rgb(132, 104, 66), 0.04f, 0.1f, 0.12f, 801u, 0.004f);
            Rod(img, t, 0.14f, 0.32f, 0.28f, 0.32f, 0.018f, PixelImage.Rgb(90, 66, 38), 0.16f, 0.16f);   // the tied neck
            Fabric(img, t, 0.16f, 0.32f, 0.26f, 0.4f, PixelImage.Rgb(120, 94, 60), 0.14f, 0.02f, 0.04f, 802u);
            Stud(img, t, 0.3f, 0.12f, 0.025f, PixelImage.Rgb(212, 172, 70), 0.12f);                        // a coin peeking out
            return Finish(img, t, 0);
        }
    }
}
