using System.Collections.Generic;
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

    /// <summary>Draws wall tiles in code and keeps one set per material.</summary>
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
            var colors = material.pattern == WallPattern.Planks
                ? Planks(material, variantIndex)
                : Bricks(material, variantIndex);

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
            texture.Apply(false);

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

        // Pixels are stored bottom row first (Unity's texture order); these
        // drawings work in rows from the top, hence (Size - 1 - row).

        static Color32[] Bricks(StructureMaterial m, int variant)
        {
            const int brickWidth = 16, brickHeight = 8; // both include the 1-pixel mortar line
            var pixels = new Color32[Size * Size];
            for (int row = 0; row < Size; row++)
            {
                int course = row / brickHeight;
                int yIn = row % brickHeight;
                int shift = (course % 2) * (brickWidth / 2);
                for (int x = 0; x < Size; x++)
                {
                    int xs = x + shift;
                    int xIn = xs % brickWidth;
                    Color32 c;
                    if (yIn == brickHeight - 1 || xIn == brickWidth - 1)
                    {
                        c = m.gapColor;
                    }
                    else
                    {
                        c = Pick(m, variant, xs / brickWidth, course);
                        if (yIn == 0 || xIn == 0) c = Shade(c, 24);
                        else if (yIn == brickHeight - 2 || xIn == brickWidth - 2) c = Shade(c, -22);
                        else if ((DamageShape.Hash((uint)variant + 7u, x, row) & 7u) == 0) c = Shade(c, -12);
                    }
                    pixels[(Size - 1 - row) * Size + x] = c;
                }
            }
            return pixels;
        }

        static Color32[] Planks(StructureMaterial m, int variant)
        {
            const int plankWidth = 8; // includes the 1-pixel gap
            var pixels = new Color32[Size * Size];
            for (int row = 0; row < Size; row++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int plank = x / plankWidth;
                    int xIn = x % plankWidth;
                    Color32 c;
                    if (xIn == plankWidth - 1)
                    {
                        c = m.gapColor;
                    }
                    else
                    {
                        c = Pick(m, variant, plank, 0);
                        if (xIn == 0) c = Shade(c, 18);
                        else if (xIn == plankWidth - 2) c = Shade(c, -20);
                        // wood grain: a few darker streaks running down each plank
                        else if ((DamageShape.Hash((uint)variant + 11u, x, row / 5) & 3u) == 0) c = Shade(c, -14);
                        // nails near the top and bottom
                        if ((row == 3 || row == Size - 4) && xIn == 3) c = new Color32(70, 70, 76, 255);
                    }
                    pixels[(Size - 1 - row) * Size + x] = c;
                }
            }
            return pixels;
        }

        static Color32 Pick(StructureMaterial m, int variant, int a, int b)
        {
            if (m.colors == null || m.colors.Length == 0) return new Color32(128, 128, 128, 255);
            return m.colors[DamageShape.Hash((uint)variant + 1u, a, b) % (uint)m.colors.Length];
        }

        public static Color32 Shade(Color32 c, int amount) => new Color32(
            (byte)Mathf.Clamp(c.r + amount, 0, 255),
            (byte)Mathf.Clamp(c.g + amount, 0, 255),
            (byte)Mathf.Clamp(c.b + amount, 0, 255),
            c.a);
    }
}
