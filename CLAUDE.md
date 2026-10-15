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
Networking: LiteNetLib 1.3.5 (`Assets/ThirdParty/LiteNetLib`, MIT), chosen over
Fish-Net because it can be tested with real connections outside Unity (see
`docs/ONLINE-PLAN.md`). `Networking/Core` is plain C# (server, client, protocol);
`Networking/NetPlay.cs` runs it in the game: `NetHost` (you host your own game and
play it) and `NetClient` (you join one); `NetServer` is a dedicated server (`-server`).
The role comes from `GameSession.Role`; `KingdomBootstrap` starts each.
Everyone sees everyone walking (`RemotePlayers`) and chats (`UI/ChatBox`). Each player's own
computer runs their kingdom (`docs/DECISIONS.md` section 16): `Networking/KingdomMirror.cs` sends
what's built (`BuiltPiece` changes) and where its villagers are; the server relays them and keeps
each joined player's whole kingdom (`SaveSystem.Upload`, handed back as `Msg.YourSave`);
`Networking/RemoteKingdoms.cs` shows the others' (walls with `DestructibleStructure.Remote`: not
damaged, not saved, not torn down). Zombies are only in the host's game so far (stage 4).
`ServerCore` remembers each player (`PlayerRecord`: key, kingdom, last position) and kingdom;
hosted saves keep them (`SaveData.others`) and a dedicated server keeps its world in
`saves/server-world.json` (`ServerWorldFile`). Bump `Protocol.Version` when messages change.
The tool projects' `.csproj` files are committed (the root `.gitignore` ignores Unity's).

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
- **Sound is made in code too** (no sound files): `Audio/SoundSynth.cs` (plain C#, no `UnityEngine`:
  the unit tests check every sound) makes each sound and the background loops; `Audio/Sounds.Play(sound, at)`
  plays one in the world (panned and quieter away from the middle of the screen, a loudness / most-at-once /
  shortest-gap per sound in `Sounds.MixOf`), `PlayHere` one that isn't anywhere; `Audio/Ambience.cs` plays wind,
  birds and crickets by the time of day. A weapon's shot sound is `WeaponConfig.sound`. Listen to them with
  `dotnet run --project tools/sound-preview/SoundPreview.csproj -- <folder>` (writes .wav files).
- Settings the player picks (graphics quality, volume) are in `Core/GameSettings.cs` (PlayerPrefs), shown by
  `UI/SettingsPanel.cs` in the main and pause menus. `UI/CheatMenu.cs` (F10, offline, Progression Config's
  Allow Cheats) is for testing.
- **Worlds are endless** (up to `NatureMap.Limit` = 32,000 tiles from the middle; house
  insides and mines live beyond that, at `Kingdom.InsideOrigin`/`MineOrigin`). Trees, rocks
  and bushes are data in `Economy/NatureMap.cs`, worked out from the seed by
  `WorldGenerator`'s generator in 32-tile squares when first asked about; only changes are
  kept (and saved), unchanged squares are forgotten when there are too many.
  `World/NatureStreamer.cs` makes `ResourceNode` objects only near rulers, the camera's view
  and kingdoms (`Kingdom.ActiveAreas`), and they write their state back into the map when
  they go. `ResourceNode.All` is only the shown ones: anything that must see more (paths,
  building checks, saving) asks the map. `World/GroundStreamer.cs` makes the ground in
  64-tile squares round the camera (maths on a background thread). Each kingdom works in
  its **lands** (`KingdomConfig.landsSize`, 512, a square round the throne = `Kingdom.Nav.Cells`):
  building, villagers' paths and gathering, zombie paths. No code may assume a whole map.
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
run in Unity**; the user starts with `docs/START-HERE.md` (one session that touches every
update), then tests them together with `docs/CASTLE-SURVIVAL-TEST.md`
and `docs/HOUSES-TEST.md` (plus `docs/PHASE-2B-TEST.md` for the details; later
updates: `docs/SAVE-TEST.md`, `WEAPONS-TEST.md`, `ONLINE-TEST.md`, `ENDLESS-WORLD-TEST.md`, `MACHINES-TEST.md`,
`ZOMBIE-BASES-TEST.md`, `AGES-CONTENT-TEST.md`, `ZOMBIE-KINDS-TEST.md`). Don't start 2C before they
confirm. See `docs/ROADMAP.md` and `docs/DECISIONS.md` sections 14-15.
The user saves work with Git Bash (`git add -A`, `git commit`, `git push`, `git pull`).

## Checks to run before every commit

- `dotnet run --project tools/tests/DestructionTests.csproj`: unit tests for
  the Unity-free code: destruction math (`DamageShape.cs`, `PixelAlgorithms.cs`),
  path finding (`Villagers/GridPathfinder.cs`), the zombies' flow field and
  waves (`Zombies/FlowField.cs`, `Zombies/WaveMath.cs`) and room finding
  (`Building/RoomFinder.cs`). Keep those files free of `UnityEngine` so the
  tests can compile them.
- `dotnet run --project tools/world-tests/WorldTests.csproj`: the endless world outside
  Unity (`Economy/NatureMap.cs` with stand-ins in `tools/world-tests/Stubs.cs` and the art
  preview's `Shim.cs`; `GroundArt.PatchMasksAt`/`TintAt` squares joining up).
- `dotnet run --project tools/net-tests/NetTests.csproj`: the online code that
  doesn't need Unity (`Assets/Scripts/Networking/Core`, plain C#: keep `UnityEngine`
  out of it) with LiteNetLib, run as a real server and real players over UDP.
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
