using System;
using System.Collections.Generic;
using System.Linq;
using KingdomGame.Automation;
using KingdomGame.Economy;

// Tests for electricity (Assets/Scripts/Automation/PowerGrid.cs): poles and wires, supply areas, generators sharing the
// load and burning fuel for what's used, brownouts, accumulators and solar panels.
static partial class Tests
{
    // Something electric for the tests: wants a set amount, remembers what it got.
    sealed class TestUser : IPowerUser
    {
        public float Wants;
        public float Got = -1f;
        public float PowerDemand() => Wants;
        public void SetPower(float share) => Got = share;
    }

    static FuelTable Fuels() => new FuelTable(RecipeBook.DefaultFuels());

    static void RunGrid(PowerGrid g, float seconds)
    {
        const float dt = 1f / 30f;
        for (float t = 0f; t < seconds; t += dt) g.Tick(dt);
    }

    static Footprint Foot(int x, int y, int size = 1) => new Footprint(x, y, size, size);

    // Poles within wire reach of each other make one network; one further away is a network of its own.
    static void PolesWireUpWithinReach()
    {
        var g = new PowerGrid();
        var a = new PowerPole(Foot(0, 0), 7.5f, 5f);
        var b = new PowerPole(Foot(7, 0), 7.5f, 5f);
        var c = new PowerPole(Foot(14, 0), 7.5f, 5f);
        var far = new PowerPole(Foot(30, 0), 7.5f, 5f);
        foreach (var p in new[] { a, b, c, far }) g.Add(p);
        Check(g.Wires.Count == 2, $"two wires: a-b and b-c (got {g.Wires.Count})");
        Check(a.Network == b.Network && b.Network == c.Network, "a, b and c are one network");
        Check(far.Network != a.Network && g.Networks.Count == 2, "the far pole is a network of its own");
        // The shorter reach decides: a big pole (30) can't wire to a small one (7.5) 10 tiles away.
        var g2 = new PowerGrid();
        var big = new PowerPole(Foot(0, 0, 2), 30f, 4f);
        var small = new PowerPole(Foot(11, 0), 7.5f, 5f);
        g2.Add(big);
        g2.Add(small);
        Check(g2.Wires.Count == 0, "a big pole and a small one 10 tiles apart aren't wired (the small one only reaches 7.5)");
        // A pole wires itself to at most its 5 nearest.
        var g3 = new PowerGrid();
        var middle = new PowerPole(Foot(0, 0), 9f, 5f);
        g3.Add(middle);
        foreach (var (x, y) in new[] { (6, 0), (-6, 0), (0, 6), (0, -6), (4, 4), (-4, 4), (4, -4), (-4, -4) }) g3.Add(new PowerPole(Foot(x, y), 9f, 5f));
        int fromMiddle = g3.Wires.Count(w => w.A == middle || w.B == middle);
        Check(fromMiddle >= 5, "the middle one is wired to at least its 5 nearest");
        Check(g3.Networks.Count == 1, "they're all one network");
        // Taking a pole away splits the network.
        g.Remove(b);
        g.Tick(1f / 30f);
        Check(a.Network != c.Network && g.Networks.Count == 3, "without the middle pole, the ends are two networks");
    }

