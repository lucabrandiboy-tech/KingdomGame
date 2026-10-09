# Phase 2, step A: how to test it

What's new:

- **Twice the detail:** every tile is now 64 × 64 pixels (it was 32). New grass
  ground, stone and wood walls, trees, rocks and berry bushes.
- **A new king:** slimmer, with a walking animation; turns to face where you
  aim; walks slower.
- **Rust-style walls:** bullets barely scratch stone (you'll see sparks, not
  holes). You need explosives: **rockets** (right click) or **C4** (press **G**
  to throw it; it sticks to the wall and blows after 4 seconds).
- **A real map:** forests, rocky clearings and berry bushes, different every
  time you press Play. Hold **E** next to a tree, rock or bush to gather
  wood, stone or food into your backpack (it holds 250 in total).
- **Day and night:** a whole day takes 12 minutes. Nights get dark and the
  king carries a warm light.
- A small walled **test outpost** just east of where you start, for trying the guns.

> **Not tested in Unity yet.** It compiles and the destruction math passes its
> tests. Send a screenshot of anything odd and any red Console errors.

---

## Step 1. Get the new files

**Close Unity first.** Then in Git Bash:

```bash
cd /c/Dev/KingdomGame
git pull
```

If `git pull` complains about local changes, send Claude a screenshot.
Don't delete anything.

## Step 2. Build the game scene

1. Open the project in Unity Hub. Wait for it to finish compiling.
   (Warnings about a **missing script** in the old Phase 1 scene are expected.
   The next step removes that scene.)
2. Menu: **KingdomGame → Build Game Scenes**. If it asks to save the current
   scene, click **Don't Save**.
3. This makes the settings files in `Assets/Data`, the **Kingdom** and
   **StressTest** scenes in `Assets/Scenes`, and removes the old Phase 1 scenes.
   **Kingdom** opens by itself.

## Step 3. Play

1. Double-click the **Game** tab so it fills the window, then press **▶ Play**.
   Building the map takes a second or two.
2. Controls (also shown in the bottom left; **H** hides them):

| Input | Does |
|---|---|
| W A S D | Walk |
| Mouse | Aim |
| Left mouse (hold) | Rifle |
| Right mouse | Rocket |
| G | Throw C4 at the mouse (up to 4 tiles away) |
| Hold E | Gather from the tree, rock or bush you're standing next to |
| F3 | Performance info (FPS) |
| H | Hide/show the controls |

3. Check these and tell Claude which work:

- [ ] Grass ground, trees, rocks and bushes look sharp and detailed
- [ ] The king is slimmer, walks with moving legs, and faces left or right with the mouse
- [ ] Walking speed feels right (not too fast)
- [ ] You walk behind the leaves of a tree but in front of its trunk
- [ ] Rifle on a stone wall: sparks, but no real hole even after a long burst
- [ ] Rocket on stone: a dent; two or three rockets in the same spot break through
- [ ] C4 thrown at a stone wall sticks, blinks faster, and blows a big hole
- [ ] Wood (the east side of the outpost) breaks more easily than stone
- [ ] Holding E at a tree gives wood (top right counter), and it becomes a stump when empty
- [ ] Rocks give stone and disappear when empty; bushes give food and lose their berries
- [ ] "Backpack full" shows up at 250
- [ ] Wait for evening (about 7 minutes in, or see Step 4): it gets dark and orange, then blue, and the king has a light

4. Press **▶** again to stop. You get a different map every time you press Play.

## Step 4. Optional tuning

All the numbers are in `Assets/Data`. Click an asset and change it in the
**Inspector**:

| Asset | What to try |
|---|---|
| `World/DayNightConfig` | **Minutes Per Day** (try 2 to see a night quickly), **Start Hour** (try 18 to start at sunset) |
| `Character/PlayerConfig` | **Move Speed**, **Backpack Capacity** |
| `Destruction/StoneWall`, `WoodPalisade` | **Pixel Health** and how much of each kind of hit gets through |
| `Destruction/Bullet`, `RocketExplosion`, `C4Explosion` | Hole size and damage |
| `Combat/C4` | Fuse time, throw range |
| `World/WorldConfig` | Map size, how much forest, rocks and bushes; **Seed** to get the same map again |
| `Economy/Tree`, `Rock`, `BerryBush` | How much they hold and how fast you gather |

## Step 5. Save and tell Claude

In Git Bash:

```bash
git add -A
git commit -m "Phase 2A: game scene built and tested"
git push
```

Then send Claude the ticked list, screenshots, and anything that felt wrong or
not fun. The next step is the **throne**: sit on it to see your whole kingdom,
place buildings on the grid, and have builder villagers put them up with the
wood and stone you gathered.
