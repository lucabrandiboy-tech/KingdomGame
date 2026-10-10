// Renders the code-drawn art (Assets/Scripts/Art) without Unity.
//
//   dotnet run -c Release --project tools/art-preview/ArtPreview.csproj -- <folder> [pixels per tile]
//
// Bakes every picture in ArtCatalog (like KingdomGame > Build Game Scenes does), then
// draws mock screenshots of a small kingdom into <folder>:
//   game_day.png    the normal view (24 tiles tall) in the late afternoon
//   game_night.png  zoomed in a bit, at night, lit by braziers and torches
//   game_close.png  zoomed in as far as the game allows (6 tiles tall), to show the detail
//   game_outpost.png  the test outpost's walls, 8 tiles tall
//   game_zoomed.png the closest zoom the game allows (4 tiles tall), at night
//   castle_night.png  a furnished castle (great hall, bedroom, library) at night
//   castle_close.png  inside it up close, by day
//   horde_night.png   zombies at the castle gate at night
// The mock lights sprites the way URP's 2D lights do (color times light, normal
// maps for point lights) and adds bloom, vignette and color grading like the game's
// post-processing. It's an approximation: Unity's own result will differ a little.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using KingdomGame.Art;
using UnityEngine;

static class Program
{
    static int Ppu = ArtCatalog.DefaultPixelsPerTile;
    // The finest mip level kept in memory: the mock screenshots never show more than
    // about 256 pixels per tile, so the full-size sheets are let go to save memory.
    static int MinLevel;

    sealed class Sheet
    {
        public List<PixelImage> Albedo = new List<PixelImage>();
        public List<PixelImage> Normals;
        public bool Repeats;
    }

    sealed class Entry
    {
        public Sheet Sheet;
        public int X, Y, W, H;
        public Vector2 Pivot;
        public float Ppt;
    }

    static readonly Dictionary<string, Entry> Bank = new Dictionary<string, Entry>();

    static void Main(string[] args)
    {
        string dir = args[0];
        if (args.Length > 1 && !args[1].StartsWith("--")) Ppu = int.Parse(args[1]);
        while ((Ppu >> MinLevel) > 256) MinLevel++;
        // Optional: --sheet=<name> also saves that sheet's picture (for looking at the art up close).
        var dump = new HashSet<string>();
        foreach (var a in args) if (a.StartsWith("--sheet=")) dump.Add(a.Substring("--sheet=".Length));
        var sw = Stopwatch.StartNew();
        // --roofs: only the roofs (each kind on a few house shapes), quickly.
        if (Array.IndexOf(args, "--roofs") >= 0)
        {
            SaveRoofs(dir);
            Console.WriteLine($"roofs done in {sw.Elapsed.TotalSeconds:0.0}s");
            return;
        }
        var walls = new[]
        {
            new WallLook { Name = "StoneWall", Logs = false, Gap = PixelImage.Rgb(46, 44, 42), Variants = 4,
                Colors = new[] { PixelImage.Rgb(112, 110, 104), PixelImage.Rgb(100, 98, 94), PixelImage.Rgb(122, 118, 110), PixelImage.Rgb(92, 92, 94) } },
            new WallLook { Name = "WoodPalisade", Logs = true, Gap = PixelImage.Rgb(30, 22, 16), Variants = 4,
                Colors = new[] { PixelImage.Rgb(150, 112, 72), PixelImage.Rgb(138, 102, 66), PixelImage.Rgb(162, 122, 80), PixelImage.Rgb(128, 94, 60) } },
        };
        int count = 0;
        ArtSheet.Pack(ArtCatalog.Items(walls), Ppu, s =>
        {
            count++;
            Console.WriteLine($"  {s.Name}: {s.Albedo.Width} x {s.Albedo.Height}{(s.Normals != null ? " + normal map" : "")}, {s.Entries.Count} pictures ({sw.Elapsed.TotalSeconds:0.0}s)");
            if (dump.Contains(s.Name)) SaveImage(s.Albedo, Path.Combine(dir, $"sheet_{s.Name}.png"));
            var sheet = new Sheet { Repeats = s.Repeats };
            sheet.Albedo.Add(s.Albedo);
            while (sheet.Albedo[^1].Width > 1 && sheet.Albedo[^1].Height > 1) sheet.Albedo.Add(sheet.Albedo[^1].Downsample(2));
            // Keep the levels with up to 256 pixels per tile (half-detail sheets keep their sharpest).
            float ppt = 0f;
            foreach (var e in s.Entries) ppt = MathF.Max(ppt, e.PixelsPerTile);
            int keep = 0;
            while (ppt / (1 << keep) > 256f) keep++;
            for (int l = 0; l < keep && l < sheet.Albedo.Count - 1; l++) sheet.Albedo[l] = null;
            if (s.Normals != null)
            {
                sheet.Normals = new List<PixelImage> { s.Normals };
                while (sheet.Normals[^1].Width > 1 && sheet.Normals[^1].Height > 1) sheet.Normals.Add(sheet.Normals[^1].Downsample(2));
                for (int l = 0; l < keep && l < sheet.Normals.Count - 1; l++) sheet.Normals[l] = null;
            }
            foreach (var e in s.Entries)
                Bank[e.Item.Key] = new Entry { Sheet = sheet, X = e.X, Y = e.Y, W = e.Width, H = e.Height, Pivot = e.Pivot, Ppt = e.PixelsPerTile };
            s.Albedo = s.Normals = null;
            GC.Collect();   // like ArtBaker
        });
        Console.WriteLine($"baked {count} sheets at {Ppu} px/tile in {sw.Elapsed.TotalSeconds:0.0}s");

        var world = new MockWorld(4242u, false);
        Render(world, Path.Combine(dir, "game_day.png"), 1920, 1080, new Vector2(1.5f, 0.5f), 12f, Time.Day);
        Render(world, Path.Combine(dir, "game_night.png"), 1920, 1080, new Vector2(1f, 1f), 6f, Time.Night);
        Render(world, Path.Combine(dir, "game_close.png"), 1920, 1080, new Vector2(-0.3f, 1.4f), 3f, Time.Day);
        Render(world, Path.Combine(dir, "game_outpost.png"), 1920, 1080, new Vector2(9f, 1.5f), 4f, Time.Day);
        Render(world, Path.Combine(dir, "game_zoomed.png"), 1920, 1080, new Vector2(0.1f, 1.6f), 2f, Time.Night);
        var castle = new MockWorld(4242u, true);
        Render(castle, Path.Combine(dir, "castle_night.png"), 1920, 1080, new Vector2(0f, 2.6f), 7.5f, Time.Night);
        Render(castle, Path.Combine(dir, "castle_close.png"), 1920, 1080, new Vector2(-1.2f, 1.2f), 3f, Time.Day);
        Render(castle, Path.Combine(dir, "horde_night.png"), 1920, 1080, new Vector2(0.5f, -4.2f), 5f, Time.Night);
        Console.WriteLine($"done in {sw.Elapsed.TotalSeconds:0.0}s");
    }

    enum Time { Day, Night }

