# Mines: how to test them

- **Mining tab** (throne view): **Mine entrance** (2 x 2, 80 wood and 20 stone).
  Builders put up a timber headframe over a shaft.
- **E at the mine entrance** climbs down. Underground is solid rock (64 x 64
  tiles) with a small room at the bottom of the ladder. **E at the ladder**
  climbs back up.
- **B underground** opens the build bar. On the **Mining** tab pick **Dig** and
  drag over rock: it's marked (yellow). Villagers with the **Miner** job (V)
  come down and dig the marked rock out, a tile at a time (6 seconds each).
  Plain rock gives **stone**; rock with flecks of **copper**, **iron** or **gold**
  gives that ore instead (iron starts about 8 tiles from the ladder, gold about
  16). They carry it up to storage. Right click a marked tile to take the mark off.
- **Dug-out floor is yours to build on**: furniture, lights, chests, floors, rugs.
- The storage line (top left) shows copper, iron and gold once you have some.
  They aren't used for anything yet (crafting comes next).
- Zombies go for the mine entrance if someone hides below; if it breaks,
  everyone comes up and the mine is lost (what chests held is left in a bag).

Also new: the map is **256 x 256 tiles** (was 96), and zombies now come from a
ring just outside your kingdom instead of the map's edge.

> **Not tested in Unity yet.**

## Set up

Close Unity, `git pull`, open the project, **KingdomGame → Build Game Scenes**
(Don't Save; it bakes the new pictures). The Console says it added mines and
made the map bigger.

## Check (Free build)

- [ ] The map is much bigger (the edge is far away); the game still starts in reasonable time (say how long)
- [ ] Mining tab: place a Mine entrance; builders build it
- [ ] E at its bottom edge climbs down: rock all round a small room, the ladder in the middle
- [ ] You can see ore in the rock: orange copper, reddish iron, gold nuggets
- [ ] B, Mining tab, Dig, drag over rock: tiles get a yellow mark
- [ ] V: give a villager the Miner job: they climb down and dig the marked tiles; each turns to floor with a puff of dust
- [ ] They carry stone (or ore) up to the storehouse; storage shows copper/iron/gold
- [ ] Build a chest and a torch on dug floor; you can't build on rock ("dig it out first")
- [ ] E at the ladder climbs back up
