using System.Collections.Generic;
using System.Threading.Tasks;
using KingdomGame.Art;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// The ground of an endless world, made where the camera looks. The ground layers (grass, dry
    /// grass, mud, and the ore fields' iron, copper, coal and stone) are tiled pictures that move
    /// with the camera; which of them shows where (the masks) and the soft tint over them are made in
    /// squares of <see cref="PieceTiles"/> tiles (ore masks only where there's ore). The work for a
    /// square runs on another thread (it's all plain maths); the pictures are put together on the main
    /// thread, a couple a frame (the first view all at once). Squares out of view go after a while; a
    /// square whose ore is mined down is made again. There's no ground past the world's edge (where
    /// house insides and mines are).
    /// </summary>
    public sealed class GroundStreamer : MonoBehaviour
    {
        public const int PieceTiles = 64;
        const int MaskDetail = 8;      // mask pixels per tile
        const int TintDetail = 2;      // tint pixels per tile
        const float KeepSeconds = 8f;
        const int MaxWorking = 4, MadePerFrame = 2;
        /// <summary>The most of the world shown across (the throne view zoomed far out).</summary>
        const float MaxView = 360f;

        sealed class Piece
        {
            public GameObject Root;
            public readonly List<Object> Made = new List<Object>();
            public float WantedAt;
            public int Ores;   // which ore layers it has masks for (bit k: OreKind k + 1)
        }

        const int FirstOreLayer = 3;

        sealed class Work
        {
            public byte[] Dry, Mud;
            public Color32[] Tint;
            public int[] Taken;                          // what's been mined round the square (copied on the main thread)
            public readonly byte[][] Ore = new byte[4][];   // by OreKind - 1 (null: none of it there)
        }

        uint seed;
        float treeLine, bushLine;
        System.Func<float, float, float> forest;
        Camera view;
        readonly Dictionary<Vector2Int, Piece> pieces = new Dictionary<Vector2Int, Piece>();
        readonly Dictionary<Vector2Int, Task<Work>> working = new Dictionary<Vector2Int, Task<Work>>();
        readonly HashSet<Vector2Int> wanted = new HashSet<Vector2Int>();
        readonly HashSet<Vector2Int> redraw = new HashSet<Vector2Int>();
        readonly List<Vector2Int> scratch = new List<Vector2Int>();
        OreMap ore;
        SpriteRenderer[] layers;
        float period = 1f;
        int startFrame;

        /// <summary>
        /// <paramref name="forestAt"/>: how much forest there is at a world position (0..1), with
        /// <paramref name="trees"/> and <paramref name="bushes"/> the levels where woods and bushes start.
        /// </summary>
        public void Setup(Camera camera, uint worldSeed, System.Func<float, float, float> forestAt, float trees, float bushes, OreMap oreMap = null)
        {
            view = camera;
            startFrame = Time.frameCount;
            seed = worldSeed;
            forest = forestAt;
            treeLine = trees;
            bushLine = bushes;
            layers = new[]
            {
                Layer("Grass", ArtKeys.GroundGrass, SortOrder.Ground, false),
                Layer("Dry grass", ArtKeys.GroundDry, SortOrder.Ground + 2, true),
                Layer("Mud", ArtKeys.GroundMud, SortOrder.Ground + 4, true),
                // Ore fields, each its own layer and masks (ranges Ground+4..+8, under the tint at Ground+10).
                Layer("Iron ore", ArtKeys.GroundOre(OreKind.Iron), SortOrder.Ground + 5, true),
                Layer("Copper ore", ArtKeys.GroundOre(OreKind.Copper), SortOrder.Ground + 6, true),
                Layer("Coal", ArtKeys.GroundOre(OreKind.Coal), SortOrder.Ground + 7, true),
                Layer("Stone ore", ArtKeys.GroundOre(OreKind.Stone), SortOrder.Ground + 8, true),
            };
            ore = oreMap;
            if (ore != null) ore.Changed += OreChanged;
            var grass = layers[0].sprite;
            if (grass != null) period = Mathf.Max(0.25f, grass.bounds.size.x);
        }

        SpriteRenderer Layer(string layerName, string key, int order, bool masked)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = ArtSprites.Get(key);
            r.drawMode = SpriteDrawMode.Tiled;
            r.tileMode = SpriteTileMode.Continuous;
            r.sortingOrder = order;
            if (masked) r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            return r;
        }

        static int FloorDiv(int v, int d) => v >= 0 ? v / d : -((-v + d - 1) / d);

        // A tile's ore looks different now: its square is made again (and a neighbour's, when it's at the edge: their
        // masks blend across it).
        void OreChanged(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    redraw.Add(new Vector2Int(FloorDiv(x + dx, PieceTiles), FloorDiv(y + dy, PieceTiles)));
        }

        void LateUpdate()
        {
            if (view == null) return;
            // The tiled layers follow the camera, moved only in whole repeats of their picture so the pattern stays put.
            float halfH = Mathf.Min(view.orthographicSize, MaxView * 0.5f) + period * 2f;
            float halfW = Mathf.Min(view.orthographicSize * view.aspect, MaxView * 0.5f) + period * 2f;
            Vector2 at = view.transform.position;
            // The view, cut to the world (nothing at all beyond its edge: in a house or a mine).
            const float edge = NatureMap.Limit;
            float vx0 = Mathf.Max(-edge, at.x - halfW), vx1 = Mathf.Min(edge, at.x + halfW);
            float vy0 = Mathf.Max(-edge, at.y - halfH), vy1 = Mathf.Min(edge, at.y + halfH);
            bool any = vx0 < vx1 && vy0 < vy1;
            var corner = new Vector2(Mathf.Floor(vx0 / period) * period, Mathf.Floor(vy0 / period) * period);
            var size = new Vector2(Mathf.Ceil((vx1 - corner.x) / period) * period, Mathf.Ceil((vy1 - corner.y) / period) * period);
            // An ore layer is only drawn while a square in view has that ore (each covers the whole view otherwise).
            int ores = 0;
            foreach (var pair in pieces) if (pair.Value.Ores != 0 && pair.Value.WantedAt >= Time.unscaledTime - 0.5f) ores |= pair.Value.Ores;
            for (int i = 0; i < layers.Length; i++)
            {
                var r = layers[i];
                if (r == null) continue;
                bool show = any && (i < FirstOreLayer || (ores & (1 << (i - FirstOreLayer))) != 0);
                if (r.enabled != show) r.enabled = show;
                if (!show) continue;
                // Tiled sprites are laid out from their pivot; the ground's pivot is its bottom-left corner.
                r.transform.position = new Vector3(corner.x, corner.y, 0f);
                r.size = size;
            }

            // Which squares are in view (with one more all round, but none past the edge).
            wanted.Clear();
            float now = Time.unscaledTime;
            // The first few frames (the game is starting, the camera finding its place): what's in view is made at once.
            bool starting = Time.frameCount - startFrame < 3;
            if (any)
            {
                int lo = FloorDiv(-NatureMap.Limit, PieceTiles), hi = FloorDiv(NatureMap.Limit - 1, PieceTiles);
                int x0 = Mathf.Max(lo, FloorDiv(Mathf.FloorToInt(vx0), PieceTiles) - 1), x1 = Mathf.Min(hi, FloorDiv(Mathf.FloorToInt(vx1), PieceTiles) + 1);
                int y0 = Mathf.Max(lo, FloorDiv(Mathf.FloorToInt(vy0), PieceTiles) - 1), y1 = Mathf.Min(hi, FloorDiv(Mathf.FloorToInt(vy1), PieceTiles) + 1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var key = new Vector2Int(x, y);
                        wanted.Add(key);
                        if (pieces.TryGetValue(key, out var piece)) piece.WantedAt = now;
                        else if (starting && OnScreen(key, vx0, vx1, vy0, vy1)) pieces[key] = Build(key, Make(key, TakenRound(key)));
                        else if (!working.ContainsKey(key) && working.Count < MaxWorking) Begin(key);
                    }
            }

            // Squares whose ore changed are made again (those not shown are simply made fresh when they're next in view).
            if (redraw.Count > 0)
            {
                scratch.Clear();
                foreach (var key in redraw)
                {
                    if (working.ContainsKey(key)) continue;   // being made from what was mined before: made again once it's done
                    if (!pieces.ContainsKey(key)) { scratch.Add(key); continue; }
                    if (working.Count >= MaxWorking) continue;
                    Begin(key);
                    scratch.Add(key);
                }
                foreach (var key in scratch) redraw.Remove(key);
            }

            // Finished work becomes pictures, a couple a frame.
            int made = 0;
            scratch.Clear();
            foreach (var pair in working)
                if (pair.Value.IsCompleted) scratch.Add(pair.Key);
            foreach (var key in scratch)
            {
                var task = working[key];
                if (made >= MadePerFrame) break;
                working.Remove(key);
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"Making the ground at {key} failed: {task.Exception?.GetBaseException().Message}");
                    continue;
                }
                if (!wanted.Contains(key))
                {
                    Drop(key);   // the camera moved on meanwhile (a square being made again is out of date: it goes too)
                    continue;
                }
                var fresh = Build(key, task.Result);
                Drop(key);   // (a square made again: the old one goes once the new one is there)
                pieces[key] = fresh;
                made++;
            }

            // Squares nobody has looked at for a while go.
            scratch.Clear();
            foreach (var pair in pieces)
                if (now - pair.Value.WantedAt > KeepSeconds) scratch.Add(pair.Key);
            foreach (var key in scratch) Drop(key);
        }

        static bool OnScreen(Vector2Int key, float x0, float x1, float y0, float y1) =>
            (key.x + 1) * PieceTiles > x0 && key.x * PieceTiles < x1 && (key.y + 1) * PieceTiles > y0 && key.y * PieceTiles < y1;

        void Begin(Vector2Int key)
        {
            var taken = TakenRound(key);   // (read here, on the main thread)
            working[key] = Task.Run(() => Make(key, taken));
        }

        // What's been mined from the square's tiles and one more all round (null: nothing).
        int[] TakenRound(Vector2Int key) =>
            ore?.TakenIn(key.x * PieceTiles - 1, key.y * PieceTiles - 1, PieceTiles + 2, PieceTiles + 2);

        // The maths for a square (plain C#, only reads fields set once in Setup and what it's given: safe on another thread).
        Work Make(Vector2Int key, int[] taken)
        {
            int ox = key.x * PieceTiles, oy = key.y * PieceTiles;
            int n = PieceTiles * MaskDetail, t = PieceTiles * TintDetail;
            var work = new Work { Dry = new byte[n * n], Mud = new byte[n * n], Tint = new Color32[t * t], Taken = taken };
            GroundArt.PatchMasksAt(ox, oy, PieceTiles, MaskDetail, seed, work.Dry, work.Mud);
            GroundArt.TintAt(ox, oy, PieceTiles, TintDetail, seed, forest, treeLine, bushLine, work.Tint);
            if (ore != null) GroundArt.OreMasksAt(ox, oy, PieceTiles, MaskDetail, ore.Seed, taken, work.Ore);
            return work;
        }

        Piece Build(Vector2Int key, Work work)
        {
            var piece = new Piece { WantedAt = Time.unscaledTime };
            piece.Root = new GameObject($"Ground {key.x},{key.y}");
            piece.Root.transform.SetParent(transform, false);
            piece.Root.transform.position = new Vector3(key.x * PieceTiles, key.y * PieceTiles, 0f);
            int n = PieceTiles * MaskDetail;
            Mask(piece, "Dry grass mask", work.Dry, n, SortOrder.Ground + 1, SortOrder.Ground + 2);
            Mask(piece, "Mud mask", work.Mud, n, SortOrder.Ground + 3, SortOrder.Ground + 4);
            for (int k = 0; k < work.Ore.Length; k++)
            {
                if (work.Ore[k] == null) continue;
                Mask(piece, $"{(OreKind)(k + 1)} ore mask", work.Ore[k], n, SortOrder.Ground + 4 + k, SortOrder.Ground + 5 + k);
                piece.Ores |= 1 << k;
            }

            int t = PieceTiles * TintDetail;
            var texture = PixelTextures.NewTexture(t, t, "Ground tint", false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(work.Tint);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, t, t), Vector2.zero, TintDetail, 0, SpriteMeshType.FullRect);
            var tint = new GameObject("Ground tint");
            tint.transform.SetParent(piece.Root.transform, false);
            var r = tint.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = SortOrder.GroundTint;
            piece.Made.Add(texture);
            piece.Made.Add(sprite);
            return piece;
        }

        // A sprite mask over the square: it only affects the layer between back and front order.
        void Mask(Piece piece, string maskName, byte[] alpha, int size, int back, int front)
        {
            var texture = new Texture2D(size, size, TextureFormat.Alpha8, true)
            {
                name = maskName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixelData(alpha, 0);
            texture.Apply(true, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.zero, MaskDetail, 0, SpriteMeshType.FullRect);
            var go = new GameObject(maskName);
            go.transform.SetParent(piece.Root.transform, false);
            var mask = go.AddComponent<SpriteMask>();
            mask.sprite = sprite;
            mask.alphaCutoff = 0.5f;
            mask.isCustomRangeActive = true;
            mask.backSortingLayerID = 0;
            mask.frontSortingLayerID = 0;
            mask.backSortingOrder = back;
            mask.frontSortingOrder = front;
            piece.Made.Add(texture);
            piece.Made.Add(sprite);
        }

        void Drop(Vector2Int key)
        {
            if (!pieces.TryGetValue(key, out var piece)) return;
            pieces.Remove(key);
            foreach (var o in piece.Made) if (o != null) Destroy(o);
            if (piece.Root != null) Destroy(piece.Root);
        }

        void OnDestroy()
        {
            if (ore != null) ore.Changed -= OreChanged;
            foreach (var key in new List<Vector2Int>(pieces.Keys)) Drop(key);
        }
    }
}
