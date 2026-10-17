using System;
using System.Collections.Generic;
using System.Linq;
using KingdomGame.Automation;
using KingdomGame.Economy;

// Tests for the belts' two lanes, underground belts and splitters (Assets/Scripts/Automation/FactoryWorld.cs).
static partial class Tests
{
    static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;

    // Where each lane leaves a belt is where it comes onto the next one (round curves too): the lanes join up.
    static void CheckJoinsMeet(FactoryWorld w)
    {
        foreach (var b in w.Belts)
        {
            var n = b.Next;
            if (n == null || b.SideEntry || n.Kind == BeltKind.Tunnel || !w.Belts.Contains(n)) continue;
            for (int lane = 0; lane < 2; lane++)
            {
                b.Place(1f, lane, out float x0, out float y0);
                n.Place(0f, lane, out float x1, out float y1);
                Check(Math.Abs(b.At.X + x0 - n.At.X - x1) < 1e-4f && Math.Abs(b.At.Y + y0 - n.At.Y - y1) < 1e-4f,
                    $"lane {lane} leaves {b.At} where it comes onto {n.At}");
            }
        }
    }

    // Arms drop onto a belt's far lane: one below a line going right fills its left lane, one above it its right lane.
    // A machine's chute drops onto the near lane.
    static void ArmsFillTheFarLane()
    {
        var w = NewFactory();
        for (int x = 0; x < 6; x++) w.AddBelt(new Spot(x, 0), 0, 2f);
        var below = new TestChest();
        below.Count[(int)ResourceType.Iron] = 50;
        var above = new TestChest();
        above.Count[(int)ResourceType.Copper] = 50;
        w.SetPort(new Spot(1, -2), below, true);
        w.SetPort(new Spot(3, 2), above, true);
        w.Add(new Arm(new Spot(1, -1), 1) { SwingSeconds = 0.3f });   // picks up below, drops up onto the belt
        w.Add(new Arm(new Spot(3, 1), 3) { SwingSeconds = 0.3f });    // picks up above, drops down onto it
        for (int i = 0; i < 30 * 12; i++)
        {
            w.Tick(1f / 30f);
            CheckSpacing(w);
        }
        var left = w.Belts.SelectMany(b => b.Lanes[0]).ToList();
        var right = w.Belts.SelectMany(b => b.Lanes[1]).ToList();
        Check(left.Count > 10 && left.All(i => i.Type == ResourceType.Iron), $"the arm below filled the left lane (its far side) with iron ({left.Count})");
        Check(right.Count > 10 && right.All(i => i.Type == ResourceType.Copper), $"the arm above filled the right lane with copper ({right.Count})");
        int held = w.Arms.Count(a => a.Holding);
        Check(ItemsOnBelts(w) + below.Total + above.Total + held == 100, "nothing lost or made");

        var m = NewFactory();
        for (int x = 0; x < 6; x++) m.AddBelt(new Spot(x, 0), 0, 2f);
        int turn = 0;
        Check(m.PushOut(2, 1, 1, 1, ResourceType.Stone, ref turn), "a machine above puts out onto the belt below it");
        turn = 0;
        Check(m.PushOut(4, -1, 1, 1, ResourceType.Wood, ref turn), "and one below onto the belt above it");
        Check(m.BeltAt(new Spot(2, 0)).Lanes[0].Count == 1 && m.BeltAt(new Spot(4, 0)).Lanes[1].Count == 1, "each onto the lane nearer it");
    }

