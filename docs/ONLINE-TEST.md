# Testing online play (hosting on your own PC)

**What works so far:** you host your own game (survival or free build) and play it as
normal while friends join. Everyone sees each other walk around, chat with **Enter**,
and every player who joins gets their own kingdom spot (a throne and storehouse) on
the same map.

**What doesn't yet:** players who joined see the map and everyone walking, but not
yet what you've built, your villagers, the zombies or wall damage. They can't build,
gather or shoot yet. Sharing all of that is the next online step. See
`docs/ONLINE-PLAN.md`.

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
   - Your new kingdom (a throne and a storehouse) somewhere near the host's, about 96 tiles away.
   - The other ruler walking about, with their name over their head, moving smoothly.
   - A throne marker with the host's name at the host's kingdom.
   - Enter, type, Enter: the message shows in both windows.
6. The host's **Esc** menu shows "Hosting on UDP port 7777" and your home network address.
   The world doesn't pause online.

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

## Settings

`Assets/Data/Network/NetworkConfig`: port, most players, server name, map size for
hosted games (1024 by default), how far apart kingdoms are, how often positions are sent.

Hosted games save to their own files (Continue on the online page), separate from your
games alone.

## A server with no player (optional, later)

The game can also run as a dedicated server with no player of its own: start the built
game with `-server` (plus `-batchmode -nographics` to run without a window). For now
that's only useful for testing. Hosting from the menu is the way to play.
