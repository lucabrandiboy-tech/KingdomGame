using KingdomGame.Art;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>What <see cref="WorldGenerator.Generate"/> made.</summary>
    public sealed class WorldParts
    {
        /// <summary>Every tree, rock and bush (as data).</summary>
        public NatureMap Nature;
        /// <summary>Makes the ones near players and kingdoms into real objects.</summary>
        public NatureStreamer Streamer;
        /// <summary>Fades tree leaves near the ruler.</summary>
        public CanopyFader Canopies;
    }

    /// <summary>
    /// Builds the map: the ground (dark grass with big patches of dry grass and
    /// mud), a soft tint over it (darker under forests), invisible walls at the
    /// edges, and trees, rocks and berry bushes placed with noise so they form
    /// forests and clearings. The pictures come from <see cref="ArtSprites"/>.
    /// The trees, rocks and bushes go into a <see cref="NatureMap"/>; the
    /// <see cref="NatureStreamer"/> makes the ones near people into real objects.
    /// The same seed and settings always make the same map (a server sends its
    /// seed to the players, who make the map themselves).
    /// </summary>
    public static class WorldGenerator
    {
        const int Variants = NatureArt.Variants;
        /// <summary>Most detail of the ground patch masks, in mask pixels per tile (fine enough for ragged edges).</summary>
        const int MaskDetail = 32;
        /// <summary>Biggest mask texture, in pixels: on big maps the masks get fewer pixels per tile to fit (video memory).</summary>
        const int MaxMask = 4096;
        /// <summary>Detail of the soft ground tint, in pixels per tile.</summary>
        const int TintDetail = 4;
        /// <summary>Biggest tint texture, in pixels (it's soft: one pixel per tile is enough on big maps).</summary>
        const int MaxTint = 1024;

        /// <summary>
        /// Builds the map. <paramref name="drawGround"/> false skips the ground pictures
        /// (a server with no screen doesn't need them).
        /// </summary>
        public static WorldParts Generate(GameContent content, Transform parent, int seed, bool drawGround = true)
        {
            var config = content.world;
            int half = config.mapSize / 2;
            uint s = (uint)seed;

            // "Forest-ness" of every tile. The cut-off is chosen so that exactly
            // Forest Cover of the map is forest, whatever the noise looks like.
            int size = config.mapSize;
            var forest = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    forest[y * size + x] = Noise.Fractal(x / 14f, y / 14f, s + 1u, 3);
            var sorted = (float[])forest.Clone();
            System.Array.Sort(sorted);
            float treeLine = Percentile(sorted, 1f - config.forestCover);
            float bushLine = Percentile(sorted, 1f - config.forestCover - 0.1f);

            if (drawGround)
            {
                MakeGround(parent, size, s);
                MakeTint(parent, size, forest, treeLine, bushLine, s);
            }
            MakeEdges(parent, half);

            var nature = new GameObject("Nature").transform;
            nature.SetParent(parent, false);
            var fader = nature.gameObject.AddComponent<CanopyFader>();
            var map = new NatureMap(new RectInt(-half, -half, size, size), s);
            NatureMap.Current = map;

            for (int y = -half + 1; y < half - 1; y++)
            {
                for (int x = -half + 1; x < half - 1; x++)
                {
                    if (x * x + y * y < config.clearRadius * config.clearRadius) continue;
                    if (config.testOutpost && x >= 3 && x <= 15 && y >= -6 && y <= 6) continue; // keep the outpost clear

                    float here = forest[(y + half) * size + (x + half)];
                    float roll = Noise.Hash01(s + 2u, x, y);
                    ResourceNodeConfig kind = null;

                    if (here > treeLine) { if (roll < config.treeDensity) kind = content.tree; }
                    else if (here > bushLine) { if (roll < config.bushChance) kind = content.bush; }
                    else if (roll < config.rockChance) kind = content.rock;
                    if (kind == null) continue;

                    map.Generate(new Vector2Int(x, y), kind, (int)(DamageShape.Hash(s + 5u, x, y) % Variants));
                }
            }
            var streamer = nature.gameObject.AddComponent<NatureStreamer>();
            streamer.Setup(map, NatureSprites(), fader);
            return new WorldParts { Nature = map, Streamer = streamer, Canopies = fader };
        }

        static float Percentile(float[] sorted, float fraction) =>
            sorted[Mathf.Clamp(Mathf.RoundToInt(fraction * (sorted.Length - 1)), 0, sorted.Length - 1)];

        /// <summary>The baked pictures (and shadows) of every tree, rock and bush: [look, variant].</summary>
        public static NodeSprites[,] NatureSprites()
        {
            var looks = new NodeSprites[3, Variants];
            for (int v = 0; v < Variants; v++)
            {
                looks[(int)NodeLook.Tree, v] = new NodeSprites
                {
                    Full = ArtSprites.Get(ArtKeys.TreeTrunk(v)), Empty = ArtSprites.Get(ArtKeys.TreeStump(v)), Canopy = ArtSprites.Get(ArtKeys.TreeLeaves(v)),
                    Shadow = ArtSprites.Get(ArtKeys.Shadow(ArtKeys.TreeLeaves(v))), EmptyShadow = ArtSprites.Get(ArtKeys.Shadow(ArtKeys.TreeStump(v))),
                    ShadowHeight = 0.6f, EmptyShadowHeight = 0.05f,
                };
                looks[(int)NodeLook.Rock, v] = new NodeSprites
                {
                    Full = ArtSprites.Get(ArtKeys.Rock(v)), Empty = null, Shadow = ArtSprites.Get(ArtKeys.Shadow(ArtKeys.Rock(v))), ShadowHeight = 0.18f,
                };
                looks[(int)NodeLook.BerryBush, v] = new NodeSprites
                {
                    Full = ArtSprites.Get(ArtKeys.BushBerries(v)), Empty = ArtSprites.Get(ArtKeys.BushBare(v)),
                    Shadow = ArtSprites.Get(ArtKeys.Shadow(ArtKeys.BushBerries(v))), EmptyShadow = ArtSprites.Get(ArtKeys.Shadow(ArtKeys.BushBerries(v))),
                    ShadowHeight = 0.12f, EmptyShadowHeight = 0.12f,
                };
            }
            return looks;
        }

        // Dark grass everywhere, with dry grass and mud in big patches: each patch
        // layer only shows inside its sprite mask, made from the patch noise.
        static void MakeGround(Transform parent, int mapSize, uint seed)
        {
            var root = new GameObject("Ground").transform;
            root.SetParent(parent, false);
            root.position = new Vector3(-mapSize / 2, -mapSize / 2, 0);
            Layer(root, "Grass", ArtKeys.GroundGrass, mapSize, SortOrder.Ground, false);
            Layer(root, "Dry grass", ArtKeys.GroundDry, mapSize, SortOrder.Ground + 2, true);
            Layer(root, "Mud", ArtKeys.GroundMud, mapSize, SortOrder.Ground + 4, true);

            int detail = Mathf.Clamp(MaxMask / mapSize, 4, MaskDetail);
            int n = mapSize * detail;
            var dry = new byte[n * n];
            var mud = new byte[n * n];
            GroundArt.PatchMasks(mapSize, detail, seed, dry, mud);
            Mask(root, "Dry grass mask", dry, n, detail, SortOrder.Ground + 1, SortOrder.Ground + 2);
            Mask(root, "Mud mask", mud, n, detail, SortOrder.Ground + 3, SortOrder.Ground + 4);
        }

        static void Layer(Transform root, string layerName, string key, int mapSize, int order, bool masked)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(root, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = ArtSprites.Get(key);
            r.drawMode = SpriteDrawMode.Tiled;
            r.tileMode = SpriteTileMode.Continuous;
            r.size = new Vector2(mapSize, mapSize);
            r.sortingOrder = order;
            if (masked) r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            // Tiled sprites are laid out from their pivot; the ground's pivot is its bottom-left corner.
            go.transform.localPosition = Vector3.zero;
        }

        // A sprite mask over the whole map: it only affects the layer between back and front order.
        // One byte per mask pixel (Alpha8), with mipmaps so the ragged edges don't shimmer far zoomed out.
        static void Mask(Transform root, string maskName, byte[] alpha, int size, int detail, int back, int front)
        {
            var texture = new Texture2D(size, size, TextureFormat.Alpha8, true)
            {
                name = maskName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixelData(alpha, 0);
            texture.Apply(true, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.zero, detail, 0, SpriteMeshType.FullRect);
            var go = new GameObject(maskName);
            go.transform.SetParent(root, false);
            var mask = go.AddComponent<SpriteMask>();
            mask.sprite = sprite;
            mask.alphaCutoff = 0.5f;
            mask.isCustomRangeActive = true;
            mask.backSortingLayerID = 0;
            mask.frontSortingLayerID = 0;
            mask.backSortingOrder = back;
            mask.frontSortingOrder = front;
        }

        // A few pixels per tile, smoothly stretched over the whole map: darker and
        // greener under forests, drier in the open, with soft blotches (GroundArt.Tint).
        // Big maps get fewer pixels per tile (at most MaxTint across), so it doesn't take long to make.
        static void MakeTint(Transform parent, int size, float[] forest, float treeLine, float bushLine, uint seed)
        {
            int tintDetail = Mathf.Clamp(MaxTint / size, 1, TintDetail);
            int n = size * tintDetail;
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float tx = (x + 0.5f) / tintDetail, ty = (y + 0.5f) / tintDetail;
                    float wood = PixelImage.Smooth(bushLine, treeLine + 0.04f, Forest(forest, size, tx, ty));
                    float edge = Mathf.Min(Mathf.Min(tx, size - tx), Mathf.Min(ty, size - ty));
                    float rim = 1f - PixelImage.Smooth(0f, 6f, edge);
                    GroundArt.Patches(tx - size / 2, ty - size / 2, seed, out float dry, out float mud);
                    pixels[y * n + x] = GroundArt.Tint(tx, ty, seed, wood, rim, dry, mud);
                }
            var texture = PixelTextures.NewTexture(n, n, "Ground tint", false);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, n, n), Vector2.zero, tintDetail, 0, SpriteMeshType.FullRect);
            var go = new GameObject("Ground tint");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-size / 2, -size / 2, 0);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = SortOrder.GroundTint;
        }

        // The forest-ness between tile centers, blended smoothly.
        static float Forest(float[] forest, int size, float x, float y)
        {
            float gx = Mathf.Clamp(x - 0.5f, 0f, size - 1.001f), gy = Mathf.Clamp(y - 0.5f, 0f, size - 1.001f);
            int x0 = (int)gx, y0 = (int)gy;
            float fx = gx - x0, fy = gy - y0;
            int x1 = Mathf.Min(x0 + 1, size - 1), y1 = Mathf.Min(y0 + 1, size - 1);
            return Mathf.Lerp(Mathf.Lerp(forest[y0 * size + x0], forest[y0 * size + x1], fx),
                              Mathf.Lerp(forest[y1 * size + x0], forest[y1 * size + x1], fx), fy);
        }

        // Invisible walls around the map so nobody walks off the edge.
        static void MakeEdges(Transform parent, int half)
        {
            var go = new GameObject("Map edges");
            go.transform.SetParent(parent, false);
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            float size = half * 2f, thick = 2f;
            Box(go, new Vector2(0, half + thick / 2), new Vector2(size + thick * 2, thick));
            Box(go, new Vector2(0, -half - thick / 2), new Vector2(size + thick * 2, thick));
            Box(go, new Vector2(half + thick / 2, 0), new Vector2(thick, size));
            Box(go, new Vector2(-half - thick / 2, 0), new Vector2(thick, size));
        }

        static void Box(GameObject go, Vector2 center, Vector2 size)
        {
            var box = go.AddComponent<BoxCollider2D>();
            box.offset = center;
            box.size = size;
        }
    }
}
