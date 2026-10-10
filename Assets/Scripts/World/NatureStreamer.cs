using System;
using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// Makes the trees, rocks and bushes of the <see cref="NatureMap"/> into real objects
    /// where they're needed: round the camera's view, round the people it follows (rulers),
    /// and over the areas it's given (kingdoms, where villagers gather). Squares of the map
    /// nobody needs any more are taken away again a few seconds later, after writing their
    /// state back into the map. Works in squares of <see cref="ChunkTiles"/> tiles, a few
    /// per frame, so walking about doesn't stutter.
    /// </summary>
    public sealed class NatureStreamer : MonoBehaviour
    {
        public const int ChunkTiles = 16;
        const float CheckSeconds = 0.25f, KeepSeconds = 6f;
        const int ChunksPerFrame = 2;

        NatureMap map;
        NodeSprites[,] looks;
        CanopyFader fader;
        int chunksX, chunksY;
        readonly Dictionary<int, float> shown = new Dictionary<int, float>();   // chunk -> last time it was wanted
        readonly HashSet<int> wanted = new HashSet<int>();
        readonly Queue<int> toShow = new Queue<int>();
        readonly HashSet<int> queued = new HashSet<int>();
        readonly List<int> scratch = new List<int>();
        float checkAt;

        /// <summary>Follow these (rulers): the map round each is shown out to <see cref="FollowRadius"/> tiles.</summary>
        public readonly List<Transform> Followed = new List<Transform>();
        public float FollowRadius { get; set; } = 28f;
        /// <summary>The camera whose view is shown (with a margin), or null (a server has none).</summary>
        public Camera View { get; set; }
        /// <summary>Areas always shown (kingdoms: where their villagers gather), asked for every check.</summary>
        public readonly List<Func<RectInt>> Areas = new List<Func<RectInt>>();
        /// <summary>The camera's view is shown at most this many tiles across (the throne view zoomed far out).</summary>
        public float MaxViewTiles { get; set; } = 220f;

        public NatureMap Map => map;
        public int ShownChunks => shown.Count;

        public void Setup(NatureMap natureMap, NodeSprites[,] nodeLooks, CanopyFader canopies)
        {
            map = natureMap;
            looks = nodeLooks;
            fader = canopies;
            chunksX = (map.Cells.width + ChunkTiles - 1) / ChunkTiles;
            chunksY = (map.Cells.height + ChunkTiles - 1) / ChunkTiles;
            map.Planted += OnPlanted;
        }

        void OnDestroy()
        {
            if (map != null) map.Planted -= OnPlanted;
        }

        /// <summary>Shows everything that's wanted right now, at once (the start, or after loading).</summary>
        public void ShowNow()
        {
            FindWanted();
            foreach (int c in wanted) if (!shown.ContainsKey(c)) Show(c);
            toShow.Clear();
            queued.Clear();
        }

        void Update()
        {
            if (map == null) return;
            if (Time.unscaledTime >= checkAt)
            {
                checkAt = Time.unscaledTime + CheckSeconds;
                FindWanted();
                float now = Time.unscaledTime;
                foreach (int c in wanted)
                {
                    if (shown.ContainsKey(c)) shown[c] = now;
                    else if (queued.Add(c)) toShow.Enqueue(c);
                }
                // Squares nobody has wanted for a while go.
                scratch.Clear();
                foreach (var pair in shown)
                    if (now - pair.Value > KeepSeconds) scratch.Add(pair.Key);
                foreach (int c in scratch) Hide(c);
            }
            for (int n = 0; n < ChunksPerFrame && toShow.Count > 0; n++)
            {
                int c = toShow.Dequeue();
                queued.Remove(c);
                if (!shown.ContainsKey(c) && wanted.Contains(c)) Show(c);
            }
        }

        void FindWanted()
        {
            wanted.Clear();
            if (View != null)
            {
                float halfH = Mathf.Min(View.orthographicSize, MaxViewTiles * 0.5f);
                float halfW = Mathf.Min(halfH * View.aspect, MaxViewTiles * 0.5f);
                var at = (Vector2)View.transform.position;
                Want(new Rect(at.x - halfW - 6f, at.y - halfH - 6f, halfW * 2f + 12f, halfH * 2f + 12f));
            }
            foreach (var t in Followed)
                if (t != null)
                {
                    var at = (Vector2)t.position;
                    Want(new Rect(at.x - FollowRadius, at.y - FollowRadius, FollowRadius * 2f, FollowRadius * 2f));
                }
            foreach (var area in Areas)
            {
                var r = area();
                if (r.width > 0 && r.height > 0) Want(new Rect(r.x, r.y, r.width, r.height));
            }
        }

        void Want(Rect r)
        {
            var cells = map.Cells;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((r.xMin - cells.x) / ChunkTiles));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((r.yMin - cells.y) / ChunkTiles));
            int x1 = Mathf.Min(chunksX - 1, Mathf.FloorToInt((r.xMax - cells.x) / ChunkTiles));
            int y1 = Mathf.Min(chunksY - 1, Mathf.FloorToInt((r.yMax - cells.y) / ChunkTiles));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    wanted.Add(y * chunksX + x);
        }

        RectInt ChunkCells(int chunk)
        {
            int cx = chunk % chunksX, cy = chunk / chunksX;
            var cells = map.Cells;
            int x = cells.x + cx * ChunkTiles, y = cells.y + cy * ChunkTiles;
            return new RectInt(x, y, Mathf.Min(ChunkTiles, cells.xMax - x), Mathf.Min(ChunkTiles, cells.yMax - y));
        }

        int ChunkOf(Vector2Int cell) =>
            (cell.y - map.Cells.y) / ChunkTiles * chunksX + (cell.x - map.Cells.x) / ChunkTiles;

        void Show(int chunk)
        {
            shown[chunk] = Time.unscaledTime;
            foreach (var cell in ChunkCells(chunk).allPositionsWithin) Make(cell);
        }

        void Hide(int chunk)
        {
            var area = ChunkCells(chunk);
            // Someone is gathering here: keep it a little longer.
            foreach (var cell in area.allPositionsWithin)
            {
                var node = map.Live(cell);
                if (node != null && node.Gatherer != null)
                {
                    shown[chunk] = Time.unscaledTime;
                    return;
                }
            }
            shown.Remove(chunk);
            foreach (var cell in area.allPositionsWithin)
            {
                var node = map.Live(cell);
                if (node == null) continue;
                node.WriteBack();
                node.Unbind();
                Destroy(node.gameObject);
            }
        }

        // A tree was planted (or put back from a save) in a square that's shown: make it now.
        void OnPlanted(Vector2Int cell)
        {
            if (!map.Inside(cell) || !shown.ContainsKey(ChunkOf(cell))) return;
            if (map.Live(cell) == null) Make(cell);
        }

        void Make(Vector2Int cell)
        {
            int i = map.Index(cell);
            if (!map.Has(i) || map.Live(cell) != null) return;
            var kind = map.KindOf(i);
            var position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            if (!map.IsPlanted(i))
            {
                // Jitter inside the tile so the map doesn't look like a grid (the same every time).
                position.x += (Noise.Hash01(map.Seed + 3u, cell.x, cell.y) - 0.5f) * 0.6f;
                position.y += (Noise.Hash01(map.Seed + 4u, cell.x, cell.y) - 0.5f) * 0.6f;
            }
            var go = new GameObject(kind.name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var node = go.AddComponent<ResourceNode>();
            int variant = Mathf.Clamp(map.VariantAt(i), 0, looks.GetLength(1) - 1);
            // Not turned: the light is drawn into the pictures (from the top-left), and turning would turn it too.
            node.Setup(kind, looks[(int)kind.look, variant], 0f);
            node.Bind(map, i);
            if (fader != null) fader.Add(node);
        }
    }
}
