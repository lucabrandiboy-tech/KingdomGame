using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// One 64 x 64-pixel tile of a wall (one grid cell). It owns the pixels'
    /// colors and health, the picture on screen (and its shadow) and the collider.
    ///
    /// Undamaged, it shares its tile picture (the baked full-detail art) with
    /// every other undamaged chunk of the same material (cheap). The first hit
    /// gives it its own, half-as-detailed picture (128 pixels across, twice the
    /// breakable pixels, so holes have smooth edges). After damage only the
    /// changed part of this chunk's picture is redrawn and only its collider is
    /// rebuilt, once per frame (<see cref="Flush"/>).
    /// </summary>
    public sealed class PixelChunk
    {
        public const int Size = PixelArt.PixelsPerUnit;
        public const int Area = Size * Size;

        // Shared scratch space for collider rebuilds (only ever used on the main thread).
        static readonly PhysicsShapeGroup2D shapes = new PhysicsShapeGroup2D(16, 64);
        static readonly List<PixelRect> rects = new List<PixelRect>();
        static readonly bool[] visited = new bool[Area];

        public readonly Vector2Int Cell;
        public readonly DestructibleStructure Owner;
        public readonly GameObject GameObject;

        readonly TileVariant variant;
        readonly SpriteRenderer spriteRenderer;
        readonly SpriteRenderer shadowRenderer;
        readonly CustomCollider2D wallCollider;

        Color32[] colors;   // null while undamaged (the variant's pixels are used)
        byte[] health;      // null while undamaged
        Color32[] picture;  // the damaged chunk's picture, WallTiles.DetailSize across; null while undamaged
        Texture2D texture;
        Sprite sprite;
        bool pictureChanged, shapeChanged;
        int dirtyMinX = int.MaxValue, dirtyMinY = int.MaxValue, dirtyMaxX = -1, dirtyMaxY = -1;

        /// <summary>Set by <see cref="PixelWorld"/> while this chunk waits for <see cref="Flush"/>.</summary>
        internal bool Queued;
        /// <summary>Set once the chunk has been taken out of the world (empty or destroyed).</summary>
        internal bool Removed;

        public int SolidCount { get; private set; }
        public bool IsDamaged => health != null;

        public PixelChunk(Vector2Int cell, DestructibleStructure owner, TileVariant variant, float wallHeight)
        {
            Cell = cell;
            Owner = owner;
            this.variant = variant;
            SolidCount = variant.SolidCount;

            GameObject = new GameObject($"Chunk {cell.x},{cell.y}");
            GameObject.transform.SetParent(owner.transform, false);
            GameObject.transform.position = new Vector3(cell.x, cell.y, 0);

            // A static body of its own makes the collider's shapes use this
            // chunk's position, with no room for doubt about which space they are in.
            var body = GameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            spriteRenderer = GameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = variant.Sprite;
            spriteRenderer.sortingOrder = SortOrder.Walls;

            // The wall's shadow: the same picture in black, shifted away from the light.
            // It shows the holes too, because it always uses the chunk's current picture.
            shadowRenderer = Look.AddShadow(GameObject.transform, variant.Sprite, wallHeight);
            shadowRenderer.color = new Color(0f, 0f, 0f, Look.ShadowOpacity);

            wallCollider = GameObject.AddComponent<CustomCollider2D>();
            SetShapes(variant.ColliderRects);
        }

        static int Index(int x, int y) => y * Size + x;

        public bool IsSolid(int x, int y) => (health ?? variant.Health)[Index(x, y)] > 0;

        public Color32 ColorAt(int x, int y) => (colors ?? variant.Colors)[Index(x, y)];

        /// <summary>
        /// Takes <paramref name="amount"/> health off one pixel (local 0..63
        /// coordinates). Returns true if that broke the pixel off, with its color.
        /// </summary>
        public bool Damage(int x, int y, int amount, out Color32 brokenColor)
        {
            brokenColor = default;
            int i = Index(x, y);
            if ((health ?? variant.Health)[i] == 0) return false;

            MakeOwnCopy();
            int left = health[i] - amount;
            if (left > 0)
            {
                // Cracked but standing: darker once it's below half health.
                int full = variant.Health[i];
                if (left * 2 < full && health[i] * 2 >= full)
                {
                    colors[i] = WallTiles.Shade(colors[i], -26);
                    MarkDirty(x, y);
                }
                health[i] = (byte)left;
                return false;
            }

            brokenColor = colors[i];
            RemoveAt(i);
            return true;
        }

        /// <summary>Breaks a pixel off regardless of its health (crumbling walls).</summary>
        public bool Break(int x, int y, out Color32 brokenColor)
        {
            brokenColor = default;
            int i = Index(x, y);
            if ((health ?? variant.Health)[i] == 0) return false;
            MakeOwnCopy();
            brokenColor = colors[i];
            RemoveAt(i);
            return true;
        }

        void RemoveAt(int i)
        {
            health[i] = 0;
            colors[i].a = 0;
            SolidCount--;
            shapeChanged = true;
            MarkDirty(i % Size, i / Size);
        }

        void MarkDirty(int x, int y)
        {
            pictureChanged = true;
            if (x < dirtyMinX) dirtyMinX = x;
            if (y < dirtyMinY) dirtyMinY = y;
            if (x > dirtyMaxX) dirtyMaxX = x;
            if (y > dirtyMaxY) dirtyMaxY = y;
        }

        // Redraws the part of the damaged picture around the changed pixels. The
        // picture is twice as detailed as the breakable pixels, so the edges of
        // holes are worked out smoothly from the pixels around them; broken edges
        // and cracked stone are darker.
        void RedrawPicture()
        {
            const int detail = WallTiles.DetailSize / Size;
            int x0 = Mathf.Max(0, (dirtyMinX - 1) * detail), y0 = Mathf.Max(0, (dirtyMinY - 1) * detail);
            int x1 = Mathf.Min(WallTiles.DetailSize - 1, (dirtyMaxX + 2) * detail - 1), y1 = Mathf.Min(WallTiles.DetailSize - 1, (dirtyMaxY + 2) * detail - 1);
            for (int py = y0; py <= y1; py++)
                for (int px = x0; px <= x1; px++)
                {
                    // How solid it is here, blended from the four nearest breakable pixels.
                    float gx = (px + 0.5f) / detail - 0.5f, gy = (py + 0.5f) / detail - 0.5f;
                    int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
                    float fx = gx - ix, fy = gy - iy;
                    float solid = Solid(ix, iy) * (1 - fx) * (1 - fy) + Solid(ix + 1, iy) * fx * (1 - fy)
                                + Solid(ix, iy + 1) * (1 - fx) * fy + Solid(ix + 1, iy + 1) * fx * fy;
                    int pi = py * WallTiles.DetailSize + px;
                    var c = variant.Detail[pi];
                    float alpha = Smooth01((solid - 0.35f) / 0.3f);
                    if (alpha > 0f)
                    {
                        int gi = Mathf.Clamp(py / detail, 0, Size - 1) * Size + Mathf.Clamp(px / detail, 0, Size - 1);
                        float shade = 1f;
                        if (health[gi] > 0 && health[gi] * 2 < variant.Health[gi]) shade *= 0.72f;         // cracked
                        shade *= Mathf.Lerp(0.55f, 1f, Smooth01((solid - 0.5f) / 0.45f));                 // rough broken edge
                        c = new Color32((byte)(c.r * shade), (byte)(c.g * shade), (byte)(c.b * shade), 255);
                    }
                    c.a = (byte)Mathf.RoundToInt(alpha * 255f);
                    picture[pi] = c;
                }
            dirtyMinX = dirtyMinY = int.MaxValue;
            dirtyMaxX = dirtyMaxY = -1;
        }

        float Solid(int x, int y)
        {
            x = Mathf.Clamp(x, 0, Size - 1);
            y = Mathf.Clamp(y, 0, Size - 1);
            return health[y * Size + x] > 0 ? 1f : 0f;
        }

        static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Writes this chunk's solid pixels (1 = solid) into a bigger mask,
        /// with this chunk's bottom-left pixel at (offsetX, offsetY).
        /// </summary>
        public void CopyMask(byte[] mask, int maskWidth, int offsetX, int offsetY)
        {
            var source = health ?? variant.Health;
            for (int y = 0; y < Size; y++)
            {
                int from = y * Size;
                int to = (offsetY + y) * maskWidth + offsetX;
                for (int x = 0; x < Size; x++)
                    mask[to + x] = source[from + x] > 0 ? (byte)1 : (byte)0;
            }
        }

        /// <summary>Uploads the new picture and rebuilds the collider if they changed.</summary>
        public void Flush()
        {
            if (pictureChanged && texture != null)
            {
                RedrawPicture();
                texture.SetPixels32(picture);
                texture.Apply(true);
            }
            if (shapeChanged)
            {
                PixelAlgorithms.Rectangles(health, Size, Size, visited, rects);
                SetShapes(rects);
            }
            pictureChanged = shapeChanged = false;
        }

        public void Destroy()
        {
            Object.Destroy(GameObject);
            if (sprite != null) Object.Destroy(sprite);
            if (texture != null) Object.Destroy(texture);
        }

        // First hit: stop sharing the tile picture and take a private copy.
        void MakeOwnCopy()
        {
            if (health != null) return;
            health = (byte[])variant.Health.Clone();
            colors = (Color32[])variant.Colors.Clone();
            picture = (Color32[])variant.Detail.Clone();
            texture = PixelTextures.NewTexture(WallTiles.DetailSize, WallTiles.DetailSize, GameObject.name);
            texture.SetPixels32(picture);
            texture.Apply(true);
            sprite = PixelTextures.NewSprite(texture, Vector2.zero, WallTiles.DetailSize, variant.DetailNormals);
            spriteRenderer.sprite = sprite;
            shadowRenderer.sprite = sprite;
            Owner.World.OnChunkFirstDamaged();
        }

        void SetShapes(List<PixelRect> boxes)
        {
            if (boxes.Count == 0)
            {
                wallCollider.ClearCustomShapes();
                return;
            }

            shapes.Clear();
            const float unit = 1f / Size;
            foreach (var r in boxes)
            {
                shapes.AddBox(
                    new Vector2((r.X + r.Width * 0.5f) * unit, (r.Y + r.Height * 0.5f) * unit),
                    new Vector2(r.Width * unit, r.Height * unit));
            }
            wallCollider.SetCustomShapes(shapes);
        }
    }
}
