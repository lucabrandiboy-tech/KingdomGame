using System;
using System.Collections.Generic;
using KingdomGame.Economy;

namespace KingdomGame.Automation
{
    /// <summary>Something arms (and belt ends) put items into or take items out of: a chest or storehouse, a machine.</summary>
    public interface IItemPort
    {
        /// <summary>Whether it would take one <paramref name="item"/> now.</summary>
        bool Accepts(ResourceType item);
        /// <summary>Takes one <paramref name="item"/>; false if it can't.</summary>
        bool Insert(ResourceType item);
        /// <summary>An item it could give that <paramref name="want"/> accepts (false: none).</summary>
        bool Offer(Func<ResourceType, bool> want, out ResourceType item);
        /// <summary>Gives one <paramref name="item"/>; false if it can't.</summary>
        bool Extract(ResourceType item);
    }

    /// <summary>A machine that works a little every tick (a furnace, an assembler, a drill).</summary>
    public interface ITicking
    {
        void Tick(float dt);
    }

    /// <summary>An item riding a lane of a belt: how far along its tile it is (0 at the back edge, 1 at the front edge).</summary>
    public struct BeltItem
    {
        public ResourceType Type;
        public float Pos;
    }

    /// <summary>The kinds of belt piece.</summary>
    public enum BeltKind
    {
        /// <summary>A plain belt: straight, or a curve when it's fed only from one side.</summary>
        Belt,
        /// <summary>An underground belt: an entrance (things go under the ground) or an exit (they come back up), see <see cref="Belt.Exit"/>.</summary>
        Underground,
        /// <summary>One of a splitter's two halves: what comes in on either is shared out between both.</summary>
        Splitter,
        /// <summary>A stretch under the ground between an underground entrance and its exit (nobody sees or touches it).</summary>
        Tunnel,
    }

    /// <summary>
    /// One tile of conveyor belt, carrying items in its direction on two lanes, left and right (as in Factorio). It hands
    /// them on to the belt in front: straight on or round a curve on the same lane, or onto the near lane of a belt it
    /// meets side on; or at the end of the line into a chest or storehouse it points at. Underground belts and
    /// splitters are belts too (<see cref="Kind"/>).
    /// </summary>
    public sealed class Belt
    {
        /// <summary>How far each lane is from the middle of the belt, in tiles.</summary>
        public const float LaneOffset = 0.22f;

        public readonly Spot At;
        public readonly int Dir;
        public readonly BeltKind Kind;
        /// <summary>Tiles a second.</summary>
        public float Speed;
        /// <summary>Underground belts pair only with others of the same tier (the same belt kind).</summary>
        public int Tier;
        /// <summary>An underground belt: how far ahead of an entrance its exit may be, in tiles.</summary>
        public int Reach = 5;
        /// <summary>The two lanes, left (0) and right (1) of the way it goes, each with its items from the back (0) to the front (1).</summary>
        public readonly List<BeltItem>[] Lanes = { new List<BeltItem>(4), new List<BeltItem>(4) };
        /// <summary>The belt it hands items on to (null: the end of the line).</summary>
        public Belt Next { get; internal set; }
        /// <summary>A curve: fed only from one side, so items turn the corner (drawn as a curve).</summary>
        public bool Curve { get; internal set; }
        /// <summary>A curve's way in: the direction items travel as they come in.</summary>
        public int CurveFrom { get; internal set; }
        /// <summary>An underground belt that's an exit (things come up out of it), not an entrance.</summary>
        public bool Exit { get; internal set; }
        /// <summary>An underground belt's other end (null: an entrance with no exit, where things wait at its mouth).</summary>
        public Belt Pair { get; internal set; }
        /// <summary>A splitter half: the other half.</summary>
        public Belt Partner { get; internal set; }
        /// <summary>A splitter half: 0 the left one (of the way it goes), 1 the right one.</summary>
        public int Side { get; internal set; }
        internal Belt Prev;           // the belt feeding it from behind (straight on, round a curve or through a wall)
        internal bool SideEntry;      // it hands items onto the side of Next (in at its middle)...
        internal int SideLane;        // ...onto this lane of it, the near one
        internal int Depth;           // how many belts it is from the end of its line (the ends move first)
        internal List<Belt> Tunnel;   // an underground entrance: the stretch under the ground to its exit
        internal int[] Turn;          // a splitter: for each lane, the half the next item goes on along (both halves share it)

        public Belt(Spot at, int dir, float speed) : this(at, dir, speed, BeltKind.Belt) { }

        internal Belt(Spot at, int dir, float speed, BeltKind kind)
        {
            At = at;
            Dir = Dirs.Wrap(dir);
            Speed = speed;
            Kind = kind;
        }

        /// <summary>How many items are on it (both lanes).</summary>
        public int Count => Lanes[0].Count + Lanes[1].Count;

