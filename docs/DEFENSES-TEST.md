# Traps and towers: how to test them

**Traps tab** (they work by themselves):

| Trap | What it does |
|---|---|
| Spike pit | Covered with branches; the first zombie falls in: hurt and stuck for a few seconds. Sets itself again |
| Stake field | Slows zombies to half speed and hurts them while they're in it (drag a line) |
| Bear trap | Snaps shut on a zombie: hurt and held for 8 seconds |
| Barbed wire | Slows zombies to a crawl and cuts them (drag a line) |
| Landmine | Blows up once when a zombie comes close; builders set a new one (it costs again) |

**Towers tab**:

| Tower | What it does |
|---|---|
| Archer tower | A guard on top shoots arrows |
| Ballista | A guard works it; its bolts go through 3 zombies |
| Cannon tower | A guard works it; cannonballs land among zombies and blow up (not too close) |
| Machine-gun nest | Shoots by itself, fast |

Towers that need a guard only shoot while a villager with the **Guard** job
(villagers panel, **V**) is on them. Guards walk to the nearest empty tower,
climb up, and don't run from zombies. Towers have unlimited ammo for now
(crafting comes later).

**Free build: F9** sends 8 zombies from the edge of the map, to try it all.

> **Not tested in Unity yet.**

## Set up

Close Unity, `git pull`, open the project, **KingdomGame → Build Game Scenes**
(it bakes the new pictures: a few minutes). The Console says it added traps and towers.

## Check

- [ ] Traps and Towers tabs show the items with their costs
- [ ] A spike pit looks like ground covered in leaves; a zombie walking on it drops in, stops, then climbs out; the pit is open, then covered again after a while
- [ ] A bear trap snaps shut on a zombie and holds it
- [ ] Zombies walk slowly through stakes and barbed wire, and get hurt
- [ ] A landmine blows up under zombies, then a blueprint for a new one appears
- [ ] V: give a villager the Guard job: they walk to the archer tower and stand on it; it shoots arrows at zombies
- [ ] The ballista and cannon turn to aim; ballista bolts go through several zombies; cannonballs blow up where they land
- [ ] The machine-gun nest shoots by itself, no guard
- [ ] Without a guard, the archer tower, ballista and cannon don't shoot
