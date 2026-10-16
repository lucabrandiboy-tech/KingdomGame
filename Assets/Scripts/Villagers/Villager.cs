using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Economy;
using KingdomGame.Zombies;
using UnityEngine;

namespace KingdomGame.Villagers
{
    /// <summary>The pictures every villager shares: [direction, frame], drawn lit from the top-left for each direction.</summary>
    public sealed class VillagerSprites
    {
        public Sprite[,] Walking;
        public Sprite[,] Carrying;
        public Sprite Shadow;
        /// <summary>Lying dead (zombies got them), one picture per direction (<see cref="Art.CharacterArt.DeadDirections"/>).</summary>
        public Sprite[] Dead;
    }

    /// <summary>
    /// A villager with a <see cref="Job"/>. When idle it asks the <see cref="Kingdom"/> for
    /// work: builders fetch resources from a storehouse and carry them to a blueprint, or
    /// build a blueprint that has everything it needs; woodcutters, miners and foragers
    /// gather from trees, rocks and bushes and carry what they get to storage. It walks along paths from the
    /// <see cref="NavGrid"/> and finds a new way if a wall goes up in front of it.
    ///
    /// Villagers have no physics body: they never get stuck on corners, and the
    /// ruler can walk through them. To build inside a house they walk to its door
    /// and step in (and out again the same way). When they see a zombie close by they drop
    /// their work and run (to the throne if that's away from it), and wait until
    /// it's safe. Zombies can kill them.
    ///
    /// They get hungry and tired (<see cref="NeedsMath"/>): they eat at a storehouse (a meal of food) and sleep at
    /// night, in a bed if there's one free (healing too), otherwise on the ground by the throne. Guards sleep by day.
    /// Hungry or tired villagers work and walk slower; fed and rested ones with a dining hall a little faster.
    /// </summary>
    public sealed class Villager : MonoBehaviour
    {
        enum Task { Idle, Fetch, Deliver, Build, Return, MakeRoom, Flee, Gather, Guard, Dig, Craft, Shelter, Eat, Sleep, Water, Douse, Hunt, PickUp }

        /// <summary>Seconds a villager stays scared after last seeing a zombie.</summary>
        const float ScaredSeconds = 4f;

        VillagerConfig config;
        Kingdom kingdom;
        NavGrid mapNav;
        Place space;             // the house or mine it's inside, or null on the map
        Place portal;            // the door it's walking to, to go in or out of
        // Where it finds its way: inside the house it's in, or on the map.
        NavGrid nav => space != null && space.Nav != null ? space.Nav : mapNav;
        VillagerSprites sprites;
        Transform body;
        SpriteRenderer bodyRenderer, shadow;

        readonly List<Vector2> path = new List<Vector2>();
        int pathIndex;
        RectInt goal;            // where the current path leads
        bool goalBeside;

        Task task;
        Blueprint blueprint, countedOn;
        Storage storage;
        ResourceNode node;       // what it's gathering from
        Defenses.Defense post;   // the tower it guards
        MineLevel mine;          // the mine it's digging in
        Furniture bench;         // the anvil it's making weapons at (or the crafting table it's making parts at)
        Machine benchMachine;    // the crafting table's machine (null at an anvil)
        Furniture shelter;       // the shelter flag it's going to or waiting at
        Vector2Int digCell;
        float digClock;
        bool onPost;             // up on it
        float gatherClock;
        float hammerAt;          // when building, the next hammer blow heard
        Job job = Job.Builder;
        ResourceType carryType;
        int carryAmount;         // in hand
        int promised;            // reserved at the storehouse / on its way to the blueprint
        float thinkAt;
        float stepClock;
        float scaredUntil, lookAt;
        Vector2 facing = Vector2.down;
        // Needs: 1 well fed / rested, 0 starving / exhausted.
        float fed = 1f, rested = 1f;
        float needsAt, eatClock, mealShare, sleepAngle, noMealUntil;
        int meal;                // food reserved at the storehouse for its meal
        Furniture bed;           // the bed it's going to or asleep in (null: the ground)
        bool asleep, inBed, napping;
        Vector2 bedside;         // where it got into bed (it gets out there)
        // Fighting a fire: the well it fetches water from, the fire, and whether its bucket is full.
        Furniture well;
        Fires.Blaze fire;
        bool bucket;
        float fillClock;
        // Hunting (hunters): the animal it's after, when it can shoot next, find its way again, and give up; a bag of meat it's fetching.
        World.Animal quarry;
        float shotAt, repathAt, huntUntil;
        LootBag pickup;
        float turnAt = -1f;      // bitten: when it turns into a zombie (-1: it isn't)

        public bool IsIdle => task == Task.Idle;
        /// <summary>Its name (survivors who joined have one; the first villagers don't).</summary>
        public string Name { get; set; }
        /// <summary>Bitten (a survivor who hid it): seconds until it turns into a zombie, or -1.</summary>
        public float TurnsIn => turnAt >= 0f ? Mathf.Max(0f, turnAt - Time.time) : -1f;

        /// <summary>It was bitten: in <paramref name="seconds"/> it turns into a zombie (pale and slow for the last half minute).</summary>
        public void Infect(float seconds) => turnAt = Time.time + Mathf.Max(1f, seconds);
        /// <summary>Its job (the villagers panel changes it; it drops what it's doing, but keeps what it carries).</summary>
        public Job Job
        {
            get => job;
            set
            {
                if (job == value) return;
                job = value;
                Abandon();
            }
        }
        /// <summary>Hit points (zombies).</summary>
        public Health Health { get; private set; }
        /// <summary>Running from or hiding from zombies.</summary>
        public bool Scared => Time.time < scaredUntil;
        /// <summary>On the move (it will be gone from where it is in a moment).</summary>
        public bool IsWalking => task != Task.Idle && pathIndex < path.Count;
        public Vector2 Position => transform.position;

