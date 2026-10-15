# Testing zombie bases, sieges, XP and villager upgrades

Survival only. Nothing here has been run in Unity yet.

## What's new

- **Zombie bases**: every 2 days (at dawn) a new base appears, 70 to 140 tiles from your throne
  (never near where you started), up to 3. It's a dark mound of rubble and bones. About 6 zombies
  camp round it and wander there; killed ones come back one at a time (every 40 s). They go for you
  if you come within about 8 tiles.
- **Sieges**: at nightfall each base has a 35% chance to besiege your kingdom: a crowd of 14
  zombies camps just outside, on the side facing their base, going for anyone who comes out. They
  don't smash your walls (they only wait). Villagers stay at their **shelter flag** while they're
  there. After 4 minutes the crowd goes home (or when you've killed them).
- **XP**: shoot, hit or blow up a base (1,500 hit points) to destroy it: **+100 XP**.
- **Villager upgrades** (V, at the bottom): walking speed, gathering and digging, crafting. Each
  level is +5% and costs more XP than the last (100, 135, 182, 246...).
- Campers at a base don't count for the waves: a wave ends when its attackers are dead.

All the numbers are in **Survival Config > Zombie bases**.

## How to test

1. Survival. Play to day 2 (or set Base Every Days to 1 in Survival Config for a quicker test).
2. You should see "A zombie base has appeared out in the wild". Walk out and look for it
   (a dark mound with zombies milling round).
3. Destroy it (dynamite works well). You should see "+100 XP".
4. Press V: the Upgrades section shows your XP; buy "+5%" for walking speed. Villagers walk faster.
5. To see a siege, set Siege Chance to 1 and wait for night: a crowd should gather outside your
   walls and stay there, and villagers should go to the shelter flag.
6. Save and load: the bases (and their damage), your XP and upgrade levels come back.
