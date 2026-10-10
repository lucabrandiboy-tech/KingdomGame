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
        readonly Dictionary<Vector2Int, float> shown = new Dictionary<Vector2Int, float>();   // square -> last time it was wanted
        readonly HashSet<Vector2Int> wanted = new HashSet<Vector2Int>();
        readonly Queue<Vector2Int> toShow = new Queue<Vector2Int>();
        readonly HashSet<Vector2Int> queued = new HashSet<Vector2Int>();
        readonly List<Vector2Int> scratch = new List<Vector2Int>();
        readonly List<RectInt> areas = new List<RectInt>();
        // Squares asked for one at a time (a villager going to a tree there), and until when.
        readonly Dictionary<Vector2Int, float> pinned = new Dictionary<Vector2Int, float>();
        float checkAt;

        /// <summary>Follow these (rulers): the map round each is shown out to <see cref="FollowRadius"/> tiles.</summary>
        public readonly List<Transform> Followed = new List<Transform>();
        public float FollowRadius { get; set; } = 28f;
        /// <summary>The camera whose view is shown (with a margin), or null (a server has none).</summary>
        public Camera View { get; set; }
        /// <summary>Areas always shown (round each kingdom's buildings, where their villagers work): each adds its areas to the list it's given, every check.</summary>
        public readonly List<Action<List<RectInt>>> Areas = new List<Action<List<RectInt>>>();
        /// <summary>The camera's view is shown at most this many tiles across (the throne view zoomed far out).</summary>
        public float MaxViewTiles { get; set; } = 220f;

        public NatureMap Map => map;
        public int ShownChunks => shown.Count;

        public void Setup(NatureMap natureMap, NodeSprites[,] nodeLooks, CanopyFader canopies)
        {
            map = natureMap;
            looks = nodeLooks;
            fader = canopies;
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
            foreach (var c in wanted) if (!shown.ContainsKey(c)) Show(c);
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
                foreach (var c in wanted)
                {
                    if (shown.ContainsKey(c)) shown[c] = now;
                    else if (queued.Add(c)) toShow.Enqueue(c);
                }
                // Squares nobody has wanted for a while go.
                scratch.Clear();
                foreach (var pair in shown)
                    if (now - pair.Value > KeepSeconds) scratch.Add(pair.Key);
                foreach (var c in scratch) Hide(c);
            }
            for (int n = 0; n < ChunksPerFrame && toShow.Count > 0; n++)
            {
                var c = toShow.Dequeue();
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
            areas.Clear();
            foreach (var add in Areas) add(areas);
            foreach (var r in areas)
                if (r.width > 0 && r.height > 0) Want(new Rect(r.x, r.y, r.width, r.height));
            if (pinned.Count > 0)
            {
                scratch.Clear();
                foreach (var pair in pinned)
                    if (pair.Value < Time.unscaledTime) scratch.Add(pair.Key);
                    else wanted.Add(pair.Key);
                foreach (var c in scratch) pinned.Remove(c);
            }
        }

        /// <summary>
        /// Makes the square round <paramref name="cell"/> real now and keeps it a minute (a villager is going
        /// to a tree there), and returns that cell's node object (null if nothing stands there).
        /// </summary>
        public ResourceNode ShowAt(Vector2Int cell)
        {
            if (map == null || !NatureMap.Inside(cell)) return null;
            var chunk = ChunkOf(cell);
            pinned[chunk] = Time.unscaledTime + 60f;
            if (!shown.ContainsKey(chunk)) Show(chunk);
            else shown[chunk] = Time.unscaledTime;
            return map.Live(cell);
        }

        static int FloorDiv(int v, int d) => v >= 0 ? v / d : -((-v + d - 1) / d);

        void Want(Rect r)
        {
            // Not past the world's edge.
            float lim = NatureMap.Limit;
            int x0 = FloorDiv(Mathf.FloorToInt(Mathf.Max(r.xMin, -lim)), ChunkTiles), y0 = FloorDiv(Mathf.FloorToInt(Mathf.Max(r.yMin, -lim)), ChunkTiles);
            int x1 = FloorDiv(Mathf.FloorToInt(Mathf.Min(r.xMax, lim)), ChunkTiles), y1 = FloorDiv(Mathf.FloorToInt(Mathf.Min(r.yMax, lim)), ChunkTiles);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    wanted.Add(new Vector2Int(x, y));
        }

        static RectInt ChunkCells(Vector2Int chunk) => new RectInt(chunk.x * ChunkTiles, chunk.y * ChunkTiles, ChunkTiles, ChunkTiles);

        static Vector2Int ChunkOf(Vector2Int cell) => new Vector2Int(FloorDiv(cell.x, ChunkTiles), FloorDiv(cell.y, ChunkTiles));

        void Show(Vector2Int chunk)
        {
            shown[chunk] = Time.unscaledTime;
            foreach (var cell in ChunkCells(chunk).allPositionsWithin) Make(cell);
        }

        void Hide(Vector2Int chunk)
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
            if (!NatureMap.Inside(cell) || !shown.ContainsKey(ChunkOf(cell))) return;
            if (map.Live(cell) == null) Make(cell);
        }

        void Make(Vector2Int cell)
        {
            var kind = map.KindAt(cell);
            if (kind == null || map.Live(cell) != null) return;
            var position = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
            if (!map.IsPlanted(cell))
            {
                // Jitter inside the tile so the map doesn't look like a grid (the same every time).
                position.x += (Noise.Hash01(map.Seed + 3u, cell.x, cell.y) - 0.5f) * 0.6f;
                position.y += (Noise.Hash01(map.Seed + 4u, cell.x, cell.y) - 0.5f) * 0.6f;
            }
            var go = new GameObject(kind.name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var node = go.AddComponent<ResourceNode>();
            int variant = Mathf.Clamp(map.VariantAt(cell), 0, looks.GetLength(1) - 1);
            // Not turned: the light is drawn into the pictures (from the top-left), and turning would turn it too.
            node.Setup(kind, looks[(int)kind.look, variant], 0f);
            node.Bind(map, cell);
            if (fader != null) fader.Add(node);
        }
    }
}
