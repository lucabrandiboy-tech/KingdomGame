using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Building;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.Villagers;
using KingdomGame.Zombies;
using UnityEngine;

namespace KingdomGame.Defenses
{
    /// <summary>
    /// A trap or tower at work (added to a piece of furniture whose building has a
    /// <see cref="DefenseConfig"/>). Traps spring on the first zombie that steps on them
    /// and set themselves again after a while; areas slow and wear down every zombie
    /// in them; mines blow up once and builders set a new one; towers turn to the
    /// nearest zombie in range and shoot it, with a guard on them in the early ages
    /// or by themselves later. Shots go through <see cref="Projectiles"/>, explosions
    /// through the <see cref="PixelWorld"/> (so they break walls like a rocket would).
    /// </summary>
    public sealed class Defense : MonoBehaviour
    {
        const float DamageTick = 0.5f;     // areas hurt in steps, not every frame
        const float SearchSeconds = 0.25f; // towers look for a new target this often

        static readonly List<Defense> all = new List<Defense>();
        /// <summary>Every trap and tower in the scene.</summary>
        public static IReadOnlyList<Defense> All => all;

        Furniture piece;
        DefenseConfig config;
        Kingdom kingdom;
        SpriteRenderer look, head;
        Sprite armedLook, sprungLook;
        Sprite[] heads;
        bool armed = true;
        float rearmAt, damageClock, shotReady, searchAt;
        float aim = 90f;
        float cloudUntil, cloudStarted, cloudClock;
        SpriteRenderer cloud;
        Zombie target;
        readonly List<Zombie> caught = new List<Zombie>();

        public DefenseConfig Config => config;
        public RectInt Cells => piece != null ? piece.Cells : default;
        public Vector2 Center => Cells.center;
        public string Name => piece != null ? piece.Config.displayName : name;
        /// <summary>A tower that only shoots with a guard on it.</summary>
        public bool NeedsGuard => config != null && config.kind == DefenseKind.Tower && config.needsGuard;
        /// <summary>The villager posted on it (or on their way), or null.</summary>
        public Villager Guard { get; set; }
        /// <summary>Ready to shoot: it needs no guard, or its guard is up on it.</summary>
        public bool Manned => !NeedsGuard || (Guard != null && Guard.OnPost(this));
        /// <summary>It has a turning head (a ballista, a cannon): its guard stands beside it, not on it.</summary>
        public bool HasHead => head != null;
        /// <summary>Ready (a trap or mine that hasn't gone off, or that has set itself again).</summary>
        public bool Armed => armed;

