using System;
using System.Collections.Generic;
using KingdomGame.Networking;

// Trading and sharing zombies between players: messages from one player to another, passed on by the server
// (Msg.Direct), and what's in them (Networking/Core/Coop.cs).
static partial class Tests
{
    // A message for one player reaches that player only (saying who sent it), the host's game hears its own,
    // and the host can send to a player too.
    static void DirectMessagesReachOnlyTheirTarget()
    {
        var server = NewServer();
        var host = server.AddLocalPlayer("host-key", "Hosty", new KingdomInfo { Id = 1, X = 0, Y = 0 });
        var hostHeard = new List<(int From, DirectKind Kind, byte[] Bytes)>();
        server.DirectReceived += (from, kind, bytes) => hostHeard.Add((from, kind, bytes));
        var a = new Player(server.Port, "key-a", "Alice");
        var b = new Player(server.Port, "key-b", "Bob");
        var c = new Player(server.Port, "key-c", "Cleo");
        var all = new[] { a, b, c };
        Pump(server, all, () => a.Welcome != null && b.Welcome != null && c.Welcome != null);
        try
        {
            var offer = new TradeOffer { Id = 7, Give = new[] { 50, 0, 10 }, Want = new[] { 0, 30 } };
            a.Core.SendDirect(b.Welcome.YourId, DirectKind.TradeOffer, offer.Write(), true);
            a.Core.SendDirect(host.Id, DirectKind.TradeReply, new TradeReply { Id = 3, Accepted = true }.Write(), true);
            server.LocalDirect(host, c.Welcome.YourId, DirectKind.PlayerHit, new PlayerHit { Damage = 9f, FromX = 1, FromY = 2 }.Write(), true);
            Pump(server, all, () => b.Direct.Count > 0 && hostHeard.Count > 0 && c.Direct.Count > 0);
            double wait = Now;
            Pump(server, all, () => Now - wait > 0.2);
            Check(b.Direct.Count == 1 && b.Direct[0].From == a.Welcome.YourId && b.Direct[0].Kind == DirectKind.TradeOffer, "Bob didn't get Alice's offer");
            var got = TradeOffer.Read(b.Direct[0].Bytes);
            Check(got.Id == 7 && got.Give[0] == 50 && got.Give[2] == 10 && got.Want[1] == 30, "the offer came out different");
            Check(a.Direct.Count == 0, "Alice heard her own message");
            Check(hostHeard.Count == 1 && hostHeard[0].From == a.Welcome.YourId && TradeReply.Read(hostHeard[0].Bytes).Accepted, "the host didn't get Alice's reply");
            Check(c.Direct.Count == 1 && c.Direct[0].From == host.Id && Math.Abs(PlayerHit.Read(c.Direct[0].Bytes).Damage - 9f) < 1e-4f, "Cleo didn't get the host's hit");
            // Zombies go unreliably (only the latest matter), and still arrive.
            var batch = new List<ZombieEntry> { new ZombieEntry { Id = 5, Kind = 2, X = 10.5f, Y = -3f, Angle = 90f, Frame = 3, Health = 0.5f } };
            b.Core.SendDirect(a.Welcome.YourId, DirectKind.Zombies, ZombieBatch.Write(batch, 0, 1), false);
            Pump(server, all, () => a.Direct.Count > 0);
            var seen = new List<ZombieEntry>();
            ZombieBatch.Read(a.Direct[0].Bytes, seen);
            Check(seen.Count == 1 && seen[0].Id == 5 && Math.Abs(seen[0].X - 10.5f) < 1e-4f, "Alice didn't see Bob's zombie");
            // To nobody: nothing happens (and nobody is disconnected).
            a.Core.SendDirect(9999, DirectKind.ZombieHit, new ZombieHit { Id = 1, Damage = 5 }.Write(), true);
            wait = Now;
            Pump(server, all, () => Now - wait > 0.2);
            Check(a.Core.Connected && b.Direct.Count == 1 && c.Direct.Count == 1, "a message to nobody went somewhere");
        }
        finally { Stop(server, a, b, c); }
    }

    // What's written is what's read, and nothing broken or absurd gets through.
    static void TradesAndZombiesReadBackTheSame()
    {
        var offer = TradeOffer.Read(new TradeOffer { Id = -2, Give = new[] { -5, 3, int.MaxValue }, Want = new int[0] }.Write());
        Check(offer.Id == -2 && offer.Give.Length == 3 && offer.Give[0] == 0 && offer.Give[1] == 3 && offer.Give[2] == TradeOffer.MaxAmount, "amounts aren't kept in bounds");
        Check(!offer.Empty && new TradeOffer().Empty, "Empty is wrong");
        var list = new List<ZombieEntry>();
        for (int i = 0; i < 100; i++) list.Add(new ZombieEntry { Id = (ushort)i, Kind = ZombieBatch.BossKind, X = i, Y = -i, Angle = i * 7f, Frame = (byte)(i % 6), Health = i / 100f });
        var bytes = ZombieBatch.Write(list, 10, 100);
        Check(bytes.Length <= Protocol.MaxUnreliableDirectBytes, $"a full batch is {bytes.Length} bytes: too big to send unreliably");
        var back = new List<ZombieEntry>();
        ZombieBatch.Read(bytes, back);
        Check(back.Count == ZombieBatch.PerMessage, $"{back.Count} zombies in a batch");
        Check(back[0].Id == 10 && back[0].Kind == ZombieBatch.BossKind && back[0].X == 10f && Math.Abs(back[0].Angle - 70f) < 1.5f && Math.Abs(back[0].Health - 0.1f) < 0.01f, "a zombie came out different");
        var hit = ZombieHit.Read(new ZombieHit { Id = 3, Damage = float.NaN }.Write());
        Check(hit.Damage == 0f, "a broken hit got through");
        Check(PlayerHit.Read(new PlayerHit { Damage = -4f }.Write()).Damage == 0f, "a negative hit got through");
    }

    // Too big for the server to pass on: dropped quietly.
    static void OversizedDirectMessagesAreDropped()
    {
        var server = NewServer();
        var a = new Player(server.Port, "key-a", "Alice");
        var b = new Player(server.Port, "key-b", "Bob");
        var both = new[] { a, b };
        Pump(server, both, () => a.Welcome != null && b.Welcome != null);
        try
        {
            a.Core.SendDirect(b.Welcome.YourId, DirectKind.TradeOffer, new byte[Protocol.MaxDirectBytes + 1], true);   // (the game won't even send it)
            a.Core.SendDirect(b.Welcome.YourId, DirectKind.TradeOffer, new byte[Protocol.MaxDirectBytes], true);
            Pump(server, both, () => b.Direct.Count > 0);
            double wait = Now;
            Pump(server, both, () => Now - wait > 0.2);
            Check(b.Direct.Count == 1 && b.Direct[0].Bytes.Length == Protocol.MaxDirectBytes, $"Bob got {b.Direct.Count} messages");
        }
        finally { Stop(server, a, b); }
    }
}
