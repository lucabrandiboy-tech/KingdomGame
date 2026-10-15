# Testing the smelter and machines

Nothing here has been run in Unity yet (it compiles, and the pictures were checked in the
art preview). Run **KingdomGame > Build Game Scenes** first: it adds everything to the
build bar's new **Industry** tab and unlocks it with its age.

## What's new

- **Steel**: a new resource, made at a smelter. The storage line on screen shows it once you have some.
- **Smelter** (Medieval, 2x2): every 20 seconds takes **2 iron + 2 wood** from storage and puts
  **1 steel** back. No villager or power needed. Iron comes from mines.
- **Generator** (Industrial, 2x2, costs steel): burns **2 wood every 30 seconds** from storage.
  Sawmills and quarries within **10 tiles** of a burning generator work.
- **Sawmill** (Industrial, 2x2): cuts the nearest tree within 8 tiles, 5 wood every 8 seconds.
- **Quarry** (Industrial, 2x2): digs 4 stone every 10 seconds out of the ground (it never runs out).
- **Conveyor belt** (Industrial, 1x1, drag to lay a line): sawmills and quarries send what they
  make along belts that touch them to a storehouse or chest the belts touch. A machine right next
  to a storehouse needs no belt. With no way to storage, a machine fills up (60) and stops.
- **Electric lamp** and **Floodlight** (Industrial, **Lights** tab, cost a little steel): bright
  white lights that only shine while a burning generator is within 10 tiles (a lamp inside a
  house counts from the house's spot). Without power they're dark and drawn darker. The
  floodlight lights about twice as far as the lamp.
- A machine that isn't working (no power, no trees, no iron, full) is drawn **darker**.

## How to test

1. Free build (everything is unlocked there). Dig some iron in a mine, or start with some.
2. Build a smelter. After about 20 seconds the storage line should show steel going up and iron going down.
3. Build a generator, then a sawmill near trees and within 10 tiles of the generator.
4. Lay conveyor belts from the sawmill to a storehouse (touching both). You should see small
   bags riding along the belt, and wood going up in storage.
5. Build a quarry the same way: small rocks ride the belt, stone goes up.
6. Run out of wood in storage: the generator stops, and the sawmill and quarry turn darker.
7. At night, put an electric lamp near the generator and one far away (over 10 tiles): only the
   near one lights up. Let the generator run out of wood: the light goes out within a second.

## Not done yet

- What's inside a machine (up to 60) and on the belts isn't saved: you lose that bit when you load.
- Belts carry in any direction (to the nearest storage they reach); there's no direction arrow yet.
- No hint text when you walk up to a machine; darker means it's not working.
- Online: machines only run in the host's own kingdom (like the rest of building so far).

## Belts into and out of houses

Every house wall has two belt holes, marked with small squares on the tile just outside the wall
and on the edge tile just inside it:
- **Red = input**: things go **in** (a belt on the red square outside joins the belt on the red square inside).
- **Blue = output**: things come **out** (inside blue to outside blue).
Red is just left of (or below) the middle of the wall, blue just right of (or above) it.
Machines inside a house get power from a generator within 10 tiles of the house's door.

Test: build a house, a quarry inside next to a blue square, a belt on that blue square, a belt on
the blue square outside the same wall leading to a storehouse, and a generator near the house.
Stone should go up in storage. Swap the belts onto the red squares: nothing comes out (red only goes in).

## Iron Age automation (no power needed)

On the Industry tab once you reach the Iron Age: **Water sawmill** (cuts trees within 6 tiles,
4 wood every 16 s), **Treadwheel quarry** (3 stone every 20 s) and **Roller track** (a wooden
conveyor, slower than the steel belt). They work like the Industrial ones but need no generator.

## Tree farm (Bronze Age)

Industry tab: **Tree farm** (2x2, wood and a little stone, no power). Every 12 seconds it plants a
young tree on open ground within 5 tiles (every other tile, so there's room to walk), until 16
trees stand round it. They grow in 3 minutes and grow back after they're cut, so woodcutters or a
sawmill next to it never run out. It doesn't plant on floors, belts or anything built.

Test: build one on open ground; within a minute saplings should appear round it and grow.
