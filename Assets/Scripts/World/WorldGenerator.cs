using KingdomGame.Art;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>What <see cref="WorldGenerator.Generate"/> made.</summary>
    public sealed class WorldParts
    {
        /// <summary>Every tree, rock and bush (worked out from the seed as needed).</summary>
        public NatureMap Nature;
        /// <summary>Makes the ones near players and kingdoms into real objects.</summary>
        public NatureStreamer Streamer;
        /// <summary>Fades tree leaves near the ruler.</summary>
        public CanopyFader Canopies;
        /// <summary>The ground round the camera (null on a server with no screen).</summary>
        public GroundStreamer Ground;
        /// <summary>The ore fields (worked out from the seed; what drills have taken is kept).</summary>
        public OreMap Ore;
    }

    /// <summary>
    /// Makes an endless world from a seed: dark grass with big patches of dry grass and mud,
    /// a soft tint over it (darker under forests), trees, rocks and berry bushes placed
    /// with noise so they form forests and clearings, and ore fields (<see cref="OreMap"/>:
    /// iron, copper, coal and stone, bare of trees and rocks) for drills to dig. Nothing is made up front: what's at any
    /// spot is worked out from the seed and the spot when it's needed (<see cref="NatureMap"/>
    /// for trees, <see cref="GroundStreamer"/> for the ground), so the same seed always makes
    /// the same world on every computer (a host sends its seed to the players, who make the
    /// world themselves). The world's edge is <see cref="NatureMap.Limit"/> tiles out.
    /// </summary>
    public static class WorldGenerator
    {
        const int Variants = NatureArt.Variants;

        /// <summary>
        /// Makes the world for <paramref name="seed"/>. <paramref name="drawGround"/> false skips the
        /// ground pictures (a server with no screen doesn't need them).
        /// </summary>
        public static WorldParts Generate(GameContent content, Transform parent, int seed, bool drawGround = true)
        {
            var config = content.world;
            uint s = (uint)seed;

            // How much forest there is anywhere (smooth noise), and the levels where woods and bushes
            // start: chosen from a big sample round the middle so that about Forest Cover of the world is forest.
            System.Func<float, float, float> forest = (x, y) => Noise.Fractal(x / 14f, y / 14f, s + 1u, 3);
            var sample = new float[160 * 160];
            for (int y = 0; y < 160; y++)
                for (int x = 0; x < 160; x++)
                    sample[y * 160 + x] = forest(x * 6.3f - 504f, y * 6.3f - 504f);
            System.Array.Sort(sample);
            float treeLine = Percentile(sample, 1f - config.forestCover);
            float bushLine = Percentile(sample, 1f - config.forestCover - 0.1f);

            int clear2 = config.clearRadius * config.clearRadius;
            bool outpost = config.testOutpost;
            var tree = content.tree;
            var bush = content.bush;
            var rock = content.rock;
            float treeDensity = config.treeDensity, bushChance = config.bushChance, rockChance = config.rockChance;
            var ore = new OreMap(s);
            OreMap.Current = ore;
            ResourceNodeConfig At(int x, int y, out int variant)
            {
                variant = 0;
                if (x * x + y * y < clear2) return null;
                if (outpost && x >= 3 && x <= 15 && y >= -6 && y <= 6) return null;   // keep the outpost clear
                if (OreMap.Generate(s, x, y, out _) != OreKind.None) return null;     // ore fields are bare (drills go on them)
                float here = forest(x, y);
                float roll = Noise.Hash01(s + 2u, x, y);
                ResourceNodeConfig kind = null;
                if (here > treeLine) { if (roll < treeDensity) kind = tree; }
                else if (here > bushLine) { if (roll < bushChance) kind = bush; }
                else if (roll < rockChance) kind = rock;
                if (kind != null) variant = (int)(DamageShape.Hash(s + 5u, x, y) % Variants);
                return kind;
            }

            var nature = new GameObject("Nature").transform;
            nature.SetParent(parent, false);
            var fader = nature.gameObject.AddComponent<CanopyFader>();
            var map = new NatureMap(s, At);
            NatureMap.Current = map;
            var streamer = nature.gameObject.AddComponent<NatureStreamer>();
            streamer.Setup(map, NatureSprites(), fader);

            GroundStreamer ground = null;
            if (drawGround)
            {
                ground = new GameObject("Ground").AddComponent<GroundStreamer>();
                ground.transform.SetParent(parent, false);
                ground.Setup(Camera.main, s, forest, treeLine, bushLine, ore);
            }
            MakeEdges(parent, NatureMap.Limit);
            return new WorldParts { Nature = map, Streamer = streamer, Canopies = fader, Ground = ground, Ore = ore };
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
                    // (a pine stays green in autumn)
                    CanopyAutumn = v % Variants == 3 ? null : ArtSprites.Get(ArtKeys.TreeLeavesAutumn(v)), CanopyWinter = ArtSprites.Get(ArtKeys.TreeLeavesWinter(v)),
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

        // Invisible walls at the world's edge.
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
