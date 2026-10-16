using System;
using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Economy;

// Ore fields (Assets/Scripts/Economy/OreMap.cs) and how the ground shows them (GroundArt.OreMasksAt).
static partial class Tests
{
    // The same seed gives the same ore everywhere (every computer online makes its own), and a map agrees with the generator.
    static void OreIsTheSameEverywhere()
    {
        var a = new OreMap(99u);
        var b = new OreMap(99u);
        for (int y = -300; y < 300; y += 7)
            for (int x = -300; x < 300; x += 3)
            {
                var kind = OreMap.Generate(99u, x, y, out int amount);
                var again = OreMap.Generate(99u, x, y, out int amount2);
                Check(kind == again && amount == amount2, $"{x},{y}: different the second time");
                Check(a.At(x, y, out int left) == kind && left == amount && b.At(x, y, out _) == kind, $"{x},{y}: a map says otherwise");
                Check((kind == OreKind.None) == (amount == 0), $"{x},{y}: ore with nothing in it, or an amount with no ore");
            }
        Check(OreMap.Generate(99u, OreMap.Limit, 5, out _) == OreKind.None && OreMap.Generate(99u, 5, -OreMap.Limit - 40, out _) == OreKind.None,
            "no ore past the world's edge (house insides and mines are there)");
        Check(OreMap.Limit == NatureMap.Limit, "the ore map's edge is the world's");
    }

    // Round the start: one field of each kind, big enough to build on, a short walk away, clear of the castle and the outpost.
    static void TheStartHasOneFieldOfEach()
    {
        foreach (uint seed in new uint[] { 1u, 7u, 1234u, 987654u, 31337u })
        {
            var count = new int[5];
            for (int y = -70; y < 70; y++)
                for (int x = -70; x < 70; x++)
                {
                    var kind = OreMap.Generate(seed, x, y, out _);
                    if (kind == OreKind.None) continue;
                    count[(int)kind]++;
                    Check(x * x + y * y > 15 * 15, $"seed {seed}: ore at {x},{y}, too near the throne");
                    Check(!(x >= 3 && x <= 15 && y >= -6 && y <= 6), $"seed {seed}: ore at {x},{y}, in the test outpost");
                }
            for (int k = 1; k <= 4; k++)
                Check(count[k] >= 60, $"seed {seed}: only {count[k]} tiles of {(OreKind)k} round the start");
            var fields = OreMap.StartFields(seed);
            Check(fields.Length == 4, "four start fields");
            for (int i = 0; i < fields.Length; i++)
                for (int j = i + 1; j < fields.Length; j++)
                    Check(!fields[i].Touches(fields[j].MinX, fields[j].MinY, fields[j].MaxX, fields[j].MaxY), $"seed {seed}: two start fields overlap");
        }
    }

    // Every field further out stays inside its own square (so a tile only has to look at its own square's field), about
    // half the squares have one, every kind turns up, and they're richer further out.
    static void OreFieldsStayInTheirSquare()
    {
        int found = 0, total = 0;
        var kinds = new HashSet<OreKind>();
        float nearRich = 0f, farRich = 0f;
        foreach (var (ox, oy) in new[] { (0, 0), (60, -40) })
            for (int ry = oy - 15; ry < oy + 15; ry++)
                for (int rx = ox - 15; rx < ox + 15; rx++)
                {
                    total++;
                    if (!OreMap.FieldOf(4242u, rx, ry, out var f)) continue;
                    found++;
                    kinds.Add(f.Kind);
                    Check(f.MinX >= rx * OreMap.Region && f.MaxX < (rx + 1) * OreMap.Region && f.MinY >= ry * OreMap.Region && f.MaxY < (ry + 1) * OreMap.Region,
                        $"square {rx},{ry}: its field reaches outside it");
                    if (ox == 0) nearRich = Math.Max(nearRich, f.Richness);
                    else farRich = Math.Max(farRich, f.Richness);
                    // Its tiles are all inside its bounds.
                    for (int y = f.MinY - 2; y <= f.MaxY + 2; y++)
                        for (int x = f.MinX - 2; x <= f.MaxX + 2; x++)
                        {
                            bool inBounds = x >= f.MinX && x <= f.MaxX && y >= f.MinY && y <= f.MaxY;
                            var kind = OreMap.Generate(4242u, x, y, out _);
                            if (!inBounds) Check(kind == OreKind.None, $"square {rx},{ry}: ore at {x},{y} outside its field's bounds");
                            else if (kind != OreKind.None) Check(kind == f.Kind, $"square {rx},{ry}: {kind} in a {f.Kind} field");
                        }
                }
        float share = found / (float)total;
        Check(share > 0.4f && share < 0.6f, $"{share:P0} of the squares have a field");
        Check(kinds.Count == 4, $"only {kinds.Count} kinds of field turned up");
        Check(farRich > nearRich, "fields far out are richer");
    }

