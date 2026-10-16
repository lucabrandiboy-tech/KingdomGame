using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Building;
using KingdomGame.Economy;
using KingdomGame.World;
using UnityEngine;

namespace KingdomGame.Zombies
{
    /// <summary>
    /// A zombie base (a nest): zombies camp round it, more come back when they're killed. It can be
    /// shot, blown up or hit like a zombie; destroyed, it gives the kingdom experience.
    /// </summary>
    public sealed class ZombieBase : MonoBehaviour
    {
        public Health Health { get; private set; }
        public Vector2 Center => transform.position;
        internal readonly List<Zombie> Campers = new List<Zombie>();
        internal float NextCamperAt;
        /// <summary>Pollution it has breathed in and not yet sent zombies for.</summary>
        internal float Breathed;

        public void Setup(Vector2 at, float health, Sprite look, Sprite shadow)
        {
            transform.position = at;
            if (shadow != null) Look.AddShadow(transform, shadow, 0.3f);
            var go = new GameObject("Look");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = look;
            r.sortingOrder = SortOrder.Furniture;
            Health = gameObject.AddComponent<Health>();
            // A zombie-side "creature" so bullets, blows and blasts hit it like a zombie (a big one).
            Health.Setup(Team.Zombie, health, 1.3f, true);
        }
    }

    /// <summary>
    /// Zombie bases in survival: every few days (at dawn) a new one appears, well away from the
    /// throne. Zombies camp round each. Some nights a base besieges the kingdom: a crowd camps just
    /// outside it, going for anyone who comes out (villagers stay at their shelter flag while
    /// zombies are near), and goes home after a while. Where the factory's pollution reaches a base,
    /// it breathes it in and, day or night, sends zombies at the kingdom for it (Factorio's biters).
    /// Destroying a base gives experience (XP).
    /// </summary>
    public sealed class ZombieBases : MonoBehaviour
    {
        sealed class Siege
        {
            public ZombieBase From;
            public readonly List<Zombie> Crowd = new List<Zombie>();
            public float Until;
        }

        SurvivalConfig config;
        ZombieDirector director;
        SurvivalDirector waves;
        Kingdom kingdom;
        DayNightCycle dayNight;
        readonly List<ZombieBase> bases = new List<ZombieBase>();
        readonly List<Siege> sieges = new List<Siege>();
        int lastDay;
        bool wasNight;
        float breatheAt;

        public IReadOnlyList<ZombieBase> Bases => bases;

        public void Setup(SurvivalConfig survival, ZombieDirector zombies, Kingdom owner, DayNightCycle cycle, SurvivalDirector survivalWaves)
        {
            config = survival;
            director = zombies;
            waves = survivalWaves;
            kingdom = owner;
            dayNight = cycle;
            lastDay = dayNight != null ? dayNight.Day : 1;
            wasNight = dayNight != null && dayNight.IsNight;
        }

        void Update()
        {
            if (config == null || director == null || kingdom == null || kingdom.Throne == null) return;
            if (dayNight != null)
            {
                // A new day: every few days, a new base.
                if (dayNight.Day != lastDay)
                {
                    lastDay = dayNight.Day;
                    if (lastDay % Mathf.Max(1, config.baseEveryDays) == 0 && bases.Count < config.maxBases) TryPlace();
                }
                // Nightfall: each base may come and besiege the kingdom.
                bool night = dayNight.IsNight;
                if (night && !wasNight)
                    foreach (var b in bases)
                        if (b != null && Random.value < config.siegeChance) StartSiege(b);
                wasNight = night;
            }
            KeepCampers();
            EndSieges();
            if (Time.time >= breatheAt)
            {
                breatheAt = Time.time + 1f;
                Breathe(1f);
            }
        }

        // ------------------------------------------------------------ pollution --

        // Each base breathes in the pollution where it stands; enough of it, and it sends zombies at the kingdom.
        void Breathe(float seconds)
        {
            var factory = kingdom.Factory;
            var smog = factory != null ? factory.Pollution : null;
            var numbers = factory != null ? factory.Config : null;
            if (smog == null || numbers == null || smog.ChunkCount == 0) return;
            foreach (var b in bases)
            {
                if (b == null || b.Health == null || b.Health.Dead) continue;
                b.Breathed += smog.Take(Automation.PollutionMap.ChunkOf(b.Center.x), Automation.PollutionMap.ChunkOf(b.Center.y), numbers.baseBreathes * seconds);
                int group = WaveMath.AttackGroup(b.Breathed, numbers.pollutionPerAttacker);
                if (group < numbers.attackGroupLeast) continue;
                group = Mathf.Min(group, numbers.attackGroupMost);
                b.Breathed -= group * numbers.pollutionPerAttacker;
                Attack(b, group);
            }
        }

