using System;
using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.World;
using UnityEngine;

namespace KingdomGame.Zombies
{
    /// <summary>
    /// Runs every zombie on the map. It keeps the <see cref="FlowField"/> that leads
    /// them to the nearest person (worked out again a few times a second, walking
    /// round what can't be broken and smashing through what's quicker to break than
    /// to go round), moves them, keeps crowds from piling into one spot, and turns
    /// their blows on walls, doors and furniture into damage. One per survival game
    /// (<see cref="Current"/>); the <see cref="SurvivalDirector"/> decides when they come.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class ZombieDirector : MonoBehaviour
    {
        /// <summary>Breaking through this much door, wall or furniture health is worth about one tile of walking round.</summary>
        const float HealthPerTile = 8f;
        const float FieldSeconds = 0.3f;
        /// <summary>Zombies would rather go for the ruler: a villager has to be this many tiles closer.</summary>
        const float VillagerHeadStart = 2f;
        /// <summary>How hard zombies push each other apart, in tiles per second.</summary>
        const float CrowdPush = 2.5f;

        SurvivalConfig config;
        PixelWorld world;
        Kingdom kingdom;
        RulerController ruler;
        Health rulerHealth;
        DayNightCycle dayNight;
        RectInt map;
        FlowField field;
        bool[] fixedBlock;   // the map's edge, trees, rocks and bushes (what's built is checked as it is right now)
        float fieldAt;
        bool fieldDirty;
        int[] bucketHead;
        int[] bucketNext = new int[256];

        // A bar over a wall or door that zombies are hitting, for a few seconds after each blow.
        sealed class Bar
        {
            public SpriteRenderer Back, Fill;
            public Component Of;
            public DestructibleStructure Wall;
            public Health Health;
            public float Until;
            public bool Active;
        }
        const float BarSeconds = 4f;
        readonly List<Bar> bars = new List<Bar>();

        readonly List<Zombie> zombies = new List<Zombie>();
        readonly Dictionary<ZombieConfig, ZombieSprites> sprites = new Dictionary<ZombieConfig, ZombieSprites>();
        readonly List<int> targets = new List<int>();
        readonly List<float> headStarts = new List<float>();
        readonly HashSet<Place> housesWithPeople = new HashSet<Place>();

        public static ZombieDirector Current { get; private set; }

        public IReadOnlyList<Zombie> Zombies => zombies;
        /// <summary>Free build: F9 sends a small horde, to try traps and towers on.</summary>
        public bool TestHordeKey { get; set; }
        public int Alive => zombies.Count;
        public int Kills { get; private set; }
        /// <summary>Puts back the kill count from a saved game.</summary>
        public void RestoreKills(int kills) => Kills = Mathf.Max(0, kills);
        /// <summary>A zombie died (for counting and the HUD).</summary>
        public event Action<Zombie> ZombieDied;
        /// <summary>Zombies are faster at night...</summary>
        public float SpeedFactor => dayNight != null && dayNight.IsNight && config != null ? config.nightSpeed : 1f;
        /// <summary>...and hit harder.</summary>
        public float DamageFactor => dayNight != null && dayNight.IsNight && config != null ? config.nightDamage : 1f;

        void Awake()
        {
            if (Current != null && Current != this) Debug.LogWarning("There is more than one ZombieDirector in the scene.", this);
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (world != null) world.CellChanged -= OnCellChanged;
            if (kingdom != null) kingdom.Changed -= OnKingdomChanged;
        }

        // Something was built, broken or torn down: find the ways again straight away.
        void OnCellChanged(Vector2Int cell, bool added) => fieldDirty = true;
        void OnKingdomChanged() => fieldDirty = true;

        public void Setup(SurvivalConfig survival, PixelWorld pixelWorld, Kingdom owner, RulerController rulerController, DayNightCycle cycle, RectInt mapCells)
        {
            config = survival;
            world = pixelWorld;
            kingdom = owner;
            ruler = rulerController;
            rulerHealth = ruler != null ? ruler.Health : null;
            dayNight = cycle;
            map = mapCells;
            field = new FlowField(map.width, map.height);
            fixedBlock = new bool[map.width * map.height];
            bucketHead = new int[map.width * map.height];
            if (world != null) world.CellChanged += OnCellChanged;
            if (kingdom != null) kingdom.Changed += OnKingdomChanged;
            UpdateField();
        }

        // ------------------------------------------------------------- frame --

        void Update()
        {
            if (field == null) return;
            // The paths are only worked out while there are zombies to follow them (finding a
            // spawn point works them out first): on a big map it's a lot of work.
            if (zombies.Count > 0 && (Time.time >= fieldAt || (fieldDirty && Time.time >= fieldAt - FieldSeconds + 0.1f)))
                RefreshField();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (TestHordeKey && GameInput.Pressed(KeyCode.F9)) TestHorde();
            Crowd();
            for (int i = zombies.Count - 1; i >= 0; i--)
            {
                var z = zombies[i];
                if (z == null) { zombies.RemoveAt(i); continue; }
                z.Tick(dt);
            }
            UpdateBars();
        }

        // ------------------------------------------------------------ spawning --

        /// <summary>A new zombie of <paramref name="kind"/> at <paramref name="at"/>, its health times <paramref name="toughness"/>.</summary>
        public Zombie Spawn(ZombieConfig kind, Vector2 at, float toughness)
        {
            if (kind == null) return null;
            var go = new GameObject(kind.displayName);
            go.transform.SetParent(transform, false);
            var z = go.AddComponent<Zombie>();
            z.Setup(this, kind, SpritesOf(kind), toughness, at);
            zombies.Add(z);
            return z;
        }

        // Eight zombies from a random side of the map (free build, F9).
        void TestHorde()
        {
            if (config == null || config.zombies == null || config.zombies.Length == 0 || config.zombies[0] == null) return;
            if (!FindSpawnPoint(UnityEngine.Random.value, 14f, out var spot)) return;
            for (int i = 0; i < 8; i++)
                Spawn(config.zombies[0], spot + UnityEngine.Random.insideUnitCircle * 0.8f, 1f);
        }

        ZombieSprites SpritesOf(ZombieConfig kind)
        {
            if (sprites.TryGetValue(kind, out var s)) return s;
            string art = string.IsNullOrEmpty(kind.art) ? CharacterArt.ZombieKinds[0] : kind.art;
            s = new ZombieSprites
            {
                Frames = new Sprite[CharacterArt.ZombieDirections, CharacterArt.ZombieFrames],
                Shadow = ArtSprites.Get(ArtKeys.ZombieShadow(art)),
                Dead = new Sprite[CharacterArt.DeadDirections],
                Eyes = ArtSprites.Get(ArtKeys.ZombieEyes),
                EyesAt = new Vector2[CharacterArt.ZombieFrames],
                Scale = CharacterArt.ZombieScale(art),
            };
            for (int f = 0; f < CharacterArt.ZombieFrames; f++) s.EyesAt[f] = CharacterArt.ZombieEyesAt(art, f);
            for (int d = 0; d < CharacterArt.ZombieDirections; d++)
                for (int f = 0; f < CharacterArt.ZombieFrames; f++)
                    s.Frames[d, f] = ArtSprites.Get(ArtKeys.Zombie(art, d, f));
            for (int d = 0; d < CharacterArt.DeadDirections; d++) s.Dead[d] = ArtSprites.Get(ArtKeys.ZombieDead(art, d));
            if (s.Frames[0, 0] == null) Debug.LogWarning($"Zombie {kind.name} has art '{art}', which isn't one of: {string.Join(", ", CharacterArt.ZombieKinds)}.", kind);
            sprites[kind] = s;
            return s;
        }

        /// <summary>
        /// A spot to come from: on a ring round the kingdom (a little outside its outermost
        /// walls, and at least <paramref name="minDistance"/> tiles from the ruler, the villagers
        /// and the throne), around <paramref name="along"/> (0..1, once round anticlockwise from
        /// the east), where they can walk and from where someone can be reached (if anywhere can).
        /// On a big map the edge would be minutes away.
        /// </summary>
        public bool FindSpawnPoint(float along, float minDistance, out Vector2 spot)
        {
            if (Time.time >= fieldAt || (fieldDirty && Time.time >= fieldAt - FieldSeconds + 0.1f)) RefreshField();
            var center = kingdom != null && kingdom.Throne != null ? kingdom.Throne.Center : map.center;
            float radius = SpawnRadius(center, minDistance);
            // Past everything built (when the kingdom is too big for the ring to fit on the map,
            // the ring is pushed to the map's edge: only the spots on it past the kingdom will do).
            float beyond2 = (builtExtent + 2f) * (builtExtent + 2f);
            int steps = Mathf.Max(64, Mathf.CeilToInt(radius * Mathf.PI * 2f));
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < steps; i++)
                {
                    // Look outwards from the chosen spot, alternating sides.
                    int step = (i + 1) / 2 * (i % 2 == 0 ? 1 : -1);
                    float a = (Mathf.Repeat(along, 1f) + step / (float)steps) * Mathf.PI * 2f;
                    var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    p = new Vector2(Mathf.Clamp(p.x, map.xMin + 3, map.xMax - 3), Mathf.Clamp(p.y, map.yMin + 3, map.yMax - 3));
                    var cell = CellOf(p);
                    if (!Walkable(cell) || !FarFromEveryone(cell, minDistance)) continue;
                    // Pass 0: past the kingdom and able to reach someone; pass 1: past the kingdom; pass 2: anywhere out here.
                    if (pass < 2 && ((Vector2)cell + new Vector2(0.5f, 0.5f) - center).sqrMagnitude < beyond2) continue;
                    if (pass == 0 && float.IsPositiveInfinity(field.Distance(cell.x - map.xMin, cell.y - map.yMin))) continue;
                    spot = (Vector2)cell + new Vector2(UnityEngine.Random.Range(0.3f, 0.7f), UnityEngine.Random.Range(0.3f, 0.7f));
                    return true;
                }
            spot = default;
            return false;
        }

