using System;
using System.Collections.Generic;
using KingdomGame.Economy;

namespace KingdomGame.Automation
{
    /// <summary>The ore in the ground, as drills see it (the game's is Economy/OreMap.cs).</summary>
    public interface IOreGround
    {
        /// <summary>What a drill would get from tile (<paramref name="x"/>, <paramref name="y"/>), and how much is left (false: none).</summary>
        bool OreAt(int x, int y, out ResourceType item, out int left);
        /// <summary>Takes one from the tile; false if there's none left.</summary>
        bool Mine(int x, int y, out ResourceType item);
    }

    /// <summary>
    /// A mining drill (Factorio's): it stands on an ore field and digs the ore under it (and, for the bigger ones, a few
    /// tiles round it), one item every <see cref="SecondsPerItem"/>, a tile at a time in turn, and puts each out in front
    /// of it (the chute): onto a belt, or into a chest, a furnace, or another burner drill (coal as its fuel). With
    /// nowhere to put it, it waits. Burner drills burn wood or coal that arms (or another drill) put in. Plain C#
    /// (unit-tested); Building/Factory.cs runs it.
    /// </summary>
    public sealed class Drill : IItemPort, ITicking, IPowerUser
    {
        public const int MaxFuel = 5;
        // How long it waits before looking again when there's no ore left under it.
        const float LookAgain = 1f;

        readonly FactoryWorld world;
        readonly IOreGround ground;
        readonly List<Spot> area = new List<Spot>();
        int next;
        float idle;
        bool wanting;   // it has ore to dig and room to put it (only power or fuel is missing, if anything)

        /// <summary>Its bottom-left cell, its size (it's square), the way its chute faces, and how many tiles round itself it mines.</summary>
        public readonly Spot At;
        public readonly int Size, Dir, Reach;
        /// <summary>Seconds for each item at full power.</summary>
        public float SecondsPerItem = 4f;
        /// <summary>Burns fuel (no electricity).</summary>
        public bool Burner;
        /// <summary>MW while digging (a burner's fuel).</summary>
        public float EnergyUse = 0.15f;
        /// <summary>A burner's energy left (MJ), and its fuel waiting to burn.</summary>
        public float Energy;
        public int FuelCount { get; private set; }
        public ResourceType FuelType { get; private set; }
        /// <summary>An electric one's share of the power it needs (0-1).</summary>
        public float Power = 1f;
        /// <summary>An item dug out and waiting to go out of the chute.</summary>
        public bool Holding { get; private set; }
        public ResourceType Held { get; private set; }
        /// <summary>Seconds into the item it's digging (at full power).</summary>
        public float Progress { get; private set; }
        /// <summary>Digging right now (for drawing it, and its pollution).</summary>
        public bool Working { get; private set; }
        /// <summary>Why it isn't digging (null: it is).</summary>
        public string Problem { get; private set; }
        /// <summary>The cell in front of its chute, where what it digs goes.</summary>
        public readonly Spot Output;
        /// <summary>The tiles it digs (its own, and <see cref="Reach"/> round them).</summary>
        public IReadOnlyList<Spot> Area => area;
        /// <summary>MW an electric one wants: its use while it digs, a thirtieth of that while it waits.</summary>
        public float PowerDemand() => Burner ? 0f : wanting ? EnergyUse : EnergyUse * PowerGrid.IdleShare;
        public void SetPower(float share) => Power = share;

        public Drill(FactoryWorld world, IOreGround ground, Spot at, int size, int dir, int reach)
        {
            this.world = world;
            this.ground = ground;
            At = at;
            Size = Math.Max(1, size);
            Dir = Dirs.Wrap(dir);
            Reach = Math.Max(0, reach);
            Output = OutputOf(at, Size, Dir);
            for (int y = -Reach; y < Size + Reach; y++)
                for (int x = -Reach; x < Size + Reach; x++)
                    area.Add(new Spot(at.X + x, at.Y + y));
        }

        /// <summary>
        /// The cell in front of a drill's chute. Facing right (0), the chute is on its right side, in the middle row (the
        /// lower of the two middle ones on an even-sized drill); turned, it goes round with the drill (as its picture does).
        /// </summary>
        public static Spot OutputOf(Spot origin, int size, int dir)
        {
            // In half-tiles from the middle, so the turn is exact for even sizes too.
            int dx = 2 * size - (size - 1), dy = 2 * ((size - 1) / 2) - (size - 1);
            for (int i = 0; i < Dirs.Wrap(dir); i++)
            {
                int t = dx;
                dx = -dy;
                dy = t;
            }
            return new Spot(origin.X + (dx + size - 1) / 2, origin.Y + (dy + size - 1) / 2);
        }