        /// <summary>Where an item <paramref name="pos"/> along lane <paramref name="lane"/> is drawn, in tiles from the tile's middle.</summary>
        public void Place(float pos, int lane, out float x, out float y)
        {
            var d = Dirs.Step(Dir);
            float side = lane == 0 ? LaneOffset : -LaneOffset;   // (to the left of the way it goes, or to the right)
            if (!Curve)
            {
                var l = Dirs.Step(Dir + 1);
                x = d.X * (pos - 0.5f) + l.X * side;
                y = d.Y * (pos - 0.5f) + l.Y * side;
                return;
            }
            // A quarter circle round the inside corner, from the side it comes in at to the front edge: the left lane is
            // the inner one on a left turn, the outer one on a right turn.
            var i = Dirs.Step(CurveFrom);
            bool leftTurn = Dir == Dirs.Wrap(CurveFrom + 1);
            float r = 0.5f - (leftTurn ? side : -side);
            double a = Math.Max(0f, Math.Min(1f, pos)) * Math.PI / 2.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            x = (-i.X + d.X) * 0.5f + (-d.X * c + i.X * s) * r;
            y = (-i.Y + d.Y) * 0.5f + (-d.Y * c + i.Y * s) * r;
        }

        /// <summary>Whether an item <paramref name="pos"/> along it is in sight (not under an underground belt's hood or a splitter's housing).</summary>
        public bool Shows(float pos)
        {
            switch (Kind)
            {
                case BeltKind.Underground: return Exit ? pos >= 0.5f : pos <= 0.5f;
                case BeltKind.Splitter: return pos <= 0.3f || pos >= 0.7f;
                case BeltKind.Tunnel: return false;
                default: return true;
            }
        }

        // Where something put on from the side lands: the middle of the part you can see.
        internal float Middle => Kind == BeltKind.Underground ? (Exit ? 0.75f : 0.25f) : Kind == BeltKind.Splitter ? 0.15f : 0.5f;

        // The lane on the far side from something coming in going <travel> (an arm drops there), or on the near side (a
        // drill's or machine's chute); the right lane when it comes along the belt (or from nowhere in particular).
        internal int LaneFrom(int travel, bool far)
        {
            if (travel < 0) return 1;
            var t = Dirs.Step(travel);
            Place(0.5f, 0, out float x0, out float y0);
            Place(0.5f, 1, out float x1, out float y1);
            float further = (x0 - x1) * t.X + (y0 - y1) * t.Y;   // how much further on the left lane is, going that way
            if (Math.Abs(further) < 1e-3f) return 1;
            return (further > 0f) == far ? 0 : 1;
        }

        // It hands items on out of its front (an underground entrance takes them under the ground instead).
        internal bool GoesOn => !(Kind == BeltKind.Underground && !Exit);
    }

    /// <summary>
    /// An arm (Factorio's inserter): picks an item up from the tile behind it and swings round to drop it on the tile in
    /// front. It takes from belts (either lane), chests and machines' finished goods, and gives to belts (onto the far
    /// lane), chests and machines (only what a machine needs). Burner arms burn fuel as they move, and take fuel for
    /// themselves when they run out.
    /// </summary>
    public sealed class Arm : IPowerUser
    {
        public readonly Spot At;
        public readonly int Dir;
        /// <summary>Seconds to swing from the pickup to the drop (and as long back).</summary>
        public float SwingSeconds = 0.6f;
        /// <summary>Burns fuel (no electricity).</summary>
        public bool Burner;
        /// <summary>MW while moving (a burner's fuel).</summary>
        public float EnergyUse = 0.1f;
        /// <summary>A burner's energy left (MJ). It may run a little below 0 to finish a swing.</summary>
        public float Energy;
        /// <summary>An electric arm's share of the power it needs (0-1).</summary>
        public float Power = 1f;
        /// <summary>It moves only this item (a filter), when set.</summary>
        public bool HasFilter;
        public ResourceType Filter;
        public bool Holding { get; private set; }
        public ResourceType Held { get; private set; }
        /// <summary>0 over the pickup, 1 over the drop.</summary>
        public float Angle { get; private set; }
        /// <summary>Why it's standing still (null: it isn't, or it's just waiting for something to pick up).</summary>
        public string Problem { get; private set; }
        int state;   // 0 over the pickup, 1 swinging to the drop, 2 over the drop, 3 swinging back

        public Arm(Spot at, int dir)
        {
            At = at;
            Dir = Dirs.Wrap(dir);
        }

        /// <summary>MW an electric arm wants: its use while it swings, a thirtieth of that while it waits.</summary>
        public float PowerDemand() => Burner ? 0f : state == 1 || state == 3 ? EnergyUse : EnergyUse * PowerGrid.IdleShare;
        public void SetPower(float share) => Power = share;

        public Spot From => At - Dirs.Step(Dir);
        public Spot To => At + Dirs.Step(Dir);
        public int State => state;

