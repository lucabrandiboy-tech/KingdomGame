using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using KingdomGame.Networking;
using LiteNetLib.Utils;

// Runs a real server and real players on this computer (UDP on 127.0.0.1) and checks
// what they hear from each other. Plain asserts, like tools/tests.
static partial class Tests
{
    static int failed;
    static readonly Stopwatch clock = Stopwatch.StartNew();
    static double Now => clock.Elapsed.TotalSeconds;

    static int Main()
    {
        Run(nameof(PlannerSpacesKingdoms), PlannerSpacesKingdoms);
        Run(nameof(PlannerKeepsToGoodSpots), PlannerKeepsToGoodSpots);
        Run(nameof(PlannerStillPlacesWhenFull), PlannerStillPlacesWhenFull);
        Run(nameof(PlannerGoesOnInAnEndlessWorld), PlannerGoesOnInAnEndlessWorld);
        Run(nameof(NamesAndChatAreCleaned), NamesAndChatAreCleaned);
        Run(nameof(BufferInterpolatesAndSnapsOnJumps), BufferInterpolatesAndSnapsOnJumps);
        Run(nameof(ServerClockFollowsQuickestMessages), ServerClockFollowsQuickestMessages);
        Run(nameof(TwoPlayersJoinAndGetKingdoms), TwoPlayersJoinAndGetKingdoms);
        Run(nameof(PlayersSeeEachOtherMove), PlayersSeeEachOtherMove);
        Run(nameof(FarPlayersAreNotSent), FarPlayersAreNotSent);
        Run(nameof(TooFastMovesArePutBack), TooFastMovesArePutBack);
        Run(nameof(ChatReachesEveryone), ChatReachesEveryone);
        Run(nameof(WrongVersionIsTurnedAway), WrongVersionIsTurnedAway);
        Run(nameof(FullServerTurnsAway), FullServerTurnsAway);
        Run(nameof(SameKeyTwiceIsTurnedAway), SameKeyTwiceIsTurnedAway);
        Run(nameof(RejoiningGetsTheSameKingdom), RejoiningGetsTheSameKingdom);
        Run(nameof(RejoiningComesBackWhereYouLeft), RejoiningComesBackWhereYouLeft);
        Run(nameof(SavedPlayersComeBackOnANewServer), SavedPlayersComeBackOnANewServer);
        Run(nameof(SavedKingdomNumbersDontClash), SavedKingdomNumbersDontClash);
        Run(nameof(NobodyThereFailsWithAReason), NobodyThereFailsWithAReason);
        Run(nameof(ManySnapshotEntriesArriveInPieces), ManySnapshotEntriesArriveInPieces);
        Run(nameof(HostIsSeenAndHeard), HostIsSeenAndHeard);
        Run(nameof(BuiltChangesAreFoundAndSent), BuiltChangesAreFoundAndSent);
        Run(nameof(BuildingsReachEveryoneAndLateJoiners), BuildingsReachEveryoneAndLateJoiners);
        Run(nameof(NobodyBuildsInAnotherKingdom), NobodyBuildsInAnotherKingdom);
        Run(nameof(SavedKingdomComesBackOnRejoin), SavedKingdomComesBackOnRejoin);
        Run(nameof(KingdomsAreKeptByANewServer), KingdomsAreKeptByANewServer);
        Run(nameof(VillagersAreSentOnlyToThoseNear), VillagersAreSentOnlyToThoseNear);
        Run(nameof(HostAndPlayersSeeEachOthersKingdoms), HostAndPlayersSeeEachOthersKingdoms);
        Run(nameof(BigKingdomArrivesInPieces), BigKingdomArrivesInPieces);
        Run(nameof(DirectMessagesReachOnlyTheirTarget), DirectMessagesReachOnlyTheirTarget);
        Run(nameof(TradesAndZombiesReadBackTheSame), TradesAndZombiesReadBackTheSame);
        Run(nameof(OversizedDirectMessagesAreDropped), OversizedDirectMessagesAreDropped);
        Console.WriteLine(failed == 0 ? "All network tests passed." : $"{failed} network test(s) FAILED.");
        return failed == 0 ? 0 : 1;
    }

