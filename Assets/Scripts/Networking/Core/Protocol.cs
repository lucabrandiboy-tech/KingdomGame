using System;
using System.Collections.Generic;
using System.Text;
using LiteNetLib.Utils;

namespace KingdomGame.Networking
{
    // Everything in Networking/Core is plain C# (no UnityEngine), so the network tests
    // (tools/net-tests) can run a real server and real players outside Unity.

    /// <summary>What every message starts with: what kind it is.</summary>
    public enum Msg : byte
    {
        /// <summary>Server → a player who just joined: the world and who's in it.</summary>
        Welcome = 1,
        PlayerJoined,
        PlayerLeft,
        /// <summary>Server → everyone: a kingdom has a spot on the map.</summary>
        KingdomPlaced,
        /// <summary>Player → server: where I am (many times a second).</summary>
        State,
        /// <summary>Server → player: where the others near you are (many times a second).</summary>
        Snapshot,
        /// <summary>Server → player: that move wasn't believable, you're back here.</summary>
        Correction,
        Chat,
        /// <summary>Server → everyone: the time of day.</summary>
        Clock,
        /// <summary>
        /// A player → server: what was built or taken away in their kingdom (or all of it). Server → others:
        /// the same, for any kingdom (a player joining hears every kingdom's whole lot).
        /// </summary>
        Built,
        /// <summary>A player → server: where their villagers are. Server → players near that kingdom: the same.</summary>
        Folk,
        /// <summary>A player → server: their whole kingdom, saved (the server keeps it for them).</summary>
        KingdomSave,
        /// <summary>Server → a player who just joined: their kingdom as they last saved it here (empty the first time).</summary>
        YourSave,
    }

    /// <summary>What a player's character is doing, sent with its position.</summary>
    [Flags]
    public enum MotionFlags : byte
    {
        None = 0,
        OnWalls = 1,
        Dead = 2,
        /// <summary>Inside a house or down a mine (far off the map).</summary>
        Inside = 4,
        Seated = 8,
        /// <summary>Moved there in one go (went through a door, came back after falling).</summary>
        Jumped = 16,
        Walking = 32,
        /// <summary>A villager carrying something.</summary>
        Carrying = 64,
    }

    /// <summary>A character's place, the way it faces and what it's doing.</summary>
    public struct Motion
    {
        public float X, Y;
        /// <summary>Which way it faces, in degrees.</summary>
        public float Aim;
        public MotionFlags Flags;

        public void Write(NetDataWriter w)
        {
            w.Put(X);
            w.Put(Y);
            w.Put(Aim);
            w.Put((byte)Flags);
        }

        public static Motion Read(NetDataReader r) =>
            new Motion { X = r.GetFloat(), Y = r.GetFloat(), Aim = r.GetFloat(), Flags = (MotionFlags)r.GetByte() };

        public bool IsFinite => !float.IsNaN(X) && !float.IsInfinity(X) && !float.IsNaN(Y) && !float.IsInfinity(Y) && !float.IsNaN(Aim) && !float.IsInfinity(Aim);
    }

    /// <summary>A kingdom's spot: whose it is and where its throne is (bottom-left cell).</summary>
    public sealed class KingdomInfo
    {
        public int Id;
        public string Owner;
        public int X, Y;

        public void Write(NetDataWriter w)
        {
            w.Put(Id);
            w.Put(Owner ?? "", Protocol.MaxNameLength);
            w.Put(X);
            w.Put(Y);
        }

        public static KingdomInfo Read(NetDataReader r) =>
            new KingdomInfo { Id = r.GetInt(), Owner = r.GetString(Protocol.MaxNameLength), X = r.GetInt(), Y = r.GetInt() };
    }

    /// <summary>Someone on the server.</summary>
    public sealed class PlayerInfo
    {
        public int Id;
        public string Name;
        public int KingdomId;

        public void Write(NetDataWriter w)
        {
            w.Put(Id);
            w.Put(Name ?? "", Protocol.MaxNameLength);
            w.Put(KingdomId);
        }

        public static PlayerInfo Read(NetDataReader r) =>
            new PlayerInfo { Id = r.GetInt(), Name = r.GetString(Protocol.MaxNameLength), KingdomId = r.GetInt() };
    }

