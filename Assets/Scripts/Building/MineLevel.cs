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
        Coal,
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
        // The picture, in squares of PieceTiles tiles (digging repaints and uploads only the squares it touches).
        const int PieceTiles = 16;
        int piecesX, piecesY;
        Texture2D[] textures;
        Sprite[] sprites;
        Color32[][] pieces;
        bool[] pieceChanged;
        readonly Dictionary<Vector2Int, SpriteRenderer> marks = new Dictionary<Vector2Int, SpriteRenderer>();
        readonly Dictionary<Vector2Int, Villager> diggers = new Dictionary<Vector2Int, Villager>();
        // Marked tiles a miner couldn't find a way to (furniture in the way), and until when to leave them be.
        readonly Dictionary<Vector2Int, float> unreachable = new Dictionary<Vector2Int, float>();

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
            float coal = Noise.Value(x / 3.6f, y / 3.6f, seed + 34u);
            if (coal > 1f - config.coal * 2.2f) return Ore.Coal;
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
                    if (unreachable.TryGetValue(c, out float until) && until > Time.time) continue;
                    // It has to be reachable: an open tile beside it.
                    if (!(Open(c + Vector2Int.left) || Open(c + Vector2Int.right) || Open(c + Vector2Int.up) || Open(c + Vector2Int.down))) continue;
                    float d = ((Vector2)c - from).sqrMagnitude;
                    if (d < best) { best = d; cell = c; found = true; }
                }
            return found;
        }

        bool Open(Vector2Int c) => Inside(c) && nav.Walkable(c);

        public void Claim(Vector2Int cell, Villager miner) => diggers[cell] = miner;

        /// <summary>A miner couldn't find a way to this tile: others go for other tiles for a while.</summary>
        public void CantReach(Vector2Int cell) => unreachable[cell] = Time.time + 15f;

        public void Release(Vector2Int cell, Villager miner)
        {
            if (diggers.TryGetValue(cell, out var v) && v == miner) diggers.Remove(cell);
        }

        /// <summary>
        /// For miners with nothing marked to dig: marks the next tile to follow the ore. That's the ore
        /// tile they can reach that's nearest the ladder, or, when no ore is in reach, the rock they can
        /// reach that's nearest to some ore (tunnelling towards it). False when there's no ore left to go for.
        /// </summary>
        public bool MarkTowardsOre()
        {
            var ores = new List<Vector2Int>();
            for (int y = bounds.yMin + 1; y < bounds.yMax - 1; y++)
                for (int x = bounds.xMin + 1; x < bounds.xMax - 1; x++)
                {
                    int i = Index(new Vector2Int(x, y));
                    if (ore[i] != Ore.None && !dug[i] && !marked[i]) ores.Add(new Vector2Int(x, y));
                }
            if (ores.Count == 0) return false;
            Vector2Int best = default;
            float bestScore = float.MaxValue;
            bool found = false;
            var exit = (Vector2)ExitCell;
            for (int y = bounds.yMin + 1; y < bounds.yMax - 1; y++)
                for (int x = bounds.xMin + 1; x < bounds.xMax - 1; x++)
                {
                    var c = new Vector2Int(x, y);
                    int i = Index(c);
                    if (dug[i] || marked[i] || !CanMark(c, out _)) continue;
                    if (unreachable.TryGetValue(c, out float until) && until > Time.time) continue;
                    if (!(Open(c + Vector2Int.left) || Open(c + Vector2Int.right) || Open(c + Vector2Int.up) || Open(c + Vector2Int.down))) continue;
                    // Ore in reach first (nearest the ladder); otherwise rock, by how near it gets to ore.
                    float score;
                    if (ore[i] != Ore.None) score = ((Vector2)c - exit).sqrMagnitude;
                    else
                    {
                        float near = float.MaxValue;
                        foreach (var o in ores) near = Mathf.Min(near, ((Vector2)(o - c)).sqrMagnitude);
                        score = 1e7f + near;
                    }
                    if (score < bestScore) { bestScore = score; best = c; found = true; }
                }
            return found && Mark(best);
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
                case Ore.Coal: type = ResourceType.Coal; amount = config.orePerTile; break;
                default: amount = config.stonePerTile; break;
            }
            ore[i] = Ore.None;
            var world = Destruction.PixelWorld.Current;
            if (world != null) world.Burst((Vector2)cell + new Vector2(0.5f, 0.5f), new Color32(110, 100, 88, 255), 10, 2.5f);
            // The tile and the rock round it look different now (the cut faces).
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                    PaintCell(cell + new Vector2Int(x, y));
            Upload();
            return true;
        }

        // ------------------------------------------------------------- saving --

        /// <summary>Which tiles are dug out, one character each ('1' dug, '0' rock), row by row from the bottom (for saving).</summary>
        public string DugText() => Bits(dug);
        /// <summary>Which tiles are marked to dig, like <see cref="DugText"/>.</summary>
        public string MarkedText() => Bits(marked);

        static string Bits(bool[] cells)
        {
            var text = new char[cells.Length];
            for (int i = 0; i < cells.Length; i++) text[i] = cells[i] ? '1' : '0';
            return new string(text);
        }

        /// <summary>Puts back what was dug and marked in a saved game (texts from <see cref="DugText"/> and <see cref="MarkedText"/>).</summary>
        public void Restore(string dugText, string markedText)
        {
            if (string.IsNullOrEmpty(dugText) || dugText.Length != dug.Length) return;
            for (int i = 0; i < dug.Length; i++)
            {
                if (dugText[i] != '1' || dug[i]) continue;
                dug[i] = true;
                ore[i] = Ore.None;
                nav.Unblock(new RectInt(bounds.x + i % bounds.width, bounds.y + i / bounds.width, 1, 1));
            }
            if (markedText != null && markedText.Length == marked.Length)
                for (int i = 0; i < marked.Length; i++)
                    if (markedText[i] == '1' && !dug[i] && !marked[i])
                    {
                        marked[i] = true;
                        Marked++;
                        ShowMark(new Vector2Int(bounds.x + i % bounds.width, bounds.y + i / bounds.width), true);
                    }
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                    PaintCell(new Vector2Int(x, y));
            Upload();
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
            piecesX = (bounds.width + PieceTiles - 1) / PieceTiles;
            piecesY = (bounds.height + PieceTiles - 1) / PieceTiles;
            int count = piecesX * piecesY;
            textures = new Texture2D[count];
            sprites = new Sprite[count];
            pieces = new Color32[count][];
            pieceChanged = new bool[count];
            for (int py = 0; py < piecesY; py++)
                for (int px = 0; px < piecesX; px++)
                {
                    int i = py * piecesX + px;
                    int w = PieceWidth(px) * Detail, h = PieceHeight(py) * Detail;
                    pieces[i] = new Color32[w * h];
                    textures[i] = PixelTextures.NewTexture(w, h, "Mine");
                }
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                    PaintCell(new Vector2Int(x, y));
            Upload();
            for (int py = 0; py < piecesY; py++)
                for (int px = 0; px < piecesX; px++)
                {
                    int i = py * piecesX + px;
                    sprites[i] = PixelTextures.NewSprite(textures[i], Vector2.zero, Detail);
                    var piece = new GameObject("Rock and floor");
                    piece.transform.SetParent(transform, false);
                    piece.transform.position = (Vector2)(bounds.position + new Vector2Int(px, py) * PieceTiles);
                    var r = piece.AddComponent<SpriteRenderer>();
                    r.sprite = sprites[i];
                    r.sortingOrder = SortOrder.Floors - 10;
                }
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

        int PieceWidth(int px) => Mathf.Min(PieceTiles, bounds.width - px * PieceTiles);
        int PieceHeight(int py) => Mathf.Min(PieceTiles, bounds.height - py * PieceTiles);

        // One tile (MineArt draws it), into the square of the picture it's in.
        void PaintCell(Vector2Int cell)
        {
            if (!Inside(cell)) return;
            int i = Index(cell);
            int tx = cell.x - bounds.x, ty = cell.y - bounds.y;
            int px = tx / PieceTiles, py = ty / PieceTiles, p = py * piecesX + px;
            MineArt.PaintCell(pieces[p], PieceWidth(px) * Detail, Detail, (tx - px * PieceTiles) * Detail, (ty - py * PieceTiles) * Detail, cell.x, cell.y, seed,
                dug[i], (int)ore[i], IsDug(cell + Vector2Int.left), IsDug(cell + Vector2Int.right), IsDug(cell + Vector2Int.up), IsDug(cell + Vector2Int.down));
            pieceChanged[p] = true;
        }

        // Sends the squares that were painted to the graphics card.
        void Upload()
        {
            for (int p = 0; p < pieces.Length; p++)
            {
                if (!pieceChanged[p]) continue;
                pieceChanged[p] = false;
                textures[p].SetPixels32(pieces[p]);
                textures[p].Apply(true);
            }
        }

        void OnDestroy()
        {
            nav?.Dispose();
            if (textures != null) foreach (var t in textures) if (t != null) Destroy(t);
            if (sprites != null) foreach (var s in sprites) if (s != null) Destroy(s);
        }
    }
}
