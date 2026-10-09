# Roadmap

From the brief (`docs/BRIEF.md`, section 13). **A phase starts only after the
previous one has been tested on your computer and you've said it works.**

Status key: ✅ done and confirmed · 🔧 in progress · ⏳ not started

## 🔧 Phase 0: Setup

- [x] Private GitHub repo with Git LFS, Unity `.gitignore` and `.gitattributes`
- [x] Repo checks action (`.meta` files, LFS pointers)
- [x] Pixel-art import script (`Assets/Scripts/Editor/PixelArtImporter.cs`)
- [x] Setup check window with fix buttons (KingdomGame > Phase 0 Setup Check)
- [ ] Unity 6.3 LTS installed *(you)*
- [ ] Project created from the Universal 2D template and moved into the repo *(you)*
- [ ] Pixel Perfect Camera configured, test wall looks crisp *(you, with the Fix buttons)*
- [ ] First commit pushed; the test PNG shows "Stored with Git LFS" on github.com *(you)*

**Done when:** every line in the Setup Check window says PASS, the test wall
looks sharp in Play mode, and Repo checks is green on GitHub.
Steps: `docs/PHASE-0-SETUP.md`.

## 🔧 Phase 1: Destruction prototype

Started early, at your request, while Phase 0 was still being set up. Code
written; **not yet tested in Unity**. Test steps: `docs/PHASE-1-TEST.md`.

- [x] Top-down ruler with twin-stick controls (WASD + mouse aim): rifle, rocket launcher, smash
- [x] Walls split into 32 × 32-pixel chunks that break pixel by pixel where hit
- [x] Three damage shapes: bullets (small holes), explosions (craters), brute smash (blocky chunks)
- [x] Pooled flying pixels with a hard cap; they settle into rubble tiles
- [x] Colliders rebuilt only for changed chunks; units and bullets pass through holes
- [x] Cut-off pieces crumble; badly damaged walls collapse
- [x] Damage as events (position, radius, type, seed) with whole-number math, so Phase 5 can send them over the network
- [x] Stress test scene with FPS on screen and in the Console
- [x] Unit tests for the destruction math (run on every push)
- [ ] Tested in Unity *(you)*
- [ ] Stress test numbers recorded below *(you)*

**Done when:** you can shoot holes through walls and walk through them, and the
stress test numbers are written down here.

**Stress test results:** *(none yet)*

## ⏳ Phase 2: Single-kingdom offline prototype
Throne view with grid placement · builder villagers and blueprints · gathering
wood, stone, food · storage buildings · villager needs · basic zombie waves
drawn by noise and size · day/night cycle.

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
