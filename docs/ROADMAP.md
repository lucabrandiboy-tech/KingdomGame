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

## ⏳ Phase 1: Destruction prototype

- A top-down ruler with twin-stick controls (WASD + mouse aim) and a gun
- Walls built from 32 × 32-pixel chunks that chip pixel by pixel where bullets
  hit, with pooled flying pixel particles, colliders rebuilt only for changed
  chunks, and collapse of detached pieces
- Explosions (big craters) and a brute-style smash, to test all three damage shapes
- A stress test scene: many walls destroyed at once, FPS shown on screen
- Damage expressed as events (position, radius, type, seed) from day one, so
  Phase 5 can send them over the network unchanged

**Done when:** you can shoot holes through walls and walk through them, and the
stress test numbers are written down in this file.

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
