# Testing each age's content

Run **KingdomGame > Build Game Scenes** first. Nothing here has been run in Unity yet.

## Crafting tables and parts

Each age has a **crafting table** (Industry tab) and a **part** it makes:

| Age | Table | Part | One batch takes | Makes |
|---|---|---|---|---|
| Stone Age | Stone Age crafting table | Rope | 2 food, 1 wood | 2 |
| Bronze Age | Bronze Age crafting table | Bronze bars | 3 copper, 1 coal | 2 |
| Iron Age | Iron Age forge table | Iron bars | 3 iron, 2 coal | 2 |
| Medieval | Medieval workshop bench | Gears | 2 steel, 2 wood | 2 |
| Renaissance | Renaissance powder table | Gunpowder | 2 coal, 1 stone, 1 food | 2 |
| Industrial | Industrial machine bench | Machine parts | 2 steel, 1 gear, 1 coal | 2 |
| World War 1 | World War 1 wiring bench | Wire | 3 copper, 1 steel | 3 |
| World War 2 | World War 2 electronics bench | Electronics | 1 gold, 2 copper, 1 wire | 2 |

- A villager with the **Crafter** job (V) works at a table while storage has fewer than 40 of
  its part and the ingredients are there. Weapons ordered at an anvil come first.
- **Everything an age brings** (its traps, towers, machines and the rest) now costs some of that
  age's part and of every earlier age's part: 4 of its own and 2 of each earlier one (twice that
  for things 2x2 and bigger). A crafting table costs the earlier ages' parts (not its own).
  Walls, doors, the first furniture, storehouses and houses don't need parts.
- The cost shows on the build bar as usual, and builders fetch the parts from storage.

Test: Stone Age, build the Stone Age crafting table, give a villager the Crafter job, keep
some food and wood in storage: rope should go up in storage (the table is drawn darker while
nobody works at it). Then build a spike pit: it takes 4 rope.

## Automation: 5 machines per age (Industry tab)

Sawmills cut trees in reach, quarries dig stone forever; both send what they make out on belts to
a storehouse. Farms, pits and drills put what they make straight into storage (the drills marked
"belts" send it out on belts). "Power" means it only works near a burning generator.

| Age | Machines |
|---|---|
| Stone Age | Woodcutter's camp (2 wood/24 s), Rock gatherer's pile (1 stone/24 s), Fish trap (2 food/40 s), Sapling bed (keeps 6 trees planted), Log chute (belt) |
| Bronze Age | Tree farm (16 trees), Copper sluice (1 copper/40 s), Berry orchard (3 food/30 s), Bronze saw pit (3 wood/20 s), Sledge track (belt) |
| Iron Age | Water sawmill, Treadwheel quarry, Roller track (belt), Bog iron pit (1 iron/45 s), Granary farm (4 food/30 s) |
| Medieval | Smelter (steel), Charcoal kiln (3 wood into 1 coal/20 s), Windmill (5 food/25 s), Wind sawmill (4 wood/14 s), Coal pit (1 coal/30 s) |
| Renaissance | Water mill (6 food/20 s), Clockwork crafter (makes gunpowder by itself, up to 40), Water-powered quarry (4 stone/15 s), Stamp mill (2 iron/40 s), Canvas belt |
| Industrial | Generator, Sawmill, Quarry, Conveyor belt, Steam drill (2 coal/20 s, power, belts) |
| World War 1 | Diesel generator (14 tiles, 45 s a coal), Electric sawmill (power), Ore drill (1 gold/40 s, power, belts), Tractor farm (10 food/20 s, power), Fast belt |
| World War 2 | Power plant (22 tiles), Assembly line (electronics by itself, power), Excavator (8 stone/10 s, power), Mechanized farm (15 food/20 s, power), Express belt |

Each belt is faster than the one before (0.6 tiles a second for the log chute up to 4 for the
express belt). All of them cost their age's parts (see above).

## Furniture with boosts: 10 per age (Furniture tab)

Every age has a bed, a table, shelves, a hearth or stove, a desk, a decoration, a seat, a chest,
a rug and a lamp. Each one makes the kingdom better at something; **each kind counts once** (ten
feast tables boost as much as one). The bonus is in its name on the build bar, like
"Feast table (+2.5% crafting)".

