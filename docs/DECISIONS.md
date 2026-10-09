# Decisions

The big technical choices, why we made them, and what they cost. Each one says
whether it is **decided** or a **recommendation** to confirm when we reach that
phase. If you change your mind about one, change it here first.

---

## 1. Engine: Unity 6.3 LTS (decided)

- **LTS** (Long Term Support) versions get two years of bug fixes without
  changes to how things work. Unity 6.3 is the newest LTS and is supported
  until **December 2027**. Unity 6.0 LTS support ends in October 2026, so it's
  not worth starting on.
- Newer numbers (6.4, 6.5, 6.6) are "update" releases: new features, short
  support. Tutorials and asset packs also lag behind them.
- When the next LTS comes out we can upgrade **between** phases, never in the
  middle of one, after committing everything.

## 2. Graphics: URP with the 2D Renderer (decided)

The "Universal 2D" template sets this up. It gives us 2D lights (torches,
muzzle flashes, explosions), 2D shadows from walls, day/night lighting,
post-processing (bloom on fire), and the Pixel Perfect Camera.

## 3. Pixel density: 32 pixels per tile, 640 × 360 view (decided)

- **One grid tile = one Unity unit = 32 × 32 pixels.** Every sprite uses this
  density. It's set in one place: `Assets/Scripts/Core/PixelArt.cs`.
- **Reference resolution 640 × 360** (20 × 11 tiles on screen). It scales by a
  whole number on every common 16:9 screen (3× on 1080p, 4× on 1440p, 6× on
  4K), so pixels stay square and everyone sees the same amount of world. If it
  feels too zoomed-in in Phase 1, 960 × 540 is the next option; changing it
  doesn't touch any art.
- **Pixel Perfect Camera settings:**
  - *Grid Snapping: Upscale Render Texture.* The whole scene is drawn at
    640 × 360 and then scaled up. Rotated guns, particles and flying debris
    pixels stay real pixels instead of turning into tiny half-pixels. The
    downside is that the camera moves in whole-pixel steps; if that looks
    jumpy in Phase 1 we'll smooth it.
  - *Crop Frame: None.* Ultrawide and 16:10 screens see slightly more world.
    That's a small advantage in PvP; we'll revisit it before Phase 6
    (letterboxing would make it equal).
- **Import settings** (applied automatically to `Assets/Art` by
  `PixelArtImporter`): Sprite, 32 PPU, Point filter (no blur), no compression,
  no mipmaps.
- **Known problem for Phase 2:** the throne view zooms far out, and Pixel
  Perfect Camera only allows whole-number zoom steps. Planned approach: the
  throne view switches the camera into a separate zoomed-out mode (pixel
  snapping off, or a simplified map drawing for very large kingdoms). Not
  solved yet.
- **Asset packs:** pick packs drawn at 32 × 32. A 16 × 16 pack would look
  chunkier than everything else.

## 4. Controls: Input System package (decided)

Unity's newer input system. Keyboard + mouse twin-stick now, gamepads and key
rebinding later without rewriting the controls.

## 5. Code layout (decided)

- All our code is in `Assets/Scripts/<Area>/`, namespace `KingdomGame`.
  Editor-only tools are in `Assets/Scripts/Editor/`.
- Our art: `Assets/Art`. Asset packs: `Assets/ThirdParty`, each one listed in
  `CREDITS.md`.
- **Data-driven:** gameplay numbers (costs, health, damage, rates, era unlocks)
  go in ScriptableObject assets under `Assets/Data`, never in code. The pixel
  density constants are the one exception: they're an engine setting, not a
  gameplay number.
- **No assembly definitions yet.** They make Unity recompile faster in big
  projects, but every new package then has to be wired up by hand. We'll add
  them when compile times get slow.
- **The name "KingdomGame" is a placeholder.** Players only see the *Product
  Name* (Edit > Project Settings > Player). Renaming the game later means
  changing that and renaming the GitHub repo; the code namespace can stay.

## 6. Repo: private GitHub repo with Git LFS (decided)

- Pictures, sounds, fonts and DLLs go to Git LFS (`.gitattributes` lists
  them). The free GitHub plan includes 10 GiB of LFS storage and 10 GiB of LFS
  downloads per month. Pixel art is tiny; music and sound effects will be most
  of it. Uploading doesn't count toward the download limit.
- The **Repo checks** action runs after every push. It fails if an asset is
  missing its `.meta` file or a big file skipped LFS. It can't open Unity, so
  it doesn't prove the game works.

## 7. Networking library (recommendation, final decision at the start of Phase 5)

What matters for this game: 100+ players, a big persistent world, lots of
units, **dedicated servers that the community can host themselves**, and a
beginner writing the code.

| Library | Cost | Self-hosted dedicated servers | Fit for 100+ players | Beginner-friendly |
|---|---|---|---|---|
| **Fish-Net** | Free, open source | Yes | Good: built-in interest management ("observers"), client prediction, server authority | Medium, with good docs and an active Discord |
| Mirror | Free, open source | Yes | OK. The most proven, but in Fish-Net's own (2023) benchmark it used much more server CPU at 100 players | Most tutorials of any |
| Netcode for GameObjects (Unity) | Free | Yes | Aimed at smaller co-op games, not 100-player worlds | Easy |
| Netcode for Entities (Unity) | Free | Yes | Best scaling | **Hard**: the whole game must be written in Unity's ECS style from day one |
| Photon Fusion | Paid per player online | Through Photon's cloud and licensing | Very good | Medium |

**Recommendation: Fish-Net.** Free, self-hostable (community servers and
plugins), server-authoritative, and its observer system is exactly the
"interest management" this game needs. Photon is out because community servers
would depend on Photon's paid cloud. Netcode for Entities would scale best,
but it's a different way of writing every system and too steep for a first
Unity project.

Before Phase 5 starts we'll check that the current Fish-Net release supports
our Unity version and run a small test.

**Honest warning about scale:** no networking library makes "100+ players,
each with a huge kingdom, hundreds of villagers, armies and zombie hordes"
work by itself. The library just sends messages. The real limits are the
server's CPU (unit AI, pathfinding, physics) and network bandwidth. The plan:

- Players only receive updates for things near them (interest management).
- Units far away from every player are simulated cheaply: no physics, updated
  every few seconds, armies moved as one group.
- Destruction is sent as small damage events, never pixel data (see the brief).
- From Phase 5 on we measure with bot players and tell you the real numbers.
  If 100+ doesn't hold, the options are a lower default player cap or
  simplifying what is simulated, not quietly dropping features.

## 8. Saving the world on the server (recommendation, decide in Phase 5)

**SQLite**: a whole database in a single file, no separate database server to
install, which keeps community hosting simple.

- The server autosaves every few minutes and on shutdown.
- Wall damage is stored per 32 × 32-pixel chunk as a compressed "which pixels
  are left" map, so late joiners and restarts get the exact damaged walls.
- A wipe starts a new world file. Cosmetics, stats and saved blueprints (the
  things that survive wipes) live in a separate account database.
- If official servers later need shared accounts across servers, that part can
  move to a central database server (for example PostgreSQL) in Phase 9.

## 9. Offline raid alerts (recommendation, decide in Phase 6)

Steam doesn't give games a way to message players who aren't playing. The
usual answer in survival games is **Discord**: players link their Discord
account in-game, and a small bot sends them a direct message when their
kingdom is attacked. In-game alerts for online players are built into the game
itself.

## 10. Anti-cheat (decided: later)

The server is the authority on everything (damage, resources, positions), which
blocks most cheats. **Easy Anti-Cheat** (free through Epic Online Services,
works with Steam games) gets added in Phase 9.
