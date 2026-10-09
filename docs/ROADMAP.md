# Roadmap

From the brief (`docs/BRIEF.md`, section 13). **A phase starts only after the
previous one has been tested on your computer and you've said it works.**

Status key: ✅ done and confirmed · 🔧 in progress · ⏳ not started

## ✅ Phase 0: Setup

Unity 6.3 LTS project from the Universal 2D template in a private GitHub repo with
Git LFS; pixel-perfect camera; every Setup Check line passed; Repo checks green.

## ✅ Phase 1: Destruction prototype

Tested in Unity: shooting and blowing holes in walls, flying pixels, rubble.

- [x] Ruler with twin-stick controls and weapons
- [x] Walls split into chunks that break pixel by pixel, with crumbling and collapse
- [x] Damage as events (position, radius, type, seed) with whole-number math
- [x] Pooled flying pixels that settle into rubble
- [x] Stress test scene with FPS (now `Assets/Scenes/StressTest`)
- [ ] Stress test numbers recorded *(still to do)*

**Stress test results:** *(none yet)*

## 🔧 Phase 2: Single-kingdom offline prototype

Split into three steps, each tested before the next.

**2A (written, not yet tested in Unity)**. Test steps: `docs/PHASE-2A-TEST.md`.
- [x] 64 × 64-pixel tiles (twice the detail) and new code-drawn art: ground, walls, trees, rocks, bushes
- [x] New ruler: slimmer, animated, faces the aim, slower
- [x] Rust-style walls: each material resists bullets, melee and explosives differently; C4
- [x] Generated map with forests, rocks and berry bushes; gathering wood, stone and food with E
- [x] Backpack (limited personal inventory)
- [x] Day/night cycle with a night light on the ruler

**2B** (next): throne view with grid placement, storage buildings, builder
villagers constructing blueprints with stored resources.

**2C**: villager needs (food, housing, happiness), basic zombie waves drawn
by noise and kingdom size and stronger at night, ruler health, death and respawn at the throne.

## ⏳ Phase 3: Production and eras
Production chains and jobs · hand-carry then carts · tech tree for the first
2–3 eras · crafting · unit tier restrictions.

## ⏳ Phase 4: Military
Unit training and army caps · automatic battles with simple orders · defenses,
traps and turrets · zombie types.

## ⏳ Phase 5: Multiplayer foundation
Dedicated server with several players · synced kingdoms, characters and
destruction events · persistence across restarts. (Networking and save-format
decisions: `docs/DECISIONS.md` sections 7–8.)

## ⏳ Phase 6: PvP
Armies traveling and interception · offline raids · loot and conquest · fog
of war and scouts · raid alerts.

## ⏳ Phase 7: Social and economy
Clans and diplomacy · central safe-zone market with player shops · chat and voice.

## ⏳ Phase 8: Remaining eras and content
Industrial through WW2 (conveyors, trains, trucks, aircraft, naval) · seasons
and weather · procedural world polish.

## ⏳ Phase 9: Steam and servers
Steamworks · server browser · community server settings and plugins · Easy
Anti-Cheat · leaderboards and cosmetics · wipes · Early Access prep.
