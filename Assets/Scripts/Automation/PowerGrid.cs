using System;
using System.Collections.Generic;
using KingdomGame.Economy;

namespace KingdomGame.Automation
{
    /// <summary>The cells something stands on: its bottom-left cell and its size.</summary>
    public readonly struct Footprint
    {
        public readonly int X, Y, W, H;

        public Footprint(int x, int y, int w, int h)
        {
            X = x;
            Y = y;
            W = Math.Max(1, w);
            H = Math.Max(1, h);
        }

        public float CenterX => X + W * 0.5f;
        public float CenterY => Y + H * 0.5f;

        /// <summary>Whether it overlaps the box from (<paramref name="x0"/>, <paramref name="y0"/>) to (<paramref name="x1"/>, <paramref name="y1"/>) (tiles).</summary>
        public bool Touches(float x0, float y0, float x1, float y1) => X < x1 && X + W > x0 && Y < y1 && Y + H > y0;

        /// <summary>Whether it overlaps the circle round (<paramref name="cx"/>, <paramref name="cy"/>).</summary>
        public bool Touches(float cx, float cy, float radius)
        {
            float nx = Math.Max(X, Math.Min(cx, X + W)), ny = Math.Max(Y, Math.Min(cy, Y + H));
            return (nx - cx) * (nx - cx) + (ny - cy) * (ny - cy) < radius * radius;
        }
    }

    /// <summary>Something that runs on electricity (an electric arm, furnace, assembler or drill; in the game a lamp or an older machine too).</summary>
    public interface IPowerUser
    {
        /// <summary>MW it wants now: what it uses while working, or a little while it waits.</summary>
        float PowerDemand();
        /// <summary>The share of what it wanted that it gets (0 to 1): it works that much slower.</summary>
        void SetPower(float share);
    }

    /// <summary>
    /// A power pole: it powers the things in a square round it (<see cref="Supply"/> tiles across), and wires join it
    /// to the nearest poles within its <see cref="WireReach"/> (and theirs), up to <see cref="PowerGrid.MaxWires"/> of
    /// them, so the poles wired together make one network. A generator has one built in: a circle round it instead.
    /// </summary>
    public sealed class PowerPole
    {
        public readonly Footprint At;
        public readonly float WireReach;
        /// <summary>The side of the square it powers (tiles), or a circle's radius for a <see cref="Round"/> one.</summary>
        public readonly float Supply;
        public readonly bool Round;
        /// <summary>The network it's in (worked out again on the grid's next tick after poles come or go).</summary>
        public PowerNetwork Network { get; internal set; }
        internal int Index;

        public PowerPole(Footprint at, float wireReach, float supply, bool round = false)
        {
            At = at;
            WireReach = Math.Max(0f, wireReach);
            Supply = Math.Max(0f, supply);
            Round = round;
        }

        /// <summary>Whether it powers something standing on <paramref name="f"/>.</summary>
        public bool Powers(Footprint f)
        {
            if (Round) return f.Touches(At.CenterX, At.CenterY, Supply);
            float h = Supply * 0.5f;
            return f.Touches(At.CenterX - h, At.CenterY - h, At.CenterX + h, At.CenterY + h);
        }
    }

    /// <summary>
    /// A generator: up to <see cref="MaxOutput"/> MW, burning as much fuel as the network uses (half the load, half the
    /// coal). Coal first; wood also burns but holds a third of coal's energy. Arms can put fuel in it; when it has none it
    /// takes some from the kingdom's storage (<see cref="TakeFromStore"/>).
    /// </summary>
    public sealed class Generator : IItemPort
    {
        public const int MaxFuel = 20;
        const float LookAgain = 1f;

        readonly FuelTable fuels;
        float retry;