    // Every kind of roof on four house shapes: a cottage, a long hall, an L and a T, side by side.
    static void SaveRoofs(string dir)
    {
        string[] plan =
        {
            "..............................",
            ".###.......#####..............",
            ".###.......#####....#######...",
            ".###.......##.......#######...",
            "...........##..........#......",
            ".#########.##..........#......",
            ".#########.............#......",
            ".#########....................",
            "..............................",
        };
        int w = plan[0].Length, h = plan.Length;
        var cells = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                cells[y * w + x] = plan[h - 1 - y][x] == '#';
        foreach (RoofStyle style in Enum.GetValues(typeof(RoofStyle)))
        {
            var roof = RoofArt.Roof(cells, w, h, 64, style, 5u);
            // On grass-coloured ground, so the edges show.
            var img = new PixelImage(roof.Width, roof.Height);
            for (int i = 0; i < img.Pixels.Length; i++)
            {
                var c = roof.Pixels[i];
                img.Pixels[i] = c.a > 0 ? c : PixelImage.Rgb(70, 84, 52);
            }
            SaveImage(img, Path.Combine(dir, $"roof_{style}.png"));
        }

        // The houses (HouseArt, like Kingdom.HouseLook): every level facing down, and the first one every way, on grass.
        var styles = new[] { RoofStyle.Thatch, RoofStyle.WoodShingles, RoofStyle.ClayTiles, RoofStyle.Slate, RoofStyle.Copper,
            RoofStyle.CorrugatedIron, RoofStyle.TarFelt, RoofStyle.Concrete };
        var chimneys = new[] { PixelImage.Rgb(112, 108, 100), PixelImage.Rgb(112, 108, 100), PixelImage.Rgb(112, 108, 100), PixelImage.Rgb(112, 108, 100),
            PixelImage.Rgb(140, 72, 52), PixelImage.Rgb(140, 72, 52), PixelImage.Rgb(118, 118, 114), PixelImage.Rgb(118, 118, 114) };
        const int cell = 64 * 5;
        var sheet = new PixelImage(cell * 6, cell * 2);
        for (int i = 0; i < sheet.Pixels.Length; i++) sheet.Pixels[i] = PixelImage.Rgb(70, 84, 52);
        for (int i = 0; i < 12; i++)
        {
            int level = i < 8 ? i : 0, rotation = i < 8 ? 0 : i - 8;
            var house = HouseArt.Exterior(new Vector2Int(3, 3), rotation, styles[level], chimneys[level],
                level >= 3 ? PixelImage.Rgb(84, 70, 58) : PixelImage.Rgb(104, 72, 44), 64, 17u + (uint)level * 7u);
            int ox = (i % 6) * cell, oy = (1 - i / 6) * cell;
            for (int y = 0; y < house.Height; y++)
                for (int x = 0; x < house.Width; x++)
                {
                    var c = house.Pixels[y * house.Width + x];
                    if (c.a > 0) sheet.Pixels[(oy + y) * sheet.Width + ox + x] = c;
                }
        }
        SaveImage(sheet, Path.Combine(dir, "houses.png"));

        // Traps and towers (DefenseArt) at 256 pixels per tile: each base, sprung trap, and four directions of each head.
        const int ppt = 256;
        var pics = new List<PixelImage>();
        foreach (var d in FurnitureArt.Defenses)
        {
            pics.Add(d.Draw(ppt));
            if (d.Sprung != null) pics.Add(d.Sprung(ppt));
            if (d.Head != null) for (int k = 0; k < 2; k++) pics.Add(d.Head(ppt, k * 135f + 30f));
        }
        int box = ppt * 2 + 16, perRow = 10;
        var defenses = new PixelImage(box * perRow, box * ((pics.Count + perRow - 1) / perRow));
        for (int i = 0; i < defenses.Pixels.Length; i++) defenses.Pixels[i] = PixelImage.Rgb(70, 84, 52);
        for (int i = 0; i < pics.Count; i++)
        {
            var pic = pics[i];
            int ox = (i % perRow) * box + (box - pic.Width) / 2, oy = defenses.Height - (i / perRow + 1) * box + (box - pic.Height) / 2;
            for (int y = 0; y < pic.Height; y++)
                for (int x = 0; x < pic.Width; x++)
                {
                    var c = pic.Pixels[y * pic.Width + x];
                    if (c.a == 0) continue;
                    int k = (oy + y) * defenses.Width + ox + x;
                    defenses.Pixels[k] = PixelImage.Mix(defenses.Pixels[k], c, c.a / 255f);
                }
        }
        SaveImage(defenses, Path.Combine(dir, "defenses.png"));
    }

    // ------------------------------------------------------------- world --

    sealed class Inst
    {
        public string Key;
        public float X, Y, Rot, Scale = 1f, Alpha = 1f;
        public float R = 1f, G = 1f, B = 1f;
        public int Order;
        public bool Glow, Shadow;
    }

    sealed class Light
    {
        public float X, Y, R, G, B, Intensity, Radius;
        public float Inner = 0.12f, Height = FireLightHeight;   // inner radius share, normal map distance (FireLight)
        public bool FireLight, NightOnly;
    }

    // FireLight.UseNormalMaps distance, in tiles.
    const float FireLightHeight = 1f;

    // A small piece of the game's map, laid out like KingdomBootstrap does.
    sealed class MockWorld
    {
        public const int MapSize = 96, Half = MapSize / 2, MaskDetail = 32, TintDetail = 4;
        public readonly uint Seed;
        public readonly List<Inst> Things = new List<Inst>();
        public readonly List<Light> Lights = new List<Light>();
        readonly HashSet<(int, int)> used = new HashSet<(int, int)>();
        // Like WorldGenerator: forest-ness per tile, the patch masks and the ground tint.
        public readonly float[] Forest = new float[MapSize * MapSize];
        public readonly float TreeLine, BushLine;
        public readonly byte[] DryMask = new byte[MapSize * MaskDetail * MapSize * MaskDetail];
        public readonly byte[] MudMask = new byte[MapSize * MaskDetail * MapSize * MaskDetail];
        public readonly Color32[] Tint = new Color32[MapSize * TintDetail * MapSize * TintDetail];

        public MockWorld(uint seed, bool castle)
        {
            Seed = seed;
            for (int y = 0; y < MapSize; y++)
                for (int x = 0; x < MapSize; x++)
                    Forest[y * MapSize + x] = Noise.Fractal(x / 14f, y / 14f, seed + 1u, 3);
            var sorted = (float[])Forest.Clone();
            Array.Sort(sorted);
            TreeLine = sorted[(int)MathF.Round((1f - 0.35f) * (sorted.Length - 1))];
            BushLine = sorted[(int)MathF.Round((1f - 0.35f - 0.1f) * (sorted.Length - 1))];
            GroundArt.PatchMasks(MapSize, MaskDetail, seed, DryMask, MudMask);
            int tn = MapSize * TintDetail;
            for (int y = 0; y < tn; y++)
                for (int x = 0; x < tn; x++)
                {
                    float tx = (x + 0.5f) / TintDetail, ty = (y + 0.5f) / TintDetail;
                    float wood = PixelImage.Smooth(BushLine, TreeLine + 0.04f, ForestAt(tx, ty));
                    float edge = MathF.Min(MathF.Min(tx, MapSize - tx), MathF.Min(ty, MapSize - ty));
                    float rim = 1f - PixelImage.Smooth(0f, 6f, edge);
                    GroundArt.Patches(tx - Half, ty - Half, seed, out float dry, out float mud);
                    Tint[y * tn + x] = GroundArt.Tint(tx, ty, seed, wood, rim, dry, mud);
                }

            if (castle) BuildCastle();
            else BuildStart();
            Nature(seed, castle);
        }