        public void Setup(Furniture furniture, DefenseConfig defense, SpriteRenderer lookRenderer, Kingdom owner)
        {
            piece = furniture;
            config = defense;
            kingdom = owner;
            look = lookRenderer;
            armedLook = look != null ? look.sprite : null;
            string art = furniture.Config.art;
            sprungLook = ArtSprites.Get(ArtKeys.Sprung(art));
            if (ArtSprites.Get(ArtKeys.Head(art, 0)) != null)
            {
                heads = new Sprite[FurnitureArt.HeadDirections];
                for (int d = 0; d < heads.Length; d++) heads[d] = ArtSprites.Get(ArtKeys.Head(art, d));
                var go = new GameObject("Head");
                go.transform.SetParent(transform, false);
                head = go.AddComponent<SpriteRenderer>();
                head.sortingOrder = SortOrder.Furniture + 2;
                ShowHead();
            }
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void Update()
        {
            if (config == null || piece == null) return;
            var director = ZombieDirector.Current;
            if (director == null) return;
            switch (config.kind)
            {
                case DefenseKind.Trap: Trap(director); break;
                case DefenseKind.Area: Area(director); break;
                case DefenseKind.Mine: Mine(director); break;
                case DefenseKind.Cloud: Cloud(director); break;
                case DefenseKind.Barrier: break;   // the zombies do the work: see ZombieDirector.HitCell
                default: Tower(director); break;
            }
        }

        // ------------------------------------------------------------- traps --

        void Trap(ZombieDirector director)
        {
            if (!armed)
            {
                if (Time.time < rearmAt) return;
                armed = true;
                if (look != null) look.sprite = armedLook;
            }
            var area = new Rect(Cells.position, Cells.size);
            caught.Clear();
            foreach (var z in director.Zombies)
            {
                if (z == null || z.Health == null || z.Health.Dead || z.Held || !area.Contains(z.Position)) continue;
                caught.Add(z);
                if (config.catchesOne) break;
            }
            if (caught.Count == 0) return;
            armed = false;
            rearmAt = Time.time + config.rearmSeconds;
            if (look != null && sprungLook != null) look.sprite = sprungLook;
            // Hurting can kill, and dying changes the director's list: so after the search.
            foreach (var z in caught)
            {
                if (config.holdSeconds > 0f) z.Hold(config.holdSeconds);
                z.Health.Damage(config.damage, Center);
            }
        }

        void Area(ZombieDirector director)
        {
            var area = new Rect(Cells.position, Cells.size);
            damageClock += Time.deltaTime;
            bool hurt = damageClock >= DamageTick;
            if (hurt) damageClock -= DamageTick;
            caught.Clear();
            foreach (var z in director.Zombies)
            {
                if (z == null || z.Health == null || z.Health.Dead || !area.Contains(z.Position)) continue;
                z.Slow(config.slowTo);
                if (hurt) caught.Add(z);
            }
            foreach (var z in caught) z.Health.Damage(config.damagePerSecond * DamageTick, z.Position);
        }

        // A gas or napalm trap: the first zombie on it sets off a cloud that hurts everything in it for a while.
        void Cloud(ZombieDirector director)
        {
            float now = Time.time;
            if (cloudUntil > now)
            {
                cloudClock += Time.deltaTime;
                bool hurt = cloudClock >= DamageTick;
                if (hurt) cloudClock -= DamageTick;
                float r2 = config.cloudRadius * config.cloudRadius;
                caught.Clear();
                if (hurt)
                    foreach (var z in director.Zombies)
                        if (z != null && z.Health != null && !z.Health.Dead && (z.Position - Center).sqrMagnitude <= r2) caught.Add(z);
                foreach (var z in caught) z.Health.Damage(config.cloudDamagePerSecond * DamageTick, z.Position);
                ShowCloud(now);
                return;
            }
            if (cloud != null && cloud.enabled) cloud.enabled = false;
            if (!armed)
            {
                if (now < rearmAt) return;
                armed = true;
                if (look != null) look.sprite = armedLook;
            }
            var area = new Rect(Cells.position, Cells.size);
            foreach (var z in director.Zombies)
            {
                if (z == null || z.Health == null || z.Health.Dead || !area.Contains(z.Position)) continue;
                armed = false;
                cloudUntil = now + config.cloudSeconds;
                cloudStarted = now;
                rearmAt = now + Mathf.Max(config.rearmSeconds, config.cloudSeconds);
                if (look != null && sprungLook != null) look.sprite = sprungLook;
                ShowCloud(now);
                return;
            }
        }

        // The cloud: a soft, slowly churning blot, growing in as it's let out and thinning before it's gone.
        void ShowCloud(float now)
        {
            if (cloud == null)
            {
                var go = new GameObject("Cloud");
                go.transform.SetParent(transform, false);
                cloud = go.AddComponent<SpriteRenderer>();
                cloud.sprite = CloudSprite();
                cloud.sortingOrder = SortOrder.Smoke;
                if (config.cloudGlows) Look.MakeGlow(cloud);
            }
            cloud.enabled = true;
            float age = now - cloudStarted, left = cloudUntil - now;
            float grow = Mathf.Clamp01(age / 0.6f), fade = Mathf.Clamp01(left / 1.5f);
            float size = config.cloudRadius * 2f * (0.6f + 0.4f * grow) * (1f + Mathf.Sin(now * 1.7f) * 0.04f);
            cloud.transform.localScale = new Vector3(size, size, 1f);
            cloud.transform.localRotation = Quaternion.Euler(0f, 0f, now * 12f);
            var c = config.cloudColor;
            c.a *= grow * fade * (0.85f + Mathf.Sin(now * 5.3f) * 0.15f);
            cloud.color = c;
        }

        static Sprite cloudSprite;

        // One soft, lumpy white blot (1 tile across) shared by every cloud; each tints it.
        static Sprite CloudSprite()
        {
            if (cloudSprite != null) return cloudSprite;
            const int size = 96;
            var img = new PixelImage(size, size);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = (x + 0.5f) / size - 0.5f, py = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Sqrt(px * px + py * py) * 2f;
                    float lumps = Noise.Fractal(px * 5f + 9f, py * 5f + 9f, 77u, 3);
                    float a = Mathf.Clamp01((1f - d) * 1.6f) * (0.55f + 0.45f * lumps);
                    img.Pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            cloudSprite = PixelTextures.ToSprite(img, "Cloud", false, size);
            return cloudSprite;
        }

        void Mine(ZombieDirector director)
        {
            if (!armed) return;
            float r2 = config.triggerRadius * config.triggerRadius;
            bool trip = false;
            foreach (var z in director.Zombies)
                if (z != null && z.Health != null && !z.Health.Dead && (z.Position - Center).sqrMagnitude <= r2) { trip = true; break; }
            if (!trip) return;
            armed = false;
            var world = PixelWorld.Current;
            if (config.blast != null && world != null && world.TryMakeEvent(config.blast, Center, out var hit)) world.ApplyDamage(hit);
            else Health.Blast(Center, 2f, config.damage, 0.35f);
            // Spent: builders set a new one in the same place (it costs again).
            if (kingdom != null) kingdom.Rebuild(piece);
        }

        // ------------------------------------------------------------ towers --

        void Tower(ZombieDirector director)
        {
            if (!Manned || config.weapon == null) return;
            float now = Time.time;
            if (target != null && (target.Health == null || target.Health.Dead || !InRange(target.Position))) target = null;
            if (target == null && now >= searchAt)
            {
                searchAt = now + SearchSeconds;
                target = Nearest(director);
            }
            if (target == null) return;
            var to = target.Position - Center;
            float wanted = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            aim = Mathf.MoveTowardsAngle(aim, wanted, config.turnSpeed * Time.deltaTime);
            ShowHead();
            if (Guard != null) Guard.Face(to);
            if (now < shotReady || Mathf.Abs(Mathf.DeltaAngle(aim, wanted)) > 8f) return;
            shotReady = now + 1f / config.shotsPerSecond;
            Fire(to);
        }

        bool InRange(Vector2 p)
        {
            float d2 = (p - Center).sqrMagnitude;
            return d2 <= config.range * config.range && d2 >= config.minRange * config.minRange;
        }

        Zombie Nearest(ZombieDirector director)
        {
            Zombie best = null;
            float bestDistance = float.MaxValue;
            foreach (var z in director.Zombies)
            {
                if (z == null || z.Health == null || z.Health.Dead || !InRange(z.Position)) continue;
                float d = (z.Position - Center).sqrMagnitude;
                if (d < bestDistance) { best = z; bestDistance = d; }
            }
            return best;
        }

        void Fire(Vector2 to)
        {
            var shots = Projectiles.Current;
            if (shots == null) return;
            var direction = (Vector2)(Quaternion.Euler(0f, 0f, aim) * Vector3.right);
            var from = Center + direction * 0.45f;
            // Up on a tower, shots fly over the walls below; lobbed ones land where they're aimed.
            if (config.lobbed)
                shots.Fire(config.weapon, from, direction, overWalls: 999f, range: Mathf.Max(0.5f, to.magnitude - 0.45f), lobbed: true);
            else
                shots.Fire(config.weapon, from, direction, overWalls: config.range + 1f, range: config.range, pierce: config.pierce);
        }

        void ShowHead()
        {
            if (head == null || heads == null) return;
            float step = 360f / heads.Length;
            int d = ((Mathf.RoundToInt(aim / step) % heads.Length) + heads.Length) % heads.Length;
            head.sprite = heads[d];
            head.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.DeltaAngle(d * step, aim));
        }
    }
}
