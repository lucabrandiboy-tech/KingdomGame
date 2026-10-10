using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using KingdomGame.Economy;
using KingdomGame.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomGame.Networking
{
    /// <summary>The ground round a kingdom's throne: cleared of trees the same way on the server and every player's game.</summary>
    public static class KingdomGround
    {
        /// <summary>Tiles round the throne's middle that are cleared.</summary>
        public const int ClearRadius = 8;

        public static void Clear(NatureMap map, KingdomInfo kingdom)
        {
            if (map == null || kingdom == null) return;
            var middle = new Vector2(kingdom.X + 1f, kingdom.Y + 1f);
            for (int y = -ClearRadius - 1; y <= ClearRadius + 1; y++)
                for (int x = -ClearRadius - 1; x <= ClearRadius + 1; x++)
                {
                    var cell = new Vector2Int(kingdom.X + x, kingdom.Y + y);
                    if (((Vector2)cell + new Vector2(0.5f, 0.5f) - middle).sqrMagnitude <= ClearRadius * ClearRadius) map.Clear(cell);
                }
        }

        /// <summary>A good place for a throne: not deep in a forest (at most a quarter of the ground round it is trees or rocks).</summary>
        public static bool IsGood(NatureMap map, int x, int y)
        {
            if (map == null) return true;
            int blocked = 0, all = 0;
            for (int dy = -10; dy <= 10; dy += 2)
                for (int dx = -10; dx <= 10; dx += 2)
                {
                    var cell = new Vector2Int(x + dx, y + dy);
                    if (!map.Inside(cell)) return false;
                    all++;
                    if (map.Blocks(cell)) blocked++;
                }
            return blocked * 4 <= all;
        }
    }

    /// <summary>
    /// The dedicated server in the game: runs <see cref="ServerCore"/> every frame, clears
    /// the ground for each new kingdom, keeps the clock, and (when it has a window) shows
    /// who's on and how others can join. Started by KingdomBootstrap when the game runs
    /// with -server or from the main menu's "Host a server".
    /// </summary>
    public sealed class NetServer : MonoBehaviour
    {
        ServerCore core;
        DayNightCycle dayNight;
        NatureMap nature;
        readonly List<string> log = new List<string>();
        string[] addresses;
        GUIStyle box, title, button;
        bool failed;

        public ServerCore Core => core;

        public void Setup(ServerSettings settings, NatureMap natureMap, DayNightCycle clock)
        {
            nature = natureMap;
            dayNight = clock;
            core = new ServerCore(settings);
            core.Log += Write;
            core.Clock = () => dayNight != null ? (dayNight.Day, dayNight.Hour) : (1, 8f);
            core.SpotIsGood = (x, y) => KingdomGround.IsGood(nature, x, y);
            core.KingdomPlaced += k => KingdomGround.Clear(nature, k);
            failed = !core.Start();
            addresses = LocalAddresses();
            Write($"Server \"{settings.Name}\": map {settings.MapSize} x {settings.MapSize}, seed {settings.Seed}, up to {settings.MaxPlayers} players");
        }

        void Write(string line)
        {
            Debug.Log("[Server] " + line);
            log.Add($"{System.DateTime.Now:HH:mm:ss}  {line}");
            if (log.Count > 14) log.RemoveAt(0);
        }

        void Update() => core?.Poll(Time.realtimeSinceStartupAsDouble);

        void OnDestroy() => core?.Stop();

        void OnApplicationQuit() => core?.Stop();

        // This computer's addresses on the home network (for players on the same network to join).
        public static string[] LocalAddresses()
        {
            var found = new List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var a in nic.GetIPProperties().UnicastAddresses)
                        if (a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address)) found.Add(a.Address.ToString());
                }
            }
            catch (System.Exception) { }
            return found.ToArray();
        }

        void OnGUI()
        {
            if (core == null) return;
            int font = Mathf.Max(13, Screen.height / 48);
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, wordWrap = true };
                title = new GUIStyle(GUI.skin.label) { richText = true };
                button = new GUIStyle(GUI.skin.button) { richText = true };
            }
            box.fontSize = button.fontSize = font;
            title.fontSize = font * 2;

            var s = core.Settings;
            float x = font, y = font, w = Screen.width - font * 2;
            GUI.Label(new Rect(x, y, w, font * 3), $"<b><color=#e8c27a>{s.Name}</color></b>  <size={font}>dedicated server</size>", title);
            y += font * 3;

            var text = new System.Text.StringBuilder();
            if (failed) text.Append($"<color=#ff7060><b>Couldn't open UDP port {s.Port}.</b> Is another server already running? Close it, or start this one with another port (-port 7778).</color>\n\n");
            else text.Append($"Listening on UDP port <b>{core.Port}</b>. Map {s.MapSize} x {s.MapSize}, seed {s.Seed}.\n");
            text.Append("Join from this computer: <b>127.0.0.1</b>\n");
            if (addresses != null && addresses.Length > 0)
                text.Append($"Join from your home network: <b>{string.Join("</b> or <b>", addresses)}</b>\n");
            text.Append("Join over the internet: your public IP address (search \"what is my IP\"), with the port forwarded on your router (docs/ONLINE-TEST.md).\n\n");
            text.Append($"<b>Players: {core.Players.Count} / {s.MaxPlayers}</b>   Kingdoms: {core.Kingdoms.Count}\n");
            foreach (var p in core.Players) text.Append($"   {p.Name}   ping {p.Ping} ms   at {p.Motion.X:0}, {p.Motion.Y:0}\n");
            text.Append("\n<b>Log</b>\n");
            foreach (var line in log) text.Append(line).Append('\n');
            float h = Screen.height - y - font * 4;
            GUI.Box(new Rect(x, y, w, h), text.ToString(), box);
            if (GUI.Button(new Rect(x, Screen.height - font * 3, font * 14, font * 2.2f), "Stop the server", button))
            {
                core.Stop();
                if (Application.CanStreamedLevelBeLoaded(GameSession.MenuScene))
                {
                    GameSession.Role = NetRole.Offline;
                    SceneManager.LoadScene(GameSession.MenuScene);
                }
                else Application.Quit();
            }
        }
    }
}
