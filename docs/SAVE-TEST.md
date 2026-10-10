# Testing saving and loading

Nothing here has been run in Unity yet. It compiles, but that's all that's
been checked. Please go through this list and tell me what goes wrong.

## Where saves go

One save for survival and one for free build, in Unity's save folder for the game:
`C:\Users\<you>\AppData\LocalLow\<Company Name>\<Product Name>\saves\`
(`survival.json`, `freebuild.json`, plus a `.bak` copy of the save before it).
Company and Product Name are in Edit > Project Settings > Player.

## When it saves

- **Esc > Save the game.**
- **Every 3 minutes** (World Config > Autosave Minutes; 0 turns it off).
- **Esc > Save and go to the main menu**, and when you **quit** (stopping Play mode counts).
- A **new** game only saves over your old one after it has been played for a
  minute. Start one by mistake, quit straight away, and your old save is safe.
- It won't save once you've fallen in survival.

## How to test

1. Run KingdomGame > Build Game Scenes, open the MainMenu scene, press Play.
2. Free build: build a house, upgrade it, put furniture inside, build walls with a door,
   a roof, floors, a storehouse, plant trees, cut some trees down, dig out part of a
   mine and leave some rock marked, and leave a blueprint half built.
   Give villagers different jobs. Walk into the house.
3. Esc > Save the game. You should see "Game saved".
4. Esc > Save and go to the main menu. There should be a **Continue free build** button
   showing the day and when it was saved.
5. Click it. You should be back inside the house, with everything above as it was:
   the same map, the same trees gone, villagers with the same jobs, the mine dug the same.
6. Survival: play until wave 2 or 3, save between waves, quit, then Continue survival.
   The wave number, kills and countdown should carry on. If you save **during** a wave,
   loading starts that wave again after 20 seconds (zombies aren't saved).

## What isn't saved (on purpose, for now)

- Zombies that are out, and bodies on the ground.
- Bullet and explosion holes in walls. Walls come back whole, but keep the damage zombies did.
  A wall with pieces blown off comes back as just the pieces that are left.
- What villagers were halfway through doing. They pick a new task when the game loads,
  and anything they were carrying goes back to storage.
- Where traps had gone off. They come back armed.

If you rename a building, wall material or tree asset, old saves lose that kind of thing.
The Console says how many things couldn't be put back.