        /// <summary>Puts back what a saved game says.</summary>
        public void Restore(bool holding, ResourceType held, float angle, int swingState, float energy)
        {
            Holding = holding;
            Held = held;
            Angle = Math.Max(0f, Math.Min(1f, angle));
            state = holding ? (swingState == 1 || swingState == 2 ? swingState : 1) : (swingState == 3 ? 3 : 0);
            Energy = energy;
        }

        /// <summary>What it's holding is taken away (it was torn down): true if it held something.</summary>
        public bool Drop(out ResourceType item)
        {
            item = Held;
            bool had = Holding;
            Holding = false;
            return had;
        }

        internal void Tick(float dt, FactoryWorld w)
        {
            switch (state)
            {
                case 0:
                {
                    if (Burner && Energy <= 0f)
                    {
                        // Out of fuel: it takes some for itself from where it picks up.
                        if (w.TakeFrom(From, w.IsFuel, out var fuel)) Energy += w.Fuels.Energy(fuel);
                        else
                        {
                            Problem = "out of fuel: put wood or coal where it picks up";
                            return;
                        }
                    }
                    if (!Burner && Power <= 0f)
                    {
                        Problem = "no power";
                        return;
                    }
                    if (w.TakeFrom(From, Wants(w), out var item))
                    {
                        Held = item;
                        Holding = true;
                        state = 1;
                        Problem = null;
                    }
                    else Problem = w.Exists(To) ? null : "nothing in front to drop into";
                    return;
                }
                case 1:
                    if (Swing(dt, +1f)) state = 2;
                    return;
                case 2:
                    if (w.PutInto(To, Held, Dir))
                    {
                        Holding = false;
                        state = 3;
                        Problem = null;
                    }
                    else Problem = "can't drop: full or doesn't need it";
                    return;
                default:
                    if (Swing(dt, -1f)) state = 0;
                    return;
            }
        }

        Func<ResourceType, bool> wants;
        FactoryWorld wantsWorld;

        // What it would pick up: what its filter allows and what's in front would take.
        Func<ResourceType, bool> Wants(FactoryWorld w)
        {
            if (wants == null || wantsWorld != w)
            {
                wantsWorld = w;
                wants = t => (!HasFilter || t == Filter) && w.CanPut(To, t);
            }
            return wants;
        }

        // Moves round; true when it gets there.
        bool Swing(float dt, float sign)
        {
            float rate = Burner ? 1f : Math.Max(0f, Math.Min(1f, Power));
            if (rate <= 0f)
            {
                Problem = "no power";
                return false;
            }
            Angle += sign * dt * rate / Math.Max(0.05f, SwingSeconds);
            if (Burner) Energy -= EnergyUse * dt;
            if (sign > 0f && Angle >= 1f) { Angle = 1f; return true; }
            if (sign < 0f && Angle <= 0f) { Angle = 0f; return true; }
            return false;
        }
    }

    /// <summary>
    /// A kingdom's factory: its belts (two lanes each, with underground belts and splitters) and the items on them, its
    /// arms, its machines, and the chests and machines they put things into and take things from (ports, one per cell).
    /// Plain C# with no Unity code, so it's unit-tested (tools/tests); Building/Factory.cs runs it in the game and draws it.
    /// </summary>
    public sealed class FactoryWorld
    {
        /// <summary>The space between items on a lane (4 to a tile, packed tight).</summary>
        public const float Gap = 0.25f;

        public readonly FuelTable Fuels;
        /// <summary>Whether something burns (the same delegate every time, so asking makes no garbage).</summary>
        public readonly Func<ResourceType, bool> IsFuel;
        readonly Dictionary<Spot, Belt> belts = new Dictionary<Spot, Belt>();
        readonly Dictionary<Spot, IItemPort> ports = new Dictionary<Spot, IItemPort>();
        readonly HashSet<Spot> unloads = new HashSet<Spot>();   // ports belts unload into at the end of a line (chests)
        readonly Dictionary<Spot, Spot> links = new Dictionary<Spot, Spot>();
        readonly List<Belt> every = new List<Belt>();   // the belts and the stretches under the ground
        readonly List<Belt> order = new List<Belt>();   // all of them, the ends of the lines first
        readonly List<Arm> arms = new List<Arm>();
        readonly List<ITicking> machines = new List<ITicking>();
        readonly List<(Spot At, ResourceType Item)> lost = new List<(Spot, ResourceType)>();
        readonly List<Belt> unders = new List<Belt>();
        readonly HashSet<Belt> done = new HashSet<Belt>(), onPath = new HashSet<Belt>();
        readonly List<Belt> path = new List<Belt>();
        bool dirty;

        public FactoryWorld(FuelTable fuels)
        {
            Fuels = fuels ?? new FuelTable(null);
            IsFuel = Fuels.IsFuel;
        }

