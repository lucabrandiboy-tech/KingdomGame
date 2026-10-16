using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.Zombies;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// The wild animals round the ruler (<see cref="Animal"/>): herds of deer out in the open, day and night (shy: they
    /// run from people, zombies and wolves; a dead one leaves a bag of meat), and from the second night on, packs of
    /// wolves that come in the dark, stalk anyone out of the firelight and leave at dawn. Hunters (a villager job) shoot
    /// deer and bring the meat home, and fight wolves; towers shoot wolves too.
    /// They come and go out of sight round wherever the ruler is, so the endless world always has some; they aren't
    /// saved, and each computer has its own (online, other players don't see yours). One per game (<see cref="Current"/>).
    /// </summary>
    public sealed class Wildlife : MonoBehaviour
    {
        const float CheckSeconds = 2f;
        const float PackCreep = 1.2f;    // tiles per second a pack's prowling spot creeps towards the ruler in the dark
        const float PackKeepOff = 12f;   // ...until it's this close

        sealed class Group
        {
            public bool Wolves;
            public Vector2 Home;
            public float DriftAt, HowlAt;
        }

        WildlifeConfig config;
        Kingdom kingdom;
        DayNightCycle dayNight;
        Camera view;
        readonly List<Animal> animals = new List<Animal>();
        readonly Dictionary<int, Group> groups = new Dictionary<int, Group>();
        readonly Dictionary<AnimalKind, AnimalSprites> sprites = new Dictionary<AnimalKind, AnimalSprites>();
        readonly List<int> emptyGroups = new List<int>();
        int nextGroup = 1;
        float checkAt, herdAt, packAt, wolvesForcedUntil;

        public static Wildlife Current { get; private set; }
        /// <summary>An animal was killed (goals count the deer).</summary>
        public event System.Action<Animal> Killed;
        public WildlifeConfig Config => config;
        public IReadOnlyList<Animal> Animals => animals;
        /// <summary>Where the ruler is (animals come and go round it).</summary>
        public Vector2 Focus { get; private set; }
        /// <summary>Night for the wolves (or the cheat menu called them).</summary>
        public bool Night => Time.time < wolvesForcedUntil || (dayNight != null && dayNight.IsNight);
        /// <summary>How many wolves are about (not leaving).</summary>
        public int WolvesOut
        {
            get { int n = 0; foreach (var a in animals) if (a != null && a.IsWolf && !a.Leaving) n++; return n; }
        }

        public void Setup(WildlifeConfig wildlifeConfig, Kingdom owner, DayNightCycle cycle)
        {
            config = wildlifeConfig;
            kingdom = owner;
            dayNight = cycle;
            Current = this;
            checkAt = Time.time + 1f;
            packAt = Time.time + Random.Range(10f, 30f);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Update()
        {
            if (config == null || !config.wildlife || Time.time < checkAt) return;
            checkAt = Time.time + CheckSeconds;
            if (view == null) view = Camera.main;
            var ruler = kingdom != null ? kingdom.Ruler : null;
            if (ruler == null) return;
            Vector2 at = ruler.position;
            // Inside a house or down a mine (far off the map): nothing comes or goes meanwhile.
            if (!NatureMap.Inside(new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y)))) return;
            Focus = at;
            TidyUp();
            Herds();
            Packs();
        }

        // Animals far off and out of sight go (new ones come instead); so do wolves well on their way.
        void TidyUp()
        {
            for (int i = animals.Count - 1; i >= 0; i--)
            {
                var a = animals[i];
                if (a == null) { animals.RemoveAt(i); continue; }
                float d = (a.Position - Focus).magnitude;
                bool gone = d > config.goneBeyond || (a.Leaving && (d > config.spawnNear || a.LeftFor > 90f));
                if (!gone || InView(a.Position, 3f)) continue;
                animals.RemoveAt(i);
                Destroy(a.gameObject);
            }
            emptyGroups.Clear();
            foreach (var pair in groups)
            {
                bool any = false;
                foreach (var a in animals) if (a != null && a.Group == pair.Key) { any = true; break; }
                if (!any) emptyGroups.Add(pair.Key);
            }
            foreach (int g in emptyGroups) groups.Remove(g);
        }

        void Herds()
        {
            int deer = 0;
            foreach (var a in animals) if (a != null && !a.IsWolf) deer++;
            bool winter = Seasons.Current != null && Seasons.Current.Winter;
            int want = Mathf.RoundToInt(config.deer * (winter ? config.winterDeer : 1f));
            int smallest = Mathf.Max(1, config.herdSize.x);
            if (deer + smallest <= want && Time.time >= herdAt && FindSpot(Focus, config.spawnNear, config.spawnFar, false, out var spot))
            {
                herdAt = Time.time + 8f;
                SpawnHerd(spot, Random.Range(smallest, Mathf.Clamp(want - deer, smallest, Mathf.Max(smallest, config.herdSize.y)) + 1));
            }
            // Herds drift about slowly as they graze.
            foreach (var g in groups.Values)
            {
                if (g.Wolves || Time.time < g.DriftAt) continue;
                g.DriftAt = Time.time + Random.Range(20f, 40f);
                g.Home = SpotNear(g.Home, 4f, false, g.Home);
            }
        }

        void Packs()
        {
            var zombies = ZombieDirector.Current;
            bool quiet = zombies == null || zombies.Alive == 0;   // (wolves keep away from a horde)
            int day = dayNight != null ? dayNight.Day : 1;
            if (Night && config.wolfPacks > 0 && (day >= config.firstWolfNight || Time.time < wolvesForcedUntil) && quiet && Time.time >= packAt)
            {
                int packs = 0;
                foreach (var g in groups.Values) if (g.Wolves) packs++;
                if (packs < config.wolfPacks && FindSpot(Focus, config.spawnNear * 0.8f, config.spawnFar * 0.8f, true, out var spot))
                {
                    packAt = Time.time + Random.Range(config.packSeconds.x, Mathf.Max(config.packSeconds.x, config.packSeconds.y));
                    SpawnPack(spot, Random.Range(Mathf.Max(1, config.packSize.x), Mathf.Max(1, config.packSize.y) + 1));
                }
            }
            // Prowling packs creep closer in the dark (not into the light), and howl now and then.
            foreach (var pair in groups)
            {
                var g = pair.Value;
                if (!g.Wolves) continue;
                var to = Focus - g.Home;
                if (to.magnitude > PackKeepOff)
                {
                    var next = g.Home + to.normalized * Mathf.Min(PackCreep * CheckSeconds, to.magnitude - PackKeepOff);
                    if (!Lit(next)) g.Home = next;
                }
                if (Time.time >= g.HowlAt && Night)
                {
                    g.HowlAt = Time.time + Random.Range(25f, 60f);
                    foreach (var a in animals)
                        if (a != null && a.Group == pair.Key && !a.Hunting && !a.Leaving) { Audio.Sounds.Play(Audio.Sound.Howl, a.Position); break; }
                }
            }
        }

        // ------------------------------------------------------- spawning --

        /// <summary>A herd of <paramref name="count"/> deer round <paramref name="at"/>.</summary>
        public void SpawnHerd(Vector2 at, int count)
        {
            int id = nextGroup++;
            groups[id] = new Group { Home = at, DriftAt = Time.time + Random.Range(20f, 40f) };
            for (int i = 0; i < count; i++) Make(AnimalKind.Deer, SpotNear(at, 2.5f, false, at), id);
        }

        /// <summary>A pack of <paramref name="count"/> wolves round <paramref name="at"/> (they howl as they come).</summary>
        public void SpawnPack(Vector2 at, int count)
        {
            int id = nextGroup++;
            groups[id] = new Group { Wolves = true, Home = at, HowlAt = Time.time + Random.Range(25f, 60f) };
            for (int i = 0; i < count; i++) Make(AnimalKind.Wolf, SpotNear(at, 2f, false, at), id);
            Audio.Sounds.Play(Audio.Sound.Howl, at);
        }

        /// <summary>Wolves now, whatever the time (the cheat menu): for <paramref name="seconds"/>, a pack <paramref name="distance"/> tiles off.</summary>
        public bool CallWolves(float seconds, float distance)
        {
            wolvesForcedUntil = Time.time + seconds;
            if (kingdom == null || kingdom.Ruler == null) return false;
            Focus = kingdom.Ruler.position;
            if (!FindSpot(Focus, distance, distance + 6f, true, out var spot, false)) return false;
            SpawnPack(spot, Random.Range(Mathf.Max(1, config.packSize.x), Mathf.Max(1, config.packSize.y) + 1));
            return true;
        }

        /// <summary>A herd <paramref name="distance"/> tiles from the ruler, even in sight (the cheat menu).</summary>
        public bool CallDeer(float distance)
        {
            if (kingdom == null || kingdom.Ruler == null) return false;
            Focus = kingdom.Ruler.position;
            if (!FindSpot(Focus, distance, distance + 6f, false, out var spot, false)) return false;
            SpawnHerd(spot, Mathf.Max(1, config.herdSize.y));
            return true;
        }

        void Make(AnimalKind kind, Vector2 at, int group)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<Animal>();
            a.Setup(this, kind, SpritesOf(kind), at, group);
            animals.Add(a);
        }

        AnimalSprites SpritesOf(AnimalKind kind)
        {
            if (sprites.TryGetValue(kind, out var s)) return s;
            string name = kind.ToString();
            s = new AnimalSprites
            {
                Frames = new Sprite[CharacterArt.AnimalDirections, CharacterArt.AnimalFrames],
                Dead = new Sprite[CharacterArt.DeadDirections],
                Shadow = ArtSprites.Get(ArtKeys.AnimalShadow(name)),
                Eyes = kind == AnimalKind.Wolf ? ArtSprites.Get(ArtKeys.ZombieEyes) : null,
            };
            for (int d = 0; d < CharacterArt.AnimalDirections; d++)
                for (int f = 0; f < CharacterArt.AnimalFrames; f++)
                    s.Frames[d, f] = ArtSprites.Get(ArtKeys.Animal(name, d, f));
            for (int d = 0; d < CharacterArt.DeadDirections; d++) s.Dead[d] = ArtSprites.Get(ArtKeys.AnimalDead(name, d));
            sprites[kind] = s;
            return s;
        }

        // A spot between `near` and `far` tiles from `around`: open ground (not in a thicket), not by the throne, out of
        // sight (unless `hidden` is false), and for wolves (`dark`) out of the firelight.
        bool FindSpot(Vector2 around, float near, float far, bool dark, out Vector2 spot, bool hidden = true)
        {
            var throne = kingdom != null && kingdom.Throne != null ? kingdom.Throne.Center : (Vector2?)null;
            float clear2 = config.kingdomClear * config.kingdomClear;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), d = Random.Range(near, Mathf.Max(near, far));
                var p = around + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                var cell = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
                if (!NatureMap.Inside(cell) || !Open(cell)) continue;
                int open = 0;
                for (int y = -1; y <= 1; y++)
                    for (int x = -1; x <= 1; x++)
                        if (Open(new Vector2Int(cell.x + x, cell.y + y))) open++;
                if (open < 8) continue;
                if (hidden && throne.HasValue && (throne.Value - p).sqrMagnitude < clear2) continue;
                if (hidden && InView(p, 4f)) continue;
                if (dark && Lit(p)) continue;
                spot = (Vector2)cell + new Vector2(0.5f, 0.5f);
                return true;
            }
            spot = default;
            return false;
        }

        // ---------------------------------------------------- for animals --

        /// <summary>Where a herd or pack keeps to.</summary>
        public Vector2 HomeOf(int group) => groups.TryGetValue(group, out var g) ? g.Home : Focus;

        /// <summary>A herd ran and stopped: it gathers there.</summary>
        public void Regroup(int group, Vector2 at)
        {
            if (groups.TryGetValue(group, out var g)) g.Home = at;
        }

        /// <summary>A deer took fright: the rest of its herd close by runs too.</summary>
        public void Alarm(Animal from, Vector2 danger)
        {
            foreach (var a in animals)
                if (a != null && a != from && a.Group == from.Group && (a.Position - from.Position).sqrMagnitude < 144f) a.Startle(danger, false);
        }

        /// <summary>An open spot within <paramref name="radius"/> of <paramref name="around"/> (out of the firelight, for wolves), or <paramref name="fallback"/>.</summary>
        public Vector2 SpotNear(Vector2 around, float radius, bool dark, Vector2 fallback)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var p = around + Random.insideUnitCircle * radius;
                if (Fits(p, 0.2f) && (!dark || !Lit(p))) return p;
            }
            return fallback;
        }

        /// <summary>
        /// What a deer runs from: the nearest person or zombie within its shy distance (or wolf a little further off),
        /// and where it is. People up on walls count too (they can be seen).
        /// </summary>
        public bool Danger(Animal deer, out Vector2 from)
        {
            from = default;
            float best = config.deerShyDistance * config.deerShyDistance;
            bool found = false;
            var at = deer.Position;
            foreach (var h in Health.Creatures)
            {
                if (h == null || h.Dead || h.Team == Team.Wild) continue;
                float d = (h.Position - at).sqrMagnitude;
                if (d < best) { best = d; from = h.Position; found = true; }
            }
            float wolfRange = config.deerShyDistance + 3f;
            float wolf2 = wolfRange * wolfRange;
            foreach (var a in animals)
            {
                if (a == null || !a.IsWolf || a.Health == null || a.Health.Dead) continue;
                float d = (a.Position - at).sqrMagnitude;
                if (d < wolf2 && (!found || d < best)) { best = d; from = a.Position; found = true; }
            }
            return found;
        }

        /// <summary>Who a wolf goes for: the nearest person within its sight who is out in the dark (not up on a wall or tower).</summary>
        public Health PreyFor(Animal wolf)
        {
            Health best = null;
            float bestDistance = config.wolfSight * config.wolfSight;
            var at = wolf.Position;
            foreach (var h in Health.Creatures)
            {
                if (h == null || h.Dead || h.Team != Team.Friendly || h.OutOfReach) continue;
                float d = (h.Position - at).sqrMagnitude;
                if (d >= bestDistance || Lit(h.Position)) continue;
                best = h;
                bestDistance = d;
            }
            return best;
        }

        /// <summary>The nearest wolf within <paramref name="range"/> tiles that's after someone (villagers run from it, hunters shoot it).</summary>
        public Animal NearestWolf(Vector2 at, float range)
        {
            Animal best = null;
            float bestDistance = range * range;
            foreach (var a in animals)
            {
                if (a == null || !a.IsWolf || a.Leaving || a.Health == null || a.Health.Dead) continue;
                float d = (a.Position - at).sqrMagnitude;
                if (d < bestDistance) { best = a; bestDistance = d; }
            }
            return best;
        }

        /// <summary>
        /// What a hunter at <paramref name="from"/> goes after: a wolf about, else the nearest deer within
        /// <paramref name="range"/> tiles, inside <paramref name="lands"/>, that no other hunter is after.
        /// </summary>
        public Animal FindQuarry(Vector2 from, float range, RectInt lands, Component hunter)
        {
            Animal best = null;
            float bestDistance = range * range;
            foreach (var a in animals)
            {
                if (a == null || a.Health == null || a.Health.Dead || a.Leaving || Time.time < a.UnreachableUntil) continue;
                if (a.Hunter != null && a.Hunter != hunter) continue;
                if (!lands.Contains(new Vector2Int(Mathf.FloorToInt(a.Position.x), Mathf.FloorToInt(a.Position.y)))) continue;
                float d = (a.Position - from).sqrMagnitude;
                if (d >= range * range) continue;
                if (a.IsWolf) d *= 0.25f;   // wolves first
                if (d < bestDistance) { best = a; bestDistance = d; }
            }
            return best;
        }

        /// <summary>Whether a fire's light keeps wolves off <paramref name="p"/>.</summary>
        public bool Lit(Vector2 p)
        {
            float fear = config.fireFear;
            if (fear <= 0f) return false;
            foreach (var light in FireLight.All)
            {
                if (light == null) continue;
                float r = light.Radius * fear;
                if (((Vector2)light.transform.position - p).sqrMagnitude < r * r) return true;
            }
            return false;
        }

        /// <summary>Whether an animal can walk in this cell: no tree, rock or bush, no wall, nothing built that's in the way (a closed door is).</summary>
        public bool Open(Vector2Int cell)
        {
            var nature = NatureMap.Current;
            if (nature != null ? nature.Blocks(cell) : !NatureMap.Inside(cell)) return false;
            var world = PixelWorld.Current;
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

        /// <summary>Whether an animal's body (a box <paramref name="halfSize"/> tiles from its middle) fits at <paramref name="p"/>.</summary>
        public bool Fits(Vector2 p, float halfSize) =>
            Open(Cell(p.x - halfSize, p.y - halfSize)) && Open(Cell(p.x + halfSize, p.y - halfSize)) &&
            Open(Cell(p.x - halfSize, p.y + halfSize)) && Open(Cell(p.x + halfSize, p.y + halfSize));

        static Vector2Int Cell(float x, float y) => new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y));

        /// <summary>The middle of the open cell nearest to <paramref name="from"/> (for an animal something was built on).</summary>
        public Vector2 NearestOpenSpot(Vector2 from)
        {
            var cell = Cell(from.x, from.y);
            float best = float.MaxValue;
            var spot = from;
            for (int radius = 1; radius <= 4 && best == float.MaxValue; radius++)
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        var c = new Vector2Int(cell.x + dx, cell.y + dy);
                        if (!Open(c)) continue;
                        var middle = (Vector2)c + new Vector2(0.5f, 0.5f);
                        float d = (middle - from).sqrMagnitude;
                        if (d < best) { best = d; spot = middle; }
                    }
            return spot;
        }

        bool InView(Vector2 p, float margin)
        {
            if (view == null) return false;
            float halfH = view.orthographicSize + margin, halfW = view.orthographicSize * view.aspect + margin;
            var c = (Vector2)view.transform.position;
            return Mathf.Abs(p.x - c.x) < halfW && Mathf.Abs(p.y - c.y) < halfH;
        }

        // ---------------------------------------------------------- dying --

        /// <summary>An animal died: it lies there, and leaves a bag of meat.</summary>
        internal void Died(Animal a, float facingDegrees, AnimalSprites pictures)
        {
            animals.Remove(a);
            var at = a.Position;
            var remains = Remains.Current;
            if (remains != null && pictures.Dead != null && pictures.Dead.Length > 0)
            {
                float turn = facingDegrees + Random.Range(-40f, 40f);
                remains.Body(pictures.Dead[RulerLook.Direction(turn, pictures.Dead.Length)], at, turn);
                remains.Blood(at, 0.9f);
            }
            int meat = a.IsWolf ? config.wolfMeat : config.deerMeat;
            if (meat > 0) LootBag.Drop(at, 0, 0, meat);
            Killed?.Invoke(a);
        }
    }
}
