using KingdomGame.Building;
using KingdomGame.Combat;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Character
{
    /// <summary>
    /// The player's ruler: twin-stick controls (WASD to move, mouse to aim; the
    /// body turns to face the mouse). Left mouse fires the rifle, right mouse a
    /// rocket, G throws C4. E does whatever is closest:
    /// <list type="bullet">
    /// <item>at the throne: sit down (the throne view, for building);</item>
    /// <item>at a storehouse: empty your backpack into it;</item>
    /// <item>at a blueprint (hold): hand over what it needs from your backpack, then help build;</item>
    /// <item>at a tree, rock or bush (hold): gather wood, stone or food.</item>
    /// </list>
    /// Walking over a bag of loot puts it in the backpack. The ruler has
    /// <see cref="Health"/>: zombies hurt them, wounds heal after a while, and
    /// zombies can't be walked through. If the ruler falls, survival is over; in
    /// free build they're back at the throne after a few seconds.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RulerController : MonoBehaviour
    {
        const float MuzzleDistance = 0.55f;   // from the grip to the end of the barrel, in tiles

        [SerializeField] PlayerConfig config;
        [SerializeField] WeaponConfig primaryWeapon;
        [SerializeField] WeaponConfig secondaryWeapon;
        [SerializeField] ExplosiveConfig explosive;
        [SerializeField] Transform gun;
        [SerializeField] Projectiles projectiles;
        [SerializeField] Explosives explosives;

        Rigidbody2D body;
        RulerLook look;
        Camera view;
        CameraRig rig;
        Vector2 aim = Vector2.right;
        Vector2 wantedVelocity;
        float primaryReady, secondaryReady, explosiveReady, gatherReady, handOverReady;
        float noticeUntil;
        int stoodUpFrame = -1;
        float respawnAt;

        public Inventory Backpack { get; private set; }
        /// <summary>What E would do right now, for the HUD (null = nothing in reach).</summary>
        public string Hint { get; private set; }
        /// <summary>A short message for the HUD, like "Backpack full".</summary>
        public string Notice { get; private set; }
        /// <summary>Sitting on the throne (no walking or shooting).</summary>
        public bool Seated { get; private set; }
        /// <summary>Hit points (made in <see cref="Setup"/>).</summary>
        public Health Health { get; private set; }
        public bool Dead => Health != null && Health.Dead;
        /// <summary>Free build: come back at the throne after falling. Survival: falling is the end.</summary>
        public bool RespawnOnDeath { get; set; } = true;

        public void Setup(PlayerConfig playerConfig, WeaponConfig primary, WeaponConfig secondary, ExplosiveConfig thrown,
            Transform gunTransform, Projectiles shots, Explosives charges)
        {
            config = playerConfig;
            primaryWeapon = primary;
            secondaryWeapon = secondary;
            explosive = thrown;
            gun = gunTransform;
            projectiles = shots;
            explosives = charges;
            Backpack = new Inventory(config != null ? config.backpackCapacity : 250);
            if (Health == null)
            {
                Health = gameObject.AddComponent<Health>();
                Health.Setup(Team.Friendly, config != null ? config.maxHealth : 100f, config != null ? config.bodyRadius : 0.15f, true);
                Health.Hurt += OnHurt;
                Health.Died += OnDied;
            }
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            look = GetComponent<RulerLook>();
        }

        void Start()
        {
            view = Camera.main;
            if (view != null) rig = view.GetComponent<CameraRig>();
            if (config == null) Debug.LogError("RulerController has no Player Config.", this);
            if (Backpack == null) Backpack = new Inventory(config != null ? config.backpackCapacity : 250);
        }

        /// <summary>Sits on the throne: the body stops, and the throne's collider no longer pushes it.</summary>
        public void Sit(Throne throne)
        {
            Seated = true;
            wantedVelocity = Vector2.zero;
            Velocity = Vector2.zero;
            body.simulated = false;
            body.position = throne.Seat;
            transform.position = throne.Seat;
            Hint = null;
        }

        public void StandUp(Throne throne)
        {
            Seated = false;
            body.position = throne.Front;
            transform.position = throne.Front;
            body.simulated = true;
            stoodUpFrame = Time.frameCount; // the E that stood us up mustn't sit us straight back down
        }

        void Update()
        {
            if (config == null || Time.timeScale <= 0f) return;
            if (Dead)
            {
                if (RespawnOnDeath && Time.time >= respawnAt) Respawn();
                return;
            }
            Heal();
            if (Seated)
            {
                if (look != null) look.Show(Vector2.zero, Vector2.down, config.moveSpeed, config.stepsPerSecond, holdingGun: false);
                return;
            }

            wantedVelocity = GameInput.Move * config.moveSpeed;
            if (view != null)
            {
                Vector2 mouse = view.ScreenToWorldPoint(GameInput.PointerPosition);
                var toMouse = mouse - (Vector2)transform.position;
                if (toMouse.sqrMagnitude > 0.04f) aim = toMouse.normalized;
            }
            if (look != null) look.Show(Velocity, aim, config.moveSpeed, config.stepsPerSecond);

            float now = Time.time;
            if (primaryWeapon != null && (primaryWeapon.automatic ? GameInput.FireHeld : GameInput.FirePressed) && now >= primaryReady)
            {
                Shoot(primaryWeapon);
                primaryReady = now + 1f / primaryWeapon.fireRate;
            }
            if (secondaryWeapon != null && GameInput.AltFirePressed && now >= secondaryReady)
            {
                Shoot(secondaryWeapon);
                secondaryReady = now + 1f / secondaryWeapon.fireRate;
            }
            if (explosive != null && explosives != null && GameInput.Pressed(KeyCode.G) && now >= explosiveReady && view != null)
            {
                Vector2 target = view.ScreenToWorldPoint(GameInput.PointerPosition);
                explosives.Throw(explosive, Muzzle, target);
                explosiveReady = now + explosive.cooldown;
            }

            PickUpLoot();
            Interact(now);
            if (now > noticeUntil) Notice = null;
        }

        void FixedUpdate()
        {
            if (config == null || Seated || Dead) return;
            Velocity = Vector2.MoveTowards(Velocity, wantedVelocity, config.acceleration * Time.fixedDeltaTime);
            KeepOutOfZombies();
        }

        // Zombies can't be walked through: no walking into them, and they shove you out of the way.
        void KeepOutOfZombies()
        {
            var here = body.position;
            var v = Velocity;
            bool pushed = false;
            foreach (var h in Health.Creatures)
            {
                if (h == null || h.Team != Team.Zombie || h.Dead) continue;
                var away = here - h.Position;
                float min = config.bodyRadius + h.Radius;
                float d2 = away.sqrMagnitude;
                if (d2 >= min * min || d2 < 1e-8f) continue;
                float d = Mathf.Sqrt(d2);
                var n = away / d;
                float into = Vector2.Dot(v, n);
                if (into < 0f) v -= n * into;
                v += n * ((min - d) * 10f);
                pushed = true;
            }
            if (pushed) Velocity = v;
        }

        // ------------------------------------------------------------ health --

        // Wounds heal once the ruler hasn't been hurt for a while.
        void Heal()
        {
            if (Health == null || Health.Current >= Health.Max || Time.time - Health.LastHurt < config.healDelay) return;
            Health.Heal(config.healPerSecond * Time.deltaTime);
        }

        void OnHurt(Health h, float amount, Vector2 from)
        {
            // Attacked on the throne: stand up and fight.
            if (Seated && !h.Dead)
            {
                var builder = Kingdom.Current != null ? Kingdom.Current.GetComponent<BuildController>() : null;
                if (builder != null) builder.Leave();
                Say("You're under attack!");
            }
        }

        void OnDied(Health h)
        {
            if (Seated)
            {
                var builder = Kingdom.Current != null ? Kingdom.Current.GetComponent<BuildController>() : null;
                if (builder != null) builder.Leave();
            }
            wantedVelocity = Vector2.zero;
            Velocity = Vector2.zero;
            body.simulated = false;
            Hint = null;
            if (look != null) look.ShowDead(look.FacingDegrees);
            // Whatever was in the backpack is left where they fell.
            LootBag.DropAll(transform.position, Backpack);
            respawnAt = Time.time + (config != null ? config.respawnSeconds : 5f);
            Say(RespawnOnDeath ? "You fell! Your backpack is where you fell." : "You fell!");
        }

        void Respawn()
        {
            var throne = Kingdom.Current != null ? Kingdom.Current.Throne : null;
            Vector2 at = throne != null ? throne.Front : (Vector2)transform.position;
            body.position = at;
            transform.position = at;
            body.simulated = true;
            Health.Revive();
            if (look != null) look.ShowAlive();
            Say("You're back at the throne. Your backpack is where you fell.");
        }

        // Walking over a bag of loot empties it into the backpack.
        void PickUpLoot()
        {
            Vector2 here = transform.position;
            foreach (var bag in LootBag.All)
            {
                if (bag == null || (bag.Position - here).sqrMagnitude > 0.36f) continue;
                if (Backpack.SpaceLeft <= 0)
                {
                    Say("Your backpack is full: empty it at a storehouse (E)");
                    return;
                }
                string what = bag.Contents();
                if (bag.TakeInto(Backpack) > 0) Say($"Picked up {what}");
                return;   // one bag a frame (an emptied bag removes itself from the list)
            }
        }

        // Unity 6 renamed Rigidbody2D.velocity to linearVelocity.
        Vector2 Velocity
        {
#if UNITY_6000_0_OR_NEWER
            get => body.linearVelocity;
            set => body.linearVelocity = value;
#else
            get => body.velocity;
            set => body.velocity = value;
#endif
        }

        Vector2 Muzzle => gun != null ? (Vector2)gun.position + aim * MuzzleDistance : (Vector2)transform.position;

        void Shoot(WeaponConfig weapon)
        {
            if (projectiles == null) return;
            if (projectiles.Fire(weapon, Muzzle, aim) && rig != null)
                rig.AddShake(weapon.recoilShake);
        }

        // ------------------------------------------------------- interacting --

        void Interact(float now)
        {
            if (Time.frameCount == stoodUpFrame) return;
            Vector2 here = transform.position;
            var kingdom = Kingdom.Current;
            float reach = kingdom != null ? kingdom.Config.reach : 0.9f;

            // Whatever is closest wins.
            float best = float.MaxValue;
            Throne throne = null;
            Storage storage = null;
            Blueprint blueprint = null;
            ResourceNode node = null;
            if (kingdom != null)
            {
                if (kingdom.Throne != null && Closer(BuildingLook.Distance(kingdom.Throne.Cells, here), reach, ref best)) throne = kingdom.Throne;
                foreach (var s in kingdom.Storages)
                    if (Closer(BuildingLook.Distance(s.Cells, here), reach, ref best)) { storage = s; throne = null; }
                foreach (var b in kingdom.Blueprints)
                    if (b != null && !b.Cancelled && Closer(BuildingLook.Distance(b.Cells, here), reach, ref best)) { blueprint = b; storage = null; throne = null; }
            }
            foreach (var n in ResourceNode.All)
            {
                if (n == null || n.Empty || n.Config == null) continue;
                if (Closer(n.DistanceFrom(here), n.Config.reach, ref best)) { node = n; blueprint = null; storage = null; throne = null; }
            }

            if (throne != null) UseThrone(kingdom);
            else if (storage != null) UseStorage(storage);
            else if (blueprint != null) UseBlueprint(blueprint, now);
            else if (node != null) Gather(node, now);
            else Hint = null;
        }

        static bool Closer(float distance, float reach, ref float best)
        {
            if (distance > reach || distance >= best) return false;
            best = distance;
            return true;
        }

        void UseThrone(Kingdom kingdom)
        {
            Hint = "E: sit on the throne (build your kingdom)";
            if (!GameInput.Pressed(KeyCode.E)) return;
            var builder = kingdom.GetComponent<BuildController>();
            if (builder != null) builder.Enter();
        }

        void UseStorage(Storage storage)
        {
            Hint = Backpack.Total > 0
                ? "E: put your backpack in the storehouse"
                : $"Storehouse: {storage.Stock.Get(ResourceType.Wood)} wood, {storage.Stock.Get(ResourceType.Stone)} stone, {storage.Stock.Get(ResourceType.Food)} food";
            if (!GameInput.Pressed(KeyCode.E) || Backpack.Total <= 0) return;

            int stored = 0, kept = 0;
            for (int i = 0; i < Inventory.TypeCount; i++)
            {
                var type = (ResourceType)i;
                int have = Backpack.Get(type);
                if (have <= 0) continue;
                int put = storage.Deposit(type, have);
                Backpack.TryRemove(type, put);
                stored += put;
                kept += have - put;
            }
            Say(kept > 0 ? $"Stored {stored}. The storehouse is full ({kept} still in your backpack)" : $"Stored {stored}");
        }

        void UseBlueprint(Blueprint blueprint, float now)
        {
            // What it still needs that the backpack has.
            ResourceType? give = null;
            for (int i = 0; i < Inventory.TypeCount; i++)
            {
                var type = (ResourceType)i;
                if (blueprint.Need(type) > 0 && Backpack.Get(type) > 0) { give = type; break; }
            }

            if (give.HasValue) Hint = $"Hold E: give {give.Value.ToString().ToLowerInvariant()} to the {blueprint.Config.displayName}";
            else if (blueprint.HasAllResources) Hint = $"Hold E: help build the {blueprint.Config.displayName}";
            else Hint = BuildController.Describe(blueprint);
            if (!GameInput.Held(KeyCode.E)) return;

            if (give.HasValue)
            {
                if (now < handOverReady) return;
                var type = give.Value;
                int amount = Mathf.Min(5, Mathf.Min(blueprint.Need(type), Backpack.Get(type)));
                int used = blueprint.Deliver(type, amount);
                Backpack.TryRemove(type, used);
                handOverReady = now + 0.15f;
            }
            else if (blueprint.HasAllResources)
            {
                blueprint.AddWork(config.buildRate * Time.deltaTime);
            }
        }

        void Gather(ResourceNode node, float now)
        {
            Hint = $"Hold E to gather {node.Config.resource.ToString().ToLowerInvariant()}";
            if (!GameInput.Held(KeyCode.E) || now < gatherReady) return;
            if (Backpack.SpaceLeft <= 0)
            {
                Say("Backpack full: empty it at a storehouse (E)");
                return;
            }
            int got = node.Gather(Backpack.SpaceLeft);
            Backpack.Add(node.Config.resource, got);
            gatherReady = now + node.Config.secondsPerHit;
        }

        void Say(string text)
        {
            Notice = text;
            noticeUntil = Time.time + 2.5f;
        }
    }
}
