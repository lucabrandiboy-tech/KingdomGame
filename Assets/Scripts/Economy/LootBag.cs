using System.Collections.Generic;
using KingdomGame.Art;
using UnityEngine;

namespace KingdomGame.Economy
{
    /// <summary>
    /// A bag of resources on the ground: dropped by some zombies, and by the ruler
    /// when they fall. Walk over it to put what's inside in your backpack. It
    /// disappears after a few minutes.
    /// </summary>
    public sealed class LootBag : MonoBehaviour
    {
        const float Lifetime = 180f, FadeSeconds = 5f;

        static readonly List<LootBag> all = new List<LootBag>();
        /// <summary>Every bag on the ground.</summary>
        public static IReadOnlyList<LootBag> All => all;

        readonly int[] amounts = new int[Inventory.TypeCount];
        SpriteRenderer look;
        float born;

        public Vector2 Position => transform.position;
        public int Total
        {
            get { int t = 0; foreach (int a in amounts) t += a; return t; }
        }

        /// <summary>Drops a bag holding these resources (nothing is dropped if it would be empty).</summary>
        public static LootBag Drop(Vector2 at, int wood, int stone, int food)
        {
            if (wood + stone + food <= 0) return null;
            var go = new GameObject("Loot");
            go.transform.position = at;
            var bag = go.AddComponent<LootBag>();
            bag.amounts[(int)ResourceType.Wood] = Mathf.Max(0, wood);
            bag.amounts[(int)ResourceType.Stone] = Mathf.Max(0, stone);
            bag.amounts[(int)ResourceType.Food] = Mathf.Max(0, food);
            return bag;
        }

        /// <summary>How much of <paramref name="type"/> is in it.</summary>
        public int Amount(ResourceType type) => amounts[(int)type];

        /// <summary>Drops a bag holding <paramref name="counts"/> (one per <see cref="ResourceType"/>; loading a saved game).</summary>
        public static LootBag Drop(Vector2 at, int[] counts)
        {
            if (counts == null) return null;
            int total = 0;
            foreach (int n in counts) total += Mathf.Max(0, n);
            if (total <= 0) return null;
            var go = new GameObject("Loot");
            go.transform.position = at;
            var bag = go.AddComponent<LootBag>();
            for (int i = 0; i < bag.amounts.Length && i < counts.Length; i++) bag.amounts[i] = Mathf.Max(0, counts[i]);
            return bag;
        }

        /// <summary>Drops a bag with everything in <paramref name="from"/>, emptying it.</summary>
        public static LootBag DropAll(Vector2 at, Inventory from)
        {
            if (from == null || from.Total <= 0) return null;
            var go = new GameObject("Loot");
            go.transform.position = at;
            var bag = go.AddComponent<LootBag>();
            for (int i = 0; i < Inventory.TypeCount; i++)
            {
                var type = (ResourceType)i;
                int n = from.Get(type);
                from.TryRemove(type, n);
                bag.amounts[i] = n;
            }
            return bag;
        }

        void Awake()
        {
            born = Time.time;
            all.Add(this);
            Look.AddShadow(transform, ArtSprites.Get(ArtKeys.Shadow(ArtKeys.LootBag)), 0.06f);
            look = gameObject.AddComponent<SpriteRenderer>();
            look.sprite = ArtSprites.Get(ArtKeys.LootBag);
            look.sortingOrder = SortOrder.Loot;
            transform.rotation = Quaternion.identity;
        }

        void OnDestroy() => all.Remove(this);

        /// <summary>Moves what fits into <paramref name="backpack"/>; returns how much. The bag goes away once it's empty.</summary>
        public int TakeInto(Inventory backpack)
        {
            int taken = 0;
            for (int i = 0; i < amounts.Length; i++)
            {
                if (amounts[i] <= 0) continue;
                int put = backpack.Add((ResourceType)i, amounts[i]);
                amounts[i] -= put;
                taken += put;
            }
            if (Total <= 0) Destroy(gameObject);
            return taken;
        }

        /// <summary>What's inside, like "6 wood, 2 stone".</summary>
        public string Contents()
        {
            var parts = new List<string>(3);
            for (int i = 0; i < amounts.Length; i++)
                if (amounts[i] > 0) parts.Add($"{amounts[i]} {((ResourceType)i).ToString().ToLowerInvariant()}");
            return string.Join(", ", parts);
        }

        void Update()
        {
            float left = born + Lifetime - Time.time;
            if (left <= 0f) Destroy(gameObject);
            else if (left < FadeSeconds) look.color = new Color(1f, 1f, 1f, left / FadeSeconds);
        }
    }
}
