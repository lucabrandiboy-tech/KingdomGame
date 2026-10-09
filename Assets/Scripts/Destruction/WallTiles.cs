using System.Collections.Generic;
using KingdomGame.Art;
using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// One finished, undamaged tile picture. Every intact chunk using it shares
    /// the same texture, so an undamaged wall costs almost nothing. A chunk only
    /// gets its own copy the first time it is hit.
    /// </summary>
    public sealed class TileVariant
    {
        public Color32[] Colors;
        /// <summary>Starting health of every pixel (0 = empty).</summary>
        public byte[] Health;
        public int SolidCount;
        public Texture2D Texture;
        public Sprite Sprite;
        /// <summary>Collider boxes for the undamaged tile.</summary>
        public List<PixelRect> ColliderRects;
    }

    /// <summary>Makes the wall tile pictures (drawn by <see cref="WallArt"/>) and keeps one set per material.</summary>
    public sealed class WallTiles
    {
        const int Size = PixelChunk.Size;

        readonly Dictionary<StructureMaterial, TileVariant[]> library = new Dictionary<StructureMaterial, TileVariant[]>();

        public TileVariant Get(StructureMaterial material, Vector2Int cell)
        {
            if (!library.TryGetValue(material, out var variants))
            {
                variants = new TileVariant[Mathf.Clamp(material.variants, 1, 8)];
                for (int i = 0; i < variants.Length; i++)
                    variants[i] = Create(material, i);
                library.Add(material, variants);
            }
            uint pick = DamageShape.Hash(0x51u, cell.x, cell.y) % (uint)variants.Length;
            return variants[pick];
        }

        public void DestroyAll()
        {
            foreach (var variants in library.Values)
            {
                foreach (var variant in variants)
                {
                    Object.Destroy(variant.Sprite);
                    Object.Destroy(variant.Texture);
                }
            }
            library.Clear();
        }

        static TileVariant Create(StructureMaterial material, int variantIndex)
        {
            var colors = material.pattern == WallPattern.Logs
                ? WallArt.Logs(Size, material.gapColor, material.colors, variantIndex)
                : WallArt.Stone(Size, material.gapColor, material.colors, variantIndex);

            var health = new byte[colors.Length];
            int solid = 0;
            for (int i = 0; i < colors.Length; i++)
            {
                if (colors[i].a == 0) continue;
                health[i] = (byte)Mathf.Clamp(material.pixelHealth, 1, 255);
                solid++;
            }

            var rects = new List<PixelRect>();
            PixelAlgorithms.Rectangles(health, Size, Size, new bool[Size * Size], rects);

            var texture = PixelTextures.NewTexture(Size, Size, $"{material.name} tile {variantIndex}");
            texture.SetPixels32(colors);
            texture.Apply(true);

            return new TileVariant
            {
                Colors = colors,
                Health = health,
                SolidCount = solid,
                Texture = texture,
                Sprite = PixelTextures.NewSprite(texture, Vector2.zero),
                ColliderRects = rects,
            };
        }

        public static Color32 Shade(Color32 c, int amount) => new Color32(
            (byte)Mathf.Clamp(c.r + amount, 0, 255),
            (byte)Mathf.Clamp(c.g + amount, 0, 255),
            (byte)Mathf.Clamp(c.b + amount, 0, 255),
            c.a);
    }
}
