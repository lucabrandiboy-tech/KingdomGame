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
- A machine that isn't working (no power, no trees, no iron, full) is drawn **darker**.

## How to test

1. Free build (everything is unlocked there). Dig some iron in a mine, or start with some.
2. Build a smelter. After about 20 seconds the storage line should show steel going up and iron going down.
3. Build a generator, then a sawmill near trees and within 10 tiles of the generator.
4. Lay conveyor belts from the sawmill to a storehouse (touching both). You should see small
   bags riding along the belt, and wood going up in storage.
5. Build a quarry the same way: small rocks ride the belt, stone goes up.
6. Run out of wood in storage: the generator stops, and the sawmill and quarry turn darker.

## Not done yet

- What's inside a machine (up to 60) and on the belts isn't saved: you lose that bit when you load.
- Belts carry in any direction (to the nearest storage they reach); there's no direction arrow yet.
- No hint text when you walk up to a machine; darker means it's not working.
- Online: machines only run in the host's own kingdom (like the rest of building so far).