    static void CurvesKeepTheirLanes()
    {
        var w = NewFactory();
        // Right, round a left turn up, round a right turn right again, then on.
        w.AddBelt(new Spot(0, 0), 0, 2f);
        w.AddBelt(new Spot(1, 0), 1, 2f);
        w.AddBelt(new Spot(1, 1), 0, 2f);
        w.AddBelt(new Spot(2, 1), 0, 2f);
        w.AddBelt(new Spot(3, 1), 0, 2f);
        for (int i = 0; i < 30 * 6; i++)
        {
            w.PutInto(new Spot(0, 0), ResourceType.Iron, 1);     // from below: onto the far (left) lane
            w.PutInto(new Spot(0, 0), ResourceType.Copper, 3);   // from above: onto the right lane
            w.Tick(1f / 30f);
            CheckSpacing(w);
        }
        var a = w.BeltAt(new Spot(1, 0));
        var b = w.BeltAt(new Spot(1, 1));
        Check(a.Curve && a.CurveFrom == 0 && b.Curve && b.CurveFrom == 1, "two curves, a left turn then a right turn");
        CheckJoinsMeet(w);
        var end = w.BeltAt(new Spot(3, 1));
        Check(end.Lanes[0].Count >= 3 && end.Lanes[0].All(i => i.Type == ResourceType.Iron), "the iron came round both corners on the left lane");
        Check(end.Lanes[1].Count >= 3 && end.Lanes[1].All(i => i.Type == ResourceType.Copper), "and the copper on the right lane");
        // Packed tight round the corners: the inner lane holds as many as the outer one, a gap apart.
        Check(a.Lanes[0].Count == 4 && a.Lanes[1].Count == 4, $"a full curve holds 4 on each lane ({a.Lanes[0].Count}, {a.Lanes[1].Count})");
    }

    // A fast belt running into a slow one: the items it hands over still keep a gap from the ones following (they used
    // to pile up across the join).
    static void FastIntoSlowKeepsAGap()
    {
        var w = NewFactory();
        for (int x = 0; x < 3; x++) w.AddBelt(new Spot(x, 0), 0, 4f);
        for (int x = 3; x < 6; x++) w.AddBelt(new Spot(x, 0), 0, 1f);
        var chest = new TestChest();
        w.SetPort(new Spot(6, 0), chest, true);
        int put = 0;
        for (int i = 0; i < 30 * 20; i++)
        {
            if (i == 30 * 10) w.ClearPort(new Spot(6, 0), chest);   // then the end blocks and it all backs up
            if (w.PutInto(new Spot(0, 0), ResourceType.Iron, 0)) put++;
            w.Tick(1f / 30f);
            CheckSpacing(w);
        }
        Check(chest.Total > 10 && ItemsOnBelts(w) + chest.Total == put, $"it flowed, then filled up, losing nothing ({chest.Total} through)");
        Check(ItemsOnBelts(w) >= 23, $"6 tiles packed tight on a lane hold about 24 ({ItemsOnBelts(w)})");
    }

    // Something put on a curve from behind lands on its middle: its start is on its side, where things come round from.
    static void ArmsDropOnACurvesMiddle()
    {
        var w = NewFactory();
        w.AddBelt(new Spot(0, 0), 0, 2f);
        w.AddBelt(new Spot(1, 0), 1, 2f);   // a curve, fed from its left
        Check(w.PutInto(new Spot(1, 0), ResourceType.Iron, 1), "an arm below it puts an item on");
        var c = w.BeltAt(new Spot(1, 0));
        var it = ItemsOn(c).Single();
        Check(c.Curve && Near(it.Pos, 0.5f), $"onto the middle of the curve (at {it.Pos})");
    }

