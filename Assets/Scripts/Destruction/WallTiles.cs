using System.Collections.Generic;
using KingdomGame.Art;
using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// One finished, undamaged tile. Every intact chunk using it shares the same
    /// sprite (the baked full-detail art), so an undamaged wall costs almost
    /// nothing. A chunk only gets its own picture the first time it's hit.
    /// </summary>
    public sealed class TileVariant
    {
        /// <summary>64 x 64: the color of each breakable pixel (for flying pieces and rubble).</summary>
        public Color32[] Colors;
        /// <summary>Starting health of every pixel (0 = empty), 64 x 64.</summary>
        public byte[] Health;
        public int SolidCount;
        /// <summary>The intact tile's sprite.</summary>
        public Sprite Sprite;
        /// <summary><see cref="WallTiles.DetailSize"/> x DetailSize: the picture a damaged chunk starts from.</summary>
        public Color32[] Detail;
        /// <summary>The normal map of <see cref="Detail"/> (shared by every damaged chunk of this variant), so torchlight still shows the bumps.</summary>
        public Texture2D DetailNormals;
        /// <summary>Collider boxes for the undamaged tile.</summary>
        public List<PixelRect> ColliderRects;
    }

    /// <summary>Makes the wall tiles (drawn by <see cref="WallArt"/>) and keeps one set per material.</summary>
    public sealed class WallTiles
    {
        const int Size = PixelChunk.Size;
        /// <summary>Pixels across a damaged chunk's picture: twice the breakable pixels, so hole edges are smooth.</summary>
        public const int DetailSize = Size * 2;

        readonly Dictionary<StructureMaterial, TileVariant[]> library = new Dictionary<StructureMaterial, TileVariant[]>();
        readonly List<Object> made = new List<Object>();

        /// <summary>What a material looks like, for drawing (and baking) its tiles.</summary>
        public static WallLook LookOf(StructureMaterial material) => new WallLook
        {
            Name = material.name,
            Logs = material.pattern == WallPattern.Logs,
            Gap = material.gapColor,
            Colors = material.colors,
            Variants = Mathf.Clamp(material.variants, 1, 8),
        };

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
            foreach (var o in made)
                if (o != null) Object.Destroy(o);
            made.Clear();
            library.Clear();
        }

        TileVariant Create(StructureMaterial material, int variantIndex)
        {
            var look = LookOf(material);
            var detail = look.Logs
                ? WallArt.Logs(DetailSize, look.Gap, look.Colors, variantIndex)
                : WallArt.Stone(DetailSize, look.Gap, look.Colors, variantIndex);
            var small = detail.Downsample(DetailSize / Size);
            var colors = small.Pixels;

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

            // The baked full-detail tile; if it isn't baked, the picture drawn here.
            var sprite = ArtSprites.Get(ArtKeys.Wall(material.name, variantIndex));
            if (sprite == null)
            {
                var copy = new PixelImage(DetailSize, DetailSize) { Pivot = Vector2.zero };
                System.Array.Copy(detail.Pixels, copy.Pixels, detail.Pixels.Length);
                sprite = PixelTextures.ToSprite(copy, $"{material.name} tile {variantIndex}", false, DetailSize);
                made.Add(sprite);
                made.Add(sprite.texture);
            }

            // Holes don't change which way the rest of the stone faces, so one normal map serves every damaged chunk.
            var normals = new Texture2D(DetailSize, DetailSize, TextureFormat.RGBA32, true, true)
            {
                name = $"{material.name} tile {variantIndex} normals",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            normals.SetPixels32(detail.NormalMap().Pixels);
            normals.Apply(true, true);
            made.Add(normals);

            return new TileVariant
            {
                Colors = colors,
                Health = health,
                SolidCount = solid,
                Sprite = sprite,
                Detail = detail.Pixels,
                DetailNormals = normals,
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
