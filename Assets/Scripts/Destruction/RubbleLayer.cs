using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// Rubble and scorch marks on the ground. Pieces that came to rest are
    /// painted into flat 64 x 64 tiles instead of staying as separate objects,
    /// so rubble costs the same however much of it piles up. Tiles are made the
    /// first time something lands on them and uploaded at most once per frame.
    /// </summary>
    public sealed class RubbleLayer
    {
        const int Size = PixelChunk.Size;
        static readonly Vector2 Light = new Vector2(-0.55f, 0.6f);

        sealed class Tile
        {
            public Color32[] Colors;
            public Texture2D Texture;
            public Sprite Sprite;
            public GameObject GameObject;
            public bool Changed;
        }

        readonly DestructionSettings settings;
        readonly Transform root;
        readonly Dictionary<Vector2Int, Tile> tiles = new Dictionary<Vector2Int, Tile>();
        readonly List<Tile> changed = new List<Tile>();
        uint seed = 7;

        public int TileCount => tiles.Count;

        public RubbleLayer(Transform parent, DestructionSettings settings)
        {
            this.settings = settings;
            root = new GameObject("Rubble").transform;
            root.SetParent(parent, false);
        }

        /// <summary>Leaves one darkened rubble pixel at this world position.</summary>
        public void Drop(Vector2 position, Color32 color)
        {
            var pixel = PixelWorld.WorldToPixel(position);
            Paint(pixel.x, pixel.y, Darken(color, settings.rubbleBrightness), 1f);
        }

        /// <summary>
        /// Leaves a chunk of rubble <paramref name="sizePixels"/> across: a rough
        /// lump, lit on its top-left side, with a dark rim on the ground around it.
        /// </summary>
        public void Stamp(Vector2 position, Color32 color, float sizePixels)
        {
            var center = PixelWorld.WorldToPixel(position);
            float r = Mathf.Max(0.8f, sizePixels * 0.5f);
            int reach = Mathf.CeilToInt(r) + 1;
            var baseColor = Darken(color, settings.rubbleBrightness);
            uint s = Next();
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    float jag = (DamageShape.Hash(s, dx, dy) & 0xFF) / 255f * 0.9f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) + jag - 0.3f;
                    if (d > r + 0.6f) continue;
                    if (d > r - 0.4f)
                    {
                        Paint(center.x + dx, center.y + dy, new Color32(14, 12, 10, 255), 0.35f);   // contact shadow
                        continue;
                    }
                    float facing = (dx * Light.x + dy * Light.y) / Mathf.Max(1f, r);
                    Paint(center.x + dx, center.y + dy, Scale(baseColor, 1f + facing * 0.35f), 1f);
                }
        }

        /// <summary>Blackens the ground (and any rubble) in a ragged circle, as if burnt.</summary>
        public void Scorch(Vector2 position, float radiusPixels)
        {
            if (radiusPixels < 1f) return;
            var center = PixelWorld.WorldToPixel(position);
            int reach = Mathf.CeilToInt(radiusPixels);
            uint s = Next();
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / radiusPixels;
                    float noise = Noise01(s, (center.x + dx) / 6, (center.y + dy) / 6) * 0.35f + Noise01(s + 1u, center.x + dx, center.y + dy) * 0.15f;
                    float strength = Mathf.Clamp01((1f - d) * 1.6f - noise);
                    if (strength <= 0f) continue;
                    Paint(center.x + dx, center.y + dy, new Color32(16, 13, 11, 255), strength * 0.8f);
                }
        }

        public void Flush()
        {
            foreach (var tile in changed)
            {
                tile.Texture.SetPixels32(tile.Colors);
                tile.Texture.Apply(true);
                tile.Changed = false;
            }
            changed.Clear();
        }

        public void Clear()
        {
            foreach (var tile in tiles.Values) DestroyTile(tile);
            tiles.Clear();
            changed.Clear();
        }

        public void Dispose()
        {
            Clear();
            if (root != null) Object.Destroy(root.gameObject);
        }

        // Paints over a world pixel, blending by coverage (0..1).
        void Paint(int pixelX, int pixelY, Color32 color, float coverage)
        {
            var cell = new Vector2Int(DamageShape.FloorDiv(pixelX, Size), DamageShape.FloorDiv(pixelY, Size));
            if (!tiles.TryGetValue(cell, out var tile))
            {
                tile = Create(cell);
                tiles.Add(cell, tile);
            }
            int i = (pixelY - cell.y * Size) * Size + (pixelX - cell.x * Size);
            var under = tile.Colors[i];
            float a = Mathf.Clamp01(coverage);
            if (under.a == 0)
            {
                tile.Colors[i] = new Color32(color.r, color.g, color.b, (byte)Mathf.RoundToInt(a * 255f));
            }
            else
            {
                tile.Colors[i] = new Color32(
                    (byte)(under.r + (color.r - under.r) * a),
                    (byte)(under.g + (color.g - under.g) * a),
                    (byte)(under.b + (color.b - under.b) * a),
                    (byte)Mathf.Max(under.a, Mathf.RoundToInt(a * 255f)));
            }
            if (!tile.Changed)
            {
                tile.Changed = true;
                changed.Add(tile);
            }
        }

        static Color32 Darken(Color32 c, float k) => new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);

        static Color32 Scale(Color32 c, float k) => new Color32(
            (byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255);

        static float Noise01(uint s, int x, int y) => (DamageShape.Hash(s, x, y) & 0xFFFF) / 65535f;

        uint Next() => seed = DamageShape.Hash(seed, 3, 5);

        Tile Create(Vector2Int cell)
        {
            var tile = new Tile
            {
                Colors = new Color32[Size * Size],
                Texture = PixelTextures.NewTexture(Size, Size, $"Rubble {cell.x},{cell.y}"),
                GameObject = new GameObject($"Rubble {cell.x},{cell.y}"),
            };
            tile.Sprite = PixelTextures.NewSprite(tile.Texture, Vector2.zero);
            tile.GameObject.transform.SetParent(root, false);
            tile.GameObject.transform.position = new Vector3(cell.x, cell.y, 0f);
            var renderer = tile.GameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = tile.Sprite;
            renderer.sortingOrder = SortOrder.Rubble;
            return tile;
        }

        static void DestroyTile(Tile tile)
        {
            Object.Destroy(tile.GameObject);
            Object.Destroy(tile.Sprite);
            Object.Destroy(tile.Texture);
        }
    }
}
