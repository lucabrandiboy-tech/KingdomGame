# Start here: one test session for everything

Nothing in the game has been run in Unity yet: every update was written and checked
here (the scripts compile, the unit, world and network tests pass), but Unity can't
run in the cloud. This is the order to try it in. The first half hour covers every
update; the other test guides (listed at the end) go into the details.

**If something goes wrong:** copy the **first red line** in Unity's Console window
(click it, then copy the text at the bottom) and send it to me, with what you were
doing. Later red lines are often caused by the first one. Screenshots help too.

## 0. Get the latest version (2 minutes)

1. Open **Git Bash** in your project folder and type `git pull`.
2. Open the project in Unity and wait until the spinner at the bottom right stops.
3. Look at the **Console** (Window > General > Console). Red errors here mean the
   scripts didn't compile: send me the first one and stop.

## 1. Build the game scenes (a few minutes)

1. Menu bar: **KingdomGame > Build Game Scenes**. It draws every picture in the game,
   so it can take several minutes. Do this after every `git pull`: it adds new things
   to your settings and keeps the ones you changed.
2. In the **Project** window, open `Assets/Scenes`, double-click **MainMenu**, press **▶ Play**.

You should see the main menu with Survival, Free build, Play online and Settings.

## 2. Settings (1 minute)

Main menu > **Settings**: graphics **Low / Medium / High** and two volume sliders (sound,
and background sound like wind and birds). It's also in the **Esc** menu during a game.
Your choice is kept on this computer.

- If the game is slow or Unity runs out of memory, try **Medium** or **Low**.

## 3. Free build: building (10 minutes)

Click **Free build**. Walk with **WASD**; **H** shows all the keys.

1. Walk to your throne and press **E** to sit on it: the **build bar** opens.
   - **Tab** changes tab, **R** turns a thing before you place it.
   - The **search box** finds anything by name (type in it, **Enter** to finish,
     **Esc** clears it).
   - Things you haven't researched are hidden; click **Show locked** to see them.
   - **Hover** over anything for a box with its cost, size, building time and what it does.
2. Place a few wall tiles, a door and a house. Your villagers carry the wood and stone
   and build them. **V** opens the villagers' jobs.
3. **Listen:** chopping, picking, hammering, doors, wind and birds by day, crickets at
   night. (All sounds are made by code, so they're simpler than recorded ones. Tell me
   any that are annoying or too loud.)
4. Beds: one bed is a home for **two** villagers now.

## 4. The cheat menu, for testing (5 minutes)

Press **F10** (offline games only). From there:

- add resources and XP, **jump to any age**, **finish all building now**, finish the
  current research (or **F8**), no damage, heal,
- set the time of day and the game speed (1x / 2x / 4x),
- in survival: start any wave, send 5 of any kind of zombie, remove all zombies.

Use it to try the later ages quickly: jump to a late age, then look through the build
bar for the new **towers** (two more per age, and cheaper to build), the new
**furniture** in every age, and machines. **F3** shows the frame rate.

To switch the cheats off: `Assets/Data/Progression/ProgressionConfig` > untick
**Allow Cheats** in the Inspector.

## 5. Zombies (5 minutes)

1. In free build press **F9** for a test horde, or start **Survival** from the main menu
   (**N** calls the next wave early; F10 > start a wave lets you skip ahead).
2. F10 > **send 5** of each new kind and watch what they do (see
   `docs/ZOMBIE-KINDS-TEST.md` for what each one should do).
3. Armored zombies: bullets do less, but **fire, gas and traps** now do their full damage.
4. Listen for groans, blows on walls, the wave horn.

## 6. Save and load (2 minutes)

**Esc > Save the game**, go to the main menu, then **Continue free build** (or
**Continue survival**). Everything should be where it was. More in `docs/SAVE-TEST.md`.

## 7. Online with a friend (or two copies on one computer)

Follow `docs/ONLINE-TEST.md`: part 1 to connect, then **part 4**: everyone who joins now
has a kingdom of their own and sees the others'. Joined players have no zombies yet,
and others' walls can't be damaged; that's the next online step.

## The detailed guides

| Guide | What it covers |
|---|---|
| `CASTLE-SURVIVAL-TEST.md` | rooms, doors, furniture, lights, survival waves, main menu |
| `PHASE-2B-TEST.md` | throne view, building, storehouses, builder villagers |
| `HOUSES-TEST.md` | houses you go into, levels per age |
| `JOBS-TEST.md`, `MINES-TEST.md`, `MACHINES-TEST.md` | villager jobs, mines, machines |
| `WEAPONS-TEST.md`, `DEFENSES-TEST.md` | the ruler's weapons, towers and traps |
| `AGES-CONTENT-TEST.md` | what each age adds |
| `ZOMBIE-BASES-TEST.md`, `ZOMBIE-KINDS-TEST.md` | zombie camps and sieges, kinds of zombies |
| `SAVE-TEST.md` | saving and loading |
| `ENDLESS-WORLD-TEST.md`, `WORLD-CHANGES-TEST.md` | the endless world |
| `ONLINE-TEST.md` | hosting, joining, everyone's kingdoms |
| `GAMEPLAY-TEST.md` | the newest gameplay: food and sleep, seasons and weather, fire, deer and wolves, the Warlord boss, survivors at the gate, ruins, goals for each age, trading and defending friends online |
