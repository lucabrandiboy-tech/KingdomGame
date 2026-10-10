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
braziers, explosion flashes), day/night lighting, and post-processing (the
dark corners, glow and color grading of the look below).

## 3. Look: dark, realistic 2D seen from straight above (decided in Phase 2)

You chose this over pixel art after seeing a dark 3D reference picture. It
stays 2D (flat sprites, a top-down camera), which keeps the destruction system,
the performance and the art pipeline simple, but it aims for a gritty,
realistic look instead of crisp pixels.

- **Straight top-down.** People, trees, rocks and buildings are drawn as seen
  from above. Bodies turn to face where they aim or walk (the art is drawn
  facing right and rotated). Nothing is sorted by its height on screen any
  more: the drawing order is fixed per kind of thing (`Core/SortOrder.cs`).
  Tree leaves are above everyone and fade when the ruler walks under them.
- **One grid tile = one Unity unit.** Wall destruction works on 64 × 64
  "health pixels" per tile (`Core/PixelArt.cs`); what you see is drawn in
  much more detail (next point).
- **High-detail baked art (you asked for about 1000 × 1000 per tile).** The art
  is drawn by code and *baked* by Build Game Scenes at the View Config's
  **Art Detail**, 1024 pixels per tile by default, into texture sheets in
  `Assets/Art/Generated` (not saved to Git: they're made again from the code)
  with a normal map for each (which way each pixel faces, so torchlight shows
  bumps). `Assets/Data/Look/ArtLibrary` records where each picture sits;
  `Core/ArtSprites` cuts the sprites out while the game runs. It only bakes
  again when the art code (`ArtCatalog.Version`), the detail or the wall colors
  change. If a picture isn't baked, the game draws it itself at 128 px per tile.
  - **Cost:** about 0.55 GB of video memory at 1024 (compressed, BC7), about
    0.15 GB at 512, and the first bake takes several minutes. The screen only
    shows about 45 pixels per tile at the normal zoom (24 tiles tall on a 1080p
    screen), so 1024 only looks different from 512 when zoomed in close
    (the mouse wheel zooms in to 4 tiles tall). Lower Art Detail to 512 if
    video memory or bake time is a problem.
  - A damaged wall chunk is redrawn at 128 px per tile (smoothed from its 64
    health pixels), so walls look a little softer once they're hit.
- **The ground** is three repeating textures (dark grass, dry khaki grass, mud)
  shown in big patches through sprite masks, with ragged edges (tufts of grass
  into the dirt) and a soft tint on top (darker under forests, blotches, and a
  blend along the patch edges).
- **No Pixel Perfect Camera.** Textures use smooth (trilinear) filtering with
  mipmaps, so art can rotate, scale and be shown at any zoom without jagged
  edges or shimmering. The camera zooms freely: walking view 24 tiles tall
  (`ViewConfig`, changeable), the throne view much further out.
  This also solved the old Phase 2B problem (pixel-perfect zoom only works in
  whole steps).
- **Light from the top-left** is baked into all art (the shading in the
  pictures), so trees and rocks aren't turned. People are drawn for 16
  (ruler) or 8 (villagers) facing directions, each lit from the top-left, and
  the nearest drawing is turned the last few degrees. **Soft shadows** fall to
  the bottom-right: blurred shadow sprites under trees, rocks, people and
  buildings; walls cast a shadow of their current shape (holes included).
- **Lights:** fires (braziers, torches, the storehouse lantern) always burn,
  much brighter at night, flicker, and use the normal maps. The day and night
  colors in `DayNightConfig` are as they look on screen: URP takes the global
  2D light's color as a linear light value without converting it (it converts
  other lights' colors), so `DayNightCycle` converts it.
- **Fake 3D debris.** Pieces breaking off walls are flat sprites drawn as real
  3D cubes, with 8 tumbling frames, a shadow that drifts away as they fly up,
  and a slight grow with height. Explosions add a flash of light, a fireball,
  smoke and a scorch mark.
- **Post-processing** (`Assets/Data/Look/PostProcessing`): vignette, bloom on
  bright things (fire, sparks, shots ignore lighting via the Glow material, so
  they glow at night), a little film grain, slightly less color. Contrast is
  only +5: URP applies contrast in a way that turns the darkest shades pure
  black, and this game is dark.
- **Art is drawn in code for now** (`Assets/Scripts/Art`, the list of every
  picture in `Art/ArtCatalog.cs`). `tools/art-preview` bakes everything like
  Unity does and renders mock screenshots (day, night, close-up, the outpost),
  lighting them the way URP does (linear color, URP's light falloff, bloom,
  contrast), so they can be checked without Unity. It's still an
  approximation. Real art (hand-made or licensed packs, drawn top-down with
  light from the top-left) can replace it piece by piece.
- **Import settings:** baked sheets get theirs from `PixelArtImporter`
  (plain texture or normal map, mipmaps, trilinear, high-quality compression,
  ground sheets repeat). Other images in `Assets/Art` get: Sprite, 64 pixels
  per unit, bilinear filter, mipmaps, no compression.
- **Performance:** sprites of the same kind share a texture sheet, so they
  draw in a few batches. Shadows double the number of sprites; if that shows
  up in the stress test, the first thing to try is baking static shadows into
  the ground.

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
  downloads per month. 2D art is small; music and sound effects will be most
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
- Wall damage is stored per 64 × 64-pixel chunk as a compressed "which pixels
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

## 11. How destruction works (Phase 1, decided; revisit after the stress test)

- **Chunks:** every grid tile of a wall is one 64 × 64-pixel chunk
  (`PixelChunk`). Walls are placed on whole grid cells, so the world keeps one
  map from cell to chunk (`PixelWorld`).
- **Undamaged chunks are almost free:** they share one of a few tile pictures
  per material and one collider box. A chunk only gets its own texture and
  pixel data on its first hit. A huge kingdom with mostly intact walls costs
  about the same as a small one.
- **Damage is an event** (`DamageEvent`: pixel position, radius, damage type,
  seed). `DamageShape` turns it into damaged pixels with whole-number math only,
  so every computer removes exactly the same pixels. This is the multiplayer
  rule from the brief, built in from the start.
- **Pixel health:** each pixel has health (set per material). Hits subtract
  damage that fades from the center to the edge; pixels at 0 break off,
  and pixels below half health get darker (cracks).
- **Bullets don't use physics.** They walk the pixel grid along their path
  (`PixelAlgorithms.Raycast`), so they fly through holes and hit the exact
  pixel they touch.
- **Colliders** for walking: each damaged chunk's collider is rebuilt from
  rectangles covering its remaining pixels (`CustomCollider2D`), once per
  frame and only for chunks that changed.
- **Crumbling:** after damage, a structure finds its separate pieces. Pieces
  smaller than *Min Standing Pixels* crumble, and a structure below *Collapse
  Below* of its pixels collapses. That's the "detached chunks break apart" rule
  for a top-down view, where nothing falls.
- **Flying pieces** (since Phase 2: little fake-3D cubes, see section 3; a share
  of the broken pixels becomes cubes) are pooled sprites (all made when the scene starts), capped
  at *Max Debris* and *Max New Debris Per Frame*. They pop up, fall, bounce and
  slide, then get painted into flat **rubble tiles**, so settled rubble costs
  nothing extra. They're only for looks and not part of the shared game state.
  - *Why sprites and not Unity's Particle System:* URP's 2D renderer only draws
    sprite-type shaders, and its sprite shaders read a per-sprite color that
    only sprite renderers set. Particles would need a custom shader, which can't
    be tested here. Pooled sprites are certain to work and get lit by 2D lights.
    If the stress test shows they're too slow, that's the first thing to change.

**Known costs to watch in the stress test:**

- Each *damaged* chunk has its own texture, so it's its own draw call. Many
  damaged chunks on screen at once could get slow. Fix if needed: pack damaged
  chunks into shared texture pages (atlases).
- Very large single structures (for example 10 × 10 tiles) make the crumble
  check slower. Buildings should be made of several smaller structures.
- The Unity Editor is slower than a real build; judge performance from a build
  before cutting features.

## 12. Rust-style wall toughness (decided in Phase 2)

Breaking into a base should take explosives, like in Rust.

- Every hit has a **category**: Bullet, Melee (tools, weapons, zombie claws) or
  Explosive. Every wall **material** says what percentage of each category gets
  through (`StructureMaterial`).
- Starting values: **stone** lets through 3% of bullets, 8% of melee and 100%
  of explosives; **wood** 8%, 35% and 140% (wood is weak to explosives).
  Pixel health: stone 255 (the maximum), wood 140.
- Result: a rifle only makes sparks on stone. A rocket dents a stone wall and
  two or three in the same spot break through. One C4 blows a large hole.
- Still whole-number math (damage × percent / 100), so multiplayer can keep
  sending damage events.
- Later eras need tougher materials (metal, armored walls). Pixel health is
  stored in one byte (max 255); tougher materials will either resist more or we
  move to two bytes per pixel. Decide when metal walls arrive (Phase 3).

## 13. Building and builder villagers (Phase 2B, decided)

- **Throne view.** Sitting on the throne (E) pulls the camera far out. You
  pick a building on the build bar (or 1-5), click to place it, or drag to
  place a straight line of walls. The world keeps running.
- **Blueprints, not instant buildings.** A placed building is a blueprint.
  Nothing is paid when you place it: builder villagers fetch the resources
  from storehouses, carry them over, then build. Cancelling gives back what was
  already delivered. The ruler can also hand over resources from the backpack
  and help build (hold E).
- **Resources live in storehouses.** The backpack has to be emptied into a
  storehouse (E) before villagers can use it. This is the brief's "physical
  storage that can be raided"; destructible storehouses come with raids.
- **Jobs:** idle villagers take the oldest blueprint that needs something, at
  most 2 builders per blueprint (`KingdomConfig`). They reserve what they're
  fetching, so two never go for the same stones.
- **Paths:** A* on the tile grid (`Villagers/GridPathfinder.cs`, unit-tested),
  8 directions, no squeezing between walls that touch at a corner, straightened
  where the way is clear. The grid knows walls, buildings, trees and rocks, and
  updates when they change; a villager whose way gets walled off finds another.
- **Villagers have no physics body,** so they can't get stuck on each other or
  on corners, and they don't push the ruler around. A building only appears
  once nobody is standing on its spot.
- **Performance with huge kingdoms:** a path search costs more the bigger the
  map. Today's map (96 × 96) is fine. With hundreds of villagers on a big
  server map we'll need cheaper searches (searching in larger regions first,
  sharing paths, spreading searches over several frames). Not needed yet, and
  noted so it isn't forgotten.

