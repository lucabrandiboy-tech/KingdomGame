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

## 4. Deer and wolves

The wild has animals now. They come and go out of sight round wherever you are (about
45-65 tiles away), so there are always some, however far you go. They aren't saved.

- **Deer:** herds of 3-5 graze and wander in open ground (fewer in winter). They're shy:
  get within about 7 tiles, or let a zombie or wolf near, and the whole herd runs. Shoot
  or club one and it leaves a **bag of 20 food** where it falls (walk over it).
- **Hunters** (a new villager job, **V**): they take a bow, creep up on a deer, shoot it
  (one good arrow), fetch the meat and carry it to storage. If no deer are about, the
  villagers panel says so.
- **Wolves:** from the second night, packs of 2-3 come in the dark. You hear them howl.
  They prowl closer and go for anyone out of the firelight (you, villagers), rushing in
  faster than you can run and biting. They won't step into the light of a brazier, torch,
  campfire or burning building, so a well-lit kingdom keeps them out. Hurt one badly and
  it runs off; they all leave at dawn, and they don't come while zombies are attacking.
  Villagers run from them (to the throne); hunters shoot them; towers shoot them when
  there's no zombie to shoot. A dead wolf leaves a little food. Their eyes shine in the dark.
- Online, each player has their own animals (the others don't see yours).

Try it:

1. Free build. Walk out of your kingdom: within a minute or so you should find a herd
   (or **F10 > Wildlife > A herd of deer near you**). Walk at them: they run.
2. Shoot one (any gun or bow; the club works if you can catch one). It drops a bag: walk
   over it for the food.
3. **V**: make a villager a **Hunter**. Watch them go out, shoot, and bring meat home
   (storage food goes up by 20).
4. **F10 > Wildlife > Wolves now** (or wait for the second night; F10 > Time > Midnight
   from day 2 on). A howl, then wolves come.
   Stand in the dark: they rush you. Run to your throne's braziers: they stop at the
   edge of the light and circle. Fight them with the club: two hits and one runs off.
5. With wolves about, the top right says *Wolves about*. Watch a villager outside run
   to the throne, and a hunter turn and shoot.

Tell me if deer are too hard or too easy to hunt, and if wolves are too much.

## 5. The boss: a Warlord every 10 waves

Every 10th wave (10, 20, 30...) a **Warlord** comes with the horde (two on wave 20, three
on wave 30): a huge zombie in rusty armor and a horned helmet, carrying a **battering ram**.

- It doesn't walk round your walls: it makes **straight for your throne** and rams through
  whatever is in the way. Two blows break a stone wall tile, one a wooden door. Each blow
  booms and throws up dust. Trees, rocks and houses it walks round.
- It's slow but very tough (2,000 health, more in later waves, and armor that takes 8 off
  every hit: small quick shots barely hurt it). Its health bar is across the top of the
  screen, and its eyes glow red.
- Killed, it leaves a big **chest**: 300 wood, stone and food, and 60 of each ore (copper,
  iron, gold, coal), plus 150 XP (V to spend).

Try it:

1. Survival. **F10 > Waves > Start wave 10 now.** The message says a Warlord is coming.
2. Watch it come at a wall: it rams through instead of going round to a gate.
3. Fight it (towers, traps, F10 > No damage if you like). Its bar goes down; when it falls,
   walk over the chest.
4. Free build: **F10 > Zombies > +1 Warlord** sends one at you.

Tell me if it's too strong or too weak for wave 10.

## 6. Survivors at the gate

Villagers no longer just appear after a wave. Survivors come out of the wild and walk up to
your throne to **ask to join**: after each wave in survival, and each morning in free build,
as many as you have room for (each bed is a home for one more villager).

- When one is waiting, a box on the right says who they are, what they did before (they
  take that job: a hunter, a soldier, a blacksmith...) and what you notice about them.
  Their name shows over their head. **Y** takes them in, **K** keeps them out (they walk
  back into the wild). They wait about two and a half minutes for an answer.
- **Some have been bitten** and hide it (15% at first, more in later waves, at most 35%).
  Often you notice something (a bloody rag round an arm, grey and sweating, a dark cough),
  but not always, and a healthy one can look ill from hunger or the cold.
- Take in a bitten one and a few minutes later (2-5) they **turn into a zombie** where
  they stand, inside your walls. In their last half minute they look grey-green.
- Zombies and wolves can kill survivors on their way in.

Try it:

1. Free build, build a bed (so there's room). **F10 > Villagers > A survivor comes.** Watch
   them walk to your throne; the box appears. Press **Y**: they join with their job (V).
2. **F10 > A bitten survivor comes**, press **Y**, then **F10 > Bitten villagers turn in
   10 s**. They go pale, then turn into a zombie. Kill it.
3. Another one, press **K**: they walk away.
4. Survival: finish a wave; survivors come instead of appearing at the throne.

Tell me if it's too easy or too hard to tell who's bitten.

## 7. Ruins in the wild

The endless world has ruins in it now: about one in three squares of 96 x 96 tiles has one
(none within about 55 tiles of your throne). Each is the same for the same world seed.

- **An abandoned village:** two to four houses fallen in (broken walls, old floors, rubble),
  sometimes the old well, a chest or two. **A wrecked caravan:** one or two wagons on their
  side with crates thrown about. **A fallen watchtower:** a broken ring of stone with a
  chest inside that always holds a **lost blueprint**.
- **Guards:** a camp of zombies (2 to 8) round each ruin comes out when you get near. If
  you leave, they hide again and are still there when you come back; kill them all and the
  ruin is clear (+25 XP). Guards can only be inside your kingdom's lands (256 tiles from
  the throne): further out, ruins are unguarded.
- **Searching:** walk up to a chest or crates: the screen says **E: search**. You find a bag
  of supplies (wood, stone, food in villages; food, ores and steel in caravans; iron, gold
  and stone in towers), more the further the ruin is from your throne. A searched chest
  turns dark. A **lost blueprint** halves your next research (or the one going now): the
  research screen (T) shows how many you have. In free build and survival-by-waves (no
  research) a blueprint gives 60 XP instead.
- The broken walls are low: you (and zombies) can climb over them.
- What you've searched, which ruins you've found and how many guards are left are saved.
  Ruins are only in your own game or one you host (not when you join someone else's).

Try it:

1. Free build (or survival). Walk away from your kingdom in one direction for a while.
   When you get near a ruin the top of the screen says *You've found an abandoned village*
   (or a caravan or a watchtower). A few zombies come out.
2. Fight them, then stand by the chest: **E**. A bag drops: walk over it.
3. Walk far away and come back: the searched chest is still dark.
4. Find a watchtower and search its chest: the message says you found a lost blueprint.

Tell me if they're too rare, too common, too hard or too easy.
