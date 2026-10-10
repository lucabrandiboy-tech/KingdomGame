# KingdomGame: notes for Claude

Multiplayer top-down kingdom builder / survival game in Unity, in a dark realistic
2D style seen from straight above (it started as pixel art). The full
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
  `Assets/Data`, never hardcoded. The destruction grid is the one exception:
  `Assets/Scripts/Core/PixelArt.cs` (64 health pixels per tile). Camera zoom
  and the look (shadows, glow material, Art Detail) are in `ViewConfig`.
  Starting values for new assets are set in `Assets/Scripts/Editor/GameSetup.cs`
  (menu KingdomGame > Build Game Scenes), which keeps existing assets.
- Art is drawn in code for now (`Assets/Scripts/Art`, pure C# on `PixelImage`),
  seen from straight above, facing right, lit from the top-left (baked into the
  pictures, so nature isn't rotated). `Art/ArtCatalog.cs` lists every picture
  (keys in `ArtKeys`). Build Game Scenes bakes them (`Editor/ArtBaker.cs`) at
  `ViewConfig.artDetail` pixels per tile (1024 by default, the user's choice:
  about 0.65 GB of video memory; furniture, floors, doors, effects, zombies
  and bodies at half that, `ArtItem.Detail`) into sheets with normal maps in
  `Assets/Art/Generated` (gitignored) plus `Assets/Data/Look/ArtLibrary`; the
  game gets sprites with `ArtSprites.Get(key)`. **Bump `ArtCatalog.Version`
  whenever any art changes**, or Unity keeps the old bake. Preview it with
  `dotnet run -c Release --project tools/art-preview/ArtPreview.csproj -- <folder> [pixels per tile] [--sheet=<name>]`
  (about a minute at 1024; game_day/night/close/outpost.png and
  castle_night/close.png, horde_night.png are mock screenshots lit like URP:
  linear color, URP's falloff, bloom, contrast) and look at the PNGs before
  committing art changes. Keep its lights, colors and layouts in step with the game.
- Drawing order: fixed per kind of thing, in `Core/SortOrder.cs` (no y-sorting).
  Shadows use `Look.AddShadow`; things that should glow at night use `Look.MakeGlow`.
- Textures are smooth (bilinear or trilinear) with mipmaps; no Pixel Perfect Camera.
  Art in `Assets/Art` gets its import settings automatically
  (`PixelArtImporter`; the baked sheets get their own). Asset packs go in `Assets/ThirdParty` and must be added
  to `CREDITS.md` with a commercial-use license.
- **Every new file or folder under `Assets/` needs a `.meta` file.** When
  creating one outside Unity, write a valid `.meta` with a fresh random GUID
  (see the existing ones). Empty folders need a `.gitkeep` (Unity ignores
  dotfiles). CI (`tools/check_meta.py`) fails on missing `.meta` files.
- Binary files (png, wav, ...) go through Git LFS (`.gitattributes`).
  CI runs `git lfs fsck --pointers`.

## Phase status

Phases 0 and 1 are done and tested in Unity. Phase 2 is split into 2A
(Rust-style walls, C4, map with gathering, day/night, restyled dark realistic
top-down), 2B (throne view, grid building, storehouses, builder villagers) and
2C (villager needs, zombies, health and respawn). The user then asked for a
**castle and survival update** before testing (rooms, doors, floors,
furniture, lights; main menu with Survival = endless zombie waves, and Free
build), then ages with real-time research, roofs, and **houses** you go into
(bigger inside, one level per age, upgraded by builders; `Building/House.cs`,
`Interior.cs`: each house's inside is a room far off the map at
`Kingdom.InsideOrigin`, reached through the door; villagers walk through doors
in `Villager.Walk`). 2A, 2B and these updates are written and compile-checked but **not yet
run in Unity**; the user tests them together with `docs/CASTLE-SURVIVAL-TEST.md`
and `docs/HOUSES-TEST.md` (plus `docs/PHASE-2B-TEST.md` for the details). Don't start 2C before they
confirm. See `docs/ROADMAP.md` and `docs/DECISIONS.md` sections 14-15.
The user saves work with Git Bash (`git add -A`, `git commit`, `git push`, `git pull`).

## Checks to run before every commit

- `dotnet run --project tools/tests/DestructionTests.csproj`: unit tests for
  the Unity-free code: destruction math (`DamageShape.cs`, `PixelAlgorithms.cs`),
  path finding (`Villagers/GridPathfinder.cs`), the zombies' flow field and
  waves (`Zombies/FlowField.cs`, `Zombies/WaveMath.cs`) and room finding
  (`Building/RoomFinder.cs`). Keep those files free of `UnityEngine` so the
  tests can compile them.
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
- Scenes and assets are created by editor menu code (`Assets/Scripts/Editor/GameSetup.cs`),
  not hand-written YAML.
- Unity's own hosts (packages.unity.com, download.unity3d.com) are blocked
  from the cloud container; GitHub raw files and NuGet work.
