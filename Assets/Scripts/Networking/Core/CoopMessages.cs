using System;
using System.Collections.Generic;
using LiteNetLib.Utils;

namespace KingdomGame.Networking
{
    // What players send each other (Msg.Direct, passed on by the server): trades, and the zombies of one kingdom
    // shown to a player helping defend it. Plain C# like the rest of Networking/Core, so the network tests check it.

    /// <summary>A trade: what the sender gives and what they want back (an amount for each kind of resource).</summary>
    public sealed class TradeOffer
    {
        /// <summary>Most kinds of resource in an offer, and most of one.</summary>
        public const int MaxKinds = 32, MaxAmount = 1000000;
        public int Id;
        public int[] Give = Array.Empty<int>();
        public int[] Want = Array.Empty<int>();

        public byte[] Write()
        {
            var w = new NetDataWriter();
            w.Put(Id);
            Put(w, Give);
            Put(w, Want);
            return w.CopyData();
        }

        public static TradeOffer Read(byte[] bytes)
        {
            var r = new NetDataReader(bytes);
            return new TradeOffer { Id = r.GetInt(), Give = Get(r), Want = Get(r) };
        }

        static void Put(NetDataWriter w, int[] amounts)
        {
            int n = Math.Min(amounts?.Length ?? 0, MaxKinds);
            w.Put((byte)n);
            for (int i = 0; i < n; i++) w.Put(Math.Max(0, Math.Min(MaxAmount, amounts[i])));
        }

        static int[] Get(NetDataReader r)
        {
            int n = Math.Min((int)r.GetByte(), MaxKinds);
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = Math.Max(0, Math.Min(MaxAmount, r.GetInt()));
            return a;
        }

        /// <summary>Whether anything at all changes hands.</summary>
        public bool Empty
        {
            get
            {
                foreach (int n in Give) if (n > 0) return false;
                foreach (int n in Want) if (n > 0) return false;
                return true;
            }
        }
    }

    /// <summary>Yes or no to a trade offer.</summary>
    public struct TradeReply
    {
        public int Id;
        public bool Accepted;

        public byte[] Write()
        {
            var w = new NetDataWriter();
            w.Put(Id);
            w.Put(Accepted);
            return w.CopyData();
        }

        public static TradeReply Read(byte[] bytes)
        {
            var r = new NetDataReader(bytes);
            return new TradeReply { Id = r.GetInt(), Accepted = r.GetBool() };
        }
    }

    /// <summary>One zombie as another player sees it.</summary>
    public struct ZombieEntry
    {
        /// <summary>Its number in its kingdom's game.</summary>
        public ushort Id;
        /// <summary>Which kind (its place in the Survival Config's list; <see cref="ZombieBatch.BossKind"/>: the boss).</summary>
        public byte Kind;
        public float X, Y;
        /// <summary>Which way it faces, in degrees.</summary>
        public float Angle;
        /// <summary>Its picture (walking frames 0-3, 4 winding up, 5 striking).</summary>
        public byte Frame;
        /// <summary>Its health, 0..1.</summary>
        public float Health;
    }

    /// <summary>Some of a kingdom's zombies, near the player they're sent to (or, as <see cref="DirectKind.ZombiesGone"/>, ones that died).</summary>
    public static class ZombieBatch
    {
        public const byte BossKind = 254;
        /// <summary>Most zombies in one message (14 bytes each: it fits in the smallest packet, unsplit).</summary>
        public const int PerMessage = 40;

        public static byte[] Write(IReadOnlyList<ZombieEntry> list, int start, int count)
        {
            var w = new NetDataWriter();
            count = Math.Max(0, Math.Min(count, Math.Min(PerMessage, list.Count - start)));
            w.Put((byte)count);
            for (int i = 0; i < count; i++)
            {
                var z = list[start + i];
                w.Put(z.Id);
                w.Put(z.Kind);
                w.Put(z.X);
                w.Put(z.Y);
                w.Put((byte)Math.Round(((z.Angle % 360f + 360f) % 360f) / 360f * 255f));
                w.Put(z.Frame);
                w.Put((byte)Math.Round(Math.Max(0f, Math.Min(1f, z.Health)) * 255f));
            }
            return w.CopyData();
        }

        public static void Read(byte[] bytes, List<ZombieEntry> into)
        {
            into.Clear();
            var r = new NetDataReader(bytes);
            int n = Math.Min((int)r.GetByte(), PerMessage);
            for (int i = 0; i < n; i++)
            {
                var z = new ZombieEntry
                {
                    Id = r.GetUShort(), Kind = r.GetByte(), X = r.GetFloat(), Y = r.GetFloat(),
                    Angle = r.GetByte() / 255f * 360f, Frame = r.GetByte(), Health = r.GetByte() / 255f,
                };
                if (float.IsNaN(z.X) || float.IsInfinity(z.X) || float.IsNaN(z.Y) || float.IsInfinity(z.Y)) continue;
                into.Add(z);
            }
        }
    }

    /// <summary>A hit on one of the receiver's zombies.</summary>
    public struct ZombieHit
    {
        public ushort Id;
        public float Damage;
        /// <summary>Where the hit came from.</summary>
        public float FromX, FromY;

        public byte[] Write()
        {
            var w = new NetDataWriter();
            w.Put(Id);
            w.Put(Damage);
            w.Put(FromX);
            w.Put(FromY);
            return w.CopyData();
        }

        public static ZombieHit Read(byte[] bytes)
        {
            var r = new NetDataReader(bytes);
            return new ZombieHit { Id = r.GetUShort(), Damage = Clean(r.GetFloat()), FromX = r.GetFloat(), FromY = r.GetFloat() };
        }

        /// <summary>No hit is negative, broken or absurd.</summary>
        internal static float Clean(float damage) => float.IsNaN(damage) || float.IsInfinity(damage) ? 0f : Math.Max(0f, Math.Min(10000f, damage));
    }

    /// <summary>The receiver's ruler was hurt (by the sender's zombie or wolf).</summary>
    public struct PlayerHit
    {
        public float Damage;
        public float FromX, FromY;

        public byte[] Write()
        {
            var w = new NetDataWriter();
            w.Put(Damage);
            w.Put(FromX);
            w.Put(FromY);
            return w.CopyData();
        }

        public static PlayerHit Read(byte[] bytes)
        {
            var r = new NetDataReader(bytes);
            return new PlayerHit { Damage = ZombieHit.Clean(r.GetFloat()), FromX = r.GetFloat(), FromY = r.GetFloat() };
        }
    }
}