        float spawnRadius, spawnRadiusAt = -999f, builtExtent;

        void RefreshField()
        {
            fieldAt = Time.time + FieldSeconds;
            fieldDirty = false;
            UpdateField();
        }

        // How far out zombies appear: past the outermost wall, building, trap or tower, worked out every few seconds.
        float SpawnRadius(Vector2 center, float minDistance)
        {
            if (Time.time - spawnRadiusAt > 5f)
            {
                spawnRadiusAt = Time.time;
                float extent = 0f;
                if (world != null)
                    foreach (var cell in world.ChunkCells)
                        extent = Mathf.Max(extent, ((Vector2)cell + new Vector2(0.5f, 0.5f) - center).magnitude);
                if (kingdom != null)
                {
                    foreach (var s in kingdom.Storages) if (s != null && map.Contains(s.Cells.position)) extent = Mathf.Max(extent, (s.Center - center).magnitude);
                    foreach (var h in kingdom.Houses) if (h != null) extent = Mathf.Max(extent, (h.Center - center).magnitude);
                    foreach (var m in kingdom.Mines) if (m != null) extent = Mathf.Max(extent, (m.Center - center).magnitude);
                    // Furniture out on the map: traps, towers, barriers.
                    foreach (var f in kingdom.AllFurniture)
                        if (f != null && map.Contains(f.Cells.position)) extent = Mathf.Max(extent, (f.Cells.center - center).magnitude);
                }
                builtExtent = extent;
                spawnRadius = extent + 12f;
            }
            float most = Mathf.Min(map.width, map.height) * 0.5f - 3f;
            return Mathf.Clamp(Mathf.Max(spawnRadius, minDistance + 4f), Mathf.Min(16f, most), most);
        }