        /// <summary>What it's doing, for the throne view.</summary>
        public string Doing
        {
            get
            {
                switch (task)
                {
                    case Task.Fetch: return $"fetching {promised} {ResourceText.Name(carryType)}";
                    case Task.Deliver: return $"carrying {carryAmount} {ResourceText.Name(carryType)}";
                    case Task.Build: return "building";
                    case Task.Return: return "taking things back to storage";
                    case Task.MakeRoom: return "making room";
                    case Task.Flee: return "running from zombies";
                    case Task.Dig: return "digging";
                    case Task.Craft: return benchMachine != null ? $"making {ResourceText.Name(benchMachine.Config.makes)}" : "making weapons";
                    case Task.Shelter: return "sheltering from the zombies";
                    case Task.Eat: return "eating";
                    case Task.Water: return "fetching water for a fire";
                    case Task.Douse: return "putting out a fire";
                    case Task.Hunt: return quarry != null && quarry.IsWolf ? "fighting a wolf" : "hunting a deer";
                    case Task.PickUp: return "fetching meat";
                    case Task.Sleep: return asleep ? (inBed ? "asleep in bed" : napping ? "napping on the ground" : "asleep on the ground") : "going to bed";
                    case Task.Guard: return onPost ? $"guarding the {post?.Name.ToLowerInvariant()}" : "going to a tower";
                    case Task.Gather: return node != null ? $"gathering {ResourceText.Name(node.Config.resource)}" : "gathering";
                    default: return Scared ? "hiding" : "idle";
                }
            }
        }

        public void Setup(VillagerConfig villagerConfig, Kingdom owner, NavGrid grid, VillagerSprites villagerSprites)
        {
            config = villagerConfig;
            kingdom = owner;
            mapNav = grid;
            sprites = villagerSprites;

            shadow = Look.AddShadow(transform, sprites.Shadow, 0.1f);
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            bodyRenderer = body.gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = sprites.Walking[0, 0];
            bodyRenderer.sortingOrder = SortOrder.Characters;
            thinkAt = Time.time + Random.Range(0f, config.thinkSeconds);

            Health = gameObject.AddComponent<Health>();
            Health.Setup(Team.Friendly, config.maxHealth, 0.18f, true);
            // Hurt: it runs (guards stay on their towers, and a hunter keeps shooting what it's after).
            Health.Hurt += (h, amount, from) => { if (!h.Dead && job != Job.Guard && task != Task.Hunt) Flee(from); };
            Health.Died += OnDied;
        }

        // ------------------------------------------------------------ orders --

        /// <summary>Go to the storehouse, pick up <paramref name="amount"/> (already reserved there) and bring it to the blueprint.</summary>
        public bool StartFetch(Storage from, Blueprint to, ResourceType type, int amount)
        {
            storage = from;
            blueprint = to;
            carryType = type;
            promised = amount;
            task = Task.Fetch;
            if (Walk(from.Cells, true)) return true;
            CancelPromise();
            to.UnreachableUntil = Time.time + 3f;
            Idle();
            return false;
        }

        /// <summary>Go to a tree, rock or bush and gather from it until its hands are full (or it's used up).</summary>
        public bool StartGather(ResourceNode target)
        {
            if (target == null || target.Empty) return false;
            node = target;
            node.Gatherer = this;
            gatherClock = 0f;
            task = Task.Gather;
            if (Walk(new RectInt(target.Cell, Vector2Int.one), true)) return true;
            Idle();
            return false;
        }

        /// <summary>Go down a mine and dig out a marked tile of rock; what comes out is carried to storage.</summary>
        public bool StartDig(MineLevel level, Vector2Int cell)
        {
            mine = level;
            digCell = cell;
            digClock = 0f;
            level.Claim(cell, this);
            task = Task.Dig;
            if (Walk(new RectInt(cell, Vector2Int.one), true)) return true;
            Idle();
            return false;
        }

        /// <summary>Zombies are about: go to the shelter flag and wait there till they're gone.</summary>
        public bool StartShelter(Furniture flag)
        {
            if (flag == null) return false;
            shelter = flag;
            task = Task.Shelter;
            if (Walk(flag.Cells, true)) return true;
            Idle();
            return false;
        }

        /// <summary>How fed it is: 1 well fed, 0 starving.</summary>
        public float Fed => fed;
        /// <summary>How rested it is: 1 rested, 0 exhausted.</summary>
        public float Rested => rested;
        /// <summary>Asleep (in a bed: out of sight in it; otherwise lying on the ground).</summary>
        public bool Asleep => asleep;
        public bool InBed => asleep && inBed;

        /// <summary>How fast it works and walks now (hunger, tiredness, a dining hall, the cold in winter).</summary>
        public float Pace => (config != null && config.needs
            ? NeedsMath.WorkFactor(fed, rested, kingdom.HasDiningHall, config.tiredSpeed, config.exhaustedSpeed, config.contentBonus)
            : 1f) * kingdom.ColdFactor;