        /// <summary>Every belt (a collection a foreach goes through without making garbage: it's drawn every frame).</summary>
        public Dictionary<Spot, Belt>.ValueCollection Belts => belts.Values;
        public int BeltCount => belts.Count;
        public IReadOnlyList<Arm> Arms => arms;
        public Belt BeltAt(Spot s) => belts.TryGetValue(s, out var b) ? b : null;
        public IItemPort PortAt(Spot s) => ports.TryGetValue(s, out var p) ? p : null;
        /// <summary>Whether there's a belt or something to put things into at <paramref name="s"/>.</summary>
        public bool Exists(Spot s) => belts.ContainsKey(s) || ports.ContainsKey(s);

        // -------------------------------------------------------------- pieces --

        public Belt AddBelt(Spot at, int dir, float speed) => AddPiece(at, dir, speed, BeltKind.Belt);

        /// <summary>
        /// An underground belt. In each line of them of the same <paramref name="tier"/> facing the same way they pair up from
        /// the back: the first is an entrance, the next within its <paramref name="reach"/> tiles its exit, and so on.
        /// </summary>
        public Belt AddUnderground(Spot at, int dir, float speed, int tier, int reach)
        {
            var b = AddPiece(at, dir, speed, BeltKind.Underground);
            b.Tier = tier;
            b.Reach = Math.Max(1, reach);
            return b;
        }

        /// <summary>
        /// A splitter facing <paramref name="dir"/>: two halves side by side across the way it goes, on <paramref name="at"/> and the
        /// cell above it (facing right or left) or to its right (facing up or down). Returns the half on <paramref name="at"/>.
        /// </summary>
        public Belt AddSplitter(Spot at, int dir, float speed)
        {
            dir = Dirs.Wrap(dir);
            var other = at + (dir % 2 == 0 ? new Spot(0, 1) : new Spot(1, 0));
            var a = AddPiece(at, dir, speed, BeltKind.Splitter);
            var b = AddPiece(other, dir, speed, BeltKind.Splitter);
            var left = Dirs.Step(dir + 1);
            a.Side = (other.X - at.X) * left.X + (other.Y - at.Y) * left.Y > 0 ? 1 : 0;   // (the other one on its left: it's the right half)
            b.Side = 1 - a.Side;
            a.Partner = b;
            b.Partner = a;
            a.Turn = b.Turn = new int[2];
            return a;
        }

        Belt AddPiece(Spot at, int dir, float speed, BeltKind kind)
        {
            var spill = new List<ResourceType>();
            RemoveBelt(at, spill);
            foreach (var t in spill) lost.Add((at, t));
            var b = new Belt(at, dir, speed, kind);
            belts[at] = b;
            dirty = true;
            return b;
        }

        /// <summary>
        /// Takes a belt away (a splitter: both its halves); the items on it, and under the ground past it when it's an
        /// underground belt with a pair, go into <paramref name="spill"/>.
        /// </summary>
        public void RemoveBelt(Spot at, List<ResourceType> spill)
        {
            if (!belts.TryGetValue(at, out var b)) return;
            Take(b, spill);
            belts.Remove(at);
            if (b.Partner != null && belts.TryGetValue(b.Partner.At, out var other) && other == b.Partner)
            {
                Take(other, spill);
                belts.Remove(other.At);
                other.Partner = null;
            }
            b.Partner = null;
            // Underground: the stretch to its exit (or from its entrance) goes with it.
            var entrance = b.Kind == BeltKind.Underground && b.Exit ? b.Pair : b;
            if (entrance != null && entrance.Tunnel != null)
            {
                foreach (var t in entrance.Tunnel) Take(t, spill);
                entrance.Tunnel = null;
            }
            if (b.Pair != null) b.Pair.Pair = null;
            b.Pair = null;
            dirty = true;
        }

        static void Take(Belt b, List<ResourceType> spill)
        {
            foreach (var lane in b.Lanes)
            {
                if (spill != null) foreach (var it in lane) spill.Add(it.Type);
                lane.Clear();
            }
        }

        /// <summary>A chest, storehouse or machine on cell <paramref name="s"/> (<paramref name="beltsUnload"/>: belts ending at it unload into it).</summary>
        public void SetPort(Spot s, IItemPort port, bool beltsUnload)
        {
            ports[s] = port;
            if (beltsUnload) unloads.Add(s);
            else unloads.Remove(s);
        }

        public void ClearPort(Spot s, IItemPort port)
        {
            if (ports.TryGetValue(s, out var p) && p == port)
            {
                ports.Remove(s);
                unloads.Remove(s);
            }
        }

        /// <summary>A belt on <paramref name="from"/> with nothing in front hands its items to the belt on <paramref name="to"/> (through a house's wall).</summary>
        public void Link(Spot from, Spot to)
        {
            if (links.TryGetValue(from, out var old) && old == to) return;
            links[from] = to;
            dirty = true;
        }

        public void ClearLinks()
        {
            if (links.Count == 0) return;
            links.Clear();
            dirty = true;
        }

