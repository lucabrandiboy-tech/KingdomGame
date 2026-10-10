# Phase 2, steps A + B: how to test them

What's new since Phase 1:

- **A new look: dark, realistic, seen from straight above,** drawn in high
  detail (1024 pixels per tile; the old art had 64). New ground with big
  patches of dry grass and dirt, trees, rocks, bushes, rough fieldstone walls
  and wooden palisades (log ends). Soft shadows everywhere. Fires (braziers,
  torches, a lantern) light up the ground around them, and torchlight shows the
  bumps of stones and bark. Dark corners, glow on fire and sparks.
- **A wider view:** you see 24 tiles from top to bottom (it was about 11). The
  mouse wheel zooms in and out; zoom right in to see the fine detail.
- **The day starts in the late afternoon,** so night comes after about a minute.
- **A top-down king** in a red cape and crown, who turns to face the mouse.
- **Walls break into little 3D-looking cubes** that tumble, fly up, cast shadows,
  bounce and become rubble. Explosions flash, leave smoke and burn the ground.
- **Rust-style walls:** bullets barely scratch stone. Use **rockets** (right
  click) or **C4** (G).
- **Gathering:** hold **E** at a tree, rock or bush for wood, stone or food.
- **Day and night** (12 minutes per day).
- **Your kingdom:** a **throne**, a **storehouse** with some wood and stone in
  it, and **3 builder villagers**.
- **Building:** sit on the throne (**E**) to see your whole kingdom. Plan stone
  walls, wood walls and new storehouses on the grid. Villagers fetch the
  resources from the storehouse, carry them over and build.

> **Not tested in Unity yet.** Everything compiles, and the path finding and
> destruction math pass their tests, but nobody has pressed Play on this
> version. Expect a few things to be off. Send a screenshot of anything odd and
> any red errors from the **Console** window (Window > General > Console).

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

1. Open the project in Unity Hub. Wait for it to finish compiling (the
   spinner in the bottom right corner stops).
2. Menu: **KingdomGame → Build Game Scenes**. If it asks to save the current
   scene, click **Don't Save**.
3. **The first time, this takes several minutes** (maybe up to 10 on a slower
   computer): a progress bar says "Baking the art". It draws every picture in
   full detail and saves them in `Assets/Art/Generated`. Leave Unity alone
   until it's done. It only does this again when the art changes.
4. It also makes the new settings files in `Assets/Data` (keeping any you
   already have; older look settings get the new look), rebuilds the
   **Kingdom** and **StressTest** scenes in `Assets/Scenes`, and opens **Kingdom**.
5. The **Console** (Window > General > Console) should say "Baked the art: 13
   texture sheets". If it shows a red error instead, send Claude a screenshot of it.

## Step 3. Walk around

1. Click the **Game** tab. In its toolbar, drag **Scale** all the way to the
   left (**1x**); anything bigger blows the picture up and makes it blurry.
   In the dropdown that says **Play Focused**, pick **Play Maximized**, so the
   game fills the Unity window when you press Play.
2. Press **▶ Play**. Building the map takes a few seconds.
3. You start just below the throne. Controls (also in the bottom left;
   **H** hides them):

| Input | Does |
|---|---|
| W A S D | Walk |
| Mouse | Aim (the king turns to face it) |
| Mouse wheel | Zoom in and out |
| Left mouse (hold) | Rifle |
| Right mouse | Rocket |
| G | Throw C4 at the mouse (up to 4 tiles away) |
| E | Use what's next to you (see the hint at the bottom of the screen) |
| F3 | Performance info (FPS) |
| H | Hide/show the controls |

4. Check these and tell Claude which work:

