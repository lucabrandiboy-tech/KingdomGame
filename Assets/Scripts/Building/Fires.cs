using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.World;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// Fire. Wooden things (wood walls, and doors, furniture, chests and houses marked as burning) can catch fire:
    /// from lightning, explosions, a flamethrower, napalm, a burning zombie, or a torch or lamp knocked over. A fire
    /// grows hot, wears what's burning down until it's gone, and spreads to wooden things right next to it. Rain
    /// slows and puts out fires (not under a roof), snow slows the spreading; villagers carry buckets from a well to
    /// put them out (see <see cref="Kingdom"/>), and the ruler can beat one out (hold E). Only this computer's own
    /// kingdom burns (online, others' walls shown here don't); fires aren't saved: a loaded game has none.
    /// </summary>
    public sealed class Fires : MonoBehaviour
    {
        public static Fires Current { get; private set; }

        /// <summary>One thing on fire.</summary>
        public sealed class Blaze
        {
            /// <summary>What's burning: a wall (<see cref="DestructibleStructure"/>) or a building.</summary>
            public Component Target;
            public DestructibleStructure Wall;
            public Health Health;
            public bool IsHouse;
            public RectInt Cells;
            public Vector2 Center;
            /// <summary>0..1: grows by itself; water, rain and beating take it off; at 0 it's out.</summary>
            public float Heat;
            /// <summary>Villagers on their way with water.</summary>
            public int Fighters;
            public bool Out;
            internal Transform Look;
            internal float SpreadAt, SmokeAt;
        }

        sealed class Puff
        {
            public SpriteRenderer Look;
            public Vector2 At, Velocity;
            public float Age, Life, Size;
        }

        const int Puffs = 160, MaxLit = 40;

        FireConfig config;
        Kingdom kingdom;
        PixelWorld world;
        Seasons seasons;
        Sprite flame;
        Sprite[] smoke;
        readonly List<Blaze> blazes = new List<Blaze>();
        readonly Dictionary<Component, Blaze> byTarget = new Dictionary<Component, Blaze>();
        readonly List<Component> scratch = new List<Component>();
        readonly Puff[] puffs = new Puff[Puffs];
        int nextPuff;
        float crackleAt;

        public FireConfig Config => config;
        /// <summary>How many things are burning.</summary>
        public int Count => blazes.Count;
        public IReadOnlyList<Blaze> All => blazes;

        void Awake()
        {
            if (Current != null && Current != this) Debug.LogWarning("There is more than one Fires in the scene.", this);
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (world != null) world.Damaged -= OnDamaged;
            if (seasons != null) seasons.LightningStruck -= OnLightning;
            if (smoke != null && smoke.Length > 0 && smoke[0] != null) Destroy(smoke[0].texture);
            if (smoke != null) foreach (var s in smoke) if (s != null) Destroy(s);
        }

        public void Setup(FireConfig fireConfig, Kingdom owner, PixelWorld pixelWorld)
        {
            config = fireConfig != null ? fireConfig : ScriptableObject.CreateInstance<FireConfig>();
            kingdom = owner;
            world = pixelWorld;
            if (world != null) world.Damaged += OnDamaged;
            flame = ArtSprites.Get(ArtKeys.Flame);
            if (Application.isBatchMode) return;
            var atlas = new SpriteAtlasBuilder();
            for (int i = 0; i < 3; i++) atlas.Add(EffectArt.Smoke(i), $"Fire smoke {i}");
            smoke = atlas.Build("Fire smoke");
            for (int i = 0; i < puffs.Length; i++)
            {
                var go = new GameObject("Smoke");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sortingOrder = SortOrder.Smoke;
                r.enabled = false;
                puffs[i] = new Puff { Look = r, Life = -1f };
            }
        }

        // -------------------------------------------------------- starting --

        /// <summary>Sets fire to whatever wooden thing is at <paramref name="at"/>, with this chance. True if it caught.</summary>
        public bool Ignite(Vector2 at, float chance)
        {
            var thing = FlammableAt(new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y)));
            return thing != null && Ignite(thing, chance);
        }

        /// <summary>Each wooden thing within <paramref name="radius"/> tiles of <paramref name="at"/> catches fire with this chance. Returns how many did.</summary>
        public int IgniteNear(Vector2 at, float radius, float chance)
        {
            if (!On || chance <= 0f) return 0;
            scratch.Clear();
            int r = Mathf.CeilToInt(radius);
            var middle = new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var cell = middle + new Vector2Int(dx, dy);
                    if (((Vector2)cell + new Vector2(0.5f, 0.5f) - at).sqrMagnitude > (radius + 0.5f) * (radius + 0.5f)) continue;
                    var thing = FlammableAt(cell);
                    if (thing != null && !scratch.Contains(thing)) scratch.Add(thing);
                }
            int caught = 0;
            foreach (var thing in scratch) if (Ignite(thing, chance)) caught++;
            return caught;
        }

        /// <summary>The thing nearest <paramref name="at"/> within <paramref name="radius"/> that can burn catches fire (lightning).</summary>
        public bool IgniteNearest(Vector2 at, float radius, float chance)
        {
            if (!On) return false;
            Component best = null;
            float bestDistance = float.MaxValue;
            int r = Mathf.CeilToInt(radius);
            var middle = new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var cell = middle + new Vector2Int(dx, dy);
                    float d = ((Vector2)cell + new Vector2(0.5f, 0.5f) - at).sqrMagnitude;
                    if (d > radius * radius || d >= bestDistance) continue;
                    var thing = FlammableAt(cell);
                    if (thing == null) continue;
                    best = thing;
                    bestDistance = d;
                }
            return best != null && Ignite(best, chance);
        }

        bool On => config != null && config.fire;

        bool Ignite(Component thing, float chance)
        {
            if (!On || thing == null || byTarget.ContainsKey(thing) || blazes.Count >= config.maxFires) return false;
            if (chance < 1f && Random.value >= chance) return false;
            var blaze = new Blaze { Target = thing, Heat = 0.2f, SpreadAt = Time.time + Random.Range(0.5f, 1.5f) };
            if (thing is DestructibleStructure wall)
            {
                blaze.Wall = wall;
                blaze.Cells = wall.Cells;
            }
            else if (thing is IBuilt built)
            {
                blaze.Health = HealthOf(thing);
                blaze.Cells = built.Cells;
                blaze.IsHouse = thing is House;
            }
            blaze.Center = blaze.Cells.center;
            blaze.Look = MakeFlames(blaze);
            blazes.Add(blaze);
            byTarget[thing] = blaze;
            return true;
        }

        Transform MakeFlames(Blaze blaze)
        {
            if (flame == null || Application.isBatchMode) return null;
            var go = new GameObject("Fire");
            go.transform.SetParent(transform, false);
            go.transform.position = blaze.Center;
            int area = blaze.Cells.width * blaze.Cells.height;
            var offsets = new Vector2[Mathf.Clamp(area * 2, 2, 8)];
            for (int i = 0; i < offsets.Length; i++)
                offsets[i] = new Vector2(Random.Range(-0.38f, 0.38f) * blaze.Cells.width, Random.Range(-0.38f, 0.38f) * blaze.Cells.height);
            // Over roofs (a burning house shows), under shots and smoke. Only the first few dozen give light (lights cost a lot to draw).
            float light = blazes.Count < MaxLit ? 0.6f + 0.08f * offsets.Length : 0f;
            go.AddComponent<Flames>().Setup(flame, offsets, blaze.IsHouse ? 2.2f : 1.5f, light, SortOrder.Roofs + 20);
            go.transform.localScale = Vector3.one * 0.4f;
            return go.transform;
        }

        // ------------------------------------------------- what burns where --

        /// <summary>What at this cell can burn (a wood wall of this kingdom, a wooden door, piece of furniture, chest or house), or null.</summary>
        public Component FlammableAt(Vector2Int cell)
        {
            var chunk = world != null ? world.ChunkAt(cell) : null;
            if (chunk != null)
            {
                var wall = chunk.Owner;
                return wall != null && !wall.Remote && !wall.Collapsed && wall.Material != null && wall.Material.flammable ? wall : null;
            }
            if (kingdom == null) return null;
            var top = kingdom.ThingAt(cell);
            if (Burns(top)) return top;
            var under = kingdom.BuiltAt(cell) as Component;   // (a rug under a stone table)
            return Burns(under) ? under : null;
        }

        static bool Burns(Component thing)
        {
            if (thing == null || !(thing is IBuilt built) || built.Config == null || HealthOf(thing) == null) return false;
            if (thing is House house)
            {
                var level = house.Config.house != null ? house.Config.house.Level(house.Level) : null;
                return level != null && ((level.walls != null && level.walls.flammable) || level.roof == RoofStyle.Thatch);
            }
            return built.Config.flammable;
        }

        static Health HealthOf(Component c) =>
            c is Door d ? d.Health : c is Furniture f ? f.Health : c is House h ? h.Health : c is Storage s ? s.Health : c is MineShaft m ? m.Health : null;

        /// <summary>Whether this thing is burning.</summary>
        public bool OnFire(Component thing) => thing != null && byTarget.ContainsKey(thing);

        /// <summary>The fire at a cell (the thing there burning), or null.</summary>
        public Blaze BlazeAt(Vector2Int cell)
        {
            foreach (var b in blazes) if (!b.Out && b.Cells.Contains(cell)) return b;
            return null;
        }

        /// <summary>The nearest fire whose edge is within <paramref name="reach"/> of <paramref name="from"/>, or null.</summary>
        public Blaze Nearest(Vector2 from, float reach)
        {
            Blaze best = null;
            float bestDistance = reach;
            foreach (var b in blazes)
            {
                if (b.Out) continue;
                float d = BuildingLook.Distance(b.Cells, from);
                if (d <= bestDistance) { bestDistance = d; best = b; }
            }
            return best;
        }

        /// <summary>The nearest fire in the kingdom's lands (or a house or mine) that wants more villagers with water, or null.</summary>
        public Blaze NeedingHands(Vector2 from)
        {
            if (kingdom == null) return null;
            Blaze best = null;
            float bestDistance = float.MaxValue;
            foreach (var b in blazes)
            {
                if (b.Out || b.Fighters >= config.firefightersPerFire) continue;
                if (!kingdom.Nav.Cells.Contains(b.Cells.position) && kingdom.PlaceAt(b.Cells.position) == null) continue;
                float d = kingdom.Travel(from, b.Center);
                if (d < bestDistance) { bestDistance = d; best = b; }
            }
            return best;
        }

        /// <summary>Water (or beating) on a fire: takes <paramref name="heat"/> off. At none, it's out.</summary>
        public void Douse(Blaze blaze, float heat)
        {
            if (blaze == null || blaze.Out) return;
            blaze.Heat -= heat;
            if (blaze.Heat <= 0f) PutOut(blaze);
        }

        void PutOut(Blaze blaze)
        {
            if (blaze.Out) return;
            blaze.Out = true;
            blazes.Remove(blaze);
            if ((object)blaze.Target != null) byTarget.Remove(blaze.Target);
            if (blaze.Look != null) Destroy(blaze.Look.gameObject);
            // A last puff of steam and smoke.
            for (int i = 0; i < 3; i++) Smoke(blaze, 0.6f);
        }

        // ---------------------------------------------------------- burning --

        void Start()
        {
            seasons = Seasons.Current;
            if (seasons != null) seasons.LightningStruck += OnLightning;
        }

        void OnLightning(Vector2 at)
        {
            if (On) IgniteNearest(at, 2.5f, config.lightningIgnites);
        }

        // Explosions can set wooden things round them alight.
        void OnDamaged(DamageEvent hit, DamageType type, Vector2 center, int broken)
        {
            if (!On || type == null || type.category != DamageCategory.Explosive) return;
            float radius = Mathf.Max(type.blastRadius, type.radiusPixels / (float)PixelArt.PixelsPerUnit);
            IgniteNear(center, radius, config.explosionIgnites);
        }

        void Update()
        {
            if (blazes.Count == 0)
            {
                UpdateSmoke(Time.deltaTime);
                return;
            }
            float dt = Time.deltaTime, now = Time.time;
            // Rain puts fires out (not under a roof), snow keeps them from spreading.
            float rain = 0f, snow = 0f;
            if (seasons != null && seasons.On)
            {
                if (seasons.Weather == Weather.Rain || seasons.Weather == Weather.Storm) rain = seasons.Strength;
                snow = seasons.SnowCover;
            }
            for (int i = blazes.Count - 1; i >= 0; i--)
            {
                if (i >= blazes.Count) continue;
                var b = blazes[i];
                // Burnt away (or torn down, or put out meanwhile).
                bool gone = b.Wall != null ? b.Wall == null || b.Wall.Collapsed : b.Health == null || b.Health.Dead || b.Target == null;
                if (gone)
                {
                    PutOut(b);
                    continue;
                }
                bool outdoors = NatureMap.Inside(b.Cells.position) && (kingdom == null || kingdom.RoofAt(b.Cells.position) == null);
                b.Heat = Mathf.Min(1f, b.Heat + config.heatPerSecond * dt);
                if (rain > 0f && outdoors) b.Heat -= config.rainDousePerSecond * rain * dt * 2f;   // (twice: it has to beat the growing too)
                if (b.Heat <= 0f)
                {
                    PutOut(b);
                    continue;
                }
                // It burns.
                if (b.Wall != null) b.Wall.Batter(config.wallDamagePerSecond * b.Heat * dt);
                else b.Health.Damage((b.IsHouse ? config.houseDamagePerSecond : config.buildingDamagePerSecond) * b.Heat * dt, b.Center, true);
                if (b.Look != null) b.Look.localScale = Vector3.one * (0.4f + 0.6f * b.Heat);
                // Once a second: it may spread to wooden things next to it.
                if (now >= b.SpreadAt)
                {
                    b.SpreadAt = now + 1f;
                    float chance = config.spreadPerSecond * b.Heat * (1f - rain * 0.8f) * Mathf.Lerp(1f, config.snowSpread, snow);
                    if (chance > 0f) Spread(b, chance);
                }
                if (now >= b.SmokeAt)
                {
                    b.SmokeAt = now + Random.Range(0.35f, 0.7f) * (blazes.Count > 40 ? blazes.Count / 40f : 1f);
                    Smoke(b, b.Heat);
                }
            }
            UpdateSmoke(dt);
            if (now >= crackleAt && blazes.Count > 0)
            {
                crackleAt = now + Random.Range(0.4f, 0.9f);
                var b = blazes[Random.Range(0, blazes.Count)];
                Audio.Sounds.Play(Audio.Sound.Crackle, b.Center, 0.5f + 0.5f * b.Heat);
            }
        }

        // Wooden things touching it may catch.
        void Spread(Blaze b, float chance)
        {
            scratch.Clear();
            int reach = Mathf.Max(1, Mathf.CeilToInt(config.spreadReach));
            var c = b.Cells;
            for (int y = c.yMin - reach; y < c.yMax + reach; y++)
                for (int x = c.xMin - reach; x < c.xMax + reach; x++)
                {
                    if (x >= c.xMin && x < c.xMax && y >= c.yMin && y < c.yMax) continue;
                    var thing = FlammableAt(new Vector2Int(x, y));
                    if (thing != null && thing != b.Target && !scratch.Contains(thing)) scratch.Add(thing);
                }
            foreach (var thing in scratch) Ignite(thing, chance);
        }

        // ------------------------------------------------------------ smoke --

        void Smoke(Blaze b, float strength)
        {
            if (smoke == null || puffs[0] == null) return;
            var p = puffs[nextPuff];
            nextPuff = (nextPuff + 1) % puffs.Length;
            var wind = new Vector2(-0.35f, 0.55f);
            p.At = b.Center + new Vector2(Random.Range(-0.4f, 0.4f) * b.Cells.width, Random.Range(-0.4f, 0.4f) * b.Cells.height);
            p.Velocity = wind * Random.Range(0.6f, 1.2f) + Random.insideUnitCircle * 0.2f;
            p.Age = 0f;
            p.Life = Random.Range(2.5f, 4f);
            p.Size = (b.IsHouse ? 2.2f : 1.4f) * (0.6f + 0.6f * strength);
            p.Look.sprite = smoke[Random.Range(0, smoke.Length)];
            p.Look.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            p.Look.enabled = true;
        }

        void UpdateSmoke(float dt)
        {
            if (smoke == null) return;
            foreach (var p in puffs)
            {
                if (p == null || p.Life < 0f) continue;
                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Life = -1f;
                    p.Look.enabled = false;
                    continue;
                }
                float k = p.Age / p.Life;
                p.At += p.Velocity * dt;
                p.Look.transform.position = p.At;
                p.Look.transform.localScale = Vector3.one * p.Size * (0.6f + k * 1.6f);
                float shade = 0.25f + k * 0.25f;   // dark smoke, greying as it goes
                p.Look.color = new Color(shade, shade, shade, Mathf.Min(k * 5f, 1f) * (1f - k) * 0.55f);
            }
        }
    }
}
