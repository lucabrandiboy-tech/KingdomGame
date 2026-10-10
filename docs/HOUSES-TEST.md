# Houses and roofs: how to test them

Two new ways to build homes:

- **Houses** (the new **Houses** tab): a 3 x 3 building with a door. Walk up to
  the door and press **E** to go inside. The inside is a room of its own,
  **bigger than the house looks from outside**: a Stone Age hut is 5 x 4 tiles
  inside. Put anything in it: furniture, lights, chests, floors, rugs.
  When your kingdom reaches the next age, **upgrade** the house: builders bring
  what it costs and work on it for a while (it doesn't finish by itself). When
  they're done the room inside is bigger and the house looks like the new age.
  The house on the map stays 3 x 3.

  | Level | Age | Inside | Roof |
  |---|---|---|---|
  | Hut | Stone | 5 x 4 | thatch |
  | Mudbrick house | Bronze | 7 x 5 | wood shingles |
  | Timber house | Iron | 9 x 6 | clay tiles |
  | Stone house | Medieval | 11 x 7 | slate |
  | Manor | Renaissance | 13 x 8 | copper |
  | Brick townhouse | Industrial | 15 x 9 | tin |
  | Concrete house | World War 1 | 17 x 10 | tar felt |
  | Bunker house | World War 2 | 19 x 11 | concrete |

- **Roofs** (the **Floors & roofs** tab): build walls and a door round a room
  the old way, then drag a roof over it. It's drawn for the room's shape (a
  hip roof with ridges, like a real one) and fades away when you walk under it.
  Each age has its own roof. A roof needs a wall or door within 4 tiles to
  hold it up, and falls in when those walls are gone.

You can still put a wall round everything: build your houses, then a ring of
walls and a gate round them.

> **Not tested in Unity yet.** It compiles and the house and roof pictures were
> checked outside Unity, but nobody has pressed Play on it. Send a screenshot
> of anything odd and any red errors from the **Console** (Window > General > Console).

## Step 1. Get it and build the scenes

Close Unity, then in Git Bash:

```bash
cd /c/Dev/KingdomGame
git pull
```

Open the project, wait for it to compile, then **KingdomGame → Build Game
Scenes** (click **Don't Save** if it asks). The Console should say it added
houses to your build bar.

## Step 2. Free build: build a house

1. Open `Assets/Scenes/MainMenu`, press **▶ Play**, click **Free build**.
   (Free build lets you upgrade straight away, so you can try every level.)
2. Sit on the throne (**E**). Click the **Houses** tab, pick **House**.
   **R** turns it (the grey step shows where the door is). Click to place it.
   Builders bring 40 wood, 10 stone and 5 food and build it.
3. Stand up (**E**), walk to the house's door, press **E** to go in.

| Where | Key | Does |
|---|---|---|
| At a house's door | E | Go in / go out |
| Inside a house | B | Furnish it (the build bar opens; Esc, B or E when you're done) |
| Inside a house | U | Upgrade it |
| On the throne, mouse over a house | F | Look inside and furnish it from the throne (Esc goes back) |
| On the throne, mouse over a house | U | Upgrade it |
| On the throne, mouse over a house | Right click | Cancel its upgrade, or tear it down (everything inside too, half the cost back) |

Check these and tell Claude which work:

- [ ] The house shows a roof, a chimney and a step at the door; R turns it before you place it
- [ ] E at the door takes you inside: a room with walls, a floor and the door at the bottom
- [ ] Inside, E by the door takes you back outside, in front of the house
- [ ] B inside opens the build bar with only furniture, lights, storage, floors and rugs
- [ ] Furniture you place inside gets built: villagers walk into the house to do it
- [ ] A chest inside works like any chest (builders fetch from it)
- [ ] U upgrades the house: builders bring the resources and work on it; when they're done the room is bigger, the house has a new roof, and the furniture is where you left it
- [ ] On the throne, hovering a house says its name, its size inside and what upgrading costs
- [ ] Beds in a house give homes (the top right "homes for" goes up)
- [ ] Tearing a house down gives back half its cost and half of what was inside

## Step 3. Free build: roofs

1. On the throne, build a small room: walls all round and a wooden door.
2. **Floors & roofs** tab: pick a roof, hold the left mouse button and drag over
   the room (walls included, or just the inside).

- [ ] The roof appears over the room once builders finish it (cell by cell)
- [ ] It looks like a roof with ridges (an L-shaped room gets an L-shaped roof)
- [ ] On the throne roofs are see-through, so you can still place furniture
- [ ] Walking into the room makes its roof fade away; walking out brings it back
- [ ] Placing a roof far from any wall says it needs a wall within 4 tiles
- [ ] Tearing down the walls makes the roof fall in
- [ ] Shift + right click on a roof (throne view) takes the whole roof off

## Step 4. Survival: hiding in a house

1. Start **Survival**, build a house, go inside when a wave comes.

- [ ] Zombies come for the house and hit it (a bar over it shows its health)
- [ ] When the house breaks, you're put outside and the things inside are lost
  (what chests held is left in a bag by the door)
- [ ] Zombies now break storehouses and chests too (before, they hit them forever without doing damage)

(Upgrades will wait for the next age once the ages are in the game: that's the
next step. For now every level can be built straight away, in both modes.)

## Step 5. Save and tell Claude

```bash
git add -A
git commit -m "Houses tested"
git push
```

Then send Claude the ticked lists and screenshots.