- [ ] Dark olive grass with big patches of khaki dry grass and dirt; their edges look ragged and natural, not like cut-out shapes
- [ ] Trees, rocks and bushes with soft shadows; zoomed right in, they're sharp and detailed
- [ ] The corners of the screen are darker than the middle (that's the "vignette")
- [ ] The king turns smoothly to face the mouse, and his legs move when he walks
- [ ] You see a lot of the map (about 24 tiles tall); the wheel zooms
- [ ] Walk under a tree: its leaves fade so you can see yourself
- [ ] Rifle on a stone wall: sparks, but no real hole even after a long burst
- [ ] Rocket on stone: a dent with a flash, smoke and a dark scorch mark; two or three rockets break through
- [ ] Little cubes fly off the wall, tumble, cast shadows and stay on the ground as rubble
- [ ] C4 thrown at a wall sticks, blinks faster and blows a big hole
- [ ] Wood (the east side of the test outpost) breaks more easily than stone
- [ ] Hold E at a tree: wood in your backpack (top right); it becomes a stump when empty
- [ ] Wait about a minute: it gets dark (full night after about 2 minutes); the king has a light, the braziers and the outpost's torches glow and light the ground warmly around them

## Step 4. Build with your villagers

1. Walk to the storehouse (the thatched hut to the left of the throne) and
   press **E**: your backpack goes into storage. The top right shows what's
   in storage.
2. Walk to the throne and press **E** to sit. The camera pulls far out.
3. In the throne view:

| Input | Does |
|---|---|
| 1, 2, 3 or the buttons at the bottom | Pick stone wall, wood wall or storehouse (press again to put it away) |
| Left click | Place it. **Walls: hold the button and drag** to place a whole line |
| Right click | Stop placing; or, with nothing picked, cancel the blueprint under the mouse (you get its resources back) |
| W A S D | Move the view |
| Mouse wheel | Zoom |
| E or Esc | Stand up |

4. Check these:

- [ ] Sitting on the throne zooms the camera out; E stands you back up
- [ ] Picking a wall shows a green square under the mouse (red where you can't build, with the reason at the bottom)
- [ ] Dragging places a straight line of see-through blueprints
- [ ] Villagers walk to the storehouse, carry stone or wood to the blueprints (with a sack on their back), then build; a yellow bar fills up
- [ ] Finished walls appear and can be shot and blown up like the others
- [ ] Villagers walk around walls, trees and rocks, and don't get stuck
- [ ] Plan more walls than you have stone for: they wait ("Waiting for ... stone in storage"); gather stone, put it in the storehouse, and they continue
- [ ] Right click on a blueprint cancels it
- [ ] Standing next to a blueprint and holding E hands over resources from your backpack, then helps build
- [ ] A new storehouse can be built (60 wood, 20 stone) and villagers use it

## Step 5. If something looks wrong

- **No dark corners or glow at all:** the project's 2D renderer may have
  post-processing switched off. In the **Project** window, find the
  **Renderer2D** asset (search "Renderer2D"), click it, and in the
  **Inspector** tick **Post-processing → Enabled** if it's there. Tell Claude
  either way.
- **Pink or invisible sparks and fire:** send a screenshot of the Console.
- **Everything is very dark even at noon:** say so; the look settings are easy to change.
- **The game stutters, or Unity says it's out of memory:** the art uses about
  0.55 GB of video memory. Click `Assets/Data/Look/ViewConfig`, set **Art
  Detail** to 512 in the Inspector, run **KingdomGame → Build Game Scenes**
  again, and tell Claude.
- **Pictures look blurry:** check the Game tab's **Scale** is at **1x**.

## Step 6. Tuning (optional)

All the numbers are in `Assets/Data`. Click an asset and change it in the
**Inspector**:

| Asset | What to try |
|---|---|
| `Look/ViewConfig` | **Walk Zoom** (bigger = see more), **Throne Zoom**, **Shadow Opacity**, **Art Detail** (then Build Game Scenes again) |
| `Look/PostProcessing` | Vignette, Bloom and Color Adjustments (contrast, saturation) |
| `Building/StoneWall`, `WoodWall`, `Storehouse` | Costs and **Build Seconds** |
| `Building/KingdomConfig` | Starting wood/stone/food and villagers |
| `Villagers/VillagerConfig` | Villager speed, how much they carry |
| `World/DayNightConfig` | **Minutes Per Day**, day and night colors and brightness, fire brightness and reach |
| `Destruction/DestructionSettings` | Cube size and how many cubes fly off (**Cube Density**) |
| `Destruction/StoneWall`, `WoodPalisade` | How tough walls are |

## Step 7. Save and tell Claude

In Git Bash:

```bash
git add -A
git commit -m "Phase 2A+2B: game scene built and tested"
git push
```

Then send Claude the ticked lists, screenshots, and anything that felt wrong
or not fun. Next is step **2C**: villagers need food, homes and happiness,
zombies attack (stronger at night), and the ruler can die and respawn at the throne.
