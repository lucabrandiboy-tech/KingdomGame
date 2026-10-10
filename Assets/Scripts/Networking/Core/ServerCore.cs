using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;

namespace KingdomGame.Networking
{
    /// <summary>A server's settings (the game fills them from its Network Config and the command line).</summary>
    public sealed class ServerSettings
    {
        public int Port = Protocol.DefaultPort;
        public int MaxPlayers = 16;
        public string Name = "Kingdom server";
        public int Seed = 1;
        public int MapSize = 1024;
        /// <summary>Tiles between kingdoms.</summary>
        public int KingdomSpacing = 96;
        /// <summary>Players are told about others within this many tiles.</summary>
        public float InterestRadius = 64f;
        /// <summary>How many times a second players are told where the others are.</summary>
        public float SnapshotRate = 15f;
        /// <summary>The fastest a ruler can believably move, in tiles a second (moves faster than this are put back).</summary>
        public float MaxSpeed = 6f;
        /// <summary>Seconds between telling everyone the time of day.</summary>
        public float ClockSeconds = 10f;
        /// <summary>Seconds a player must wait between chat messages.</summary>
        public float ChatSeconds = 0.5f;
    }

    /// <summary>One player on the server.</summary>
    public sealed class PlayerSession
    {
        public int Id;
        /// <summary>Who they are from game to game (a code their game keeps); their kingdom is tied to it.</summary>
        public string Key;
        public string Name;
        public int KingdomId;
        public Motion Motion;
        public bool HasMotion;
        internal double LastStateAt, LastChatAt;
        internal NetPeer Peer;
        public int Ping => Peer != null ? Peer.Ping : 0;
        public string Address => Peer != null ? Peer.ToString() : "";
    }

    /// <summary>
    /// The server's side of the network: lets players in (or says why not), gives each a
    /// kingdom spot the first time they come, tells everyone who's here, passes on where
    /// everyone is (only to those near them), chat and the time of day. It checks moves are
    /// believable (not faster than <see cref="ServerSettings.MaxSpeed"/>) and puts players
    /// back when they aren't. The game drives it by calling <see cref="Poll"/> every frame.
    /// </summary>
    public sealed class ServerCore : INetEventListener
    {
        /// <summary>Most entries in one snapshot packet (so it fits the smallest packet size).</summary>
        const int SnapshotEntriesPerPacket = 24;
        /// <summary>Most others a player is told about at once (the nearest).</summary>
        const int MaxSnapshotEntries = 96;

        readonly ServerSettings settings;
        readonly NetManager net;
        readonly NetDataWriter writer = new NetDataWriter();
        readonly Dictionary<int, (string Key, string Name)> pending = new Dictionary<int, (string, string)>();
        readonly Dictionary<int, PlayerSession> byPeer = new Dictionary<int, PlayerSession>();
        readonly List<PlayerSession> players = new List<PlayerSession>();
        readonly Dictionary<string, int> kingdomOfKey = new Dictionary<string, int>();
        readonly List<KingdomInfo> kingdoms = new List<KingdomInfo>();
        readonly KingdomPlanner planner;
        readonly List<(PlayerSession P, float D)> near = new List<(PlayerSession, float)>();
        int nextId = 1;
        double now, nextSnapshot, nextClock;

        public ServerSettings Settings => settings;
        public IReadOnlyList<PlayerSession> Players => players;
        public IReadOnlyList<KingdomInfo> Kingdoms => kingdoms;
        public int Port => net.LocalPort;
        public bool Running => net.IsRunning;

        /// <summary>The time of day to tell players (day number, hour 0..24).</summary>
        public Func<(int Day, float Hour)> Clock { get; set; }
        /// <summary>Whether a kingdom's throne can go at a cell (see <see cref="KingdomPlanner.IsGood"/>).</summary>
        public Func<int, int, bool> SpotIsGood { get => planner.IsGood; set => planner.IsGood = value; }

        public event Action<PlayerSession> Joined;
        public event Action<PlayerSession> Left;
        /// <summary>A new kingdom got a spot (the game builds its throne there).</summary>
        public event Action<KingdomInfo> KingdomPlaced;
        public event Action<PlayerSession, string> Said;
        /// <summary>Things worth a line in the server's log.</summary>
        public event Action<string> Log;

        public ServerCore(ServerSettings serverSettings)
        {
            settings = serverSettings ?? new ServerSettings();
            planner = new KingdomPlanner(settings.MapSize, settings.KingdomSpacing, settings.Seed);
            net = new NetManager(this)
            {
                AutoRecycle = false,
                DisconnectTimeout = 10000,
                UpdateTime = 15,
                IPv6Enabled = false,
            };
        }

        /// <summary>Starts listening on the port. False if it couldn't (the port is in use).</summary>
        public bool Start()
        {
            bool ok = net.Start(settings.Port);
            Log?.Invoke(ok ? $"Listening on UDP port {net.LocalPort}" : $"Couldn't open UDP port {settings.Port}: is another server running?");
            return ok;
        }

