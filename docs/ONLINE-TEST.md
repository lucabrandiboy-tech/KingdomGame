# Testing online play (hosting on your own PC)

**What works so far:** you host your own game (survival or free build) and play it as
normal while friends join. Everyone sees each other walk around and chats with **Enter**.
Every player who joins gets **a real kingdom of their own** in the same endless world
(villagers, building, research, weapons), runs it on their own computer, and sees
everyone else's walls, buildings and villagers. The host keeps each player's kingdom
for when they come back (part 4 below).

**What doesn't yet:** players who joined have no zombies (only the host's game has
them), others' walls can't be damaged, roofs and the insides of other players' houses
and mines aren't shown, and trees someone else cuts down are still standing on your
screen. Zombies online are the next step. See `docs/ONLINE-PLAN.md`.

Nothing here has been run in Unity yet. The networking part was tested with a real
server and players outside Unity, which is the part I can run here. Tell me what goes
wrong.

## 1. First, on one computer (two copies of the game)

A game can only join another game that's running separately, so you need a **build**
(the game as an .exe) as well as the Unity Editor:

1. In Unity: **KingdomGame > Build Game Scenes** (once, it adds the Network Config).
2. **File > Build Profiles** (or Build Settings) > make sure *MainMenu* and *Kingdom*
   are in the scene list (MainMenu first) > **Build**. Pick an empty folder, e.g. `C:\Dev\KingdomBuild`.
3. Run the built `.exe`. Main menu > **Play online** > type a name > **Host free build**.
   (Windows may ask whether to allow it through the firewall: click **Allow**, both boxes if there are two.)
4. Back in Unity, press Play. Main menu > **Play online** > type a **different name** >
   address `127.0.0.1` > **Join**.
5. You should see:
   - "Joining 127.0.0.1..." and then the map.
   - Your new kingdom near the host's, about 560 tiles away (a few minutes' walk: each
     kingdom has lands 512 tiles across): a throne, a storehouse with the starting wood,
     stone and food, and villagers, just like a new game alone.
   - The other ruler walking about, with their name over their head, moving smoothly.
   - A throne marker with the host's name at the host's kingdom.
   - Enter, type, Enter: the message shows in both windows.
6. The host's **Esc** menu shows "Hosting on UDP port 7777" and your home network address.
   The world doesn't pause online.
7. **Joined players are saved with the host's game.** Walk the joined player somewhere,
   then in the host window press Esc > Save. Close both. Host again with **Continue** on
   the online page, and join again with the **same name** from the same game: you should
   get the same kingdom back and start where you were standing. A new name counts as a
   new player (your game keeps a code plus your name to know who you are).

Tip: in the Unity Editor, *Edit > Project Settings > Player > Run In Background* lets the
unfocused window keep going (the game also sets it while online).

## 2. With a friend on your home network

Same as above, but your friend types the address from your **Esc** menu (like `192.168.1.20`).

## 3. With a friend over the internet

Your router has to send the game's traffic to your PC. This is called **port forwarding**:

1. Find your PC's home address: the host's Esc menu shows it (like `192.168.1.20`).
2. Open your router's page in a browser (often `192.168.1.1` or `192.168.0.1`; the
   address and password are often on a sticker on the router).
3. Find **Port forwarding** (sometimes under *Advanced*, *NAT* or *Virtual server*) and add:
   protocol **UDP**, port **7777**, to your PC's home address.
4. Find your **public** address: search "what is my IP" in a browser.
5. Your friend joins with that public address. If your port isn't 7777, they add it: `81.2.69.160:7778`.

If it doesn't connect: check the Windows firewall allowed the game (step 1.3), that the
forward is **UDP** (not TCP), and that you're hosting at the time. Some internet providers
don't give you a public address of your own ("CGNAT"); if nothing works, tell me and
we'll look at the other options.

## 4. Everyone's own kingdom

Two copies on one computer as in part 1 (host in the .exe, join from the Editor) is enough.

1. **Joined player builds.** In the joining window, build like in a game alone: a few
   wall tiles, a door, a house, a torch. Your villagers build them.
2. **The host sees it.** Walk the host's ruler towards the joined player's kingdom
   (the cheat menu is off online, so it's a walk of a minute or two). From about 400
   tiles away the kingdom appears: walls, buildings, lit torches at night,
   the villagers walking about carrying things. Walk up to their door: it opens for you.
   Their walls stop you; shooting them does nothing.
3. **The other way round.** Build something at the host's kingdom: the joined player
   sees it appear within a second or so if they're near.
4. **Coming back.** In the joining window: Esc > **Leave the game** (it says
   "Kingdom saved on the host" first). Join again with the same name: your kingdom
   comes back as you left it ("Your kingdom is back"), villagers, storehouse stock,
   research and all. While you were away, the host could still see your kingdom.
5. **Kept by the host's save.** Host: Esc > Save, close both. Host again with
   **Continue**, join again with the same name: still your kingdom.
6. **A late joiner sees everything.** With something built in both kingdoms, join with
   a third name (a second build of the game, or close and rejoin with a new name): both
   kingdoms show up when you get near them.

What to tell me: anything that doesn't appear, appears in the wrong place or facing the
wrong way, anything you can walk through that you shouldn't, and how smooth it is
(F3 shows frames per second) near a big kingdom.

## Settings

`Assets/Data/Network/NetworkConfig`: port, most players, server name, how far apart
kingdoms are (560 tiles), how often positions are sent.

Hosted games save to their own files (Continue on the online page), separate from your
games alone. The save holds the players who joined too: their kingdoms' spots and where
each one was standing (while they're on, where they are at the save; otherwise where they
were when they left). Their kingdoms stay in the world while they're away.

## A server with no player (optional, later)

The game can also run as a dedicated server with no player of its own: start the built
game with `-server` (plus `-batchmode -nographics` to run without a window). Players
get free build there (everything open); add `-survival` and they research the ages
instead. For now that's only useful for testing. Hosting from the menu is the way to play.

A dedicated server keeps its world in `saves/server-world.json` (in the game's folder,
`C:\Users\you\AppData\LocalLow\...\saves`): the seed, the time of day, and every player's
kingdom and spot. It's written every minute and when the server stops, and read the next
time it starts, so players come back to the same world. `-world name` uses another file
(`server-name.json`); `-newworld` starts a new world (the old file is kept as `.bak`).
