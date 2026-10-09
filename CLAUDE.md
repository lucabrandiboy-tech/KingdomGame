# KingdomGame: notes for Claude

Multiplayer top-down pixel-art kingdom builder / survival game in Unity. The full
design brief is `docs/BRIEF.md`; read it before starting any phase. Phase status
is in `docs/ROADMAP.md`, and the decisions with their reasons in `docs/DECISIONS.md`.

## Working with the user

- The user is a **total beginner** with Unity and C#. Explain every step
  plainly, say exactly what to click in the Unity Editor, define terms the first
  time they're used, and work in small steps that each end with "how to test it,
  what you should see".
- **Phases are gated:** don't start the next phase until the user has tested the
  current one on their computer and confirmed it works.
- **Never claim something works unless it was verified.** Unity can't run in the
  cloud container, so anything that needs the Editor is untested until the user
  tries it. Say so.
- Warn (and suggest an alternative) when a feature would hurt performance with
  100+ players or huge kingdoms; never silently cut it.
- Commit after each working step with a clear message.

## Stack

Unity 6.3 LTS (6000.3.x), URP 2D Renderer, Input System package, C# 9.
Networking (Phase 5): Fish-Net recommended, not installed yet.

## Conventions

- Code: `Assets/Scripts/<Area>/`, namespace `KingdomGame` (editor tools:
  `Assets/Scripts/Editor/`, namespace `KingdomGame.EditorTools`). No assembly
  definitions yet.
- **Data-driven:** gameplay numbers live in ScriptableObjects under
  `Assets/Data`, never hardcoded. Pixel density is the one exception:
  `Assets/Scripts/Core/PixelArt.cs` (32 PPU, 640 × 360 reference resolution).
- Art in `Assets/Art` gets pixel-art import settings automatically
  (`PixelArtImporter`). Asset packs go in `Assets/ThirdParty` and must be added
  to `CREDITS.md` with a commercial-use license.
- **Every new file or folder under `Assets/` needs a `.meta` file.** When
  creating one outside Unity, write a valid `.meta` with a fresh random GUID
  (see the existing ones). Empty folders need a `.gitkeep` (Unity ignores
  dotfiles). CI (`tools/check_meta.py`) fails on missing `.meta` files.
- Binary files (png, wav, ...) go through Git LFS (`.gitattributes`).
  CI runs `git lfs fsck --pointers`.

## Phase status

Phase 0 (setup): the user is doing the Unity side now. Phase 1 (destruction
prototype) was started early at the user's request: the code is written and
compile-checked, the pure math is unit-tested, but it has **not run in Unity**.
Test steps for the user: `docs/PHASE-1-TEST.md`. Design: `docs/DECISIONS.md` section 11.

## Checks to run before every commit

- `dotnet run --project tools/tests/DestructionTests.csproj`: unit tests for
  the Unity-free destruction math (`DamageShape.cs`, `PixelAlgorithms.cs`).
  Keep those two files free of `UnityEngine` so the tests can compile them.
- `python3 tools/check_meta.py`
- `dotnet build tools/compile-check/CompileCheck.csproj` compiles every script
  in `Assets/` against Unity 2021.3 reference assemblies (NuGet
  `UnityEngine.Modules`) plus hand-written stubs for UnityEditor and URP in
  `tools/compile-check/stubs/`. When a script uses a new Unity API, add it to
  the stubs with the signature copied from Unity 6.3's source
  (`raw.githubusercontent.com/Unity-Technologies/UnityCsReference/6000.3/...`,
  `.../Graphics/6000.3/staging/Packages/...`). Passing means "no typos or type
  errors", not "works in Unity". The .NET 8 SDK may need installing first
  (`apt-get install dotnet-sdk-8.0`). Also build it with
  `-p:DefineConstants="UNITY_EDITOR%3BENABLE_LEGACY_INPUT_MANAGER"` to check the
  old-input code path. Code that needs Unity 6-only APIs (like
  `Rigidbody2D.linearVelocity`) goes behind `#if UNITY_6000_0_OR_NEWER`, with
  the older name in the `#else`, so the 2021.3 reference assemblies still compile it.
- Scenes and assets are created by editor menu code (`Assets/Scripts/Editor/Phase1Setup.cs`),
  not hand-written YAML.
- Unity's own hosts (packages.unity.com, download.unity3d.com) are blocked
  from the cloud container; GitHub raw files and NuGet work.
