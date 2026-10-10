using System;
using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Economy;
using KingdomGame.Progression;
using UnityEngine;

namespace KingdomGame.Combat
{
    /// <summary>
    /// The kingdom's weapons: which the ruler has, the order book (paid for from storage when
    /// ordered, made one at a time by crafter villagers working at an anvil), and which one
    /// is in the ruler's hand. A weapon can only be ordered once the kingdom has reached its age.
    /// In free build every weapon is there from the start.
    /// </summary>
    public sealed class Armory : MonoBehaviour
    {
        sealed class WorkOrder
        {
            public ArmsItem Item;
            public float Work;
        }

        ArmoryConfig config;
        Kingdom kingdom;
        readonly HashSet<ArmsItem> owned = new HashSet<ArmsItem>();
        readonly List<WorkOrder> orders = new List<WorkOrder>();
        ArmsItem selected;

        public IReadOnlyList<ArmsItem> Items => config != null ? config.items : Array.Empty<ArmsItem>();
        /// <summary>Something was made, ordered or picked up.</summary>
        public event Action Changed;
        /// <summary>A weapon was finished (for a message).</summary>
        public event Action<ArmsItem> Made;

        public void Setup(ArmoryConfig armory, Kingdom owner, bool everything)
        {
            config = armory;
            kingdom = owner;
            foreach (var item in Items)
                if (item != null && (everything || item.startsOwned)) owned.Add(item);
            selected = Best();
        }

        public bool Owns(ArmsItem item) => item != null && owned.Contains(item);
        public bool HasWork => orders.Count > 0;
        public ArmsItem Making => orders.Count > 0 ? orders[0].Item : null;
        public float MakingProgress => orders.Count > 0 ? Mathf.Clamp01(orders[0].Work / Mathf.Max(1f, orders[0].Item.craftSeconds)) : 0f;
        public bool IsOrdered(ArmsItem item) => orders.Exists(o => o.Item == item);

        /// <summary>The weapon in the ruler's hand (left mouse button), or null.</summary>
        public ArmsItem Selected => selected != null && owned.Contains(selected) ? selected : (selected = Best());
        /// <summary>The heavy weapon fired with the right mouse button (the best one owned), or null.</summary>
        public WeaponConfig Heavy => BestOf(i => i.heavy && i.weapon != null)?.weapon;
        /// <summary>What G throws (the best explosive owned), or null.</summary>
        public ExplosiveConfig Explosive => BestOf(i => i.explosive != null)?.explosive;

        /// <summary>The weapons that go in the hand, in order (Q and 1-9 pick from these).</summary>
        public List<ArmsItem> HandWeapons()
        {
            var list = new List<ArmsItem>();
            foreach (var item in Items)
                if (item != null && owned.Contains(item) && item.weapon != null && !item.heavy) list.Add(item);
            return list;
        }

        ArmsItem Best() => BestOf(i => i.weapon != null && !i.heavy);

        // The latest one (the highest age, later in the list on a tie) that's owned and fits.
        ArmsItem BestOf(Predicate<ArmsItem> fits)
        {
            ArmsItem best = null;
            foreach (var item in Items)
                if (item != null && owned.Contains(item) && fits(item) && (best == null || item.age >= best.age)) best = item;
            return best;
        }

