# Testing the new gameplay

Each part below is one of the new gameplay ideas. None of it has been run in Unity yet:
the code compiles and its rules are unit-tested, but how it plays is untested until you
try it. Run **KingdomGame > Build Game Scenes** after `git pull` first.

The cheat menu (**F10**, offline) helps: game speed 4x, time of day, resources.

## 1. Food and sleep

Villagers get hungry and tired now.

- **Food:** a villager eats a meal at a storehouse when they're getting hungry: **3 food**,
  or **2** if you have a **kitchen** (a cauldron, or any age's hearth, oven or stove,
  anywhere). Each one eats about twice a day. A new kingdom starts with 60 food and one
  of the first villagers is a forager.
- **Sleep:** everyone goes to sleep at night (guards sleep in the middle of the day and
  keep watch at night). With a free **bed** they sleep in it (out of sight; it heals them
  too) and wake up rested. Without one they sleep on the ground by the throne, lying
  down, and wake up tired: by the evening they're slow.
- **Hungry or tired** villagers work and walk at 75%; **starving or exhausted** at 50%.
  **Content** villagers (fed and rested) work 10% faster if you have a **dining hall**:
  a room (walls and a door) with a table and a chair, bench or stool in it.
- Zombies wake sleepers up (they run). Guards get up for their towers when zombies come.

Try it:

1. Start a free build. **V** opens the villagers panel: under the jobs there's a new
   *Food and sleep* part (how many are hungry or tired, meals, food left in days, beds).
2. Sit on the throne and hover the mouse over a villager: it says what they're doing,
   how fed and rested they are and how fast they're working.
3. **F10 > Time > Evening**, game speed 4x. When night falls, villagers stop work and
   lie down by the throne (no beds yet). In the morning they get up.
4. Build a bed (Furniture tab; it holds two), then night again: two villagers go to
   bed and disappear into it; the third lies by the throne.
5. Build a cauldron: the panel says meals take 2 food.
6. Wall in a room with a door, put a table and a chair in it: the panel says you have
   a dining hall; a fed, rested villager's hover says "content, working at 110% speed".
7. **F10 > Villagers > Make everyone hungry and tired**: they drop their work and go to
   eat, and hovering says they're slow. When the storehouse has no food left (rugs and
   beds take food: build some), the status at the top right says they're going hungry.
8. Save and load: how fed and rested each one was comes back.

Tell me if villagers get stuck going back and forth, never go to bed, or a bed shows a
villager standing on it.
