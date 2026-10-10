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
    /// the players. Others who join get a kingdom spot, see you, each other and chat.
    /// </summary>
    public sealed class NetHost : MonoBehaviour
    {
        ServerCore core;
        PlayerSession me;
        RulerController ruler;
        RemotePlayers remotes;
        ChatBox chat;
        Vector2 lastSent;
        bool failed;
        GUIStyle style;

        public ServerCore Core => core;

        public void Setup(ServerSettings settings, Kingdom kingdom, RulerController rulerController, RemotePlayers remotePlayers, ChatBox chatBox,
            NatureMap nature, DayNightCycle dayNight)
        {
            ruler = rulerController;
            remotes = remotePlayers;
            chat = chatBox;
            core = new ServerCore(settings);
            core.Log += line => Debug.Log("[Host] " + line);
            core.Clock = () => dayNight != null ? (dayNight.Day, dayNight.Hour) : (1, 8f);
            core.SpotIsGood = (x, y) => KingdomGround.IsGood(nature, x, y);
            core.KingdomPlaced += k =>
            {
                KingdomGround.Clear(nature, k);
                remotes.AddKingdom(k);
            };
            core.Joined += p =>
            {
                remotes.Add(p.Id, p.Name);
                chat.Notice($"{p.Name} joined the game");
            };
            core.Left += p =>
            {
                remotes.Remove(p.Id);
                chat.Notice($"{p.Name} left");
            };
            core.PositionTaken += (p, time) => remotes.Heard(p.Id, time, p.Motion);
            core.Said += (p, text) => chat.Add(p.Name, text);
            failed = !core.Start();

            // You're the first player, with the kingdom that's already here.
            var throne = kingdom != null && kingdom.Throne != null ? kingdom.Throne.Cells.position : Vector2Int.zero;
            var mine = new KingdomInfo { Id = 1, X = throne.x, Y = throne.y };
            KingdomGround.Clear(nature, mine);   // as the others' games clear it
            me = core.AddLocalPlayer(GameSession.PlayerKey, GameSession.PlayerName, mine);
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
    /// answers), hands the world's seed and size to the game to make the same map, then
    /// sends where your ruler is and shows the others. If the connection goes, it says why.
    /// </summary>
    public sealed class NetClient : MonoBehaviour
    {
        ClientCore core;
        readonly ServerClock clock = new ServerClock();
        readonly Dictionary<int, string> players = new Dictionary<int, string>();
        RulerController ruler;
        RemotePlayers remotes;
        ChatBox chat;
        DayNightCycle dayNight;
        NatureMap nature;
        float sendEvery = 1f / 15f, sendAt;
        Vector2 lastSent;
        string address, failure;
        bool inWorld;
        GUIStyle big, style, button;

        /// <summary>The host answered: make the world (seed, size, kingdoms) and then call <see cref="Enter"/>.</summary>
        public Action<WelcomeInfo> WorldReady { get; set; }

        public void Connect(string host, int port, float sendRate)
        {
            address = port == Protocol.DefaultPort ? host : $"{host}:{port}";
            sendEvery = 1f / Mathf.Max(1f, sendRate);
            core = new ClientCore();
            core.Welcomed += w => WorldReady?.Invoke(w);
            core.Failed += why => failure = why;
            core.Disconnected += why => failure = why;
            core.PlayerJoined += p =>
            {
                players[p.Id] = p.Name;
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
                if (!inWorld) return;
                remotes.Remove(id);
                chat.Notice($"{name} left");
            };
            core.KingdomPlaced += k =>
            {
                if (!inWorld) return;
                KingdomGround.Clear(nature, k);
                remotes.AddKingdom(k);
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

        /// <summary>The world is made: start showing the others and sending where the ruler is.</summary>
        public void Enter(WelcomeInfo welcome, RulerController rulerController, RemotePlayers remotePlayers, ChatBox chatBox, DayNightCycle clockOfDay, NatureMap natureMap)
        {
            ruler = rulerController;
            remotes = remotePlayers;
            chat = chatBox;
            dayNight = clockOfDay;
            nature = natureMap;
            foreach (var p in welcome.Players)
            {
                players[p.Id] = p.Name;
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
            if (ruler != null && Time.unscaledTime >= sendAt)
            {
                sendAt = Time.unscaledTime + sendEvery;
                core.SendState(RulerMotion.Of(ruler, ref lastSent));
            }
        }

        void OnDestroy() => core?.Disconnect();
        void OnApplicationQuit() => core?.Disconnect();

        /// <summary>Leaves the game and goes back to the main menu.</summary>
        public void Leave()
        {
            core?.Disconnect();
            core = null;
            GameSession.Role = NetRole.Offline;
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(GameSession.MenuScene)) SceneManager.LoadScene(GameSession.MenuScene);
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
