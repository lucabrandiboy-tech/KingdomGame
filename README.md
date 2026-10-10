# KingdomGame (placeholder name)

An online multiplayer, top-down kingdom builder and survival game (dark, realistic 2D):
build a huge kingdom, automate production, advance from the Stone Age to World
War 2, survive zombie sieges, and raid other players. Walls break apart pixel
by pixel. Made in Unity 6.3 LTS (URP 2D), headed for Steam Early Access.

**Status:** Phases 0 and 1 done. Phase 2 steps A and B (the dark realistic
restyle, Rust-style walls, C4, gathering, day/night, the throne view, building
with storehouses and builder villagers) and the castle and survival update
(rooms, doors, furniture, lights; a survival mode with endless zombie waves)
are written but not yet tested in Unity: see
[`docs/CASTLE-SURVIVAL-TEST.md`](docs/CASTLE-SURVIVAL-TEST.md).

## Docs

| File | What's in it |
|---|---|
| [`docs/BRIEF.md`](docs/BRIEF.md) | The full game design brief |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Phases 0–9 and what's done |
| [`docs/PHASE-0-SETUP.md`](docs/PHASE-0-SETUP.md) | Step-by-step setup for a new computer |
| [`docs/CASTLE-SURVIVAL-TEST.md`](docs/CASTLE-SURVIVAL-TEST.md) | How to test the current step |
| [`docs/PHASE-2B-TEST.md`](docs/PHASE-2B-TEST.md) | Detailed checks for the look, walls and building (still to test) |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | Unity version, the look, networking, saving, and why |
| [`CREDITS.md`](CREDITS.md) | Every third-party asset and its license |

## Folders

```
Assets/
  Art/            our art (Generated/ holds the baked art: made by Build Game Scenes, not in Git)
  Audio/          music and sound effects
  Data/           ScriptableObject configs: costs, health, damage, rates, era unlocks
  Prefabs/
  Scenes/         (from the Unity template)
  Scripts/
    Art/          code-drawn art: people, zombies, trees, rocks, ground, walls, buildings, furniture, effects
    Core/         shared basics: art density, input, camera, drawing order, look settings
    Destruction/  pixel-by-pixel walls, damage events, flying cubes, rubble, explosions
    Combat/       weapons and projectiles
    Character/    the ruler
    Economy/      resources, backpack, trees/rocks/bushes you gather from
    World/        map generation, day and night, game scene setup, stress test
    Building/     the kingdom: throne view, blueprints, storehouses, rooms, doors, floors, furniture
    Villagers/    builder villagers and path finding
    Zombies/      zombies, their flow-field path finding, survival waves, bodies and blood
    UI/           on-screen info, main menu, pause menu
    Networking/  Units/   (later phases)
    Editor/       editor-only tools (setup check, art importer, scene builder)
  ThirdParty/     asset packs, each listed in CREDITS.md
Packages/         Unity package list
ProjectSettings/  Unity project settings
docs/             design and setup docs
tools/            repo checks, unit tests and the compile check (not part of the game)
```

## Opening the project on a new computer

1. Install **Unity 6.3 LTS** with Unity Hub, and **GitHub Desktop**.
2. GitHub Desktop → File → Clone repository → `KingdomGame` (click **Initialize Git LFS** if asked).
3. Unity Hub → Projects → Add → **Add project from disk** → the cloned folder.
