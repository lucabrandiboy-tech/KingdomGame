using KingdomGame.Art;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// Builds the map: the ground, a soft tint over it (darker under forests),
    /// invisible walls at the edges, and trees, rocks and berry bushes placed
    /// with noise so they form forests and clearings.
    /// </summary>
    public static class WorldGenerator
    {
        const int Variants = 3;

        /// <summary>Builds the map and returns the fader for its tree leaves.</summary>
        public static CanopyFader Generate(GameContent content, Transform parent, int seed)
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

            MakeGround(parent, size, s);
            MakeTint(parent, size, forest, treeLine, bushLine, s);
            MakeEdges(parent, half);

            var nature = new GameObject("Nature").transform;
            nature.SetParent(parent, false);
            var fader = nature.gameObject.AddComponent<CanopyFader>();
            var looks = MakeNatureSprites();

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

                    // Jitter inside the tile so it doesn't look like a grid.
                    float jx = (Noise.Hash01(s + 3u, x, y) - 0.5f) * 0.6f;
                    float jy = (Noise.Hash01(s + 4u, x, y) - 0.5f) * 0.6f;
                    var position = new Vector2(x + 0.5f + jx, y + 0.5f + jy);
                    int variant = (int)(DamageShape.Hash(s + 5u, x, y) % Variants);
                    float rotation = Noise.Hash01(s + 6u, x, y) * 360f;

                    var go = new GameObject(kind.name);
                    go.transform.SetParent(nature, false);
                    go.transform.position = position;
                    var node = go.AddComponent<ResourceNode>();
                    node.Setup(kind, looks[(int)kind.look, variant], rotation);
                    fader.Add(node);
                }
            }
            return fader;
        }

        static float Percentile(float[] sorted, float fraction) =>
            sorted[Mathf.Clamp(Mathf.RoundToInt(fraction * (sorted.Length - 1)), 0, sorted.Length - 1)];

        // Every tree, rock and bush picture (and shadow) in one texture, so they draw in a few batches.
        static NodeSprites[,] MakeNatureSprites()
        {
            var atlas = new SpriteAtlasBuilder();
            var ids = new int[3, Variants, 5];
            for (int v = 0; v < Variants; v++)
            {
                var canopy = NatureArt.Canopy(v);
                var stump = NatureArt.Stump(v);
                ids[0, v, 0] = atlas.Add(NatureArt.Trunk(v), $"Trunk {v}");
                ids[0, v, 1] = atlas.Add(stump, $"Stump {v}");
                ids[0, v, 2] = atlas.Add(canopy, $"Leaves {v}");
                ids[0, v, 3] = atlas.Add(canopy.Shadow(10, 1f), $"Tree shadow {v}");
                ids[0, v, 4] = atlas.Add(stump.Shadow(2, 1f), $"Stump shadow {v}");

                var rock = NatureArt.Rock(v);
                ids[1, v, 0] = atlas.Add(rock, $"Rock {v}");
                ids[1, v, 3] = atlas.Add(rock.Shadow(4, 1f), $"Rock shadow {v}");

                var bush = NatureArt.Bush(v, true);
                ids[2, v, 0] = atlas.Add(bush, $"Berry bush {v}");
                ids[2, v, 1] = atlas.Add(NatureArt.Bush(v, false), $"Bush {v}");
                ids[2, v, 3] = atlas.Add(bush.Shadow(3, 1f), $"Bush shadow {v}");
            }
            var sprites = atlas.Build("Nature");

            var looks = new NodeSprites[3, Variants];
            for (int v = 0; v < Variants; v++)
            {
                looks[(int)NodeLook.Tree, v] = new NodeSprites
                {
                    Full = sprites[ids[0, v, 0]], Empty = sprites[ids[0, v, 1]], Canopy = sprites[ids[0, v, 2]],
                    Shadow = sprites[ids[0, v, 3]], EmptyShadow = sprites[ids[0, v, 4]], ShadowHeight = 0.6f, EmptyShadowHeight = 0.05f,
                };
                looks[(int)NodeLook.Rock, v] = new NodeSprites
                {
                    Full = sprites[ids[1, v, 0]], Empty = null, Shadow = sprites[ids[1, v, 3]], ShadowHeight = 0.18f,
                };
                looks[(int)NodeLook.BerryBush, v] = new NodeSprites
                {
                    Full = sprites[ids[2, v, 0]], Empty = sprites[ids[2, v, 1]], Shadow = sprites[ids[2, v, 3]],
                    EmptyShadow = sprites[ids[2, v, 3]], ShadowHeight = 0.12f, EmptyShadowHeight = 0.12f,
                };
            }
            return looks;
        }

        static void MakeGround(Transform parent, int mapSize, uint seed)
        {
            // 1024 pixels = 16 tiles, repeated over the map; the tint below hides the repeat.
            var picture = GroundArt.Ground(1024, seed + 99u);
            var sprite = PixelTextures.ToSprite(picture, "Ground", repeat: true);
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-mapSize / 2, -mapSize / 2, 0);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.drawMode = SpriteDrawMode.Tiled;
            r.tileMode = SpriteTileMode.Continuous;
            r.size = new Vector2(mapSize, mapSize);
            r.sortingOrder = SortOrder.Ground;
        }

        // One pixel per tile, smoothly stretched over the whole map: darker and
        // greener under forests, drier in the open, with big soft blotches.
        static void MakeTint(Transform parent, int size, float[] forest, float treeLine, float bushLine, uint seed)
        {
            var picture = new PixelImage(size, size) { Pivot = Vector2.zero };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float f = forest[y * size + x];
                    float wood = PixelImage.Smooth(bushLine, treeLine + 0.04f, f);
                    float dry = Noise.Fractal(x / 9f, y / 9f, seed + 71u, 2);
                    float blotch = Noise.Fractal(x / 5f, y / 5f, seed + 73u, 2);
                    float edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    float rim = 1f - PixelImage.Smooth(0f, 6f, edge);

                    Color32 c;
                    if (wood > 0.05f)
                        c = PixelImage.Rgb(6, 12, 6, Mathf.RoundToInt(255 * (0.15f + 0.35f * wood)));
                    else if (dry > 0.58f)
                        c = PixelImage.Rgb(96, 84, 52, Mathf.RoundToInt(255 * PixelImage.Smooth(0.58f, 0.75f, dry) * 0.18f));
                    else
                        c = PixelImage.Rgb(8, 10, 6, Mathf.RoundToInt(255 * PixelImage.Smooth(0.45f, 0.75f, blotch) * 0.18f));
                    if (rim > 0f) c = PixelImage.Mix(c, PixelImage.Rgb(4, 6, 4, 220), rim);
                    picture.Set(x, y, c);
                }
            var texture = PixelTextures.NewTexture(size, size, "Ground tint", false);
            texture.SetPixels32(picture.Pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
            var go = new GameObject("Ground tint");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-size / 2, -size / 2, 0);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = SortOrder.GroundTint;
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