        bool FarFromEveryone(Vector2Int cell, float minDistance)
        {
            var at = (Vector2)cell + new Vector2(0.5f, 0.5f);
            float min2 = minDistance * minDistance;
            if (kingdom != null && kingdom.Throne != null && (kingdom.Throne.Center - at).sqrMagnitude < min2) return false;
            foreach (var h in Health.Creatures)
                if (h != null && h.Team == Team.Friendly && (h.Position - at).sqrMagnitude < min2) return false;
            return true;
        }

        // ------------------------------------------------------------- dying --

        /// <summary>A zombie died: count it, leave it lying there, and maybe drop loot.</summary>
        internal void Died(Zombie z, float facingDegrees, ZombieSprites pictures)
        {
            zombies.Remove(z);
            Kills++;
            var at = z.Position;
            var remains = Remains.Current;
            if (remains != null)
            {
                // It falls backwards, a little twisted.
                float a = facingDegrees + 180f + UnityEngine.Random.Range(-30f, 30f);
                int d = RulerLook.Direction(a, pictures.Dead.Length);
                remains.Body(pictures.Dead[d], at, a);
                remains.Blood(at, 1.1f);
            }
            if (world != null) world.Burst(at, new Color32(80, 10, 12, 255), 10, 2.5f);
            var c = z.Config;
            if (c.lootAmount > 0 && UnityEngine.Random.value < c.lootChance)
            {
                int wood = UnityEngine.Random.Range(0, c.lootAmount + 1);
                int stone = UnityEngine.Random.Range(0, c.lootAmount - wood + 1);
                LootBag.Drop(at, wood, stone, c.lootAmount - wood - stone);
            }
            ZombieDied?.Invoke(z);
        }

