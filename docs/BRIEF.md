# Untitled Kingdom Survival Game: Claude Code Project Prompt

You are helping me build an **online multiplayer, top-down, pixel-art kingdom builder and survival game** in **Unity**, stored on **GitHub** and released on **Steam (Early Access)**. Players build huge kingdoms, automate production, advance from the primitive age to World War 2, survive constant zombie sieges, and fight and raid other players' kingdoms on persistent servers that wipe.

**I am a total beginner with Unity and C#.** Explain every step plainly, tell me exactly what to click in the Unity Editor when something can't be done from code, and define terms the first time you use them. Work in small, testable steps. After each step, tell me how to test it and what I should see. Never tell me something works unless you have actually verified it. If something is untested, broken or a placeholder, say so clearly.

The game is untitled. Use a placeholder project name like `KingdomGame` and keep the name easy to change later.

---

## 1. Core Concept

- **View:** Top-down. You control a **ruler character** in real time with **twin-stick controls** (WASD to move, mouse to aim and shoot or swing).
- **Throne view:** When your character sits on the **throne**, the camera pulls out to a **top-down overview of your entire kingdom**. From there you place structures on a **grid**, queue blueprints and manage the kingdom. The world **does not pause** (it's multiplayer), so you are vulnerable while on the throne.
- **Building:** Structures are placed as blueprints. **Builder villagers** construct queued blueprints automatically, and the player character can also build.
- **Loop:** Gather → build → automate → research → defend against zombies and players → raid → advance eras.
- **Kingdoms are very big.** They grow with no hard size limit; you can build anywhere you can hold and defend.

---

## 2. Art Style and Destruction (very important)

### Graphics
- **Detailed 2D pixel art, viewed top-down.** Think of the charm of classic side-view pixel-art sandbox games, but drawn for a top-down camera and with **much higher detail and polish**:
  - Richer palettes and more shading
  - Animated foliage, water, fire and smoke
  - 2D dynamic lighting and shadows (Unity URP 2D Renderer)
  - Day/night lighting, weather effects
  - Screen shake and impact effects
- Keep a **consistent pixel density** (pick one pixels-per-unit value and stick to it) and use **Pixel Perfect Camera** so pixels stay crisp.
- Use **original art only.** Do not copy sprites from any existing game. Free asset packs are fine if their licenses allow commercial use; record every asset's source and license in `CREDITS.md`.

### Pixel-level destruction
- **Every structure can be broken.** This includes walls, gates, towers, houses, factories and rails.
- When a structure takes damage, **individual pixels break off the sprite and fly away as physics particles**. You should see the exact damaged pixels chip out of the wall.
- Damage is applied **where the hit lands**:
  - Bullets chip small holes.
  - Explosions blow out large craters.
  - Brute zombies smash chunks.
- Structures update their **collision shape** after damage, so units can walk or shoot through holes.
- Heavily damaged structures **collapse**: detached chunks break apart and fall or scatter with physics.
- **Performance rules for this system:**
  - Pool pixel particles.
  - Cap how many particles exist at once.
  - Have debris settle and fade or merge into static rubble decals.
  - Only regenerate colliders for chunks that changed.
  - Split large structures into chunks (for example 32×32 pixel tiles) so damage only rebuilds small pieces.
- **Multiplayer:** Sync destruction as **damage events** (position, radius, damage type, seed), not per-pixel data. Clients reproduce the same damage locally. Flying pixels are **cosmetic and client-side only**. The server owns the authoritative structure state (which chunks and pixels remain) so late-joining players load the correct damaged state.

Build a **standalone destruction prototype early** (Phase 1) and test performance with many walls being destroyed at once before building other systems on top of it.

---

## 3. Multiplayer and Servers

- **Online multiplayer** with **100+ players per server**.
- **Persistent servers with wipes.** The **server owner chooses the wipe schedule.**
- **Official servers and community-hosted servers.**
- **Community server customization:** server settings plus **plugin/mod support** (similar in spirit to how community servers mod survival games). Settings include:
  - Player cap
  - Map size
  - Wipe schedule
  - Gather and production rates
  - Era progression speed
  - Clan size limit
  - Zombie difficulty
- **Server-authoritative**, with **Easy Anti-Cheat** integrated later.
- **Networking library:** Recommend one suited to 100+ players, a large persistent world and many simulated units (consider Fish-Net, Mirror, Netcode for GameObjects and others). Explain the tradeoffs simply and pick one.
- Use **interest management** so players only receive updates for nearby areas.
- **Persistence:** The server saves the world state (kingdoms, structures, damage, storage, units) to disk/database and survives restarts. Recommend an approach.
- **What carries over between wipes:** cosmetics, stats and saved blueprints only.
- **Leaderboards:** global and per-server.

---

## 4. Eras and Tech Tree

8 eras, each with its own tech tree:
1. Stone Age
2. Bronze Age
3. Iron Age
4. Medieval
5. Renaissance
6. Industrial
7. World War 1
8. World War 2

- **Pace:** Active players should be able to reach WW2 within one wipe (tunable by server setting).
- **No balancing between eras.** A WW2 kingdom can crush a primitive one. There is **no new-player protection**. Keep both as server-config options in case I change my mind.
- **Weapon and gear rule:** Your character can **craft anything you have the materials and knowledge for**, but **units can only use equipment from tiers your kingdom has researched**.
- **UI changes with progression:** The UI theme depends on your **armor tier and intelligence/tech tier**, starting crude and primitive and becoming more refined, industrial and military in later eras.

---

## 5. Economy and Automation

### Resources
- **Basic:** wood, stone, food
- **Metals:** iron, copper, steel
- **Late eras:** coal, oil, electricity

### Villagers
- Villagers have needs: **food, housing and happiness**. Unhappy villagers work slower or leave.
- **Population grows** with housing and food.
- Villagers are assigned to jobs and production chains, **Factorio-style**.
- **In later eras, machines replace villagers**, so villager management becomes much less important as you advance.

### Logistics
Logistics advance by era:
- Villagers carry goods by hand
- Carts and roads
- Conveyor belts (Industrial and later)
- Trains and trucks (late eras)

### Storage
- Players build **storage buildings**. Resources live in physical storage, which can be raided or destroyed.
- The character has a **limited personal inventory**.

### Trading
- A **central market in the middle of the map** is a **safe zone** (no combat).
- Players set up **shops in their own bases**. Others can browse and **order items from those shops** at the market.

---

## 6. Ruler Character

- **Controls:** Twin-stick (WASD + mouse aim).
- **Getting stronger:** Gear only, with no character levels.
- **Death:** You respawn at your **throne** and **lose the items you were carrying** (they drop where you died and can be looted).
- **Travel across your kingdom:**
  - Horses/mounts (early)
  - Drivable cars and trucks (later)
  - Your own trains
  - Fast travel between owned buildings
- **Raiding:**
  - Lead your army and fight in the battle yourself.
  - Solo raids (sneaking in, sabotage, stealing) are possible but **intentionally very hard**.

---

## 7. Zombies

- Zombie pressure is **attracted by noise and kingdom size**: bigger, louder, more industrial kingdoms draw bigger hordes.
- **Scales with your era.**
- **Stronger at night.**
- **Types:**
  - Basic walkers
  - Fast runners
  - Brutes that smash walls (big destruction)
  - Specials: spitters, exploders, climbers
- **Zombies drop loot.**
- **Violence:** Gory zombie kills (blood and gibs, rendered in pixel art).

---

## 8. Military and Raiding

### Units
- **Infantry by era:** clubmen through riflemen
- **Cavalry to armor:** horses through tanks
- **Siege and artillery:** catapults through artillery
- **Aircraft** in late eras: biplanes through WW2 fighters and bombers
- **Naval:** boats through warships

### Rules
- **Army size is capped** by era and buildings (no upkeep).
- **Battles are automatic:** units fight on their own AI.
- During a battle, players can **watch and give simple orders**: retreat, focus a target.
- **Armies travel across the map in real time** and can be intercepted.
- **Offline raids are allowed.** Defenses and garrisons fight back automatically.
- **Raid rewards:** steal a percentage of stored resources **and** conquer land or buildings.
- **Alerts:**
  - In-game alert when an enemy army approaches
  - Offline notifications (Steam/Discord or similar; recommend a method)

### Defenses
- Walls, gates, towers
- Traps: spikes, mines
- Garrisoned troops
- Turrets that upgrade by era: archer towers through machine-gun nests and artillery

### Scouting
- **Fog of war.** Players need scouts to see other kingdoms.

---

## 9. Social Systems

- **Clans:** The server owner sets the max clan size. Clan members can:
  - Send troops to defend allies
  - Run joint raids
  - Share resources
  - Use clan chat
- **Diplomacy between kingdoms:**
  - Truces and peace treaties
  - Formal war declarations
  - Tribute (pay to avoid raids)
- **Chat:** global text chat, clan text chat, proximity voice, clan voice.

---

## 10. World

- **Procedurally generated map**, different every wipe. The server owner chooses the size.
- **Biomes and terrain** with water (naval units need oceans, rivers and lakes).
- **Day/night cycle and seasons.** Seasons affect food production and visuals.
- **Weather:** rain, snow, fog.

---

## 11. Cosmetics, Pricing and Audio

- **Pricing:** Paid game, plus optional cosmetics.
- **Cosmetics (no gameplay advantage):** banners/flags, building skins, unit skins, kingdom themes.
- **Music:** Tense survival music.
- **Audio:** Strong impact sounds for destruction. Every pixel shatter should feel satisfying.

---

## 12. Tech Setup

- **Unity**
  - Recommend a current LTS version and explain why.
  - Use the **URP 2D Renderer** for 2D lights.
- **C#**, with clean folders such as:
  - `Scripts/Destruction`
  - `Scripts/Networking`
  - `Scripts/Building`
  - `Scripts/Economy`
  - `Scripts/Units`
  - `Scripts/Zombies`
  - `Scripts/Character`
  - `Scripts/UI`
  - `Scripts/World`
- **Data-driven:** All numbers (costs, health, damage, rates, era unlocks) live in ScriptableObjects or config files, never hardcoded.
- **Repo:** **Private GitHub repo with Git LFS** for art and audio. Set up a proper Unity `.gitignore` and `.gitattributes` and walk me through it.
- **Assets:** Free pixel-art asset packs (commercial-use licenses only) plus simple art and effects generated in code. Track every license in `CREDITS.md`.
- **Platform:** Windows on Steam (Steamworks integration later).

---

## 13. Build Order

Work through these phases in order. **Do not start the next phase until the current one is tested and I confirm it works.**

**Phase 0: Setup**
- Install Unity
- Create the 2D URP project
- Set up the GitHub repo with LFS
- Configure Pixel Perfect Camera and pixel settings

**Phase 1: Destruction prototype**
- One top-down character with twin-stick controls and a gun
- A few walls that chip pixel by pixel, with flying pixel particles, collider updates and collapse
- A stress test: many walls being destroyed at once, with FPS reported

**Phase 2: Single-kingdom offline prototype**
- Throne view with grid placement
- Builder villagers constructing blueprints
- Gathering wood, stone and food
- Storage buildings
- Villager needs
- Basic zombie waves attracted by noise/size
- Day/night cycle

**Phase 3: Production and eras**
- Production chains and villager jobs
- Logistics (hand carry, then carts)
- Tech tree for the first 2–3 eras
- Crafting
- Unit tier restrictions

**Phase 4: Military**
- Unit training and army caps
- Automatic battles with simple orders
- Defenses, traps and turrets
- Zombie types

**Phase 5: Multiplayer foundation**
- Dedicated server with several players
- Synced kingdoms, characters and destruction events
- Persistence across restarts

**Phase 6: PvP**
- Armies traveling and interception
- Offline raids
- Loot and conquest
- Fog of war and scouts
- Raid alerts

**Phase 7: Social and economy**
- Clans and diplomacy
- Central safe-zone market with player shops
- Chat and voice

**Phase 8: Remaining eras and content**
- Industrial through WW2: conveyors, trains, trucks, aircraft, naval units
- Seasons and weather
- Procedural world polish

**Phase 9: Steam and servers**
- Steamworks integration
- Server browser
- Community server settings and plugin support
- Easy Anti-Cheat
- Leaderboards and cosmetics
- Wipes
- Early Access prep

---

## 14. How to Work With Me

- Start by asking any questions you still have, then propose your plan for Phase 0.
- Keep explanations beginner-friendly.
- Commit to GitHub after each working step, with clear commit messages.
- Be honest about what's hard, what's risky, and what you haven't tested.
- If a feature will hurt performance with 100+ players or big kingdoms, warn me and suggest an alternative instead of silently cutting it.
