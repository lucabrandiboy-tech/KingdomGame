using System;
using System.Collections.Generic;
using System.Linq;
using KingdomGame.Networking;
using LiteNetLib.Utils;

// Shared kingdoms (online stage 3): what each player builds reaches the others (and players joining later),
// villagers are seen near their kingdom, and each player's kingdom is kept by the server for next time.
static partial class Tests
{
    static BuiltPiece WallAt(int x, int y) => new BuiltPiece { Wall = true, Name = "StoneWall", X = x, Y = y, W = 1, H = 1 };

    static BuiltPiece Piece(string name, int x, int y, int rotation = 0, int level = 0) =>
        new BuiltPiece { Name = name, X = x, Y = y, W = 2, H = 1, Rotation = (byte)rotation, Level = (byte)level };

    static List<BuiltPiece> Pieces(params BuiltPiece[] p) => new List<BuiltPiece>(p);

    static void BuiltChangesAreFoundAndSent()
    {
        var before = new HashSet<BuiltPiece> { WallAt(0, 0), WallAt(1, 0), Piece("Bed", 5, 5) };
        var now = new HashSet<BuiltPiece> { WallAt(0, 0), Piece("Bed", 5, 5, rotation: 1), Piece("Table", 7, 7) };
        var removed = new List<BuiltPiece>();
        var added = new List<BuiltPiece>();
        BuiltChanges.Diff(before, now, removed, added);
        Check(removed.Count == 2 && removed.Contains(WallAt(1, 0)) && removed.Contains(Piece("Bed", 5, 5)), $"removed: {string.Join("; ", removed)}");
        Check(added.Count == 2 && added.Contains(Piece("Bed", 5, 5, rotation: 1)) && added.Contains(Piece("Table", 7, 7)), $"added: {string.Join("; ", added)}");
        BuiltChanges.Diff(now, now, removed, added);
        Check(removed.Count == 0 && added.Count == 0, "the same twice isn't a change");
        Check(!Piece("House", 1, 1, level: 1).Equals(Piece("House", 1, 1, level: 2)), "a house going up a level is a change");
        Check(!WallAt(3, 3).Equals(new BuiltPiece { Name = "StoneWall", X = 3, Y = 3, W = 1, H = 1 }), "a wall and a building aren't the same");

        // Many changes go in several messages; only the first says "replace all"; reading them back gives the same.
        var many = new List<BuiltPiece>();
        for (int i = 0; i < 1000; i++) many.Add(WallAt(i, -i));
        var gone = Pieces(Piece("Bed", 1, 1), Piece("Chair", 2, 2));
        var messages = new List<byte[]>();
        BuiltChanges.Write(new NetDataWriter(), 9, true, gone, many, w => messages.Add(w.CopyData()));
        Check(messages.Count == (1002 + Protocol.PiecesPerMessage - 1) / Protocol.PiecesPerMessage, $"{messages.Count} messages for 1002 pieces");
        var gotRemoved = new List<BuiltPiece>();
        var gotAdded = new List<BuiltPiece>();
        for (int m = 0; m < messages.Count; m++)
        {
            var r = new NetDataReader(messages[m]);
            Check(r.GetByte() == (byte)Msg.Built, "not a Built message");
            var (kingdom, all) = BuiltChanges.Read(r, removed, added);
            Check(kingdom == 9, $"kingdom {kingdom}");
            Check(all == (m == 0), $"message {m} says replace all: {all}");
            gotRemoved.AddRange(removed);
            gotAdded.AddRange(added);
        }
        Check(gotRemoved.SequenceEqual(gone) && gotAdded.SequenceEqual(many), "what was read back isn't what was written");
        // Nothing to say, but replace all: one message, empty.
        messages.Clear();
        BuiltChanges.Write(new NetDataWriter(), 9, true, new List<BuiltPiece>(), new List<BuiltPiece>(), w => messages.Add(w.CopyData()));
        Check(messages.Count == 1, "an empty replace-all still goes");
    }

