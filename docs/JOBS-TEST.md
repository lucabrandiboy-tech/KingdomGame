# Villager jobs and farming: how to test them

New:

- **Jobs.** Every villager has one: **Builder** (builds what you plan, like
  before), **Woodcutter** (chops trees), **Miner** (breaks rocks), **Forager**
  (picks berries). Gatherers fill their hands, carry it to the nearest
  storehouse or chest, and go back for more. You start with 2 builders and 1
  woodcutter. Survivors who join in survival start with no job.
- **The villagers panel: V.** Each job with **-** and **+**. **-** takes a
  villager off that job (they get no job), **+** gives a villager with no job
  that job. If gatherers are stuck it says why (storage full, nothing left).
- **Farming tab** (throne view): **Tree sapling** and **Berry bush**. Drag an
  area to plant a row or a field. They start small and grow (a tree takes 4
  minutes, a bush 2.5). Woodcutters go for planted trees first, and **a planted
  tree grows back** after it's cut (5 minutes), so a tree farm keeps the wood
  coming. Hovering a young tree on the throne shows how grown it is.

Crafting, weapons and defences come in the next steps.

> **Not tested in Unity yet.** Send a screenshot of anything odd and any red
> Console errors.

## Set up

Close Unity, `git pull` in Git Bash, open the project, then **KingdomGame →
Build Game Scenes** (Don't Save). The Console says it added farming (and roofs)
to your build bar.

## Try it (Free build)

- [ ] V opens the panel: Builders 2, Woodcutters 1, Miners 0, Foragers 0
- [ ] The woodcutter walks to a tree, chops it (chips fly), carries wood to the storehouse, goes back
- [ ] The storehouse's wood goes up (hover it on the throne)
- [ ] - by Builders, then + by Miners: that villager stops building and goes for rocks
- [ ] A forager picks berries; bushes grow their berries back after a while
- [ ] Planting: Farming tab, Tree sapling, drag an area; builders plant small trees that slowly grow
- [ ] Woodcutters cut the grown planted trees first; a cut planted tree starts again small and grows back
- [ ] With storage full, the panel says the gatherers are stuck because storage is full
- [ ] Survival: survivors who join say "give them a job (V)" and show under "No job"
