using System;
using System.Collections.Generic;
using System.Linq;
using KingdomGame.Automation;
using KingdomGame.Economy;

// Tests for the factory: belts, arms, furnaces and assemblers (Assets/Scripts/Automation).
static partial class Tests
{
    // A chest for the tests: holds anything, up to a limit.
    sealed class TestChest : IItemPort
    {
        public readonly int[] Count = new int[Inventory.TypeCount];
        public int Capacity = 1000;
        public int Total => Count.Sum();
        public bool Accepts(ResourceType item) => Total < Capacity;
        public bool Insert(ResourceType item)
        {
            if (!Accepts(item)) return false;
            Count[(int)item]++;
            return true;
        }
        public bool Offer(Func<ResourceType, bool> want, out ResourceType item)
        {
            for (int i = 0; i < Count.Length; i++)
                if (Count[i] > 0 && want((ResourceType)i)) { item = (ResourceType)i; return true; }
            item = default;
            return false;
        }
        public bool Extract(ResourceType item)
        {
            if (Count[(int)item] <= 0) return false;
            Count[(int)item]--;
            return true;
        }
    }

    static FactoryWorld NewFactory() => new FactoryWorld(new FuelTable(new[] { new Fuel(ResourceType.Wood, 2f), new Fuel(ResourceType.Coal, 4f) }));

    static void Run(FactoryWorld w, float seconds)
    {
        const float dt = 1f / 30f;
        for (float t = 0f; t < seconds; t += dt) w.Tick(dt);
    }

    static int ItemsOnBelts(FactoryWorld w) => w.Belts.Sum(b => b.Items.Count);

    static void CheckSpacing(FactoryWorld w)
    {
        foreach (var b in w.Belts)
            for (int i = 1; i < b.Items.Count; i++)
                Check(b.Items[i].Pos - b.Items[i - 1].Pos >= FactoryWorld.Gap - 1e-4f, $"items too close on the belt at {b.At}");
    }

    static void BeltsCarryItemsAlongALine()
    {
        var w = NewFactory();
        for (int x = 0; x < 5; x++) w.AddBelt(new Spot(x, 0), 0, 2f);
        Check(w.PutInto(new Spot(0, 0), ResourceType.Iron, 0), "an item goes onto the start of the line");
        Run(w, 2.3f);
        Check(w.BeltAt(new Spot(4, 0)).Items.Count == 1 && w.BeltAt(new Spot(4, 0)).Items[0].Pos < 1f, "after 2.3 s (4.6 tiles at 2 a second) it's on the last belt, not yet at its end");
        Run(w, 0.4f);
        var last = w.BeltAt(new Spot(4, 0));
        Check(last.Items.Count == 1 && Math.Abs(last.Items[0].Pos - 1f) < 1e-4f, "then it waits at the end of the line");
        Check(ItemsOnBelts(w) == 1, "nothing lost or made");
    }

    static void ItemsPackTightAtTheEnd()
    {
        var w = NewFactory();
        for (int x = 0; x < 4; x++) w.AddBelt(new Spot(x, 0), 0, 3f);
        int put = 0;
        for (int i = 0; i < 400; i++)
        {
            if (w.PutInto(new Spot(0, 0), ResourceType.Stone, 0)) put++;
            w.Tick(1f / 30f);
            CheckSpacing(w);
        }
        Check(ItemsOnBelts(w) == put, "every item put on is still there");
        Check(put >= 15 && put <= 17, $"4 tiles hold about 16 items packed tight (got {put})");
        // Across the joins between tiles they're a gap apart too.
        var all = new List<float>();
        for (int x = 0; x < 4; x++) foreach (var it in w.BeltAt(new Spot(x, 0)).Items) all.Add(x + it.Pos);
        all.Sort();
        for (int i = 1; i < all.Count; i++) Check(all[i] - all[i - 1] >= FactoryWorld.Gap - 1e-3f, "a gap between items across tiles");
    }