    static void ArmsTakeTheFrontMostItem()
    {
        var w = NewFactory();
        w.AddBelt(new Spot(0, 0), 0, 2f);
        Check(w.LoadItem(new Spot(0, 0), 0, 0, new BeltItem { Type = ResourceType.Iron, Pos = 0.3f })
              && w.LoadItem(new Spot(0, 0), 0, 0, new BeltItem { Type = ResourceType.Gold, Pos = 0.8f })
              && w.LoadItem(new Spot(0, 0), 1, 0, new BeltItem { Type = ResourceType.Copper, Pos = 0.6f }), "three items put on");
        Check(!w.LoadItem(new Spot(0, 0), 1, 0, new BeltItem { Type = ResourceType.Stone, Pos = 0.7f }), "never closer than a gap to another");
        Check(w.TakeFrom(new Spot(0, 0), t => t == ResourceType.Iron, out var only) && only == ResourceType.Iron, "a filter takes what it wants, wherever it is");
        Check(w.TakeFrom(new Spot(0, 0), t => true, out var first) && first == ResourceType.Gold, "otherwise the front-most on either lane");
        Check(w.TakeFrom(new Spot(0, 0), t => true, out var second) && second == ResourceType.Copper, "then the next");
        Check(w.BeltAt(new Spot(0, 0)).Count == 0, "and none are left");
    }

    // An underground belt carries things under whatever is built over the gap, here a belt crossing it.
    static void UndergroundBeltsGoUnderneath()
    {
        var w = NewFactory();
        var chest = new TestChest();
        w.AddBelt(new Spot(0, 0), 0, 2f);
        var entrance = w.AddUnderground(new Spot(1, 0), 0, 2f, 4, 5);
        var exit = w.AddUnderground(new Spot(5, 0), 0, 2f, 4, 5);
        w.AddBelt(new Spot(6, 0), 0, 2f);
        w.SetPort(new Spot(7, 0), chest, true);
        for (int y = -2; y <= 2; y++) w.AddBelt(new Spot(3, y), 1, 2f);   // a line going up over the gap
        var crossing = new TestChest();
        w.SetPort(new Spot(3, 3), crossing, true);
        int put = 0, crossed = 0;
        bool seenUnder = false, seenHidden = false;
        var all = new List<(Spot At, int Lane, int Under, BeltItem Item)>();
        for (int i = 0; i < 30 * 14; i++)
        {
            if (i < 30 * 6 && w.PutInto(new Spot(0, 0), ResourceType.Iron, 0)) put++;
            if (i < 30 * 6 && w.PutInto(new Spot(3, -2), ResourceType.Stone, 1)) crossed++;
            w.Tick(1f / 30f);
            CheckSpacing(w);
            all.Clear();
            w.SaveItems(all);
            seenUnder |= all.Any(e => e.Under > 0);
            seenHidden |= ItemsOn(entrance).Any(e => !entrance.Shows(e.Pos)) || ItemsOn(exit).Any(e => !exit.Shows(e.Pos));
            Check(all.Count + chest.Total + crossing.Total == put + crossed, "nothing lost or made");
        }
        Check(entrance.Pair == exit && exit.Pair == entrance && exit.Exit && !entrance.Exit, "the first is the entrance, paired with the exit");
        Check(seenUnder && seenHidden, "things went under the ground, out of sight");
        Check(put > 20 && chest.Count[(int)ResourceType.Iron] == put, $"all the iron came up the other side into the chest ({put})");
        Check(crossed > 20 && crossing.Count[(int)ResourceType.Stone] == crossed && chest.Count[(int)ResourceType.Stone] == 0, "the line over it kept its own");
    }

