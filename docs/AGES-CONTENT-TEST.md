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