        /// <summary>A zombie that can't reach anyone wanders off into the dark (not a kill).</summary>
        internal void Vanish(Zombie z)
        {
            zombies.Remove(z);
            if (z != null) Destroy(z.gameObject);
        }

        // -------------------------------------------------------------- field --

        void UpdateField()
        {
            UpdateCosts();
            targets.Clear();
            headStarts.Clear();
            housesWithPeople.Clear();
            foreach (var h in Health.Creatures)
            {
                if (h == null || h.Dead || h.Team != Team.Friendly) continue;
                // Hiding in a house: they go for the house, and break it down to get in.
                var inside = kingdom != null ? kingdom.PlaceAt(h.Position) : null;
                if (inside != null)
                {
                    if (inside.Building != null && (Component)inside.Building != null && housesWithPeople.Add(inside))
                        foreach (var houseCell in inside.Building.Cells.allPositionsWithin) AddTarget(houseCell, h == rulerHealth ? 0f : VillagerHeadStart);
                    continue;
                }
                if (SeatedRuler(h))
                {
                    // On the throne: reached from the cells along its sides (through a wall there, if need be).
                    var r = kingdom.Throne.Cells;
                    for (int x = r.xMin; x < r.xMax; x++) { AddTarget(new Vector2Int(x, r.yMin - 1)); AddTarget(new Vector2Int(x, r.yMax)); }
                    for (int y = r.yMin; y < r.yMax; y++) { AddTarget(new Vector2Int(r.xMin - 1, y)); AddTarget(new Vector2Int(r.xMax, y)); }
                    continue;
                }
                var cell = CellOf(h.Position);
                if (Passable(cell)) AddTarget(cell, h == rulerHealth ? 0f : VillagerHeadStart);
            }
            field.Compute(targets, headStarts);
        }

        void AddTarget(Vector2Int cell, float headStart = 0f)
        {
            if (!Passable(cell)) return;
            targets.Add(Index(cell));
            headStarts.Add(headStart);
        }