        public readonly Footprint At;
        public float MaxOutput = 0.9f;
        /// <summary>Energy ready to use (MJ), from fuel burnt.</summary>
        public float Energy;
        public int FuelCount { get; private set; }
        public ResourceType FuelType { get; private set; }
        /// <summary>Takes one fuel from storage (false: there's none).</summary>
        public TakeFuel TakeFromStore;
        /// <summary>What it gave last tick (MW), and that as a share of what it can give.</summary>
        public float Output { get; internal set; }
        public float Load => MaxOutput > 0f ? Output / MaxOutput : 0f;
        /// <summary>Why it can't give power (null: it can).</summary>
        public string Problem { get; private set; }

        public delegate bool TakeFuel(out ResourceType item);

        public Generator(Footprint at, FuelTable fuels)
        {
            At = at;
            this.fuels = fuels ?? new FuelTable(null);
        }

        /// <summary>The energy one <paramref name="item"/> gives it (MJ): wood a third of coal's.</summary>
        public float EnergyOf(ResourceType item) =>
            item == ResourceType.Wood && fuels.IsFuel(ResourceType.Coal) ? fuels.Energy(ResourceType.Coal) / 3f : fuels.Energy(item);

        public void Restore(float energy, ResourceType fuelType, int fuelCount)
        {
            Energy = Math.Max(0f, energy);
            FuelType = fuelType;
            FuelCount = Math.Max(0, Math.Min(MaxFuel, fuelCount));
        }

        /// <summary>Empties its fuel into <paramref name="spill"/> (counts by type).</summary>
        public void TakeAll(int[] spill)
        {
            if (FuelCount > 0 && spill != null && (int)FuelType < spill.Length) spill[(int)FuelType] += FuelCount;
            FuelCount = 0;
        }

        // What it can give this tick (MW): all it has if there's fuel (burning more when it runs low).
        internal float Available(float dt)
        {
            float need = MaxOutput * dt;
            if (Energy < need) Refuel(dt);
            if (Energy <= 0f)
            {
                Problem = "no fuel: coal or wood in storage, or put in by an arm";
                return 0f;
            }
            Problem = null;
            return Math.Min(MaxOutput, Energy / Math.Max(1e-4f, dt));
        }

        void Refuel(float dt)
        {
            if (FuelCount > 0)
            {
                FuelCount--;
                Energy += EnergyOf(FuelType);
                return;
            }
            if (retry > 0f)
            {
                retry -= dt;
                return;
            }
            if (TakeFromStore != null && TakeFromStore(out var item)) Energy += EnergyOf(item);
            else retry = LookAgain;
        }

        bool FuelFits(ResourceType item) =>
            EnergyOf(item) > 0f && FuelCount < MaxFuel && (FuelCount == 0 || FuelType == item);

        public bool Accepts(ResourceType item) => FuelFits(item);

        public bool Insert(ResourceType item)
        {
            if (!FuelFits(item)) return false;
            FuelType = item;
            FuelCount++;
            return true;
        }

        public bool Offer(Func<ResourceType, bool> want, out ResourceType item)
        {
            item = default;
            return false;
        }

        public bool Extract(ResourceType item) => false;
    }

    /// <summary>An accumulator: stores what the network has spare (up to <see cref="Capacity"/> MJ) and gives it back when it's short, at night.</summary>
    public sealed class Accumulator
    {
        public readonly Footprint At;
        public float Capacity = 5f;
        /// <summary>The most it takes in or gives out (MW).</summary>
        public float Rate = 0.3f;
        public float Stored;
        /// <summary>Last tick: MW going in (above 0) or out (below 0).</summary>
        public float Flow { get; internal set; }

        public Accumulator(Footprint at) => At = at;
    }

    /// <summary>A solar panel: <see cref="MaxOutput"/> MW in full daylight (<see cref="PowerGrid.Daylight"/>), none at night. No fuel.</summary>
    public sealed class SolarPanel
    {
        public readonly Footprint At;
        public float MaxOutput = 0.06f;
        public float Output { get; internal set; }

        public SolarPanel(Footprint at) => At = at;
    }