        /// <summary>Puts back how fed and rested it was (a saved game).</summary>
        public void SetNeeds(float fedLevel, float restedLevel)
        {
            fed = Mathf.Clamp01(fedLevel);
            rested = Mathf.Clamp01(restedLevel);
        }

        /// <summary>Whether it should go to sleep now (<paramref name="nap"/>: outside its usual time, it's exhausted).</summary>
        public bool WantsSleep(out bool nap)
        {
            nap = false;
            if (config == null || !config.needs) return false;
            var clock = kingdom.Clock;
            float hour = clock != null ? clock.Hour : 12f;
            bool night = clock != null && clock.IsNight;
            bool guard = job == Job.Guard;
            if (!NeedsMath.ShouldSleep(hour, night, rested, guard)) return false;
            nap = !(guard ? NeedsMath.GuardSleepTime(hour) : night);
            return true;
        }

        // A fire in the kingdom wanting hands, and a well to fetch water from.
        bool FireToFight()
        {
            var fires = Fires.Current;
            return fires != null && fires.Count > 0 && kingdom.HasWell && fires.NeedingHands(Position) != null;
        }

        /// <summary>Go to a storehouse and eat a meal there (<paramref name="amount"/> food, already reserved there).</summary>
        public bool StartEat(Storage from, int amount)
        {
            storage = from;
            meal = amount;
            eatClock = 0f;
            task = Task.Eat;
            if (Walk(from.Cells, true)) return true;
            Idle();   // (gives the meal back to the storehouse)
            noMealUntil = Time.time + 30f;   // no way to the food: don't keep dropping work for it
            return false;
        }

        /// <summary>
        /// Go to sleep: in <paramref name="toBed"/> if it's given (its place in it is taken now), otherwise on the ground at
        /// <paramref name="ground"/>, or where it is if there's no way there. <paramref name="nap"/>: just until rested enough.
        /// </summary>
        public void StartSleep(Furniture toBed, Vector2 ground, bool nap)
        {
            Abandon();
            task = Task.Sleep;
            napping = nap;
            if (toBed != null)
            {
                bed = toBed;
                kingdom.ClaimBed(bed);
                if (Walk(bed.Cells, true)) return;
                kingdom.ReleaseBed(bed);
                bed = null;
            }
            if (Walk(new RectInt(NavGrid.CellAt(ground), Vector2Int.one), false)) return;
            // No way there: sleeps where it is.
            path.Clear();
            pathIndex = 0;
            portal = null;
        }

        // At the bed (or the spot on the ground): into it, out of sight, or lying down.
        void LieDown()
        {
            asleep = true;
            sleepAngle = Random.Range(0f, 360f);
            if (bed != null)
            {
                inBed = true;
                bedside = Position;
                transform.position = bed.Cells.center;
            }
            ShowAsleep();
        }

        // Out of bed (woken, or the bed went): back on its feet where it got in.
        void WakeUp()
        {
            if (!asleep) return;
            if (inBed) transform.position = bedside;
            asleep = false;
            inBed = false;
            ShowAsleep();
        }

        void ShowAsleep()
        {
            if (bodyRenderer != null) bodyRenderer.enabled = !inBed;
            if (shadow != null) shadow.enabled = !asleep && !onPost;
            if (asleep && !inBed && sprites.Dead != null && sprites.Dead.Length > 0)
            {
                // Lying on its back on the ground.
                bodyRenderer.sprite = sprites.Dead[Character.RulerLook.Direction(sleepAngle, sprites.Dead.Length)];
                body.localRotation = Quaternion.Euler(0f, 0f, sleepAngle);
            }
        }

        // Hunger and tiredness over time; once a second: whether it should drop what it's doing to eat or sleep,
        // or (asleep) wake up.
        void Needs()
        {
            float hours = Time.deltaTime * kingdom.GameHoursPerSecond;
            fed = NeedsMath.Hunger(fed, hours, config.hungerHours);
            rested = NeedsMath.Rest(rested, hours, asleep, inBed, config.awakeHours, config.bedSleepHours, config.groundSleepHours, config.groundRestCap);
            if (asleep && inBed) Health.Heal(config.healPerHourInBed * hours);
            if (Time.time < needsAt) return;
            needsAt = Time.time + 1f;

            var clock = kingdom.Clock;
            float hour = clock != null ? clock.Hour : 12f;
            bool night = clock != null && clock.IsNight;
            bool guard = job == Job.Guard;
            if (task == Task.Sleep)
            {
                if (!asleep) return;   // still on the way
                // Zombies about: guards get up for their towers. A fire nearby and a well to fight it with: everyone up.
                if ((guard && kingdom.ShelterNow) || !NeedsMath.StayAsleep(hour, night, rested, guard, napping) || (!guard && FireToFight()))
                {
                    Idle();
                    thinkAt = Time.time + Random.Range(0f, 2f);   // not everyone at once
                }
                return;
            }
            if (task == Task.Idle || task == Task.Eat || task == Task.Flee || task == Task.Shelter || task == Task.MakeRoom || task == Task.Water || task == Task.Douse || Scared) return;
            if (guard && kingdom.ShelterNow) return;   // keeps watch while zombies are about
            bool sleepy = NeedsMath.ShouldSleep(hour, night, rested, guard);
            bool starving = fed < NeedsMath.Urgent && Time.time >= noMealUntil && kingdom.Available(ResourceType.Food) > 0;
            if (sleepy || starving)
            {
                Abandon();
                thinkAt = Time.time;
            }
        }