    // A pole powers what's in the square round it, and nothing outside it.
    static void PolesPowerTheirSupplyArea()
    {
        var g = new PowerGrid();
        g.Add(new PowerPole(Foot(0, 0), 7.5f, 5f));   // powers cells -2..2 each way
        var gen = new Generator(Foot(-2, -2), Fuels()) { MaxOutput = 1f };
        for (int i = 0; i < 5; i++) gen.Insert(ResourceType.Coal);
        g.Add(gen);
        var inside = new TestUser { Wants = 0.1f };
        var edge = new TestUser { Wants = 0.1f };
        var outside = new TestUser { Wants = 0.1f };
        var big = new TestUser { Wants = 0.1f };
        g.AddUser(inside, Foot(1, 1));
        g.AddUser(edge, Foot(2, -2));
        g.AddUser(outside, Foot(3, 0));
        g.AddUser(big, Foot(3, 2, 3));   // a 3x3 machine reaching its corner into the area? (3..5: no)
        RunGrid(g, 0.5f);
        Check(inside.Got == 1f && edge.Got == 1f, "things in the supply area get all they want");
        Check(outside.Got == 0f && big.Got == 0f, "things outside it get nothing");
        var touching = new TestUser { Wants = 0.1f };
        g.AddUser(touching, Foot(2, 2, 3));   // its corner cell (2, 2) is in the area
        RunGrid(g, 0.1f);
        Check(touching.Got == 1f, "a machine with one cell in the area is powered");
        // A generator's own wiring: a circle round it.
        var g2 = new PowerGrid();
        var own = new Generator(Foot(0, 0, 2), Fuels()) { MaxOutput = 1f };
        own.Insert(ResourceType.Coal);
        g2.Add(own);
        g2.Add(new PowerPole(own.At, 10f, 10f, true));
        var near = new TestUser { Wants = 0.1f };
        var away = new TestUser { Wants = 0.1f };
        g2.AddUser(near, Foot(8, 1));
        g2.AddUser(away, Foot(12, 1));
        RunGrid(g2, 0.1f);
        Check(near.Got == 1f && away.Got == 0f, "a generator powers what's within its reach");
    }

    // Two generators share the load half and half and burn fuel only for what's used; with too little power everything
    // gets the same share (a brownout); with no fuel there's none.
    static void GeneratorsShareTheLoad()
    {
        var g = new PowerGrid();
        g.Add(new PowerPole(Foot(0, 0), 7.5f, 9f));
        int taken = 0;
        var gens = new List<Generator>();
        for (int i = 0; i < 2; i++)
        {
            var gen = new Generator(Foot(-3 + i * 2, 2), Fuels()) { MaxOutput = 0.9f };
            gen.TakeFromStore = (out ResourceType item) => { item = ResourceType.Coal; taken++; return true; };
            g.Add(gen);
            gens.Add(gen);
        }
        var users = new List<TestUser>();
        for (int i = 0; i < 3; i++)
        {
            var u = new TestUser { Wants = 0.3f };
            users.Add(u);
            g.AddUser(u, Foot(-2 + i, -2));
        }
        RunGrid(g, 40f);
        Check(users.All(u => u.Got == 1f), "0.9 MW wanted of 1.8: everyone gets all of it");
        Check(Math.Abs(gens[0].Output - 0.45f) < 0.01f && Math.Abs(gens[1].Output - 0.45f) < 0.01f, $"each generator gives half ({gens[0].Output:0.00}, {gens[1].Output:0.00} MW)");
        // 0.9 MW for 40 s is 36 MJ: 9 coal at 4 MJ (plus at most one each held ready).
        Check(taken >= 9 && taken <= 11, $"they burnt coal for what was used: {taken} taken for 36 MJ");
        var net = g.NetworkOf(users[0]);
        Check(net != null && Math.Abs(net.Demand - 0.9f) < 0.01f && Math.Abs(net.Capacity - 1.8f) < 0.05f, "the network knows what's wanted and what it could give");

        // Twice as much wanted as there is: everyone gets half.
        foreach (var u in users) u.Wants = 1.2f;
        RunGrid(g, 1f);
        Check(users.All(u => Math.Abs(u.Got - 0.5f) < 0.01f), $"3.6 MW wanted of 1.8: everyone gets half (got {users[0].Got:0.00})");

        // No fuel anywhere: no power, and the generators say why.
        var cold = new PowerGrid();
        cold.Add(new PowerPole(Foot(0, 0), 7.5f, 5f));
        var empty = new Generator(Foot(1, 1), Fuels()) { MaxOutput = 0.9f, TakeFromStore = (out ResourceType item) => { item = default; return false; } };
        cold.Add(empty);
        var waiting = new TestUser { Wants = 0.1f };
        cold.AddUser(waiting, Foot(0, 1));
        RunGrid(cold, 1f);
        Check(waiting.Got == 0f && empty.Problem != null, "no fuel: no power, and the generator says why");
        Check(empty.Insert(ResourceType.Wood) && !empty.Insert(ResourceType.Coal), "an arm can put wood in (then only more wood)");
        Check(Math.Abs(empty.EnergyOf(ResourceType.Wood) - empty.EnergyOf(ResourceType.Coal) / 3f) < 1e-4f, "wood holds a third of coal's energy in a generator");
        RunGrid(cold, 0.2f);
        Check(waiting.Got == 1f, "with the wood in, there's power");
    }