        public void Stop() => net.Stop(true);

        /// <summary>A kingdom from before (loading the world): its spot is taken and its owner gets it back.</summary>
        public void AddKingdom(KingdomInfo kingdom, string ownerKey)
        {
            kingdoms.Add(kingdom);
            planner.Add(kingdom.X, kingdom.Y);
            if (!string.IsNullOrEmpty(ownerKey)) kingdomOfKey[ownerKey] = kingdom.Id;
        }

        /// <summary>The key of whoever owns a kingdom (for saving), or null.</summary>
        public string OwnerKey(int kingdomId)
        {
            foreach (var pair in kingdomOfKey) if (pair.Value == kingdomId) return pair.Key;
            return null;
        }

        /// <summary>Handles what came in, and sends what's due. <paramref name="time"/>: seconds, always going up.</summary>
        public void Poll(double time)
        {
            now = time;
            net.PollEvents();
            if (now >= nextSnapshot)
            {
                nextSnapshot = now + 1.0 / Math.Max(1f, settings.SnapshotRate);
                foreach (var p in players) SendSnapshot(p);
            }
            if (now >= nextClock && Clock != null)
            {
                nextClock = now + Math.Max(1f, settings.ClockSeconds);
                var (day, hour) = Clock();
                writer.Reset();
                writer.Put((byte)Msg.Clock);
                writer.Put(day);
                writer.Put(hour);
                SendToAll(DeliveryMethod.ReliableOrdered);
            }
        }

        /// <summary>Sends a chat line from the server itself to everyone.</summary>
        public void Announce(string text)
        {
            writer.Reset();
            writer.Put((byte)Msg.Chat);
            writer.Put("Server", Protocol.MaxNameLength);
            writer.Put(Protocol.CleanChat(text), Protocol.MaxChatLength);
            SendToAll(DeliveryMethod.ReliableOrdered);
        }

        // ------------------------------------------------------------ joining --

        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
            string why = Check(request.Data, out string key, out string name);
            if (why != null)
            {
                Log?.Invoke($"Turned away {request.RemoteEndPoint}: {why}");
                var w = new NetDataWriter();
                w.Put(why, 200);
                request.Reject(w);
                return;
            }
            var peer = request.Accept();
            if (peer != null) pending[peer.Id] = (key, name);
        }

        // Why someone can't come in, or null.
        string Check(NetDataReader data, out string key, out string name)
        {
            key = name = null;
            try
            {
                if (data.GetString(32) != Protocol.Key) return "That isn't a Kingdom server";
                int version = data.GetInt();
                if (version != Protocol.Version)
                    return $"The server runs a different version of the game (server {Protocol.Version}, yours {version}). Update so both match.";
                key = data.GetString(Protocol.MaxPlayerKeyLength);
                name = Protocol.CleanName(data.GetString(Protocol.MaxNameLength * 2));
            }
            catch (Exception)
            {
                return "That isn't a Kingdom server";
            }
            if (string.IsNullOrEmpty(key)) return "Your game didn't say who you are";
            if (players.Count + pending.Count >= settings.MaxPlayers) return $"The server is full ({settings.MaxPlayers} players)";
            foreach (var p in players) if (p.Key == key) return "You're already on this server (in another window?)";
            foreach (var p in pending.Values) if (p.Key == key) return "You're already joining this server";
            return null;
        }

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            if (!pending.TryGetValue(peer.Id, out var who)) { peer.Disconnect(); return; }
            pending.Remove(peer.Id);

            // Their kingdom: the one they had, or a new spot.
            if (!kingdomOfKey.TryGetValue(who.Key, out int kingdomId))
            {
                var (x, y) = planner.Next();
                var kingdom = new KingdomInfo { Id = kingdoms.Count + 1, Owner = who.Name, X = x, Y = y };
                kingdoms.Add(kingdom);
                kingdomOfKey[who.Key] = kingdom.Id;
                kingdomId = kingdom.Id;
                Log?.Invoke($"{who.Name}'s kingdom goes at {x}, {y}");
                KingdomPlaced?.Invoke(kingdom);
                writer.Reset();
                writer.Put((byte)Msg.KingdomPlaced);
                kingdom.Write(writer);
                SendToAll(DeliveryMethod.ReliableOrdered);
            }
            else
            {
                Find(kingdomId).Owner = who.Name;   // they may have changed their name
            }

            var session = new PlayerSession { Id = nextId++, Key = who.Key, Name = who.Name, KingdomId = kingdomId, Peer = peer, LastStateAt = now };
            // Everyone else hears about them; they hear about everyone.
            writer.Reset();
            writer.Put((byte)Msg.PlayerJoined);
            new PlayerInfo { Id = session.Id, Name = session.Name, KingdomId = kingdomId }.Write(writer);
            SendToAll(DeliveryMethod.ReliableOrdered);

