using UnityEngine;

namespace KingdomGame
{
    /// <summary>The ways to play.</summary>
    public enum GameMode
    {
        /// <summary>Endless waves of zombies; the game ends when the ruler falls.</summary>
        Survival,
        /// <summary>No zombies: build in peace (the ruler comes back at the throne if they die).</summary>
        FreeBuild,
    }

    /// <summary>Whether this copy of the game plays alone, hosts, runs a server, or plays on someone else's game.</summary>
    public enum NetRole
    {
        Offline,
        /// <summary>Playing your own game, with friends able to join it (this computer is the server).</summary>
        Host,
        /// <summary>Playing in someone else's game (a host's, or a dedicated server's).</summary>
        Client,
        /// <summary>A dedicated server: runs the world for everyone, no player of its own (started with -server).</summary>
        Server,
    }

    /// <summary>
    /// What the main menu chose, carried into the game scene. When the game scene
    /// is played straight from the editor (no menu), the scene's own default is used.
    /// </summary>
    public static class GameSession
    {
        public const string MenuScene = "MainMenu";
        public const string GameScene = "Kingdom";

        /// <summary>The mode picked in the main menu, or null if the game scene was started directly.</summary>
        public static GameMode? Chosen { get; set; }
        /// <summary>Load the saved game of the chosen mode instead of starting a new one (the game scene clears it once read).</summary>
        public static bool LoadSave { get; set; }

        /// <summary>Offline, server or client (set by the main menu or the command line).</summary>
        public static NetRole Role { get; set; }
        /// <summary>Client: the server's address and port.</summary>
        public static string JoinHost { get; set; } = "127.0.0.1";
        public static int JoinPort { get; set; } = Networking.Protocol.DefaultPort;

        // Statics survive between plays when Unity's "Enter Play Mode Options" skip reloading scripts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Chosen = null;
            LoadSave = false;
            Role = CommandLine.Has("-server") ? NetRole.Server : NetRole.Offline;
            JoinHost = "127.0.0.1";
            JoinPort = Networking.Protocol.DefaultPort;
        }

        /// <summary>The name shown to other players (kept between games).</summary>
        public static string PlayerName
        {
            get => PlayerPrefs.GetString("PlayerName", "");
            set => PlayerPrefs.SetString("PlayerName", Networking.Protocol.CleanName(value));
        }

        /// <summary>
        /// Who this player is from game to game, so a server gives them their own kingdom back: a code
        /// made once on this computer, plus their name (two windows on one computer with different
        /// names are two players).
        /// </summary>
        public static string PlayerKey
        {
            get
            {
                string code = PlayerPrefs.GetString("PlayerCode", "");
                if (string.IsNullOrEmpty(code))
                {
                    code = System.Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString("PlayerCode", code);
                    PlayerPrefs.Save();
                }
                return code + ":" + Networking.Protocol.CleanName(PlayerName).ToLowerInvariant();
            }
        }

        /// <summary>Reads "host" or "host:port" into <see cref="JoinHost"/> and <see cref="JoinPort"/>. False if the port isn't a number.</summary>
        public static bool SetAddress(string address)
        {
            address = (address ?? "").Trim();
            int port = Networking.Protocol.DefaultPort;
            int colon = address.LastIndexOf(':');
            // "host:port" (an IPv6 address has more than one colon: left as it is).
            if (colon > 0 && address.IndexOf(':') == colon)
            {
                if (!int.TryParse(address.Substring(colon + 1), out port) || port < 1 || port > 65535) return false;
                address = address.Substring(0, colon);
            }
            JoinHost = address.Length > 0 ? address : "127.0.0.1";
            JoinPort = port;
            return true;
        }
    }

    /// <summary>The game's command line: a dedicated server is started with -server (and -port 7777, -maxplayers 16, ...).</summary>
    public static class CommandLine
    {
        public static bool Has(string flag)
        {
            foreach (var arg in System.Environment.GetCommandLineArgs())
                if (string.Equals(arg, flag, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>The word after <paramref name="flag"/>, or null.</summary>
        public static string Value(string flag)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], flag, System.StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        public static int Int(string flag, int otherwise) => int.TryParse(Value(flag), out int v) ? v : otherwise;
    }
}
