using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;

namespace KingdomGame.Networking
{
    /// <summary>
    /// A player's side of the network: connects to a server (saying who it is), then hears
    /// about the world, the other players, where they are, chat and the time of day, and
    /// sends where its own ruler is. The game drives it by calling <see cref="Poll"/> every frame.
    /// </summary>
    public sealed class ClientCore : INetEventListener
    {
        readonly NetManager net;
        readonly NetDataWriter writer = new NetDataWriter();
        readonly List<(int Id, Motion Motion)> entries = new List<(int, Motion)>();
        NetPeer server;
        bool leaving;

        /// <summary>What the server said when we joined (null until then).</summary>
        public WelcomeInfo Welcome { get; private set; }
        public bool Connected => Welcome != null && server != null && server.ConnectionState == ConnectionState.Connected;
        /// <summary>Milliseconds a message takes to the server.</summary>
        public int Ping => server != null ? server.Ping : 0;

        /// <summary>We're in: here's the world.</summary>
        public event Action<WelcomeInfo> Welcomed;
        /// <summary>Couldn't join, and why (in words for the player).</summary>
        public event Action<string> Failed;
        /// <summary>Lost the server after joining, and why.</summary>
        public event Action<string> Disconnected;
        public event Action<PlayerInfo> PlayerJoined;
        public event Action<int> PlayerLeft;
        public event Action<KingdomInfo> KingdomPlaced;
        /// <summary>Where the others near us are, as of the server's time (seconds).</summary>
        public event Action<double, List<(int Id, Motion Motion)>> Snapshot;
        /// <summary>The server put our ruler back here (a move it didn't believe).</summary>
        public event Action<Motion> Corrected;
        public event Action<string, string> Chat;
        public event Action<int, float> Clock;

        public ClientCore()
        {
            net = new NetManager(this)
            {
                AutoRecycle = false,
                DisconnectTimeout = 10000,
                UpdateTime = 15,
                IPv6Enabled = false,
            };
        }

        /// <summary>Starts joining. <paramref name="playerKey"/>: who we are from game to game.</summary>
        public void Connect(string host, int port, string playerKey, string name)
        {
            leaving = false;
            Welcome = null;
            if (!net.IsRunning) net.Start();
            try
            {
                server = net.Connect(string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim(), port, Protocol.Hello(playerKey, name));
            }
            catch (Exception)
            {
                server = null;
            }
            if (server == null) Failed?.Invoke($"Couldn't find \"{host}\". Check the address.");
        }

        /// <summary>Leaves the server.</summary>
        public void Disconnect()
        {
            leaving = true;
            net.Stop(true);
            server = null;
            Welcome = null;
        }

        public void Poll() => net.PollEvents();

        /// <summary>Where our ruler is (sent often, so it may be lost now and then: only the latest matters).</summary>
        public void SendState(Motion motion)
        {
            if (!Connected) return;
            writer.Reset();
            writer.Put((byte)Msg.State);
            motion.Write(writer);
            server.Send(writer, DeliveryMethod.Sequenced);
        }

        public void SendChat(string text)
        {
            text = Protocol.CleanChat(text);
            if (!Connected || text.Length == 0) return;
            writer.Reset();
            writer.Put((byte)Msg.Chat);
            writer.Put(text, Protocol.MaxChatLength);
            server.Send(writer, DeliveryMethod.ReliableOrdered);
        }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod method)
        {
            try
            {
                if (reader.AvailableBytes < 1) return;
                var kind = (Msg)reader.GetByte();
                if (Welcome == null && kind != Msg.Welcome) return;
                switch (kind)
                {
                    case Msg.Welcome:
                        Welcome = WelcomeInfo.Read(reader);
                        Welcomed?.Invoke(Welcome);
                        break;
                    case Msg.PlayerJoined:
                        PlayerJoined?.Invoke(PlayerInfo.Read(reader));
                        break;
                    case Msg.PlayerLeft:
                        PlayerLeft?.Invoke(reader.GetInt());
                        break;
                    case Msg.KingdomPlaced:
                        KingdomPlaced?.Invoke(KingdomInfo.Read(reader));
                        break;
                    case Msg.Snapshot:
                    {
                        double time = reader.GetDouble();
                        int n = reader.GetByte();
                        entries.Clear();
                        for (int i = 0; i < n; i++) entries.Add((reader.GetInt(), Motion.Read(reader)));
                        Snapshot?.Invoke(time, entries);
                        break;
                    }
                    case Msg.Correction:
                        Corrected?.Invoke(Motion.Read(reader));
                        break;
                    case Msg.Chat:
                    {
                        string from = reader.GetString(Protocol.MaxNameLength);
                        Chat?.Invoke(from, Protocol.CleanChat(reader.GetString(Protocol.MaxChatLength)));
                        break;
                    }
                    case Msg.Clock:
                        Clock?.Invoke(reader.GetInt(), reader.GetFloat());
                        break;
                }
            }
            finally
            {
                reader.Recycle();
            }
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            if (peer != server) return;
            bool joined = Welcome != null;
            server = null;
            Welcome = null;
            if (leaving) return;
            string why = Reason(info);
            if (joined) Disconnected?.Invoke(why);
            else Failed?.Invoke(why);
        }