            players.Add(session);
            byPeer[peer.Id] = session;
            var (day, hour) = Clock != null ? Clock() : (1, 8f);
            var welcome = new WelcomeInfo
            {
                YourId = session.Id, YourKingdom = kingdomId, ServerName = settings.Name, Seed = settings.Seed, MapSize = settings.MapSize,
                Day = day, Hour = hour,
            };
            foreach (var p in players) welcome.Players.Add(new PlayerInfo { Id = p.Id, Name = p.Name, KingdomId = p.KingdomId });
            welcome.Kingdoms.AddRange(kingdoms);
            writer.Reset();
            welcome.Write(writer);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            Log?.Invoke($"{session.Name} joined from {peer} ({players.Count} on)");
            Joined?.Invoke(session);
        }

        KingdomInfo Find(int id)
        {
            foreach (var k in kingdoms) if (k.Id == id) return k;
            return null;
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            pending.Remove(peer.Id);
            if (!byPeer.TryGetValue(peer.Id, out var session)) return;
            byPeer.Remove(peer.Id);
            players.Remove(session);
            session.Peer = null;
            writer.Reset();
            writer.Put((byte)Msg.PlayerLeft);
            writer.Put(session.Id);
            SendToAll(DeliveryMethod.ReliableOrdered);
            Log?.Invoke($"{session.Name} left ({info.Reason}; {players.Count} on)");
            Left?.Invoke(session);
        }

        // ---------------------------------------------------------- messages --

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
        {
            try
            {
                if (!byPeer.TryGetValue(peer.Id, out var session) || reader.AvailableBytes < 1) return;
                switch ((Msg)reader.GetByte())
                {
                    case Msg.State:
                        Moved(session, Motion.Read(reader));
                        break;
                    case Msg.Chat:
                        Chat(session, reader.GetString(Protocol.MaxChatLength * 2));
                        break;
                }
            }
            catch (Exception e)
            {
                // Something that doesn't read right: not our game, or a broken one.
                Log?.Invoke($"Bad message from {peer}: {e.GetType().Name}. Disconnecting them.");
                peer.Disconnect();
            }
            finally
            {
                reader.Recycle();
            }
        }

        void Moved(PlayerSession p, Motion m)
        {
            if (!m.IsFinite) return;
            if (p.HasMotion && (m.Flags & MotionFlags.Jumped) == 0)
            {
                // Faster than anyone can walk: put them back where they were.
                float dx = m.X - p.Motion.X, dy = m.Y - p.Motion.Y;
                double dt = Math.Max(0.05, now - p.LastStateAt);
                double allowed = settings.MaxSpeed * dt * 1.5 + 1.0;
                if (dx * dx + dy * dy > allowed * allowed)
                {
                    writer.Reset();
                    writer.Put((byte)Msg.Correction);
                    p.Motion.Write(writer);
                    p.Peer?.Send(writer, DeliveryMethod.ReliableOrdered);
                    p.LastStateAt = now;
                    return;
                }
            }
            p.Motion = m;
            p.HasMotion = true;
            p.LastStateAt = now;
        }

        void Chat(PlayerSession p, string text)
        {
            text = Protocol.CleanChat(text);
            if (text.Length == 0 || now - p.LastChatAt < settings.ChatSeconds) return;
            p.LastChatAt = now;
            writer.Reset();
            writer.Put((byte)Msg.Chat);
            writer.Put(p.Name, Protocol.MaxNameLength);
            writer.Put(text, Protocol.MaxChatLength);
            SendToAll(DeliveryMethod.ReliableOrdered);
            Log?.Invoke($"{p.Name}: {text}");
            Said?.Invoke(p, text);
        }

        // Where the others near this player are, nearest first, in packets small enough to always fit.
        void SendSnapshot(PlayerSession to)
        {
            if (to.Peer == null || !to.HasMotion) return;
            near.Clear();
            float r2 = settings.InterestRadius * settings.InterestRadius;
            foreach (var p in players)
            {
                if (p == to || !p.HasMotion) continue;
                float dx = p.Motion.X - to.Motion.X, dy = p.Motion.Y - to.Motion.Y;
                float d2 = dx * dx + dy * dy;
                if (d2 <= r2) near.Add((p, d2));
            }
            near.Sort((a, b) => a.D.CompareTo(b.D));
            int count = Math.Min(near.Count, MaxSnapshotEntries);
            int sent = 0;
            do
            {
                int n = Math.Min(SnapshotEntriesPerPacket, count - sent);
                writer.Reset();
                writer.Put((byte)Msg.Snapshot);
                writer.Put(now);
                writer.Put((byte)n);
                for (int i = 0; i < n; i++)
                {
                    var p = near[sent + i].P;
                    writer.Put(p.Id);
                    p.Motion.Write(writer);
                }
                to.Peer.Send(writer, DeliveryMethod.Sequenced);
                sent += n;
            } while (sent < count);
        }

        void SendToAll(DeliveryMethod method)
        {
            foreach (var p in players) p.Peer?.Send(writer, method);
        }

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) =>
            Log?.Invoke($"Network error with {endPoint}: {socketError}");

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) =>
            reader.Recycle();

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    }
}
