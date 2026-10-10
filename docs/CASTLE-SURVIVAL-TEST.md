# The castle and survival update: how to test it

You asked for this before testing Phase 2A and 2B, so it's built on top of
them and **tests them too**. Everything in `docs/PHASE-2B-TEST.md` still
applies; this guide covers what's new.

What's new:

- **Rooms.** Close an area with walls and doors and it becomes a room. On the
  throne you see its name, which depends on what's in it: a bed makes a
  *Bedroom*, three beds a *Barracks*, a table with seats a *Dining hall* (or a
  *Great hall* if it's big), shelves a *Library*, weapons an *Armory*, a
  cauldron a *Kitchen*, an anvil a *Workshop*, chests and barrels a
  *Storeroom*; the throne's room is the *Throne room*.
- **Doors** (wooden and heavy). They open by themselves for you and your
  villagers and shut behind you. Zombies have to break them down. Put a door
  in a gap, or right on top of a wall tile: the door replaces that tile when
  it's built. Doors turn to fit the wall by themselves.
- **Floors** (wooden boards, flagstones) and **rugs**: drag to cover a whole area.
- **Furniture:** chair, table, long table, bench, bed, bookshelf, weapon rack,
  armor stand, cauldron, anvil, banner, well. **R** turns things before you
  place them.
- **Lights:** torch, candelabra, brazier, fireplace. They light the room
  around them and flicker.
- **Storage:** chest, barrel and crates hold resources like a small storehouse.
- **A main menu** with two modes:
  - **Survival:** endless waves of zombies. You get two minutes to build
    (press **N** to call a wave early). Every wave is bigger, and new kinds join
    in: **walkers** first, fast **runners** from wave 3, big **brutes** that
    smash walls from wave 5. Zombies are faster and hit harder at night. They
    find the weakest way in (a door, a wooden wall, a gap), break through, and
    go for you and your villagers. After each wave, survivors come to join you
    if you have **beds** for them. When you fall, the game is over.
  - **Free build:** no zombies; build in peace.
- **Health:** you have 100. Zombies hurt you (the screen flashes red); you
  heal after a few seconds without being hit. Your own rockets and C4 hurt you
  too if you're too close.
- **Shooting over your walls:** your rifle shots fly over a wall you stand
  right behind (within about a tile and a half), so you can defend from inside.
  Rockets don't: they hit the first wall in the way.
- **Loot:** some zombies drop a bag of wood, stone and food. Walk over it to
  pick it up.
- **Villagers** run from zombies they can see (to the throne, or away), and
  can be killed.
- **Pause:** Esc (Resume, Start again, Main menu, Quit).

> **Not tested in Unity yet.** Everything compiles, the zombie path finding,
> waves and room finding pass their tests, and the pictures were checked in
> mock screenshots, but nobody has pressed Play on this version. Expect a few
> things to be off: send a screenshot of anything odd, and any red errors
> from the **Console** window (Window > General > Console).

---

## Step 1. Get the new files

**Close Unity first.** Then in Git Bash:

```bash
cd /c/Dev/KingdomGame
git pull
```

If `git pull` complains about local changes, send Claude a screenshot.
Don't delete anything.

## Step 2. Build the scenes

1. Open the project in Unity Hub. Wait for it to finish compiling (the
   spinner in the bottom right corner stops).
2. Menu: **KingdomGame → Build Game Scenes**. If it asks to save the current
   scene, click **Don't Save**.
3. **It bakes the art again** (there are a lot of new pictures), which takes a
   few minutes: a progress bar says "Baking the art". Leave Unity alone until
   it's done.
4. It makes the new settings files in `Assets/Data` (keeping the ones you
   already have), builds three scenes in `Assets/Scenes`: **MainMenu**,
   **Kingdom** and **StressTest**, and opens **Kingdom**.
5. The **Console** should say "Baked the art: 20 texture sheets" (give or
   take a couple). If you'd run Build Game Scenes before, it also says it added
   the castle furniture to your build bar. A red error instead: send Claude a
   screenshot.

## Step 3. Free build: build a castle

1. In the **Project** window, open `Assets/Scenes` and double-click
   **MainMenu**. Press **▶ Play** and click **Free build**.
2. Walk to the throne and press **E** to sit. In the throne view:

| Input | Does |
|---|---|
| Tab, or click a tab | Walls & doors, Floors & rugs, Furniture, Lights, Storage |
| 1-9, or click a button | Pick something on the tab (again = put it away) |
| R | Turn it (furniture, beds, doors) |
| Left click | Place it. **Walls:** hold and drag for a line. **Floors:** hold and drag for an area |
| Right click | Stop placing. With nothing picked: cancel a blueprint, or **tear down** what's built under the mouse (half its cost back) |
| W A S D / mouse wheel | Move the view / zoom |
| E or Esc | Stand up |

3. Try to build a small castle. You start with some wood and stone; gather
   more (hold **E** at trees and rocks) and empty your backpack at the
   storehouse (**E**). Check these and tell Claude which work:

- [ ] The build bar has tabs; each shows its own buttons with their costs
- [ ] A ring of walls with a door in it becomes a room: its name shows over it on the throne view, and hovering the mouse over it says its name and size
- [ ] Placing a door on a wall tile works; when it's built, the wall tile is gone and the door is there
- [ ] Doors turn to fit the wall; R turns them yourself
- [ ] Walking up to a door opens it; it shuts behind you. Villagers walk through doors too
- [ ] Dragging a floor covers the whole area with boards or flagstones; furniture can stand on floors
- [ ] A rug lies flat under people and furniture
- [ ] Furniture turns with R before you place it (a chair faces the way you pick)
- [ ] Beds, tables and shelves block the way; chairs, benches and rugs can be walked over
- [ ] Torches, candelabras, braziers and fireplaces flicker and light the room (best seen at night)
- [ ] The room's name changes with what's in it (try a bed, then three beds; a table and a chair)
- [ ] The top right shows "Villagers: 3 (homes for 3)": each bed adds a home
- [ ] Right click on something built tears it down and gives half its cost back
- [ ] A chest, barrel or crates hold resources; builders fetch from them

## Step 4. Survival

1. Press **Esc → Main menu** (or Play the MainMenu scene again), and click
   **Survival**. (Pressing Play in the **Kingdom** scene also starts survival
   straight away.)
2. The top left shows your health and "The first wave comes in 2:00". Build
   walls and a door round the throne and storehouse. Press **N** when you're
   ready, or wait.
3. Extra controls:

| Input | Does |
|---|---|
| N | Call the next wave now |
| Esc | Pause |
| Walk over a bag | Pick up loot |

4. Check these:

- [ ] "Wave 1: 6 zombies are coming from the ..." appears, and zombies come from that side of the map
- [ ] Zombies walk round trees and rocks, and head for you or a villager
- [ ] Zombies at a wall or door hit it: wood splinters, stone gets scratched; after a while the wall caves in or the door breaks
- [ ] Zombies go for the weakest way in (a gap, a door or a wooden wall) rather than straight through stone
- [ ] Standing right behind your wall, your rifle shots fly over it and hit the zombies outside
- [ ] Zombies flash red when hit, leave blood, and fall down dead; the bodies fade after a while
- [ ] Rockets and C4 kill groups of zombies (and hurt you if you're too close)
- [ ] Some zombies drop a bag; walking over it puts it in your backpack
- [ ] Zombies hurt you: the health bar goes down and the screen flashes red; after a few seconds without being hit it heals
- [ ] Zombies can't be walked through; a crowd can trap you
- [ ] Villagers run when they see a zombie, and come back to work when it's safe
- [ ] Sitting on the throne while zombies attack it stands you up ("You're under attack!")
- [ ] "Wave 1 survived!" then a countdown to wave 2; with beds, survivors join you
- [ ] Later waves are bigger; runners (wave 3) and brutes (wave 5) show up; brutes smash walls fast
- [ ] At night zombies are faster
- [ ] When you fall: "You have fallen" with your wave, time and kills; **Try again** starts over
- [ ] Esc pauses the game (and stands you up first if you're on the throne)
- [ ] With a big wave (wave 10+, press N a few times), the game still runs smoothly (F3 shows the frame rate)

## Step 5. If something looks wrong

- **Zombies walk through walls or get stuck:** note where (a screenshot of the
  throne view helps) and tell Claude.
- **No zombies come:** check the Console for a red or yellow message and send it.
- **A wave never ends:** a zombie may be stuck somewhere out of sight. Zombies
  that can't reach anyone wander off after 25 seconds, so wait a little; if it
  still doesn't end, tell Claude.
- **The game stutters with many zombies:** lower **Max Alive** in
  `Assets/Data/Zombies/SurvivalConfig` (default 120) and tell Claude the wave number.
- Everything from Step 5 of `docs/PHASE-2B-TEST.md` (dark corners missing, out of
  video memory, blurry pictures) still applies.

## Step 6. Tuning (optional)

Click an asset in `Assets/Data` and change it in the **Inspector**:

| Asset | What to try |
|---|---|
| `Zombies/SurvivalConfig` | **Prep Seconds** (time before the first wave), **First Wave Zombies**, **Extra Zombies Per Wave**, **Growth**, **Break Seconds**, **Max Alive**, **Night Speed/Damage**, starting resources |
| `Zombies/Walker`, `Runner`, `Brute` | Health, speed, damage, **Wall Damage**, which wave they start in, how common they are, loot |
| `Destruction/StoneWall`, `WoodPalisade` | **Siege Health**: how much zombie battering one tile takes (stone 600, wood 200) |
| `Combat/Rifle`, `RocketLauncher` | **Hit Damage** to zombies, **Over Walls** (how far shots fly over walls) |
| `Destruction/RocketExplosion`, `C4Explosion` | **Creature Damage** and **Blast Radius** |
| `Character/PlayerConfig` | **Max Health**, how fast you heal |
| `Villagers/VillagerConfig` | Villagers' health, how close a zombie has to be before they run |
| `Building/...` | Costs, build time and health of every door, floor, piece of furniture and light |

## Step 7. Save and tell Claude

In Git Bash:

```bash
git add -A
git commit -m "Castle and survival update: scenes built and tested"
git push
```

Then send Claude the ticked lists, screenshots, how far you got in survival,
and anything that felt wrong or not fun.
