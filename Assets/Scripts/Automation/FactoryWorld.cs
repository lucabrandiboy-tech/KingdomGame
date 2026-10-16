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

    /// <summary>An item riding a belt: how far along its tile it is (0 at the back edge, 1 at the front edge).</summary>
    public struct BeltItem
    {
        public ResourceType Type;
        public float Pos;
    }

    /// <summary>
    /// One tile of conveyor belt, carrying items in its direction. It hands them to the belt in front (straight on,
    /// round a curve, or onto its side), or at the end of the line into a chest or storehouse it points at.
    /// </summary>
    public sealed class Belt
    {
        public readonly Spot At;
        public readonly int Dir;
        /// <summary>Tiles a second.</summary>
        public float Speed;
        /// <summary>The items on it, from the back (0) to the front (1).</summary>
        public readonly List<BeltItem> Items = new List<BeltItem>(4);
        /// <summary>The belt it hands items on to (null: the end of the line).</summary>
        public Belt Next { get; internal set; }
        /// <summary>A curve: fed only from one side, so items turn the corner (drawn as a curve).</summary>
        public bool Curve { get; internal set; }
        /// <summary>A curve's way in: the direction items travel as they come in.</summary>
        public int CurveFrom { get; internal set; }
        internal bool SideEntry;   // it hands items onto the side of Next (in at its middle)
        internal int Depth;        // how many belts it is from the end of its line (the ends move first)

        public Belt(Spot at, int dir, float speed)
        {
            At = at;
            Dir = Dirs.Wrap(dir);
            Speed = speed;
        }

        /// <summary>Where an item <paramref name="pos"/> along this tile is drawn, in tiles from the tile's middle.</summary>
        public void Place(float pos, out float x, out float y)
        {
            var d = Dirs.Step(Dir);
            if (!Curve)
            {
                x = d.X * (pos - 0.5f);
                y = d.Y * (pos - 0.5f);
                return;
            }
            // A quarter circle from the side it comes in at to the front edge.
            var i = Dirs.Step(CurveFrom);
            double a = Math.Max(0f, Math.Min(1f, pos)) * Math.PI / 2.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            x = (-i.X + d.X) * 0.5f + (-d.X * c + i.X * s) * 0.5f;
            y = (-i.Y + d.Y) * 0.5f + (-d.Y * c + i.Y * s) * 0.5f;
        }
    }

    /// <summary>
    /// An arm (Factorio's inserter): picks an item up from the tile behind it and swings round to drop it on the tile in
    /// front. It takes from belts, chests and machines' finished goods, and gives to belts, chests and machines (only
    /// what a machine needs). Burner arms burn fuel as they move, and take fuel for themselves when they run out.
    /// </summary>
    public sealed class Arm
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
                        if (w.TakeFrom(From, w.Fuels.IsFuel, out var fuel)) Energy += w.Fuels.Energy(fuel);
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
                    if (w.PutInto(To, Held))
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
    /// A kingdom's factory: its belts and the items on them, its arms, its machines, and the chests and machines they
    /// put things into and take things from (ports, one per cell). Plain C# with no Unity code, so it's unit-tested
    /// (tools/tests); Building/Factory.cs runs it in the game and draws it. One lane of items per belt.
    /// </summary>
    public sealed class FactoryWorld
    {
        /// <summary>The space between items on a belt (4 to a tile, packed tight).</summary>
        public const float Gap = 0.25f;

        public readonly FuelTable Fuels;
        readonly Dictionary<Spot, Belt> belts = new Dictionary<Spot, Belt>();
        readonly Dictionary<Spot, IItemPort> ports = new Dictionary<Spot, IItemPort>();
        readonly HashSet<Spot> unloads = new HashSet<Spot>();   // ports belts unload into at the end of a line (chests)
        readonly Dictionary<Spot, Spot> links = new Dictionary<Spot, Spot>();
        readonly List<Belt> order = new List<Belt>();
        readonly List<Arm> arms = new List<Arm>();
        readonly List<ITicking> machines = new List<ITicking>();
        bool dirty;

        public FactoryWorld(FuelTable fuels) => Fuels = fuels ?? new FuelTable(null);

        public IEnumerable<Belt> Belts => belts.Values;
        public int BeltCount => belts.Count;
        public IReadOnlyList<Arm> Arms => arms;
        public Belt BeltAt(Spot s) => belts.TryGetValue(s, out var b) ? b : null;
        public IItemPort PortAt(Spot s) => ports.TryGetValue(s, out var p) ? p : null;
        /// <summary>Whether there's a belt or something to put things into at <paramref name="s"/>.</summary>
        public bool Exists(Spot s) => belts.ContainsKey(s) || ports.ContainsKey(s);

        // -------------------------------------------------------------- pieces --

        public Belt AddBelt(Spot at, int dir, float speed)
        {
            var spill = new List<ResourceType>();
            RemoveBelt(at, spill);
            var b = new Belt(at, dir, speed);
            belts[at] = b;
            dirty = true;
            return b;
        }

        /// <summary>Takes a belt away; the items on it go into <paramref name="spill"/>.</summary>
        public void RemoveBelt(Spot at, List<ResourceType> spill)
        {
            if (!belts.TryGetValue(at, out var b)) return;
            foreach (var it in b.Items) spill?.Add(it.Type);
            b.Items.Clear();
            belts.Remove(at);
            dirty = true;
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

        // ---------------------------------------------------------- the tick --

        /// <summary>Everything moves on by <paramref name="dt"/> seconds: belts (ends of the lines first), arms, machines.</summary>
        public void Tick(float dt)
        {
            if (dirty) Rebuild();
            foreach (var b in order) Move(b, dt);
            for (int i = 0; i < arms.Count; i++) arms[i].Tick(dt, this);
            for (int i = 0; i < machines.Count; i++) machines[i].Tick(dt);
        }

        void Move(Belt belt, float dt)
        {
            var items = belt.Items;
            if (items.Count == 0) return;
            float move = belt.Speed * dt;
            // How far the front item may go: the end of the tile, or a gap short of the first item on the belt in front.
            float cap = 1f;
            var next = belt.Next;
            if (next != null && !belt.SideEntry && next.Items.Count > 0) cap = Math.Min(1f, 1f + next.Items[0].Pos - Gap);
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                float target = it.Pos + move;
                if (target >= cap)
                {
                    if (i == items.Count - 1 && cap >= 1f && HandOver(belt, it.Type, target - 1f))
                    {
                        items.RemoveAt(i);
                        continue;
                    }
                    target = Math.Max(it.Pos, cap);
                }
                it.Pos = target;
                items[i] = it;
                cap = target - Gap;
            }
        }

        // The front item reached the end of its tile: onto the next belt, or into a chest at the end of the line.
        bool HandOver(Belt belt, ResourceType type, float overshoot)
        {
            var next = belt.Next;
            if (next != null)
            {
                if (belt.SideEntry) return Insert(next, type, 0.5f);
                float p = overshoot;
                if (next.Items.Count > 0) p = Math.Min(p, next.Items[0].Pos - Gap);
                if (p < 0f) return false;
                next.Items.Insert(0, new BeltItem { Type = type, Pos = p });
                return true;
            }
            var front = belt.At + Dirs.Step(belt.Dir);
            return unloads.Contains(front) && ports.TryGetValue(front, out var port) && port.Insert(type);
        }

        // An item onto a belt at a point along it, if there's room (Gap from the items either side).
        static bool Insert(Belt b, ResourceType type, float pos)
        {
            var items = b.Items;
            int i = 0;
            while (i < items.Count && items[i].Pos < pos) i++;
            if (i > 0 && pos - items[i - 1].Pos < Gap) return false;
            if (i < items.Count && items[i].Pos - pos < Gap) return false;
            items.Insert(i, new BeltItem { Type = type, Pos = pos });
            return true;
        }

        // Works out which belt feeds which, the curves, and the order they move in.
        void Rebuild()
        {
            dirty = false;
            foreach (var n in belts.Values)
            {
                var behind = BeltAt(n.At - Dirs.Step(n.Dir));
                bool straight = (behind != null && behind.Dir == n.Dir) || LinkedInto(n.At);
                int sides = 0, from = -1;
                for (int k = 1; k <= 3; k += 2)
                {
                    int side = Dirs.Wrap(n.Dir + k);
                    var f = BeltAt(n.At + Dirs.Step(side));
                    if (f != null && f.Dir == Dirs.Opposite(side)) { sides++; from = f.Dir; }
                }
                n.Curve = !straight && sides == 1;
                n.CurveFrom = n.Curve ? from : n.Dir;
            }
            foreach (var b in belts.Values)
            {
                b.Next = null;
                b.SideEntry = false;
                var n = BeltAt(b.At + Dirs.Step(b.Dir));
                if (n == null)
                {
                    if (links.TryGetValue(b.At, out var to)) b.Next = BeltAt(to);
                    continue;
                }
                if (n.Dir == Dirs.Opposite(b.Dir)) continue;   // head on: blocked
                b.Next = n;
                b.SideEntry = n.Dir != b.Dir && !(n.Curve && n.CurveFrom == b.Dir);
            }
            // The ends of the lines first, so items move up into the room made in front of them in the same tick.
            var done = new HashSet<Belt>();
            var onPath = new HashSet<Belt>();
            var path = new List<Belt>();
            foreach (var start in belts.Values)
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
            order.AddRange(belts.Values);
            order.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        }

        bool LinkedInto(Spot s)
        {
            foreach (var l in links) if (l.Value == s) return true;
            return false;
        }

        // ------------------------------------------------- for arms and machines --

        /// <summary>Whether <paramref name="item"/> could go into what's on <paramref name="s"/> (a belt takes anything, when there's room).</summary>
        public bool CanPut(Spot s, ResourceType item) =>
            belts.ContainsKey(s) || (ports.TryGetValue(s, out var p) && p.Accepts(item));

        /// <summary>
        /// Puts an item onto the belt on <paramref name="s"/> (onto its middle, or its start when <paramref name="travel"/>, the direction
        /// it's coming from, is the belt's own) or into what's there. False if there's no room (or the belt points the other way).
        /// </summary>
        public bool PutInto(Spot s, ResourceType item, int travel = -1)
        {
            if (belts.TryGetValue(s, out var b))
            {
                if (travel >= 0 && travel == Dirs.Opposite(b.Dir)) return false;
                if (dirty) Rebuild();
                return Insert(b, item, travel >= 0 && travel == b.Dir ? 0f : 0.5f);
            }
            return ports.TryGetValue(s, out var p) && p.Insert(item);
        }

        /// <summary>Takes an item that <paramref name="want"/> accepts from the belt or port on <paramref name="s"/> (the front-most on a belt).</summary>
        public bool TakeFrom(Spot s, Func<ResourceType, bool> want, out ResourceType item)
        {
            item = default;
            if (belts.TryGetValue(s, out var b))
            {
                for (int i = b.Items.Count - 1; i >= 0; i--)
                {
                    if (!want(b.Items[i].Type)) continue;
                    item = b.Items[i].Type;
                    b.Items.RemoveAt(i);
                    return true;
                }
                return false;
            }
            return ports.TryGetValue(s, out var p) && p.Offer(want, out item) && p.Extract(item);
        }

        /// <summary>
        /// One item out of a machine covering the cells (<paramref name="x"/>, <paramref name="y"/>) to (x + w - 1, y + h - 1): onto a belt
        /// touching it that doesn't point into it, or into a chest touching it. <paramref name="turn"/> goes round the sides
        /// in turn. False if there's nowhere to put it.
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
                bool done = belts.ContainsKey(cell) ? PutInto(cell, item, dir)
                    : unloads.Contains(cell) && ports.TryGetValue(cell, out var p) && p.Insert(item);
                if (!done) continue;
                turn = n + 1;
                return true;
            }
            return false;
        }

        /// <summary>Puts back the items on a belt (a saved game).</summary>
        public void SetItems(Spot at, IEnumerable<BeltItem> items)
        {
            if (!belts.TryGetValue(at, out var b)) return;
            b.Items.Clear();
            foreach (var it in items) b.Items.Add(new BeltItem { Type = it.Type, Pos = Math.Max(0f, Math.Min(1f, it.Pos)) });
            b.Items.Sort((p, q) => p.Pos.CompareTo(q.Pos));
            for (int i = b.Items.Count - 2; i >= 0; i--)
                if (b.Items[i + 1].Pos - b.Items[i].Pos < Gap * 0.99f) b.Items.RemoveAt(i);   // (never closer than a gap)
        }
    }
}