    static void UndergroundsPairInTurn()
    {
        var w = NewFactory();
        var u = new[] { 0, 3, 6, 9 }.Select(x => w.AddUnderground(new Spot(x, 0), 0, 2f, 4, 5)).ToArray();
        // Too far apart (6 tiles, reach 5); another kind; facing the other way.
        var far0 = w.AddUnderground(new Spot(0, 2), 0, 2f, 4, 5);
        var far1 = w.AddUnderground(new Spot(6, 2), 0, 2f, 4, 5);
        var kind0 = w.AddUnderground(new Spot(0, 4), 0, 2f, 4, 5);
        var kind1 = w.AddUnderground(new Spot(3, 4), 0, 2f, 5, 7);
        var way0 = w.AddUnderground(new Spot(0, 6), 0, 2f, 4, 5);
        var way1 = w.AddUnderground(new Spot(3, 6), 2, 2f, 4, 5);
        // Woven: a fast pair inside a plain pair, going up.
        var outer0 = w.AddUnderground(new Spot(20, 0), 1, 2f, 4, 5);
        var inner0 = w.AddUnderground(new Spot(20, 1), 1, 2f, 5, 7);
        var inner1 = w.AddUnderground(new Spot(20, 3), 1, 2f, 5, 7);
        var outer1 = w.AddUnderground(new Spot(20, 4), 1, 2f, 4, 5);
        w.Tick(0f);
        Check(!u[0].Exit && u[1].Exit && !u[2].Exit && u[3].Exit && u[0].Pair == u[1] && u[2].Pair == u[3], "in a line they pair up in turn: in, out, in, out");
        Check(far0.Pair == null && far1.Pair == null && !far1.Exit, "too far apart: two entrances");
        Check(kind0.Pair == null && kind1.Pair == null, "different kinds don't pair");
        Check(way0.Pair == null && way1.Pair == null, "facing different ways don't pair");
        Check(outer0.Pair == outer1 && inner0.Pair == inner1, "one kind weaves inside another");
        // An entrance with no exit: things wait at its mouth, in sight.
        var v = NewFactory();
        v.AddBelt(new Spot(0, 0), 0, 2f);
        var lonely = v.AddUnderground(new Spot(1, 0), 0, 2f, 4, 5);
        v.PutInto(new Spot(0, 0), ResourceType.Iron, 0);
        Run(v, 3f);
        Check(lonely.Count == 1 && Near(lonely.Lanes[1][0].Pos, 0.5f) && lonely.Shows(0.5f), "it waits at the mouth of an entrance with no exit");
        Check(lonely.Next == null && v.BeltAt(new Spot(0, 0)).Next == lonely, "the belt behind still feeds it");
    }

    // Taking away an underground belt gives back what was under the ground; building another one in the line breaks
    // the pair up, and what was under the ground comes back as lost (the game gives it back).
    static void CuttingAnUndergroundGivesItsItemsBack()
    {
        var w = NewFactory();
        w.AddBelt(new Spot(0, 0), 0, 1f);
        w.AddUnderground(new Spot(1, 0), 0, 1f, 4, 5);
        w.AddUnderground(new Spot(6, 0), 0, 1f, 4, 5);   // 4 tiles under the ground; its exit leads nowhere, so it backs up
        int put = 0;
        for (int i = 0; i < 30 * 30; i++)
        {
            if (w.PutInto(new Spot(0, 0), ResourceType.Iron, 0)) put++;
            w.Tick(1f / 30f);
            CheckSpacing(w);
        }
        var all = new List<(Spot At, int Lane, int Under, BeltItem Item)>();
        w.SaveItems(all);
        int under = all.Count(e => e.Under > 0), onExit = w.BeltAt(new Spot(6, 0)).Count;
        Check(under == 16, $"the 4 tiles under the ground filled up, 4 to a tile ({under})");
        var spill = new List<ResourceType>();
        w.RemoveBelt(new Spot(6, 0), spill);
        Check(spill.Count == under + onExit, $"taking the exit away gave back what was on it and under the ground ({spill.Count})");
        Check(ItemsOnBelts(w) + spill.Count == put, "nothing lost");

        // An exit again, filled up; then another one built in the gap: 1 pairs with 3, and 6 has no pair.
        w.AddUnderground(new Spot(6, 0), 0, 1f, 4, 5);
        for (int i = 0; i < 30 * 30; i++)
        {
            if (w.PutInto(new Spot(0, 0), ResourceType.Iron, 0)) put++;
            w.Tick(1f / 30f);
        }
        int before = ItemsOnBelts(w);
        var middle = w.AddUnderground(new Spot(3, 0), 0, 1f, 4, 5);
        w.Tick(0f);
        var lost = new List<(Spot At, ResourceType Item)>();
        int n = w.TakeLost(lost);
        Check(n == 16 && lost.All(l => l.At == new Spot(1, 0)), $"the cut stretch's items are handed back, from the entrance ({n})");
        Check(ItemsOnBelts(w) + n == before, "none lost");
        Check(w.BeltAt(new Spot(1, 0)).Pair == middle && middle.Exit && w.BeltAt(new Spot(6, 0)).Pair == null, "the pairs changed");
        Check(w.TakeLost(null) == 0, "and they're only handed back once");
    }

