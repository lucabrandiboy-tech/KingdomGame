# Playing online: the plan

What you asked for: host a **dedicated server** on your own PC, a big map (since then:
an **endless, randomly generated world**),
friends join over the internet, **everyone has their own kingdom**, and **zombie
hordes come every night** in a world that keeps running.

This is the biggest change the game has had. Today almost every part of it assumes
one player and one kingdom, so it's built in stages. Each stage ends with something
you can test.

## How it works

- **Each player's computer runs their own kingdom** (changed in stage 3, see
  `DECISIONS.md` section 16): villagers, building, storage and research run on your
  computer exactly as offline, and it sends the others what's built and where its
  villagers are. The host or server passes that on, and keeps each player's kingdom
  for when they come back. Zombies (stage 4) will be run by the host or server.
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

1. **Big map** (done), then **endless worlds** (done, `docs/ENDLESS-WORLD-TEST.md`).
   Trees, rocks and bushes are data, worked out from the seed wherever someone goes;
   only the ones near players and kingdoms are real objects, and only changes are saved.
   The ground is made in squares round the camera. Also works offline.
2. **Connecting** (done, `docs/ONLINE-TEST.md`). You **host** your own game from the
   main menu and play it while friends **join** with your address. Everyone sees each
   other walk around, chats, and each new player gets a kingdom spot. (A dedicated
   server with no player, started with `-server`, also exists for later.)
3. **Own kingdoms, synced** (done, `docs/ONLINE-TEST.md` part 4). Everyone who joins
   gets a real kingdom: villagers, building, research, weapons, saved by the host.
   Everyone sees everyone's walls, buildings and villagers; late joiners see all of
   it. Not yet: zombies for players who joined, others' roofs and house insides,
   trees cut by others.
4. **Nights.** Hordes every night at every kingdom, getting harder over the days.
   Zombies, towers, traps and wall damage are synced. Falling means respawning at
   your throne.
5. **The server keeps the world.** Started: a hosted game's save and a dedicated
   server's world file keep every player's kingdom spot, where they were, what each
   kingdom has built and each player's whole kingdom (since stage 3). Then we measure
   with test players (bots) and tune how much each player is sent.

Steam, server browsers, PvP raiding, clans and anti-cheat are later phases (see
`ROADMAP.md`).

## Honest limits

- None of this can be run in Unity from the cloud. The networking part is tested
  with real connections outside Unity. Everything that needs Unity is untested until
  you try it.
- Your own character's movement and your own kingdom are decided by your computer;
  the server only checks movement is believable (not too fast). That's the simple,
  responsive way, and fine between friends, but a modded game could cheat. Proper
  anti-cheat comes later.
- The "100+ players" goal from the brief needs measuring (stage 5). The first goal is
  you and a few friends playing smoothly.