## 14. Rooms, doors and furniture (castle update, decided)

- **Three layers per cell** (`Building/Kingdom.cs`): floors at the bottom,
  rugs on them, and everything else (doors, furniture, chests, blueprints) on
  top. Walls live in the `PixelWorld` as before.
- **Rooms** are found with a flood fill from the map's edge over everything
  that isn't a wall or a door (`Building/RoomFinder.cs`, unit-tested): what the
  outside can't reach is a room, unless it's huge (over 300 tiles: that's just
  a walled-in area). Found again at most four times a second after something
  changes. A room is named after its furniture (`BuildingConfig.roomTag`).
- **Furniture turns** in quarter turns. Its art is drawn once facing down and
  turned before the top-left light is baked in, so every direction is lit the
  same way. Big pieces (beds, tables, shelves) block movement; chairs, benches,
  rugs, torches and banners don't.
- **Doors** block with a collider that's switched off while open. They open for
  anyone on your side within about a tile and stay open while someone stands
  in the doorway. A door can be placed on a single wall tile and replaces it
  when built (half the wall's cost back), so you don't have to tear a hole
  first. Doors turn to fit the walls beside them.
- **Half-detail art:** furniture, floors, doors, effects, zombies and bodies are
  baked at half the Art Detail (512 per tile by default, `ArtItem.Detail`).
  They never show bigger than about 270 screen pixels per tile (the closest
  zoom), and there are a lot of them: at full detail they'd add about 0.4 GB
  of video memory; at half, about 0.1 GB. Every picture's edge pixels are
  copied into the space around it in the sheet, so tiles that fill their cell
  (floors, walls) don't show seams when the view zooms out.

## 15. Zombies and survival (castle update, decided; revisit after testing)

- **Flow field, not a path per zombie** (`Zombies/FlowField.cs`, unit-tested):
  every few tenths of a second one Dijkstra search from every person outwards
  gives each cell its "effort" to the nearest person. Each zombie just steps to
  its cheapest neighbour, so 5 or 500 zombies cost the same search. The ruler
  is preferred: a villager has to be 2 tiles closer.
- **Breaking through is part of the path:** each cell has a cost. Trees, rocks
  and the throne can't be broken (zombies walk round); walls, doors,
  storehouses, chests and big furniture can, and cost more the more health
  they have left (8 health ≈ 1 tile of walking round). So zombies smash through
  a door or a wooden wall when that's quicker than the way round, and look for
  the weakest point of a closed castle. Everything you build can be broken, so
  nothing (a ring of crates, say) seals a castle for good; a broken storehouse
  or chest spills what's inside in a bag. The ruler on the throne is reached
  from the cells along the throne's sides.
- **What zombies can walk through is checked as it is right now** (a wall built
  a moment ago stops them), and the field is worked out again as soon as
  something is built, broken or torn down. A zombie that something is built on
  steps out to the nearest open cell.
- **Zombies wear walls down instead of breaking pixels.** A zombie's claws
  would need hundreds of blows to cut a hole through a stone tile pixel by
  pixel. Instead each blow takes the zombie's Wall Damage off the tile's
  **siege health** (stone 600, wood 200, less if explosions already broke
  pieces off) and leaves scratches (a tiny damage event: wood splinters,
  stone cracks). At 0 the tile caves in with the usual crumbling. Players'
  weapons still break pixels like before.
- **No physics bodies on zombies.** The director moves them (they slide along
  anything they can't walk through), pushes crowds apart, and keeps the ruler
  out of them (the ruler can't walk into a zombie and gets shoved out of a
  crowd). Shots find zombies through `Core/Health.cs`.
- **Shots fly over your own wall** for the first 1.6 tiles (rifle) so you can
  defend from behind a wall you stand next to; further walls stop them, and
  rockets always hit the first wall. Explosions hurt every zombie in the blast,
  and your side (you, villagers, doors, furniture, storehouses) a third as much
  (`DamageType.friendlyShare`), so a rocket at a zombie in your face hurts but
  doesn't kill you. C4 at your feet still does: run.
- **Waves** (`Zombies/SurvivalDirector.cs`, numbers in `SurvivalConfig`):
  6 zombies, then 3 more each wave, all times 1.05 per wave (wave 10: 51,
  wave 20: about 160), with 6% more health each wave. New kinds join over three
  waves. Bigger waves come from more sides. At most 120 alive at once; the
  rest wait their turn. Zombies that can't reach anyone for 25 seconds wander
  off, so a wave can't get stuck.
- **Performance:** the field search is about a millisecond on the 96 × 96 map;
  zombies check neighbours through a grid, not each other. Hundreds of
  zombies on a much bigger server map will need the field computed in pieces
  or less often far away. Not needed yet.