    // What's on the belts and under the ground is saved and put back where it was.
    static void BeltItemsSaveAndLoad()
    {
        FactoryWorld Build()
        {
            var w = NewFactory();
            w.AddBelt(new Spot(0, 0), 0, 2f);
            w.AddUnderground(new Spot(1, 0), 0, 2f, 4, 5);
            w.AddUnderground(new Spot(4, 0), 0, 2f, 4, 5);
            w.AddSplitter(new Spot(5, 0), 0, 2f);
            w.AddBelt(new Spot(6, 0), 0, 2f);
            w.AddBelt(new Spot(6, 1), 0, 2f);
            return w;
        }
        var a = Build();
        for (int i = 0; i < 30 * 8; i++)
        {
            a.PutInto(new Spot(0, 0), ResourceType.Iron, 1);
            a.PutInto(new Spot(0, 0), ResourceType.Copper, 3);
            a.Tick(1f / 30f);
        }
        var saved = new List<(Spot At, int Lane, int Under, BeltItem Item)>();
        a.SaveItems(saved);
        Check(saved.Any(e => e.Under > 0) && saved.Any(e => e.Lane == 0) && saved.Any(e => e.Lane == 1), "things on both lanes and under the ground");
        var b = Build();
        Check(saved.All(e => b.LoadItem(e.At, e.Lane, e.Under, e.Item)), "every item goes back");
        var again = new List<(Spot At, int Lane, int Under, BeltItem Item)>();
        b.SaveItems(again);
        string Key((Spot At, int Lane, int Under, BeltItem Item) e) => $"{e.At} {e.Lane} {e.Under} {e.Item.Type} {e.Item.Pos:0.0000}";
        Check(saved.Select(Key).OrderBy(k => k).SequenceEqual(again.Select(Key).OrderBy(k => k)), "exactly where it was");
        Check(!b.LoadItem(new Spot(1, 0), 0, 9, new BeltItem { Type = ResourceType.Iron, Pos = 0.5f }), "nowhere past the exit");
    }

    // A splitter fed on one side shares out what comes in between both its halves, in turn, each on its own lane.
    static void SplittersShareOutEvenly()
    {
        var w = NewFactory();
        for (int x = 0; x < 2; x++) w.AddBelt(new Spot(x, 0), 0, 2f);
        var s = w.AddSplitter(new Spot(2, 0), 0, 2f);
        Check(s.Side == 1 && s.Partner.Side == 0 && s.Partner.At == new Spot(2, 1) && s.Partner.Partner == s, "facing right, the lower half is the right one");
        for (int x = 3; x < 6; x++)
        {
            w.AddBelt(new Spot(x, 0), 0, 2f);
            w.AddBelt(new Spot(x, 1), 0, 2f);
        }
        var low = new TestChest();
        var high = new TestChest();
        w.SetPort(new Spot(6, 0), low, true);
        w.SetPort(new Spot(6, 1), high, true);
        int iron = 0, copper = 0;
        for (int i = 0; i < 30 * 16; i++)
        {
            if (iron < 40 && w.PutInto(new Spot(0, 0), ResourceType.Iron, 1)) iron++;       // the left lane
            if (copper < 40 && w.PutInto(new Spot(0, 0), ResourceType.Copper, 3)) copper++; // the right lane
            w.Tick(1f / 30f);
            CheckSpacing(w);
            Check(w.Belts.All(b => b.Lanes[0].All(it => it.Type == ResourceType.Iron) && b.Lanes[1].All(it => it.Type == ResourceType.Copper)),
                "each lane keeps to itself through the splitter");
        }
        Check(iron == 40 && copper == 40 && low.Total + high.Total == 80, "everything went through");
        int lowIron = low.Count[(int)ResourceType.Iron], highIron = high.Count[(int)ResourceType.Iron];
        int lowCopper = low.Count[(int)ResourceType.Copper], highCopper = high.Count[(int)ResourceType.Copper];
        Check(Math.Abs(lowIron - highIron) <= 1 && Math.Abs(lowCopper - highCopper) <= 1,
            $"shared out evenly (iron {lowIron} and {highIron}, copper {lowCopper} and {highCopper})");
    }