        // The start of a game, laid out like KingdomBootstrap: throne, storehouse, test outpost, ruler and villagers.
        void BuildStart()
        {
            // Throne at (-1, 2), braziers on its lower corners (Throne.Setup).
            Building(ArtKeys.Throne, -1, 2, 2, 0.12f);
            foreach (float side in new[] { -1f, 1f }) Fire(side * 0.8f, 2.2f, 1f, 1f);
            // Storehouse at (-6, -1), its lantern on the porch (Storage.AddLantern).
            Building(ArtKeys.Storehouse, -6, -1, 2, 0.3f);
            Fire(-5f + 2 * 0.18f, 0f - 2 * 0.36f, 0.45f, 0.75f);
            // The test outpost (KingdomBootstrap.BuildOutpost): stone walls north and south, a palisade on the east side, a tower, torches.
            for (int x = 5; x < 14; x++) { Wall("StoneWall", x, 4); Wall("StoneWall", x, -4); }
            for (int y = -3; y < 4; y++) Wall("WoodPalisade", 13, y);
            for (int x = 8; x < 10; x++) for (int y = -1; y < 1; y++) Wall("StoneWall", x, y);
            foreach (var p in new[] { new Vector2(5.5f, 3.5f), new Vector2(12.5f, 3.5f), new Vector2(5.5f, -3f), new Vector2(12.5f, -3f) })
                Fire(p.x, p.y, 0.75f, 0.9f);
            // People.
            Person(ArtKeys.Ruler(1, 1), 0.3f, 0.6f, 25f, CharacterArt.RulerDirections, ArtKeys.RulerShadow(1));
            var hand = Rotate(CharacterArt.RulerHand, 25f);
            Things.Add(new Inst { Key = ArtKeys.Rifle, X = 0.3f + hand.x, Y = 0.6f + hand.y, Rot = 25f, Order = 1001 });
            // The ruler's own light (DayNightCycle.AddNightLight): only at night.
            Lights.Add(new Light { X = 0.3f, Y = 0.6f, R = 1f, G = 0.82f, B = 0.55f, Intensity = 1.1f, Radius = 5.5f, Inner = 0.25f, NightOnly = true });
            Person(ArtKeys.Villager(true, 1, 1), -3.5f, 0.2f, 40f, CharacterArt.VillagerDirections, ArtKeys.VillagerShadow);
            Person(ArtKeys.Villager(false, 4, 2), -1.2f, -1.4f, 175f, CharacterArt.VillagerDirections, ArtKeys.VillagerShadow);
            Person(ArtKeys.Villager(false, 2, 0), 3.4f, 2.6f, 95f, CharacterArt.VillagerDirections, ArtKeys.VillagerShadow);
        }