    static void CurvesTurnTheCorner()
    {
        var w = NewFactory();
        w.AddBelt(new Spot(0, 0), 0, 2f);   // right
        w.AddBelt(new Spot(1, 0), 1, 2f);   // up: fed only from its side, so a curve
        w.AddBelt(new Spot(1, 1), 1, 2f);
        w.PutInto(new Spot(0, 0), ResourceType.Wood, 0);
        Run(w, 0.05f);
        var corner = w.BeltAt(new Spot(1, 0));
        Check(corner.Curve && corner.CurveFrom == 0, "the corner belt is a curve coming in from the left");
        corner.Place(0f, out float x0, out float y0);
        corner.Place(1f, out float x1, out float y1);
        Check(Math.Abs(x0 + 0.5f) < 1e-4f && Math.Abs(y0) < 1e-4f && Math.Abs(x1) < 1e-4f && Math.Abs(y1 - 0.5f) < 1e-4f, "items are drawn from its left edge round to its top edge");
        Run(w, 2f);
        var top = w.BeltAt(new Spot(1, 1));
        Check(top.Items.Count == 1 && top.Items[0].Pos > 0.99f, "the item went round the corner to the end");
    }

    static void SideLoadingMergesTwoLines()
    {
        var w = NewFactory();
        for (int x = 0; x < 4; x++) w.AddBelt(new Spot(x, 0), 0, 2f);   // the main line, going right
        w.AddBelt(new Spot(2, -2), 1, 2f);                              // a side line coming up into (2, 0)
        w.AddBelt(new Spot(2, -1), 1, 2f);
        int put = 0;
        for (int i = 0; i < 60; i++)
        {
            if (w.PutInto(new Spot(0, 0), ResourceType.Iron, 0)) put++;
            if (w.PutInto(new Spot(2, -2), ResourceType.Copper, 1)) put++;
            w.Tick(1f / 30f);
            CheckSpacing(w);
        }
        Run(w, 4f);
        Check(ItemsOnBelts(w) == put, "nothing lost where the lines meet");
        Check(!w.BeltAt(new Spot(2, 0)).Curve, "a belt fed from behind isn't a curve");
        var end = w.BeltAt(new Spot(3, 0));
        Check(end.Items.Any(i => i.Type == ResourceType.Copper), "side-loaded copper got onto the main line");
        Check(w.Belts.SelectMany(b => b.Items).Any(i => i.Type == ResourceType.Iron), "iron is still on the main line");
    }

    static void HeadOnBeltsBlock()
    {
        var w = NewFactory();
        w.AddBelt(new Spot(0, 0), 0, 2f);
        w.AddBelt(new Spot(1, 0), 2, 2f);
        w.PutInto(new Spot(0, 0), ResourceType.Iron, 0);
        Run(w, 3f);
        var a = w.BeltAt(new Spot(0, 0));
        Check(a.Items.Count == 1 && a.Items[0].Pos > 0.99f, "belts pointing at each other: it stops at the end");
        Check(!w.PutInto(new Spot(1, 0), ResourceType.Iron, 0), "nothing goes onto a belt from the way it's pointing");
    }

    static void BeltsUnloadIntoAChest()
    {
        var w = NewFactory();
        var chest = new TestChest();
        w.SetPort(new Spot(3, 0), chest, true);
        for (int x = 0; x < 3; x++) w.AddBelt(new Spot(x, 0), 0, 2f);
        for (int i = 0; i < 90; i++)
        {
            w.PutInto(new Spot(0, 0), ResourceType.Wood, 0);
            w.Tick(1f / 30f);
        }
        Run(w, 3f);
        Check(chest.Count[(int)ResourceType.Wood] > 0 && ItemsOnBelts(w) == 0, "everything ended up in the chest");
        var machine = new TestChest();
        var w2 = NewFactory();
        w2.SetPort(new Spot(1, 0), machine, false);
        w2.AddBelt(new Spot(0, 0), 0, 2f);
        w2.PutInto(new Spot(0, 0), ResourceType.Wood, 0);
        Run(w2, 2f);
        Check(machine.Total == 0, "a belt doesn't unload into a machine (that takes an arm)");
    }

    static void LoopsKeepGoing()
    {
        var w = NewFactory();
        w.AddBelt(new Spot(0, 0), 0, 2f);
        w.AddBelt(new Spot(1, 0), 1, 2f);
        w.AddBelt(new Spot(1, 1), 2, 2f);
        w.AddBelt(new Spot(0, 1), 3, 2f);
        w.PutInto(new Spot(0, 0), ResourceType.Gold, 0);
        w.PutInto(new Spot(1, 1), ResourceType.Gold, 2);
        // Where an item is round the loop, in tiles from the start.
        var loop = new[] { new Spot(0, 0), new Spot(1, 0), new Spot(1, 1), new Spot(0, 1) };
        List<float> Where() => loop.SelectMany((s, i) => w.BeltAt(s).Items.Select(it => i + it.Pos)).OrderBy(v => v).ToList();
        Run(w, 10f);
        Check(ItemsOnBelts(w) == 2, "a loop keeps its items");
        var before = Where();
        Run(w, 0.5f);   // a tile at 2 tiles a second
        var after = Where();
        float moved = 0f;
        foreach (var a in after) moved += before.Min(b => Math.Min(Math.Abs(a - b - 1f), Math.Abs(a - b + 3f)));
        Check(moved < 0.2f, $"both items went a tile further round in half a second (off by {moved:0.00})");
    }

