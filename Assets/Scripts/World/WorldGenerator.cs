using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// Builds the map: grass ground, invisible walls at the edges, and trees,
    /// rocks and berry bushes placed with noise so they form forests and clearings.
    /// </summary>
    public static class WorldGenerator
    {
        const int Variants = 3;

        public static void Generate(GameContent content, Transform parent, int seed)
        {
            var config = content.world;
            int half = config.mapSize / 2;
            uint s = (uint)seed;

            MakeGround(parent, config.mapSize, s);
            MakeEdges(parent, half);

            var sprites = new Dictionary<ResourceNodeConfig, Sprite[]>();
            var empty = new Dictionary<ResourceNodeConfig, Sprite[]>();
            var nodes = new GameObject("Nature").transform;
            nodes.SetParent(parent, false);

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
                    position = new Vector2(Snap(position.x), Snap(position.y));
                    int variant = (int)(DamageShape.Hash(s + 5u, x, y) % Variants);
                    Spawn(kind, position, variant, nodes, sprites, empty);
                }
            }
        }

        static float Percentile(float[] sorted, float fraction) =>
            sorted[Mathf.Clamp(Mathf.RoundToInt(fraction * (sorted.Length - 1)), 0, sorted.Length - 1)];

        static float Snap(float v) => Mathf.Round(v * PixelArt.PixelsPerUnit) / PixelArt.PixelsPerUnit;

        static void Spawn(ResourceNodeConfig kind, Vector2 position, int variant, Transform parent,
            Dictionary<ResourceNodeConfig, Sprite[]> sprites, Dictionary<ResourceNodeConfig, Sprite[]> empty)
        {
            if (!sprites.TryGetValue(kind, out var full))
            {
                full = new Sprite[Variants];
                var used = new Sprite[Variants];
                for (int v = 0; v < Variants; v++)
                {
                    switch (kind.look)
                    {
                        case NodeLook.Tree:
                            full[v] = PixelTextures.ToSprite(NatureArt.Tree(v), $"Tree {v}");
                            used[v] = PixelTextures.ToSprite(NatureArt.Stump(v), $"Stump {v}");
                            break;
                        case NodeLook.Rock:
                            full[v] = PixelTextures.ToSprite(NatureArt.Rock(v), $"Rock {v}");
                            used[v] = null; // rocks disappear when mined out
                            break;
                        default:
                            full[v] = PixelTextures.ToSprite(NatureArt.Bush(v, true), $"Berry bush {v}");
                            used[v] = PixelTextures.ToSprite(NatureArt.Bush(v, false), $"Bush {v}");
                            break;
                    }
                }
                sprites.Add(kind, full);
                empty.Add(kind, used);
            }

            var go = new GameObject(kind.name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<ResourceNode>().Setup(kind, full[variant], empty[kind][variant], YSort.Order(position.y));
        }

        static void MakeGround(Transform parent, int mapSize, uint seed)
        {
            const int pictureTiles = 8;
            var picture = GroundArt.Grass(pictureTiles, seed + 99u);
            var sprite = PixelTextures.ToSprite(picture, "Grass");
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-mapSize / 2, -mapSize / 2, 0);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.drawMode = SpriteDrawMode.Tiled;
            r.tileMode = SpriteTileMode.Continuous;
            r.size = new Vector2(mapSize, mapSize);
            r.sortingOrder = -30000;
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