        /// <summary>Fetch a bucket of water from <paramref name="fromWell"/> and throw it on <paramref name="blaze"/>.</summary>
        public bool StartFirefight(Furniture fromWell, Fires.Blaze blaze)
        {
            if (fromWell == null || blaze == null || blaze.Out) return false;
            well = fromWell;
            fire = blaze;
            fire.Fighters++;
            bucket = false;
            fillClock = 0f;
            task = Task.Water;
            if (Walk(well.Cells, true)) return true;
            Idle();
            return false;
        }

        /// <summary>Go to an anvil and make the weapons on order there (crafters).</summary>
        public bool StartCraft(Furniture anvil)
        {
            if (anvil == null) return false;
            bench = anvil;
            benchMachine = anvil.Config.machine != null && anvil.Config.machine.kind == MachineKind.Workbench ? anvil.GetComponent<Machine>() : null;
            task = Task.Craft;
            if (Walk(anvil.Cells, true)) return true;
            Idle();
            return false;
        }

        /// <summary>Go to a tower and stand on it (guards): it shoots while they're up there.</summary>
        public bool StartGuard(Defenses.Defense tower)
        {
            if (tower == null) return false;
            post = tower;
            tower.Guard = this;
            onPost = false;
            task = Task.Guard;
            if (Walk(tower.Cells, true)) return true;
            Idle();
            return false;
        }

        /// <summary>Whether it's up on <paramref name="tower"/>, working it.</summary>
        public bool OnPost(Defenses.Defense tower) => onPost && post == tower && Health != null && !Health.Dead;