        // A small castle round the throne, as a player might build it (Kingdom.Build): a great hall
        // with the throne, a barracks and a library behind it, a gate in the south wall, and a
        // few zombies and their dead at the gate at night.
        void BuildCastle()
        {
            Building(ArtKeys.Throne, -1, 2, 2, 0.12f);
            foreach (float side in new[] { -1f, 1f }) Fire(side * 0.8f, 2.2f, 1f, 1f);
            // Walls: the outer ring with a heavy gate, a wall across with two doors, and one between the back rooms.
            for (int x = -6; x <= 6; x++)
            {
                if (x != 0) Wall("StoneWall", x, -3);
                Wall("StoneWall", x, 9);
            }
            for (int y = -2; y <= 8; y++) { Wall("StoneWall", -6, y); Wall("StoneWall", 6, y); }
            for (int x = -5; x <= 5; x++) if (x != -3 && x != 3) Wall("StoneWall", x, 4);
            for (int y = 5; y <= 8; y++) Wall("StoneWall", 0, y);
            DoorAt(true, 0, -3, 0, false);
            DoorAt(false, -3, 4, 0, false);
            DoorAt(false, 3, 4, 0, true);
            // Floors: flagstones in the hall, boards in the back rooms.
            for (int y = -2; y <= 3; y++) for (int x = -5; x <= 5; x++) if (!(y >= 2 && x >= -1 && x <= 0)) Floor(FurnitureArt.StoneFloor, x, y);
            for (int y = 5; y <= 8; y++) for (int x = -5; x <= 5; x++) if (x != 0) Floor(FurnitureArt.WoodFloor, x, y);
            // The great hall: a rug before the throne, banners beside it, feasting tables, a fireplace and torches.
            Piece(FurnitureArt.Rug, -1, -1, 2, 3, 0, 0f, flat: true);
            Piece(FurnitureArt.Banner, -2, 3, 1, 1, 3, 0.9f);
            Piece(FurnitureArt.Banner, 1, 3, 1, 1, 3, 0.9f);
            Piece(FurnitureArt.LongTable, -4, -1, 2, 1, 1, 0.3f);
            Piece(FurnitureArt.Bench, -5, -1, 2, 1, 1, 0.15f);
            Piece(FurnitureArt.Bench, -3, -1, 2, 1, 1, 0.15f);
            Piece(FurnitureArt.LongTable, 3, -1, 2, 1, 1, 0.3f);
            Piece(FurnitureArt.Bench, 2, -1, 2, 1, 1, 0.15f);
            Piece(FurnitureArt.Bench, 4, -1, 2, 1, 1, 0.15f);
            Piece(FurnitureArt.Fireplace, 2, 3, 2, 1, 0, 0.7f, flames: new[] { FurnitureArt.FireplaceFlame }, flameSize: 2.4f, light: 1.2f);
            foreach (var (x, y) in new[] { (-5, 3), (5, 3), (-5, -2), (5, -2) })
                Piece(FurnitureArt.Torch, x, y, 1, 1, 0, 0.55f, flames: new[] { Vector2.zero }, flameSize: 1f, light: 0.75f);
            // The barracks: three beds, a chest and candles.
            for (int x = -5; x <= -3; x++) Piece(FurnitureArt.Bed, x, 7, 1, 2, 0, 0.25f);
            Piece(FurnitureArt.Chest, -1, 8, 1, 1, 2, 0.3f);
            Piece(FurnitureArt.Candelabra, -1, 5, 1, 1, 0, 0.4f, flames: FurnitureArt.CandelabraFlames, flameSize: 0.45f, light: 0.6f);
            // The library: shelves along the wall, a table with chairs, an armor stand.
            Piece(FurnitureArt.Bookshelf, 1, 8, 2, 1, 0, 0.6f);
            Piece(FurnitureArt.Bookshelf, 4, 8, 2, 1, 0, 0.6f);
            Piece(FurnitureArt.Table, 3, 6, 1, 1, 0, 0.3f);
            Piece(FurnitureArt.Chair, 2, 6, 1, 1, 1, 0.25f);
            Piece(FurnitureArt.Chair, 4, 6, 1, 1, 3, 0.25f);
            Piece(FurnitureArt.ArmorStand, 5, 5, 1, 1, 0, 0.7f);
            Piece(FurnitureArt.Candelabra, 1, 5, 1, 1, 0, 0.4f, flames: FurnitureArt.CandelabraFlames, flameSize: 0.45f, light: 0.6f);
            // Torches outside the gate.
            Piece(FurnitureArt.Torch, -2, -4, 1, 1, 0, 0.55f, flames: new[] { Vector2.zero }, flameSize: 1f, light: 0.75f);
            Piece(FurnitureArt.Torch, 2, -4, 1, 1, 0, 0.55f, flames: new[] { Vector2.zero }, flameSize: 1f, light: 0.75f);

            // A ladder and stone stairs up onto the south wall; the ruler up on the wall by the gate,
            // aiming down at the horde (RulerLook.SetRaised: a little bigger, shadow further off).
            Piece(FurnitureArt.Ladder, 2, -2, 1, 1, 2, 0.35f);
            Piece(FurnitureArt.StoneStairs, -3, -2, 1, 1, 2, 0.35f);
            Things.Add(new Inst { Key = ArtKeys.RulerShadow(1), X = 1.5f + 0.55f * 0.36f, Y = -2.5f - 0.65f * 0.36f, Rot = -85f, Order = -100, Shadow = true });
            Things.Add(new Inst { Key = ArtKeys.Ruler(12, 1), X = 1.5f, Y = -2.5f, Rot = -85f, Scale = 1.07f, Order = 1000 });
            var hand = Rotate(CharacterArt.RulerHand * 1.07f, -85f);
            Things.Add(new Inst { Key = ArtKeys.Rifle, X = 1.5f + hand.x, Y = -2.5f + hand.y, Rot = -85f, Scale = 1.07f, Order = 1001 });
            Lights.Add(new Light { X = 1.5f, Y = -2.5f, R = 1f, G = 0.82f, B = 0.55f, Intensity = 1.1f, Radius = 5.5f, Inner = 0.25f, NightOnly = true });
            Person(ArtKeys.Villager(false, 0, 0), 1.6f, 1.6f, 200f, CharacterArt.VillagerDirections, ArtKeys.VillagerShadow);

            // The horde: walkers clawing at the gate and the wall, more coming, a brute and a runner, and the dead.
            Zombie("Walker", 0.45f, -3.27f, 90f, 5);
            Zombie("Walker", -0.55f, -3.25f, 92f, 4);
            Zombie("Walker", 1.3f, -3.27f, 85f, 0);
            Zombie("Brute", -1.7f, -3.36f, 88f, 4);
            Zombie("Walker", 0.8f, -5.5f, 100f, 1);
            Zombie("Walker", -0.4f, -6.1f, 85f, 3);
            Zombie("Runner", 2.6f, -5.9f, 120f, 2);
            Zombie("Walker", -2.7f, -6.6f, 60f, 1);
            Zombie("Walker", 1.9f, -7.4f, 95f, 2);
            Zombie("Walker", -1.2f, -8.3f, 80f, 0);
            Zombie("Walker", 0.4f, -9f, 100f, 3);
            Dead("Walker", 1.6f, -6.6f, 230f);
            Dead("Walker", -1.9f, -7.6f, 300f);
            Dead("Runner", 3.2f, -8.1f, 160f);
            Dead("Walker", -0.2f, -7.3f, 20f);
            foreach (var (x, y, v) in new[] { (1.5f, -6.4f, 0), (-1.8f, -7.4f, 1), (3.1f, -7.9f, 2), (-0.1f, -7.1f, 3), (0.5f, -4.6f, 1), (-2.4f, -5.2f, 2) })
                Things.Add(new Inst { Key = ArtKeys.Blood(v), X = x, Y = y, Rot = v * 77f, Order = -800, Alpha = 0.85f });
            Things.Add(new Inst { Key = ArtKeys.Shadow(ArtKeys.LootBag), X = 2.4f + 0.55f * 0.06f, Y = -6.9f - 0.65f * 0.06f, Order = -100, Shadow = true });
            Things.Add(new Inst { Key = ArtKeys.LootBag, X = 2.4f, Y = -6.9f, Order = 250 });
            for (int y = -12; y <= 12; y++) for (int x = -9; x <= 9; x++) used.Add((x, y));
        }

        // A piece of furniture like Furniture.Setup: its picture for the way it faces, centered on
        // its cells, a shadow unless it lies flat, and its flames (unlit) with one fire light.
        void Piece(string art, int x, int y, int w, int h, int rotation, float height, bool flat = false,
            Vector2[] flames = null, float flameSize = 1f, float light = 0f)
        {
            int turns = 1;
            foreach (var p in FurnitureArt.Pieces) if (p.Art == art) turns = p.Rotations;
            rotation %= turns;
            float cx = x + (rotation % 2 == 0 ? w : h) / 2f, cy = y + (rotation % 2 == 0 ? h : w) / 2f;
            string key = ArtKeys.Turned(art, rotation);
            if (!flat) Things.Add(new Inst { Key = ArtKeys.Shadow(key), X = cx + 0.55f * height, Y = cy - 0.65f * height, Order = -100, Shadow = true });
            Things.Add(new Inst { Key = key, X = cx, Y = cy, Order = flat ? -900 : 150 });
            if (flames == null) return;
            var middle = new Vector2(0, 0);
            foreach (var f in flames)
            {
                var o = Turn(f, rotation);
                Things.Add(new Inst { Key = ArtKeys.Flame, X = cx + o.x, Y = cy + o.y, Scale = flameSize, Order = 151, Glow = true });
                middle = middle + o;
            }
            middle = middle / flames.Length;
            if (light > 0f)
                Lights.Add(new Light { X = cx + middle.x, Y = cy + middle.y, R = FireColor.x, G = FireColor.y, B = FireColor.z,
                    Intensity = 1.3f * MathF.Sqrt(light), Radius = 5f * light, FireLight = true });
        }

        static Vector2 Turn(Vector2 o, int rotation) =>
            rotation == 1 ? new Vector2(-o.y, o.x) : rotation == 2 ? new Vector2(-o.x, -o.y) : rotation == 3 ? new Vector2(o.y, -o.x) : o;

        void Floor(string art, int x, int y)
        {
            int v = (int)(KingdomGame.Destruction.DamageShape.Hash(0xF1u, x, y) % (uint)ArtCatalog.FloorVariants);
            Things.Add(new Inst { Key = ArtKeys.FloorTile(art, v), X = x + 0.5f, Y = y + 0.5f, Order = -1000 });
        }