        // A group from a base comes for the kingdom (like a horde's zombies: through the walls if they must).
        void Attack(ZombieBase from, int count)
        {
            int further = waves != null ? waves.Evolution : 0;
            float toughness = WaveMath.Toughness((waves != null ? waves.Wave : 1) + further, config.toughnessPerWave);
            int sent = 0;
            for (int i = 0; i < count; i++)
            {
                var kind = PickKind();
                if (kind == null) break;
                if (director.Spawn(kind, from.Center + Random.insideUnitCircle * 3f, toughness) != null) sent++;
            }
            if (sent == 0) return;
            var way = from.Center - kingdom.Throne.Center;
            string[] compass = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
            int point = Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg, 360f) / 45f) % 8;
            kingdom.Report($"Your pollution reached a zombie base: {sent} zombies are coming from the {compass[point]}!");
            Audio.Sounds.PlayHere(Audio.Sound.Horn);
        }

        // ------------------------------------------------------------ bases --

        bool TryPlace()
        {
            var throne = kingdom.Throne.Center;
            var lands = kingdom.Nav != null ? kingdom.Nav.Cells : new RectInt(-256, -256, 512, 512);
            var nature = NatureMap.Current;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float a = Random.value * Mathf.PI * 2f, d = Random.Range(config.baseMinDistance, config.baseMaxDistance);
                var at = throne + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                var cell = new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));
                if (!lands.Contains(cell) || kingdom.ThingAt(cell) != null || kingdom.BuiltAt(cell) != null) continue;
                bool farFromOthers = true;
                foreach (var b in bases) if (b != null && (b.Center - at).sqrMagnitude < 40f * 40f) farFromOthers = false;
                if (!farFromOthers) continue;
                // Clear the ground round it (so its zombies can move about).
                if (nature != null)
                    for (int y = -4; y <= 4; y++)
                        for (int x = -4; x <= 4; x++)
                            if (x * x + y * y <= 16) nature.Clear(cell + new Vector2Int(x, y));
                Place((Vector2)cell + new Vector2(0.5f, 0.5f), config.baseHealth);
                kingdom.Report("A zombie base has appeared out in the wild. Destroy it for XP.");
                return true;
            }
            return false;
        }

        /// <summary>Puts a base at <paramref name="at"/> with <paramref name="health"/> hit points (a new one, or from a save).</summary>
        public ZombieBase Place(Vector2 at, float health)
        {
            var go = new GameObject("Zombie base");
            go.transform.SetParent(transform, false);
            var b = go.AddComponent<ZombieBase>();
            string key = ArtKeys.Turned(FurnitureArt.ZombieNest, 0);
            b.Setup(at, config.baseHealth, ArtSprites.Get(key), ArtSprites.Get(ArtKeys.Shadow(key)));
            if (health < config.baseHealth) b.Health.SetCurrent(Mathf.Max(1f, health));
            b.Health.Died += _ => Destroyed(b);
            bases.Add(b);
            director.KeepInView.Add(b.Center);
            director.Rewindow();
            return b;
        }

        void Destroyed(ZombieBase b)
        {
            bases.Remove(b);
            director.KeepInView.Remove(b.Center);
            // Its campers scatter towards the kingdom like any other zombie.
            foreach (var z in b.Campers) if (z != null) z.StopCamping();
            b.Campers.Clear();
            kingdom.AddXp(config.baseXp, "Zombie base destroyed");
            var world = Destruction.PixelWorld.Current;
            if (world != null) world.Burst(b.Center, new Color32(70, 40, 36, 255), 40, 5f);
            Destroy(b.gameObject, 0.05f);
            director.Rewindow();
        }

        // A kind of zombie for a camp, a siege or an attack: the kinds the waves (and the pollution) have got up to by now.
        ZombieConfig PickKind()
        {
            if (waves != null) return waves.PickKind(waves.Wave + waves.Evolution);
            var kinds = config.zombies;
            return kinds != null && kinds.Length > 0 ? kinds[0] : null;
        }

        // Each base keeps its campers topped up, one at a time.
        void KeepCampers()
        {
            foreach (var b in bases)
            {
                if (b == null) continue;
                b.Campers.RemoveAll(z => z == null || z.Health == null || z.Health.Dead);
                if (b.Campers.Count >= config.baseCampers || Time.time < b.NextCamperAt) continue;
                b.NextCamperAt = Time.time + config.baseCamperSeconds;
                var z = director.Spawn(PickKind(), b.Center + Random.insideUnitCircle * 2.5f, 1f);
                if (z == null) continue;
                z.Camp(b.Center, 5f, 8f, true);
                b.Campers.Add(z);
            }
        }

        // ----------------------------------------------------------- sieges --

        void StartSiege(ZombieBase from)
        {
            if (PickKind() == null) return;   // no zombies set up
            // Where they wait: just outside the kingdom, on the side facing their base.
            var throne = kingdom.Throne.Center;
            var dir = from.Center - throne;
            float along = Mathf.Repeat(Mathf.Atan2(dir.y, dir.x) / (Mathf.PI * 2f), 1f);
            if (!director.FindSpawnPoint(along, 10f, out var spot)) return;
            var siege = new Siege { From = from, Until = Time.time + config.siegeSeconds };
            for (int i = 0; i < config.siegeZombies; i++)
            {
                var z = director.Spawn(PickKind(), spot + Random.insideUnitCircle * 3f, 1f);
                if (z == null) continue;
                z.Camp(spot, 7f, 10f);
                siege.Crowd.Add(z);
            }
            sieges.Add(siege);
            kingdom.Report("A zombie siege! A crowd is waiting outside. Villagers stay at their shelter flag.");
            Audio.Sounds.PlayHere(Audio.Sound.Horn);
        }

        void EndSieges()
        {
            for (int i = sieges.Count - 1; i >= 0; i--)
            {
                var s = sieges[i];
                s.Crowd.RemoveAll(z => z == null || z.Health == null || z.Health.Dead);
                if (s.Crowd.Count > 0 && Time.time < s.Until) continue;
                // Time's up: back home to their base (or off into the dark if it's gone).
                foreach (var z in s.Crowd)
                {
                    if (s.From != null && s.From.Health != null && !s.From.Health.Dead) z.Camp(s.From.Center, 5f, 8f, true);
                    else director.Vanish(z);
                }
                sieges.RemoveAt(i);
            }
        }

        // ------------------------------------------------------------ saving --

        /// <summary>Each base's position and hit points, for saving.</summary>
        public List<(Vector2 At, float Health)> Saved()
        {
            var list = new List<(Vector2, float)>();
            foreach (var b in bases) if (b != null && b.Health != null && !b.Health.Dead) list.Add((b.Center, b.Health.Current));
            return list;
        }
    }
}