    /// <summary>Poles wired together, and what they power: how much is wanted and how much it got (last tick).</summary>
    public sealed class PowerNetwork
    {
        internal readonly List<PowerPole> Poles = new List<PowerPole>();
        internal readonly List<Generator> Generators = new List<Generator>();
        internal readonly List<Accumulator> Accumulators = new List<Accumulator>();
        internal readonly List<SolarPanel> Panels = new List<SolarPanel>();
        internal readonly List<IPowerUser> Users = new List<IPowerUser>();
        internal float[] Available = new float[4];   // (each generator's share this tick, reused)

        /// <summary>MW wanted, MW given (from generators, panels and accumulators), MW it could give, and the share of what was wanted that it got.</summary>
        public float Demand { get; internal set; }
        public float Supplied { get; internal set; }
        public float Capacity { get; internal set; }
        public float Satisfaction { get; internal set; } = 1f;
        /// <summary>MJ in its accumulators, and the most they hold.</summary>
        public float Stored { get; internal set; }
        public float StoreCapacity { get; internal set; }
        public int PoleCount => Poles.Count;
        public int GeneratorCount => Generators.Count;
        public int AccumulatorCount => Accumulators.Count;
        public int PanelCount => Panels.Count;
        public int UserCount => Users.Count;
    }

    /// <summary>
    /// A kingdom's electricity, Factorio-style: poles wired together make networks; the generators, solar panels and
    /// accumulators in a network's supply areas feed everything electric in them. Each tick a network adds up what its
    /// machines want; panels give first, then the generators share the rest (burning fuel only for what's used), then
    /// accumulators make up a shortfall, and anything spare charges them. When there isn't enough, every machine on the
    /// network gets the same share and works that much slower (a brownout). Plain C# (tools/tests/PowerTests.cs); the game
    /// runs it in Building/Factory.cs.
    /// </summary>
    public sealed class PowerGrid
    {
        /// <summary>The most poles a pole wires itself to (the nearest ones).</summary>
        public const int MaxWires = 5;
        /// <summary>The share of its working use a machine draws while it waits (Factorio's drain).</summary>
        public const float IdleShare = 1f / 30f;

        sealed class UserEntry
        {
            public IPowerUser User;
            public Footprint At;
            public PowerNetwork Network;
        }

        /// <summary>The grid poles are kept in (tiles a square), so wiring and finding the pole that powers something only look nearby.</summary>
        const int Bucket = 16;

        readonly List<PowerPole> poles = new List<PowerPole>();
        readonly Dictionary<long, List<PowerPole>> buckets = new Dictionary<long, List<PowerPole>>();
        readonly List<Generator> generators = new List<Generator>();
        readonly List<Accumulator> accumulators = new List<Accumulator>();
        readonly List<SolarPanel> panels = new List<SolarPanel>();
        readonly List<UserEntry> users = new List<UserEntry>();
        readonly Dictionary<IPowerUser, UserEntry> userIndex = new Dictionary<IPowerUser, UserEntry>();
        readonly List<PowerNetwork> networks = new List<PowerNetwork>();
        readonly List<(PowerPole A, PowerPole B)> wires = new List<(PowerPole, PowerPole)>();
        readonly Dictionary<object, PowerNetwork> producerNetwork = new Dictionary<object, PowerNetwork>();
        readonly List<PowerPole> nearby = new List<PowerPole>();
        float farthestSupply;   // the most any pole's supply area reaches from its middle
        // Poles came or went: the wires and networks are worked out again (everything else is kept up to date as it comes and goes).
        bool dirty;

        /// <summary>How bright the day is (0 night to 1 noon): solar panels give that share.</summary>
        public float Daylight = 1f;

        public IReadOnlyList<PowerPole> Poles => poles;
        /// <summary>Goes up each time the wires and networks are worked out again (for redrawing the wires).</summary>
        public int Version { get; private set; }
        public IReadOnlyList<(PowerPole A, PowerPole B)> Wires
        {
            get
            {
                if (dirty) Rebuild();
                return wires;
            }
        }
        public IReadOnlyList<PowerNetwork> Networks
        {
            get
            {
                if (dirty) Rebuild();
                return networks;
            }
        }

