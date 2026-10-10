using System.Collections.Generic;
using KingdomGame.Art;
using UnityEngine;

namespace KingdomGame
{
    /// <summary>
    /// Hands out the game's sprites by name (see <see cref="ArtKeys"/>). They come
    /// from the baked <see cref="ArtLibrary"/> (the View Config's Art Detail, 1024
    /// pixels per tile by default, with normal maps). If a picture isn't baked (the
    /// bake didn't run, or something new was added), it's drawn on the spot more
    /// coarsely so the game still works, and a warning says to run
    /// KingdomGame > Build Game Scenes.
    /// </summary>
    public static class ArtSprites
    {
        /// <summary>Detail of pictures drawn on the spot when the baked one is missing.</summary>
        public const int FallbackPixelsPerTile = 128;

        static ArtLibrary library;
        static readonly Dictionary<string, int> index = new Dictionary<string, int>();
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, ArtItem> items = new Dictionary<string, ArtItem>();
        static readonly Dictionary<string, PixelImage> drawn = new Dictionary<string, PixelImage>();
        static readonly List<Object> made = new List<Object>();
        static bool warned;

        /// <summary>Sets the library and the wall looks (for drawing wall tiles that aren't baked).</summary>
        public static void Configure(ArtLibrary art, IList<WallLook> walls)
        {
            Release();
            library = art;
            if (library != null && library.entries != null)
                for (int i = 0; i < library.entries.Length; i++) index[library.entries[i].key] = i;
            foreach (var item in ArtCatalog.Items(walls)) items[item.Key] = item;
        }

        /// <summary>The sprite called <paramref name="key"/>, or null if there's no such picture at all.</summary>
        public static Sprite Get(string key)
        {
            if (cache.TryGetValue(key, out var sprite)) return sprite;
            sprite = FromLibrary(key) ?? Fallback(key);
            cache[key] = sprite;
            return sprite;
        }

        /// <summary>Destroys the sprites and textures made so far (when the game scene ends).</summary>
        public static void Release()
        {
            foreach (var o in made)
                if (o != null) Object.Destroy(o);
            made.Clear();
            cache.Clear();
            index.Clear();
            items.Clear();
            drawn.Clear();
            warned = false;
        }

        static Sprite FromLibrary(string key)
        {
            if (library == null || !index.TryGetValue(key, out int i)) return null;
            var e = library.entries[i];
            if (e.sheet < 0 || e.sheet >= library.sheets.Length) return null;
            var sheet = library.sheets[e.sheet];
            if (sheet == null || sheet.albedo == null) return null;
            var rect = new Rect(e.rect.x, e.rect.y, e.rect.width, e.rect.height);
            Sprite sprite;
#if UNITY_6000_0_OR_NEWER
            if (sheet.normals != null)
            {
                // The normal map lets torches and braziers light up the picture's bumps.
                var normal = new[] { new SecondarySpriteTexture { name = "_NormalMap", texture = sheet.normals } };
                sprite = Sprite.Create(sheet.albedo, rect, e.pivot, e.pixelsPerTile, 0, SpriteMeshType.FullRect, Vector4.zero, false, normal);
            }
            else
#endif
                sprite = Sprite.Create(sheet.albedo, rect, e.pivot, e.pixelsPerTile, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            made.Add(sprite);
            return sprite;
        }

        static Sprite Fallback(string key)
        {
            if (!items.TryGetValue(key, out var item)) return null;
            if (!warned)
            {
                warned = true;
                Debug.LogWarning($"The art '{key}' isn't baked, so it's drawn more coarsely while the game runs. " +
                                 "Run KingdomGame > Build Game Scenes to bake the full-detail art.");
            }
            var sprite = PixelTextures.ToSprite(Draw(item), key, item.Repeats, ArtCatalog.PixelsPerTile(item, FallbackPixelsPerTile));
            made.Add(sprite);
            made.Add(sprite.texture);
            return sprite;
        }

        static PixelImage Draw(ArtItem item)
        {
            if (drawn.TryGetValue(item.Key, out var image)) return image;
            image = ArtCatalog.Render(item, FallbackPixelsPerTile, key => items.TryGetValue(key, out var source) ? Draw(source) : new PixelImage(1, 1));
            drawn[item.Key] = image;
            return image;
        }
    }
}
