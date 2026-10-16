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

## 2. Seasons and weather

The year goes **spring, summer, autumn, winter**, three game days each (a game day is
12 minutes, so a season is about 36 minutes and a year about 2.4 hours). The weather
changes every few game hours. Both come from the world's seed and the clock, so everyone
in an online game has the same.

- **Day length:** summer days are longer (the sun rises earlier and sets later), winter
  nights are longer (longer horde nights in survival).
- **Plants** (planted trees and bushes, berries growing back) grow a bit faster in spring,
  slower in autumn, and **not at all in winter**; rain makes them grow faster. Store food
  before winter.
- **Winter:** villagers burn **2 wood each a day** for warmth (taken from storage, a bit at
  a time). Without wood they're **cold**: 75% speed. Snow settles over the first day of
  winter and melts in spring; deep snow slows everyone a little (you, villagers and
  zombies). Trees lose their leaves (pines get snow on them).
- **Autumn:** the leaves turn orange and red.
- **Weather:** rain (darker sky, plants grow faster), storms (summer: heavy rain and
  lightning flashes with thunder), fog (drifting fog; towers only see two thirds as far)
  and snow (winter). Rain and storms are heard outside, not inside a house.
- The top right shows the season, days left and the weather.

Try it (**F10** has a *Time* part with **Next season** and the weather buttons):

1. Free build. The top right says *Spring (2 days left): clear* or similar.
2. F10 > Weather **Rain**: rain streaks fall and splash, the sky dims, you hear rain.
   Walk into a house: the rain sound fades. **Fog**: fog drifts over everything.
   **Storm**: lightning flashes now and then and thunder follows (sooner when it's close).
   **Snow**: snowflakes drift down. **The sky's** puts the real weather back.
3. **Next season** three times to reach winter (or wait). Snow settles on the ground over
   the first day; the trees go bare and snowy. You walk a little slower in it.
4. In winter open **V**: the panel says how much wood they burn a day. Spend all the wood:
   the top right says they're cold, and hovering a villager shows them slower.
5. Plant a berry bush in winter: it doesn't grow until spring.
6. In autumn the leaves are orange; in spring the snow melts away.

Tell me how it looks (too dark, too much rain, fog too thick?) and whether walking
through deep snow feels too slow.

## 3. Fire

Wooden things can burn now: **wood walls**, and wooden **doors, gates, furniture, chests,
storehouses, towers and traps** (the build bar's hover box says "Burns"), and houses with
a thatch roof or wooden walls. Stone and metal don't.

- **What starts a fire:** lightning in a storm, explosions (rockets, C4, bloaters, cannon
  shells), a flamethrower's fire, napalm, a **burning zombie** (a new kind from wave 8:
  charred, with flames on it; its blows set wood alight, and so does it when it falls), and
  a torch, brazier, lamp or fireplace being knocked over (broken).
- **Burning:** a fire grows hot over a few seconds, wears what's burning down until it's
  gone (a wood wall lasts about 17 seconds) and spreads to wooden things right next to it.
  Flames, light, smoke and crackling.
- **Putting it out:** build a **well** (Furniture tab). Every villager but the guards fetches
  a bucket from it and throws it on the nearest fire (two to a fire); a bucket puts out one
  burning thing. A fire wakes sleeping villagers. You can beat a fire out yourself: stand
  next to it and **hold E**. **Rain** puts fires out (not under a roof) and **snow** keeps
  them from spreading.
- Fires aren't saved: a loaded game has none.

Try it:

1. Free build. Build a line of wood walls and a few wooden things next to them (chairs,
   a chest). Hover them on the build bar: "Burns".
2. Stand by them, **F10 > Fire > Set fire to wood near you**. Flames and smoke appear,
   the top right says *Fire!*, and it spreads along the wood.
3. Without a well, hold **E** next to a burning wall: the hint shows the fire going down
   to nothing.
4. Build a well, set fire again: villagers run to the well, then to the fire, splash, and
   it goes out. Hover a burning thing on the throne: it says how hot it is and who's coming.
5. F10 > Weather > Rain while something burns outside: it goes out by itself.
6. Survival: F10 > send 5 **Burning** zombies at a wooden gate.

Tell me if fires spread too fast or too slow, or villagers ignore them.