    /// <summary>What a player is told when they join.</summary>
    public sealed class WelcomeInfo
    {
        public int YourId;
        public int YourKingdom;
        public string ServerName;
        /// <summary>Everyone makes the same world from the same seed.</summary>
        public int Seed;
        public int Day;
        public float Hour;
        /// <summary>The host's game: survival (research the ages) or free build (everything open).</summary>
        public bool Survival;
        /// <summary>Where you were when you last left this server (false the first time: start at your throne).</summary>
        public bool HasSpawn;
        public float SpawnX, SpawnY;
        public readonly List<PlayerInfo> Players = new List<PlayerInfo>();
        public readonly List<KingdomInfo> Kingdoms = new List<KingdomInfo>();

        public void Write(NetDataWriter w)
        {
            w.Put((byte)Msg.Welcome);
            w.Put(YourId);
            w.Put(YourKingdom);
            w.Put(ServerName ?? "", Protocol.MaxServerNameLength);
            w.Put(Seed);
            w.Put(Day);
            w.Put(Hour);
            w.Put(Survival);
            w.Put(HasSpawn);
            w.Put(SpawnX);
            w.Put(SpawnY);
            w.Put((ushort)Players.Count);
            foreach (var p in Players) p.Write(w);
            w.Put((ushort)Kingdoms.Count);
            foreach (var k in Kingdoms) k.Write(w);
        }

        /// <summary>Reads one (after its <see cref="Msg"/> byte).</summary>
        public static WelcomeInfo Read(NetDataReader r)
        {
            var info = new WelcomeInfo
            {
                YourId = r.GetInt(),
                YourKingdom = r.GetInt(),
                ServerName = r.GetString(Protocol.MaxServerNameLength),
                Seed = r.GetInt(),
                Day = r.GetInt(),
                Hour = r.GetFloat(),
                Survival = r.GetBool(),
                HasSpawn = r.GetBool(),
                SpawnX = r.GetFloat(),
                SpawnY = r.GetFloat(),
            };
            int players = r.GetUShort();
            for (int i = 0; i < players; i++) info.Players.Add(PlayerInfo.Read(r));
            int kingdoms = r.GetUShort();
            for (int i = 0; i < kingdoms; i++) info.Kingdoms.Add(KingdomInfo.Read(r));
            return info;
        }
    }

    /// <summary>
    /// One thing built in a kingdom, as the others see it: a wall tile (<see cref="Name"/>: its material), or a
    /// building (its config's name), with its cells, the way it faces and (a house) its level. Two the same are equal.
    /// </summary>
    public struct BuiltPiece : IEquatable<BuiltPiece>
    {
        public bool Wall;
        public string Name;
        public int X, Y, W, H;
        public byte Rotation, Level;

        public void Write(NetDataWriter w)
        {
            w.Put(Wall);
            w.Put(Name ?? "", Protocol.MaxPieceName);
            w.Put(X);
            w.Put(Y);
            w.Put((short)W);
            w.Put((short)H);
            w.Put(Rotation);
            w.Put(Level);
        }

        public static BuiltPiece Read(NetDataReader r) => new BuiltPiece
        {
            Wall = r.GetBool(), Name = r.GetString(Protocol.MaxPieceName), X = r.GetInt(), Y = r.GetInt(),
            W = r.GetShort(), H = r.GetShort(), Rotation = r.GetByte(), Level = r.GetByte(),
        };

        public bool Equals(BuiltPiece o) =>
            Wall == o.Wall && X == o.X && Y == o.Y && W == o.W && H == o.H && Rotation == o.Rotation && Level == o.Level && string.Equals(Name, o.Name);

