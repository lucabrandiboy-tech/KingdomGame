# Testing the endless world

Every new game makes a new random world from a seed (World Config > Seed: 0 = random,
any other number = always the same world). The world has no map edge you'll normally
reach: it goes **32,000 tiles** from the middle each way, which is nearly **3 hours of
walking** from the middle to the edge. (It isn't truly infinite: much further out, the
numbers a computer keeps positions in get too coarse and things start to shake.)

Nothing here has been run in Unity yet. What I could test outside Unity: the tree/rock
data (the same world every time from the same seed, also at negative positions; saved
changes come back; old squares are forgotten without losing changes) and the ground
squares joining up exactly (`tools/world-tests`). Everything you see on screen is untested.

## What changed

- **Your kingdom has lands**: a square 512 tiles across round your throne (Kingdom Config >
  Lands Size). You build there, your villagers work and gather there, and zombies find their
  way there. Building further out says "Too far from your throne: your kingdom's lands end here".
  You can still walk anywhere.
- The ground is made in squares of 64 tiles round the camera, a couple per frame, on a
  background thread. Trees, rocks and bushes appear round you as before.
- Saves keep only what changed (cut trees, cleared spots, planted trees), plus the seed.
- World Config's Map Size and Network Config's Map Size are gone.

## How to test

1. Run **KingdomGame > Build Game Scenes** (it updates your settings: online kingdoms are
   now 560 tiles apart so their lands don't overlap).
2. Play free build. You should see the usual start: grass, forests, the test outpost.
3. **Walk a long way** in one direction (a couple of minutes). Watch for:
   - Ground appearing late or squares popping in at the screen edges (tell me).
   - Stutters every few seconds (tell me; F3 shows the frame rate).
   - Visible lines where ground squares meet (tell me, with a screenshot).
   - Trees, rocks and bushes all along the way.
4. Zoom the throne view far out (scroll). The ground should still cover the screen.
5. Try to build far from the throne: you should get the "lands end here" message.
6. Cut a few trees far from home, **Esc > Save and go to the main menu**, Continue: you
   should be back where you were, with the trees still cut.
7. Start two new games: the worlds should look different. Put a number in World Config >
   Seed and start twice: the same world both times.

## Saves from before

A game saved before this change loads, with everything you built, but the trees and
ground round it are new (the world is made differently now). Hosted games from before
didn't save the players who joined, so they get new kingdom spots when they come back.
Nothing else should be lost; tell me if it is.
