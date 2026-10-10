# Traps and towers: how to test them

31 defences, four or five per age. On the throne, **[** and **]** turn the
build bar's pages from age to age (free build has every age; survival
moves to the next age every 3 waves).

Kinds:

- **Traps** spring on the first zombie (hurt it, hold it), then set themselves again.
- **Areas** slow every zombie in them and wear them down (drag a line).
- **Mines** blow up once; builders set a new one (it costs again).
- **Barriers** stand in the way: zombies must smash through; thorny ones hurt them for every blow.
- **Clouds** (gas, napalm): the first zombie sets off a cloud that hurts everything in it for a while.
- **Towers** shoot: early ones only with a **Guard** on them, later ones by themselves.

| Age | Traps tab | Towers tab |
|---|---|---|
| Stone | Spike pit, Stake field, Fire pit (burns) | Sling post (guard) |
| Bronze | Spike plate (hits all on it), Tar pit (very slow), Thorn hedge (barrier) | Archer tower (guard) |
| Iron | Caltrops, Abatis (barrier) | Ballista (guard, bolts pierce 3), Catapult (guard, lobs stones) |
| Medieval | Bear trap | Boiling oil (guard, short range splash), Crossbow tower (guard), Trebuchet (guard, very long range) |
| Renaissance | Powder keg (mine), Cheval de frise (barrier) | Musket tower (guard), Cannon tower (guard) |
| Industrial | Spring snare, Barbed wire, Dynamite tripwire (mine) | Gatling turret (automatic) |
| World War 1 | Landmine, Gas trap (cloud), Sandbag wall (barrier) | Machine-gun nest, Mortar pit (both automatic) |
| World War 2 | Bouncing mine, Napalm trap (fire cloud), Czech hedgehog (barrier) | Flamethrower bunker, Howitzer (both automatic) |

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

- [ ] Traps and Towers tabs show the items with their costs; [ and ] show each age's own
- [ ] A gas trap makes a green cloud that hurts zombies in it; a napalm trap a glowing fire cloud
- [ ] Zombies stop at a thorn hedge or abatis and hit it, and get hurt doing it
- [ ] The fire pit burns and lights up at night; zombies walking through it get hurt
- [ ] A spike pit looks like ground covered in leaves; a zombie walking on it drops in, stops, then climbs out; the pit is open, then covered again after a while
- [ ] A bear trap snaps shut on a zombie and holds it
- [ ] Zombies walk slowly through stakes and barbed wire, and get hurt
- [ ] A landmine blows up under zombies, then a blueprint for a new one appears
- [ ] V: give a villager the Guard job: they walk to the archer tower and stand on it; it shoots arrows at zombies
- [ ] The ballista and cannon turn to aim; ballista bolts go through several zombies; cannonballs blow up where they land
- [ ] The machine-gun nest shoots by itself, no guard
- [ ] Without a guard, the archer tower, ballista and cannon don't shoot
