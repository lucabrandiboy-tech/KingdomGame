# Testing the factory (Factorio-style)

The kingdom now has a factory you build like the rest (builders build it from blueprints):

- **Belts** carry items one way, on **two lanes** (left and right, as in Factorio): you see every item ride along. Drag a
  line of belts from the throne and they point the way you drag (or press **R** to turn one). A belt fed from one side
  only turns the corner, each lane staying on its side. A belt joining another from the side puts everything onto the
  near lane of the one it joins (side-loading), so two belts joining from both sides fill both lanes. Arms drop onto the
  **far** lane; drills and the older machines drop onto the **near** one. Two belts pointing at each other stop. A belt
  that ends pointing at a chest or storehouse unloads both lanes into it.
- **Underground belts** (from the Industrial age, one for each of the conveyor, fast and express belts) carry things
  under whatever is built over them. Place one, facing the way things go: that's an **entrance**. Place another of the
  same kind ahead of it, facing the same way, within its reach (5 tiles for the plain one, 7 for the fast, 9 for the
  express): that's its **exit**. While you place one, the text says which it will be. In a line of them they pair up from
  the back: in, out, in, out. An entrance with no exit keeps things waiting at its mouth.
- **Splitters** (Industrial, fast and express; two tiles across the way things go) share what comes in on either belt
  between their two belts going out, in turn, each lane staying on its side. When one way out is full, or leads nowhere,
  everything goes the other way. Their sides are closed: belts can't join them from the side.
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
- **Craftables** (like Factorio's science packs): from the Bronze Age, assemblers can make each age's Craftable (bronze,
  iron, medieval, renaissance, industrial, modern craftables: corked flasks of the age's colour) from several of the age's
  goods. The research into the next age takes a pile of them from storage (10 bronze craftables for the Iron Age, then 15,
  20, 25, 30, 40), on top of its other costs. Making them pollutes four times as much as anything else.
- **Pollution**: furnaces, drills, assemblers and generators put pollution in the air while they work. It spreads out
  round the factory in 32-tile squares and is absorbed by the ground and, much more, by trees (cut the forest down and it
  spreads further). **P** shows it as red squares. In survival it makes things much harder: the hordes get bigger with the
  pollution in the air (twice as big at 2,000, up to four times), the zombies get tougher and their later kinds come sooner
  with all the pollution ever made, and a zombie base the cloud reaches breathes it in and sends zombies at you, day or night.
  The screen says how much there is and what it's doing.
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
| Industrial | conveyor belt, underground belt (reach 5), splitter, electric arm, assembling machine, electric mining drill (3x3, digs 5x5 tiles, 30 a minute), wooden power pole (wires 7.5 tiles, powers 5x5); generator 0.9 MW |
| World War 1 | fast belt, fast underground belt (reach 7), fast splitter, fast arm, electric furnace, medium power pole (wires 9, powers 7x7), big power pole (2x2, wires 30, powers 4x4), accumulator (5 MJ); diesel generator 1.8 MW |
| World War 2 | express belt, express underground belt (reach 9), express splitter, assembly robot, big mining drill (3x3, digs 7x7 tiles, 60 a minute), substation (2x2, wires 18, powers 18x18), solar panel (60 kW by day); power plant 4 MW |

None of this has been run in Unity yet. The belts, arms, furnaces, assemblers, drills and electricity are plain code that
the unit tests run (tools/tests/FactoryTests.cs: lines, packing, curves, side-loading, head-on belts, chests, loops, arms,
assemblers, furnaces, burner fuel, drills digging onto a belt, waiting when blocked, feeding each other and a furnace;
tools/tests/BeltTests.cs: the two lanes (arms on the far lane, chutes on the near one, side-loading, curves keeping
lanes, a fast belt into a slow one keeping its gaps), underground belts (pairing, carrying under a crossing belt, giving
back what was under the ground when a pair is cut, saving), splitters (sharing evenly, the other way when one is
blocked, merging), and random factories built and torn down while running, checking nothing is ever lost or overlaps;
tools/tests/PowerTests.cs: poles wiring up, supply areas, generators sharing the load and burning for what's used,
brownouts slowing machines, accumulators and solar panels through day and night; tools/tests/PollutionTests.cs: clouds
spreading and settling, trees and bases taking it in, saving, the hordes' sizes and toughness, the Craftables' recipes);
the ore fields
are checked by tools/world-tests (the same everywhere, one of each round the start, mining and saving, the ground showing
ore exactly where it is); the pictures were checked in the art preview (belts.png: a factory run by the real belt code).
Run **KingdomGame > Build Game Scenes** after `git pull` (the art changed, so it bakes again; the underground belts and
splitters join the build bar).

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

## Two lanes, underground belts and splitters

1. Free build, **F10**: **+10,000 of everything**. A line of **conveyor belt** (about 10 tiles) into a storehouse.
2. Two chests (or storehouses) with iron and copper, one on each side of the start of the line, each with an **electric
   arm** (and a generator's power) dropping onto the belt, filtered (**E**) to iron and to copper. You should see the
   iron ride on one lane and the copper on the other, side by side, round any corner too.
3. A belt joining the line from the side: what it carries goes onto the lane on its side only.
4. An **underground belt** a few tiles along the line, facing the way it goes (take that belt away first): the text says
   "It will be an entrance". Another one 3 tiles further on, facing the same way: "It will be the exit of the
   underground belt 3 tiles behind it". Build them, then put anything between them (a wall, another belt crossing):
   things go into the entrance, out of sight, and come up out of the exit. Point at each from the throne: entrance / exit.
5. Take the exit away: what was under the ground comes back to storage. The entrance says it has no exit, and things
   wait at its mouth.
6. A **splitter** on the line (two tiles across it; R turns it), and a line of belt out of each of its halves into a
   storehouse each: half the items go each way, each lane on its side. Block one way out (take away the storehouse at its
   end and let it fill up): everything goes the other way.
7. Save, load: the items on both lanes, and under the ground, are where they were.

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

## Craftables

Survival (free build has every age open, so it doesn't need them):

1. **F10**: **+10,000 of everything**, then get to the **Bronze Age** (F10 ages, or research it).
2. **T**: the research into the Iron Age now also takes **10 bronze craftables**. It waits ("Waiting for ... 10 bronze
   craftables in storage").
3. Build a **workshop assembler**, **E**: choose **Bronze craftables** (2 bronze bars, 1 rope, 1 copper). Feed it with arms
   from a storehouse (it has bronze bars, rope and copper from the cheat) and take the flasks out with an arm into a chest
   or storehouse. Ten of them, and the research starts.
4. Each later age's research takes more of its own Craftable (iron craftables need gears, iron bars and coal; medieval
   ones steel, gears and bronze bars; renaissance ones gunpowder, steel, gears and iron bars; industrial ones machine
   parts, wire and steel; modern ones electronics, machine parts and gunpowder).

## Pollution

1. Start a factory (a few drills and furnaces, or **F10, Pollution: +2,000 where you stand**). The screen's top right
   says "Pollution: ... in the air".
2. **P**: red squares show it, darker where there's more; it spreads out over a minute or two. Point at the ground from
   the throne: "Pollution here: ...".
3. Survival: the next horde's message says "(x1.5: your pollution)" and the screen says how much bigger the hordes are and
   how many waves further on the zombies are.
4. Pile pollution on (several **+2,000**) until the red reaches a zombie base: soon "Your pollution reached a zombie base:
   ... zombies are coming from the ...!", and they come for your kingdom, even by day.
5. A forest takes pollution in: the red stops short over woods, and spreads further where you've cut them down.
6. **F10, Clear the pollution**: the air is clean (the zombies stay as far on as they were). Save, load: the pollution
   is kept.

Things that aren't done yet (honestly):

- Splitters have no settings yet (Factorio's input and output priority and filter).
- Saves from before the two lanes put every item on the right lane.
- Belts placed before this update all point right after it (they had no direction before): turn them by tearing them
  down and dragging them again.
- Other players online see your belts and machines but not the items moving on them.
- Online, each player's computer keeps its own ore fields: what your drills dig isn't taken out of a friend's world.
- Generators don't need water (Factorio's boilers and pumps are left out).
- Pollution isn't shared online: each player's computer has its own cloud and its own bases.
- In survival with Survival Ages By Waves on (off by default), ages still come every few waves and need no Craftables.
- The older machines (smelter, charcoal kiln, kilns...) don't pollute; only the factory's machines and generators do.