| Piece | Boosts | Bonus |
|---|---|---|
| Bed | walking speed (rested villagers), and homes for 4 (Stone Age) up to 10 (World War 2) | +1% (Stone Age) up to +4.5% (World War 2) |
| Table | crafting (weapons and parts) | same |
| Shelves / rack / lockers | gathering and digging | same |
| Hearth / oven / stove / kitchen | building (builders' work) | same |
| Desk | research speed | same |
| Decoration (totem, idol, banner, tapestry, bust, clock, flag, poster) | walking speed | same |
| Seat (log stool, wicker stool, oak bench, high-backed chair, velvet and leather armchairs, camp chair, office chair) | research speed | same |
| Chest (basket, clay jars, iron-bound chest, coffer, wardrobe, safe, ammunition crates, filing cabinet) | gathering and digging | same |
| Rug, 2x2 (hide, reed mat, wool rug, bearskin, Persian carpet, patterned carpet, duckboards, linoleum) | walking speed | same |
| Lamp (fire ring, brazier, iron lantern post, candle stand, silver candelabra, gas lamp, oil lantern, hurricane lamp) | crafting, and it lights the room at night | same |

They add to the XP upgrades (V). With every age's furniture, walking speed is about +66%,
crafting, gathering and research about +44% and building about +22%.

What to check: seats, rugs and lamps can be walked through (rugs lie flat, under everything);
lamps have flames and light up at night (the silver candelabra has five candles).

## Better farming each age (Farming tab)

| Age | Plant | Gives | Grows back in |
|---|---|---|---|
| Stone Age | Wild berry patch | 25 food | 2 min |
| Bronze Age | Fruit tree (picked by foragers) | 40 food | 3.3 min |
| Iron Age | Fast pine | 60 wood | 2 min |
| Medieval | Apple orchard tree | 60 food | 3 min |
| Renaissance | Hardwood oak | 100 wood | 3.3 min |
| Industrial | Fertilised berry bush | 40 food | 1 min |
| World War 1 | Hybrid poplar | 120 wood | 1.5 min |
| World War 2 | Greenhouse berry bush | 80 food | 45 s |

Plant them like saplings (drag a line). Trees grow back where they're cut; bushes refill. Each
kind is tinted a little (fruit trees reddish, pines bluish green...) so you can tell them apart.
They don't cost parts. Each age's farm machines are in the automation table above.

## Two more towers each age (Towers tab)

| Age | Tower | Size | Who shoots | What it does |
|---|---|---|---|---|
| Stone Age | Spear thrower's post | 1x1 | guard | throws spears, reach 7 |
| Stone Age | Rock-drop platform | 1x1 | guard | lobs rocks over walls, reach 6 |
| Bronze Age | Javelin tower | 2x2 | guard | javelins, reach 8.5 |
| Bronze Age | Bronze bolt thrower | 2x2 | guard | bolts that go through 2 zombies, reach 9.5 |
| Iron Age | Scorpion | 2x2 | guard | bolts through 3 zombies, reach 10.5 |
| Iron Age | Iron archer keep | 2x2 | guard | fast arrows, reach 9, very tough |
| Medieval | Longbow tower | 2x2 | guard | arrows, reach 11.5 |
| Medieval | Mangonel | 2x2 | guard | lobs stones, reach 13 |
| Renaissance | Swivel gun tower | 2x2 | guard | musket balls, reach 9.5 |
| Renaissance | Culverin | 2x2 | guard | long-range cannonballs that burst, reach 15 |
| Industrial | Rifle tower | 2x2 | guard | rifle shots, reach 11 |
| Industrial | Steam cannon | 2x2 | by itself | cannonballs, reach 12 |
| World War 1 | Sniper nest | 2x2 | guard | slow, very hard-hitting shots through 2 zombies, reach 16 |
| World War 1 | Field gun | 2x2 | by itself | cannonballs, reach 16 |
| World War 2 | Anti-tank gun | 2x2 | by itself | shells through 4 zombies, reach 14 |
| World War 2 | Rocket battery | 2x2 | by itself | rockets that blow up where they land, reach 20 |

Like every other thing in an age, they cost that age's part and the earlier ages' parts (made at
the crafting tables). "Guard" towers need a villager on them, like the archer tower.

**Every tower (the older ones too) is half price and builds twice as fast:** half the wood, stone and
food, half the parts, half the building time.

What to check: each age's two towers are on the Towers tab once you reach the age; a guard climbs
the guard ones; the ones with a turning top (rock drop, bolt throwers, scorpion, mangonel, swivel
gun, cannons, anti-tank gun, rocket battery) turn to face the zombie they shoot; straight-fired
cannonballs (culverin, steam cannon, field gun) burst on the first zombie they hit.