        /// <summary>Takes the <paramref name="n"/>th hand weapon (0 = the first). False if there's no such one.</summary>
        public bool Select(int n)
        {
            var list = HandWeapons();
            if (n < 0 || n >= list.Count || list[n] == selected) return false;
            selected = list[n];
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes the next hand weapon.</summary>
        public void SelectNext()
        {
            var list = HandWeapons();
            if (list.Count < 2) return;
            selected = list[(list.IndexOf(Selected) + 1) % list.Count];
            Changed?.Invoke();
        }

        /// <summary>Whether <paramref name="item"/> can be ordered now, and if not, why.</summary>
        public bool CanOrder(ArmsItem item, out string reason)
        {
            reason = null;
            if (item == null) reason = "Nothing to make";
            else if (owned.Contains(item)) reason = "You have it";
            else if (IsOrdered(item)) reason = "Already being made";
            else
            {
                var research = Research.Current;
                if (research != null && research.Mode != Research.Unlocking.Everything && research.Age < item.age)
                {
                    var age = research.AgeAt(item.age);
                    reason = $"Needs the {(age != null ? age.displayName : "next age")}";
                }
                else if (kingdom != null)
                    for (int k = 0; k < Inventory.TypeCount; k++)
                        if (kingdom.Available((ResourceType)k) < item.Cost((ResourceType)k)) { reason = $"Needs {item.CostText()} in storage"; break; }
            }
            return reason == null;
        }

        /// <summary>Orders a weapon: its cost is taken from storage now, and crafters make it. Returns a message.</summary>
        public string Order(ArmsItem item)
        {
            if (!CanOrder(item, out string reason)) return reason;
            var costs = new int[Inventory.TypeCount];
            for (int k = 0; k < costs.Length; k++) costs[k] = item.Cost((ResourceType)k);
            if (kingdom != null && !kingdom.TrySpend(costs)) return $"Needs {item.CostText()} in storage";
            orders.Add(new WorkOrder { Item = item });
            Changed?.Invoke();
            return $"Ordered: {item.displayName}. Crafters make it at an anvil";
        }

        /// <summary>Takes an order back (what was paid goes back to storage).</summary>
        public void Cancel(ArmsItem item)
        {
            int i = orders.FindIndex(o => o.Item == item);
            if (i < 0) return;
            orders.RemoveAt(i);
            if (kingdom != null)
            {
                var at = kingdom.Throne != null ? kingdom.Throne.Center : Vector2.zero;
                for (int k = 0; k < Inventory.TypeCount; k++) kingdom.Store((ResourceType)k, item.Cost((ResourceType)k), at);
            }
            Changed?.Invoke();
        }

        /// <summary>A crafter worked on the first order for <paramref name="seconds"/>.</summary>
        public void Work(float seconds)
        {
            if (orders.Count == 0) return;
            var order = orders[0];
            order.Work += seconds;
            if (order.Work < order.Item.craftSeconds) return;
            orders.RemoveAt(0);
            owned.Add(order.Item);
            // A better hand weapon goes straight into the hand.
            if (order.Item.weapon != null && !order.Item.heavy && (Selected == null || order.Item.age >= Selected.age)) selected = order.Item;
            Made?.Invoke(order.Item);
            Changed?.Invoke();
        }

        // ------------------------------------------------------------ saving --

        /// <summary>What a save needs to put the armory back.</summary>
        [Serializable]
        public sealed class Saved
        {
            public List<string> owned = new List<string>();
            public List<string> orders = new List<string>();
            public float work;
            public string selected;
        }

        public Saved Save()
        {
            var s = new Saved { selected = Selected != null ? Selected.displayName : null, work = orders.Count > 0 ? orders[0].Work : 0f };
            foreach (var item in Items) if (item != null && owned.Contains(item)) s.owned.Add(item.displayName);
            foreach (var o in orders) s.orders.Add(o.Item.displayName);
            return s;
        }

        public void Load(Saved s)
        {
            if (s == null) return;
            ArmsItem Find(string name)
            {
                foreach (var item in Items) if (item != null && item.displayName == name) return item;
                return null;
            }
            foreach (var n in s.owned) { var item = Find(n); if (item != null) owned.Add(item); }
            orders.Clear();
            foreach (var n in s.orders) { var item = Find(n); if (item != null && !owned.Contains(item)) orders.Add(new WorkOrder { Item = item }); }
            if (orders.Count > 0) orders[0].Work = s.work;
            var chosen = Find(s.selected);
            if (chosen != null && owned.Contains(chosen)) selected = chosen;
            Changed?.Invoke();
        }
    }
}