        void DoorAt(bool heavy, int x, int y, int rotation, bool open)
        {
            string art = heavy ? FurnitureArt.HeavyDoor : FurnitureArt.WoodDoor;
            if (!open) Things.Add(new Inst { Key = ArtKeys.Shadow(ArtKeys.DoorClosed(art, rotation)), X = x + 0.5f + 0.55f * 0.2f, Y = y + 0.5f - 0.65f * 0.2f, Order = -100, Shadow = true });
            Things.Add(new Inst { Key = open ? ArtKeys.DoorOpen(art, rotation) : ArtKeys.DoorClosed(art, rotation), X = x + 0.5f, Y = y + 0.5f, Order = 50 });
        }

        // A zombie like Zombie.Show: the picture for the nearest of its directions, turned the rest of the way.
        void Zombie(string kind, float x, float y, float facing, int frame)
        {
            float step = 360f / CharacterArt.ZombieDirections;
            int d = ((int)MathF.Round(facing / step) % CharacterArt.ZombieDirections + CharacterArt.ZombieDirections) % CharacterArt.ZombieDirections;
            Things.Add(new Inst { Key = ArtKeys.ZombieShadow(kind), X = x + 0.55f * 0.1f, Y = y - 0.65f * 0.1f, Rot = facing, Order = -100, Shadow = true });
            Things.Add(new Inst { Key = ArtKeys.Zombie(kind, d, frame), X = x, Y = y, Rot = facing, Order = 999 });
            // The glowing eyes (Zombie.Setup): unlit, turned with the body.
            float k = CharacterArt.ZombieScale(kind);
            var eyes = Rotate(CharacterArt.ZombieEyesAt(kind, frame) * k, facing);
            Things.Add(new Inst { Key = ArtKeys.ZombieEyes, X = x + eyes.x, Y = y + eyes.y, Rot = facing, Scale = k, Order = 1000, Glow = true, Alpha = 0.85f });
        }

        void Dead(string kind, float x, float y, float degrees)
        {
            float step = 360f / CharacterArt.DeadDirections;
            int d = ((int)MathF.Round(degrees / step) % CharacterArt.DeadDirections + CharacterArt.DeadDirections) % CharacterArt.DeadDirections;
            Things.Add(new Inst { Key = ArtKeys.ZombieDead(kind, d), X = x, Y = y, Rot = degrees, Order = -700 });
        }

        // Nature, placed like WorldGenerator: forests where the noise says, rocks and bushes elsewhere, kept clear around the start.
        void Nature(uint seed, bool castle)
        {
            for (int y = -14; y < 14; y++)
                for (int x = -26; x < 26; x++)
                {
                    if (x * x + y * y < 49 || used.Contains((x, y))) continue;
                    if (!castle && x >= 3 && x <= 15 && y >= -6 && y <= 6) continue;
                    if (castle && x >= -9 && x <= 9 && y >= -10 && y <= 12) continue;
                    float forest = Forest[(y + Half) * MapSize + (x + Half)];
                    float roll = Noise.Hash01(seed + 2u, x, y);
                    int v = (int)(KingdomGame.Destruction.DamageShape.Hash(seed + 5u, x, y) % NatureArt.Variants);
                    float jx = (Noise.Hash01(seed + 3u, x, y) - 0.5f) * 0.6f, jy = (Noise.Hash01(seed + 4u, x, y) - 0.5f) * 0.6f;
                    float px = x + 0.5f + jx, py = y + 0.5f + jy;
                    if (forest > TreeLine && roll < 0.3f)
                    {
                        Things.Add(new Inst { Key = ArtKeys.TreeTrunk(v), X = px, Y = py, Order = 100 });
                        Things.Add(new Inst { Key = ArtKeys.Shadow(ArtKeys.TreeLeaves(v)), X = px + 0.55f * 0.6f, Y = py - 0.65f * 0.6f, Order = -100, Shadow = true });
                        Things.Add(new Inst { Key = ArtKeys.TreeLeaves(v), X = px, Y = py, Order = 5000 });
                    }
                    else if (forest > TreeLine) { }
                    else if (forest > BushLine) { if (roll < 0.05f) Simple(ArtKeys.BushBerries(v), px, py, 0.12f); }
                    else if (roll < 0.012f) Simple(ArtKeys.Rock(v), px, py, 0.18f);
                }
        }

        // Forest-ness between tile centers (map corner coordinates), like WorldGenerator.Forest.
        float ForestAt(float x, float y)
        {
            float gx = Math.Clamp(x - 0.5f, 0f, MapSize - 1.001f), gy = Math.Clamp(y - 0.5f, 0f, MapSize - 1.001f);
            int x0 = (int)gx, y0 = (int)gy;
            float fx = gx - x0, fy = gy - y0;
            int x1 = Math.Min(x0 + 1, MapSize - 1), y1 = Math.Min(y0 + 1, MapSize - 1);
            float a = Forest[y0 * MapSize + x0] + (Forest[y0 * MapSize + x1] - Forest[y0 * MapSize + x0]) * fx;
            float b = Forest[y1 * MapSize + x0] + (Forest[y1 * MapSize + x1] - Forest[y1 * MapSize + x0]) * fx;
            return a + (b - a) * fy;
        }

        // A mask's alpha at a world position, blended between mask pixels like the GPU does.
        public static float MaskAt(byte[] mask, float wx, float wy)
        {
            int n = MapSize * MaskDetail;
            float gx = (wx + Half) * MaskDetail - 0.5f, gy = (wy + Half) * MaskDetail - 0.5f;
            int x0 = Math.Clamp((int)MathF.Floor(gx), 0, n - 2), y0 = Math.Clamp((int)MathF.Floor(gy), 0, n - 2);
            float fx = Math.Clamp(gx - x0, 0f, 1f), fy = Math.Clamp(gy - y0, 0f, 1f);
            float a = mask[y0 * n + x0] + (mask[y0 * n + x0 + 1] - mask[y0 * n + x0]) * fx;
            float b = mask[(y0 + 1) * n + x0] + (mask[(y0 + 1) * n + x0 + 1] - mask[(y0 + 1) * n + x0]) * fx;
            return (a + (b - a) * fy) / 255f;
        }

        // The ground tint at a world position, blended between its pixels: color (0..1) and alpha.
        public (float r, float g, float b, float a) TintAt(float wx, float wy)
        {
            int n = MapSize * TintDetail;
            float gx = (wx + Half) * TintDetail - 0.5f, gy = (wy + Half) * TintDetail - 0.5f;
            int x0 = Math.Clamp((int)MathF.Floor(gx), 0, n - 2), y0 = Math.Clamp((int)MathF.Floor(gy), 0, n - 2);
            float fx = Math.Clamp(gx - x0, 0f, 1f), fy = Math.Clamp(gy - y0, 0f, 1f);
            float r = 0, g = 0, b = 0, a = 0;
            for (int k = 0; k < 4; k++)
            {
                var c = Tint[(y0 + (k >> 1)) * n + x0 + (k & 1)];
                float wgt = ((k & 1) != 0 ? fx : 1 - fx) * ((k >> 1) != 0 ? fy : 1 - fy);
                r += c.r / 255f * wgt; g += c.g / 255f * wgt; b += c.b / 255f * wgt; a += c.a / 255f * wgt;
            }
            return (r, g, b, a);
        }

