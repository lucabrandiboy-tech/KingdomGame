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

## 8. Goals for each age

Every age has three or four goals now, with a reward for each. The list is on the left of
the screen (**J** hides it), with how far along you are: for example in the Stone Age
*Have 6 villagers*, *Build 20 tiles of wall*, *Hunt 2 deer*, *Live through 2 nights*.

- Done, a goal pays its reward into storage at once (wood, stone, food, ores, steel) and
  some give XP (V to spend). The message says what you got; if storage is full, the rest
  is left in a bag by the throne.
- The list shows the earliest age you've reached that still has goals left, so finishing
  an age's goals brings up the next age's (once you've reached it; in free build, straight
  away). Wave goals only count in survival.
- Later ages: more villagers, longer walls, towers, rooms, ores and steel in storage,
  zombies killed, chests searched in ruins, and in the last age, killing a Warlord.
- What's done is saved.

Try it:

1. Start a game: the goals box is on the left. Press **J**: it hides; again: it's back.
2. Build 20 wall tiles (free build is quickest): the goal ticks up, then the message says
   *Goal done* and the stone goes into storage.
3. Free build: once the Stone Age goals are done, the Bronze Age ones show.

Tell me if goals are too easy, too slow, or the rewards too small.

## 9. Playing together: trading and defending friends

Online (hosting, or joined), press **O** for the new *Playing together* panel.

- **Trading:** next to each player, **Trade**: set what you give and what you want (wood,
  stone, food, copper, iron, gold, steel, coal; **+**/**-** in tens, hold **Shift** for
  hundreds) and **Send the offer**. What you give leaves your storage straight away and is
  held until they answer. They get a message (and a reminder on the right): **O**, then
  **Accept** (only if they have what you asked for) or **Decline**. Taken: what you asked
  for goes into your storage. Turned down, unanswered after a minute, or they leave: your
  goods come back. If you save and quit with an offer still out, the held goods are saved
  as a bag by your throne.
- **Every player has their own hordes now** (survival): before, only the host's kingdom
  had zombies. Each night every kingdom is attacked, run on its own player's computer.
- **Defending a friend:** **Ride there** (next to their name) takes you to their kingdom in
  about 7 seconds (the screen goes dark on the way; nothing can hurt you on the road).
  **Ride home** brings you back. At their kingdom you see their zombies, your shots and
  blows hit them (the damage is sent to their game, where the zombies really are), and
  their zombies come for you and bite (that damage is sent to yours). Their towers shoot
  the zombies as usual. Your own kingdom carries on without you meanwhile (your towers,
  villagers and walls).
- Online, falling isn't the end of the game for anyone, the host included: you get up
  again at your throne.

What doesn't work yet (honestly): they don't see your bullets fly (only the zombies
getting hit); walls the zombies break in your friend's kingdom break in their game, but
your screen shows their walls as they were built (destruction isn't sent yet); ruins and
wild animals are each player's own.

Try it (two computers, or two copies of the game on one: see docs/ONLINE-TEST.md):

1. Host a **survival** game; join it from the other copy.
2. Host: **O**, **Trade** next to the other player, give 50 wood, want 20 stone, **Send**.
   Your wood goes down by 50. On the other: the reminder appears; **O**, **Accept**. Both
   storages change. Try **Decline** too: the wood comes back.
3. At night both kingdoms get a horde. On the joined copy: **O**, **Ride there** next to the
   host. You arrive by their throne, see their zombies, shoot them (they die on the host's
   screen too) and get bitten (your health goes down).
4. **O**, **Ride home**.

Tell me what feels laggy or wrong (zombies jumping about, hits not landing).

## 10. Kinds of room, soldiers and armor

**Kinds of room.** A room is a space closed in all round by walls and doors (give it a door
so villagers can get in). What you put in it decides what kind it is, and its name shows on
the floor when you build from the throne. **I** opens the rooms guide: every kind, what
makes one, what it does, and how many you have. The first kind on the list that fits
wins, so a room with a bed and a weapon rack is a bedroom, not an armory.

| Room | Needs | Does |
| --- | --- | --- |
| Infirmary | a **medicine table** (new) and a bed | hurt villagers and soldiers go to its beds and heal fast |
| Barracks | 3 or more beds | soldiers asleep there heal twice as fast |
| Dining hall / great hall | a table and a chair or bench (the cafeteria) | fed, rested villagers work 10% faster |
| Armory | a weapon rack or armor stand | E there: armor for you, weapons and armor for soldiers |
| Training room | a **training dummy** (new) | soldiers train there and go up in rank |
| Library | a bookshelf | research 10% faster |
| Workshop | an anvil or crafting table | crafters 15% faster |
| Kitchen, storeroom, bedroom, cottage | as before | as before |

**Soldiers** are a new job (**V**, Soldier). They don't run from zombies:
- They go after zombies and wolves they can see (14 tiles), and when a horde attacks,
  after any zombie near the throne. A zombie outside your walls: they go as close as they
  can and shoot over the wall (like you do), if they have a gun or bow.
- They start with a club. Badly hurt (below 30%) they leave the fight for a bed (an
  infirmary's first), and get up again once they're better.
- With nothing to fight they train at a training dummy (in a training room), or stand
  guard in front of the throne. They sleep by day, like guards.
- Ranks: recruit, private, corporal, sergeant, captain. Each rank gives more health and
  faster attacks. Training gets them to sergeant; captain takes real fights (kills).

**Armor and weapons** (E at a weapon rack or armor stand inside an armory room):
- **You**: buy armor for each age (padded jerkin, bronze scales, chainmail, plate, ...,
  flak vest). It takes some damage off every hit and adds health. Your look doesn't change
  (yet); your health bar does.
- **Soldiers**: the arrows pick one soldier, or all of them. Give them copies of the weapons
  you've made at the anvil (half the cost) and armor (full cost each). Their look changes
  with their armor: tunic, padding, mail, plate, a modern uniform.

Try it:

1. **F10**: +10,000 of everything. Build a walled room with a door and put a **weapon rack**
   in it. Press **I**: the guide says you have 1 armory. Walk to the rack: "E: gear up".
2. Buy yourself the padded jerkin. Your health goes up by 15.
3. **V**: make 3 villagers soldiers (**-** by another job frees one up, then **+** by Soldier).
   They walk to the throne and stand guard, each with a club.
4. Back at the rack: give all soldiers the jerkin, and a bow (free build has every weapon
   already; in survival make it at the anvil first). They change look; the weapon shows in
   their hands.
5. Another walled room with a **training dummy**: soldiers walk in and hit it in turns (one
   per dummy). **F10, Soldiers +100 experience**: a message says they're now corporals.
6. **F10, Wolves now**, or wait for a horde: the soldiers go for them; shooters stop at
   range and shoot.
7. A room with a **medicine table** and a bed (the guide says Infirmary). **F10, Hurt
   everyone**: hurt villagers go and lie in its beds, and get up healed after a few seconds.
   The soldiers (below 30%) go to bed too, the infirmary's first.
8. Save, load: soldiers keep their weapon, armor and rank; you keep your armor.

What isn't done (honestly): other players online see your soldiers as ordinary villagers;
the ruler's look doesn't change with armor; soldiers in melee can hit a zombie through a
thin wall (so can you with the club).