        /// <summary>All the links at once (houses' belt holes); nothing changes (or is worked out again) if they're the same.</summary>
        public void SetLinks(IReadOnlyList<(Spot From, Spot To)> all)
        {
            bool same = all.Count == links.Count;
            if (same)
                foreach (var l in all)
                    if (!links.TryGetValue(l.From, out var to) || to != l.To) { same = false; break; }
            if (same) return;
            links.Clear();
            foreach (var l in all) links[l.From] = l.To;
            dirty = true;
        }

        public void Add(Arm arm)
        {
            if (arm != null && !arms.Contains(arm)) arms.Add(arm);
        }

        public void Remove(Arm arm, List<ResourceType> spill)
        {
            if (arm == null) return;
            if (arm.Drop(out var held)) spill?.Add(held);
            arms.Remove(arm);
        }

        public void Add(ITicking machine)
        {
            if (machine != null && !machines.Contains(machine)) machines.Add(machine);
        }

        public void Remove(ITicking machine) => machines.Remove(machine);

        /// <summary>
        /// Items that fell off the belts by themselves (under the ground between an underground pair that was broken up by
        /// building or taking away another one in its line, or on a belt built over), each with where: the game gives them
        /// back. Returns how many there were.
        /// </summary>
        public int TakeLost(List<(Spot At, ResourceType Item)> into)
        {
            int n = lost.Count;
            if (n == 0) return 0;
            into?.AddRange(lost);
            lost.Clear();
            return n;
        }

        // ---------------------------------------------------------- the tick --

        /// <summary>Everything moves on by <paramref name="dt"/> seconds: belts (ends of the lines first), arms, machines.</summary>
        public void Tick(float dt)
        {
            if (dirty) Rebuild();
            for (int i = 0; i < order.Count; i++)
            {
                var b = order[i];
                float move = b.Speed * dt;
                MoveLane(b, 0, move);
                MoveLane(b, 1, move);
            }
            for (int i = 0; i < arms.Count; i++) arms[i].Tick(dt, this);
            for (int i = 0; i < machines.Count; i++) machines[i].Tick(dt);
        }

        void MoveLane(Belt belt, int lane, float move)
        {
            var items = belt.Lanes[lane];
            if (items.Count == 0) return;
            float cap = Cap(belt, lane);
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                float target = it.Pos + move;
                // A splitter shares things out between its halves in their middle (under its housing).
                if (belt.Partner != null && it.Pos < 0.5f && target >= 0.5f && Split(belt, lane, i, it.Pos, ref target, cap)) continue;
                if (target >= cap)
                {
                    if (i == items.Count - 1 && cap >= 1f && HandOver(belt, lane, it.Type, target - 1f, out float after))
                    {
                        items.RemoveAt(i);
                        cap = after;   // (the next one stays a gap behind it, wherever it went)
                        continue;
                    }
                    target = Math.Max(it.Pos, cap);
                }
                it.Pos = target;
                items[i] = it;
                cap = target - Gap;
            }
        }

        // How far the front item on a lane may go: the end of the tile, or a gap short of the first item on that lane of
        // the belt in front. An underground entrance with no exit: its mouth, where things wait.
        static float Cap(Belt belt, int lane)
        {
            if (belt.Kind == BeltKind.Underground && !belt.Exit && belt.Pair == null) return 0.5f;
            var next = belt.Next;
            if (next == null || belt.SideEntry) return 1f;
            var ahead = next.Lanes[lane];
            return ahead.Count > 0 ? Math.Min(1f, 1f + ahead[0].Pos - Gap) : 1f;
        }

        // An item on a splitter half reaching its middle goes on along the half whose turn it is (and the next one along
        // the other), or along the other when that one's blocked. A half whose front leads nowhere (no belt or chest) gets
        // things only when the other doesn't lead anywhere either. True if it moved across to the other half; if it may
        // go neither way, <target> is held short of the middle.
        bool Split(Belt half, int lane, int i, float pos, ref float target, float cap)
        {
            var other = half.Partner;
            bool leads = Leads(half), otherLeads = Leads(other);
            bool go = cap >= 0.5f && (leads || !otherLeads);   // (it may go on past the middle of its own half)
            var turn = half.Turn;
            if ((turn[lane] != half.Side || !go) && (otherLeads || !leads))
            {
                var items = half.Lanes[lane];
                if (Insert(other, lane, items[i].Type, target))
                {
                    items.RemoveAt(i);
                    turn[lane] = half.Side;
                    return true;
                }
            }
            if (go) turn[lane] = other.Side;
            else target = Math.Max(pos, Math.Min(target, 0.4999f));
            return false;
        }

        // Whether a belt's front leads somewhere: a belt, or a chest it unloads into.
        bool Leads(Belt b) => b.Next != null || unloads.Contains(b.At + Dirs.Step(b.Dir));