    static void RemovingABeltSpillsItsItems()
    {
        var w = NewFactory();
        w.AddBelt(new Spot(0, 0), 0, 1f);
        w.PutInto(new Spot(0, 0), ResourceType.Iron, 0);
        var spill = new List<ResourceType>();
        w.RemoveBelt(new Spot(0, 0), spill);
        Check(spill.Count == 1 && spill[0] == ResourceType.Iron && w.BeltCount == 0, "the item comes back when the belt goes");
    }

    static void ArmsMoveItemsAtTheirSpeed()
    {
        var w = NewFactory();
        var chest = new TestChest();
        chest.Count[(int)ResourceType.Iron] = 20;
        w.SetPort(new Spot(0, 0), chest, true);
        var arm = new Arm(new Spot(1, 0), 0) { SwingSeconds = 0.5f };   // electric: a round trip a second
        w.Add(arm);
        w.AddBelt(new Spot(2, 0), 1, 2f);
        w.AddBelt(new Spot(2, 1), 1, 2f);
        w.AddBelt(new Spot(2, 2), 1, 2f);
        Run(w, 5.05f);
        int moved = 20 - chest.Count[(int)ResourceType.Iron];
        Check(moved >= 5 && moved <= 6, $"a round trip a second moves about 5 in 5 s (moved {moved})");
        arm.Power = 0f;
        int was = chest.Count[(int)ResourceType.Iron];
        Run(w, 3f);
        Check(chest.Count[(int)ResourceType.Iron] == was, "with no power it stops");
    }

    static Recipe GearRecipe() => Recipe.Make("Gears", ResourceType.Gear, 1, 0.5f, 0, false, new ResourceAmount(ResourceType.Iron, 2));

    static void AssemblersTakeOnlyWhatTheyNeed()
    {
        var w = NewFactory();
        var input = new TestChest();
        input.Count[(int)ResourceType.Iron] = 10;
        input.Count[(int)ResourceType.Wood] = 10;
        var output = new TestChest();
        var assembler = new Crafter(false, w.Fuels);
        assembler.SetRecipe(GearRecipe(), null);
        // chest (0,0) -> arm (1,0) -> assembler (2..3, 0..1) -> arm (4,0) -> chest (5,0)
        w.SetPort(new Spot(0, 0), input, true);
        for (int x = 2; x <= 3; x++) for (int y = 0; y <= 1; y++) w.SetPort(new Spot(x, y), assembler, false);
        w.Add(assembler);
        w.Add(new Arm(new Spot(1, 0), 0) { SwingSeconds = 0.25f });
        w.Add(new Arm(new Spot(4, 0), 0) { SwingSeconds = 0.25f });
        w.SetPort(new Spot(5, 0), output, true);
        Run(w, 10f);
        Check(input.Count[(int)ResourceType.Wood] == 10, "the arm never took wood: the assembler doesn't need it");
        Check(input.Count[(int)ResourceType.Iron] == 0, "all the iron went in");
        Check(output.Count[(int)ResourceType.Gear] == 5, $"10 iron made 5 gears, taken out to the chest (got {output.Count[(int)ResourceType.Gear]})");
        Check(assembler.Input(ResourceType.Iron) == 0 && assembler.Output == 0, "nothing left inside");
    }

    static void AssemblersHoldTwoBatches()
    {
        var w = NewFactory();
        var a = new Crafter(false, w.Fuels);
        a.SetRecipe(GearRecipe(), null);
        int took = 0;
        while (a.Insert(ResourceType.Iron)) took++;
        Check(took == 4, $"it takes ingredients for two batches (took {took})");
        Check(!a.Accepts(ResourceType.Wood), "and nothing it doesn't use");
        var spill = new int[Inventory.TypeCount];
        a.SetRecipe(Recipe.Make("Wire", ResourceType.Wire, 2, 0.5f, 0, false, new ResourceAmount(ResourceType.Copper, 1)), spill);
        Check(spill[(int)ResourceType.Iron] == 4 && a.Input(ResourceType.Iron) == 0, "changing the recipe hands back what was in it");
    }

