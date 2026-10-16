using KingdomGame.Building;
using KingdomGame.Combat;
using KingdomGame.Economy;
using KingdomGame.Villagers;
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
    /// At a ladder or stairs, E climbs onto the wall it leans on: up there the
    /// ruler walks along walls and gates, out of zombies' reach (they batter the
    /// wall underneath instead), and shots and charges fly over the walls below.
    /// E climbs back down a ladder, or jumps down. A wall that caves in drops them.
    /// Walking over a bag of loot puts it in the backpack. The ruler has
    /// <see cref="Health"/>: zombies hurt them, wounds heal after a while, and
    /// zombies can't be walked through. If the ruler falls, survival is over; in
    /// free build they're back at the throne after a few seconds.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RulerController : MonoBehaviour
    {
        const float MuzzleDistance = 0.55f;   // from the grip to the end of the barrel, in tiles
        /// <summary>From up on a wall, shots fly over walls for this many tiles (charges for less).</summary>
        const float OverWallsFromTop = 3f;
        /// <summary>Damage from jumping off a wall, and from falling when it caves in.</summary>
        const float JumpDamage = 8f, FallDamage = 12f;

        [SerializeField] PlayerConfig config;
        [SerializeField] WeaponConfig primaryWeapon;
        [SerializeField] WeaponConfig secondaryWeapon;
        [SerializeField] ExplosiveConfig explosive;
        [SerializeField] Transform gun;
        [SerializeField] Projectiles projectiles;
        [SerializeField] Explosives explosives;

        Rigidbody2D body;
        Collider2D feet;
        RulerLook look;
        Camera view;
        CameraRig rig;
        Vector2 aim = Vector2.right;
        Vector2 wantedVelocity;
        float primaryReady, secondaryReady, explosiveReady, gatherReady, handOverReady;
        float noticeUntil;
        float hammerAt;   // when building, the next hammer blow heard
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
        /// <summary>Up on the walls (climbed a ladder or stairs).</summary>
        public bool OnWalls { get; private set; }
        /// <summary>The house or mine the ruler is inside, or null.</summary>
        public Place Inside { get; private set; }
        /// <summary>Furnishing a house (standing still while the build bar is open).</summary>
        public bool Busy { get; private set; }
        /// <summary>Typing in the chat: stands still.</summary>
        public bool Typing { get; set; }
        /// <summary>Riding to a friend's kingdom or home (online): stands still meanwhile.</summary>
        public bool Riding { get; set; }
        /// <summary>The kingdom's weapons: the one in hand is fired (null: the weapons given in Setup).</summary>
        public Combat.Armory Armory { get; set; }
        /// <summary>Opens the weapons screen (E at an anvil).</summary>
        public System.Action OpenArmory { get; set; }
        /// <summary>Opens the gear screen (E at a weapon rack or armor stand in an armory room).</summary>
        public System.Action OpenGear { get; set; }
        /// <summary>Opens the crafting screen (E at a tool bench in a crafting room).</summary>
        public System.Action OpenCrafting { get; set; }
        /// <summary>Opens a factory machine's screen (E at an arm, furnace or assembler).</summary>
        public System.Action<Machine> OpenFactory { get; set; }
        /// <summary>Opens a storehouse or chest to put things in or take them out (E at one).</summary>
        public System.Action<Storage> OpenStorage { get; set; }
        /// <summary>The weapon in hand, for the HUD.</summary>
        public string WeaponName => Armory != null ? (Armory.Selected != null ? Armory.Selected.displayName : "Bare hands") : "Rifle";
        /// <summary>Which way the ruler aims (a direction).</summary>
        public Vector2 AimDirection => aim;
        public Vector2 CurrentVelocity => Velocity;
        Vector2 cameOutAt;   // where to come out if the house goes while the ruler is inside

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
            feet = GetComponent<Collider2D>();
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
            WearArmor();
            Heal();
            // The wall under them caved in (or was torn down): down they go.
            if (OnWalls && Kingdom.Current != null && !Kingdom.Current.WallTopAt(NavCell(transform.position)))
            {
                GetDown(transform.position);
                Health.Damage(FallDamage, transform.position);
                Say("The wall gave way under you!");
                if (Dead) return;
            }
            // The house or mine they were in was torn down or broken: out they come.
            if ((object)Inside != null && Inside == null)
            {
                LeaveHouse();
                Say("The place you were in is gone!");
            }
            // The room grows when the house is upgraded: the camera may go further.
            if (Inside != null && rig != null && !Busy) rig.Inside = RoomRect(Inside);
            if (Seated || Busy)
            {
                if (look != null) look.Show(Vector2.zero, Vector2.down, config.moveSpeed, config.stepsPerSecond, holdingGun: false);
                return;
            }
            if (Typing || Riding)
            {
                wantedVelocity = Vector2.zero;
                if (look != null) look.Show(Velocity, aim, config.moveSpeed, config.stepsPerSecond);
                return;
            }

            // Deep snow slows you down (on the map; not inside a house or a mine).
            float snow = Inside == null && World.Seasons.Current != null ? World.Seasons.Current.MoveFactor : 1f;
            wantedVelocity = GameInput.Move * config.moveSpeed * snow;
            if (view != null)
            {
                Vector2 mouse = view.ScreenToWorldPoint(GameInput.PointerPosition);
                var toMouse = mouse - (Vector2)transform.position;
                if (toMouse.sqrMagnitude > 0.04f) aim = toMouse.normalized;
            }
            if (look != null) look.Show(Velocity, aim, config.moveSpeed, config.stepsPerSecond);
            if (UiFocus.PanelOpen)
            {
                Hint = null;
                return;
            }

            float now = Time.time;
            // The weapons: from the armory (the one in hand, the heavy one, the explosive), or those given in Setup.
            SwitchWeapons();
            var primary = Armory != null ? Armory.Selected?.weapon : primaryWeapon;
            var secondary = Armory != null ? Armory.Heavy : secondaryWeapon;
            var thrown = Armory != null ? Armory.Explosive : explosive;
            if (primary != null && (primary.automatic ? GameInput.FireHeld : GameInput.FirePressed) && now >= primaryReady)
            {
                if (primary.melee) Swing(primary);
                else Shoot(primary);
                primaryReady = now + 1f / primary.fireRate;
            }
            if (secondary != null && GameInput.AltFirePressed && now >= secondaryReady)
            {
                Shoot(secondary);
                secondaryReady = now + 1f / secondary.fireRate;
            }
            if (thrown != null && explosives != null && GameInput.Pressed(KeyCode.G) && now >= explosiveReady && view != null)
            {
                Vector2 target = view.ScreenToWorldPoint(GameInput.PointerPosition);
                explosives.Throw(thrown, Muzzle, target, OnWalls ? OverWallsFromTop * 0.6f : 0f);
                Audio.Sounds.Play(Audio.Sound.Throw, Muzzle);
                explosiveReady = now + thrown.cooldown;
            }

            UseBandage(now);
            PickUpLoot();
            if (Inside != null && Kingdom.Current != null && InsideKeys(Kingdom.Current)) return;
            Interact(now);
            if (now > noticeUntil) Notice = null;
        }

        void FixedUpdate()
        {
            if (config == null || Seated || Busy || Dead) return;
            Velocity = Vector2.MoveTowards(Velocity, wantedVelocity, config.acceleration * Time.fixedDeltaTime);
            if (OnWalls) StayOnWalls();
            else if (Inside is MineLevel) StayOnFloor();
            else KeepOutOfZombies();
        }

        // ------------------------------------------------------------ walls --

        static Vector2Int NavCell(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));

        // Up on the walls the body passes over them (its collider is off), so it's kept on top here:
        // no step that would take any part of it off the walls and gates.
        void StayOnWalls()
        {
            var kingdom = Kingdom.Current;
            if (kingdom == null) return;
            float dt = Time.fixedDeltaTime;
            var p = body.position;
            var v = Velocity;
            if (!OnTop(kingdom, p + new Vector2(v.x * dt, 0f))) v.x = 0f;
            if (!OnTop(kingdom, p + new Vector2(v.x * dt, v.y * dt))) v.y = 0f;
            Velocity = v;
        }

        // In a mine the rock has no colliders: no step that would take any part of the body into rock.
        void StayOnFloor()
        {
            var nav = Inside.Nav;
            if (nav == null) return;
            float dt = Time.fixedDeltaTime;
            var p = body.position;
            var v = Velocity;
            if (!OnFloor(nav, p + new Vector2(v.x * dt, 0f))) v.x = 0f;
            if (!OnFloor(nav, p + new Vector2(v.x * dt, v.y * dt))) v.y = 0f;
            Velocity = v;
        }

        bool OnFloor(NavGrid nav, Vector2 p)
        {
            float r = config.bodyRadius * 0.9f;
            return nav.Walkable(NavCell(p + new Vector2(-r, -r))) && nav.Walkable(NavCell(p + new Vector2(r, -r))) &&
                   nav.Walkable(NavCell(p + new Vector2(-r, r))) && nav.Walkable(NavCell(p + new Vector2(r, r)));
        }

        bool OnTop(Kingdom kingdom, Vector2 p)
        {
            float r = config.bodyRadius * 0.9f;
            return kingdom.WallTopAt(NavCell(p + new Vector2(-r, -r))) && kingdom.WallTopAt(NavCell(p + new Vector2(r, -r))) &&
                   kingdom.WallTopAt(NavCell(p + new Vector2(-r, r))) && kingdom.WallTopAt(NavCell(p + new Vector2(r, r)));
        }

        void GetUp(Vector2 at)
        {
            OnWalls = true;
            if (feet != null) feet.enabled = false;
            Health.OutOfReach = true;
            MoveTo(at);
            if (look != null) look.SetRaised(true);
        }

        void GetDown(Vector2 at)
        {
            OnWalls = false;
            if (feet != null) feet.enabled = true;
            Health.OutOfReach = false;
            MoveTo(at);
            if (look != null) look.SetRaised(false);
        }

        void MoveTo(Vector2 at)
        {
            body.position = at;
            transform.position = at;
            Velocity = Vector2.zero;
            stoodUpFrame = Time.frameCount;   // the E that did it mustn't do something else too
        }

        // On the walls, E climbs down a ladder here or jumps down (towards where you aim).
        void InteractOnWalls(Kingdom kingdom)
        {
            var here = NavCell(transform.position);
            Furniture ladder = null;
            foreach (var f in kingdom.AllFurniture)
                if (f != null && f.Config.climb && Kingdom.LeansOn(f.Cells, f.Rotation) == here) { ladder = f; break; }
            Hint = ladder != null ? $"E: climb down the {ladder.Config.displayName.ToLowerInvariant()}" : "E: jump down (it hurts a little)";
            if (!GameInput.Pressed(KeyCode.E)) return;
            if (ladder != null)
            {
                GetDown(ladder.Cells.center);
                return;
            }
            // The free ground next to this bit of wall that's most the way you aim.
            Vector2Int best = here;
            float bestScore = float.MinValue;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var cell = new Vector2Int(here.x + dx, here.y + dy);
                    if ((dx == 0 && dy == 0) || kingdom.WallTopAt(cell) || !kingdom.Nav.Walkable(cell)) continue;
                    float score = Vector2.Dot(new Vector2(dx, dy).normalized, aim) - (dx != 0 && dy != 0 ? 0.3f : 0f);
                    if (score > bestScore) { bestScore = score; best = cell; }
                }
            if (best == here)
            {
                Say("Nowhere to jump down here");
                return;
            }
            GetDown(new Vector2(best.x + 0.5f, best.y + 0.5f));
            Health.Damage(JumpDamage, transform.position);
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
        float bandageReady;

        // An arm, furnace or assembler (its screen opens with E).
        static bool IsWorks(Furniture f)
        {
            var m = f.Config.machine;
            return m != null && (m.kind == MachineKind.Arm || m.kind == MachineKind.Furnace || m.kind == MachineKind.Assembler);
        }

        // C: a bandage from the crafting room's supplies heals you (not at full health).
        void UseBandage(float now)
        {
            if (!GameInput.Pressed(KeyCode.C) || now < bandageReady) return;
            var supplies = Kingdom.Current != null ? Kingdom.Current.Crafting : null;
            if (supplies == null || supplies.BandageHeal <= 0f) return;
            if (Health.Current >= Health.Max) { Say("You're not hurt"); return; }
            if (!supplies.UseBandage()) { Say("No bandages: order them at a crafting room's tool bench"); return; }
            Health.Heal(supplies.BandageHeal);
            bandageReady = now + 1f;
            Say($"Bandaged ({supplies.Bandages} left)");
        }

        Combat.ArmorItem wornArmor;
        bool armorWorn;

        // The armor bought at an armory room: it takes some off every hit and adds health (the share of health is kept).
        void WearArmor()
        {
            if (Health == null || config == null || Health.Dead) return;
            var suit = Armory != null ? Armory.RulerArmor : null;
            if (armorWorn && suit == wornArmor) return;
            wornArmor = suit;
            armorWorn = true;
            Health.Armor = suit != null ? suit.armor : 0f;
            float max = config.maxHealth + (suit != null ? suit.health : 0f);
            if (Mathf.Approximately(max, Health.Max)) return;
            float share = Health.Fraction;
            Health.SetMax(max);
            Health.SetCurrent(share * max);
        }

        void Heal()
        {
            if (Health == null || Health.Current >= Health.Max || Time.time - Health.LastHurt < config.healDelay) return;
            Health.Heal(config.healPerSecond * Time.deltaTime);
        }

        void OnHurt(Health h, float amount, Vector2 from)
        {
            if (amount > 0f) Audio.Sounds.PlayHere(Audio.Sound.Hurt);
            // Attacked on the throne: stand up and fight.
            if (Seated && !h.Dead)
            {
                var builder = Kingdom.Current != null ? Kingdom.Current.GetComponent<BuildController>() : null;
                if (builder != null) builder.LeaveAll();
                Say("You're under attack!");
            }
        }

        void OnDied(Health h)
        {
            if (Seated || Busy)
            {
                var builder = Kingdom.Current != null ? Kingdom.Current.GetComponent<BuildController>() : null;
                if (builder != null) builder.LeaveAll();
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
            if ((object)Inside != null)
            {
                Inside = null;
                if (rig != null)
                {
                    rig.Inside = default;
                    rig.Follow(transform, true);   // straight back to the map, no glide across it
                }
            }
            body.simulated = true;
            GetDown(at);
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
                if (bag.TakeInto(Backpack) > 0)
                {
                    Say($"Picked up {what}");
                    Audio.Sounds.PlayHere(Audio.Sound.Loot);
                }
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
            // From up on the walls, shots fly over the walls below.
            bool fired = false;
            for (int i = 0; i < Mathf.Max(1, weapon.pellets); i++)
                fired |= projectiles.Fire(weapon, Muzzle, aim, OnWalls ? Mathf.Max(weapon.overWalls, OverWallsFromTop) : -1f, -1f, weapon.pierce);
            if (fired && rig != null) rig.AddShake(weapon.recoilShake);
            if (fired) Audio.Sounds.Fired(weapon, Muzzle);
        }

        // A club: hits every zombie (and wild animal) in front within reach.
        void Swing(WeaponConfig weapon)
        {
            if (look != null) look.Swing();
            Vector2 here = transform.position;
            Audio.Sounds.Fired(weapon, here);
            float halfArc = weapon.meleeArc * 0.5f;
            var hit = new System.Collections.Generic.List<Health>();
            foreach (var h in Health.Creatures)
            {
                if (h == null || h.Dead || h.Team == Team.Friendly) continue;
                var to = h.Position - here;
                if (to.magnitude > weapon.range + h.Radius || Vector2.Angle(aim, to) > halfArc) continue;
                hit.Add(h);
            }
            foreach (var h in hit) h.Damage(weapon.hitDamage, here);
            if (hit.Count > 0 && rig != null) rig.AddShake(0.02f);
        }

        // Q takes the next weapon, 1-9 a weapon by its place in the list; the picture in hand follows.
        void SwitchWeapons()
        {
            if (Armory == null) return;
            if (GameInput.Pressed(KeyCode.Q)) Armory.SelectNext();
            for (int n = 0; n < 9; n++)
                if (GameInput.Pressed(KeyCode.Alpha1 + n)) Armory.Select(n);
            ShowWeapon();
        }

        Combat.ArmsItem shownWeapon;

        /// <summary>Puts the weapon in hand into the ruler's picture.</summary>
        public void ShowWeapon()
        {
            if (Armory == null || look == null) return;
            var hand = Armory.Selected;
            if (hand == shownWeapon) return;
            shownWeapon = hand;
            look.SetWeapon(hand != null ? ArtSprites.Get(Art.ArtKeys.Weapon(hand.art)) : null);
        }

        // ------------------------------------------------------- interacting --

        void Interact(float now)
        {
            if (Time.frameCount == stoodUpFrame) return;
            Vector2 here = transform.position;
            var kingdom = Kingdom.Current;
            float reach = kingdom != null ? kingdom.Config.reach : 0.9f;
            if (OnWalls)
            {
                if (kingdom != null) InteractOnWalls(kingdom);
                return;
            }

            // Whatever is closest wins.
            float best = float.MaxValue;
            Throne throne = null;
            Storage storage = null;
            Blueprint blueprint = null;
            ResourceNode node = null;
            Furniture ladder = null, anvil = null, lever = null, rack = null, toolBench = null, works = null;
            Place door = null;
            if (kingdom != null)
            {
                // A house's door or a mine's shaft: in from outside, out from inside (the door wins over anything else in reach).
                if (Inside != null)
                {
                    if (BuildingLook.Distance(new RectInt(Inside.ExitCell, Vector2Int.one), here) <= reach) door = Inside;
                }
                else
                {
                    float nearest = reach;
                    foreach (var p in kingdom.Places)
                    {
                        if (p == null) continue;
                        float d = (p.DoorPoint - here).magnitude;
                        if (d <= nearest) { nearest = d; door = p; }
                    }
                }
                if (door != null)
                {
                    UseHouseDoor(door);
                    return;
                }
                // A fire right here: beat it out.
                var fires = Fires.Current;
                var blaze = fires != null ? fires.Nearest(here, reach + 0.2f) : null;
                if (blaze != null)
                {
                    Hint = $"Hold E: beat out the fire ({Mathf.RoundToInt(blaze.Heat * 100f)}%)";
                    if (GameInput.Held(KeyCode.E)) fires.Douse(blaze, fires.Config.rulerDousePerSecond * Time.deltaTime);
                    return;
                }
                if (kingdom.Throne != null && Closer(BuildingLook.Distance(kingdom.Throne.Cells, here), reach, ref best)) throne = kingdom.Throne;
                foreach (var f in kingdom.AllFurniture)
                {
                    if (f == null) continue;
                    if (f.Config.climb && Closer(BuildingLook.Distance(f.Cells, here), reach, ref best)) { ladder = f; anvil = null; throne = null; lever = null; }
                    else if (f.Config.lever && Closer(BuildingLook.Distance(f.Cells, here), reach, ref best)) { lever = f; ladder = null; anvil = null; throne = null; }
                    else if (f.Config.roomTag == RoomTag.Workshop && f.Config.machine == null && Armory != null && OpenArmory != null && Closer(BuildingLook.Distance(f.Cells, here), reach, ref best))
                    { anvil = f; ladder = null; throne = null; rack = null; toolBench = null; works = null; }
                    else if (f.Config.roomTag == RoomTag.Weapons && Armory != null && OpenGear != null && Closer(BuildingLook.Distance(f.Cells, here), reach, ref best))
                    { rack = f; anvil = null; ladder = null; throne = null; toolBench = null; works = null; }
                    else if (f.Config.roomTag == RoomTag.Crafting && kingdom.Crafting != null && OpenCrafting != null && Closer(BuildingLook.Distance(f.Cells, here), reach, ref best))
                    { toolBench = f; rack = null; anvil = null; ladder = null; throne = null; works = null; }
                    else if (IsWorks(f) && OpenFactory != null && Closer(BuildingLook.Distance(f.Cells, here), reach, ref best))
                    { works = f; toolBench = null; rack = null; anvil = null; ladder = null; throne = null; }
                }
                foreach (var s in kingdom.Storages)
                    if (Closer(BuildingLook.Distance(s.Cells, here), reach, ref best)) { storage = s; throne = null; ladder = null; anvil = null; rack = null; toolBench = null; works = null; }
                foreach (var b in kingdom.Blueprints)
                    if (b != null && !b.Cancelled && Closer(BuildingLook.Distance(b.Cells, here), reach, ref best)) { blueprint = b; storage = null; throne = null; ladder = null; anvil = null; rack = null; toolBench = null; works = null; }
            }
            // Trees, rocks and bushes in the cells round the ruler.
            var nature = NatureMap.Current;
            var at = NavCell(here);
            if (nature != null)
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        var n = nature.Live(at + new Vector2Int(dx, dy));
                        if (n == null || n.Empty || n.Config == null) continue;
                        if (Closer(n.DistanceFrom(here), n.Config.reach, ref best)) { node = n; blueprint = null; storage = null; throne = null; ladder = null; anvil = null; rack = null; toolBench = null; works = null; }
                    }

            if (lever != null)
            {
                Hint = "E: pull the lever (opens or shuts the gates near it)";
                if (GameInput.Pressed(KeyCode.E) && !UiFocus.TakesEscape) kingdom.PullLever(lever);
            }
            else if (ladder != null) UseLadder(kingdom, ladder);
            else if (anvil != null)
            {
                Hint = $"E: make weapons at the {anvil.Config.displayName.ToLowerInvariant()}";
                if (GameInput.Pressed(KeyCode.E) && !UiFocus.TakesEscape) OpenArmory();
            }
            else if (rack != null)
            {
                // Gear is handed out in an armory room only (a walled room with a door, Building/RoomTypes.cs).
                string name = rack.Config.displayName.ToLowerInvariant();
                var room = kingdom.RoomTypeAt(rack.Cells.position);
                if (room == RoomType.Armory)
                {
                    Hint = $"E: gear up at the {name} (armor for you, weapons and armor for your soldiers)";
                    if (GameInput.Pressed(KeyCode.E) && !UiFocus.TakesEscape) OpenGear();
                }
                else if (room == RoomType.None)
                    Hint = $"Gear up here once the {name} is in an armory: walls and a door all round it (I shows the kinds of room)";
                else
                    Hint = $"This room is a {RoomTypes.Name(room).ToLowerInvariant()}, not an armory: keep beds, tables with seats and the throne out of it (I shows the kinds of room)";
            }
            else if (works != null)
            {
                var kind = works.Config.machine.kind;
                Hint = kind == MachineKind.Assembler ? $"E: choose what the {works.Config.displayName.ToLowerInvariant()} makes"
                    : kind == MachineKind.Arm ? $"E: choose what the {works.Config.displayName.ToLowerInvariant()} moves"
                    : $"E: see what the {works.Config.displayName.ToLowerInvariant()} is doing";
                if (GameInput.Pressed(KeyCode.E) && !UiFocus.TakesEscape) OpenFactory(works.GetComponent<Machine>());
            }
            else if (toolBench != null)
            {
                // Tools and supplies are made in a crafting room only (a walled room with a door, Building/RoomTypes.cs).
                var room = kingdom.RoomTypeAt(toolBench.Cells.position);
                if (room == RoomType.Crafting)
                {
                    Hint = "E: crafting (better tools for your villagers, bandages, cures for bites)";
                    if (GameInput.Pressed(KeyCode.E) && !UiFocus.TakesEscape) OpenCrafting();
                }
                else if (room == RoomType.None)
                    Hint = "Craft here once the tool bench is in a crafting room: walls and a door all round it (I shows the kinds of room)";
                else
                    Hint = $"This room is a {RoomTypes.Name(room).ToLowerInvariant()}, not a crafting room: keep beds, tables with seats, weapon racks and bookshelves out of it (I)";
            }
            else if (throne != null) UseThrone(kingdom);
            else if (storage != null) UseStorage(storage);
            else if (blueprint != null) UseBlueprint(blueprint, now);
            else if (node != null) Gather(node, now);
            else Hint = Inside != null && kingdom != null ? InsideHint(kingdom) : null;
        }

        // ------------------------------------------------------------ houses --

        void UseHouseDoor(Place place)
        {
            if (Inside == place)
            {
                Hint = place is Interior ? "E: go outside" : "E: climb back up";
                if (GameInput.Pressed(KeyCode.E)) LeaveHouse();
                return;
            }
            string upgrading = place is Interior room && room.House.Upgrade != null ? " (being upgraded)" : "";
            Hint = place is Interior ? $"E: go into the {place.Name.ToLowerInvariant()}{upgrading}" : $"E: climb down into the {place.Name.ToLowerInvariant()}";
            if (GameInput.Pressed(KeyCode.E)) EnterHouse(place);
        }

        /// <summary>Goes in through a house's door or down a mine: the view moves inside.</summary>
        public void EnterHouse(Place place)
        {
            if (place == null) return;
            cameOutAt = place.Outside;
            Inside = place;
            MoveTo(place.Entrance);
            if (rig != null)
            {
                rig.Inside = RoomRect(place);
                rig.Follow(transform, true);
            }
        }

        static Rect RoomRect(Place inside)
        {
            var b = inside.Bounds;
            return new Rect(b.xMin, b.yMin, b.width, b.height);
        }

        /// <summary>
        /// Puts the ruler back where a saved game had them: at <paramref name="at"/>, inside
        /// <paramref name="place"/> (or on the map if null), up on the walls if <paramref name="onWalls"/>
        /// and there's still wall there, with this much health and this in the backpack.
        /// </summary>
        public void Restore(Vector2 at, Place place, bool onWalls, float health, int[] backpack)
        {
            if (rig == null && Camera.main != null) rig = Camera.main.GetComponent<CameraRig>();
            if (place != null)
            {
                EnterHouse(place);
                MoveTo(place.Bounds.Contains(NavCell(at)) ? at : place.Entrance);
            }
            else
            {
                var kingdom = Kingdom.Current;
                if (onWalls && kingdom != null && config != null && OnTop(kingdom, at)) GetUp(at);
                else MoveTo(at);
                if (rig != null) rig.Follow(transform, true);
            }
            WearArmor();   // (its health first, so a saved game's health isn't cut down)
            if (Health != null) Health.SetCurrent(health);
            if (Backpack != null && backpack != null)
                for (int i = 0; i < backpack.Length && i < Inventory.TypeCount; i++)
                    if (backpack[i] > 0) Backpack.Add((ResourceType)i, backpack[i]);
        }

        /// <summary>Comes out of the house's door onto the map.</summary>
        public void LeaveHouse()
        {
            var at = Inside != null ? Inside.Outside : cameOutAt;
            Inside = null;
            MoveTo(at);
            if (rig != null)
            {
                rig.Inside = default;
                rig.Follow(transform, true);
            }
        }

        // Inside a house: B furnishes it, U upgrades it. True if one was pressed.
        bool InsideKeys(Kingdom kingdom)
        {
            if (Time.frameCount == stoodUpFrame) return false;
            if (GameInput.Pressed(KeyCode.B))
            {
                var builder = kingdom.GetComponent<BuildController>();
                if (builder != null) builder.Furnish(Inside, false);
                return true;
            }
            if (GameInput.Pressed(KeyCode.U) && Inside is Interior room)
            {
                Say(kingdom.StartUpgrade(room.House));
                return true;
            }
            return false;
        }

        string InsideHint(Kingdom kingdom)
        {
            if (!(Inside is Interior room)) return $"B: dig and build in the {Inside.Name.ToLowerInvariant()}";
            var house = room.House;
            var next = house.Next;
            string upgrade = house.Upgrade != null ? BuildController.Describe(house.Upgrade)
                : next == null ? "it's as good as it gets"
                : kingdom.UpgradeLockedReason(house) ?? $"U: upgrade to a {next.displayName.ToLowerInvariant()} ({next.inside.x} x {next.inside.y} inside)";
            return $"B: furnish the {house.Name.ToLowerInvariant()}          {upgrade}";
        }

        /// <summary>Stands still while the build bar is open inside a house (or walks again).</summary>
        public void SetBusy(bool busy)
        {
            Busy = busy;
            wantedVelocity = Vector2.zero;
            Velocity = Vector2.zero;
            if (busy) Hint = null;
            else stoodUpFrame = Time.frameCount;   // the key that closed the build bar mustn't do anything else
        }

        static bool Closer(float distance, float reach, ref float best)
        {
            if (distance > reach || distance >= best) return false;
            best = distance;
            return true;
        }

        void UseLadder(Kingdom kingdom, Furniture ladder)
        {
            var top = Kingdom.LeansOn(ladder.Cells, ladder.Rotation);
            if (!kingdom.WallTopAt(top))
            {
                Hint = "The wall this leaned on is gone";
                return;
            }
            Hint = $"E: climb the {ladder.Config.displayName.ToLowerInvariant()} onto the wall";
            if (GameInput.Pressed(KeyCode.E)) GetUp(new Vector2(top.x + 0.5f, top.y + 0.5f));
        }

        void UseThrone(Kingdom kingdom)
        {
            var builder = kingdom.GetComponent<BuildController>();
            if (builder == null)
            {
                Hint = "Your throne";
                return;
            }
            Hint = "E: sit on the throne (build your kingdom)";
            if (!GameInput.Pressed(KeyCode.E)) return;
            builder.Enter();
        }

        /// <summary>Puts the ruler straight at <paramref name="at"/> (the server didn't believe a move).</summary>
        public void ForceMove(Vector2 at)
        {
            if (OnWalls) GetDown(at);
            else MoveTo(at);
        }

        void UseStorage(Storage storage)
        {
            if (OpenStorage != null)
            {
                // The storage screen: put the backpack in, or take things out.
                Hint = $"E: open the {(storage.Config != null ? storage.Config.displayName.ToLowerInvariant() : "storehouse")} (put in or take out)";
                if (GameInput.Pressed(KeyCode.E) && !UiFocus.TakesEscape) OpenStorage(storage);
                return;
            }
            Hint = Backpack.Total > 0
                ? "E: put your backpack in the storehouse"
                : $"Storehouse: {Economy.ResourceText.Describe(storage.Stock)}";
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
                if (Time.time >= hammerAt)
                {
                    hammerAt = Time.time + Random.Range(0.45f, 0.7f);
                    Audio.Sounds.Play(Audio.Sound.Hammer, transform.position);
                }
            }
        }

        void Gather(ResourceNode node, float now)
        {
            Hint = $"Hold E to gather {ResourceText.Name(node.Config.resource)}";
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