        // The front item of a lane reached the end of its tile: onto the belt in front (on the same lane straight on, round
        // a curve, through a wall or under the ground; onto its near lane from the side), or at the end of the line into a
        // chest it points at. <after>: how far the next item on the lane may go now.
        bool HandOver(Belt belt, int lane, ResourceType type, float overshoot, out float after)
        {
            after = 1f;
            var next = belt.Next;
            if (next != null)
            {
                if (belt.SideEntry) return Insert(next, belt.SideLane, type, next.Middle);
                var ahead = next.Lanes[lane];
                float p = overshoot;
                if (ahead.Count > 0) p = Math.Min(p, ahead[0].Pos - Gap);
                if (p < 0f) return false;
                ahead.Insert(0, new BeltItem { Type = type, Pos = p });
                after = Math.Min(1f, 1f + p - Gap);
                return true;
            }
            if (!belt.GoesOn) return false;
            var front = belt.At + Dirs.Step(belt.Dir);
            return unloads.Contains(front) && ports.TryGetValue(front, out var port) && port.Insert(type);
        }

        // An item onto a lane of a belt at a point along it, if there's room: a gap from the items either side of it, and
        // (near its back edge) from the front item on that lane of the belt feeding it from behind.
        static bool Insert(Belt b, int lane, ResourceType type, float pos)
        {
            var items = b.Lanes[lane];
            int i = 0;
            while (i < items.Count && items[i].Pos < pos) i++;
            if (i > 0 && pos - items[i - 1].Pos < Gap) return false;
            if (i < items.Count && items[i].Pos - pos < Gap) return false;
            if (i == 0 && pos < Gap && b.Prev != null)
            {
                var behind = b.Prev.Lanes[lane];
                if (behind.Count > 0 && pos + 1f - behind[behind.Count - 1].Pos < Gap) return false;
            }
            items.Insert(i, new BeltItem { Type = type, Pos = pos });
            return true;
        }

        // Works out the underground pairs, the curves, which belt feeds which, and the order they move in.
        void Rebuild()
        {
            dirty = false;
            PairUndergrounds();
            // Curves: a plain belt fed only from one side (nothing behind it going its way) turns the corner.
            foreach (var n in belts.Values)
            {
                n.Curve = false;
                n.CurveFrom = n.Dir;
                if (n.Kind != BeltKind.Belt) continue;
                var behind = BeltAt(n.At - Dirs.Step(n.Dir));
                bool straight = (behind != null && behind.Dir == n.Dir && behind.GoesOn) || LinkedInto(n.At);
                int sides = 0, from = -1;
                for (int k = 1; k <= 3; k += 2)
                {
                    int side = Dirs.Wrap(n.Dir + k);
                    var f = BeltAt(n.At + Dirs.Step(side));
                    if (f != null && f.Dir == Dirs.Opposite(side) && f.GoesOn) { sides++; from = f.Dir; }
                }
                n.Curve = !straight && sides == 1;
                n.CurveFrom = n.Curve ? from : n.Dir;
            }
            every.Clear();
            foreach (var b in belts.Values)
            {
                every.Add(b);
                b.Next = null;
                b.Prev = null;
                b.SideEntry = false;
                b.SideLane = 0;
                if (!b.GoesOn)
                {
                    // An underground entrance: under the ground to its exit (none: things wait at its mouth).
                    if (b.Pair != null) b.Next = b.Tunnel != null && b.Tunnel.Count > 0 ? b.Tunnel[0] : b.Pair;
                    continue;
                }
                var n = BeltAt(b.At + Dirs.Step(b.Dir));
                if (n == null)
                {
                    if (links.TryGetValue(b.At, out var to)) b.Next = BeltAt(to);
                    continue;
                }
                if (n.Dir == Dirs.Opposite(b.Dir)) continue;   // head on: blocked
                if (n.Dir == b.Dir)
                {
                    if (n.Kind == BeltKind.Underground && n.Exit) continue;   // (an exit's back is closed)
                    b.Next = n;
                }
                else if (n.Curve && n.CurveFrom == b.Dir) b.Next = n;
                else if (n.Kind != BeltKind.Splitter)   // (a splitter's sides are closed)
                {
                    b.Next = n;
                    b.SideEntry = true;
                    b.SideLane = Dirs.Opposite(b.Dir) == Dirs.Wrap(n.Dir + 1) ? 0 : 1;
                }
            }
            foreach (var e in belts.Values)
            {
                if (e.Tunnel == null) continue;
                for (int i = 0; i < e.Tunnel.Count; i++)
                {
                    var t = e.Tunnel[i];
                    t.Next = i + 1 < e.Tunnel.Count ? e.Tunnel[i + 1] : e.Pair;
                    t.Prev = null;
                    every.Add(t);
                }
            }
            foreach (var b in every)
                if (b.Next != null && !b.SideEntry && b.Next.Prev == null) b.Next.Prev = b;
            // The ends of the lines first, so items move up into the room made in front of them in the same tick.
            done.Clear();
            foreach (var start in every)
            {
                if (done.Contains(start)) continue;
                path.Clear();
                onPath.Clear();
                var cur = start;
                while (cur != null && !done.Contains(cur) && onPath.Add(cur))
                {
                    path.Add(cur);
                    cur = cur.Next;
                }
                int depth = cur != null && done.Contains(cur) ? cur.Depth : -1;   // (a loop starts again at 0)
                for (int i = path.Count - 1; i >= 0; i--)
                {
                    path[i].Depth = ++depth;
                    done.Add(path[i]);
                }
            }
            order.Clear();
            order.AddRange(every);
            order.Sort(byDepth);
        }

