using System.Collections.Generic;
using KingdomGame.Art;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// Draws the roofs. Every roof (all the touching roof cells of the same kind) is one
    /// picture drawn for its shape (<see cref="RoofArt"/>), with its shadow on the ground.
    /// A roof fades away while the ruler is under it, so you can see inside the house
    /// you're in, and is see-through on the throne view, so you can furnish the rooms
    /// below. A roof is drawn again a moment after it changes (builders finishing it a
    /// cell at a time, a wall caving in).
    /// </summary>
    public sealed class RoofView : MonoBehaviour
    {
        const float InsideAlpha = 0.08f;
        const float ThroneAlpha = 0.3f;
        const float FadeSpeed = 4f;            // alpha per second
        const float RedrawDelay = 0.2f;        // seconds, so many changes at once make one drawing
        const int MaxPicture = 2048;           // pixels; a bigger roof is drawn with less detail
        const int ShadowDetail = 8;            // pixels per tile (shadows are blurry anyway)
        const int BuildingDetail = 16;         // pixels per tile while builders are still finishing it (it's drawn again each time a cell is done)

        sealed class Piece
        {
            public BuildingConfig Config;
            public readonly HashSet<Vector2Int> Cells = new HashSet<Vector2Int>();
            public SpriteRenderer Look, Shadow;
            public float Alpha = 1f;
        }

        Kingdom kingdom;
        int detail = 64;
        readonly List<Piece> pieces = new List<Piece>();
        readonly Dictionary<Vector2Int, Piece> pieceAt = new Dictionary<Vector2Int, Piece>();
        readonly HashSet<Vector2Int> touched = new HashSet<Vector2Int>();
        readonly List<Vector2Int> flood = new List<Vector2Int>();
        float redrawAt;

        /// <summary>How many separate roofs there are.</summary>
        public int Count => pieces.Count;

        public void Setup(Kingdom owner, int pixelsPerTile)
        {
            kingdom = owner;
            detail = Mathf.Clamp(pixelsPerTile, 16, 128);
        }

        /// <summary>A roof cell was built or removed here: the roofs around it are drawn again.</summary>
        public void Touch(Vector2Int cell)
        {
            if (touched.Count == 0) redrawAt = Time.time + RedrawDelay;
            touched.Add(cell);
        }

        void LateUpdate()
        {
            if (kingdom == null) return;
            if (touched.Count > 0 && Time.time >= redrawAt) Redraw();
            Fade();
        }

        // Draws again every roof at or next to a changed cell.
        void Redraw()
        {
            var seeds = new HashSet<Vector2Int>();
            foreach (var cell in touched)
                for (int i = 0; i < 5; i++)
                {
                    var c = cell + (i == 0 ? Vector2Int.zero : i == 1 ? Vector2Int.left : i == 2 ? Vector2Int.right : i == 3 ? Vector2Int.down : Vector2Int.up);
                    if (pieceAt.TryGetValue(c, out var old))
                    {
                        foreach (var oc in old.Cells) seeds.Add(oc);
                        Drop(old);
                    }
                    if (kingdom.RoofAt(c) != null) seeds.Add(c);
                }
            touched.Clear();

            var done = new HashSet<Vector2Int>();
            foreach (var seed in seeds)
            {
                if (done.Contains(seed)) continue;
                var roof = kingdom.RoofAt(seed);
                if (roof == null) continue;
                // Every touching cell of the same roof.
                var piece = new Piece { Config = roof.Config };
                flood.Clear();
                flood.Add(seed);
                done.Add(seed);
                for (int k = 0; k < flood.Count; k++)
                {
                    var c = flood[k];
                    piece.Cells.Add(c);
                    foreach (var n in new[] { c + Vector2Int.left, c + Vector2Int.right, c + Vector2Int.down, c + Vector2Int.up })
                    {
                        if (done.Contains(n)) continue;
                        var other = kingdom.RoofAt(n);
                        if (other == null || other.Config != roof.Config) continue;
                        done.Add(n);
                        flood.Add(n);
                    }
                }
                Draw(piece);
            }
        }

        void Draw(Piece piece)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var c in piece.Cells)
            {
                minX = Mathf.Min(minX, c.x); minY = Mathf.Min(minY, c.y);
                maxX = Mathf.Max(maxX, c.x); maxY = Mathf.Max(maxY, c.y);
            }
            // One empty cell all round, for the eaves.
            var origin = new Vector2Int(minX - 1, minY - 1);
            int w = maxX - minX + 3, h = maxY - minY + 3;
            var mask = new bool[w * h];
            foreach (var c in piece.Cells) mask[(c.y - origin.y) * w + (c.x - origin.x)] = true;
            int ppt = Mathf.Max(8, Mathf.Min(StillBuilding(piece) ? BuildingDetail : detail, MaxPicture / Mathf.Max(w, h)));
            uint seed = (uint)(origin.x * 73856093) ^ (uint)(origin.y * 19349663);
            var style = piece.Config.roofStyle;

            var image = RoofArt.Roof(mask, w, h, ppt, style, seed);
            piece.Look = Part("Roof", PixelTextures.ToSprite(image, piece.Config.displayName, false, ppt), SortOrder.Roofs, origin);
            var shadow = RoofArt.Shadow(mask, w, h, ShadowDetail);
            piece.Shadow = Part("Roof shadow", PixelTextures.ToSprite(shadow, piece.Config.displayName + " shadow", false, ShadowDetail),
                SortOrder.Shadows, origin + Look.ShadowDirection * RoofArt.Height(style));
            piece.Shadow.color = new Color(1f, 1f, 1f, Look.ShadowOpacity);
            // A new roof over the ruler starts faded, instead of flashing up.
            piece.Alpha = Target(piece);
            Apply(piece);

            pieces.Add(piece);
            foreach (var c in piece.Cells) pieceAt[c] = piece;
        }

        // Whether more of this roof is planned right next to it (then it's drawn quickly and roughly, and properly when the last cell is done).
        bool StillBuilding(Piece piece)
        {
            foreach (var c in piece.Cells)
                if (kingdom.RoofPlanned(c + Vector2Int.left, piece.Config) || kingdom.RoofPlanned(c + Vector2Int.right, piece.Config)
                    || kingdom.RoofPlanned(c + Vector2Int.down, piece.Config) || kingdom.RoofPlanned(c + Vector2Int.up, piece.Config))
                    return true;
            return false;
        }

        SpriteRenderer Part(string partName, Sprite sprite, int order, Vector2 at)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        void Drop(Piece piece)
        {
            pieces.Remove(piece);
            foreach (var c in piece.Cells)
                if (pieceAt.TryGetValue(c, out var p) && p == piece) pieceAt.Remove(c);
            Destroy(piece.Look);
            Destroy(piece.Shadow);
        }

        static void Destroy(SpriteRenderer r)
        {
            if (r == null) return;
            var sprite = r.sprite;
            if (sprite != null)
            {
                Object.Destroy(sprite.texture);
                Object.Destroy(sprite);
            }
            Object.Destroy(r.gameObject);
        }

        // ------------------------------------------------------------- fading --

        void Fade()
        {
            float step = Time.unscaledDeltaTime * FadeSpeed;
            foreach (var piece in pieces)
            {
                float target = Target(piece);
                if (Mathf.Approximately(piece.Alpha, target)) continue;
                piece.Alpha = Mathf.MoveTowards(piece.Alpha, target, step);
                Apply(piece);
            }
        }

        float Target(Piece piece)
        {
            if (kingdom.ThroneView) return ThroneAlpha;
            var ruler = kingdom.Ruler;
            return ruler != null && Covers(piece, ruler.position) ? InsideAlpha : 1f;
        }

        static void Apply(Piece piece)
        {
            if (piece.Look != null) piece.Look.color = new Color(1f, 1f, 1f, piece.Alpha);
        }

        // Whether the roof reaches over a point (its cells, and the eaves past them).
        static bool Covers(Piece piece, Vector2 p)
        {
            var cell = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var c = new Vector2Int(cell.x + dx, cell.y + dy);
                    if (!piece.Cells.Contains(c)) continue;
                    float ox = Mathf.Max(Mathf.Max(c.x - p.x, 0f), p.x - (c.x + 1));
                    float oy = Mathf.Max(Mathf.Max(c.y - p.y, 0f), p.y - (c.y + 1));
                    if (Mathf.Max(ox, oy) <= RoofArt.Eave) return true;
                }
            return false;
        }

        void OnDestroy()
        {
            foreach (var piece in pieces)
            {
                Destroy(piece.Look);
                Destroy(piece.Shadow);
            }
            pieces.Clear();
        }
    }
}
