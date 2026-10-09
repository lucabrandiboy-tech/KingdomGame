using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// Rubble on the ground: flying pixels that came to rest are painted into
    /// flat 32 x 32 tiles instead of staying as separate objects, so rubble
    /// costs the same however much of it piles up. Tiles are made the first
    /// time rubble lands on them and uploaded at most once per frame.
    /// </summary>
    public sealed class RubbleLayer
    {
        const int Size = PixelChunk.Size;

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

        public int TileCount => tiles.Count;

        public RubbleLayer(Transform parent, DestructionSettings settings)
        {
            this.settings = settings;
            root = new GameObject("Rubble").transform;
            root.SetParent(parent, false);
        }

        /// <summary>Leaves a darkened rubble pixel at this world position.</summary>
        public void Drop(Vector2 position, Color32 color)
        {
            var pixel = PixelWorld.WorldToPixel(position);
            var cell = new Vector2Int(DamageShape.FloorDiv(pixel.x, Size), DamageShape.FloorDiv(pixel.y, Size));
            if (!tiles.TryGetValue(cell, out var tile))
            {
                tile = Create(cell);
                tiles.Add(cell, tile);
            }

            float k = settings.rubbleBrightness;
            int i = (pixel.y - cell.y * Size) * Size + (pixel.x - cell.x * Size);
            tile.Colors[i] = new Color32((byte)(color.r * k), (byte)(color.g * k), (byte)(color.b * k), 255);
            if (!tile.Changed)
            {
                tile.Changed = true;
                changed.Add(tile);
            }
        }

        public void Flush()
        {
            foreach (var tile in changed)
            {
                tile.Texture.SetPixels32(tile.Colors);
                tile.Texture.Apply(false);
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
            renderer.sortingOrder = settings.rubbleSortingOrder;
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
