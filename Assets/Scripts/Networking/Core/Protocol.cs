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

    public static class Protocol
    {
        /// <summary>Goes up whenever messages change: a game and a server must have the same one.</summary>
        public const int Version = 2;
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
