# Playing online: the plan

What you asked for: host a **dedicated server** on your own PC, a **1024 x 1024** map,
friends join over the internet, **everyone has their own kingdom**, and **zombie
hordes come every night** in a world that keeps running.

This is the biggest change the game has had. Today almost every part of it assumes
one player and one kingdom, so it's built in stages. Each stage ends with something
you can test.

## How it works

- **The server runs the real game.** It decides where everyone's villagers walk,
  what gets built, what the zombies do and what breaks. Players' copies of the game
  show what the server tells them and send it what they want to do ("build a wall
  here", "I walked here").
- **Built things go over the network as events** ("a stone wall went up on these
  cells", "this blow hit the wall here"). Every computer rebuilds the same result
  itself. Wall damage was written for this from the start: every computer breaks
  exactly the same pixels from the same hit. Things that move (players, villagers,
  zombies) are sent as positions about 15 times a second, for things near you only.
- **Someone joining late** gets the state of what's near them, in the same format
  the save system uses.
- **Networking library: LiteNetLib** (free, MIT license, plain C#), not Fish-Net as
  `DECISIONS.md` suggested earlier. Why: everything in this game is built by code
  at runtime, while Fish-Net expects prefabs and generates code inside Unity. I can't
  run either inside Unity from here, but I can run LiteNetLib with real network
  connections, a test server and test players in the cloud. That way the networking
  is actually tested before you try it.

## Stages

1. **Big map.** Trees, rocks and bushes become data. Only the ones near players and
   kingdoms are real objects (about 140,000 on a 1024 map would be far too many).
   The ground is drawn more cheaply on big maps. Also works offline.
2. **Connecting.** A dedicated server mode: start the game with `-server`, or press
   "Host a server" in the main menu. Plus "Join" with an address, your name, seeing
   each other walk around, chat, and the server choosing a spot for each new
   player's kingdom. *Test: you and a friend on the same map.*
3. **Own kingdoms, synced.** Several kingdoms in one world. Building, blueprints,
   villagers, storage and research all go through the server. Late joiners see
   everything.
4. **Nights.** Hordes every night at every kingdom, getting harder over the days.
   Zombies, towers, traps and wall damage are synced. Falling means respawning at
   your throne.
5. **The server keeps the world.** It saves to disk every few minutes and on
   shutdown, and loads again on start. Then we measure with test players (bots)
   and tune how much each player is sent.

Steam, server browsers, PvP raiding, clans and anti-cheat are later phases (see
`ROADMAP.md`).

## Honest limits

- None of this can be run in Unity from the cloud. The networking part is tested
  with real connections outside Unity. Everything that needs Unity is untested until
  you try it.
- To start with, your own character's movement is decided by your computer, and
  the server only checks it's believable (not too fast, not through walls). That's
  the simple, responsive way. Proper anti-cheat comes later.
- The "100+ players" goal from the brief needs measuring (stage 5). The first goal is
  you and a few friends playing smoothly.