        // A brazier, torch or lantern (Brazier.Setup): the glowing coals and a FireLight.
        void Fire(float x, float y, float size, float lightSize)
        {
            Things.Add(new Inst { Key = ArtKeys.Brazier, X = x, Y = y, Scale = size, Order = 201, Glow = true });
            Lights.Add(new Light { X = x, Y = y, R = FireColor.x, G = FireColor.y, B = FireColor.z, Intensity = 1.3f * MathF.Sqrt(lightSize), Radius = 5f * lightSize, FireLight = true });
        }

        void Building(string key, int x, int y, int size, float height)
        {
            float cx = x + size / 2f, cy = y + size / 2f;
            Things.Add(new Inst { Key = ArtKeys.Shadow(key), X = cx + 0.55f * height, Y = cy - 0.65f * height, Order = -100, Shadow = true });
            Things.Add(new Inst { Key = key, X = cx, Y = cy, Order = 200 });
            for (int i = x - 1; i <= x + size; i++) for (int j = y - 1; j <= y + size; j++) used.Add((i, j));
        }

        void Wall(string material, int x, int y)
        {
            int v = (int)(KingdomGame.Destruction.DamageShape.Hash(0x51u, x, y) % 4u);
            Things.Add(new Inst { Key = ArtKeys.Wall(material, v), X = x + 0.55f * 0.22f, Y = y - 0.65f * 0.22f, Order = -100, Shadow = true });
            Things.Add(new Inst { Key = ArtKeys.Wall(material, v), X = x, Y = y, Order = 0 });
        }

        void Simple(string key, float x, float y, float height)
        {
            Things.Add(new Inst { Key = ArtKeys.Shadow(key), X = x + 0.55f * height, Y = y - 0.65f * height, Order = -100, Shadow = true });
            Things.Add(new Inst { Key = key, X = x, Y = y, Order = 100 });
        }

        void Person(string key, float x, float y, float facing, int directions, string shadow)
        {
            // Like the game (RulerLook): the picture drawn for the nearest direction (each one
            // faces right, lit for that direction), turned by the whole facing angle.
            float step = 360f / directions;
            int d = ((int)MathF.Round(facing / step) % directions + directions) % directions;
            string frame = key.Substring(key.LastIndexOf('/') + 1);
            string parts = key.Substring(0, key.LastIndexOf('/'));
            string withDirection = parts.Substring(0, parts.LastIndexOf('/') + 1) + d + "/" + frame;
            Things.Add(new Inst { Key = shadow, X = x + 0.55f * 0.12f, Y = y - 0.65f * 0.12f, Rot = facing, Order = -100, Shadow = true });
            Things.Add(new Inst { Key = withDirection, X = x, Y = y, Rot = facing, Order = 1000 });
        }
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float a = degrees * MathF.PI / 180f, c = MathF.Cos(a), s = MathF.Sin(a);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // ------------------------------------------------------------ render --

    // The project renders in linear color (Unity's "Linear" color space): textures are
    // turned from sRGB into linear light values, lit and blended there, and turned back
    // for the screen at the end. Colors handed to lights are converted the same way.
    static float Lin(float c) => c <= 0f ? 0f : MathF.Pow(c, 2.2f);
    static float Srgb(float c) => c <= 0f ? 0f : MathF.Pow(c, 1f / 2.2f);
    static Vector3 Lin(Vector3 c) => new Vector3(Lin(c.x), Lin(c.y), Lin(c.z));

    static void Render(MockWorld world, string path, int w, int h, Vector2 center, float zoom, Time time)
    {
        float ppt = h / (2f * zoom);                       // screen pixels per tile
        var color = new float[w * h * 3];                  // linear
        bool night = time == Time.Night;
        Night = night;
        // Global light (DayNightConfig): a moody, slightly warm afternoon, or a blue night.
        // DayNightCycle hands URP these "as seen on screen" values converted to linear light.
        var sky = night ? NightColor * NightBrightness : DayColor * DayBrightness;
        var ambient = Lin(sky);
        float fire = night ? 1f : 0.35f;                     // fires are always lit, but stand out at night (FireLight.DaytimeShare)

        // Ground: grass everywhere, dry grass and mud in patches (like the sprite masks), then the soft tint.
        var grass = Bank[ArtKeys.GroundGrass]; var dry = Bank[ArtKeys.GroundDry]; var mud = Bank[ArtKeys.GroundMud];
        for (int sy = 0; sy < h; sy++)
            for (int sx = 0; sx < w; sx++)
            {
                float wx = center.x + (sx + 0.5f - w / 2f) / ppt, wy = center.y + (h / 2f - sy - 0.5f) / ppt;
                // The sprite masks: a patch layer shows where its mask is above the cut-off (0.5).
                bool mudHere = MockWorld.MaskAt(world.MudMask, wx, wy) > 0.5f;
                bool dryHere = MockWorld.MaskAt(world.DryMask, wx, wy) > 0.5f;
                var e = mudHere ? mud : dryHere ? dry : grass;
                float u = Mod(wx + MockWorld.Half, GroundArt.Tiles) * e.Ppt, v = Mod(wy + MockWorld.Half, GroundArt.Tiles) * e.Ppt;
                float level = MathF.Log2(MathF.Max(1f, e.Ppt / ppt));
                var c = Lin(Sample(e.Sheet.Albedo, u, v, level, true));
                var n = Normal(e.Sheet, u, v, level, true, 0f);
                var lit = LightAt(world, wx, wy, n, ambient, fire);
                // The soft tint is a lit, see-through sprite over the ground.
                var tint = world.TintAt(wx, wy);
                float r = c.x + (Lin(tint.r) - c.x) * tint.a, g = c.y + (Lin(tint.g) - c.y) * tint.a, b = c.z + (Lin(tint.b) - c.z) * tint.a;
                int i = (sy * w + sx) * 3;
                color[i] = r * lit.x; color[i + 1] = g * lit.y; color[i + 2] = b * lit.z;
            }

        var things = new List<Inst>(world.Things);
        things.Sort((a, b) => a.Order.CompareTo(b.Order));
        foreach (var t in things) Draw(world, t, color, w, h, center, ppt, ambient, fire);

        PostProcess(color, w, h);
        Save(path, color, w, h);
        Console.WriteLine($"wrote {path}");
    }

    static bool Night;

    // The DayNightConfig defaults.
    static readonly Vector3 DayColor = new Vector3(0.96f, 0.91f, 0.8f), NightColor = new Vector3(0.52f, 0.64f, 0.95f);
    const float DayBrightness = 0.8f, NightBrightness = 0.3f;
    static readonly Vector3 FireColor = new Vector3(1f, 0.68f, 0.38f);
    // The PostProcessing profile (GameSetup).
    const float Contrast = 1.05f, Saturation = 0.75f;