    // When one way out is full (or goes nowhere), everything goes the other way.
    static void SplittersUseTheOtherSideWhenOneIsBlocked()
    {
        var w = NewFactory();
        for (int x = 0; x < 2; x++) w.AddBelt(new Spot(x, 0), 0, 2f);
        w.AddSplitter(new Spot(2, 0), 0, 2f);
        for (int x = 3; x < 6; x++) w.AddBelt(new Spot(x, 0), 0, 2f);
        var low = new TestChest();
        w.SetPort(new Spot(6, 0), low, true);
        w.AddBelt(new Spot(3, 1), 0, 2f);   // the top way out: one tile of belt going nowhere, which fills up
        int put = 0;
        for (int i = 0; i < 30 * 20; i++)
        {
            if (put < 60 && w.PutInto(new Spot(0, 0), ResourceType.Stone, 0)) put++;
            w.Tick(1f / 30f);
            CheckSpacing(w);
        }
        var top = w.BeltAt(new Spot(3, 1));
        Check(put == 60 && top.Lanes[1].Count == 5 && top.Lanes[1][0].Pos < 0.01f, "the top way filled up (5 to a tile, from its back edge to its front)");
        Check(low.Total + ItemsOnBelts(w) == 60 && w.BeltAt(new Spot(0, 0)).Count == 0 && w.BeltAt(new Spot(1, 0)).Count == 0,
            $"and everything else went the other way ({low.Total} in the chest)");

        // A splitter with nothing in front of one half: nothing goes along that half at all.
        var v = NewFactory();
        v.AddBelt(new Spot(0, 1), 0, 2f);   // into its top half
        var half = v.AddSplitter(new Spot(1, 0), 0, 2f);
        v.AddBelt(new Spot(2, 0), 0, 2f);   // out of its bottom half only
        var chest = new TestChest();
        v.SetPort(new Spot(3, 0), chest, true);
        int sent = 0;
        for (int i = 0; i < 30 * 12; i++)
        {
            if (sent < 30 && v.PutInto(new Spot(0, 1), ResourceType.Wood, 0)) sent++;
            v.Tick(1f / 30f);
            CheckSpacing(v);
            Check(!ItemsOn(half.Partner).Any(it => it.Pos >= 0.5f), "nothing goes on along the half that leads nowhere");
        }
        Check(chest.Total == 30, $"it all crossed to the half that leads somewhere ({chest.Total})");
    }

    // Two lines into a splitter with one way out: they merge, nothing lost.
    static void SplittersMergeTwoLines()
    {
        var w = NewFactory();
        w.AddBelt(new Spot(0, 0), 0, 2f);
        w.AddBelt(new Spot(0, 1), 0, 2f);
        w.AddSplitter(new Spot(1, 0), 0, 2f);
        w.AddBelt(new Spot(2, 1), 0, 2f);
        var chest = new TestChest();
        w.SetPort(new Spot(3, 1), chest, true);
        int put = 0;
        for (int i = 0; i < 30 * 20; i++)
        {
            if (i < 30 * 8 && w.PutInto(new Spot(0, 0), ResourceType.Iron, 0)) put++;
            if (i < 30 * 8 && w.PutInto(new Spot(0, 1), ResourceType.Copper, 0)) put++;
            w.Tick(1f / 30f);
            CheckSpacing(w);
        }
        Check(chest.Total == put && chest.Count[(int)ResourceType.Iron] > 0 && chest.Count[(int)ResourceType.Copper] > 0, $"both lines came out of the one way out ({chest.Total} of {put})");
    }