    static void Run(string name, Action test)
    {
        try
        {
            test();
            Console.WriteLine($"  ok    {name}");
        }
        catch (Exception e)
        {
            failed++;
            Console.WriteLine($"  FAIL  {name}: {e.Message}");
        }
    }

    static void Check(bool condition, string what)
    {
        if (!condition) throw new Exception(what);
    }

    // ------------------------------------------------------------ helpers --

    sealed class Player
    {
        public readonly ClientCore Core = new ClientCore();
        public WelcomeInfo Welcome;
        public string FailedWhy, DisconnectedWhy;
        public readonly List<PlayerInfo> Joined = new List<PlayerInfo>();
        public readonly List<int> Left = new List<int>();
        public readonly List<KingdomInfo> Kingdoms = new List<KingdomInfo>();
        public readonly Dictionary<int, Motion> Seen = new Dictionary<int, Motion>();
        public readonly List<(string From, string Text)> Chat = new List<(string, string)>();
        public Motion? Corrected;
        public int SnapshotPackets;
        /// <summary>What each kingdom has built, as this player has heard it.</summary>
        public readonly Dictionary<int, HashSet<BuiltPiece>> Built = new Dictionary<int, HashSet<BuiltPiece>>();
        public int BuiltMessages;
        /// <summary>Where each kingdom's villagers are, as heard.</summary>
        public readonly Dictionary<int, Dictionary<int, Motion>> Folk = new Dictionary<int, Dictionary<int, Motion>>();
        public byte[] YourSave;
        /// <summary>Messages other players sent this one (from, kind, message).</summary>
        public readonly List<(int From, DirectKind Kind, byte[] Bytes)> Direct = new List<(int, DirectKind, byte[])>();

        public Player(int port, string key, string name)
        {
            Core.Direct += (from, kind, bytes) => Direct.Add((from, kind, bytes));
            Core.Welcomed += w => Welcome = w;
            Core.Failed += why => FailedWhy = why;
            Core.Disconnected += why => DisconnectedWhy = why;
            Core.PlayerJoined += p => Joined.Add(p);
            Core.PlayerLeft += id => Left.Add(id);
            Core.KingdomPlaced += k => Kingdoms.Add(k);
            Core.Snapshot += (t, list) => { SnapshotPackets++; foreach (var e in list) Seen[e.Id] = e.Motion; };
            Core.Chat += (from, text) => Chat.Add((from, text));
            Core.Corrected += m => Corrected = m;
            Core.Built += (k, all, removed, added) =>
            {
                BuiltMessages++;
                if (!Built.TryGetValue(k, out var set)) Built[k] = set = new HashSet<BuiltPiece>();
                if (all) set.Clear();
                foreach (var piece in removed) set.Remove(piece);
                foreach (var piece in added) set.Add(piece);
            };
            Core.Folk += (k, t, list) =>
            {
                if (!Folk.TryGetValue(k, out var d)) Folk[k] = d = new Dictionary<int, Motion>();
                foreach (var e in list) d[e.Index] = e.Motion;
            };
            Core.YourSave += bytes => YourSave = bytes;
            if (port > 0) Core.Connect("127.0.0.1", port, key, name);
        }
    }

    static ServerCore NewServer(int maxPlayers = 8, float interest = 64f)
    {
        var server = new ServerCore(new ServerSettings
        {
            Port = 0, MaxPlayers = maxPlayers, Name = "Test server", Seed = 4242,
            KingdomSpacing = 96, InterestRadius = interest, SnapshotRate = 30f, MaxSpeed = 6f,
        });
        server.Clock = () => (3, 21.5f);
        Check(server.Start(), "the server didn't start");
        return server;
    }

    static void Pump(ServerCore server, IEnumerable<Player> players, Func<bool> until, double seconds = 5)
    {
        double end = Now + seconds;
        while (Now < end)
        {
            server?.Poll(Now);
            foreach (var p in players) p.Core.Poll();
            if (until()) return;
            Thread.Sleep(2);
        }
        throw new Exception("timed out waiting");
    }

    static void Stop(ServerCore server, params Player[] players)
    {
        foreach (var p in players) p.Core.Disconnect();
        server?.Stop();
    }

    // ------------------------------------------------------------ planner --

