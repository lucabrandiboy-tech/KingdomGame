using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// One 32 x 32-pixel tile of a wall (one grid cell). It owns the pixels'
    /// colors and health, the picture on screen and the collider.
    ///
    /// Undamaged, it shares its tile picture with every other undamaged chunk
    /// of the same material (cheap). The first hit gives it its own copy. After
    /// damage only this chunk's picture is re-uploaded and only its collider is
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
        readonly CustomCollider2D wallCollider;

        Color32[] colors;   // null while undamaged (the variant's pixels are used)
        byte[] health;      // null while undamaged
        Texture2D texture;
        Sprite sprite;
        bool pictureChanged, shapeChanged;

        /// <summary>Set by <see cref="PixelWorld"/> while this chunk waits for <see cref="Flush"/>.</summary>
        internal bool Queued;
        /// <summary>Set once the chunk has been taken out of the world (empty or destroyed).</summary>
        internal bool Removed;

        public int SolidCount { get; private set; }
        public bool IsDamaged => health != null;

        public PixelChunk(Vector2Int cell, DestructibleStructure owner, TileVariant variant, int sortingOrder)
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
            spriteRenderer.sortingOrder = sortingOrder;

            wallCollider = GameObject.AddComponent<CustomCollider2D>();
            SetShapes(variant.ColliderRects);
        }

        static int Index(int x, int y) => y * Size + x;

        public bool IsSolid(int x, int y) => (health ?? variant.Health)[Index(x, y)] > 0;

        public Color32 ColorAt(int x, int y) => (colors ?? variant.Colors)[Index(x, y)];

        /// <summary>
        /// Takes <paramref name="amount"/> health off one pixel (local 0..31
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
                // Cracked but standing: darken once it's below half health.
                int full = variant.Health[i];
                if (left * 2 < full && health[i] * 2 >= full)
                {
                    colors[i] = WallTiles.Shade(colors[i], -26);
                    pictureChanged = true;
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
            colors[i] = default; // fully transparent
            SolidCount--;
            pictureChanged = shapeChanged = true;
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
                texture.SetPixels32(colors);
                texture.Apply(false);
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
            texture = PixelTextures.NewTexture(Size, Size, GameObject.name);
            texture.SetPixels32(colors);
            texture.Apply(false);
            sprite = PixelTextures.NewSprite(texture, Vector2.zero);
            spriteRenderer.sprite = sprite;
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
