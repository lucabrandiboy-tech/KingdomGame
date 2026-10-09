# KingdomGame (placeholder name)

An online multiplayer, top-down, pixel-art kingdom builder and survival game:
build a huge kingdom, automate production, advance from the Stone Age to World
War 2, survive zombie sieges, and raid other players. Walls break apart pixel
by pixel. Made in Unity 6.3 LTS (URP 2D), headed for Steam Early Access.

**Status: Phase 0 (setup).** Nothing playable yet. Follow
[`docs/PHASE-0-SETUP.md`](docs/PHASE-0-SETUP.md).

## Docs

| File | What's in it |
|---|---|
| [`docs/BRIEF.md`](docs/BRIEF.md) | The full game design brief |
| [`docs/ROADMAP.md`](docs/ROADMAP.md) | Phases 0–9 and what's done |
| [`docs/PHASE-0-SETUP.md`](docs/PHASE-0-SETUP.md) | Step-by-step setup for a new computer |
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
    Core/         shared basics (PixelArt.cs: pixel density)
    Destruction/  Networking/  Building/  Economy/  Units/
    Zombies/      Character/   UI/        World/
    Editor/       editor-only tools (setup check, art importer)
  ThirdParty/     asset packs, each listed in CREDITS.md
Packages/         Unity package list
ProjectSettings/  Unity project settings
docs/             design and setup docs
tools/            repo checks and the compile check (not part of the game)
```

## Opening the project on a new computer

1. Install **Unity 6.3 LTS** with Unity Hub, and **GitHub Desktop**.
2. GitHub Desktop → File → Clone repository → `KingdomGame` (click **Initialize Git LFS** if asked).
3. Unity Hub → Projects → Add → **Add project from disk** → the cloned folder.