        // What each cell takes to get through (see FlowField.Cost).
        void UpdateCosts()
        {
            var cost = field.Cost;
            int w = map.width, h = map.height;
            for (int i = 0; i < cost.Length; i++) cost[i] = 1f;
            // The edge of the map.
            for (int x = 0; x < w; x++) { cost[x] = 0f; cost[(h - 1) * w + x] = 0f; }
            for (int y = 0; y < h; y++) { cost[y * w] = 0f; cost[y * w + w - 1] = 0f; }
            // Trees, rocks and bushes: walked round.
            foreach (var node in ResourceNode.All)
                if (node != null && node.Blocks) Set(node.Cell, 0f);
            for (int i = 0; i < cost.Length; i++) fixedBlock[i] = cost[i] <= 0f;
            // Walls: the more battering one would take, the further zombies will walk to go round it.
            if (world != null)
                foreach (var cell in world.ChunkCells)
                {
                    var wall = world.ChunkAt(cell)?.Owner;
                    float left = wall != null ? wall.SiegeHealthLeft / Mathf.Max(1, wall.Cells.width * wall.Cells.height) : 0f;
                    Set(cell, 1.5f + left / HealthPerTile);
                }
            if (kingdom == null) return;
            // The throne can't be broken; storehouses, chests, doors and furniture can.
            if (kingdom.Throne != null) SetArea(kingdom.Throne.Cells, 0f);
            foreach (var s in kingdom.Storages)
                if (s != null) SetArea(s.Cells, s.Health != null ? 1.5f + s.Health.Current / HealthPerTile : 0f);
            foreach (var d in kingdom.Doors)
                if (d != null) SetArea(d.Cells, d.IsOpen || d.Health == null ? 1f : 1.5f + d.Health.Current / HealthPerTile);
            foreach (var house in kingdom.Houses)
                if (house != null && house.Health != null) SetArea(house.Cells, 1.5f + house.Health.Current / (HealthPerTile * house.Cells.width * house.Cells.height));
            foreach (var m in kingdom.Mines)
                if (m != null && m.Health != null) SetArea(m.Cells, 1.5f + m.Health.Current / (HealthPerTile * m.Cells.width * m.Cells.height));
            foreach (var f in kingdom.AllFurniture)
            {
                if (f == null || !f.Config.blocksMovement || f.Config.flat) continue;
                SetArea(f.Cells, f.Health != null ? 1.5f + f.Health.Current / HealthPerTile : 0f);
            }
        }

        void Set(Vector2Int cell, float value)
        {
            if (map.Contains(cell)) field.Cost[Index(cell)] = value;
        }

        void SetArea(RectInt cells, float value)
        {
            foreach (var cell in cells.allPositionsWithin) Set(cell, value);
        }

        // ------------------------------------------------------ for zombies --

        public Vector2Int CellOf(Vector2 position) => new Vector2Int(Mathf.FloorToInt(position.x), Mathf.FloorToInt(position.y));

        int Index(Vector2Int cell) => (cell.y - map.yMin) * map.width + (cell.x - map.xMin);

        bool Passable(Vector2Int cell) => map.Contains(cell) && field.Cost[Index(cell)] > 0f;

        /// <summary>
        /// Whether a zombie can walk into this cell right now: not the map's edge or a tree, and
        /// nothing built there that stops it (a wall, a closed door, the throne, a storehouse, a
        /// table...). Checked as things are this moment, so a wall built a moment ago already stops them.
        /// </summary>
        public bool Walkable(Vector2Int cell)
        {
            if (!map.Contains(cell) || fixedBlock[Index(cell)]) return false;
            if (world != null && world.ChunkAt(cell) != null) return false;
            if (kingdom == null) return true;
            switch (kingdom.ThingAt(cell))
            {
                case Door door: return door.IsOpen;
                case Throne _:
                case Storage _:
                case House _:
                case MineShaft _:
                    return false;
                case Furniture piece: return !piece.Config.blocksMovement || piece.Config.flat;
                default: return true;
            }
        }