        static string Reason(DisconnectInfo info)
        {
            if (info.Reason == DisconnectReason.ConnectionRejected && info.AdditionalData != null)
            {
                try
                {
                    string said = info.AdditionalData.GetString(200);
                    if (!string.IsNullOrEmpty(said)) return said;
                }
                catch (Exception) { }
            }
            switch (info.Reason)
            {
                case DisconnectReason.ConnectionFailed:
                    return "Nobody answered at that address. Is the server running, and is its port open (see docs/ONLINE-TEST.md)?";
                case DisconnectReason.Timeout:
                    return "Lost the connection to the server (it stopped answering).";
                case DisconnectReason.RemoteConnectionClose:
                    return "The server closed the connection.";
                case DisconnectReason.ConnectionRejected:
                    return "The server turned you away.";
                case DisconnectReason.HostUnreachable:
                case DisconnectReason.NetworkUnreachable:
                    return "Can't reach that address from here (check your internet connection).";
                case DisconnectReason.UnknownHost:
                    return "Couldn't find that address.";
                default:
                    return $"Disconnected ({info.Reason}).";
            }
        }

        void INetEventListener.OnPeerConnected(NetPeer peer) { }
        void INetEventListener.OnConnectionRequest(ConnectionRequest request) => request.Reject();
        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }
        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) => reader.Recycle();
        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
    }

    /// <summary>
    /// Smooth movement for others' characters: keeps the last positions heard (with the
    /// server's time) and gives where one was at any moment in between. Shown a little in
    /// the past (<see cref="Delay"/>), so there's always a newer position to move towards.
    /// </summary>
    public sealed class MotionBuffer
    {
        /// <summary>How far in the past others are shown, in seconds.</summary>
        public const double Delay = 0.12;
        const int Size = 32;
        readonly double[] times = new double[Size];
        readonly Motion[] motions = new Motion[Size];
        int count, newest = -1;

        public bool Empty => count == 0;
        public double NewestTime => count > 0 ? times[newest] : 0;

        public void Add(double time, Motion motion)
        {
            if (count > 0 && time <= times[newest]) return;   // old or repeated
            newest = (newest + 1) % Size;
            times[newest] = time;
            motions[newest] = motion;
            if (count < Size) count++;
        }

        /// <summary>Where it was at <paramref name="time"/> (server seconds). False if nothing was heard yet.</summary>
        public bool Sample(double time, out Motion motion)
        {
            motion = default;
            if (count == 0) return false;
            if (time >= times[newest])
            {
                motion = motions[newest];
                return true;
            }
            for (int k = 0; k < count - 1; k++)
            {
                int b = (newest - k + Size) % Size, a = (b - 1 + Size) % Size;
                if (time < times[a]) continue;
                var ma = motions[a];
                var mb = motions[b];
                // A jump (through a door, a respawn): no gliding across the map.
                float dx = mb.X - ma.X, dy = mb.Y - ma.Y;
                if ((mb.Flags & MotionFlags.Jumped) != 0 || dx * dx + dy * dy > 16f)
                {
                    motion = time - times[a] < times[b] - time ? ma : mb;
                    return true;
                }
                float t = (float)((time - times[a]) / Math.Max(1e-6, times[b] - times[a]));
                motion = new Motion
                {
                    X = ma.X + dx * t,
                    Y = ma.Y + dy * t,
                    Aim = LerpAngle(ma.Aim, mb.Aim, t),
                    Flags = t < 0.5f ? ma.Flags : mb.Flags,
                };
                return true;
            }
            motion = motions[(newest - (count - 1) + Size) % Size];   // older than anything kept
            return true;
        }

        static float LerpAngle(float a, float b, float t)
        {
            float d = ((b - a) % 360f + 540f) % 360f - 180f;
            return a + d * t;
        }
    }

    /// <summary>
    /// Works out the server's clock from when its messages arrive, so others can be shown
    /// at the right moment. It follows the earliest-arriving messages (the least delayed).
    /// </summary>
    public sealed class ServerClock
    {
        double offset;
        bool known;

        /// <summary>A message stamped <paramref name="serverTime"/> arrived at <paramref name="localTime"/>.</summary>
        public void Heard(double serverTime, double localTime)
        {
            double o = serverTime - localTime;
            if (!known) { offset = o; known = true; return; }
            // A quicker message: trust it at once. A slower one: drift towards it slowly (the clocks may run apart a little).
            offset = o > offset ? o : offset + (o - offset) * 0.02;
        }

        public bool Known => known;
        public double Now(double localTime) => localTime + offset;
    }
}
