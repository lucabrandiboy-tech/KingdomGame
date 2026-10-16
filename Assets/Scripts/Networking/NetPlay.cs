using System;
using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Economy;
using KingdomGame.UI;
using KingdomGame.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomGame.Networking
{
    /// <summary>The ruler's position and what they're doing, as sent over the network.</summary>
    public static class RulerMotion
    {
        /// <summary>
        /// <paramref name="last"/> is where it was last sent from: a move of more than a few
        /// tiles at once (a door, coming back after falling) is marked as a jump.
        /// </summary>
        public static Motion Of(RulerController ruler, ref Vector2 last)
        {
            Vector2 at = ruler.transform.position;
            var aim = ruler.AimDirection;
            var flags = MotionFlags.None;
            if (ruler.OnWalls) flags |= MotionFlags.OnWalls;
            if (ruler.Dead) flags |= MotionFlags.Dead;
            if (ruler.Inside != null) flags |= MotionFlags.Inside;
            if (ruler.Seated) flags |= MotionFlags.Seated;
            if (ruler.CurrentVelocity.sqrMagnitude > 0.02f) flags |= MotionFlags.Walking;
            if ((at - last).sqrMagnitude > 9f) flags |= MotionFlags.Jumped;
            last = at;
            return new Motion { X = at.x, Y = at.y, Aim = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, Flags = flags };
        }
    }

    /// <summary>
    /// Hosting: your own game, open to others. This computer runs the server
    /// (<see cref="ServerCore"/>) and you play on it at the same time: your ruler is one of
    /// the players. Others who join get a kingdom of their own (run on their computer), see you,
    /// each other, each other's kingdoms and villagers, and chat. Your kingdom is sent to them by
    /// a <see cref="KingdomMirror"/>; theirs are shown here by <see cref="RemoteKingdoms"/>.
    /// </summary>
    public sealed class NetHost : MonoBehaviour, ICoopLink
    {
        ServerCore core;
        PlayerSession me;
        RulerController ruler;
        RemotePlayers remotes;
        RemoteKingdoms kingdoms;
        ChatBox chat;
        Vector2 lastSent;
        bool failed;
        GUIStyle style;

        public ServerCore Core => core;

        // ---- For playing together (Coop): who's on, and messages to and from one of them.
        public int MyId => me != null ? me.Id : 0;
        public IEnumerable<PlayerInfo> Others
        {
            get
            {
                if (core == null) yield break;
                foreach (var p in core.Players)
                    if (p != me) yield return new PlayerInfo { Id = p.Id, Name = p.Name, KingdomId = p.KingdomId };
            }
        }
        public KingdomInfo KingdomOf(int kingdomId)
        {
            if (core != null) foreach (var k in core.Kingdoms) if (k.Id == kingdomId) return k;
            return null;
        }
        public void SendDirect(int to, DirectKind kind, byte[] bytes, bool reliable) => core?.LocalDirect(me, to, kind, bytes, reliable);
        public event Action<int, DirectKind, byte[]> Direct;
        public event Action<int> PlayerGone;

        /// <summary><paramref name="others"/>: the players who joined before, from the saved game (null: nobody yet).</summary>
        public void Setup(ServerSettings settings, Kingdom kingdom, RulerController rulerController, RemotePlayers remotePlayers, ChatBox chatBox,
            NatureMap nature, DayNightCycle dayNight, OthersSave others, RemoteKingdoms otherKingdoms, KingdomMirror mirror, bool survival)
        {
            ruler = rulerController;
            remotes = remotePlayers;
            kingdoms = otherKingdoms;
            chat = chatBox;
            core = new ServerCore(settings) { Survival = survival };
            core.Log += line => Debug.Log("[Host] " + line);
            core.Clock = () => dayNight != null ? (dayNight.Day, dayNight.Hour) : (1, 8f);
            core.SpotIsGood = (x, y) => KingdomGround.IsGood(nature, x, y);
            core.KingdomPlaced += k =>
            {
                KingdomGround.Clear(nature, k);
                remotes.AddKingdom(k);
                kingdoms.Place(k);
            };
            // The others' kingdoms and villagers, as their games send them.
            core.KingdomBuilt += (k, replaceAll, removed, added) => kingdoms.Apply(k, replaceAll, removed, added);
            core.FolkMoved += (k, time, folk) => kingdoms.HeardFolk(k, time, folk);
            core.Joined += p =>
            {
                remotes.Add(p.Id, p.Name);
                chat.Notice($"{p.Name} joined the game");
            };
            core.Left += p =>
            {
                remotes.Remove(p.Id);
                chat.Notice($"{p.Name} left");
                PlayerGone?.Invoke(p.Id);
            };
            core.DirectReceived += (from, kind, bytes) => Direct?.Invoke(from, kind, bytes);
            core.PositionTaken += (p, time) => remotes.Heard(p.Id, time, p.Motion);
            core.Said += (p, text) => chat.Add(p.Name, text);
            // The players who were here before (a saved game): their kingdoms are where they were, and each
            // of them gets theirs back (and comes back where they left off) when they join again.
            if (others != null)
            {
                others.Into(core);
                foreach (var k in core.Kingdoms)
                {
                    KingdomGround.Clear(nature, k);
                    remotes.AddKingdom(k);
                    kingdoms.Place(k);
                }
                // What they had built: shown even while they're away.
                foreach (var (k, pieces) in core.AllBuilt()) kingdoms.Apply(k, true, System.Array.Empty<BuiltPiece>(), new List<BuiltPiece>(pieces));
            }
            failed = !core.Start();

            // You're a player too, with the kingdom that's already here.
            var throne = kingdom != null && kingdom.Throne != null ? kingdom.Throne.Cells.position : Vector2Int.zero;
            var mine = new KingdomInfo { X = throne.x, Y = throne.y };
            KingdomGround.Clear(nature, mine);   // as the others' games clear it
            me = core.AddLocalPlayer(GameSession.PlayerKey, GameSession.PlayerName, mine);
            // Your kingdom, as the others see it.
            mirror.SendBuilt = (replaceAll, removed, added) => core.LocalBuilt(me, replaceAll, removed, added);
            mirror.SendFolk = folk => core.LocalFolk(me, folk);
            chat.Send = text => core.ChatLocal(me, text);
            chat.Notice(failed ? $"Couldn't open UDP port {settings.Port}: nobody can join (is another game hosting on this computer?)"
                : $"Hosting on port {core.Port}. Friends join with your address (Esc shows how).");
        }

        void Update()
        {
            if (core == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (ruler != null) core.MoveLocal(me, RulerMotion.Of(ruler, ref lastSent));
            core.Poll(now);
            remotes.RenderTime = now;
            kingdoms.RenderTime = now;
        }

        void OnDestroy() => core?.Stop();
        void OnApplicationQuit() => core?.Stop();

        /// <summary>How others join, for the pause menu.</summary>
        public string HowToJoin()
        {
            if (core == null) return null;
            if (failed) return $"<color=#ff8a80>Couldn't open UDP port {core.Settings.Port}, so nobody can join. Is another game hosting on this computer?</color>";
            var lan = NetServer.LocalAddresses();
            return $"<b>Hosting</b> on UDP port {core.Port}, {core.Players.Count} / {core.Settings.MaxPlayers} players.\n" +
                   (lan.Length > 0 ? $"On your home network friends join <b>{lan[0]}</b>." : "") +
                   " Over the internet: your public IP (search \"what is my IP\"), with the port forwarded on your router (docs/ONLINE-TEST.md).";
        }

        void OnGUI()
        {
            if (core == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, richText = true };
            style.fontSize = Mathf.Max(11, Screen.height / 60);
            string text = failed ? "<color=#ff8a80>Hosting failed: the port is in use</color>" : $"<color=#c8b890>Hosting: {core.Players.Count} player{(core.Players.Count == 1 ? "" : "s")}   (Enter: chat)</color>";
            GUI.Label(new Rect(0, 4, Screen.width, 30), text, style);
        }
    }

    /// <summary>
    /// Playing in someone else's game: connects (showing "Connecting..." until the host
    /// answers), hands the world's seed to the game to make the same world, then
    /// sends where your ruler is and shows the others. Your kingdom runs on this computer:
    /// the host keeps it for you (saved there every few minutes and when you leave) and hands
    /// it back when you join again; the others see it. If the connection goes, it says why.
    /// </summary>
    public sealed class NetClient : MonoBehaviour, ICoopLink
    {
        ClientCore core;
        readonly ServerClock clock = new ServerClock();
        readonly Dictionary<int, string> players = new Dictionary<int, string>();
        readonly Dictionary<int, int> playerKingdoms = new Dictionary<int, int>();
        readonly List<KingdomInfo> known = new List<KingdomInfo>();
        RulerController ruler;
        RemotePlayers remotes;
        RemoteKingdoms kingdoms;
        KingdomMirror mirror;
        World.SaveSystem saves;
        ChatBox chat;
        DayNightCycle dayNight;
        NatureMap nature;
        float sendEvery = 1f / 15f, sendAt;
        Vector2 lastSent;
        string address, failure;
        bool inWorld, leaving, quitting, kingdomBack;
        int myKingdom;
        GUIStyle big, style, button;

        /// <summary>The host answered: make the world (seed, size, kingdoms) and then call <see cref="Enter"/>.</summary>
        public Action<WelcomeInfo> WorldReady { get; set; }

        // ---- For playing together (Coop): who's on, and messages to and from one of them.
        public int MyId => core != null && core.Welcome != null ? core.Welcome.YourId : 0;
        public IEnumerable<PlayerInfo> Others
        {
            get
            {
                foreach (var pair in players)
                    if (pair.Key != MyId)
                        yield return new PlayerInfo { Id = pair.Key, Name = pair.Value, KingdomId = playerKingdoms.TryGetValue(pair.Key, out int k) ? k : 0 };
            }
        }
        public KingdomInfo KingdomOf(int kingdomId)
        {
            foreach (var k in known) if (k.Id == kingdomId) return k;
            return null;
        }
        public void SendDirect(int to, DirectKind kind, byte[] bytes, bool reliable) => core?.SendDirect(to, kind, bytes, reliable);
        public event Action<int, DirectKind, byte[]> Direct;
        public event Action<int> PlayerGone;
        /// <summary>The host's copy of our kingdom couldn't be read: start a new kingdom instead.</summary>
        public Action SaveUnreadable { get; set; }

        public void Connect(string host, int port, float sendRate)
        {
            address = port == Protocol.DefaultPort ? host : $"{host}:{port}";
            sendEvery = 1f / Mathf.Max(1f, sendRate);
            core = new ClientCore();
            core.Welcomed += w => WorldReady?.Invoke(w);
            core.Failed += why => failure = why;
            core.Disconnected += why => failure = why;
            core.Direct += (from, kind, bytes) => { if (inWorld) Direct?.Invoke(from, kind, bytes); };
            core.PlayerJoined += p =>
            {
                players[p.Id] = p.Name;
                playerKingdoms[p.Id] = p.KingdomId;
                if (inWorld)
                {
                    remotes.Add(p.Id, p.Name);
                    chat.Notice($"{p.Name} joined the game");
                }
            };
            core.PlayerLeft += id =>
            {
                string name = players.TryGetValue(id, out var n) ? n : "Someone";
                players.Remove(id);
                playerKingdoms.Remove(id);
                if (!inWorld) return;
                remotes.Remove(id);
                chat.Notice($"{name} left");
                PlayerGone?.Invoke(id);
            };
            core.KingdomPlaced += k =>
            {
                known.RemoveAll(o => o.Id == k.Id);
                known.Add(k);
                if (!inWorld) return;
                KingdomGround.Clear(nature, k);
                remotes.AddKingdom(k);
                kingdoms.Place(k);
            };
            // The others' kingdoms and villagers.
            core.Built += (k, replaceAll, removed, added) =>
            {
                if (inWorld && k != myKingdom) kingdoms.Apply(k, replaceAll, removed, added);
            };
            core.Folk += (k, time, folk) =>
            {
                clock.Heard(time, Time.realtimeSinceStartupAsDouble);
                if (inWorld && k != myKingdom) kingdoms.HeardFolk(k, time, folk);
            };
            // Our kingdom as we left it on this server: put back, then the others are sent all of it.
            // (It comes every time, empty the first time: then there's nothing to wait for.)
            core.YourSave += bytes =>
            {
                if (!inWorld || kingdomBack) return;
                kingdomBack = true;
                var data = bytes.Length > 0 ? World.SaveSystem.Unpack(bytes) : null;
                if (data != null) saves.Restore(data);
                else
                {
                    // Never saved over on its own: the host keeps the old one unless you save on purpose (Esc).
                    SaveUnreadable?.Invoke();
                    saves.KeepOldSave("Your kingdom from last time couldn't be read, so you have a new one");
                }
                saves.Waiting = null;
                mirror.enabled = true;
                mirror.SendAllAgain();
            };
            core.Snapshot += (time, list) =>
            {
                clock.Heard(time, Time.realtimeSinceStartupAsDouble);
                if (!inWorld) return;
                foreach (var e in list) remotes.Heard(e.Id, time, e.Motion);
            };
            core.Corrected += m => { if (ruler != null) ruler.ForceMove(new Vector2(m.X, m.Y)); };
            core.Chat += (from, text) => { if (inWorld) chat.Add(from, text); };
            core.Clock += (day, hour) => SyncClock(day, hour);
            core.Connect(host, port, GameSession.PlayerKey, GameSession.PlayerName);
        }

        /// <summary>
        /// The world is made: start showing the others and sending where the ruler is. <paramref name="saveSystem"/>
        /// saves our kingdom on the host; <paramref name="kingdomMirror"/> (switched on once our kingdom from last time
        /// is back) sends it to the others; <paramref name="otherKingdoms"/> shows theirs.
        /// </summary>
        public void Enter(WelcomeInfo welcome, RulerController rulerController, RemotePlayers remotePlayers, ChatBox chatBox, DayNightCycle clockOfDay,
            NatureMap natureMap, RemoteKingdoms otherKingdoms, KingdomMirror kingdomMirror, World.SaveSystem saveSystem)
        {
            ruler = rulerController;
            remotes = remotePlayers;
            chat = chatBox;
            dayNight = clockOfDay;
            nature = natureMap;
            kingdoms = otherKingdoms;
            mirror = kingdomMirror;
            saves = saveSystem;
            myKingdom = welcome.YourKingdom;
            foreach (var k in welcome.Kingdoms) if (k.Id != myKingdom) kingdoms.Place(k);
            mirror.SendBuilt = (replaceAll, removed, added) => core.SendBuilt(replaceAll, removed, added);
            mirror.SendFolk = folk => core.SendFolk(folk);
            saves.Upload = bytes => core.SendKingdomSave(bytes);
            // Coming back: until our kingdom from last time is here (Msg.YourSave, right after the welcome), the
            // others aren't sent it and it isn't saved (that would put an empty kingdom over the real one).
            kingdomBack = !welcome.HasSave;
            mirror.enabled = kingdomBack;
            if (!kingdomBack) saves.Waiting = "Your kingdom from last time is still on its way";
            known.Clear();
            known.AddRange(welcome.Kingdoms);
            foreach (var p in welcome.Players)
            {
                players[p.Id] = p.Name;
                playerKingdoms[p.Id] = p.KingdomId;
                if (p.Id != welcome.YourId) remotes.Add(p.Id, p.Name);
            }
            chat.Send = text => core.SendChat(text);
            if (dayNight != null) dayNight.SetTime(welcome.Day, welcome.Hour);
            inWorld = true;
            chat.Notice($"Joined {welcome.ServerName}. Enter: chat");
        }

        void SyncClock(int day, float hour)
        {
            if (dayNight == null) return;
            float mine = dayNight.Day * 24f + dayNight.Hour, theirs = day * 24f + hour;
            if (Mathf.Abs(mine - theirs) > 0.1f) dayNight.SetTime(day, hour);
        }

        void Update()
        {
            if (core == null) return;
            core.Poll();
            if (!inWorld) return;
            remotes.RenderTime = clock.Known ? clock.Now(Time.realtimeSinceStartupAsDouble) : 0;
            kingdoms.RenderTime = remotes.RenderTime;
            if (ruler != null && Time.unscaledTime >= sendAt)
            {
                sendAt = Time.unscaledTime + sendEvery;
                core.SendState(RulerMotion.Of(ruler, ref lastSent));
            }
        }

        void OnDestroy() => core?.Disconnect();
        void OnApplicationQuit() => core?.Disconnect();

        /// <summary>
        /// Leaves the game (saving our kingdom on the host first, if we're in) and goes back to the main menu,
        /// or with <paramref name="quit"/> closes the game.
        /// </summary>
        public void Leave(bool quit = false)
        {
            if (leaving) return;
            leaving = true;
            quitting = quit;
            if (core != null && core.Connected && inWorld && saves != null && saves.SafeToOverwrite())
            {
                saves.Save(true);
                StartCoroutine(LeaveSoon());   // a moment for the save to get there
                return;
            }
            LeaveNow();
        }

        System.Collections.IEnumerator LeaveSoon()
        {
            float until = Time.unscaledTime + 1f;
            while (Time.unscaledTime < until)
            {
                core?.Poll();
                yield return null;
            }
            LeaveNow();
        }

        void LeaveNow()
        {
            core?.Disconnect();
            core = null;
            GameSession.Role = NetRole.Offline;
            Time.timeScale = 1f;
            if (quitting) PauseMenu.Quit();
            else if (Application.CanStreamedLevelBeLoaded(GameSession.MenuScene)) SceneManager.LoadScene(GameSession.MenuScene);
            else Application.Quit();
        }

        public string Status => core != null && core.Welcome != null
            ? $"Playing in {core.Welcome.ServerName} ({address}), {players.Count} player{(players.Count == 1 ? "" : "s")}, ping {core.Ping} ms"
            : null;

        void OnGUI()
        {
            int font = Mathf.Max(13, Screen.height / 46);
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, richText = true };
                button = new GUIStyle(GUI.skin.button) { richText = true };
            }
            big.fontSize = button.fontSize = font;
            style.fontSize = Mathf.Max(11, Screen.height / 60);

            if (failure != null || !inWorld)
            {
                // Connecting, or it went wrong: a screen of its own, with the way back.
                var old = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, inWorld ? 0.7f : 1f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = old;
                string text = failure != null ? $"<b><color=#ff8a80>{failure}</color></b>" : $"Joining <b>{address}</b>...";
                GUI.Label(new Rect(Screen.width * 0.15f, Screen.height * 0.3f, Screen.width * 0.7f, font * 6f), text, big);
                float w = font * 14f;
                if (GUI.Button(new Rect((Screen.width - w) / 2f, Screen.height * 0.3f + font * 7f, w, font * 2.4f), failure != null ? "Main menu" : "Cancel", button)) Leave();
                return;
            }
            GUI.Label(new Rect(0, 4, Screen.width, 30), $"<color=#c8b890>Online: {players.Count} player{(players.Count == 1 ? "" : "s")}   ping {core.Ping} ms   (Enter: chat)</color>", style);
        }
    }
}