        /// <summary>Turns to face a way (a guard aiming at a zombie).</summary>
        public void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude > 1e-6f) facing = direction.normalized;
        }

        // Climbs up onto its tower: zombies can't reach it there.
        void Mount()
        {
            onPost = true;
            transform.position = post.Center + (post.HasHead ? new Vector2(0f, -0.55f) : Vector2.zero);
            Health.OutOfReach = true;
            bodyRenderer.sortingOrder = SortOrder.Furniture + 3;
            if (shadow != null) shadow.enabled = false;
        }

        // Back down beside the tower (its tower went, or its job changed).
        void Dismount()
        {
            if (!onPost) return;
            onPost = false;
            Health.OutOfReach = false;
            bodyRenderer.sortingOrder = SortOrder.Characters;
            if (shadow != null) shadow.enabled = true;
            if (nav.NearestFreeSpot(Position, post != null ? post.Cells : new RectInt(NavGrid.CellAt(Position), Vector2Int.one), out var spot))
                transform.position = spot;
        }

        /// <summary>Carry what's in hand to a blueprint that needs it (already counted as on its way there).</summary>
        public bool StartDeliver(Blueprint to)
        {
            blueprint = to;
            promised = carryAmount;
            task = Task.Deliver;
            if (Walk(to.Cells, true)) return true;
            to.RemoveIncoming(carryType, promised);
            promised = 0;
            to.UnreachableUntil = Time.time + 3f;
            Idle();
            return false;
        }

        public bool StartBuild(Blueprint target)
        {
            blueprint = target;
            task = Task.Build;
            CountAsBuilder(target);
            if (Walk(target.Cells, true)) return true;
            target.UnreachableUntil = Time.time + 3f;
            Idle();
            return false;
        }

        /// <summary>Go after an animal with a bow (hunters): close enough to shoot, then shoot until it's dead (or it gets away).</summary>
        public bool StartHunt(World.Animal target)
        {
            if (target == null || target.Health == null || target.Health.Dead || config.huntWeapon == null) return false;
            quarry = target;
            quarry.Hunter = this;
            task = Task.Hunt;
            huntUntil = Time.time + config.huntSeconds;
            shotAt = Time.time + 0.4f;
            repathAt = Time.time + 1f;
            if (space == null && InShot()) return true;
            if (Walk(new RectInt(NavGrid.CellAt(target.Position), Vector2Int.one), true)) return true;
            target.UnreachableUntil = Time.time + 15f;
            Idle();
            return false;
        }

        /// <summary>Go and pick up the food in a bag on the ground (meat from a deer), to take to storage.</summary>
        public bool StartPickUp(LootBag bag)
        {
            if (bag == null) return false;
            pickup = bag;
            bag.Collector = this;
            task = Task.PickUp;
            if (Walk(new RectInt(NavGrid.CellAt(bag.Position), Vector2Int.one), true)) return true;
            Idle();
            return false;
        }

        public bool StartReturn(Storage to)
        {
            storage = to;
            task = Task.Return;
            if (Walk(to.Cells, true)) return true;
            Idle();
            return false;
        }

        /// <summary>Step out of <paramref name="area"/> (a building is about to appear there).</summary>
        public void MakeRoom(RectInt area)
        {
            if (task == Task.MakeRoom) return;
            if (!nav.NearestFreeSpot(Position, area, out var spot)) return;
            Abandon();
            task = Task.MakeRoom;
            if (Walk(new RectInt(NavGrid.CellAt(spot), Vector2Int.one), false)) return;
            path.Clear();          // no path found: just step straight there
            path.Add(spot);
        }

        /// <summary>
        /// Runs from a zombie at <paramref name="from"/>: to the throne if that's further from
        /// it, otherwise straight away from it. Drops the job it was doing.
        /// </summary>
        void Flee(Vector2 from)
        {
            scaredUntil = Time.time + ScaredSeconds;
            if (task == Task.Flee) return;
            Abandon();
            task = Task.Flee;
            var throne = kingdom.Throne;
            if (throne != null && (throne.Center - from).sqrMagnitude > (Position - from).sqrMagnitude + 1f && Walk(throne.Cells, true)) return;
            var away = Position + (Position - from).normalized * 6f;
            if (nav.NearestFreeSpot(away, new RectInt(0, 0, 0, 0), out var spot) && Walk(new RectInt(NavGrid.CellAt(spot), Vector2Int.one), false)) return;
            Idle();   // nowhere to run: hide where it is
        }

        void OnDied(Health h)
        {
            var remains = Remains.Current;
            if (remains != null && sprites.Dead != null && sprites.Dead.Length > 0)
            {
                float a = body.localEulerAngles.z + 180f + Random.Range(-30f, 30f);
                remains.Body(sprites.Dead[Character.RulerLook.Direction(a, sprites.Dead.Length)], Position, a);
                remains.Blood(Position, 1.1f);
            }
            // What they were carrying is left where they fell.
            if (carryAmount > 0)
            {
                var carried = new int[Inventory.TypeCount];
                carried[(int)carryType] = carryAmount;
                LootBag.Drop(Position, carried);
            }
            carryAmount = 0;
            Abandon();
            kingdom.Lost(this);
            Destroy(gameObject);
        }

        // It turns into a zombie where it stands (what it carried is left on the ground).
        void Turn()
        {
            var at = Position;
            if (carryAmount > 0)
            {
                var carried = new int[Inventory.TypeCount];
                carried[(int)carryType] = carryAmount;
                LootBag.Drop(at, carried);
                carryAmount = 0;
            }
            Abandon();
            kingdom.Lost(this);
            var director = ZombieDirector.Current;
            if (director != null && director.TurnedKind != null) director.Spawn(director.TurnedKind, at, 1f);
            var pixels = Destruction.PixelWorld.Current;
            if (pixels != null) pixels.Burst(at, new Color32(80, 10, 12, 255), 8, 2f);
            Audio.Sounds.Play(Audio.Sound.Groan, at);
            kingdom.Report($"{(string.IsNullOrEmpty(Name) ? "A villager" : Name)} was bitten, and has turned! A zombie is loose in your kingdom.");
            Destroy(gameObject);
        }

        /// <summary>Carrying something that nobody needs right now.</summary>
        public bool HasLeftovers => carryAmount > 0 && task == Task.Idle;
        public ResourceType CarryType => carryType;
        /// <summary>Which way it faces, in degrees (for showing it to other players).</summary>
        public float Facing => body != null ? body.localEulerAngles.z : 0f;
        /// <summary>Walking (not standing still).</summary>
        public bool Moving { get; private set; }
        public int CarryAmount => carryAmount;

        // ------------------------------------------------------------ update --

        void Update()
        {
            if (config == null || Health == null || Health.Dead) return;
            // Bitten: it turns (out on the map: not inside a house or mine, where zombies can't be).
            if (turnAt >= 0f && Time.time >= turnAt && space == null)
            {
                Turn();
                return;
            }
            Vector2 before = transform.position;
            if (config.needs) Needs();

            if (Time.time >= lookAt)
            {
                lookAt = Time.time + 0.3f;
                var director = ZombieDirector.Current;
                var zombie = director != null && job != Job.Guard ? director.NearestVisible(Position, config.fleeDistance) : null;
                // Wolves: a hunter shoots one coming close; the others run (not guards).
                var wild = World.Wildlife.Current;
                bool hunter = job == Job.Hunter && config.huntWeapon != null;
                var wolf = wild != null && space == null && job != Job.Guard ? wild.NearestWolf(Position, hunter ? config.huntRange : config.fleeDistance) : null;
                if (zombie != null) Flee(zombie.Position);
                else if (wolf != null && hunter)
                {
                    if (task != Task.Hunt || quarry != wolf)
                    {
                        Abandon();
                        StartHunt(wolf);
                    }
                }
                else if (wolf != null) Flee(wolf.Position);
                // Zombies about: drop the job and make for the shelter (the kingdom sends it there). Not out of bed, though.
                else if (job != Job.Guard && task != Task.Shelter && task != Task.Flee && !InBed && kingdom.ShelterNow && kingdom.HasShelter)
                {
                    Abandon();
                    thinkAt = Time.time;
                }
            }

            if (task == Task.Idle)
            {
                if (Time.time >= thinkAt && !Scared)
                {
                    thinkAt = Time.time + config.thinkSeconds * Random.Range(0.8f, 1.2f);
                    kingdom.AssignJob(this);
                }
            }
            else if ((task == Task.Fetch || task == Task.Deliver || task == Task.Build) && (blueprint == null || blueprint.Cancelled))
            {
                Abandon();
            }
            else if ((task == Task.Fetch || task == Task.Return || task == Task.Eat) && storage == null)
            {
                Abandon();
            }
            else if (task == Task.Sleep && (object)bed != null && bed == null)
            {
                Idle();   // its bed was broken or torn down
                thinkAt = Time.time;
            }
            else if ((task == Task.Water || task == Task.Douse) && (fire == null || fire.Out || (task == Task.Water && well == null)))
            {
                Idle();   // the fire's out (or the well went)
                thinkAt = Time.time;
            }
            else if (task == Task.Hunt)
            {
                Hunting();
            }
            else if (task == Task.PickUp && pickup == null)
            {
                Idle();   // someone else picked it up (or it was left too long)
                thinkAt = Time.time;
            }
            else if (task == Task.Dig && (mine == null || !mine.IsMarked(digCell)))
            {
                Idle();   // the mark was taken off, or the mine is gone
                thinkAt = Time.time;
            }
            else if (task == Task.Craft && (bench == null || (benchMachine != null ? !benchMachine.WantsWork : kingdom.Armory == null || !kingdom.Armory.HasWork)))
            {
                Idle();   // the anvil went, or there's nothing left to make
                thinkAt = Time.time;
            }
            else if (task == Task.Shelter && (shelter == null || !kingdom.ShelterNow))
            {
                Idle();   // the zombies have gone (or the flag has): back to work
                thinkAt = Time.time;
            }
            else if (task == Task.Guard && post == null)
            {
                Idle();   // its tower was broken or torn down
                thinkAt = Time.time;
            }
            else if (task == Task.Gather && (node == null || node.Empty))
            {
                Idle();   // someone else used it up, or it went: find another
                thinkAt = Time.time;
            }
            else if (pathIndex < path.Count)
            {
                FollowPath();
            }
            else if ((object)portal != null)   // (a house that went meanwhile counts too: GoThrough drops the job)
            {
                GoThrough();
            }
            else
            {
                Arrive();
            }

            Animate(((Vector2)transform.position - before) / Mathf.Max(Time.deltaTime, 1e-5f));
        }

        void FollowPath()
        {
            var target = path[pathIndex];
            // A wall or building went up on the way: find another way.
            if (!nav.Walkable(target))
            {
                if (!Walk(goal, goalBeside)) Abandon();
                return;
            }
            Vector2 here = transform.position;
            // (deep snow slows walking on the map, not inside houses and mines)
            float snow = space == null && World.Seasons.Current != null ? World.Seasons.Current.MoveFactor : 1f;
            float step = config.moveSpeed * kingdom.Boost(Kingdom.Upgrade.Speed) * Pace * snow * Time.deltaTime;
            var to = target - here;
            if (to.magnitude <= step)
            {
                transform.position = target;
                pathIndex++;
            }
            else
            {
                transform.position = here + to.normalized * step;
            }
        }

        void Arrive()
        {
            switch (task)
            {
                case Task.Fetch:
                {
                    int got = storage.TakeReserved(carryType, promised);
                    if (got < promised) blueprint.RemoveIncoming(carryType, promised - got);
                    carryAmount = got;
                    promised = 0;
                    storage = null;
                    if (got <= 0) { Idle(); return; }
                    promised = got;   // still counted as on its way to the blueprint
                    task = Task.Deliver;
                    if (!Walk(blueprint.Cells, true))
                    {
                        blueprint.RemoveIncoming(carryType, promised);
                        promised = 0;
                        blueprint.UnreachableUntil = Time.time + 3f;
                        Idle();
                    }
                    return;
                }
                case Task.Deliver:
                {
                    blueprint.RemoveIncoming(carryType, promised);
                    promised = 0;
                    carryAmount -= blueprint.Deliver(carryType, carryAmount);
                    Idle();
                    thinkAt = Time.time; // look for the next job straight away
                    return;
                }
                case Task.Build:
                {
                    if (blueprint.WorkDone || !blueprint.HasAllResources)
                    {
                        Idle();
                        return;
                    }
                    blueprint.AddWork(config.workRate * kingdom.BuildBoost * Pace * Time.deltaTime);
                    if (Time.time >= hammerAt)
                    {
                        hammerAt = Time.time + Random.Range(0.45f, 0.75f);
                        Audio.Sounds.Play(Audio.Sound.Hammer, Position, 0.8f);
                    }
                    var toSite = blueprint.Center - Position;
                    if (toSite.sqrMagnitude > 0.0001f) facing = toSite.normalized;
                    return;
                }
                case Task.Guard:
                    if (!onPost) Mount();
                    return;
                case Task.Shelter:
                    return;   // waits here
                case Task.Sleep:
                    if (!asleep) LieDown();
                    return;
                case Task.Water:
                {
                    var toWell = well.Cells.center - Position;
                    if (toWell.sqrMagnitude > 0.0001f) facing = toWell.normalized;
                    fillClock += Time.deltaTime;
                    if (fillClock < 1f) return;
                    bucket = true;
                    task = Task.Douse;
                    if (!Walk(fire.Cells, true)) { Idle(); thinkAt = Time.time; }
                    return;
                }
                case Task.Douse:
                {
                    var toFire = fire.Center - Position;
                    if (toFire.sqrMagnitude > 0.0001f) facing = toFire.normalized;
                    var fires = Fires.Current;
                    if (fires != null) fires.Douse(fire, fires.Config.bucketDouse);
                    var pixels = Destruction.PixelWorld.Current;
                    if (pixels != null) pixels.Burst(Position + facing * 0.6f, new Color32(150, 180, 210, 255), 14, 2.5f);
                    Audio.Sounds.Play(Audio.Sound.Splash, Position, 0.7f);
                    Idle();
                    thinkAt = Time.time;   // another bucket, if it's still burning
                    return;
                }
                case Task.Eat:
                {
                    var toStore = storage.Center - Position;
                    if (toStore.sqrMagnitude > 0.0001f) facing = toStore.normalized;
                    if (meal > 0)
                    {
                        // Puts down what it's carrying (if there's room), takes its meal and eats it there.
                        if (carryAmount > 0) carryAmount -= storage.Deposit(carryType, carryAmount);
                        int asked = meal;
                        int got = storage.TakeReserved(ResourceType.Food, meal);
                        meal = 0;
                        if (got <= 0) { Idle(); thinkAt = Time.time; return; }
                        mealShare = got / (float)asked;   // (someone took some first: part of a meal)
                        eatClock = 0f;
                    }
                    eatClock += Time.deltaTime;
                    if (eatClock < config.eatSeconds) return;
                    fed = Mathf.Min(1f, fed + mealShare);
                    mealShare = 0f;
                    Idle();
                    thinkAt = Time.time;
                    return;
                }
                case Task.Craft:
                {
                    var toBench = bench.Cells.center - Position;
                    if (toBench.sqrMagnitude > 0.0001f) facing = toBench.normalized;
                    float work = Time.deltaTime * config.workRate * kingdom.Boost(Kingdom.Upgrade.Crafting) * Pace;
                    if (benchMachine != null) benchMachine.Work(work);   // a crafting table: the age's part
                    else kingdom.Armory.Work(work);                       // an anvil: weapons
                    return;
                }
                case Task.Dig:
                {
                    var toRock = (Vector2)digCell + new Vector2(0.5f, 0.5f) - Position;
                    if (toRock.sqrMagnitude > 0.0001f) facing = toRock.normalized;
                    digClock += Time.deltaTime * config.workRate * kingdom.Boost(Kingdom.Upgrade.Gathering) * Pace;
                    if (digClock < mine.Config.digSeconds) return;
                    if (mine.Dig(digCell, out var type, out int amount))
                    {
                        if (carryAmount > 0 && carryType != type) carryAmount = 0;   // (the kingdom sends it to storage first; just in case)
                        carryType = type;
                        carryAmount = Mathf.Min(config.carryAmount, carryAmount + amount);
                    }
                    Idle();
                    thinkAt = Time.time;
                    return;
                }
                case Task.Gather:
                {
                    var toNode = (Vector2)node.transform.position - Position;
                    if (toNode.sqrMagnitude > 0.0001f) facing = toNode.normalized;
                    gatherClock += Time.deltaTime * config.workRate * kingdom.Boost(Kingdom.Upgrade.Gathering) * Pace;
                    if (gatherClock < node.Config.secondsPerHit) return;
                    gatherClock -= node.Config.secondsPerHit;
                    if (carryAmount > 0 && carryType != node.Config.resource) { Idle(); return; }
                    carryType = node.Config.resource;
                    carryAmount += node.Gather(config.carryAmount - carryAmount);
                    // Full, or nothing left here: off to storage (or the next one).
                    if (carryAmount >= config.carryAmount || node.Empty)
                    {
                        Idle();
                        thinkAt = Time.time;
                    }
                    return;
                }
                case Task.Return:
                {
                    carryAmount -= storage.Deposit(carryType, carryAmount);
                    storage = null;
                    Idle();
                    return;
                }
                case Task.PickUp:
                {
                    if (carryAmount > 0 && carryType != ResourceType.Food) { Idle(); return; }
                    carryType = ResourceType.Food;
                    carryAmount += pickup.Take(ResourceType.Food, config.carryAmount - carryAmount);
                    Idle();
                    thinkAt = Time.time;   // off to storage
                    return;
                }
                default:
                    Idle();
                    return;
            }
        }

        // Hunting: closer until it's in range with nothing in the way, then a shot every so often (aimed where the
        // animal will be when the arrow gets there). Gives up after a while, or if there's no way to it.
        void Hunting()
        {
            if (quarry == null || quarry.Health == null || quarry.Health.Dead || quarry.Leaving || Time.time >= huntUntil)
            {
                if (quarry != null && Time.time >= huntUntil) quarry.UnreachableUntil = Time.time + 20f;   // (try another for a while)
                Idle();
                thinkAt = Time.time;
                return;
            }
            if (space == null && InShot())
            {
                path.Clear();
                pathIndex = 0;
                portal = null;
                var to = quarry.Position - Position;
                if (to.sqrMagnitude > 1e-6f) facing = to.normalized;
                if (Time.time >= shotAt)
                {
                    shotAt = Time.time + config.huntShotSeconds;
                    Shoot();
                }
                return;
            }
            // Out of range (or something in the way): after it, finding the way again as it moves.
            if (Time.time >= repathAt)
            {
                repathAt = Time.time + 1f;
                if (!Walk(new RectInt(NavGrid.CellAt(quarry.Position), Vector2Int.one), true))
                {
                    quarry.UnreachableUntil = Time.time + 15f;
                    Idle();
                    thinkAt = Time.time;
                    return;
                }
            }
            if (pathIndex < path.Count) FollowPath();
            else if ((object)portal != null) GoThrough();
        }

        // Close enough to shoot it, with no wall in the way.
        bool InShot()
        {
            var to = quarry.Position - Position;
            float d = to.magnitude;
            if (d > config.huntRange) return false;
            var pixels = Destruction.PixelWorld.Current;
            return d < 0.5f || pixels == null || !pixels.Raycast(Position + to / d * 0.3f, quarry.Position, out _, out _);
        }

        void Shoot()
        {
            var shots = Combat.Projectiles.Current;
            var bow = config.huntWeapon;
            if (shots == null || bow == null) return;
            float flight = Vector2.Distance(Position, quarry.Position) / Mathf.Max(1f, bow.projectileSpeed);
            var aim = quarry.Position + quarry.Velocity * flight - Position;
            if (aim.sqrMagnitude < 1e-6f) return;
            aim.Normalize();
            if (shots.Fire(bow, Position + aim * 0.3f, aim)) Audio.Sounds.Fired(bow, Position);
        }

        // Drops the current job, giving back anything promised.
        void Abandon()
        {
            CancelPromise();
            Idle();
        }

        void CancelPromise()
        {
            if (promised > 0)
            {
                if (task == Task.Fetch && storage != null) storage.Unreserve(carryType, promised);
                if (blueprint != null && !blueprint.Cancelled) blueprint.RemoveIncoming(carryType, promised);
            }
            promised = 0;
        }

        void Idle()
        {
            WakeUp();
            if ((object)bed != null) kingdom.ReleaseBed(bed);
            bed = null;
            napping = false;
            if (meal > 0 && storage != null) storage.Unreserve(ResourceType.Food, meal);
            meal = 0;
            if (fire != null) fire.Fighters = Mathf.Max(0, fire.Fighters - 1);
            fire = null;
            if (quarry != null && quarry.Hunter == (Component)this) quarry.Hunter = null;
            quarry = null;
            if (pickup != null && pickup.Collector == (Component)this) pickup.Collector = null;
            pickup = null;
            well = null;
            bucket = false;
            task = Task.Idle;
            if (node != null && node.Gatherer == (Component)this) node.Gatherer = null;
            node = null;
            Dismount();
            if (post != null && post.Guard == this) post.Guard = null;
            post = null;
            if (mine != null) mine.Release(digCell, this);
            mine = null;
            bench = null;
            benchMachine = null;
            shelter = null;
            CountAsBuilder(null);
            blueprint = null;
            storage = null;
            path.Clear();
            pathIndex = 0;
        }

        void CountAsBuilder(Blueprint target)
        {
            if (countedOn != null) countedOn.Builders = Mathf.Max(0, countedOn.Builders - 1);
            countedOn = target;
            if (countedOn != null) countedOn.Builders++;
        }

        bool Walk(RectInt target, bool beside)
        {
            goal = target;
            goalBeside = beside;
            pathIndex = 0;
            portal = null;
            var there = kingdom.PlaceAt(target.position);
            if (there == space) return nav.FindPath(Position, target, beside, path);
            if (space != null)
            {
                // Out of this house first: to the door, inside.
                portal = space;
                return space.Nav != null && space.Nav.FindPath(Position, new RectInt(space.EntranceCell, Vector2Int.one), false, path);
            }
            // On the map, going into a house: to its door.
            portal = there;
            return mapNav.FindPath(Position, new RectInt(there.OutsideCell, Vector2Int.one), false, path);
        }

        // At a house's door: in (or out), then on towards the goal.
        void GoThrough()
        {
            var through = portal;
            portal = null;
            if (through == null)
            {
                // The house went while it was on its way.
                Abandon();
                return;
            }
            if (space == through)
            {
                space = null;
                transform.position = through.Outside;
            }
            else
            {
                space = through;
                transform.position = through.Entrance;
            }
            if (!Walk(goal, goalBeside))
            {
                // In the mine but no way to the rock it came to dig (furniture in the way).
                if (task == Task.Dig && mine != null) mine.CantReach(digCell);
                Abandon();
            }
        }

        /// <summary>Puts back what a saved game says: the house or mine it's in (or null), what it carries, its hit points.</summary>
        public void Restore(Place inside, ResourceType type, int amount, float health)
        {
            space = inside;
            portal = null;
            carryType = type;
            carryAmount = Mathf.Max(0, amount);
            if (Health != null) Health.SetCurrent(health);
        }

        /// <summary>Its house is gone (torn down, broken): it's put out by the door and drops what it was doing.</summary>
        public void PutOutside(Vector2 at)
        {
            Abandon();   // (first: getting out of bed puts it back by the bed)
            space = null;
            portal = null;
            transform.position = at;
        }

        void Animate(Vector2 velocity)
        {
            if (asleep)
            {
                Moving = false;
                return;   // (in bed, or lying on the ground: see ShowAsleep)
            }
            float speed = velocity.magnitude;
            Moving = speed > 0.1f;
            if (speed > 0.1f)
            {
                facing = velocity / speed;
                stepClock += Time.deltaTime * config.stepsPerSecond;
            }
            else
            {
                stepClock = 0f;
            }
            var frames = carryAmount > 0 || bucket ? sprites.Carrying : sprites.Walking;
            // Turning: greyer and greener over its last half minute.
            float turning = turnAt >= 0f ? Mathf.Clamp01(1f - (turnAt - Time.time) / 30f) : 0f;
            bodyRenderer.color = Color.Lerp(Color.white, new Color(0.7f, 0.84f, 0.64f), turning);
            // Turn smoothly towards where it's going, showing the drawing for the nearest direction.
            float wanted = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            float now = Mathf.MoveTowardsAngle(body.localEulerAngles.z, wanted, 540f * Time.deltaTime);
            int direction = Character.RulerLook.Direction(now, frames.GetLength(0));
            bodyRenderer.sprite = frames[direction, Mathf.FloorToInt(stepClock) % frames.GetLength(1)];
            body.localRotation = Quaternion.Euler(0f, 0f, now);
            shadow.transform.localRotation = body.localRotation;
        }

        void OnDestroy()
        {
            CancelPromise();
            CountAsBuilder(null);
        }
    }
}