    static void PlannerSpacesKingdoms()
    {
        var planner = new KingdomPlanner(32000, 96, 7);
        var first = planner.Next();
        Check(Math.Abs(first.X) <= 2 && Math.Abs(first.Y) <= 2, $"first kingdom at {first}, not the middle");
        for (int i = 0; i < 40; i++) planner.Next();
        var spots = planner.Spots;
        for (int a = 0; a < spots.Count; a++)
        {
            Check(Math.Abs(spots[a].X) <= 32000 - 96 && Math.Abs(spots[a].Y) <= 32000 - 96, $"spot {spots[a]} is at the world's edge");
            for (int b = a + 1; b < spots.Count; b++)
            {
                double dx = spots[a].X - spots[b].X, dy = spots[a].Y - spots[b].Y;
                Check(Math.Sqrt(dx * dx + dy * dy) >= 96, $"kingdoms {spots[a]} and {spots[b]} are too close");
            }
        }
        // Neighbours, not across the map: the second is about one spacing from the first.
        double d1 = Math.Sqrt(Math.Pow(spots[1].X - spots[0].X, 2) + Math.Pow(spots[1].Y - spots[0].Y, 2));
        Check(d1 < 96 * 1.3, $"the second kingdom is {d1:0} tiles from the first");
    }

    static void PlannerKeepsToGoodSpots()
    {
        var planner = new KingdomPlanner(32000, 64, 3) { IsGood = (x, y) => x > 0 };   // only the east half is open ground
        for (int i = 0; i < 10; i++)
        {
            var s = planner.Next();
            Check(s.X > 0, $"spot {s} isn't good");
        }
    }

    static void PlannerStillPlacesWhenFull()
    {
        var planner = new KingdomPlanner(128, 96, 1);
        for (int i = 0; i < 12; i++) planner.Next();
        Check(planner.Spots.Count == 12, "not everyone got a spot");
    }