        public override bool Equals(object obj) => obj is BuiltPiece o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = (Name ?? "").GetHashCode();
                h = h * 31 + X;
                h = h * 31 + Y;
                h = h * 31 + (W << 8 | H);
                h = h * 31 + (Rotation << 8 | Level);
                return Wall ? ~h : h;
            }
        }

        public override string ToString() => $"{(Wall ? "wall" : "building")} {Name} at {X},{Y} ({W}x{H}, turned {Rotation}, level {Level})";
    }

    /// <summary>What a kingdom has built, worked out as changes: what went and what's new since the last time.</summary>
    public static class BuiltChanges
    {
        /// <summary>The pieces in <paramref name="before"/> that aren't in <paramref name="now"/>, and the other way round.</summary>
        public static void Diff(HashSet<BuiltPiece> before, HashSet<BuiltPiece> now, List<BuiltPiece> removed, List<BuiltPiece> added)
        {
            removed.Clear();
            added.Clear();
            foreach (var p in before) if (!now.Contains(p)) removed.Add(p);
            foreach (var p in now) if (!before.Contains(p)) added.Add(p);
        }

        /// <summary>
        /// Writes the changes as Built messages, as many as it takes (each small enough to send in one go), calling
        /// <paramref name="send"/> for each. Only the first says <paramref name="replaceAll"/>.
        /// </summary>
        public static void Write(NetDataWriter w, int kingdom, bool replaceAll, IReadOnlyList<BuiltPiece> removed, IReadOnlyList<BuiltPiece> added, Action<NetDataWriter> send)
        {
            int r = 0, a = 0;
            bool first = true;
            while (first || r < removed.Count || a < added.Count)
            {
                int rn = Math.Min(Protocol.PiecesPerMessage, removed.Count - r);
                int an = Math.Min(Protocol.PiecesPerMessage - rn, added.Count - a);
                w.Reset();
                w.Put((byte)Msg.Built);
                w.Put(kingdom);
                w.Put(first && replaceAll);
                w.Put((ushort)rn);
                for (int i = 0; i < rn; i++) removed[r + i].Write(w);
                w.Put((ushort)an);
                for (int i = 0; i < an; i++) added[a + i].Write(w);
                send(w);
                r += rn;
                a += an;
                first = false;
            }
        }

        /// <summary>Reads a Built message (after its <see cref="Msg"/> byte).</summary>
        public static (int Kingdom, bool ReplaceAll) Read(NetDataReader r, List<BuiltPiece> removed, List<BuiltPiece> added)
        {
            int kingdom = r.GetInt();
            bool replaceAll = r.GetBool();
            removed.Clear();
            added.Clear();
            int rn = r.GetUShort();
            for (int i = 0; i < rn; i++) removed.Add(BuiltPiece.Read(r));
            int an = r.GetUShort();
            for (int i = 0; i < an; i++) added.Add(BuiltPiece.Read(r));
            return (kingdom, replaceAll);
        }
    }

    /// <summary>What the server keeps of a player's kingdom while they're away: what's built (for the others to see) and their save.</summary>
    public sealed class KingdomMemory
    {
        public int KingdomId;
        public readonly List<BuiltPiece> Pieces = new List<BuiltPiece>();
        /// <summary>Their whole kingdom as their game saved it (compressed), or null.</summary>
        public byte[] Save;
    }

    public static class Protocol
    {
        /// <summary>Goes up whenever messages change: a game and a server must have the same one.</summary>
        public const int Version = 3;
        /// <summary>Longest name of a building or wall material in a Built message.</summary>
        public const int MaxPieceName = 64;
        /// <summary>Most pieces in one Built message (so each is a modest size).</summary>
        public const int PiecesPerMessage = 300;
        /// <summary>Most pieces the server keeps for one kingdom.</summary>
        public const int MaxPiecesPerKingdom = 200000;
        /// <summary>Biggest saved kingdom the server takes (bytes, compressed).</summary>
        public const int MaxSaveBytes = 8 * 1024 * 1024;
        /// <summary>Most villagers in one Folk message.</summary>
        public const int FolkPerMessage = 64;
        /// <summary>Starts every connection request, so stray traffic is turned away early.</summary>
        public const string Key = "KingdomGame";
        public const int DefaultPort = 7777;
        public const int MaxNameLength = 20;
        public const int MaxServerNameLength = 40;
        public const int MaxChatLength = 160;
        public const int MaxPlayerKeyLength = 64;

        /// <summary>
        /// A name as it may be shown: letters, digits, spaces and a few marks, no angle brackets
        /// (the game's text understands &lt;color&gt; tags), at most <see cref="MaxNameLength"/> long.
        /// </summary>
        public static string CleanName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name ?? "")
            {
                if (sb.Length >= MaxNameLength) break;
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.' || c == '\'') sb.Append(c);
            }
            string clean = sb.ToString().Trim();
            return clean.Length > 0 ? clean : "Ruler";
        }

        /// <summary>Chat text as it may be shown: no tags, no control characters, at most <see cref="MaxChatLength"/> long.</summary>
        public static string CleanChat(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text ?? "")
            {
                if (sb.Length >= MaxChatLength) break;
                if (c == '<' || c == '>' || char.IsControl(c)) continue;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        /// <summary>The connection request a game sends: who it is.</summary>
        public static NetDataWriter Hello(string playerKey, string name)
        {
            var w = new NetDataWriter();
            w.Put(Key, 32);
            w.Put(Version);
            w.Put(playerKey ?? "", MaxPlayerKeyLength);
            w.Put(name ?? "", MaxNameLength * 2);
            return w;
        }
    }
}