        // ------------------------------------------------------------ pieces --

        static long BucketKey(int bx, int by) => ((long)bx << 32) | (uint)by;
        static int BucketOf(float v) => (int)Math.Floor(v / Bucket);

        public void Add(PowerPole pole)
        {
            if (pole == null || poles.Contains(pole)) return;
            poles.Add(pole);
            long key = BucketKey(BucketOf(pole.At.CenterX), BucketOf(pole.At.CenterY));
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<PowerPole>();
            list.Add(pole);
            dirty = true;
        }

        public void Remove(PowerPole pole)
        {
            if (pole == null || !poles.Remove(pole)) return;
            long key = BucketKey(BucketOf(pole.At.CenterX), BucketOf(pole.At.CenterY));
            if (buckets.TryGetValue(key, out var list))
            {
                list.Remove(pole);
                if (list.Count == 0) buckets.Remove(key);
            }
            pole.Network = null;
            dirty = true;
        }

        public void Add(Generator g)
        {
            if (g == null || generators.Contains(g)) return;
            generators.Add(g);
            if (!dirty) Join(g, g.At, n => n.Generators);
        }

        public void Remove(Generator g)
        {
            if (g == null || !generators.Remove(g)) return;
            if (producerNetwork.TryGetValue(g, out var n)) n.Generators.Remove(g);
            producerNetwork.Remove(g);
        }

        public void Add(Accumulator a)
        {
            if (a == null || accumulators.Contains(a)) return;
            accumulators.Add(a);
            if (!dirty) Join(a, a.At, n => n.Accumulators);
        }

        public void Remove(Accumulator a)
        {
            if (a == null || !accumulators.Remove(a)) return;
            if (producerNetwork.TryGetValue(a, out var n)) n.Accumulators.Remove(a);
            producerNetwork.Remove(a);
        }

        public void Add(SolarPanel p)
        {
            if (p == null || panels.Contains(p)) return;
            panels.Add(p);
            if (!dirty) Join(p, p.At, n => n.Panels);
        }

        public void Remove(SolarPanel p)
        {
            if (p == null || !panels.Remove(p)) return;
            if (producerNetwork.TryGetValue(p, out var n)) n.Panels.Remove(p);
            producerNetwork.Remove(p);
        }

        // A generator, accumulator or panel joins the network whose pole powers where it stands (if any).
        void Join<T>(T producer, Footprint at, Func<PowerNetwork, List<T>> listOf)
        {
            var n = FirstPowering(at)?.Network;
            if (n == null) return;
            listOf(n).Add(producer);
            producerNetwork[producer] = n;
        }

        /// <summary>Something electric standing on <paramref name="at"/> (a machine inside a house: its door's cell).</summary>
        public void AddUser(IPowerUser user, Footprint at)
        {
            if (user == null || userIndex.ContainsKey(user)) return;
            var e = new UserEntry { User = user, At = at };
            users.Add(e);
            userIndex[user] = e;
            if (dirty) return;   // (the networks are being worked out again anyway)
            e.Network = FirstPowering(at)?.Network;
            e.Network?.Users.Add(user);
        }

        public void RemoveUser(IPowerUser user)
        {
            if (user == null || !userIndex.TryGetValue(user, out var e)) return;
            userIndex.Remove(user);
            users.Remove(e);
            e.Network?.Users.Remove(user);
            e.Network = null;
        }

        /// <summary>The network powering something electric (null: none reaches it).</summary>
        public PowerNetwork NetworkOf(IPowerUser user)
        {
            if (dirty) Rebuild();
            return user != null && userIndex.TryGetValue(user, out var e) ? e.Network : null;
        }

