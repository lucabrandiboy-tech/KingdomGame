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
    /// it's safe. Zombies can kill them. (Needs like food and sleep come later.)
    /// </summary>
    public sealed class Villager : MonoBehaviour
    {
        enum Task { Idle, Fetch, Deliver, Build, Return, MakeRoom, Flee, Gather }

        /// <summary>Seconds a villager stays scared after last seeing a zombie.</summary>
        const float ScaredSeconds = 4f;

        VillagerConfig config;
        Kingdom kingdom;
        NavGrid mapNav;
        Interior space;          // the house it's inside, or null on the map
        Interior portal;         // the house door it's walking to, to go in or out of
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
        float gatherClock;
        Job job = Job.Builder;
        ResourceType carryType;
        int carryAmount;         // in hand
        int promised;            // reserved at the storehouse / on its way to the blueprint
        float thinkAt;
        float stepClock;
        float scaredUntil, lookAt;
        Vector2 facing = Vector2.down;

        public bool IsIdle => task == Task.Idle;
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
                    case Task.Fetch: return $"fetching {promised} {carryType.ToString().ToLowerInvariant()}";
                    case Task.Deliver: return $"carrying {carryAmount} {carryType.ToString().ToLowerInvariant()}";
                    case Task.Build: return "building";
                    case Task.Return: return "taking things back to storage";
                    case Task.MakeRoom: return "making room";
                    case Task.Flee: return "running from zombies";
                    case Task.Gather: return node != null ? $"gathering {node.Config.resource.ToString().ToLowerInvariant()}" : "gathering";
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
            Health.Hurt += (h, amount, from) => { if (!h.Dead) Flee(from); };
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
                LootBag.Drop(Position, carryType == ResourceType.Wood ? carryAmount : 0, carryType == ResourceType.Stone ? carryAmount : 0,
                    carryType == ResourceType.Food ? carryAmount : 0);
            carryAmount = 0;
            Abandon();
            kingdom.Lost(this);
            Destroy(gameObject);
        }

        /// <summary>Carrying something that nobody needs right now.</summary>
        public bool HasLeftovers => carryAmount > 0 && task == Task.Idle;
        public ResourceType CarryType => carryType;
        public int CarryAmount => carryAmount;

        // ------------------------------------------------------------ update --

        void Update()
        {
            if (config == null || Health == null || Health.Dead) return;
            Vector2 before = transform.position;

            if (Time.time >= lookAt)
            {
                lookAt = Time.time + 0.3f;
                var director = ZombieDirector.Current;
                var zombie = director != null ? director.NearestVisible(Position, config.fleeDistance) : null;
                if (zombie != null) Flee(zombie.Position);
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
            else if ((task == Task.Fetch || task == Task.Return) && storage == null)
            {
                Abandon();
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
            float step = config.moveSpeed * Time.deltaTime;
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
                    blueprint.AddWork(config.workRate * Time.deltaTime);
                    var toSite = blueprint.Center - Position;
                    if (toSite.sqrMagnitude > 0.0001f) facing = toSite.normalized;
                    return;
                }
                case Task.Gather:
                {
                    var toNode = (Vector2)node.transform.position - Position;
                    if (toNode.sqrMagnitude > 0.0001f) facing = toNode.normalized;
                    gatherClock += Time.deltaTime * config.workRate;
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
                default:
                    Idle();
                    return;
            }
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
            task = Task.Idle;
            if (node != null && node.Gatherer == (Component)this) node.Gatherer = null;
            node = null;
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
            var there = kingdom.InteriorAt(target.position);
            if (there == space) return nav.FindPath(Position, target, beside, path);
            if (space != null)
            {
                // Out of this house first: to the door, inside.
                portal = space;
                return space.Nav != null && space.Nav.FindPath(Position, new RectInt(space.EntranceCell, Vector2Int.one), false, path);
            }
            // On the map, going into a house: to its door.
            portal = there;
            return mapNav.FindPath(Position, new RectInt(there.House.OutsideCell, Vector2Int.one), false, path);
        }

        // At a house's door: in (or out), then on towards the goal.
        void GoThrough()
        {
            var through = portal;
            portal = null;
            if (through == null || through.House == null)
            {
                // The house went while it was on its way.
                Abandon();
                return;
            }
            if (space == through)
            {
                space = null;
                transform.position = through.House.Outside;
            }
            else
            {
                space = through;
                transform.position = through.Entrance;
            }
            if (!Walk(goal, goalBeside)) Abandon();
        }

        /// <summary>Its house is gone (torn down, broken): it's put out by the door and drops what it was doing.</summary>
        public void PutOutside(Vector2 at)
        {
            space = null;
            portal = null;
            transform.position = at;
            Abandon();
        }

        void Animate(Vector2 velocity)
        {
            float speed = velocity.magnitude;
            if (speed > 0.1f)
            {
                facing = velocity / speed;
                stepClock += Time.deltaTime * config.stepsPerSecond;
            }
            else
            {
                stepClock = 0f;
            }
            var frames = carryAmount > 0 ? sprites.Carrying : sprites.Walking;
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