    // Random factories of belts, underground belts and splitters, built and torn down while things ride them: nothing is
    // ever lost or made, items stay a gap apart on every lane, and each stays on its tile.
    static void BeltNetworksNeverLoseOrOverlap()
    {
        var rng = new Random(20261017);
        float[] speeds = { 1f, 2f, 3f, 4f };
        for (int round = 0; round < 40; round++)
        {
            var w = NewFactory();
            var chests = new List<TestChest>();
            const int size = 9;
            // Chests all round the edge.
            for (int k = -1; k <= size; k++)
                foreach (var c in new[] { new Spot(k, -1), new Spot(k, size), new Spot(-1, k), new Spot(size, k) })
                {
                    if (w.PortAt(c) != null) continue;
                    var chest = new TestChest();
                    chests.Add(chest);
                    w.SetPort(c, chest, true);
                }
            void AddRandom()
            {
                var at = new Spot(rng.Next(size), rng.Next(size));
                int dir = rng.Next(4), r = rng.Next(20);
                if (r < 9) w.AddBelt(at, dir, speeds[rng.Next(4)]);
                else if (r < 17) w.AddUnderground(at, dir, 2f, 4 + rng.Next(2), 3 + rng.Next(3));
                else if (r < 19 && at.X < size - 1 && at.Y < size - 1) w.AddSplitter(at, dir, speeds[rng.Next(4)]);
            }
            for (int k = 0; k < 70; k++) AddRandom();
            int put = 0, gone = 0;
            var spill = new List<ResourceType>();
            var all = new List<(Spot At, int Lane, int Under, BeltItem Item)>();
            for (int t = 0; t < 900; t++)
            {
                for (int k = 0; k < 4; k++)
                {
                    var at = new Spot(rng.Next(size), rng.Next(size));
                    if (w.PutInto(at, (ResourceType)rng.Next(6), rng.Next(5) - 1, rng.Next(2) == 0)) put++;
                }
                if (t % 30 == 15)
                {
                    // Build or tear down something while it runs.
                    if (rng.Next(2) == 0) AddRandom();
                    else
                    {
                        spill.Clear();
                        w.RemoveBelt(new Spot(rng.Next(size), rng.Next(size)), spill);
                        gone += spill.Count;
                    }
                }
                if (t % 100 == 0)
                {
                    // An arm picking off the front-most item somewhere, now and then.
                    if (w.TakeFrom(new Spot(rng.Next(size), rng.Next(size)), _ => true, out _)) gone++;
                }
                w.Tick(1f / 30f);
                gone += w.TakeLost(null);
                foreach (var b in EveryBelt(w))
                    for (int lane = 0; lane < 2; lane++)
                    {
                        var items = b.Lanes[lane];
                        for (int i = 0; i < items.Count; i++)
                        {
                            Check(items[i].Pos >= 0f && items[i].Pos <= 1f, $"an item stays on its tile ({items[i].Pos} at {b.At})");
                            if (i > 0) Check(items[i].Pos - items[i - 1].Pos >= FactoryWorld.Gap - 1e-4f, $"items a gap apart (round {round}, tick {t}, {b.At}, lane {lane})");
                        }
                    }
                all.Clear();
                w.SaveItems(all);
                if (put != all.Count + chests.Sum(c => c.Total) + gone)
                {
                    Check(false, $"nothing lost or made (round {round}, tick {t}: {put} put on, {all.Count} on belts, {chests.Sum(c => c.Total)} in chests, {gone} taken off)");
                    return;
                }
            }
            CheckJoinsMeet(w);
        }
    }
}