        /// <summary>The ore left under it (and in reach), and the kind there's most of.</summary>
        public int OreLeft(out ResourceType most)
        {
            most = ResourceType.IronOre;
            int total = 0, best = 0;
            var counts = new Dictionary<ResourceType, int>();
            foreach (var s in area)
            {
                if (ground == null || !ground.OreAt(s.X, s.Y, out var item, out int left)) continue;
                total += left;
                counts.TryGetValue(item, out int n);
                counts[item] = n + left;
                if (n + left > best)
                {
                    best = n + left;
                    most = item;
                }
            }
            return total;
        }

        /// <summary>Puts back what a saved game says.</summary>
        public void Restore(bool holding, ResourceType held, float progress, ResourceType fuelType, int fuelCount, float energy)
        {
            Holding = holding;
            Held = held;
            Progress = Math.Max(0f, progress);
            FuelType = fuelType;
            FuelCount = Math.Max(0, Math.Min(MaxFuel, fuelCount));
            Energy = energy;
        }

        /// <summary>Empties it into <paramref name="spill"/> (counts by type): what it dug out and its fuel.</summary>
        public void TakeAll(int[] spill)
        {
            if (Holding && spill != null && (int)Held < spill.Length) spill[(int)Held]++;
            Holding = false;
            if (FuelCount > 0 && spill != null && (int)FuelType < spill.Length) spill[(int)FuelType] += FuelCount;
            FuelCount = 0;
        }

        bool FuelFits(ResourceType item) =>
            Burner && world != null && world.Fuels.IsFuel(item) && FuelCount < MaxFuel && (FuelCount == 0 || FuelType == item);

        // Arms (or another drill) put fuel in; arms can take out what it dug, when the chute is blocked.
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
            item = Held;
            return Holding && want(Held);
        }

        public bool Extract(ResourceType item)
        {
            if (!Holding || item != Held) return false;
            Holding = false;
            return true;
        }

        // Out of the chute: onto a belt there (coming in the way the chute faces), or into what's there.
        bool PutOut()
        {
            if (!Holding) return true;
            if (world == null || !world.PutInto(Output, Held, Dir)) return false;
            Holding = false;
            return true;
        }

        // The next tile in turn with ore left (false: none).
        bool FindOre(out Spot spot)
        {
            spot = default;
            if (ground == null) return false;
            for (int k = 0; k < area.Count; k++)
            {
                int i = (next + k) % area.Count;
                if (!ground.OreAt(area[i].X, area[i].Y, out _, out _)) continue;
                next = i;
                spot = area[i];
                return true;
            }
            return false;
        }

        public void Tick(float dt)
        {
            Working = false;
            wanting = false;
            if (Holding && !PutOut())
            {
                Problem = world != null && world.Exists(Output)
                    ? "can't put out what it dug: what's in front is full"
                    : "nothing in front of its chute: a belt, chest, furnace or another drill";
                return;
            }
            if (idle > 0f)
            {
                idle -= dt;
                return;
            }
            if (!FindOre(out var spot))
            {
                Problem = "no ore left under it";
                idle = LookAgain;
                return;
            }
            wanting = true;
            float rate;
            if (Burner)
            {
                if (Energy <= 0f && FuelCount > 0)
                {
                    FuelCount--;
                    Energy += world.Fuels.Energy(FuelType);
                }
                if (Energy <= 0f)
                {
                    Problem = "no fuel: put wood or coal in it";
                    return;
                }
                rate = 1f;
                Energy -= EnergyUse * dt;
            }
            else
            {
                if (Power <= 0f)
                {
                    Problem = "no power";
                    return;
                }
                rate = Math.Min(1f, Power);
            }
            Working = true;
            Problem = null;
            Progress += dt * rate;
            float each = Math.Max(0.05f, SecondsPerItem);
            if (Progress < each) return;
            Progress -= each;
            if (!ground.Mine(spot.X, spot.Y, out var item)) return;
            next = (next + 1) % area.Count;   // (the next tile next time: they go down evenly)
            Held = item;
            Holding = true;
            PutOut();
        }
    }
}
