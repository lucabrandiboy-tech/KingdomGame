# KingdomGame (placeholder name)

An online multiplayer, top-down, pixel-art kingdom builder and survival game:
build a huge kingdom, automate production, advance from the Stone Age to World
War 2, survive zombie sieges, and raid other players. Walls break apart pixel
by pixel. Made in Unity 6.3 LTS (URP 2D), headed for Steam Early Access.

**Status:** Phases 0 and 1 done. Phase 2 step A (64 px art, Rust-style walls,
C4, gathering, day/night) is written but not yet tested in Unity: see
[`docs/PHASE-2A-TEST.md`](docs/PHASE-2A-TEST.md).

## Docs

| File | What's in it |
|---|---|
| [`docs/BRIEF.md`](docs/BRIEF.md) | The full game design brief |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Phases 0–9 and what's done |
| [`docs/PHASE-0-SETUP.md`](docs/PHASE-0-SETUP.md) | Step-by-step setup for a new computer |
| [`docs/PHASE-2A-TEST.md`](docs/PHASE-2A-TEST.md) | How to test the current step |
| [`docs/DECISIONS.md`](docs/DECISIONS.md) | Unity version, pixel size, networking, saving, and why |
| [`CREDITS.md`](CREDITS.md) | Every third-party asset and its license |

## Folders

```
Assets/
  Art/            our pixel art (imported crisply at 32 px per tile automatically)
  Audio/          music and sound effects
  Data/           ScriptableObject configs: costs, health, damage, rates, era unlocks
  Prefabs/
  Scenes/         (from the Unity template)
  Scripts/
    Art/          code-drawn pixel art: ruler, trees, rocks, ground, walls
    Core/         shared basics: pixel density, input, camera, drawing order
    Destruction/  pixel-by-pixel walls, damage events, flying pixels, rubble
    Combat/       weapons and projectiles
    Character/    the ruler
    Economy/      resources, backpack, trees/rocks/bushes you gather from
    World/        map generation, day and night, game scene setup, stress test
    UI/           on-screen info
    Networking/  Building/  Units/  Zombies/   (later phases)
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