    // The fields the ground drawing is given for a square include every tile with ore in it.
    static void FieldsNearFindsEveryField()
    {
        var fields = new List<OreMap.Field>();
        foreach (var (x0, y0) in new[] { (-64, -64), (0, 0), (-128, 64), (192, -192), (1000, 3000), (-70, 10) })
        {
            OreMap.FieldsNear(7u, x0, y0, x0 + 63, y0 + 63, fields);
            for (int y = y0; y < y0 + 64; y++)
                for (int x = x0; x < x0 + 64; x++)
                {
                    var kind = OreMap.Generate(7u, x, y, out _);
                    if (kind == OreKind.None) continue;
                    bool listed = false;
                    foreach (var f in fields) listed |= f.Kind == kind && OreMap.Inside(f, x + 0.5f, y + 0.5f) > 0f;
                    Check(listed, $"{kind} at {x},{y} isn't in the square's list of fields");
                }
        }
    }

    // A tile goes down a quarter at a time as it's mined (each step a change to draw), until it's empty; what's been
    // taken is saved and put back exactly, and a square's copy of it lines up.
    static void MiningEmptiesTilesAndIsSaved()
    {
        var map = new OreMap(7u);
        int tx = 0, ty = 0, amount = 0;
        for (int y = -60; y < 60 && amount == 0; y++)
            for (int x = -60; x < 60 && amount == 0; x++)
                if (map.At(x, y, out int left) == OreKind.Iron) { tx = x; ty = y; amount = left; }
        Check(amount > 0, "no iron round the start");
        var changes = new List<(int, int)>();
        map.Changed += (x, y) => changes.Add((x, y));
        int mined = 0;
        while (map.Mine(tx, ty, out var kind))
        {
            Check(kind == OreKind.Iron, "it gave something other than iron");
            mined++;
            Check(mined <= amount, "it gave more than it had");
        }
        Check(mined == amount, $"mined {mined} of {amount}");
        Check(map.At(tx, ty, out int none) == OreKind.None && none == 0, "an empty tile still has ore");
        Check(changes.Count == 4 && changes.TrueForAll(c => c == (tx, ty)), $"{changes.Count} changes to draw, not 4 (one a quarter)");
        Check(OreMap.Level(amount, 0) == 4 && OreMap.Level(amount, amount) == 0 && OreMap.Level(amount, amount - 1) == 1, "the levels are wrong");

        // Some of the tile next to it, then save and load into a fresh map.
        int nx = tx + 1;
        bool next = map.At(nx, ty, out int nextLeft) != OreKind.None;
        if (next) for (int i = 0; i < 5; i++) map.Mine(nx, ty, out _);
        var data = map.Save();
        var loaded = new OreMap(7u);
        var redrawn = new List<(int, int)>();
        loaded.Changed += (x, y) => redrawn.Add((x, y));
        loaded.Load(data);
        Check(loaded.At(tx, ty, out _) == OreKind.None, "the mined-out tile came back full");
        if (next) Check(loaded.At(nx, ty, out int after) != OreKind.None && after == nextLeft - 5, "the tile next to it didn't keep what was taken");
        Check(redrawn.Contains((tx, ty)), "loading doesn't say the ground there changed");
        loaded.Load(null);
        Check(loaded.At(tx, ty, out int back) != OreKind.None && back == amount && redrawn.Count >= 2, "loading an older save (none taken) doesn't put it back");

        var copy = map.TakenIn(tx - 3, ty - 2, 10, 10);
        Check(copy != null && copy[2 * 10 + 3] == amount, "the square's copy is in the wrong place");
        Check(map.TakenIn(tx + 500, ty + 500, 8, 8) == null, "a square nobody mined has a copy");
    }