    static void BuildingsReachEveryoneAndLateJoiners()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        Player b = null;
        try
        {
            Pump(server, new[] { a }, () => a.Welcome != null && a.YourSave != null);
            Check(a.YourSave.Length == 0, "a new player has a save");
            int ka = a.Welcome.YourKingdom;
            a.Core.SendBuilt(true, Pieces(), Pieces(WallAt(0, 0), WallAt(1, 0), Piece("Bed", 3, 3)));
            Pump(server, new[] { a }, () => server.BuiltIn(ka).Count == 3);

            b = new Player(server.Port, "key-b", "Bob");
            var both = new[] { a, b };
            Pump(server, both, () => b.Built.TryGetValue(ka, out var s) && s.Count == 3);
            // A change reaches Bob; Alice isn't sent her own building back.
            a.Core.SendBuilt(false, Pieces(WallAt(1, 0)), Pieces(Piece("Table", 6, 6)));
            Pump(server, both, () => b.Built[ka].Contains(Piece("Table", 6, 6)) && !b.Built[ka].Contains(WallAt(1, 0)));
            Check(server.BuiltIn(ka).Count == 3, $"the server knows {server.BuiltIn(ka).Count} pieces of Alice's");
            Check(!a.Built.ContainsKey(ka), "Alice was sent her own building");
            // Bob builds: Alice sees it.
            int kb = b.Welcome.YourKingdom;
            b.Core.SendBuilt(true, Pieces(), Pieces(WallAt(500, 500)));
            Pump(server, both, () => a.Built.TryGetValue(kb, out var s) && s.Contains(WallAt(500, 500)));
            // Bob leaves: his kingdom is still there for those who join after.
            b.Core.Disconnect();
            Pump(server, new[] { a }, () => server.Players.Count == 1);
            var c = new Player(server.Port, "key-c", "Carol");
            try
            {
                Pump(server, new[] { a, c }, () => c.Built.TryGetValue(kb, out var s) && s.Count == 1 && c.Built.TryGetValue(ka, out var t) && t.Count == 3);
            }
            finally { c.Core.Disconnect(); }
        }
        finally
        {
            if (b != null) b.Core.Disconnect();
            Stop(server, a);
        }
    }

    // A game that isn't ours (or a broken one) says it's building in someone else's kingdom: it's its own that changes.
    static void NobodyBuildsInAnotherKingdom()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        var listener = new LiteNetLib.EventBasedNetListener();
        var net = new LiteNetLib.NetManager(listener);
        LiteNetLib.NetPeer peer = null;
        try
        {
            Pump(server, new[] { a }, () => a.Welcome != null);
            int ka = a.Welcome.YourKingdom;
            a.Core.SendBuilt(true, Pieces(), Pieces(WallAt(0, 0)));
            Pump(server, new[] { a }, () => server.BuiltIn(ka).Count == 1);

            bool welcomed = false;
            listener.NetworkReceiveEvent += (from, reader, channel, method) =>
            {
                if (reader.AvailableBytes > 0 && reader.GetByte() == (byte)Msg.Welcome) welcomed = true;
                reader.Recycle();
            };
            net.Start();
            peer = net.Connect("127.0.0.1", server.Port, Protocol.Hello("key-sneaky", "Sneaky"));
            Pump(server, new[] { a }, () => { net.PollEvents(); return welcomed; });
            var sneaky = server.Players.First(p => p.Name == "Sneaky");
            // "In Alice's kingdom: her wall is gone, here's mine."
            BuiltChanges.Write(new NetDataWriter(), ka, true, Pieces(WallAt(0, 0)), Pieces(WallAt(9, 9)), w => peer.Send(w, LiteNetLib.DeliveryMethod.ReliableOrdered));
            Pump(server, new[] { a }, () => { net.PollEvents(); return server.BuiltIn(sneaky.KingdomId).Count == 1; });
            Check(server.BuiltIn(ka).Count == 1 && server.BuiltIn(ka).Contains(WallAt(0, 0)), "Alice's kingdom was changed by someone else");
            Check(server.BuiltIn(sneaky.KingdomId).Contains(WallAt(9, 9)), "the change didn't go to the sender's own kingdom");
        }
        finally
        {
            net.Stop();
            Stop(server, a);
        }
    }

    static void SavedKingdomComesBackOnRejoin()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        Player again = null;
        try
        {
            Pump(server, new[] { a }, () => a.Welcome != null && a.YourSave != null);
            int ka = a.Welcome.YourKingdom;
            // A big save (several hundred packets' worth): it arrives whole.
            var bytes = new byte[300000];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i * 7 + i / 977);
            a.Core.SendKingdomSave(bytes);
            Pump(server, new[] { a }, () => server.SaveOf(ka) != null, 10);
            Check(server.SaveOf(ka).SequenceEqual(bytes), "the server kept a different save");
            a.Core.Disconnect();
            Pump(server, new Player[0], () => server.Players.Count == 0);
            again = new Player(server.Port, "key-a", "Alice");
            Pump(server, new[] { again }, () => again.YourSave != null, 10);
            Check(again.Welcome.YourKingdom == ka, "Alice came back to another kingdom");
            Check(again.YourSave.SequenceEqual(bytes), $"Alice got back {again.YourSave.Length} bytes, not her save");
        }
        finally
        {
            if (again != null) again.Core.Disconnect();
            Stop(server, a);
        }
    }

    static void KingdomsAreKeptByANewServer()
    {
        var first = NewServer();
        var a = new Player(first.Port, "key-a", "Alice");
        int ka;
        var save = new byte[] { 1, 2, 3, 4, 5 };
        List<KingdomMemory> memories;
        List<KingdomInfo> kingdoms;
        List<PlayerRecord> players;
        try
        {
            Pump(first, new[] { a }, () => a.Welcome != null);
            ka = a.Welcome.YourKingdom;
            a.Core.SendBuilt(true, Pieces(), Pieces(WallAt(0, 0), Piece("Bed", 2, 2)));
            a.Core.SendKingdomSave(save);
            Pump(first, new[] { a }, () => first.BuiltIn(ka).Count == 2 && first.SaveOf(ka) != null);
            memories = first.Memories();
            (kingdoms, players) = first.Remembered();
        }
        finally { Stop(first, a); }
        Check(memories.Count == 1 && memories[0].KingdomId == ka && memories[0].Pieces.Count == 2, "the memories to save are wrong");

        var second = NewServer();
        second.Remember(kingdoms, players);
        second.RememberBuilt(memories);
        Check(second.BuiltIn(ka).Count == 2 && second.SaveOf(ka).SequenceEqual(save), "the new server forgot Alice's kingdom");
        var b = new Player(second.Port, "key-b", "Bob");
        var back = new Player(second.Port, "key-a", "Alice");
        try
        {
            Pump(second, new[] { b, back }, () => b.Built.TryGetValue(ka, out var s) && s.Count == 2 && back.YourSave != null);
            Check(back.Welcome.YourKingdom == ka && back.YourSave.SequenceEqual(save), "Alice didn't get her kingdom back from the new server");
            Check(!back.Built.ContainsKey(ka), "Alice was sent her own kingdom's pieces (her save has them)");
        }
        finally { Stop(second, b, back); }
    }

    static void VillagersAreSentOnlyToThoseNear()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        var near = new Player(server.Port, "key-b", "Bob");
        var far = new Player(server.Port, "key-c", "Carol");
        var all = new[] { a, near, far };
        try
        {
            Pump(server, all, () => all.All(p => p.Welcome != null));
            int ka = a.Welcome.YourKingdom;
            var k = a.Welcome.Kingdoms.First(x => x.Id == ka);
            double start = Now;
            Pump(server, all, () =>
            {
                near.Core.SendState(new Motion { X = k.X + 20, Y = k.Y });
                far.Core.SendState(new Motion { X = k.X + 3000, Y = k.Y + 3000 });
                a.Core.SendFolk(new List<(int, Motion)> { (0, new Motion { X = k.X + 1, Y = k.Y, Flags = MotionFlags.Walking }), (1, new Motion { X = k.X + 2, Y = k.Y }) });
                return Now - start > 0.4 && near.Folk.TryGetValue(ka, out var f) && f.Count == 2;
            });
            Check(Math.Abs(near.Folk[ka][0].X - (k.X + 1)) < 1e-4 && (near.Folk[ka][0].Flags & MotionFlags.Walking) != 0, "Bob sees the villager wrong");
            Check(!far.Folk.ContainsKey(ka), "Carol, far away, was told about Alice's villagers");
            Check(!a.Folk.ContainsKey(ka), "Alice was sent her own villagers");
        }
        finally { Stop(server, all); }
    }

    static void HostAndPlayersSeeEachOthersKingdoms()
    {
        var server = NewServer();
        var host = server.AddLocalPlayer("host-key", "Hosty", new KingdomInfo { Id = 1, X = 0, Y = 0 });
        var hostSaw = new Dictionary<int, HashSet<BuiltPiece>>();
        var hostSawFolk = new Dictionary<int, int>();
        server.KingdomBuilt += (k, replaceAll, removed, added) =>
        {
            if (!hostSaw.TryGetValue(k, out var set)) hostSaw[k] = set = new HashSet<BuiltPiece>();
            if (replaceAll) set.Clear();
            foreach (var p in removed) set.Remove(p);
            foreach (var p in added) set.Add(p);
        };
        server.FolkMoved += (k, t, list) => hostSawFolk[k] = list.Count;
        // The host built before anyone came: a player joining sees it.
        server.LocalBuilt(host, true, Pieces(), Pieces(WallAt(0, 3), Piece("Bed", 4, 4)));
        var a = new Player(server.Port, "key-a", "Alice");
        try
        {
            Pump(server, new[] { a }, () => a.Built.TryGetValue(host.KingdomId, out var s) && s.Count == 2);
            int ka = a.Welcome.YourKingdom;
            a.Core.SendBuilt(true, Pieces(), Pieces(WallAt(300, 300)));
            Pump(server, new[] { a }, () => hostSaw.TryGetValue(ka, out var s) && s.Contains(WallAt(300, 300)));
            // The host's villagers, seen by Alice (by the host's kingdom); Alice's, seen by the host.
            double start = Now;
            Pump(server, new[] { a }, () =>
            {
                a.Core.SendState(new Motion { X = 5, Y = 5 });
                server.LocalFolk(host, new List<(int, Motion)> { (0, new Motion { X = 1, Y = 1 }) });
                a.Core.SendFolk(new List<(int, Motion)> { (0, new Motion { X = 301, Y = 301 }), (1, new Motion { X = 302, Y = 301 }) });
                return Now - start > 0.3 && a.Folk.ContainsKey(host.KingdomId) && hostSawFolk.TryGetValue(ka, out int n) && n == 2;
            });
            // The host takes something down: Alice sees it go.
            server.LocalBuilt(host, false, Pieces(Piece("Bed", 4, 4)), Pieces());
            Pump(server, new[] { a }, () => !a.Built[host.KingdomId].Contains(Piece("Bed", 4, 4)));
            Check(server.Memories().All(m => m.KingdomId != host.KingdomId), "the host's own kingdom is among what's kept for the others");
        }
        finally { Stop(server, a); }
    }

    static void BigKingdomArrivesInPieces()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        Player b = null;
        try
        {
            Pump(server, new[] { a }, () => a.Welcome != null);
            int ka = a.Welcome.YourKingdom;
            var lots = new List<BuiltPiece>();
            for (int y = 0; y < 50; y++)
                for (int x = 0; x < 50; x++) lots.Add(WallAt(x, y));
            a.Core.SendBuilt(true, Pieces(), lots);
            Pump(server, new[] { a }, () => server.BuiltIn(ka).Count == lots.Count, 10);
            b = new Player(server.Port, "key-b", "Bob");
            Pump(server, new[] { a, b }, () => b.Built.TryGetValue(ka, out var s) && s.Count == lots.Count, 10);
            Check(b.BuiltMessages >= lots.Count / Protocol.PiecesPerMessage, $"{lots.Count} pieces came in {b.BuiltMessages} messages");
        }
        finally
        {
            if (b != null) b.Core.Disconnect();
            Stop(server, a);
        }
    }
}