    static void Draw(MockWorld world, Inst t, float[] color, int w, int h, Vector2 center, float ppt, Vector3 ambient, float fire)
    {
        if (!Bank.TryGetValue(t.Key, out var e)) { Console.WriteLine($"missing {t.Key}"); return; }
        float sizeX = e.W / e.Ppt * t.Scale, sizeY = e.H / e.Ppt * t.Scale;
        float reach = MathF.Sqrt(sizeX * sizeX + sizeY * sizeY);
        int minX = (int)((t.X - reach - center.x) * ppt + w / 2f) - 1, maxX = (int)((t.X + reach - center.x) * ppt + w / 2f) + 1;
        int minY = (int)(h / 2f - (t.Y + reach - center.y) * ppt) - 1, maxY = (int)(h / 2f - (t.Y - reach - center.y) * ppt) + 1;
        float a = -t.Rot * MathF.PI / 180f, ca = MathF.Cos(a), sa = MathF.Sin(a);
        float level = MathF.Log2(MathF.Max(1f, e.Ppt / (ppt * t.Scale)));
        for (int sy = Math.Max(0, minY); sy <= Math.Min(h - 1, maxY); sy++)
            for (int sx = Math.Max(0, minX); sx <= Math.Min(w - 1, maxX); sx++)
            {
                float wx = center.x + (sx + 0.5f - w / 2f) / ppt, wy = center.y + (h / 2f - sy - 0.5f) / ppt;
                float lx = (wx - t.X) * ca - (wy - t.Y) * sa, ly = (wx - t.X) * sa + (wy - t.Y) * ca;
                float u = lx / t.Scale * e.Ppt + e.Pivot.x * e.W, v = ly / t.Scale * e.Ppt + e.Pivot.y * e.H;
                if (u < 0 || v < 0 || u >= e.W || v >= e.H) continue;
                var c = Lin(SampleA(e.Sheet.Albedo, e.X + u, e.Y + v, level, out float alpha));
                alpha *= t.Alpha;
                if (alpha < 0.003f) continue;
                int i = (sy * w + sx) * 3;
                Vector3 outColor;
                if (t.Shadow) { outColor = new Vector3(0, 0, 0); alpha *= 0.45f; }   // ViewConfig.shadowOpacity
                else if (t.Glow) outColor = c;                                        // unlit (the glow material)
                else
                {
                    var n = Normal(e.Sheet, e.X + u, e.Y + v, level, false, t.Rot);
                    var lit = LightAt(world, wx, wy, n, ambient, fire);
                    outColor = new Vector3(c.x * lit.x * t.R, c.y * lit.y * t.G, c.z * lit.z * t.B);
                }
                color[i] += (outColor.x - color[i]) * alpha;
                color[i + 1] += (outColor.y - color[i + 1]) * alpha;
                color[i + 2] += (outColor.z - color[i + 2]) * alpha;
            }
    }

    // URP 2D lights, in linear color: the global light plus point lights. A point light
    // fades from its inner to its outer radius along URP's falloff curve (falloff
    // strength 0.5: the distance fraction, read from an sRGB lookup texture, so about
    // its 2.2 power), and with normal maps it's times the cosine between the pixel's
    // normal and the direction to the light, which hangs "normal map distance" above.
    static Vector3 LightAt(MockWorld world, float x, float y, Vector3 n, Vector3 ambient, float fire)
    {
        var sum = ambient;
        foreach (var l in world.Lights)
        {
            float dx = l.X - x, dy = l.Y - y;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d >= l.Radius) continue;
            float inner = l.Radius * l.Inner;
            float falloff = Lin(Math.Clamp((l.Radius - d) / (l.Radius - inner), 0f, 1f));
            float height = l.Height;
            float len = MathF.Sqrt(dx * dx + dy * dy + height * height);
            float ndl = MathF.Max(0f, (n.x * dx + n.y * dy + n.z * height) / len);
            if (l.NightOnly && !Night) continue;
            float k = l.Intensity * (l.FireLight ? fire * 0.92f : 1f);   // 0.92: fires flicker around that
            var c = Lin(new Vector3(l.R * k, l.G * k, l.B * k));
            sum = sum + c * (falloff * ndl);
        }
        return sum;
    }

    // ----------------------------------------------------------- sampling --

    static Vector3 Sample(List<PixelImage> mips, float u, float v, float level, bool wrap) => SampleRaw(mips, u, v, level, wrap, out _);

    static Vector3 SampleA(List<PixelImage> mips, float u, float v, float level, out float alpha) => SampleRaw(mips, u, v, level, false, out alpha);

    static Vector3 Normal(Sheet sheet, float u, float v, float level, bool wrap, float rotation)
    {
        if (sheet.Normals == null) return new Vector3(0, 0, 1);
        var c = SampleRaw(sheet.Normals, u, v, level, wrap, out _);
        var n = new Vector3(c.x * 2f - 1f, c.y * 2f - 1f, c.z * 2f - 1f).normalized;
        var r = Rotate(new Vector2(n.x, n.y), rotation);
        return new Vector3(r.x, r.y, n.z);
    }

    // Bilinear sample at a mip level (no blending between levels), weighted by alpha.
    static Vector3 SampleRaw(List<PixelImage> mips, float u, float v, float level, bool wrap, out float alpha)
    {
        int first = 0;
        while (first < mips.Count - 1 && mips[first] == null) first++;
        int l = Math.Clamp((int)MathF.Round(level), first, mips.Count - 1);
        var img = mips[l];
        float scale = 1f / (1 << l);
        float x = u * scale - 0.5f, y = v * scale - 0.5f;
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float fx = x - x0, fy = y - y0;
        float r = 0, g = 0, b = 0, a = 0;
        for (int k = 0; k < 4; k++)
        {
            int sx = x0 + (k & 1), sy = y0 + (k >> 1);
            if (wrap) { sx = ((sx % img.Width) + img.Width) % img.Width; sy = ((sy % img.Height) + img.Height) % img.Height; }
            else { sx = Math.Clamp(sx, 0, img.Width - 1); sy = Math.Clamp(sy, 0, img.Height - 1); }
            float wgt = ((k & 1) != 0 ? fx : 1 - fx) * ((k >> 1) != 0 ? fy : 1 - fy);
            var c = img.Pixels[sy * img.Width + sx];
            float ca = c.a / 255f * wgt;
            r += c.r * ca; g += c.g * ca; b += c.b * ca; a += ca;
        }
        alpha = a;
        if (a <= 1e-5f) return new Vector3(0, 0, 0);
        return new Vector3(r / a / 255f, g / a / 255f, b / a / 255f);
    }

    // The game's ground masks: the patch values on a grid of 8 per tile, smoothly stretched.
    static float Mod(float a, float m) => ((a % m) + m) % m;
    static float SmoothStep(float e0, float e1, float x) { float t = Math.Clamp((x - e0) / (e1 - e0), 0f, 1f); return t * t * (3 - 2 * t); }

    // ------------------------------------------------------- post-process --