        /// <summary>The network of a generator, accumulator or solar panel (null: no pole reaches it).</summary>
        public PowerNetwork NetworkOf(object producer)
        {
            if (dirty) Rebuild();
            return producer != null && producerNetwork.TryGetValue(producer, out var n) ? n : null;
        }

        /// <summary>The network whose poles power <paramref name="at"/> (null: none).</summary>
        public PowerNetwork NetworkAt(Footprint at)
        {
            if (dirty) Rebuild();
            return FirstPowering(at)?.Network;
        }

        // The poles whose middles are within <reach> of the box (x0, y0)-(x1, y1), into `nearby`.
        void PolesNear(float x0, float y0, float x1, float y1, float reach)
        {
            nearby.Clear();
            int bx0 = BucketOf(x0 - reach), bx1 = BucketOf(x1 + reach), by0 = BucketOf(y0 - reach), by1 = BucketOf(y1 + reach);
            for (int by = by0; by <= by1; by++)
                for (int bx = bx0; bx <= bx1; bx++)
                    if (buckets.TryGetValue(BucketKey(bx, by), out var list)) nearby.AddRange(list);
        }

        // The pole that powers <at> (the first built, of those that do), or null.
        PowerPole FirstPowering(Footprint at)
        {
            PolesNear(at.X, at.Y, at.X + at.W, at.Y + at.H, farthestSupply);
            PowerPole best = null;
            foreach (var p in nearby)
                if (p.Powers(at) && (best == null || p.Index < best.Index)) best = p;
            return best;
        }

        // ------------------------------------------------------------ wiring --

