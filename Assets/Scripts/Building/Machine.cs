using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// A machine working by itself (see <see cref="MachineKind"/>). The smelter takes iron and
    /// wood from storage and puts steel back. A generator burns wood from storage and powers
    /// the sawmills and quarries within its reach. Those send what they make along the
    /// conveyor belts touching them to a storehouse or chest the belts (or the machine) touch;
    /// with no way to storage they fill up and stop. A machine that isn't working is drawn darker.
    /// An electric lamp shines only while a burning generator's power reaches it.
    /// </summary>
    public sealed class Machine : MonoBehaviour
    {
        const float RouteSeconds = 2f;
        const int MaxBelts = 2000;

        static readonly List<Machine> generators = new List<Machine>();
        static readonly HashSet<Vector2Int> belts = new HashSet<Vector2Int>();

        sealed class Parcel
        {
            public Transform Look;
            public List<Vector2> Path;
            public float Start;
            public ResourceType Type;
            public int Amount;
            public Storage To;
        }

        Furniture piece;
        MachineConfig config;
        Kingdom kingdom;
        SpriteRenderer look;
        float workAt, poweredUntil, routeAt;
        FireLight lamp;
        int held;
        bool working;
        Storage target;
        readonly List<Vector2> route = new List<Vector2>();
        readonly List<Parcel> parcels = new List<Parcel>();

        public MachineConfig Config => config;
        /// <summary>A generator that's burning now.</summary>
        public bool Burning => Time.time < poweredUntil;

        public void Setup(Furniture furniture, MachineConfig machine, SpriteRenderer picture, Kingdom owner)
        {
            piece = furniture;
            config = machine;
            look = picture;
            kingdom = owner;
            workAt = Time.time + config.workSeconds;
            if (config.kind == MachineKind.Conveyor)
            {
                foreach (var c in furniture.Cells.allPositionsWithin) belts.Add(c);
                enabled = false;
            }
            else if (config.kind == MachineKind.Generator) generators.Add(this);
            else if (config.kind == MachineKind.Lamp)
            {
                // A cold white electric light, off until there's power.
                lamp = FireLight.Create(transform, Vector2.zero, new Color(0.9f, 0.95f, 1f),
                    FireLight.DefaultRadius * config.reach, FireLight.DefaultIntensity * 1.15f);
                lamp.gameObject.SetActive(false);
                workAt = Time.time + Random.value * 0.5f;   // (lamps don't all check on the same frame)
            }
        }

        void OnDestroy()
        {
            if (config == null) return;
            if (config.kind == MachineKind.Conveyor && piece != null) foreach (var c in piece.Cells.allPositionsWithin) belts.Remove(c);
            generators.Remove(this);
            foreach (var p in parcels) if (p.Look != null) Destroy(p.Look.gameObject);
        }

        /// <summary>Whether a generator's power reaches <paramref name="at"/>.</summary>
        public static bool PoweredAt(Vector2 at)
        {
            foreach (var g in generators)
                if (g != null && g.Burning && ((Vector2)g.transform.position - at).sqrMagnitude <= g.config.reach * g.config.reach) return true;
            return false;
        }

        void Update()
        {
            if (kingdom == null) return;
            float now = Time.time;
            if (config.kind == MachineKind.Lamp)
            {
                if (now < workAt) return;
                workAt = now + 0.5f;
                // Inside a house (far off the map): powered if the house is in a generator's reach.
                bool on = PoweredAt(PowerSpot());
                if (lamp != null && lamp.gameObject.activeSelf != on) lamp.gameObject.SetActive(on);
                if (look != null) look.color = on ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
                return;
            }
            Move(now);
            if (now >= workAt)
            {
                workAt = now + config.workSeconds;
                working = Work();
            }
            if (held > 0 && parcels.Count < 4) Send(now);
            if (look != null) look.color = working || Burning ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
        }

        // One round of work; false if it couldn't (no power, nothing to use, full).
        bool Work()
        {
            Vector2 at = transform.position;
            switch (config.kind)
            {
                case MachineKind.Generator:
                    if (config.woodIn > 0 && !kingdom.TrySpend(config.woodIn, 0, 0)) return false;
                    poweredUntil = Time.time + config.workSeconds + 0.5f;
                    return true;

                case MachineKind.Smelter:
                {
                    if (kingdom.Available(ResourceType.Iron) < config.ironIn || kingdom.Available(ResourceType.Wood) < config.woodIn) return false;
                    var cost = new int[Inventory.TypeCount];
                    cost[(int)ResourceType.Iron] = config.ironIn;
                    cost[(int)ResourceType.Wood] = config.woodIn;
                    if (!kingdom.TrySpend(cost)) return false;
                    int stored = kingdom.Store(ResourceType.Steel, config.output, at);
                    if (stored < config.output) kingdom.Store(ResourceType.Iron, config.ironIn, at);   // storage full: the iron goes back
                    return stored > 0;
                }

                case MachineKind.Sawmill:
                case MachineKind.Quarry:
                {
                    if (held >= config.holds || !PoweredAt(PowerSpot())) return false;
                    if (config.kind == MachineKind.Quarry)
                    {
                        held += config.output;
                        return true;
                    }
                    // The nearest tree with wood left within reach (one nobody is cutting).
                    ResourceNode best = null;
                    float bestD = config.reach * config.reach;
                    foreach (var n in ResourceNode.All)
                    {
                        if (n == null || n.Empty || n.Config == null || n.Config.resource != ResourceType.Wood || n.Gatherer != null) continue;
                        float d = ((Vector2)n.transform.position - at).sqrMagnitude;
                        if (d <= bestD) { best = n; bestD = d; }
                    }
                    if (best == null) return false;
                    int got = 0;
                    while (got < config.output && !best.Empty)
                    {
                        int g = best.Gather(config.output - got);
                        if (g <= 0) break;
                        got += g;
                    }
                    held += got;
                    return got > 0;
                }
            }
            return false;
        }

        // Where power is measured from: here, or for a machine inside a house (far off the map), the house's door.
        Vector2 PowerSpot()
        {
            var place = kingdom.PlaceAt((Vector2)transform.position);
            return place != null ? place.Outside : (Vector2)transform.position;
        }

        ResourceType Makes => config.kind == MachineKind.Quarry ? ResourceType.Stone : ResourceType.Wood;

        // What's held goes out along the belts to storage, if there's a way and room.
        void Send(float now)
        {
            if (now >= routeAt)
            {
                routeAt = now + RouteSeconds;
                FindRoute();
            }
            if (target == null || target.Stock.SpaceLeft < held) return;
            var parcel = new Parcel { Path = new List<Vector2>(route), Start = now, Type = Makes, Amount = held, To = target };
            held = 0;
            var go = new GameObject("On the belt");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = ArtSprites.Get(parcel.Type == ResourceType.Stone ? ArtKeys.Rock(0) : ArtKeys.LootBag);
            r.sortingOrder = SortOrder.Furniture + 2;
            go.transform.localScale = Vector3.one * 0.4f;
            go.transform.position = parcel.Path[0];
            parcel.Look = go.transform;
            parcels.Add(parcel);
        }

        void Move(float now)
        {
            for (int i = parcels.Count - 1; i >= 0; i--)
            {
                var p = parcels[i];
                float along = (now - p.Start) * config.beltSpeed;
                int seg = Mathf.FloorToInt(along);
                if (seg >= p.Path.Count - 1)
                {
                    // Arrived: into the storehouse (or the nearest with room, if that one went or filled up).
                    int put = p.To != null ? p.To.Deposit(p.Type, p.Amount) : 0;
                    if (put < p.Amount) put += kingdom.Store(p.Type, p.Amount - put, p.Path[p.Path.Count - 1]);
                    if (put < p.Amount) held += p.Amount - put;
                    if (p.Look != null) Destroy(p.Look.gameObject);
                    parcels.RemoveAt(i);
                    continue;
                }
                // (Through a house's wall hole the belt goes to its inside, far off the map: no flying across.)
                if (p.Look != null) p.Look.position = (p.Path[seg + 1] - p.Path[seg]).sqrMagnitude > 4f ? p.Path[seg + 1]
                    : Vector2.Lerp(p.Path[seg], p.Path[seg + 1], along - seg);
            }
        }

        static readonly Vector2Int[] Sides = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        // The shortest way from the machine along touching belts to a storehouse or chest (none: target null).
        void FindRoute()
        {
            target = null;
            route.Clear();
            var storeAt = new Dictionary<Vector2Int, Storage>();
            // Each house has a belt hole in the middle of each wall: a belt just outside it joins one on the
            // inside edge tile there (both ways), so machines inside can send things out and the other way.
            var holes = new Dictionary<Vector2Int, Vector2Int>();
            foreach (var h in kingdom.Houses)
            {
                if (h == null || h.Interior == null) continue;
                RectInt o = h.Cells, i = h.Interior.Inner;
                void Hole(Vector2Int outside, Vector2Int inside) { holes[outside] = inside; holes[inside] = outside; }
                Hole(new Vector2Int(o.xMax, o.y + o.height / 2), new Vector2Int(i.xMax - 1, i.y + i.height / 2));
                Hole(new Vector2Int(o.x - 1, o.y + o.height / 2), new Vector2Int(i.x, i.y + i.height / 2));
                Hole(new Vector2Int(o.x + o.width / 2, o.yMax), new Vector2Int(i.x + i.width / 2, i.yMax - 1));
                Hole(new Vector2Int(o.x + o.width / 2, o.y - 1), new Vector2Int(i.x + i.width / 2, i.y));
            }
            foreach (var s in kingdom.Storages)
                if (s != null) foreach (var c in s.Cells.allPositionsWithin) storeAt[c] = s;

            var cells = piece.Cells;
            var from = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            foreach (var c in cells.allPositionsWithin)
                foreach (var d in Sides)
                {
                    var n = c + d;
                    if (cells.Contains(n)) continue;
                    if (storeAt.TryGetValue(n, out var next))
                    {
                        target = next;
                        route.Add(cells.center);
                        route.Add((Vector2)n + new Vector2(0.5f, 0.5f));
                        return;
                    }
                    if (belts.Contains(n) && !from.ContainsKey(n))
                    {
                        from[n] = c;
                        queue.Enqueue(n);
                    }
                }
            while (queue.Count > 0 && from.Count < MaxBelts)
            {
                var c = queue.Dequeue();
                if (holes.TryGetValue(c, out var other) && belts.Contains(other) && !from.ContainsKey(other))
                {
                    from[other] = c;
                    queue.Enqueue(other);
                }
                foreach (var d in Sides)
                {
                    var n = c + d;
                    if (storeAt.TryGetValue(n, out var store))
                    {
                        target = store;
                        var back = new List<Vector2> { (Vector2)n + new Vector2(0.5f, 0.5f) };
                        for (var k = c; ; k = from[k])
                        {
                            back.Add((Vector2)k + new Vector2(0.5f, 0.5f));
                            if (cells.Contains(from[k])) break;
                        }
                        back.Add(cells.center);
                        back.Reverse();
                        route.AddRange(back);
                        return;
                    }
                    if (belts.Contains(n) && !from.ContainsKey(n) && !cells.Contains(n))
                    {
                        from[n] = c;
                        queue.Enqueue(n);
                    }
                }
            }
        }
    }
}