    // Hundreds of kingdoms in an endless world: all far enough apart, none past the edge, and
    // packed round the first (not scattered over the world), quickly.
    static void PlannerGoesOnInAnEndlessWorld()
    {
        var planner = new KingdomPlanner(32000, 560, 11);
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 200; i++) planner.Next();
        var spots = planner.Spots;
        double furthest = 0;
        for (int a = 0; a < spots.Count; a++)
        {
            Check(Math.Abs(spots[a].X) < 32000 && Math.Abs(spots[a].Y) < 32000, $"spot {spots[a]} is past the world's edge");
            furthest = Math.Max(furthest, Math.Sqrt((double)spots[a].X * spots[a].X + (double)spots[a].Y * spots[a].Y));
            for (int b = a + 1; b < spots.Count; b++)
            {
                double dx = spots[a].X - spots[b].X, dy = spots[a].Y - spots[b].Y;
                Check(Math.Sqrt(dx * dx + dy * dy) >= 560, $"kingdoms {spots[a]} and {spots[b]} are too close");
            }
        }
        // 200 kingdoms 560 apart fit in a circle about 560 * sqrt(200) / 1.9 across... give it plenty of room.
        Check(furthest < 560 * Math.Sqrt(200) * 1.5, $"the kingdoms are spread {furthest:0} tiles out: not neighbours");
        Check(watch.Elapsed.TotalSeconds < 5, $"placing 200 kingdoms took {watch.Elapsed.TotalSeconds:0.0} s");
    }

    static void NamesAndChatAreCleaned()
    {
        Check(Protocol.CleanName("<color=red>Bob</color>") == "colorredBobcolor", $"name: {Protocol.CleanName("<color=red>Bob</color>")}");
        Check(Protocol.CleanName("   ") == "Ruler", "an empty name gets a default");
        Check(Protocol.CleanName(new string('a', 50)).Length == Protocol.MaxNameLength, "long names are cut");
        Check(Protocol.CleanChat("hi <b>there</b>\n") == "hi bthere/b", $"chat: {Protocol.CleanChat("hi <b>there</b>\n")}");
    }

    static void BufferInterpolatesAndSnapsOnJumps()
    {
        var b = new MotionBuffer();
        Check(!b.Sample(1, out _), "an empty buffer has nothing");
        b.Add(1.0, new Motion { X = 0, Y = 0, Aim = 350 });
        b.Add(2.0, new Motion { X = 1, Y = 2, Aim = 10 });
        b.Sample(1.5, out var m);
        Check(Math.Abs(m.X - 0.5f) < 1e-4 && Math.Abs(m.Y - 1) < 1e-4, $"halfway is {m.X},{m.Y}");
        float aim = ((m.Aim % 360f) + 360f) % 360f;
        Check(aim < 0.01f || aim > 359.99f, $"aim goes the short way round, got {m.Aim}");
        b.Sample(5, out m);
        Check(m.X == 1, "after the newest it holds the newest");
        b.Add(3.0, new Motion { X = 2000, Y = 4000, Flags = MotionFlags.Jumped });
        b.Sample(2.9, out m);
        Check(m.X == 2000, "a jump isn't glided across");
        b.Add(2.5, new Motion { X = -5 });
        b.Sample(2.6, out m);
        Check(m.X != -5, "older messages arriving late are ignored");
    }

    static void ServerClockFollowsQuickestMessages()
    {
        var c = new ServerClock();
        c.Heard(100.0, 10.0);   // 50 ms late
        c.Heard(100.5, 10.48);  // 30 ms late: quicker, trusted
        Check(Math.Abs(c.Now(11) - 101.02) < 1e-6, $"clock {c.Now(11)}");
        c.Heard(101, 11.5);     // a slow one barely moves it
        Check(Math.Abs(c.Now(11) - 101.02) < 0.02, $"clock {c.Now(11)}");
    }

    // ------------------------------------------------------------ network --

    static void TwoPlayersJoinAndGetKingdoms()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        Pump(server, new[] { a }, () => a.Welcome != null);
        var b = new Player(server.Port, "key-b", "Bob");
        Pump(server, new[] { a, b }, () => b.Welcome != null && a.Joined.Count > 0 && a.Kingdoms.Count > 0);
        try
        {
            Check(a.Welcome.YourId != b.Welcome.YourId, "players got the same id");
            Check(a.Welcome.Seed == 4242 && b.Welcome.Seed == 4242, "the world wasn't sent");
            Check(a.Welcome.Day == 3 && Math.Abs(b.Welcome.Hour - 21.5f) < 1e-4, "the clock wasn't sent");
            Check(a.Welcome.ServerName == "Test server", "server name");
            Check(a.Welcome.YourKingdom != b.Welcome.YourKingdom, "players share a kingdom");
            Check(b.Welcome.Kingdoms.Count == 2 && b.Welcome.Players.Count == 2, "the second player wasn't told about everyone");
            Check(a.Joined[0].Name == "Bob" && a.Joined[0].Id == b.Welcome.YourId, "the first player wasn't told Bob came");
            var ka = b.Welcome.Kingdoms.Find(k => k.Id == a.Welcome.YourKingdom);
            var kb = a.Kingdoms[0];
            Check(ka != null && ka.Owner == "Alice" && kb.Owner == "Bob", "kingdom owners");
            double d = Math.Sqrt(Math.Pow(ka.X - kb.X, 2) + Math.Pow(ka.Y - kb.Y, 2));
            Check(d >= 96, $"kingdoms only {d:0} tiles apart");
            Check(server.Players.Count == 2, "the server lists both");
        }
        finally { Stop(server, a, b); }
    }

    static void PlayersSeeEachOtherMove()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        var b = new Player(server.Port, "key-b", "Bob");
        var both = new[] { a, b };
        Pump(server, both, () => a.Welcome != null && b.Welcome != null);
        try
        {
            b.Core.SendState(new Motion { X = 10, Y = 10 });
            double start = Now;
            // Alice walks east at 3 tiles a second.
            Pump(server, both, () =>
            {
                float x = (float)((Now - start) * 3.0);
                a.Core.SendState(new Motion { X = x, Y = 5, Aim = 0, Flags = MotionFlags.Walking });
                b.Core.SendState(new Motion { X = 10, Y = 10 });
                return Now - start > 1.0 && b.Seen.TryGetValue(a.Welcome.YourId, out var m) && m.X > 2f;
            });
            var seen = b.Seen[a.Welcome.YourId];
            Check(Math.Abs(seen.Y - 5) < 1e-4 && (seen.Flags & MotionFlags.Walking) != 0, "Bob sees Alice as she is");
            Check(a.Seen.ContainsKey(b.Welcome.YourId), "Alice sees Bob");
            Check(a.Corrected == null, "walking speed was put back");
        }
        finally { Stop(server, a, b); }
    }

    static void FarPlayersAreNotSent()
    {
        var server = NewServer(interest: 50f);
        var a = new Player(server.Port, "key-a", "Alice");
        var b = new Player(server.Port, "key-b", "Bob");
        var c = new Player(server.Port, "key-c", "Carol");
        var all = new[] { a, b, c };
        Pump(server, all, () => a.Welcome != null && b.Welcome != null && c.Welcome != null);
        try
        {
            double start = Now;
            Pump(server, all, () =>
            {
                a.Core.SendState(new Motion { X = 0, Y = 0 });
                b.Core.SendState(new Motion { X = 30, Y = 0 });
                c.Core.SendState(new Motion { X = 300, Y = 0 });
                return Now - start > 0.6;
            });
            Check(a.Seen.ContainsKey(b.Welcome.YourId), "Alice sees Bob (30 tiles)");
            Check(!a.Seen.ContainsKey(c.Welcome.YourId), "Alice was sent Carol (300 tiles away)");
            Check(!c.Seen.ContainsKey(a.Welcome.YourId), "Carol was sent Alice");
        }
        finally { Stop(server, a, b, c); }
    }

    static void TooFastMovesArePutBack()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        var b = new Player(server.Port, "key-b", "Bob");
        var both = new[] { a, b };
        Pump(server, both, () => a.Welcome != null && b.Welcome != null);
        try
        {
            a.Core.SendState(new Motion { X = 0, Y = 0 });
            b.Core.SendState(new Motion { X = 5, Y = 5 });
            double start = Now;
            Pump(server, both, () => Now - start > 0.2);
            a.Core.SendState(new Motion { X = 80, Y = 0 });   // 80 tiles in a moment
            Pump(server, both, () => a.Corrected != null);
            Check(Math.Abs(a.Corrected.Value.X) < 1e-4, $"put back to {a.Corrected.Value.X}");
            // Going through a door is a jump, and that's allowed.
            a.Corrected = null;
            a.Core.SendState(new Motion { X = 4000, Y = 4000, Flags = MotionFlags.Jumped | MotionFlags.Inside });
            start = Now;
            Pump(server, both, () => Now - start > 0.4);
            Check(a.Corrected == null, "a jump through a door was put back");
            Check(server.Players[0].Motion.X == 4000 || server.Players[1].Motion.X == 4000, "the server didn't take the jump");
        }
        finally { Stop(server, a, b); }
    }

    static void ChatReachesEveryone()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        var b = new Player(server.Port, "key-b", "Bob");
        var both = new[] { a, b };
        Pump(server, both, () => a.Welcome != null && b.Welcome != null);
        try
        {
            a.Core.SendChat("hello <color=red>world</color>");
            Pump(server, both, () => a.Chat.Count > 0 && b.Chat.Count > 0);
            Check(b.Chat[0].From == "Alice" && b.Chat[0].Text == "hello color=redworld/color", $"Bob heard {b.Chat[0].From}: {b.Chat[0].Text}");
            a.Core.SendChat("again");   // too soon after the first
            server.Announce("Night is coming");
            Pump(server, both, () => b.Chat.Count >= 2);
            double wait = Now;
            Pump(server, both, () => Now - wait > 0.2);
            Check(b.Chat.Count == 2 && b.Chat[1].From == "Server", "spam limit or server announcement");
        }
        finally { Stop(server, a, b); }
    }

    static void WrongVersionIsTurnedAway()
    {
        var server = NewServer();
        var p = new Player(0, null, null);
        // A game from a different version.
        var hello = new NetDataWriter();
        hello.Put(Protocol.Key, 32);
        hello.Put(Protocol.Version + 1);
        hello.Put("key-x", 64);
        hello.Put("Xavier", 40);
        var net = new LiteNetLib.NetManager(new LiteNetLib.EventBasedNetListener());
        var listener = new LiteNetLib.EventBasedNetListener();
        string why = null;
        listener.PeerDisconnectedEvent += (peer, info) =>
        {
            if (info.AdditionalData != null && info.AdditionalData.AvailableBytes > 0) why = info.AdditionalData.GetString(200);
            else why = info.Reason.ToString();
        };
        net = new LiteNetLib.NetManager(listener);
        net.Start();
        net.Connect("127.0.0.1", server.Port, hello);
        try
        {
            double end = Now + 5;
            while (why == null && Now < end) { server.Poll(Now); net.PollEvents(); Thread.Sleep(2); }
            Check(why != null && why.Contains("different version"), $"turned away for: {why}");
            Check(server.Players.Count == 0, "they got in");
        }
        finally { net.Stop(); Stop(server, p); }
    }

    static void FullServerTurnsAway()
    {
        var server = NewServer(maxPlayers: 1);
        var a = new Player(server.Port, "key-a", "Alice");
        Pump(server, new[] { a }, () => a.Welcome != null);
        var b = new Player(server.Port, "key-b", "Bob");
        try
        {
            Pump(server, new[] { a, b }, () => b.FailedWhy != null);
            Check(b.FailedWhy.Contains("full"), $"turned away for: {b.FailedWhy}");
            Check(b.Welcome == null, "Bob got in anyway");
        }
        finally { Stop(server, a, b); }
    }

    static void SameKeyTwiceIsTurnedAway()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        Pump(server, new[] { a }, () => a.Welcome != null);
        var again = new Player(server.Port, "key-a", "Alice again");
        try
        {
            Pump(server, new[] { a, again }, () => again.FailedWhy != null);
            Check(again.FailedWhy.Contains("already"), $"turned away for: {again.FailedWhy}");
        }
        finally { Stop(server, a, again); }
    }

    static void RejoiningGetsTheSameKingdom()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        var b = new Player(server.Port, "key-b", "Bob");
        Pump(server, new[] { a, b }, () => a.Welcome != null && b.Welcome != null);
        int kingdom = a.Welcome.YourKingdom;
        int oldId = a.Welcome.YourId;
        a.Core.Disconnect();
        try
        {
            Pump(server, new[] { b }, () => b.Left.Contains(oldId));
            var back = new Player(server.Port, "key-a", "Alice");
            Pump(server, new[] { b, back }, () => back.Welcome != null);
            Check(back.Welcome.YourKingdom == kingdom, "Alice got a new kingdom");
            Check(back.Welcome.Kingdoms.Count == 2, "a kingdom was added for a returning player");
            back.Core.Disconnect();
        }
        finally { Stop(server, b); }
    }

    // Leaving and coming back puts you where you were (the first time, at your throne).
    static void RejoiningComesBackWhereYouLeft()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        var b = new Player(server.Port, "key-b", "Bob");
        Pump(server, new[] { a, b }, () => a.Welcome != null && b.Welcome != null);
        Player back = null;
        try
        {
            Check(!a.Welcome.HasSpawn, "a first-time player was given somewhere to start");
            int oldId = a.Welcome.YourId;
            // Walk a little way, a step at a time (not too fast for the server).
            for (int step = 1; step <= 10; step++)
            {
                var m = new Motion { X = step * 0.5f, Y = 40f + step * 0.25f, Aim = 0, Flags = MotionFlags.Walking };
                if (step == 1) m.Flags |= MotionFlags.Jumped;
                a.Core.SendState(m);
                Pump(server, new[] { a, b }, () => FindPlayer(server, oldId) is PlayerSession p && p.HasMotion && Math.Abs(p.Motion.X - m.X) < 1e-4);
            }
            a.Core.Disconnect();
            Pump(server, new[] { b }, () => b.Left.Contains(oldId));
            back = new Player(server.Port, "key-a", "Alice");
            Pump(server, new[] { b, back }, () => back.Welcome != null);
            Check(back.Welcome.HasSpawn, "the returning player wasn't told where they were");
            Check(Math.Abs(back.Welcome.SpawnX - 5f) < 1e-3 && Math.Abs(back.Welcome.SpawnY - 42.5f) < 1e-3,
                $"came back at {back.Welcome.SpawnX}, {back.Welcome.SpawnY}, not 5, 42.5");
        }
        finally { Stop(server, b); back?.Core.Disconnect(); }
    }

    static PlayerSession FindPlayer(ServerCore server, int id)
    {
        foreach (var p in server.Players) if (p.Id == id) return p;
        return null;
    }

    // A hosted world is saved (the players who joined: their kingdoms, where they were) and loaded
    // into a new server: they get their kingdoms back and come back where they were, the host's own
    // kingdom isn't in the save, and someone new gets a spot away from all of them.
    static void SavedPlayersComeBackOnANewServer()
    {
        var first = NewServer();
        first.AddLocalPlayer("host-key", "Hosty", new KingdomInfo { X = -1, Y = 2 });
        var a = new Player(first.Port, "key-a", "Alice");
        var b = new Player(first.Port, "key-b", "Bob");
        Pump(first, new[] { a, b }, () => a.Welcome != null && b.Welcome != null);
        var aKingdom = a.Welcome.Kingdoms.Find(k => k.Id == a.Welcome.YourKingdom);
        var bKingdom = b.Welcome.Kingdoms.Find(k => k.Id == b.Welcome.YourKingdom);
        List<KingdomInfo> savedKingdoms;
        List<PlayerRecord> savedPlayers;
        try
        {
            // Alice is standing somewhere when the game is saved (still on); Bob never moved.
            a.Core.SendState(new Motion { X = 300.5f, Y = -20.25f, Flags = MotionFlags.Jumped });
            Pump(first, new[] { a, b }, () => FindPlayer(first, a.Welcome.YourId) is PlayerSession p && p.HasMotion);
            (savedKingdoms, savedPlayers) = first.Remembered();
        }
        finally { Stop(first, a, b); }

        Check(savedKingdoms.Count == 2, $"{savedKingdoms.Count} kingdoms saved, not the 2 that joined (the host's isn't saved)");
        Check(!savedPlayers.Exists(p => p.Key == "host-key"), "the host was saved with the others");
        var savedAlice = savedPlayers.Find(p => p.Key == "key-a");
        Check(savedAlice != null && savedAlice.HasPosition && Math.Abs(savedAlice.X - 300.5f) < 1e-3, "Alice's spot wasn't saved");
        var savedBob = savedPlayers.Find(p => p.Key == "key-b");
        Check(savedBob != null && !savedBob.HasPosition, "Bob never moved, but has a saved spot");

        var second = NewServer();
        second.Remember(savedKingdoms, savedPlayers);
        var host = second.AddLocalPlayer("host-key", "Hosty", new KingdomInfo { X = -1, Y = 2 });
        var alice = new Player(second.Port, "key-a", "Alice");
        var carol = new Player(second.Port, "key-c", "Carol");
        try
        {
            Pump(second, new[] { alice, carol }, () => alice.Welcome != null && carol.Welcome != null);
            var mine = alice.Welcome.Kingdoms.Find(k => k.Id == alice.Welcome.YourKingdom);
            Check(mine != null && mine.X == aKingdom.X && mine.Y == aKingdom.Y, "Alice's kingdom moved");
            Check(alice.Welcome.HasSpawn && Math.Abs(alice.Welcome.SpawnX - 300.5f) < 1e-3 && Math.Abs(alice.Welcome.SpawnY + 20.25f) < 1e-3,
                "Alice didn't come back where she was");
            Check(alice.Welcome.Kingdoms.Exists(k => k.X == bKingdom.X && k.Y == bKingdom.Y && k.Owner == "Bob"), "Bob's kingdom is gone while he's away");
            var ids = new HashSet<int>();
            foreach (var k in carol.Welcome.Kingdoms) Check(ids.Add(k.Id), $"two kingdoms are number {k.Id}");
            Check(carol.Welcome.Kingdoms.Count == 4, $"{carol.Welcome.Kingdoms.Count} kingdoms, not host + Alice + Bob + Carol");
            Check(host.KingdomId > 0 && host.KingdomId != mine.Id, "the host's kingdom took a saved one's number");
            var theirs = carol.Welcome.Kingdoms.Find(k => k.Id == carol.Welcome.YourKingdom);
            foreach (var k in carol.Welcome.Kingdoms)
            {
                if (k == theirs) continue;
                double d = Math.Sqrt(Math.Pow(k.X - theirs.X, 2) + Math.Pow(k.Y - theirs.Y, 2));
                Check(d >= 96, $"Carol's kingdom is {d:0} tiles from {k.Owner}'s");
            }
            Check(!carol.Welcome.HasSpawn, "a new player was given somewhere to start");
        }
        finally { Stop(second, alice, carol); }
    }

    // A save whose kingdom has the number the host's would get: they get different ones.
    static void SavedKingdomNumbersDontClash()
    {
        var server = new ServerCore(new ServerSettings { Port = 0 });
        server.Remember(new[] { new KingdomInfo { Id = 1, Owner = "Old", X = 500, Y = 0 }, new KingdomInfo { Id = 1, Owner = "Twin", X = 0, Y = 900 } },
            new[] { new PlayerRecord { Key = "k-old", Name = "Old", KingdomId = 1 } });
        var host = server.AddLocalPlayer("host-key", "Hosty", new KingdomInfo { Id = 1, X = 0, Y = 0 });
        var ids = new HashSet<int>();
        foreach (var k in server.Kingdoms) Check(ids.Add(k.Id), $"two kingdoms are number {k.Id}");
        Check(server.Kingdoms.Count == 3, "a kingdom was lost");
        Check(server.OwnerKey(host.KingdomId) == "host-key", "the host doesn't own their kingdom");
    }

    static void NobodyThereFailsWithAReason()
    {
        // A port nobody listens on.
        var probe = NewServer();
        int port = probe.Port;
        probe.Stop();
        var p = new Player(port, "key-a", "Alice");
        try
        {
            Pump(null, new[] { p }, () => p.FailedWhy != null, 15);
            Check(p.FailedWhy.Contains("Nobody answered"), $"failed with: {p.FailedWhy}");
        }
        finally { p.Core.Disconnect(); }
    }

    static void HostIsSeenAndHeard()
    {
        var server = NewServer();
        var host = server.AddLocalPlayer("host-key", "Hosty", new KingdomInfo { Id = 1, X = 0, Y = 0 });
        double movedAt = -1;
        server.PositionTaken += (p, t) => movedAt = t;
        var a = new Player(server.Port, "key-a", "Alice");
        var sameAsHost = new Player(server.Port, "host-key", "Hosty");
        var both = new[] { a, sameAsHost };
        try
        {
            Pump(server, both, () => a.Welcome != null && sameAsHost.FailedWhy != null);
            Check(sameAsHost.FailedWhy.Contains("host"), $"same key as the host: {sameAsHost.FailedWhy}");
            Check(a.Welcome.Players.Exists(p => p.Name == "Hosty" && p.Id == host.Id), "the host isn't in the welcome");
            var mine = a.Welcome.Kingdoms.Find(k => k.Id == a.Welcome.YourKingdom);
            Check(mine != null && mine.Id != 1 && Math.Sqrt(mine.X * mine.X + mine.Y * mine.Y) >= 96, "the joiner's kingdom is too near the host's");
            double start = Now;
            Pump(server, both, () =>
            {
                server.MoveLocal(host, new Motion { X = 3, Y = 4, Aim = 90 });
                a.Core.SendState(new Motion { X = 10, Y = 10 });
                return Now - start > 0.3 && a.Seen.ContainsKey(host.Id) && movedAt > 0;
            });
            Check(Math.Abs(a.Seen[host.Id].X - 3) < 1e-4, "Alice sees the host where they are");
            server.ChatLocal(host, "welcome!");
            Pump(server, both, () => a.Chat.Count > 0);
            Check(a.Chat[0].From == "Hosty" && a.Chat[0].Text == "welcome!", "the host's chat");
        }
        finally { Stop(server, a, sameAsHost); }
    }

    static void ManySnapshotEntriesArriveInPieces()
    {
        var server = NewServer(maxPlayers: 40);
        var players = new List<Player>();
        for (int i = 0; i < 30; i++) players.Add(new Player(server.Port, $"key-{i}", $"P{i}"));
        Pump(server, players, () => players.TrueForAll(p => p.Welcome != null), 10);
        try
        {
            double start = Now;
            Pump(server, players, () =>
            {
                for (int i = 0; i < players.Count; i++) players[i].Core.SendState(new Motion { X = i, Y = 0 });
                return Now - start > 0.5 && players[0].Seen.Count == 29;
            }, 10);
            Check(players[0].Seen.Count == 29, $"saw {players[0].Seen.Count} of 29 others");
        }
        finally { Stop(server, players.ToArray()); }
    }
}
