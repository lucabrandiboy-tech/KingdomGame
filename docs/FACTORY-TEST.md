# Testing the factory (Factorio-style)

The kingdom now has a factory you build like the rest (builders build it from blueprints):

- **Belts** carry items one way: you see every item ride along. Drag a line of belts from the throne and they point the
  way you drag (or press **R** to turn one). A belt fed from one side only turns the corner; a belt fed from behind and
  from the side merges both lines (side-loading). Two belts pointing at each other stop. A belt that ends pointing at a
  chest or storehouse unloads into it.
- **Arms** (like Factorio's inserters) pick an item up from the tile behind them and put it down on the tile in front
  (the arrow on the base): belts, chests, storehouses and machines. They only put into a machine what it needs. The
  wooden and iron arms burn wood or coal; when they run out they take some for themselves from where they pick up.
  **E** beside an arm: choose what it moves (anything, or one kind of item).
- **Furnaces** smelt whatever arms put in: iron ore makes iron, copper ore makes copper, and from the Medieval age iron
  makes steel (5 to 1). Stone and brick furnaces burn wood or coal (an arm puts it in, like the ore).
- **Assemblers** make the recipe you choose (**E** beside one): rope, bronze bars, iron bars, gears, gunpowder, machine
  parts, wire, electronics, each from its age. The recipe shows on the assembler. They hold ingredients for two batches.
- **Ore fields** lie on the ground: bare patches of iron ore (blue-grey rocks with rust), copper ore (orange with green),
  coal (black lumps) and stone (pale cobbles), with no trees or rocks on them. There's one of each 30 to 42 tiles from the
  start, and more the further you go (about one every 100 tiles, richer far out). Each tile holds a few hundred; it looks
  thinner as it's dug, and bare when it's empty. From the throne, point at one to see how much is in that tile.
- **Drills** go on ore fields and dig the ore under them, a tile at a time in turn, and put it out of their **chute** (the
  side they face, **R** turns them before you place them): onto a belt, or into a chest, storehouse, furnace, or another
  burner drill (coal is its fuel). With nowhere to put it they wait. While you place one it says how much ore it would dig
  there. The digging frame and the burner drill burn wood or coal (an arm puts it in); the electric ones need power. Their
  bit turns while they dig.
- **Electricity** (from the Industrial age): a **generator** powers what's within its reach (10 tiles for the first one)
  and every **power pole** wired to it. Poles wire themselves to the nearest poles within their wire reach (you see the
  wires) and power the square round them; poles wired together are one network sharing all its generators. Generators burn
  only for what's used (coal from storage, or wood, which holds a third as much; an arm can put fuel straight in). If more
  is wanted than the network gives, every machine on it works slower (a brownout). **Accumulators** store what's spare and
  give it back when it's short; **solar panels** give power by day only. While you place anything electric, blue areas show
  what's powered. Electric arms, furnaces, assemblers, drills, lamps and the older electric machines all run on it.
- The older machines (woodcutter's camps, quarries, steam and ore drills) put what they make out an item at a time onto
  a belt touching them, or into a chest touching them; arms can take it out of them too.
- From the throne, point at any factory piece to see what it's doing or why it's stopped.

What's where, by age (Industry tab):

| Age | New |
| --- | --- |
| Stone Age | log chute (belt), wooden arm, stone furnace, digging frame (2x2 drill, 10 a minute) |
| Bronze Age | sledge track, workshop assembler |
| Iron Age | roller track, iron arm, burner drill (2x2, 15 a minute) |
| Medieval | brick furnace (twice as fast) |
| Renaissance | canvas belt, clockwork assembler |
| Industrial | conveyor belt, electric arm, assembling machine, electric mining drill (3x3, digs 5x5 tiles, 30 a minute), wooden power pole (wires 7.5 tiles, powers 5x5); generator 0.9 MW |
| World War 1 | fast belt, fast arm, electric furnace, medium power pole (wires 9, powers 7x7), big power pole (2x2, wires 30, powers 4x4), accumulator (5 MJ); diesel generator 1.8 MW |
| World War 2 | express belt, assembly robot, big mining drill (3x3, digs 7x7 tiles, 60 a minute), substation (2x2, wires 18, powers 18x18), solar panel (60 kW by day); power plant 4 MW |

None of this has been run in Unity yet. The belts, arms, furnaces, assemblers, drills and electricity are plain code that
the unit tests run (tools/tests/FactoryTests.cs: lines, packing, curves, side-loading, head-on belts, chests, loops, arms,
assemblers, furnaces, burner fuel, drills digging onto a belt, waiting when blocked, feeding each other and a furnace;
tools/tests/PowerTests.cs: poles wiring up, supply areas, generators sharing the load and burning for what's used,
brownouts slowing machines, accumulators and solar panels through day and night); the ore fields
are checked by tools/world-tests (the same everywhere, one of each round the start, mining and saving, the ground showing
ore exactly where it is); the pictures were checked in the art preview. Run **KingdomGame > Build Game Scenes** after
`git pull` (the art changed, so it bakes again; the new drills join the build bar).

## Try it (free build is easiest: every age is open)

1. **F10**: **+10,000 of everything**, then **Factory: +300 iron ore, copper ore and coal**.
2. From the throne, next to a storehouse: a **wooden arm** touching the storehouse and pointing away from it (R turns
   it before you place it), and a **stone furnace** in front of that arm. A second wooden arm from the storehouse into
   another side of the furnace. A third wooden arm on the far side of the furnace, pointing away from it, and a line of
   **conveyor belt** dragged from in front of it round to the storehouse (the last belt pointing into it).
   **F10, Finish all building now**.
3. Walk to the first arm, **E**, pick **Iron ore**. The second arm: **Coal**. (Without a choice an arm takes anything
   the furnace would take: from a storehouse full of iron it would put iron in, and the furnace would make steel.)
4. You should see: the arms swing ore and coal into the furnace; the furnace light up; the third arm take iron out and
   drop it on the belt; the iron ride the belt round the corners into the storehouse. Point at each piece from the
   throne: it says what it's doing, or why it's stopped.
5. Put down an **assembling machine** (or a workshop assembler in the earlier ages), walk to it and press **E**: choose
   **Gears**. Feed it iron with an arm from a belt or chest, and take the gears out with another arm. In the Industrial
   machines' case a **generator**'s power has to reach them.
6. Drag belts so one line runs into the side of another: the items join it. Drag one from above into the start of a
   line: it curves round the corner.
7. **E** beside an arm: pick **Coal**. It moves only coal now.
8. Save, load: the items on the belts, what's in the machines, the arms' filters and the assemblers' recipes are kept.

## Ore fields and drills

1. **F10**: **Where are the ore fields?** It says how far and which way the nearest field of each kind is. Walk there (or
   look from the throne, zoomed out): bare ground covered in lumps of ore. Point at a tile from the throne: "Ore field:
   ... in this tile".
2. Pick the **digging frame** (Stone Age) or the **burner drill** (Iron Age) on the Industry tab. Move it over the iron
   field: the text under it says how much ore it would dig there (or that there's no ore). **R** turns it so its chute
   points where you'll put the belt. Place it, and a line of belts from its chute to a storehouse. **F10, Finish all
   building now**.
3. It doesn't dig yet: it needs fuel. Put a wooden arm between a chest or storehouse with coal (or wood) and the drill,
   pointing into the drill, and set the arm to **Coal** (E). The drill's bit turns, iron ore drops on the belt and rides
   into the storehouse.
4. Walk to the drill and press **E**: what it's digging, how much ore is left under it, and its fuel.
5. Two burner drills on a coal field, each with its chute pointing into the other, and one coal put into one of them
   (with an arm from a storehouse, then take the arm away): they keep each other going until both are full of coal, then
   wait (as in Factorio).
6. A drill's chute pointing straight into a stone furnace (no belt): the furnace smelts the ore as it comes.
7. Leave a drill digging for a while (or use **F10** game speed x4): the tiles under it look thinner, a quarter at a
   time, and a tile that's dug out is bare ground. When nothing is left under it, it stops and says so.
8. Save, load: the drills' fuel and what they hold are kept, and what's been dug stays dug.

## Electricity

1. Free build (every age is open), **F10**: **+10,000 of everything** (coal included).
2. Put down a **generator** near the storehouse (the storehouse has coal from the cheat). Pick an **electric arm** or an
   **assembling machine**: while you hold it, a faint blue circle shows the generator's reach. Place it inside the circle.
   **F10, Finish all building now**. It works; point at it from the throne: no "No power" line.
3. Place one outside the circle: point at it: "No power: put it within a generator's reach or a power pole's area".
4. Place **wooden power poles** in a line from the generator to it, each within 7.5 tiles of the last (the blue squares
   show what each powers). Finish building: wires join the poles, and the machine works.
5. Point at a pole (or E beside it): "Power network: ... used of ... (1 generator, ...)".
6. Put down many electric machines working at once (or several electric drills on an ore field): when they want more than
   0.9 MW the network line turns orange ("only 60% of what's wanted"), and they all work slower. A second generator fixes it.
7. Take the coal out of storage (or use it up): the generator says "no fuel", and everything on it stops.
8. An **accumulator** and a **solar panel** on a pole's area: by day the panel charges the
   accumulator (point at it: "Stored ... charging"); at night (F10, time of day) the accumulator keeps things going until
   it's empty. A **big power pole** reaches 30 tiles to another big pole; a **substation** powers an 18x18 square.
9. Lamps in a house are powered when the house's door is in a generator's reach or a pole's area.
10. Save, load: the generators' fuel and the accumulators' charge are kept; the wires come back by themselves.

Things that aren't done yet (honestly):

- One lane of items per belt (Factorio has two).
- Belts placed before this update all point right after it (they had no direction before): turn them by tearing them
  down and dragging them again.
- Other players online see your belts and machines but not the items moving on them.
- Online, each player's computer keeps its own ore fields: what your drills dig isn't taken out of a friend's world.
- Generators don't need water (Factorio's boilers and pumps are left out).
- Craftables for the ages and pollution come next.
