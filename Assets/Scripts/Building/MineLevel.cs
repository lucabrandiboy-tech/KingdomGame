using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Economy;
using KingdomGame.Villagers;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>What's in a tile of rock.</summary>
    public enum Ore : byte
    {
        None,
        Copper,
        Iron,
        Gold,
    }

    /// <summary>
    /// The underground of a mine: solid rock all round a small room at the bottom of the
    /// shaft. Mark rock to dig (the Mining tab's Dig, from inside the mine) and miners dig
    /// it out a tile at a time: plain rock gives stone, and some of it has copper, iron or
    /// gold in it (you can see the flecks in the rock; the rarer ores lie further from the
    /// shaft). What's dug out is floor to build on, like a house's inside. The whole level
    /// is one picture, painted again round each tile that's dug.
    /// </summary>
    public sealed class MineLevel : Place
    {
        /// <summary>Pixels per tile of the mine's picture.</summary>
        const int Detail = 24;

        Kingdom kingdom;
        MineConfig config;
        RectInt bounds;
        NavGrid nav;
        bool[] dug;
        bool[] marked;
        Ore[] ore;
        uint seed;
        Texture2D texture;
        Sprite sprite;
        Color32[] pixels;
        readonly Dictionary<Vector2Int, SpriteRenderer> marks = new Dictionary<Vector2Int, SpriteRenderer>();
        readonly Dictionary<Vector2Int, Villager> diggers = new Dictionary<Vector2Int, Villager>();

        public MineShaft Shaft { get; private set; }
        public MineConfig Config => config;
        public override string Name => "Mine";
        public override RectInt Bounds => bounds;
        public override NavGrid Nav => nav;
        /// <summary>The bottom of the shaft (the ladder up): in the middle.</summary>
        public override Vector2Int ExitCell => new Vector2Int(bounds.x + bounds.width / 2, bounds.y + bounds.height / 2);
        public override Vector2Int EntranceCell => ExitCell + Vector2Int.down;
        public override Vector2 Outside => Shaft.Outside;
        public override Vector2Int OutsideCell => Shaft.OutsideCell;
        public override Vector2 DoorPoint => Shaft.DoorPoint;
        public override IBuilt Building => Shaft;
        public override Health BuildingHealth => Shaft != null ? Shaft.Health : null;
        /// <summary>How many tiles are marked to dig and not dug yet.</summary>
        public int Marked { get; private set; }

        int Index(Vector2Int cell) => (cell.y - bounds.y) * bounds.width + (cell.x - bounds.x);
        bool Inside(Vector2Int cell) => bounds.Contains(cell);
        public bool IsDug(Vector2Int cell) => Inside(cell) && dug[Index(cell)];
        public bool IsMarked(Vector2Int cell) => Inside(cell) && marked[Index(cell)];
        public Ore OreAt(Vector2Int cell) => Inside(cell) ? ore[Index(cell)] : Ore.None;

        public override bool CanBuildAt(Vector2Int cell) => IsDug(cell) && cell != ExitCell;
        public override string CantBuildReason(Vector2Int cell) =>
            cell == ExitCell ? "That's the ladder up" : !Inside(cell) ? "Outside the mine" : "Solid rock: dig it out first (Mining tab: Dig)";

        public void Setup(MineShaft shaft, MineConfig mineConfig, Vector2Int origin, Kingdom owner)
        {
            Shaft = shaft;
            config = mineConfig != null ? mineConfig : ScriptableObject.CreateInstance<MineConfig>();
            kingdom = owner;
            int size = Mathf.Clamp(config.size, 16, 96);
            bounds = new RectInt(origin, new Vector2Int(size, size));
            seed = (uint)(origin.x * 92821) ^ (uint)(origin.y * 68917);
            dug = new bool[size * size];
            marked = new bool[size * size];
            ore = new Ore[size * size];

            // The rock and its ore; the outermost ring can't be dug (the mine's edge).
            var shaftCell = ExitCell;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var cell = new Vector2Int(bounds.x + x, bounds.y + y);
                    float from = Vector2.Distance(cell, shaftCell);
                    ore[y * size + x] = OreFor(x, y, from);
                }
            // The room at the bottom of the shaft.
            int half = Mathf.Clamp(config.startRoom, 3, 9) / 2;
            for (int y = -half; y <= half; y++)
                for (int x = -half; x <= half; x++)
                {
                    int i = Index(shaftCell + new Vector2Int(x, y));
                    dug[i] = true;
                    ore[i] = Ore.None;
                }

            nav = new NavGrid(bounds, null);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    if (!dug[y * size + x]) nav.Block(new RectInt(bounds.x + x, bounds.y + y, 1, 1));
            nav.Block(new RectInt(shaftCell, Vector2Int.one));   // the ladder

            Paint();
        }

        // Ore in veins (patches of noise), the rarer kinds only further from the shaft.
        Ore OreFor(int x, int y, float fromShaft)
        {
            float gold = Noise.Value(x / 2.2f, y / 2.2f, seed + 31u);
            float iron = Noise.Value(x / 2.8f, y / 2.8f, seed + 32u);
            float copper = Noise.Value(x / 3.2f, y / 3.2f, seed + 33u);
            // Smooth noise sits mostly round the middle: these cut-offs give roughly the asked-for shares.
            if (fromShaft >= config.goldFrom && gold > 1f - config.gold * 2.2f) return Ore.Gold;
            if (fromShaft >= config.ironFrom && iron > 1f - config.iron * 2.2f) return Ore.Iron;
            if (copper > 1f - config.copper * 2.2f) return Ore.Copper;
            return Ore.None;
        }

        // ------------------------------------------------------------ digging --

        /// <summary>Marks rock to be dug out (miners come). False if it can't be (dug already, the edge, outside).</summary>
        public bool Mark(Vector2Int cell)
        {
            if (!CanMark(cell, out _)) return false;
            marked[Index(cell)] = true;
            Marked++;
            ShowMark(cell, true);
            return true;
        }

        public bool CanMark(Vector2Int cell, out string reason)
        {
            reason = null;
            if (!Inside(cell)) reason = "Outside the mine";
            else if (cell.x == bounds.xMin || cell.y == bounds.yMin || cell.x == bounds.xMax - 1 || cell.y == bounds.yMax - 1) reason = "The mine ends here";
            else if (dug[Index(cell)]) reason = "Already dug out";
            else if (marked[Index(cell)]) reason = "Already marked";
            return reason == null;
        }

        /// <summary>Takes a mark off (nobody digs there).</summary>
        public void Unmark(Vector2Int cell)
        {
            if (!IsMarked(cell)) return;
            marked[Index(cell)] = false;
            Marked--;
            ShowMark(cell, false);
            if (diggers.TryGetValue(cell, out var v) && v != null) diggers.Remove(cell);
        }

        /// <summary>
        /// The marked tile nearest to <paramref name="from"/> that a miner can stand next to and that
        /// nobody else is digging, or false if there's none.
        /// </summary>
        public bool NextToDig(Vector2 from, Villager miner, out Vector2Int cell)
        {
            cell = default;
            if (Marked <= 0) return false;
            float best = float.MaxValue;
            bool found = false;
            for (int y = bounds.yMin + 1; y < bounds.yMax - 1; y++)
                for (int x = bounds.xMin + 1; x < bounds.xMax - 1; x++)
                {
                    var c = new Vector2Int(x, y);
                    int i = Index(c);
                    if (!marked[i] || dug[i]) continue;
                    if (diggers.TryGetValue(c, out var other) && other != null && other != miner) continue;
                    // It has to be reachable: an open tile beside it.
                    if (!(Open(c + Vector2Int.left) || Open(c + Vector2Int.right) || Open(c + Vector2Int.up) || Open(c + Vector2Int.down))) continue;
                    float d = ((Vector2)c - from).sqrMagnitude;
                    if (d < best) { best = d; cell = c; found = true; }
                }
            return found;
        }

        bool Open(Vector2Int c) => Inside(c) && nav.Walkable(c);

        public void Claim(Vector2Int cell, Villager miner) => diggers[cell] = miner;

        public void Release(Vector2Int cell, Villager miner)
        {
            if (diggers.TryGetValue(cell, out var v) && v == miner) diggers.Remove(cell);
        }

        /// <summary>Digs a marked tile out: it becomes floor. Returns what came out of it (stone, or its ore) and how much.</summary>
        public bool Dig(Vector2Int cell, out ResourceType type, out int amount)
        {
            type = ResourceType.Stone;
            amount = 0;
            if (!IsMarked(cell) || IsDug(cell)) return false;
            int i = Index(cell);
            dug[i] = true;
            marked[i] = false;
            Marked--;
            ShowMark(cell, false);
            diggers.Remove(cell);
            nav.Unblock(new RectInt(cell, Vector2Int.one));
            switch (ore[i])
            {
                case Ore.Copper: type = ResourceType.Copper; amount = config.orePerTile; break;
                case Ore.Iron: type = ResourceType.Iron; amount = config.orePerTile; break;
                case Ore.Gold: type = ResourceType.Gold; amount = config.orePerTile; break;
                default: amount = config.stonePerTile; break;
            }
            ore[i] = Ore.None;
            var world = Destruction.PixelWorld.Current;
            if (world != null) world.Burst((Vector2)cell + new Vector2(0.5f, 0.5f), new Color32(110, 100, 88, 255), 10, 2.5f);
            // The tile and the rock round it look different now (the cut faces).
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                    PaintCell(cell + new Vector2Int(x, y));
            texture.SetPixels32(pixels);
            texture.Apply(true);
            return true;
        }

        void ShowMark(Vector2Int cell, bool on)
        {
            if (!on)
            {
                if (marks.TryGetValue(cell, out var r) && r != null) Destroy(r.gameObject);
                marks.Remove(cell);
                return;
            }
            if (marks.ContainsKey(cell) || kingdom == null) return;
            var go = new GameObject("Dig here");
            go.transform.SetParent(transform, false);
            go.transform.position = (Vector2)cell + new Vector2(0.5f, 0.5f);
            var mark = go.AddComponent<SpriteRenderer>();
            mark.sprite = kingdom.CellMarker;
            mark.color = new Color(1f, 0.82f, 0.3f, 0.85f);
            mark.sortingOrder = SortOrder.Markers - 1;
            marks[cell] = mark;
        }

        // ------------------------------------------------------------ picture --

        void Paint()
        {
            int n = bounds.width * Detail;
            pixels = new Color32[n * n];
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                    PaintCell(new Vector2Int(x, y));
            texture = PixelTextures.NewTexture(n, n, "Mine");
            texture.SetPixels32(pixels);
            texture.Apply(true);
            sprite = PixelTextures.NewSprite(texture, Vector2.zero, Detail);
            var go = new GameObject("Rock and floor");
            go.transform.SetParent(transform, false);
            go.transform.position = (Vector2)bounds.position;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = SortOrder.Floors - 10;
            // The ladder up the shaft.
            var ladder = ArtSprites.Get(ArtKeys.Turned(FurnitureArt.Ladder, 0));
            if (ladder != null)
            {
                var up = new GameObject("Ladder up");
                up.transform.SetParent(transform, false);
                up.transform.position = (Vector2)ExitCell + new Vector2(0.5f, 0.5f);
                var lr = up.AddComponent<SpriteRenderer>();
                lr.sprite = ladder;
                lr.sortingOrder = SortOrder.Furniture;
            }
        }

        // One tile (MineArt draws it).
        void PaintCell(Vector2Int cell)
        {
            if (!Inside(cell)) return;
            int i = Index(cell);
            MineArt.PaintCell(pixels, bounds.width * Detail, Detail, (cell.x - bounds.x) * Detail, (cell.y - bounds.y) * Detail, cell.x, cell.y, seed,
                dug[i], (int)ore[i], IsDug(cell + Vector2Int.left), IsDug(cell + Vector2Int.right), IsDug(cell + Vector2Int.up), IsDug(cell + Vector2Int.down));
        }

        void OnDestroy()
        {
            nav?.Dispose();
            if (texture != null) Destroy(texture);
            if (sprite != null) Destroy(sprite);
        }
    }
}