    // Like Assets/Data/Look/PostProcessing, roughly in URP's order: bloom (threshold 0.85
    // given in gamma, soft knee), vignette and exposure in linear color, then contrast,
    // saturation and film grain on the screen colors.
    static void PostProcess(float[] c, int w, int h)
    {
        float threshold = Lin(0.85f), knee = threshold * 0.5f;
        var bright = new float[c.Length];
        for (int i = 0; i < c.Length; i += 3)
        {
            float brightness = MathF.Max(c[i], MathF.Max(c[i + 1], c[i + 2]));
            float soft = Math.Clamp(brightness - threshold + knee, 0f, 2f * knee);
            soft = soft * soft / (4f * knee + 1e-4f);
            float k = MathF.Max(brightness - threshold, soft) / MathF.Max(brightness, 1e-4f);
            bright[i] = c[i] * k; bright[i + 1] = c[i + 1] * k; bright[i + 2] = c[i + 2] * k;
        }
        int radius = Math.Max(4, h / 60);
        for (int pass = 0; pass < 3; pass++) bright = BoxBlur(bright, w, h, radius);
        var rnd = new Random(7);
        float exposure = MathF.Pow(2f, -0.1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 3;
                float r = c[i] + bright[i] * 0.9f, g = c[i + 1] + bright[i + 1] * 0.9f, b = c[i + 2] + bright[i + 2] * 0.9f;
                // URP's vignette (intensity 0.3, smoothness 0.4): pow(saturate(1 - |d|^2), smoothness * 5), d = |uv - 0.5| * intensity * 3.
                float dx = MathF.Abs(x / (float)w - 0.5f) * 0.9f, dy = MathF.Abs(y / (float)h - 0.5f) * 0.9f;
                float vig = MathF.Pow(Math.Clamp(1f - (dx * dx + dy * dy), 0f, 1f), 2f);
                r *= vig * exposure; g *= vig * exposure; b *= vig * exposure;
                // URP's color grading (LutBuilderLdr): contrast in LogC around mid gray, then saturation, in linear.
                const float midGray = 0.4135884f;
                r = MathF.Max(0f, LogCToLinear((LinearToLogC(r) - midGray) * Contrast + midGray));
                g = MathF.Max(0f, LogCToLinear((LinearToLogC(g) - midGray) * Contrast + midGray));
                b = MathF.Max(0f, LogCToLinear((LinearToLogC(b) - midGray) * Contrast + midGray));
                float luma = r * 0.2126729f + g * 0.7151522f + b * 0.0721750f;
                r = luma + (r - luma) * Saturation; g = luma + (g - luma) * Saturation; b = luma + (b - luma) * Saturation;
                float grain = ((float)rnd.NextDouble() - 0.5f) * 0.02f;
                c[i] = Srgb(r) + grain; c[i + 1] = Srgb(g) + grain; c[i + 2] = Srgb(b) + grain;
            }
    }

    // ARRI LogC (as in URP's Color.hlsl), which URP's contrast works in.
    static float LinearToLogC(float x)
    {
        const float cut = 0.011361f, a = 5.555556f, b = 0.047996f, c = 0.244161f, d = 0.386036f, e = 5.301883f, f = 0.092819f;
        return x > cut ? c * MathF.Log10(a * x + b) + d : e * x + f;
    }

    static float LogCToLinear(float x)
    {
        const float a = 5.555556f, b = 0.047996f, c = 0.244161f, d = 0.386036f, e = 5.301883f, f = 0.092819f;
        return x > e * 0.011361f + f ? (MathF.Pow(10f, (x - d) / c) - b) / a : (x - f) / e;
    }

    static float[] BoxBlur(float[] src, int w, int h, int r)
    {
        var tmp = new float[src.Length];
        var dst = new float[src.Length];
        for (int y = 0; y < h; y++)
            for (int ch = 0; ch < 3; ch++)
            {
                float sum = 0;
                for (int k = -r; k <= r; k++) sum += src[(y * w + Math.Clamp(k, 0, w - 1)) * 3 + ch];
                for (int x = 0; x < w; x++)
                {
                    tmp[(y * w + x) * 3 + ch] = sum / (2 * r + 1);
                    sum += src[(y * w + Math.Min(w - 1, x + r + 1)) * 3 + ch] - src[(y * w + Math.Max(0, x - r)) * 3 + ch];
                }
            }
        for (int x = 0; x < w; x++)
            for (int ch = 0; ch < 3; ch++)
            {
                float sum = 0;
                for (int k = -r; k <= r; k++) sum += tmp[(Math.Clamp(k, 0, h - 1) * w + x) * 3 + ch];
                for (int y = 0; y < h; y++)
                {
                    dst[(y * w + x) * 3 + ch] = sum / (2 * r + 1);
                    sum += tmp[(Math.Min(h - 1, y + r + 1) * w + x) * 3 + ch] - tmp[(Math.Max(0, y - r) * w + x) * 3 + ch];
                }
            }
        return dst;
    }

    // --------------------------------------------------------------- png --

    static void Save(string path, float[] c, int w, int h)
    {
        var rgba = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            rgba[i * 4] = (byte)Math.Clamp((int)(c[i * 3] * 255f), 0, 255);
            rgba[i * 4 + 1] = (byte)Math.Clamp((int)(c[i * 3 + 1] * 255f), 0, 255);
            rgba[i * 4 + 2] = (byte)Math.Clamp((int)(c[i * 3 + 2] * 255f), 0, 255);
            rgba[i * 4 + 3] = 255;
        }
        WritePng(path, w, h, rgba);
    }

    // A PixelImage (bottom row first) as a PNG (top row first).
    static void SaveImage(PixelImage img, string path)
    {
        var rgba = new byte[img.Width * img.Height * 4];
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
            {
                var c = img.Pixels[(img.Height - 1 - y) * img.Width + x];
                int i = (y * img.Width + x) * 4;
                rgba[i] = c.r; rgba[i + 1] = c.g; rgba[i + 2] = c.b; rgba[i + 3] = c.a;
            }
        WritePng(path, img.Width, img.Height, rgba);
    }

    static void WritePng(string path, int w, int h, byte[] rgba)
    {
        var raw = new MemoryStream();
        for (int y = 0; y < h; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgba, y * w * 4, w * 4);
        }
        var comp = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionLevel.Fastest, true)) { raw.Position = 0; raw.CopyTo(z); }
        using var f = File.Create(path);
        f.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Chunk(f, "IHDR", BE(w), BE(h), new byte[] { 8, 6, 0, 0, 0 });
        Chunk(f, "IDAT", comp.ToArray());
        Chunk(f, "IEND");
    }
    static byte[] BE(int v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
    static void Chunk(Stream s, string type, params byte[][] parts)
    {
        var data = new MemoryStream(); foreach (var p in parts) data.Write(p);
        var d = data.ToArray(); var t = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(BE(d.Length)); s.Write(t); s.Write(d);
        s.Write(BE((int)Crc(t, d)));
    }
    static uint Crc(byte[] t, byte[] d)
    {
        uint c = 0xFFFFFFFF;
        foreach (var b in t) c = Step(c, b); foreach (var b in d) c = Step(c, b);
        return c ^ 0xFFFFFFFF;
        static uint Step(uint c, byte b) { c ^= b; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; return c; }
    }
}