    // A brownout slows electric machines: an assembler at half power takes twice as long.
    static void BrownoutsSlowMachines()
    {
        var w = NewFactory();
        var g = new PowerGrid();
        g.Add(new PowerPole(Foot(0, 0), 7.5f, 9f));
        var gen = new Generator(Foot(3, 3), w.Fuels) { MaxOutput = 0.075f };
        for (int i = 0; i < 5; i++) gen.Insert(ResourceType.Coal);
        g.Add(gen);
        var gears = RecipeBook.Defaults().First(r => r.name == "Gears");
        var a = new Crafter(false, w.Fuels) { Burner = false, EnergyUse = 0.15f, Speed = 1f };
        a.SetRecipe(gears, null);
        for (int i = 0; i < 2; i++) a.Insert(ResourceType.Iron);
        g.AddUser(a, Foot(-1, -1, 3));
        w.Add(a);
        const float dt = 1f / 30f;
        for (float t = 0f; t < 1.5f; t += dt)
        {
            g.Tick(dt);
            w.Tick(dt);
        }
        Check(a.Output == 0, "at half power (0.075 of 0.15 MW) a 1 s gear isn't done in 1.5 s");
        for (float t = 0f; t < 0.7f; t += dt)
        {
            g.Tick(dt);
            w.Tick(dt);
        }
        Check(a.Output == 1, "it's done in about 2 s");
        // Waiting, it draws a thirtieth: the generator can give that in full.
        g.Tick(dt);
        Check(Math.Abs(a.PowerDemand() - 0.15f / 30f) < 1e-5f, "an idle assembler draws a thirtieth of its use");
        Check(Math.Abs(a.Power - 1f) < 1e-4f, "and gets all of it");
    }

    // Solar panels give power by day; spare power charges accumulators, which keep things going at night until they're empty.
    static void AccumulatorsCarryTheNight()
    {
        var g = new PowerGrid();
        g.Add(new PowerPole(Foot(0, 0), 7.5f, 9f));
        var panel = new SolarPanel(Foot(-4, -4, 3)) { MaxOutput = 0.06f };
        var store = new Accumulator(Foot(2, 2, 2)) { Capacity = 5f, Rate = 0.3f };
        g.Add(panel);
        g.Add(store);
        var lamp = new TestUser { Wants = 0.02f };
        g.AddUser(lamp, Foot(0, 0));
        g.Daylight = 1f;
        RunGrid(g, 60f);
        Check(lamp.Got == 1f, "by day the panel powers the lamp");
        Check(Math.Abs(store.Stored - 0.04f * 60f) < 0.1f, $"and its spare 0.04 MW charges the accumulator ({store.Stored:0.00} MJ after a minute)");
        float before = store.Stored;
        g.Daylight = 0f;
        RunGrid(g, 30f);
        Check(lamp.Got == 1f && panel.Output == 0f, "at night the accumulator keeps the lamp lit");
        Check(Math.Abs(before - store.Stored - 0.02f * 30f) < 0.05f, "using up what it stored");
        RunGrid(g, 200f);
        Check(store.Stored <= 0.001f && lamp.Got == 0f, "until it's empty: then the lamp goes out");
        // A full accumulator stops charging, and charging never goes past full.
        g.Daylight = 1f;
        RunGrid(g, 400f);
        Check(Math.Abs(store.Stored - store.Capacity) < 1e-3f, "by day it fills up to its capacity, and no further");
    }
}