        static readonly Comparison<Belt> byDepth = (a, b) => a.Depth.CompareTo(b.Depth);

        bool LinkedInto(Spot s)
        {
            foreach (var l in links) if (l.Value == s) return true;
            return false;
        }

        // How far along its way an underground belt is, and which line (row or column) it's in.
        static int Along(Belt u) => u.Dir == 0 ? u.At.X : u.Dir == 1 ? u.At.Y : u.Dir == 2 ? -u.At.X : -u.At.Y;
        static int Line(Belt u) => u.Dir % 2 == 0 ? u.At.Y : u.At.X;
        static bool SameLine(Belt a, Belt b) => a.Tier == b.Tier && a.Dir == b.Dir && Line(a) == Line(b);

        static int ByLine(Belt a, Belt b)
        {
            int c = a.Tier.CompareTo(b.Tier);
            if (c == 0) c = a.Dir.CompareTo(b.Dir);
            if (c == 0) c = Line(a).CompareTo(Line(b));
            return c != 0 ? c : Along(a).CompareTo(Along(b));
        }

        static readonly Comparison<Belt> byLine = ByLine;

        // Each line of underground belts (same tier, facing the same way) pairs up from its back end: an entrance, the
        // next one within its reach is its exit, the one after that an entrance again, and so on. The stretch under the
        // ground between a pair keeps its items while the pair stays the same; if it changes, they're lost (given back).
        void PairUndergrounds()
        {
            unders.Clear();
            foreach (var u in belts.Values) if (u.Kind == BeltKind.Underground) unders.Add(u);
            if (unders.Count == 0) return;
            unders.Sort(byLine);
            Belt open = null;
            foreach (var u in unders)
            {
                if (open != null && SameLine(open, u) && Along(u) - Along(open) <= open.Reach)
                {
                    SetPair(open, u);
                    open = null;
                    continue;
                }
                if (open != null) SetPair(open, null);
                open = u;
            }
            if (open != null) SetPair(open, null);
        }

        void SetPair(Belt entrance, Belt exit)
        {
            entrance.Exit = false;
            int gap = exit != null ? Along(exit) - Along(entrance) - 1 : -1;
            bool same = exit != null && entrance.Pair == exit && entrance.Tunnel != null && entrance.Tunnel.Count == gap;
            if (!same)
            {
                LoseTunnel(entrance);
                if (exit != null)
                {
                    LoseTunnel(exit);   // (it was an entrance before)
                    var step = Dirs.Step(entrance.Dir);
                    entrance.Tunnel = new List<Belt>(gap);
                    for (int k = 1; k <= gap; k++)
                        entrance.Tunnel.Add(new Belt(new Spot(entrance.At.X + step.X * k, entrance.At.Y + step.Y * k), entrance.Dir, entrance.Speed, BeltKind.Tunnel)
                            { Tier = entrance.Tier });
                }
            }
            entrance.Pair = exit;
            if (exit == null) return;
            exit.Exit = true;
            exit.Pair = entrance;
        }

        void LoseTunnel(Belt entrance)
        {
            if (entrance.Tunnel == null) return;
            foreach (var t in entrance.Tunnel)
                foreach (var lane in t.Lanes)
                {
                    foreach (var it in lane) lost.Add((entrance.At, it.Type));
                    lane.Clear();
                }
            entrance.Tunnel = null;
        }

        // ------------------------------------------------- for arms and machines --

        /// <summary>Whether <paramref name="item"/> could go into what's on <paramref name="s"/> (a belt takes anything, when there's room).</summary>
        public bool CanPut(Spot s, ResourceType item) =>
            belts.ContainsKey(s) || (ports.TryGetValue(s, out var p) && p.Accepts(item));

