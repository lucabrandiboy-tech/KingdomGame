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