    // The ground shows ore where tiles have ore and not where they don't; a mined-out tile is bare.
    static void OreShowsWhereTheOreIs()
    {
        const int tiles = 64, detail = 8;
        uint seed = 7u;
        var masks = new byte[4][];
        int shownInside = 0, inside = 0;
        foreach (var (ox, oy) in new[] { (-64, -64), (0, -64), (-64, 0), (0, 0) })
        {
            if (!GroundArt.OreMasksAt(ox, oy, tiles, detail, seed, null, masks)) continue;
            for (int y = 0; y < tiles; y++)
                for (int x = 0; x < tiles; x++)
                {
                    int wx = ox + x, wy = oy + y;
                    var kind = OreMap.Generate(seed, wx, wy, out _);
                    int p = (y * detail + detail / 2) * tiles * detail + x * detail + detail / 2;   // the tile's middle
                    // Far from any ore (no ore in the 3x3 round it): nothing shows.
                    bool near = false;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) near |= OreMap.Generate(seed, wx + dx, wy + dy, out _) != OreKind.None;
                    for (int k = 0; k < 4; k++)
                        if (!near) Check(masks[k] == null || masks[k][p] < 128, $"ore shows at {wx},{wy}, which has none");
                    if (kind == OreKind.None) continue;
                    // Well inside the field (two tiles all round are ore too; nearer the edge it thins out): its own kind shows nearly everywhere.
                    bool deep = true;
                    for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++) deep &= OreMap.Generate(seed, wx + dx, wy + dy, out _) == kind;
                    if (!deep) continue;
                    inside++;
                    if (masks[(int)kind - 1] != null && masks[(int)kind - 1][p] >= 128) shownInside++;
                }
        }
        Check(inside > 150, $"only {inside} tiles deep inside fields round the start");
        Check(shownInside > inside * 0.97f, $"only {shownInside} of {inside} ore tiles show their ore");
        // Every ore tile shows some of its ore somewhere in it, even at the thinned-out edge.
        int tilesWithOre = 0, bareTiles = 0;
        foreach (var (ox, oy) in new[] { (-64, -64), (0, -64), (-64, 0), (0, 0) })
        {
            if (!GroundArt.OreMasksAt(ox, oy, tiles, detail, seed, null, masks)) continue;
            for (int y = 0; y < tiles; y++)
                for (int x = 0; x < tiles; x++)
                {
                    var kind = OreMap.Generate(seed, ox + x, oy + y, out _);
                    if (kind == OreKind.None) continue;
                    tilesWithOre++;
                    var mask = masks[(int)kind - 1];
                    int shown = 0;
                    for (int py = 0; py < detail; py++)
                        for (int px = 0; px < detail; px++)
                            if (mask[(y * detail + py) * tiles * detail + x * detail + px] >= 128) shown++;
                    if (shown == 0) bareTiles++;
                }
        }
        Check(bareTiles <= tilesWithOre / 50, $"{bareTiles} of {tilesWithOre} ore tiles show none of their ore");

        // Mine out a tile deep inside: it's bare in the middle.
        var map = new OreMap(seed);
        int bx = 0, by = 0;
        bool found = false;
        for (int y = -60; y < 60 && !found; y++)
            for (int x = -60; x < 60 && !found; x++)
            {
                bool deep = map.At(x, y, out _) != OreKind.None;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) deep &= OreMap.Generate(seed, x + dx, y + dy, out _) != OreKind.None;
                if (deep) { bx = x; by = y; found = true; }
            }
        var kindThere = map.At(bx, by, out _);
        while (map.Mine(bx, by, out _)) { }
        int sx = (int)Math.Floor(bx / 64.0) * 64, sy = (int)Math.Floor(by / 64.0) * 64;
        GroundArt.OreMasksAt(sx, sy, tiles, detail, seed, map.TakenIn(sx - 1, sy - 1, tiles + 2, tiles + 2), masks);
        int mid = ((by - sy) * detail + detail / 2) * tiles * detail + (bx - sx) * detail + detail / 2;
        Check(masks[(int)kindThere - 1][mid] < 128, "a mined-out tile still shows ore in its middle");
    }

    // Neighbouring squares' ore masks join up exactly (the same pixel worked out from either side).
    static void OreMasksJoinUp()
    {
        const int tiles = 32, detail = 8;
        uint seed = 1234u;
        var field = OreMap.StartFields(seed)[0];
        // Squares split right through the middle of the iron field.
        int cx = (int)Math.Floor(field.X), cy = (int)Math.Floor(field.Y);
        var whole = new byte[4][];
        var left = new byte[4][];
        var right = new byte[4][];
        GroundArt.OreMasksAt(cx - tiles, cy - tiles / 2, tiles * 2, detail, seed, null, whole);
        GroundArt.OreMasksAt(cx - tiles, cy - tiles / 2, tiles, detail, seed, null, left);
        GroundArt.OreMasksAt(cx, cy - tiles / 2, tiles, detail, seed, null, right);
        int k = (int)field.Kind - 1;
        Check(whole[k] != null && left[k] != null && right[k] != null, "the field doesn't show on both sides");
        int n = tiles * detail;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Check(left[k][y * n + x] == whole[k][y * n * 2 + x], $"left square differs at {x},{y}");
                Check(right[k][y * n + x] == whole[k][y * n * 2 + n + x], $"right square differs at {x},{y}");
            }
    }
}