        /// <summary>
        /// Puts an item onto the belt on <paramref name="s"/>, coming in going <paramref name="travel"/> (-1: from nowhere in
        /// particular), or into what's there. An arm drops it on the belt's far lane, onto its middle (its start when it
        /// comes from behind a straight belt). A <paramref name="chute"/> (a drill or machine putting out what it made)
        /// drops it on the near lane, and not onto a belt pointing back into it. False if there's no room.
        /// </summary>
        public bool PutInto(Spot s, ResourceType item, int travel = -1, bool chute = false)
        {
            if (belts.TryGetValue(s, out var b))
            {
                if (chute && travel >= 0 && travel == Dirs.Opposite(b.Dir)) return false;
                if (dirty) Rebuild();
                int lane = b.LaneFrom(travel, !chute);
                float pos = travel == b.Dir && b.Kind == BeltKind.Belt && !b.Curve ? 0f : b.Middle;
                return Insert(b, lane, item, pos);
            }
            return ports.TryGetValue(s, out var p) && p.Insert(item);
        }

        /// <summary>
        /// Takes an item that <paramref name="want"/> accepts from the belt or port on <paramref name="s"/> (on a belt the front-most
        /// in sight, on either lane).
        /// </summary>
        public bool TakeFrom(Spot s, Func<ResourceType, bool> want, out ResourceType item)
        {
            item = default;
            if (belts.TryGetValue(s, out var b))
            {
                int bestLane = -1, best = -1;
                float bestPos = -1f;
                for (int lane = 0; lane < 2; lane++)
                {
                    var items = b.Lanes[lane];
                    for (int i = items.Count - 1; i >= 0; i--)
                    {
                        if (items[i].Pos <= bestPos) break;   // (the rest of this lane is further back)
                        if (!b.Shows(items[i].Pos) || !want(items[i].Type)) continue;
                        bestLane = lane;
                        best = i;
                        bestPos = items[i].Pos;
                        break;
                    }
                }
                if (best < 0) return false;
                item = b.Lanes[bestLane][best].Type;
                b.Lanes[bestLane].RemoveAt(best);
                return true;
            }
            return ports.TryGetValue(s, out var p) && p.Offer(want, out item) && p.Extract(item);
        }

        /// <summary>
        /// One item out of a machine covering the cells (<paramref name="x"/>, <paramref name="y"/>) to (x + w - 1, y + h - 1): onto the
        /// near lane of a belt touching it that doesn't point into it, or into a chest touching it. <paramref name="turn"/> goes
        /// round the sides in turn. False if there's nowhere to put it.
        /// </summary>
        public bool PushOut(int x, int y, int w, int h, ResourceType item, ref int turn)
        {
            int around = 2 * (w + h);
            for (int k = 0; k < around; k++)
            {
                int n = (turn + k) % around;
                Spot cell;
                int dir;
                if (n < w) { cell = new Spot(x + n, y + h); dir = 1; }
                else if (n < 2 * w) { cell = new Spot(x + n - w, y - 1); dir = 3; }
                else if (n < 2 * w + h) { cell = new Spot(x + w, y + n - 2 * w); dir = 0; }
                else { cell = new Spot(x - 1, y + n - 2 * w - h); dir = 2; }
                bool done = belts.ContainsKey(cell) ? PutInto(cell, item, dir, true)
                    : unloads.Contains(cell) && ports.TryGetValue(cell, out var p) && p.Insert(item);
                if (!done) continue;
                turn = n + 1;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ saving --

        /// <summary>
        /// Every item on the belts, for a save: the cell of the belt it's on, its lane, how many tiles under the ground past
        /// that belt it is (0: on it; 1 and on: between an underground entrance and its exit), and the item.
        /// </summary>
        public void SaveItems(List<(Spot At, int Lane, int Under, BeltItem Item)> into)
        {
            if (dirty) Rebuild();
            foreach (var b in belts.Values)
            {
                for (int lane = 0; lane < 2; lane++)
                    foreach (var it in b.Lanes[lane]) into.Add((b.At, lane, 0, it));
                if (b.Tunnel == null) continue;
                for (int k = 0; k < b.Tunnel.Count; k++)
                    for (int lane = 0; lane < 2; lane++)
                        foreach (var it in b.Tunnel[k].Lanes[lane]) into.Add((b.At, lane, k + 1, it));
            }
        }

        /// <summary>Puts back an item from a saved game (see <see cref="SaveItems"/>). False if it can't go there: no such belt, or no room.</summary>
        public bool LoadItem(Spot at, int lane, int under, BeltItem item)
        {
            if (!belts.TryGetValue(at, out var b) || lane < 0 || lane > 1) return false;
            if (dirty) Rebuild();
            var on = b;
            if (under > 0)
            {
                if (b.Tunnel == null || under > b.Tunnel.Count) return false;
                on = b.Tunnel[under - 1];
            }
            var items = on.Lanes[lane];
            float pos = Math.Max(0f, Math.Min(1f, item.Pos));
            int i = 0;
            while (i < items.Count && items[i].Pos < pos) i++;
            // (never closer than a gap, a little less for rounding)
            if (i > 0 && pos - items[i - 1].Pos < Gap * 0.99f) return false;
            if (i < items.Count && items[i].Pos - pos < Gap * 0.99f) return false;
            items.Insert(i, new BeltItem { Type = item.Type, Pos = pos });
            return true;
        }
    }
}