        /// <summary>The middle of the open cell nearest to <paramref name="from"/> (for a zombie that something was built on).</summary>
        public Vector2 NearestOpenSpot(Vector2 from)
        {
            var cell = CellOf(from);
            float best = float.MaxValue;
            var spot = from;
            for (int radius = 1; radius <= 3 && best == float.MaxValue; radius++)
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        var c = new Vector2Int(cell.x + dx, cell.y + dy);
                        if (!Walkable(c)) continue;
                        var middle = (Vector2)c + new Vector2(0.5f, 0.5f);
                        float d = (middle - from).sqrMagnitude;
                        if (d < best) { best = d; spot = middle; }
                    }
            return spot;
        }

        /// <summary>Whether a zombie's body (a box <paramref name="halfSize"/> tiles from its middle) fits at <paramref name="p"/>.</summary>
        public bool Fits(Vector2 p, float halfSize) =>
            Walkable(CellOf(new Vector2(p.x - halfSize, p.y - halfSize))) && Walkable(CellOf(new Vector2(p.x + halfSize, p.y - halfSize))) &&
            Walkable(CellOf(new Vector2(p.x - halfSize, p.y + halfSize))) && Walkable(CellOf(new Vector2(p.x + halfSize, p.y + halfSize)));

        /// <summary>
        /// The next cell to step into from <paramref name="from"/> on the way to the nearest
        /// person. <paramref name="blocked"/>: something there has to be broken first. False if
        /// there's nowhere to go.
        /// </summary>
        public bool NextStep(Vector2 from, out Vector2Int next, out bool blocked)
        {
            var cell = CellOf(from);
            blocked = false;
            next = cell;
            if (!map.Contains(cell) || !field.Next(cell.x - map.xMin, cell.y - map.yMin, out int nx, out int ny)) return false;
            next = new Vector2Int(nx + map.xMin, ny + map.yMin);
            blocked = !Walkable(next);
            return true;
        }

        /// <summary>
        /// Whether nothing zombies can't walk through stands between two points (walls,
        /// closed doors, tables), including squeezing diagonally between two that touch at a corner.
        /// </summary>
        public bool ClearLine(Vector2 a, Vector2 b)
        {
            var line = b - a;
            float length = line.magnitude;
            if (length < 0.3f) return true;
            var start = CellOf(a);
            var end = CellOf(b);
            var previous = start;
            int samples = Mathf.CeilToInt(length / 0.2f);
            for (int i = 1; i <= samples; i++)
            {
                var cell = CellOf(a + line * (i / (float)samples));
                if (cell == previous) continue;
                if (cell != end && !Walkable(cell)) return false;
                if (cell.x != previous.x && cell.y != previous.y &&
                    !(Walkable(new Vector2Int(previous.x, cell.y)) && Walkable(new Vector2Int(cell.x, previous.y))))
                    return false;
                previous = cell;
            }
            return true;
        }

        bool SeatedRuler(Health h) => h != null && h == rulerHealth && ruler != null && ruler.Seated && kingdom != null && kingdom.Throne != null;

        /// <summary>Where to swing at someone: their middle, or for the ruler on the throne, the nearest edge of the throne.</summary>
        public Vector2 AimPoint(Health h, Vector2 from)
        {
            if (!SeatedRuler(h)) return h.Position;
            var r = kingdom.Throne.Cells;
            return new Vector2(Mathf.Clamp(from.x, r.xMin, r.xMax - 0.001f), Mathf.Clamp(from.y, r.yMin, r.yMax - 0.001f));
        }

        /// <summary>How far <paramref name="from"/> is from someone's body (from the throne, if the ruler is sitting on it).</summary>
        public float Gap(Health h, Vector2 from) =>
            SeatedRuler(h) ? (AimPoint(h, from) - from).magnitude : (h.Position - from).magnitude - h.Radius;

        /// <summary>The closest person within <paramref name="range"/> tiles that a zombie at <paramref name="from"/> can see.</summary>
        public Health FindPrey(Vector2 from, float range)
        {
            Health best = null;
            float bestDistance = range * range;
            foreach (var h in Health.Creatures)
            {
                if (h == null || h.Dead || h.Team != Team.Friendly || h.OutOfReach) continue;
                var at = AimPoint(h, from);
                float d = (at - from).sqrMagnitude;
                if (d < bestDistance && ClearLine(from, at))
                {
                    best = h;
                    bestDistance = d;
                }
            }
            return best;
        }

        /// <summary>The closest zombie within <paramref name="range"/> tiles that can see <paramref name="from"/> (villagers run from it).</summary>
        public Zombie NearestVisible(Vector2 from, float range)
        {
            Zombie best = null;
            float bestDistance = range * range;
            foreach (var z in zombies)
            {
                if (z == null) continue;
                float d = (z.Position - from).sqrMagnitude;
                if (d < bestDistance && ClearLine(z.Position, from))
                {
                    best = z;
                    bestDistance = d;
                }
            }
            return best;
        }

        /// <summary>A zombie's blow lands on what's in <paramref name="cell"/>: a wall wears down, a door or piece of furniture loses health.</summary>
        public void HitCell(Vector2Int cell, Zombie z)
        {
            float damage = DamageFactor;
            var chunk = world != null ? world.ChunkAt(cell) : null;
            if (chunk != null && chunk.Owner != null)
            {
                // A few scratches where it hits (just for looks)...
                var middle = (Vector2)cell + new Vector2(0.5f, 0.5f);
                if (z.Config.wallScratch != null && world.Raycast(z.Position, middle, out var pixel, out _)
                    && world.TryMakeEvent(z.Config.wallScratch, PixelWorld.PixelCenter(pixel.x, pixel.y), out var scratch))
                    world.ApplyDamage(scratch);
                // ...and the wall wears down until it caves in.
                var wall = chunk.Owner;
                wall.Batter(z.Config.wallDamage * damage);
                if (!wall.Collapsed) ShowBar(wall, wall, null);
                return;
            }
            if (kingdom == null) return;
            var thing = kingdom.ThingAt(cell);
            var target = thing is Door door ? door.Health : thing is Furniture piece ? piece.Health : thing is House house ? house.Health
                : thing is Storage store ? store.Health : thing is MineShaft shaft ? shaft.Health : null;
            if (target == null) return;
            target.Damage(z.Config.structureDamage * damage, z.Position);
            if (!target.Dead) ShowBar(target, null, target);
            // Thorns, stakes, spikes: the zombie gets hurt hitting it.
            if (thing is Furniture barrier && barrier.Config.defense != null && barrier.Config.defense.thorns > 0f && z.Health != null)
                z.Health.Damage(barrier.Config.defense.thorns, (Vector2)cell + new Vector2(0.5f, 0.5f));
        }

        // ---------------------------------------------------------- bars --

        void ShowBar(Component of, DestructibleStructure wall, Health health)
        {
            var white = kingdom != null ? kingdom.White : null;
            if (white == null) return;
            var bar = bars.Find(b => b.Active && b.Of == of);
            if (bar == null)
            {
                bar = bars.Find(b => !b.Active);   // a spare one
                if (bar == null)
                {
                    bar = new Bar { Back = BarPart("Damage bar", SortOrder.Markers, white), Fill = BarPart("Damage bar fill", SortOrder.Markers + 1, white) };
                    bar.Back.color = new Color(0f, 0f, 0f, 0.65f);
                    bars.Add(bar);
                }
                bar.Active = true;
                bar.Of = of;
                bar.Wall = wall;
                bar.Health = health;
            }
            bar.Until = Time.time + BarSeconds;
        }

        SpriteRenderer BarPart(string partName, int order, Sprite white)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = white;
            r.sortingOrder = order;
            return r;
        }

        // Keeps each bar over what it belongs to, showing how much is left; hides it a few seconds after the last blow.
        void UpdateBars()
        {
            foreach (var bar in bars)
            {
                if (!bar.Active) continue;
                bool gone = bar.Of == null || Time.time > bar.Until || (bar.Wall != null && bar.Wall.Collapsed) || (bar.Health != null && bar.Health.Dead);
                float fraction = 0f;
                RectInt cells = default;
                if (!gone && bar.Wall != null)
                {
                    float full = bar.Wall.Material != null ? bar.Wall.Material.siegeHealth * bar.Wall.Cells.width * bar.Wall.Cells.height : 1f;
                    fraction = bar.Wall.SiegeHealthLeft / Mathf.Max(1f, full);
                    cells = bar.Wall.Cells;
                }
                else if (!gone && bar.Health != null && bar.Health.GetComponent<IBuilt>() is IBuilt built)
                {
                    fraction = bar.Health.Fraction;
                    cells = built.Cells;
                }
                else gone = true;
                bar.Back.enabled = bar.Fill.enabled = !gone;
                if (gone)
                {
                    bar.Active = false;
                    bar.Of = null;
                    bar.Wall = null;
                    bar.Health = null;
                    continue;
                }
                // The white sprite is 4 x 4 pixels: scale it to the bar's size in tiles (like the blueprints' bars).
                float unit = PixelArt.PixelsPerUnit / 4f;
                float width = Mathf.Min(1.2f, cells.width * 0.8f), height = 0.09f;
                var at = new Vector2(cells.center.x, cells.yMax + 0.12f);
                fraction = Mathf.Clamp01(fraction);
                bar.Back.transform.position = at;
                bar.Back.transform.localScale = new Vector3(width * unit, height * unit, 1f);
                bar.Fill.transform.position = at - new Vector2(width * 0.5f * (1f - fraction), 0f);
                bar.Fill.transform.localScale = new Vector3(width * fraction * unit, height * 0.7f * unit, 1f);
                bar.Fill.color = Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(0.95f, 0.75f, 0.3f), fraction);
            }
        }

        // Pushes zombies that stand too close apart (only neighbours are compared: they're sorted by cell first).
        void Crowd()
        {
            int n = zombies.Count;
            if (n == 0) return;
            if (bucketNext.Length < n) bucketNext = new int[n * 2];
            for (int i = 0; i < bucketHead.Length; i++) bucketHead[i] = -1;
            for (int i = 0; i < n; i++)
            {
                var z = zombies[i];
                bucketNext[i] = -1;
                if (z == null) continue;
                var cell = CellOf(z.Position);
                if (!map.Contains(cell)) continue;
                int b = Index(cell);
                bucketNext[i] = bucketHead[b];
                bucketHead[b] = i;
            }
            for (int i = 0; i < n; i++)
            {
                var z = zombies[i];
                if (z == null) continue;
                var push = Vector2.zero;
                var cell = CellOf(z.Position);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var c = new Vector2Int(cell.x + dx, cell.y + dy);
                        if (!map.Contains(c)) continue;
                        for (int j = bucketHead[Index(c)]; j >= 0; j = bucketNext[j])
                        {
                            if (j == i) continue;
                            var other = zombies[j];
                            var away = z.Position - other.Position;
                            float min = z.Radius + other.Radius;
                            float d2 = away.sqrMagnitude;
                            if (d2 >= min * min) continue;
                            if (d2 < 1e-8f) away = new Vector2(Mathf.Cos(i * 2.4f), Mathf.Sin(i * 2.4f)) * 0.01f;   // right on top: split them
                            float d = Mathf.Sqrt(Mathf.Max(d2, 1e-8f));
                            push += away / Mathf.Max(d, 0.01f) * ((min - d) / min);
                        }
                    }
                z.Crowding = Vector2.ClampMagnitude(push, 1f) * CrowdPush;
            }
        }
    }
}