        static float Distance(PowerPole a, PowerPole b)
        {
            float dx = a.At.CenterX - b.At.CenterX, dy = a.At.CenterY - b.At.CenterY;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // Wires (each pole to its nearest few in reach), networks (the poles wires join), and what each powers.
        void Rebuild()
        {
            dirty = false;
            Version++;
            farthestSupply = 0f;
            for (int i = 0; i < poles.Count; i++)
            {
                var p = poles[i];
                p.Index = i;
                // (a square's corner is half its side times the square root of two from its middle)
                farthestSupply = Math.Max(farthestSupply, p.Round ? p.Supply : p.Supply * 0.7072f);
            }
            wires.Clear();
            var parent = new int[poles.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }
            var seen = new HashSet<long>();
            var near = new List<(float D, PowerPole P)>();
            foreach (var a in poles)
            {
                near.Clear();
                PolesNear(a.At.CenterX, a.At.CenterY, a.At.CenterX, a.At.CenterY, a.WireReach);
                foreach (var b in nearby)
                {
                    if (b == a) continue;
                    float d = Distance(a, b);
                    if (d <= Math.Min(a.WireReach, b.WireReach) + 1e-4f) near.Add((d, b));
                }
                near.Sort((p, q) => p.D != q.D ? p.D.CompareTo(q.D) : p.P.Index.CompareTo(q.P.Index));
                for (int k = 0; k < near.Count && k < MaxWires; k++)
                {
                    var b = near[k].P;
                    int lo = Math.Min(a.Index, b.Index), hi = Math.Max(a.Index, b.Index);
                    if (!seen.Add(((long)lo << 32) | (uint)hi)) continue;
                    wires.Add((poles[lo], poles[hi]));
                    parent[Find(lo)] = Find(hi);
                }
            }
            networks.Clear();
            var byRoot = new Dictionary<int, PowerNetwork>();
            foreach (var p in poles)
            {
                int root = Find(p.Index);
                if (!byRoot.TryGetValue(root, out var n))
                {
                    n = new PowerNetwork();
                    byRoot[root] = n;
                    networks.Add(n);
                }
                p.Network = n;
                n.Poles.Add(p);
            }
            producerNetwork.Clear();
            foreach (var g in generators) Join(g, g.At, n => n.Generators);
            foreach (var a in accumulators) Join(a, a.At, n => n.Accumulators);
            foreach (var s in panels) Join(s, s.At, n => n.Panels);
            foreach (var u in users)
            {
                u.Network = FirstPowering(u.At)?.Network;
                u.Network?.Users.Add(u.User);
            }
        }

        // ------------------------------------------------------------ the tick --

        /// <summary>Power flows for <paramref name="dt"/> seconds: every electric thing gets its share for the next tick.</summary>
        public void Tick(float dt)
        {
            if (dirty) Rebuild();
            if (dt <= 0f) return;
            float daylight = Math.Max(0f, Math.Min(1f, Daylight));
            foreach (var g in generators) g.Output = 0f;
            foreach (var s in panels) s.Output = 0f;
            foreach (var a in accumulators) a.Flow = 0f;
            foreach (var n in networks) Flow(n, dt, daylight);
            foreach (var u in users)
                if (u.Network == null) u.User.SetPower(0f);
        }

        static void Flow(PowerNetwork n, float dt, float daylight)
        {
            float demand = 0f;
            foreach (var u in n.Users) demand += Math.Max(0f, u.PowerDemand());
            float solar = 0f;
            foreach (var s in n.Panels) solar += s.MaxOutput * daylight;
            if (n.Available.Length < n.Generators.Count) n.Available = new float[n.Generators.Count * 2];
            var available = n.Available;
            float genCap = 0f;
            for (int i = 0; i < n.Generators.Count; i++) genCap += available[i] = n.Generators[i].Available(dt);
            float discharge = 0f, charge = 0f, stored = 0f, storeCap = 0f;
            foreach (var a in n.Accumulators)
            {
                discharge += Math.Min(a.Rate, a.Stored / dt);
                charge += Math.Min(a.Rate, Math.Max(0f, a.Capacity - a.Stored) / dt);
                storeCap += a.Capacity;
            }

            // What's wanted: panels first, then the generators, then the accumulators.
            float fromSolar = Math.Min(solar, demand);
            float fromGen = Math.Min(genCap, demand - fromSolar);
            float fromStore = Math.Min(discharge, demand - fromSolar - fromGen);
            float given = fromSolar + fromGen + fromStore;
            // Anything spare charges the accumulators: what the panels have left first, then the generators.
            float spareSolar = solar - fromSolar;
            float charged = Math.Min(charge, spareSolar + (genCap - fromGen));
            float genLoad = fromGen + Math.Max(0f, charged - spareSolar);

            // The generators share the load by what each can give, and burn fuel for it.
            if (genCap > 0f)
                for (int i = 0; i < n.Generators.Count; i++)
                {
                    float share = genLoad * available[i] / genCap;
                    var g = n.Generators[i];
                    g.Output = share;
                    g.Energy = Math.Max(0f, g.Energy - share * dt);
                }
            foreach (var s in n.Panels) s.Output = solar > 0f ? s.MaxOutput * daylight * (fromSolar + Math.Min(charged, spareSolar)) / solar : 0f;
            // Accumulators: out in proportion to what they can give, in in proportion to their room.
            foreach (var a in n.Accumulators)
            {
                float canOut = Math.Min(a.Rate, a.Stored / dt), canIn = Math.Min(a.Rate, Math.Max(0f, a.Capacity - a.Stored) / dt);
                float flow = (discharge > 0f ? -fromStore * canOut / discharge : 0f) + (charge > 0f ? charged * canIn / charge : 0f);
                a.Flow = flow;
                a.Stored = Math.Max(0f, Math.Min(a.Capacity, a.Stored + flow * dt));
                stored += a.Stored;
            }

            float satisfaction = demand > 1e-6f ? Math.Min(1f, given / demand) : (solar + genCap + discharge > 0f ? 1f : 0f);
            foreach (var u in n.Users) u.SetPower(satisfaction);
            n.Demand = demand;
            n.Supplied = given;
            n.Capacity = solar + genCap + discharge;
            n.Satisfaction = satisfaction;
            n.Stored = stored;
            n.StoreCapacity = storeCap;
        }
    }
}