    static void FurnacesSmeltWhatGoesIn()
    {
        var w = NewFactory();
        var furnace = new Crafter(true, w.Fuels) { Burner = true, EnergyUse = 0.09f };
        furnace.FurnaceRecipes = new[]
        {
            Recipe.Make("Iron", ResourceType.Iron, 1, 3.2f, 0, true, new ResourceAmount(ResourceType.IronOre, 1)),
            Recipe.Make("Copper", ResourceType.Copper, 1, 3.2f, 0, true, new ResourceAmount(ResourceType.CopperOre, 1)),
        };
        Check(!furnace.Accepts(ResourceType.Gold), "it doesn't take what it can't smelt");
        Check(furnace.Insert(ResourceType.IronOre) && furnace.Insert(ResourceType.IronOre), "it takes iron ore");
        Check(!furnace.Accepts(ResourceType.CopperOre), "but no copper ore while it's smelting iron");
        Run(w, 4f);
        furnace.Tick(1f / 30f);
        Check(furnace.Output == 0, "no fuel: nothing smelted");
        Check(furnace.Insert(ResourceType.Coal), "coal goes in as fuel");
        for (int i = 0; i < 30 * 7; i++) furnace.Tick(1f / 30f);
        Check(furnace.Output == 2 && furnace.OutputType == ResourceType.Iron, $"two iron ores made two iron (got {furnace.Output} {furnace.OutputType})");
        furnace.Extract(ResourceType.Iron);
        furnace.Extract(ResourceType.Iron);
        Check(furnace.Insert(ResourceType.CopperOre), "empty again: it takes copper ore");
        for (int i = 0; i < 30 * 4; i++) furnace.Tick(1f / 30f);
        Check(furnace.Output == 1 && furnace.OutputType == ResourceType.Copper, "and makes copper");
        Check(furnace.Energy < 4f, "it burnt some of the coal");
    }

    static void BurnerArmsFuelThemselves()
    {
        var w = NewFactory();
        var from = new TestChest();
        from.Count[(int)ResourceType.Coal] = 1;
        from.Count[(int)ResourceType.Stone] = 5;
        var to = new TestChest();
        w.SetPort(new Spot(0, 0), from, true);
        w.SetPort(new Spot(2, 0), to, true);
        var arm = new Arm(new Spot(1, 0), 0) { Burner = true, SwingSeconds = 0.5f, EnergyUse = 0.1f };
        w.Add(arm);
        Run(w, 8f);
        Check(from.Count[(int)ResourceType.Coal] == 0 && to.Count[(int)ResourceType.Coal] == 0, "it burnt the coal itself");
        Check(to.Count[(int)ResourceType.Stone] == 5, "then moved the stone");
        var lazy = NewFactory();
        var only = new TestChest();
        only.Count[(int)ResourceType.Stone] = 5;
        lazy.SetPort(new Spot(0, 0), only, true);
        lazy.SetPort(new Spot(2, 0), new TestChest(), true);
        var cold = new Arm(new Spot(1, 0), 0) { Burner = true };
        lazy.Add(cold);
        Run(lazy, 5f);
        Check(only.Count[(int)ResourceType.Stone] == 5 && cold.Problem != null, "no fuel anywhere: it doesn't move, and says why");
    }

    static void MachinesPushOntoBeltsGoingAway()
    {
        var w = NewFactory();
        w.AddBelt(new Spot(2, 0), 0, 2f);    // to the right of a 2x2 machine at (0..1, 0..1), going away from it
        w.AddBelt(new Spot(-1, 0), 0, 2f);   // to its left, pointing into it
        int turn = 0;
        Check(w.PushOut(0, 0, 2, 2, ResourceType.Wood, ref turn), "it puts the wood on the belt going away");
        Check(w.BeltAt(new Spot(2, 0)).Items.Count == 1 && w.BeltAt(new Spot(-1, 0)).Items.Count == 0, "not on the belt pointing into it");
        Check(w.BeltAt(new Spot(2, 0)).Items[0].Pos < 0.01f, "onto the start of the belt");
        Check(!w.PushOut(0, 0, 2, 2, ResourceType.Wood, ref turn), "no room right at the start: it waits");
    }
}
