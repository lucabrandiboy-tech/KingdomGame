using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>One picture of the game's art and how to draw it.</summary>
    public sealed class ArtItem
    {
        /// <summary>The name the game asks for, like "Tree/Leaves/2" (see <see cref="ArtKeys"/>).</summary>
        public string Key;
        /// <summary>Which texture sheet it's packed into. Pictures used together share a sheet so they draw in one batch.</summary>
        public string Sheet;
        /// <summary>Draws it at a given number of pixels per tile.</summary>
        public Func<int, PixelImage> Draw;
        /// <summary>Also bake a normal map from its heights, so lights show its bumps.</summary>
        public bool NormalMap;
        /// <summary>A seamless, repeating picture that gets a texture of its own (the ground).</summary>
        public bool Repeats;
        /// <summary>If set, this picture is the soft shadow of that other item (drawn smaller and blurred).</summary>
        public string ShadowOf;
        /// <summary>For shadows: how many times smaller than the item it belongs to.</summary>
        public int Shrink = 1;
        /// <summary>For shadows: how blurry, in tiles.</summary>
        public float BlurTiles;
        /// <summary>
        /// Drawn at this share of the art detail (1 = full). Furniture, floors and creatures
        /// never show big enough on screen to need full detail, and there are many of them,
        /// so they're drawn at half to save video memory. Shadows get their item's.
        /// </summary>
        public float Detail = 1f;
    }

    /// <summary>What a wall material looks like, for drawing its tiles.</summary>
    public struct WallLook
    {
        public string Name;
        public bool Logs;
        public Color32 Gap;
        public Color32[] Colors;
        public int Variants;
    }

    /// <summary>The names of the pictures the game uses.</summary>
    public static class ArtKeys
    {
        public static string TreeLeaves(int v) => $"Tree/Leaves/{v}";
        public static string TreeTrunk(int v) => $"Tree/Trunk/{v}";
        public static string TreeStump(int v) => $"Tree/Stump/{v}";
        public static string Rock(int v) => $"Rock/{v}";
        public static string BushBerries(int v) => $"Bush/Berries/{v}";
        public static string BushBare(int v) => $"Bush/Bare/{v}";
        public static string Shadow(string key) => key + "/Shadow";
        public const string Throne = "Building/Throne";
        public const string Storehouse = "Building/Storehouse";
        public const string Brazier = "Building/Brazier";
        public static string Ruler(int direction, int frame) => $"Ruler/{direction}/{frame}";
        public static string RulerShadow(int frame) => $"Ruler/Shadow/{frame}";
        public const string Rifle = "Ruler/Rifle";
        /// <summary>One of the ruler's era weapons (names in WeaponArt).</summary>
        public static string Weapon(string name) => $"Ruler/Weapon/{name}";
        public static string Villager(bool carrying, int direction, int frame) => $"Villager/{(carrying ? "Carrying" : "Walking")}/{direction}/{frame}";
        public const string VillagerShadow = "Villager/Shadow";
        public static string Wall(string material, int variant) => $"Wall/{material}/{variant}";
        public const string GroundGrass = "Ground/Grass";
        public const string GroundDry = "Ground/Dry";
        public const string GroundMud = "Ground/Mud";

        /// <summary>A piece of furniture (or a chest) facing direction <paramref name="rotation"/> (0..3).</summary>
        public static string Turned(string art, int rotation) => $"{art}/{rotation}";
        /// <summary>One of a floor's pictures (<see cref="ArtCatalog.FloorVariants"/> of each).</summary>
        public static string FloorTile(string art, int variant) => $"{art}/{variant}";
        public static string DoorClosed(string art, int rotation) => $"{art}/Closed/{rotation}";
        public static string DoorOpen(string art, int rotation) => $"{art}/Open/{rotation}";
        /// <summary>A trap once it's gone off (a pit opened up, jaws shut, a crater).</summary>
        public static string Sprung(string art) => $"{art}/Sprung";
        /// <summary>A tower's turning head, drawn for direction <paramref name="direction"/> of <see cref="FurnitureArt.HeadDirections"/>.</summary>
        public static string Head(string art, int direction) => $"{art}/Head/{direction}";
        /// <summary>A small bright flame (torches, candles, fireplaces).</summary>
        public const string Flame = "Effect/Flame";
        public static string Blood(int variant) => $"Effect/Blood/{variant}";
        public const string LootBag = "Effect/LootBag";
        /// <summary>The fallen, lying facing one of <see cref="CharacterArt.DeadDirections"/> directions.</summary>
        public static string VillagerDead(int direction) => $"Villager/Dead/{direction}";
        public static string RulerDead(int direction) => $"Ruler/Dead/{direction}";
        public static string Zombie(string kind, int direction, int frame) => $"Zombie/{kind}/{direction}/{frame}";
        public static string ZombieDead(string kind, int direction) => $"Zombie/{kind}/Dead/{direction}";
        public static string ZombieShadow(string kind) => $"Zombie/{kind}/Shadow";
        /// <summary>Faintly glowing eyes, drawn over any zombie.</summary>
        public const string ZombieEyes = "Zombie/Eyes";
    }

    /// <summary>
    /// Every picture the game bakes (KingdomGame > Build Game Scenes draws them at
    /// the View Config's Art Detail, <see cref="DefaultPixelsPerTile"/> unless changed,
    /// and saves them as textures), and how to draw each one. The game uses the same
    /// list to draw a picture itself, more coarsely, if the baked one is missing.
    /// </summary>
    public static class ArtCatalog
    {
        /// <summary>Detail of the baked art when nothing else is set: pixels per tile.</summary>
        public const int DefaultPixelsPerTile = 1024;
        /// <summary>Bump this whenever any art changes, so Build Game Scenes bakes again.</summary>
        public const int Version = 15;
        /// <summary>How many different pictures each kind of floor has.</summary>
        public const int FloorVariants = 4;
        /// <summary>Blood splatter pictures.</summary>
        public const int BloodVariants = 4;

        public static List<ArtItem> Items(IList<WallLook> walls)
        {
            var items = new List<ArtItem>();

            for (int v = 0; v < NatureArt.Variants; v++)
            {
                int variant = v;
                Add(items, ArtKeys.TreeLeaves(v), "Nature", p => NatureArt.Canopy(variant, p), true);
                Add(items, ArtKeys.TreeTrunk(v), "Nature", p => NatureArt.Trunk(variant, p), true);
                Add(items, ArtKeys.TreeStump(v), "Nature", p => NatureArt.Stump(variant, p), true);
                Add(items, ArtKeys.Rock(v), "Nature", p => NatureArt.Rock(variant, p), true);
                Add(items, ArtKeys.BushBerries(v), "Nature", p => NatureArt.Bush(variant, p, true), true);
                Add(items, ArtKeys.BushBare(v), "Nature", p => NatureArt.Bush(variant, p, false), true);
                Shadow(items, ArtKeys.TreeLeaves(v), "NatureShadows", 0.06f);
                Shadow(items, ArtKeys.TreeStump(v), "NatureShadows", 0.015f);
                Shadow(items, ArtKeys.Rock(v), "NatureShadows", 0.025f);
                Shadow(items, ArtKeys.BushBerries(v), "NatureShadows", 0.02f);
            }

            Add(items, ArtKeys.Throne, "Buildings", BuildingArt.Throne, true);
            Add(items, ArtKeys.Storehouse, "Buildings", BuildingArt.Storehouse, true);
            Add(items, ArtKeys.Brazier, "Buildings", BuildingArt.Brazier, true);
            Shadow(items, ArtKeys.Throne, "BuildingShadows", 0.03f);
            Shadow(items, ArtKeys.Storehouse, "BuildingShadows", 0.04f);

            for (int d = 0; d < CharacterArt.RulerDirections; d++)
                for (int f = 0; f < CharacterArt.WalkFrames; f++)
                {
                    int frame = f;
                    float facing = d * 360f / CharacterArt.RulerDirections;
                    Add(items, ArtKeys.Ruler(d, f), "Ruler", p => CharacterArt.RulerFrame(p, facing, frame), false);
                }
            for (int f = 0; f < CharacterArt.WalkFrames; f++)
                Shadow(items, ArtKeys.Ruler(0, f), "CharacterShadows", 0.02f, ArtKeys.RulerShadow(f));
            Add(items, ArtKeys.Rifle, "Ruler", CharacterArt.Rifle, false);
            foreach (var weapon in WeaponArt.All)
            {
                string w = weapon;
                Add(items, ArtKeys.Weapon(w), "Ruler", p => WeaponArt.Draw(w, p), false);
            }

            foreach (bool carrying in new[] { false, true })
                for (int d = 0; d < CharacterArt.VillagerDirections; d++)
                    for (int f = 0; f < CharacterArt.WalkFrames; f++)
                    {
                        bool c = carrying;
                        int frame = f;
                        float facing = d * 360f / CharacterArt.VillagerDirections;
                        Add(items, ArtKeys.Villager(carrying, d, f), "Villagers", p => CharacterArt.VillagerFrame(p, c, facing, frame), false);
                    }
            Shadow(items, ArtKeys.Villager(false, 0, 0), "CharacterShadows", 0.02f, ArtKeys.VillagerShadow);

            // Castle furniture (a picture for each way it can face), floors, doors, small effects,
            // the zombies and the fallen: at half detail (see ArtItem.Detail).
            const float half = 0.5f;
            foreach (var piece in FurnitureArt.Pieces)
                for (int r = 0; r < piece.Rotations; r++)
                {
                    var draw = piece.Draw;
                    int rotation = r;
                    string key = ArtKeys.Turned(piece.Art, r);
                    Add(items, key, "Furniture", p => draw(p, rotation), true, half);
                    if (!piece.Flat) Shadow(items, key, "FurnitureShadows", 0.025f);
                }
            foreach (bool stone in new[] { false, true })
                for (int v = 0; v < FloorVariants; v++)
                {
                    int variant = v;
                    Add(items, ArtKeys.FloorTile(stone ? FurnitureArt.StoneFloor : FurnitureArt.WoodFloor, v), "Floors",
                        p => stone ? FurnitureArt.StoneFloorTile(p, variant) : FurnitureArt.WoodFloorTile(p, variant), true, half);
                }
            foreach (bool heavy in new[] { false, true })
                for (int r = 0; r < 2; r++)
                {
                    string art = heavy ? FurnitureArt.HeavyDoor : FurnitureArt.WoodDoor;
                    int rotation = r;
                    Add(items, ArtKeys.DoorClosed(art, r), "Doors", p => FurnitureArt.Door(p, heavy, false, rotation), true, half);
                    Add(items, ArtKeys.DoorOpen(art, r), "Doors", p => FurnitureArt.Door(p, heavy, true, rotation), true, half);
                    Shadow(items, ArtKeys.DoorClosed(art, r), "FurnitureShadows", 0.02f);
                }
            // Big gates: 3 tiles (wood) and 4 tiles (castle) across.
            foreach (var (art, width, castle) in new[] { (FurnitureArt.WoodGate, 3, false), (FurnitureArt.CastleGate, 4, true), (FurnitureArt.LeverGate, 3, true) })
                for (int r = 0; r < 2; r++)
                {
                    int rotation = r;
                    Add(items, ArtKeys.DoorClosed(art, r), "Doors", p => FurnitureArt.Gate(p, width, castle, false, rotation), true, half);
                    Add(items, ArtKeys.DoorOpen(art, r), "Doors", p => FurnitureArt.Gate(p, width, castle, true, rotation), true, half);
                    Shadow(items, ArtKeys.DoorClosed(art, r), "FurnitureShadows", 0.02f);
                }
            // Each age's newer things (crafting tables, machines, furniture, towers): there are many, so a quarter of the detail.
            const float quarter = 0.25f;
            foreach (var d in FurnitureArt.AgePieces())
            {
                var piece = d;
                string key = ArtKeys.Turned(piece.Art, 0);
                Add(items, key, "AgeContent", p => piece.Draw(p), true, quarter);
                if (!piece.Flat) Shadow(items, key, "FurnitureShadows", 0.03f);
                if (piece.Head == null) continue;
                for (int h = 0; h < FurnitureArt.HeadDirections; h++)
                {
                    float degrees = h * 360f / FurnitureArt.HeadDirections;
                    Add(items, ArtKeys.Head(piece.Art, h), "AgeHeads", p => piece.Head(p, degrees), true, 0.18f);
                }
            }
            // Traps and towers (their turning heads drawn for each direction).
            foreach (var d in FurnitureArt.Defenses)
            {
                var piece = d;
                string key = ArtKeys.Turned(piece.Art, 0);
                Add(items, key, "Defenses", p => piece.Draw(p), true, half);
                if (!piece.Flat) Shadow(items, key, "FurnitureShadows", 0.03f);
                if (piece.Sprung != null) Add(items, ArtKeys.Sprung(piece.Art), "Defenses", p => piece.Sprung(p), true, half);
                if (piece.Head == null) continue;
                for (int h = 0; h < FurnitureArt.HeadDirections; h++)
                {
                    float degrees = h * 360f / FurnitureArt.HeadDirections;
                    Add(items, ArtKeys.Head(piece.Art, h), "DefenseHeads", p => piece.Head(p, degrees), true, half);
                }
            }
            Add(items, ArtKeys.Flame, "Effects", FurnitureArt.Flame, false, half);
            for (int v = 0; v < BloodVariants; v++)
            {
                int variant = v;
                Add(items, ArtKeys.Blood(v), "Effects", p => FurnitureArt.Blood(p, variant), false, half);
            }
            Add(items, ArtKeys.LootBag, "Effects", FurnitureArt.LootBag, false, half);
            Shadow(items, ArtKeys.LootBag, "FurnitureShadows", 0.015f);

            foreach (string kind in CharacterArt.ZombieKinds)
            {
                for (int d = 0; d < CharacterArt.ZombieDirections; d++)
                    for (int f = 0; f < CharacterArt.ZombieFrames; f++)
                    {
                        int frame = f;
                        float facing = d * 360f / CharacterArt.ZombieDirections;
                        Add(items, ArtKeys.Zombie(kind, d, f), "Zombies", p => CharacterArt.ZombieFrame(p, kind, facing, frame), false, half);
                    }
                Shadow(items, ArtKeys.Zombie(kind, 0, 0), "CharacterShadows", 0.02f, ArtKeys.ZombieShadow(kind));
            }
            Add(items, ArtKeys.ZombieEyes, "Effects", CharacterArt.ZombieEyes, false, half);
            for (int d = 0; d < CharacterArt.DeadDirections; d++)
            {
                float facing = d * 360f / CharacterArt.DeadDirections;
                foreach (string kind in CharacterArt.ZombieKinds)
                    Add(items, ArtKeys.ZombieDead(kind, d), "Fallen", p => CharacterArt.ZombieDead(p, kind, facing), false, half);
                Add(items, ArtKeys.VillagerDead(d), "Fallen", p => CharacterArt.VillagerDead(p, facing), false, half);
                Add(items, ArtKeys.RulerDead(d), "Fallen", p => CharacterArt.RulerDead(p, facing), false, half);
            }

            if (walls != null)
                foreach (var wall in walls)
                    for (int v = 0; v < Mathf.Max(1, wall.Variants); v++)
                    {
                        var look = wall;
                        int variant = v;
                        Add(items, ArtKeys.Wall(wall.Name, v), "Walls-" + wall.Name, p =>
                        {
                            var img = look.Logs ? WallArt.Logs(p, look.Gap, look.Colors, variant) : WallArt.Stone(p, look.Gap, look.Colors, variant);
                            img.Pivot = Vector2.zero;   // wall chunks are placed by their bottom-left corner
                            return img;
                        }, true);
                    }

            items.Add(new ArtItem { Key = ArtKeys.GroundGrass, Sheet = "Ground-Grass", Draw = p => GroundArt.Grass(p, 101u), NormalMap = true, Repeats = true });
            items.Add(new ArtItem { Key = ArtKeys.GroundDry, Sheet = "Ground-Dry", Draw = p => GroundArt.DryGrass(p, 202u), NormalMap = true, Repeats = true });
            items.Add(new ArtItem { Key = ArtKeys.GroundMud, Sheet = "Ground-Mud", Draw = p => GroundArt.Mud(p, 303u), NormalMap = true, Repeats = true });
            return items;
        }

        static void Add(List<ArtItem> items, string key, string sheet, Func<int, PixelImage> draw, bool normals, float detail = 1f) =>
            items.Add(new ArtItem { Key = key, Sheet = sheet, Draw = draw, NormalMap = normals, Detail = detail });

        // Shadows are blurry, so they're drawn 4 times smaller (than their item, at its detail).
        static void Shadow(List<ArtItem> items, string of, string sheet, float blurTiles, string key = null)
        {
            var source = items.FindLast(i => i.Key == of);
            items.Add(new ArtItem
            {
                Key = key ?? ArtKeys.Shadow(of), Sheet = sheet, ShadowOf = of, Shrink = 4, BlurTiles = blurTiles,
                Detail = source != null ? source.Detail : 1f,
            });
        }

        /// <summary>Pixels per tile an item is drawn at when the art is drawn at <paramref name="ppu"/> (its <see cref="ArtItem.Detail"/> share).</summary>
        public static int DetailOf(ArtItem item, int ppu) =>
            item.Detail >= 1f ? ppu : Mathf.Max(16, Mathf.RoundToInt(ppu * Mathf.Max(0.01f, item.Detail)));

        /// <summary>Draws one item at <paramref name="ppu"/> pixels per tile, times its detail (shadows need their item, drawn the same way).</summary>
        public static PixelImage Render(ArtItem item, int ppu, Func<string, PixelImage> source)
        {
            int detail = DetailOf(item, ppu);
            if (item.ShadowOf == null) return item.Draw(detail);
            var of = source(item.ShadowOf);
            int shrink = Mathf.Max(1, item.Shrink);
            int blur = Mathf.Max(1, Mathf.RoundToInt(item.BlurTiles * detail / shrink));
            return of.Shadow(blur, 1f, shrink);
        }

        /// <summary>Whether a baked sheet's picture repeats across the map (the ground), so its texture should wrap around.</summary>
        public static bool Repeats(string sheet)
        {
            foreach (var item in Items(null))
                if (item.Repeats && item.Sheet == sheet) return true;
            return false;
        }

        /// <summary>Pixels per tile of an item's picture when the art is drawn at <paramref name="ppu"/>.</summary>
        public static float PixelsPerTile(ArtItem item, int ppu)
        {
            int detail = DetailOf(item, ppu);
            return item.ShadowOf != null ? detail / (float)Mathf.Max(1, item.Shrink) : detail;
        }
    }

    /// <summary>A packed sheet: where each picture sits in it.</summary>
    public sealed class ArtSheet
    {
        public string Name;
        public bool Repeats;
        public PixelImage Albedo;
        public PixelImage Normals;   // null if none of its pictures have one
        public readonly List<(ArtItem Item, int X, int Y, int Width, int Height, Vector2 Pivot, float PixelsPerTile)> Entries =
            new List<(ArtItem, int, int, int, int, Vector2, float)>();

        /// <summary>The biggest sheet: graphics cards can't all use bigger textures.</summary>
        public const int MaxSheetSize = 8192;

        /// <summary>
        /// Draws every item at <paramref name="ppu"/> and packs them into sheets. Pictures
        /// are spaced apart so smooth filtering never mixes neighbors, even far zoomed out.
        /// Each finished sheet goes to <paramref name="finished"/> straight away and isn't
        /// kept, so only one sheet's pixels are in memory at a time.
        /// The pictures of a sheet are drawn on several processor cores at once (the art
        /// code is plain math with no shared state). <paramref name="progress"/> (told
        /// what's being drawn and how far along the whole bake is, 0..1) and
        /// <paramref name="finished"/> are only called on the calling thread.
        /// </summary>
        public static void Pack(List<ArtItem> items, int ppu, Action<ArtSheet> finished, Action<string, float> progress = null)
        {
            var byKey = new Dictionary<string, ArtItem>();
            foreach (var item in items) byKey[item.Key] = item;
            // Pictures are only kept until the shadows drawn from them are done.
            var shadowsLeft = new Dictionary<string, int>();
            foreach (var item in items)
                if (item.ShadowOf != null) shadowsLeft[item.ShadowOf] = shadowsLeft.TryGetValue(item.ShadowOf, out int n) ? n + 1 : 1;
            var kept = new Dictionary<string, PixelImage>();
            PixelImage Get(string key)
            {
                if (kept.TryGetValue(key, out var img)) return img;
                if (!byKey.TryGetValue(key, out var item)) throw new ArgumentException($"No art called {key}.");
                img = ArtCatalog.Render(item, ppu, Get);
                if (item.ShadowOf != null && --shadowsLeft[item.ShadowOf] == 0) kept.Remove(item.ShadowOf);
                if (shadowsLeft.TryGetValue(key, out int left) && left > 0) kept[key] = img;
                return img;
            }

            var bySheet = new Dictionary<string, List<ArtItem>>();
            var sheetOrder = new List<string>();
            foreach (var item in items)
            {
                if (!bySheet.TryGetValue(item.Sheet, out var list))
                {
                    bySheet[item.Sheet] = list = new List<ArtItem>();
                    sheetOrder.Add(item.Sheet);
                }
                list.Add(item);
            }

            int done = 0;
            int workers = Mathf.Clamp(Environment.ProcessorCount - 1, 1, 8);
            // Repeating pictures (the ground) are a sheet each; they're drawn together,
            // side by side, when the first of them comes up.
            var repeating = new Dictionary<string, PixelImage>();
            foreach (string sheetName in sheetOrder)
            {
                var sheetItems = bySheet[sheetName];
                var drawn = new PixelImage[sheetItems.Count];
                if (sheetItems[0].Repeats && repeating.Count == 0)
                {
                    var ground = new List<ArtItem>();
                    foreach (string other in sheetOrder)
                        if (bySheet[other][0].Repeats) ground.AddRange(bySheet[other]);
                    var pictures = new PixelImage[ground.Count];
                    DrawAll(pictures.Length, i => pictures[i] = ground[i].Draw(ArtCatalog.DetailOf(ground[i], ppu)), workers,
                        count => progress?.Invoke($"The ground: {count} of {ground.Count} drawn", (done + count) / (float)items.Count));
                    for (int i = 0; i < ground.Count; i++) repeating[ground[i].Key] = pictures[i];
                }

                // The pictures of their own (the slow part) on several cores, while this thread
                // keeps the progress up to date; then the shadows, which are quick and made
                // from other pictures.
                int toDraw = sheetItems.FindAll(i => i.ShadowOf == null).Count;
                DrawAll(sheetItems.Count, i =>
                {
                    if (sheetItems[i].ShadowOf != null) return;
                    if (repeating.TryGetValue(sheetItems[i].Key, out var picture)) drawn[i] = picture;
                    else drawn[i] = sheetItems[i].Draw(ArtCatalog.DetailOf(sheetItems[i], ppu));
                }, workers, count => progress?.Invoke($"{sheetName}: {count} of {toDraw} pictures drawn", (done + count) / (float)items.Count));
                foreach (var item in sheetItems) repeating.Remove(item.Key);
                for (int i = 0; i < sheetItems.Count; i++)
                {
                    string key = sheetItems[i].Key;
                    if (drawn[i] != null && shadowsLeft.TryGetValue(key, out int left) && left > 0) kept[key] = drawn[i];
                }
                for (int i = 0; i < sheetItems.Count; i++)
                    if (drawn[i] == null)
                    {
                        progress?.Invoke(sheetItems[i].Key, (done + i) / (float)items.Count);
                        drawn[i] = Get(sheetItems[i].Key);
                    }
                done += sheetItems.Count;

                if (sheetItems[0].Repeats)
                {
                    // A repeating picture (the ground) is a texture of its own, exactly its size.
                    if (sheetItems.Count != 1) throw new InvalidOperationException($"The repeating sheet {sheetName} must hold exactly one picture.");
                    var img = drawn[0];
                    var ground = new ArtSheet { Name = sheetName, Repeats = true, Albedo = img };
                    ground.Normals = sheetItems[0].NormalMap ? img.NormalMap(wrap: true) : null;
                    ground.Entries.Add((sheetItems[0], 0, 0, img.Width, img.Height, img.Pivot, ArtCatalog.PixelsPerTile(sheetItems[0], ppu)));
                    finished(ground);
                    continue;
                }

                // Shelf packing, tallest first. Of the sheet widths that fit everything, the
                // one with the least area wins (sheets needn't be square or a power of two);
                // if even the widest can't, the rest go on another sheet (like "Nature_2").
                // Room between pictures: a sixteenth of a tile (shadows are drawn 4 times smaller).
                int detail = 0;
                foreach (var item in sheetItems) detail = Mathf.Max(detail, ArtCatalog.DetailOf(item, ppu));
                int pad = sheetItems[0].ShadowOf != null ? Mathf.Max(4, detail / 64) : Mathf.Max(4, detail / 16);
                var remaining = new List<int>();
                for (int i = 0; i < drawn.Length; i++) remaining.Add(i);
                remaining.Sort((a, b) => drawn[b].Height.CompareTo(drawn[a].Height));
                bool anyNormals = sheetItems.Exists(i => i.NormalMap);
                for (int part = 1; remaining.Count > 0; part++)
                {
                    int bestWidth = 0, bestHeight = 0, bestPlaced = 0;
                    long bestArea = long.MaxValue;
                    for (int width = 256; width <= MaxSheetSize; width *= 2)
                    {
                        int placed = Shelf(drawn, remaining, width, pad, null, out int height);
                        long area = (long)width * height;
                        if (placed > bestPlaced || (placed == bestPlaced && placed > 0 && area < bestArea))
                        {
                            bestPlaced = placed;
                            bestArea = area;
                            bestWidth = width;
                            bestHeight = height;
                        }
                    }
                    if (bestPlaced == 0)
                        throw new InvalidOperationException($"A picture in sheet {sheetName} is bigger than {MaxSheetSize} pixels.");
                    var spots = new Vector2Int[drawn.Length];
                    Shelf(drawn, remaining, bestWidth, pad, spots, out _);

                    var sheet = new ArtSheet { Name = part == 1 ? sheetName : $"{sheetName}_{part}" };
                    sheet.Albedo = new PixelImage(bestWidth, bestHeight);
                    if (anyNormals)
                    {
                        sheet.Normals = new PixelImage(bestWidth, bestHeight);
                        for (int i = 0; i < sheet.Normals.Pixels.Length; i++) sheet.Normals.Pixels[i] = new Color32(128, 128, 255, 255);
                    }
                    for (int k = 0; k < bestPlaced; k++)
                    {
                        int i = remaining[k];
                        var img = drawn[i];
                        sheet.Albedo.Stamp(img, spots[i].x, spots[i].y);
                        Extrude(sheet.Albedo, spots[i].x, spots[i].y, img.Width, img.Height, pad / 2);
                        if (anyNormals && sheetItems[i].NormalMap)
                        {
                            sheet.Normals.Stamp(img.NormalMap(), spots[i].x, spots[i].y);
                            Extrude(sheet.Normals, spots[i].x, spots[i].y, img.Width, img.Height, pad / 2);
                        }
                        sheet.Entries.Add((sheetItems[i], spots[i].x, spots[i].y, img.Width, img.Height, img.Pivot, ArtCatalog.PixelsPerTile(sheetItems[i], ppu)));
                        drawn[i] = null;   // let go of the picture
                    }
                    remaining.RemoveRange(0, bestPlaced);
                    finished(sheet);
                }
            }
        }

        // Runs draw(0 .. count-1) on several cores; this thread meanwhile tells progress how
        // many are done (Unity's progress bar only works from this thread).
        static void DrawAll(int count, Action<int> draw, int workers, Action<int> progress)
        {
            int finishedCount = 0;
            var work = Task.Run(() => Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = workers }, i =>
            {
                draw(i);
                Interlocked.Increment(ref finishedCount);
            }));
            try
            {
                while (!work.Wait(100)) progress(Volatile.Read(ref finishedCount));
            }
            catch (AggregateException e)
            {
                // Pass on the art code's own error, not the wrapper.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.Flatten().InnerExceptions[0]).Throw();
            }
        }

        // Copies a picture's outermost pixels outwards into the room around it, so smooth
        // filtering and the smaller versions (mipmaps) never mix its edge with the empty
        // room: pictures that fill their tile edge to edge (floors, walls) would get seams.
        static void Extrude(PixelImage sheet, int x, int y, int width, int height, int by)
        {
            if (by <= 0 || width <= 0 || height <= 0) return;
            for (int sy = y - by; sy < y + height + by; sy++)
                for (int sx = x - by; sx < x + width + by; sx++)
                {
                    if (sx >= x && sx < x + width && sy >= y && sy < y + height) { sx = x + width - 1; continue; }
                    if (!sheet.Inside(sx, sy)) continue;
                    int cx = Mathf.Clamp(sx, x, x + width - 1), cy = Mathf.Clamp(sy, y, y + height - 1);
                    sheet.Pixels[sy * sheet.Width + sx] = sheet.Pixels[cy * sheet.Width + cx];
                }
        }

        // Places pictures left to right in rows, in the given order, until one doesn't fit
        // within MaxSheetSize; returns how many were placed and the height used (rounded up
        // to 64 pixels, which keeps compressed textures and their smaller versions happy).
        static int Shelf(PixelImage[] images, List<int> order, int width, int pad, Vector2Int[] spots, out int height)
        {
            int x = pad, y = pad, rowHeight = 0, placed = 0;
            foreach (int i in order)
            {
                var img = images[i];
                if (img.Width + pad * 2 > width) break;
                if (x + img.Width + pad > width)
                {
                    x = pad;
                    y += rowHeight + pad;
                    rowHeight = 0;
                }
                if (y + img.Height + pad > MaxSheetSize) break;
                if (spots != null) spots[i] = new Vector2Int(x, y);
                x += img.Width + pad;
                rowHeight = Mathf.Max(rowHeight, img.Height);
                placed++;
            }
            height = Mathf.Min(MaxSheetSize, (y + rowHeight + pad + 63) / 64 * 64);
            return placed;
        }
    }
}
