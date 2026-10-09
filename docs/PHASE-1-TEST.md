# Phase 1: Destruction prototype, how to test it

Do this **after Phase 0 works** (every line in the Setup Check window says PASS).

What Phase 1 adds:

- Your ruler, walking with **WASD** and aiming with the **mouse**
- A **rifle** (left click) that chips small holes, a **rocket launcher**
  (right click) that blows craters, and a **smash** (Space) that knocks out
  blocky chunks like a brute zombie will
- Stone and wooden walls that break **pixel by pixel** where they're hit, with
  pixels flying off and settling as rubble. You can walk and shoot through
  the holes. Pieces that get cut off crumble, and badly damaged walls collapse.
- A **stress test** scene that blows up lots of walls and shows the frame rate

> **Not tested in Unity yet.** The code compiles and the destruction math
> passes its unit tests, but nobody has pressed Play on it. Expect a problem or
> two. Send Claude a screenshot of anything odd, plus any red Console errors.

---

## Step 1. Get the new files

1. **Close Unity** if it's open.
2. In **GitHub Desktop**, click **Fetch origin**, then **Pull origin**.
3. Open the project in Unity Hub. Unity compiles the new scripts (a few seconds).
   If the **Console** window shows red errors, stop and send them to Claude.

## Step 2. Make the test scenes

1. In the menu bar: **KingdomGame → Phase 1 → Create Test Scenes**.
   If Unity asks to save the current scene, click **Save**.
2. This creates:
   - settings assets in `Assets/Data/Phase1` (damage types, wall materials,
     weapons, player speed). You'll tune these later.
   - two scenes in `Assets/Scenes`: **Phase1_Destruction** and **Phase1_StressTest**
3. **Phase1_Destruction** opens by itself.

## Step 3. Play with the walls

1. Double-click the **Game** tab so it fills the window, then press **▶ Play**.
2. Controls:

| Input | Does |
|---|---|
| W A S D | Walk |
| Mouse | Aim |
| Left mouse (hold) | Rifle |
| Right mouse | Rocket |
| Space | Smash the wall in front of you |
| H | Hide/show the info box |

3. Check each of these. Tick the ones that work, note the ones that don't:

- [ ] Walls are visible: grey stone bricks, one brown wooden palisade, a 3×3 stone tower
- [ ] The ruler walks and can't walk through walls
- [ ] Rifle shots chip small holes exactly where they hit; pixels fly off
- [ ] Rockets blow big craters; the screen shakes
- [ ] Space knocks out a blocky chunk
- [ ] Flying pixels land and stay as darker rubble on the ground
- [ ] A hole all the way through a wall lets bullets **and** the ruler through
- [ ] Small pieces that get cut off crumble away
- [ ] A wall with most of it destroyed collapses completely
- [ ] Wood breaks faster than stone
- [ ] The info box shows FPS and numbers that change as you shoot

4. Press **▶** again to stop.

## Step 4. Run the stress test

1. In the **Project** window: **Assets → Scenes**, double-click **Phase1_StressTest**.
2. Press **▶ Play**. 60 walls appear and explosions start going off by themselves.
3. Keys: **T** pauses/resumes explosions, **X** blows up every wall at once,
   **R** rebuilds all the walls.
4. Every 5 seconds a summary line appears in the **Console** window, like:
   `[Stress test] 144 FPS, worst frame 9.3 ms, 75 explosions in the last 5 s, ...`
5. Do this and **write down the numbers** (or screenshot the Console):
   - the FPS after about 20 seconds of automatic explosions
   - the worst frame right after you press **X**
6. Also note your graphics card and CPU (Windows key → type **System Information**,
   or Task Manager → Performance).

**Important:** the Unity Editor itself is slow. A real game build is usually
noticeably faster, so editor numbers are a worst case. That's fine for now.

## Step 5. Commit and tell Claude

1. GitHub Desktop: you'll see the new assets in `Assets/Data/Phase1` and the two
   scenes in `Assets/Scenes`, with their `.meta` files. Summary:
   `Phase 1: test scenes created and tested`. **Commit to main**, then **Push origin**.
2. Send Claude:
   - which items in the Step 3 list work and which don't
   - the stress test numbers from Step 4, and your graphics card/CPU
   - screenshots of anything that looks wrong, and any red Console errors

## Tuning (optional)

Click any asset in `Assets/Data/Phase1` and change numbers in the **Inspector**.
Changes made while playing are kept for these assets (they're files, not scene objects).

| Asset | Try |
|---|---|
| **Bullet**, **Explosion**, **BruteSmash** | Hole radius, damage, raggedness, how many pixels fly, screen shake |
| **StoneWall**, **WoodPalisade** | Pixel health (toughness), colors |
| **Rifle**, **RocketLauncher** | Fire rate, shot speed, range, spread |
| **PlayerConfig** | Walking speed |
| **DestructionSettings** | Flying pixel limit, rubble on/off, when walls crumble and collapse |
| **StressTestConfig** | Number of walls, explosions per second |

Running **KingdomGame → Phase 1 → Create Test Scenes** again rebuilds the two
scenes but keeps your tuned assets.
