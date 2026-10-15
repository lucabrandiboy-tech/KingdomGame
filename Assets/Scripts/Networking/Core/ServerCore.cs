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
        /// <summary>Tiles from the middle of the world to its edge (the world is endless up to there).</summary>
        public int WorldLimit = 32000;
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
        /// <summary>Players are told where a kingdom's villagers are when they're within this many tiles of its throne.</summary>
        public float FolkRadius = 400f;
    }

    /// <summary>
    /// What the server remembers about someone who has played on it (kept while they're away,
    /// and saved with the world): their kingdom, and where they were when they last left.
    /// </summary>
    public sealed class PlayerRecord
    {
        public string Key;
        public string Name;
        public int KingdomId;
        public bool HasPosition;
        public float X, Y;
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
        /// <summary>The host: playing on the computer the server runs on (no connection of its own).</summary>
        public bool IsLocal;
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
        readonly Dictionary<string, PlayerRecord> records = new Dictionary<string, PlayerRecord>();
        readonly List<KingdomInfo> kingdoms = new List<KingdomInfo>();
        readonly KingdomPlanner planner;
        readonly List<(PlayerSession P, float D)> near = new List<(PlayerSession, float)>();
        // What each kingdom has built (for players joining later), and each player's saved kingdom.
        readonly Dictionary<int, HashSet<BuiltPiece>> built = new Dictionary<int, HashSet<BuiltPiece>>();
        readonly Dictionary<int, byte[]> saves = new Dictionary<int, byte[]>();
        readonly List<BuiltPiece> removedIn = new List<BuiltPiece>(), addedIn = new List<BuiltPiece>();
        readonly List<(int Index, Motion Motion)> folkIn = new List<(int, Motion)>();
        int nextId = 1, nextKingdomId = 1;
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
        /// <summary>A player's new position was taken (server time, for smooth movement on the host's screen).</summary>
        public event Action<PlayerSession, double> PositionTaken;
        /// <summary>Things worth a line in the server's log.</summary>
        public event Action<string> Log;
        /// <summary>
        /// A player who joined built or took away something in their kingdom (kingdom, replace all, what went, what's new),
        /// for the host's game to show. The lists are reused: copy what you keep.
        /// </summary>
        public event Action<int, bool, List<BuiltPiece>, List<BuiltPiece>> KingdomBuilt;
        /// <summary>Where a joined player's villagers are (kingdom, server time, villagers). The list is reused.</summary>
        public event Action<int, double, List<(int Index, Motion Motion)>> FolkMoved;

        /// <summary>The host's game: survival or free build (told to players joining).</summary>
        public bool Survival { get; set; }

        /// <summary>What a kingdom has built, as the server knows it.</summary>
        public IReadOnlyCollection<BuiltPiece> BuiltIn(int kingdom) =>
            built.TryGetValue(kingdom, out var set) ? (IReadOnlyCollection<BuiltPiece>)set : Array.Empty<BuiltPiece>();

        /// <summary>A kingdom's save, as its player last sent it (null if none).</summary>
        public byte[] SaveOf(int kingdom) => saves.TryGetValue(kingdom, out var bytes) ? bytes : null;

        public ServerCore(ServerSettings serverSettings)
        {
            settings = serverSettings ?? new ServerSettings();
            planner = new KingdomPlanner(settings.WorldLimit, settings.KingdomSpacing, settings.Seed);
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

        /// <summary>
        /// A kingdom from before (loading the world): its spot is taken and its owner gets it back.
        /// An <see cref="KingdomInfo.Id"/> of 0 (or one already used) gets a new one.
        /// </summary>
        public void AddKingdom(KingdomInfo kingdom, string ownerKey)
        {
            if (kingdom == null) return;
            if (kingdom.Id <= 0 || Find(kingdom.Id) != null) kingdom.Id = nextKingdomId;
            nextKingdomId = Math.Max(nextKingdomId, kingdom.Id + 1);
            kingdoms.Add(kingdom);
            planner.Add(kingdom.X, kingdom.Y);
            if (string.IsNullOrEmpty(ownerKey)) return;
            var record = Record(ownerKey, kingdom.Owner);
            record.KingdomId = kingdom.Id;
        }

        PlayerRecord Record(string key, string name)
        {
            if (!records.TryGetValue(key, out var record))
            {
                record = new PlayerRecord { Key = key };
                records[key] = record;
            }
            if (!string.IsNullOrEmpty(name)) record.Name = name;
            return record;
        }

        /// <summary>
        /// Puts back what was saved about the players (before anyone joins): their kingdoms, and
        /// where each of them was. Players whose kingdom isn't among <paramref name="savedKingdoms"/> get a new one when they come.
        /// </summary>
        public void Remember(IEnumerable<KingdomInfo> savedKingdoms, IEnumerable<PlayerRecord> savedPlayers)
        {
            var owners = new Dictionary<int, PlayerRecord>();
            if (savedPlayers != null)
                foreach (var p in savedPlayers)
                    if (p != null && !string.IsNullOrEmpty(p.Key) && p.KingdomId > 0) owners[p.KingdomId] = p;
            if (savedKingdoms != null)
                foreach (var k in savedKingdoms)
                {
                    if (k == null) continue;
                    owners.TryGetValue(k.Id, out var owner);
                    int savedId = k.Id;
                    var copy = new KingdomInfo { Id = k.Id, Owner = k.Owner, X = k.X, Y = k.Y };
                    AddKingdom(copy, owner?.Key);
                    if (owner == null) continue;
                    var record = records[owner.Key];
                    record.Name = string.IsNullOrEmpty(owner.Name) ? record.Name : owner.Name;
                    record.HasPosition = owner.HasPosition && IsFinite(owner.X) && IsFinite(owner.Y);
                    record.X = owner.X;
                    record.Y = owner.Y;
                    if (copy.Id != savedId) Log?.Invoke($"Kingdom {savedId} was given number {copy.Id} (its number was taken)");
                }
        }

        static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>
        /// What to save about the players who joined (not the host on this computer, whose kingdom and ruler
        /// the game saves itself): every kingdom of theirs, and each player with where they are now or were last.
        /// </summary>
        public (List<KingdomInfo> Kingdoms, List<PlayerRecord> Players) Remembered()
        {
            var local = new HashSet<int>();
            foreach (var p in players)
                if (p.IsLocal) local.Add(p.KingdomId);
            foreach (var p in players)
                if (!p.IsLocal) Note(p);
            var keptKingdoms = new List<KingdomInfo>();
            foreach (var k in kingdoms)
                if (!local.Contains(k.Id)) keptKingdoms.Add(new KingdomInfo { Id = k.Id, Owner = k.Owner, X = k.X, Y = k.Y });
            var keptPlayers = new List<PlayerRecord>();
            foreach (var r in records.Values)
                if (r.KingdomId > 0 && !local.Contains(r.KingdomId))
                    keptPlayers.Add(new PlayerRecord { Key = r.Key, Name = r.Name, KingdomId = r.KingdomId, HasPosition = r.HasPosition, X = r.X, Y = r.Y });
            return (keptKingdoms, keptPlayers);
        }

        /// <summary>
        /// What to save of the joined players' kingdoms (not the host's, which the game saves itself): what each has built
        /// and each one's saved kingdom.
        /// </summary>
        public List<KingdomMemory> Memories()
        {
            var local = new HashSet<int>();
            foreach (var p in players)
                if (p.IsLocal) local.Add(p.KingdomId);
            var list = new List<KingdomMemory>();
            foreach (var k in kingdoms)
            {
                if (local.Contains(k.Id)) continue;
                var memory = new KingdomMemory { KingdomId = k.Id, Save = SaveOf(k.Id) };
                memory.Pieces.AddRange(BuiltIn(k.Id));
                if (memory.Pieces.Count > 0 || memory.Save != null) list.Add(memory);
            }
            return list;
        }

        /// <summary>Puts back what was saved of the kingdoms (after <see cref="Remember"/>, before anyone joins).</summary>
        public void RememberBuilt(IEnumerable<KingdomMemory> memories)
        {
            if (memories == null) return;
            foreach (var m in memories)
            {
                if (m == null || Find(m.KingdomId) == null) continue;
                var set = Set(m.KingdomId);
                foreach (var piece in m.Pieces)
                    if (set.Count < Protocol.MaxPiecesPerKingdom) set.Add(piece);
                if (m.Save != null && m.Save.Length > 0) saves[m.KingdomId] = m.Save;
            }
        }

        /// <summary>Every kingdom's built pieces (the host's game shows the others' when it starts).</summary>
        public IEnumerable<(int Kingdom, IReadOnlyCollection<BuiltPiece> Pieces)> AllBuilt()
        {
            foreach (var kv in built) yield return (kv.Key, kv.Value);
        }

        HashSet<BuiltPiece> Set(int kingdom)
        {
            if (!built.TryGetValue(kingdom, out var set)) built[kingdom] = set = new HashSet<BuiltPiece>();
            return set;
        }

        // Writes down where a player is (to put them back there next time), unless they're dead or inside somewhere.
        void Note(PlayerSession p)
        {
            if (!p.HasMotion || (p.Motion.Flags & (MotionFlags.Dead | MotionFlags.Inside)) != 0) return;
            var record = Record(p.Key, p.Name);
            record.HasPosition = true;
            record.X = p.Motion.X;
            record.Y = p.Motion.Y;
        }

        /// <summary>
        /// The host, playing on this computer: one of the players, with the kingdom that's already here.
        /// Their position is set with <see cref="MoveLocal"/> and their chat with <see cref="ChatLocal"/>.
        /// </summary>
        public PlayerSession AddLocalPlayer(string key, string name, KingdomInfo kingdom)
        {
            name = Protocol.CleanName(name);
            if (kingdom != null)
            {
                kingdom.Owner = name;
                kingdom.Id = 0;   // (a new number: one from a save may have it)
                AddKingdom(kingdom, key);
            }
            var session = new PlayerSession { Id = nextId++, Key = key, Name = name, KingdomId = kingdom != null ? kingdom.Id : 0, IsLocal = true, LastStateAt = now };
            players.Add(session);
            return session;
        }

        /// <summary>Where the host's ruler is now.</summary>
        public void MoveLocal(PlayerSession host, Motion motion)
        {
            if (host == null || !motion.IsFinite) return;
            host.Motion = motion;
            host.HasMotion = true;
            host.LastStateAt = now;
        }

        /// <summary>The host says something in chat.</summary>
        public void ChatLocal(PlayerSession host, string text)
        {
            if (host != null) Chat(host, text);
        }

        /// <summary>The host built or took away something in their kingdom: everyone else hears.</summary>
        public void LocalBuilt(PlayerSession host, bool replaceAll, IReadOnlyList<BuiltPiece> removed, IReadOnlyList<BuiltPiece> added)
        {
            if (host == null || host.KingdomId <= 0) return;
            Apply(host.KingdomId, replaceAll, removed, added);
            Forward(host, host.KingdomId, replaceAll, removed, added);
        }

        /// <summary>Where the host's villagers are: players near the host's kingdom hear.</summary>
        public void LocalFolk(PlayerSession host, IReadOnlyList<(int Index, Motion Motion)> folk)
        {
            if (host == null || host.KingdomId <= 0) return;
            SendFolk(host, host.KingdomId, folk);
        }

        // Changes what the server knows a kingdom has built.
        void Apply(int kingdom, bool replaceAll, IReadOnlyList<BuiltPiece> removed, IReadOnlyList<BuiltPiece> added)
        {
            var set = Set(kingdom);
            if (replaceAll) set.Clear();
            foreach (var p in removed) set.Remove(p);
            foreach (var p in added)
                if (set.Count < Protocol.MaxPiecesPerKingdom) set.Add(p);
        }

        // Passes a kingdom's changes on to every player but the one they came from.
        void Forward(PlayerSession from, int kingdom, bool replaceAll, IReadOnlyList<BuiltPiece> removed, IReadOnlyList<BuiltPiece> added)
        {
            BuiltChanges.Write(writer, kingdom, replaceAll, removed, added, w =>
            {
                foreach (var p in players)
                    if (p != from) p.Peer?.Send(w, DeliveryMethod.ReliableOrdered);
            });
        }

        // Villagers' positions, to the players near their kingdom (as of now, server time).
        void SendFolk(PlayerSession from, int kingdom, IReadOnlyList<(int Index, Motion Motion)> folk)
        {
            var k = Find(kingdom);
            if (k == null) return;
            float r2 = settings.FolkRadius * settings.FolkRadius;
            for (int start = 0; start < folk.Count; start += Protocol.FolkPerMessage)
            {
                int n = Math.Min(Protocol.FolkPerMessage, folk.Count - start);
                writer.Reset();
                writer.Put((byte)Msg.Folk);
                writer.Put(kingdom);
                writer.Put(now);
                writer.Put((byte)n);
                for (int i = 0; i < n; i++)
                {
                    writer.Put((ushort)folk[start + i].Index);
                    folk[start + i].Motion.Write(writer);
                }
                foreach (var p in players)
                {
                    if (p == from || p.Peer == null || !p.HasMotion) continue;
                    float dx = p.Motion.X - k.X, dy = p.Motion.Y - k.Y;
                    if (dx * dx + dy * dy <= r2) p.Peer.Send(writer, DeliveryMethod.Sequenced);
                }
            }
        }

        /// <summary>The key of whoever owns a kingdom (for saving), or null.</summary>
        public string OwnerKey(int kingdomId)
        {
            foreach (var r in records.Values) if (r.KingdomId == kingdomId) return r.Key;
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
            foreach (var p in players) if (p.Key == key) return p.IsLocal ? "That's the host's name on this computer: pick another name" : "You're already on this server (in another window?)";
            foreach (var p in pending.Values) if (p.Key == key) return "You're already joining this server";
            return null;
        }

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
            if (!pending.TryGetValue(peer.Id, out var who)) { peer.Disconnect(); return; }
            pending.Remove(peer.Id);

            // Their kingdom: the one they had, or a new spot.
            var record = Record(who.Key, who.Name);
            int kingdomId = record.KingdomId;
            var theirs = kingdomId > 0 ? Find(kingdomId) : null;
            if (theirs == null)
            {
                var (x, y) = planner.Next();
                var kingdom = new KingdomInfo { Id = nextKingdomId++, Owner = who.Name, X = x, Y = y };
                kingdoms.Add(kingdom);
                record.KingdomId = kingdomId = kingdom.Id;
                record.HasPosition = false;   // (they start at their new throne)
                Log?.Invoke($"{who.Name}'s kingdom goes at {x}, {y}");
                KingdomPlaced?.Invoke(kingdom);
                writer.Reset();
                writer.Put((byte)Msg.KingdomPlaced);
                kingdom.Write(writer);
                SendToAll(DeliveryMethod.ReliableOrdered);
            }
            else
            {
                theirs.Owner = who.Name;   // they may have changed their name
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
                YourId = session.Id, YourKingdom = kingdomId, ServerName = settings.Name, Seed = settings.Seed,
                Day = day, Hour = hour, Survival = Survival, HasSpawn = record.HasPosition, SpawnX = record.X, SpawnY = record.Y,
            };
            foreach (var p in players) welcome.Players.Add(new PlayerInfo { Id = p.Id, Name = p.Name, KingdomId = p.KingdomId });
            welcome.Kingdoms.AddRange(kingdoms);
            writer.Reset();
            welcome.Write(writer);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            // Their kingdom as they left it here (empty the first time)...
            var save = SaveOf(kingdomId) ?? Array.Empty<byte>();
            writer.Reset();
            writer.Put((byte)Msg.YourSave);
            writer.Put(save.Length);
            writer.Put(save);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
            // ...and what everyone else has built.
            var none = Array.Empty<BuiltPiece>();
            foreach (var kv in built)
            {
                if (kv.Key == kingdomId || kv.Value.Count == 0) continue;
                BuiltChanges.Write(writer, kv.Key, true, none, new List<BuiltPiece>(kv.Value), w => peer.Send(w, DeliveryMethod.ReliableOrdered));
            }
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
            Note(session);
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
                    case Msg.Built:
                    {
                        // (Whatever kingdom it says, it's theirs that changes: nobody builds in someone else's.)
                        var (_, replaceAll) = BuiltChanges.Read(reader, removedIn, addedIn);
                        if (session.KingdomId <= 0) break;
                        Apply(session.KingdomId, replaceAll, removedIn, addedIn);
                        Forward(session, session.KingdomId, replaceAll, removedIn, addedIn);
                        KingdomBuilt?.Invoke(session.KingdomId, replaceAll, removedIn, addedIn);
                        break;
                    }
                    case Msg.Folk:
                    {
                        reader.GetInt();      // (the kingdom: theirs)
                        reader.GetDouble();   // (their time: the server stamps its own)
                        int n = reader.GetByte();
                        folkIn.Clear();
                        for (int i = 0; i < n; i++) folkIn.Add((reader.GetUShort(), Motion.Read(reader)));
                        for (int i = folkIn.Count - 1; i >= 0; i--) if (!folkIn[i].Motion.IsFinite) folkIn.RemoveAt(i);
                        if (session.KingdomId <= 0) break;
                        SendFolk(session, session.KingdomId, folkIn);
                        FolkMoved?.Invoke(session.KingdomId, now, folkIn);
                        break;
                    }
                    case Msg.KingdomSave:
                    {
                        int length = reader.GetInt();
                        if (length < 0 || length > Protocol.MaxSaveBytes || length > reader.AvailableBytes)
                        {
                            Log?.Invoke($"{session.Name}'s saved kingdom is too big or broken ({length} bytes): not kept");
                            break;
                        }
                        var bytes = new byte[length];
                        reader.GetBytes(bytes, length);
                        if (session.KingdomId > 0) saves[session.KingdomId] = bytes;
                        break;
                    }
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
            PositionTaken?.Invoke(p, now);
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
