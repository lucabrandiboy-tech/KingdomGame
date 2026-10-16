using System;
using System.Collections.Generic;
using KingdomGame.Economy;
using KingdomGame.Progression;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// The crafting room's order book: things ordered at a tool bench (paid for from storage when ordered), made one at
    /// a time by crafter villagers at a tool bench in a crafting room. Tools are kept for good and add up (villagers
    /// gather and build faster); bandages and cures go into the kingdom's supplies until they're used.
    /// </summary>
    public sealed class Crafting : MonoBehaviour
    {
        sealed class WorkOrder
        {
            public CraftItem Item;
            public float Work;
        }

        CraftingConfig config;
        Kingdom kingdom;
        readonly HashSet<CraftItem> tools = new HashSet<CraftItem>();
        readonly List<WorkOrder> orders = new List<WorkOrder>();
        int bandages, cures;
        float gathering, building;

        public IReadOnlyList<CraftItem> Items => config != null && config.items != null ? config.items : Array.Empty<CraftItem>();
        /// <summary>Something was made (for a message).</summary>
        public event Action<CraftItem> Made;

        public void Setup(CraftingConfig crafting, Kingdom owner)
        {
            config = crafting;
            kingdom = owner;
        }

        public bool HasWork => orders.Count > 0;
        public CraftItem Making => orders.Count > 0 ? orders[0].Item : null;
        public float MakingProgress => orders.Count > 0 ? Mathf.Clamp01(orders[0].Work / Mathf.Max(1f, orders[0].Item.craftSeconds)) : 0f;
        public int Ordered(CraftItem item) => orders.FindAll(o => o.Item == item).Count;
        public bool HasTools(CraftItem item) => item != null && tools.Contains(item);

        /// <summary>How much faster villagers gather with the tools made so far (0.16 = 16%).</summary>
        public float Gathering => gathering;
        /// <summary>How much faster builders build with the tools made so far.</summary>
        public float Building => building;
        public int Bandages => bandages;
        public int Cures => cures;

        /// <summary>Hit points a bandage heals (the first bandage item's).</summary>
        public float BandageHeal
        {
            get
            {
                foreach (var item in Items) if (item != null && item.kind == CraftKind.Bandages) return item.heal;
                return 0f;
            }
        }

        /// <summary>Takes a bandage from the supplies. False if there are none.</summary>
        public bool UseBandage()
        {
            if (bandages <= 0) return false;
            bandages--;
            return true;
        }

        /// <summary>Takes a cure for a bite from the supplies. False if there are none.</summary>
        public bool UseCure()
        {
            if (cures <= 0) return false;
            cures--;
            return true;
        }

        /// <summary>Whether <paramref name="item"/> can be ordered now, and if not, why.</summary>
        public bool CanOrder(CraftItem item, out string reason)
        {
            reason = null;
            if (item == null) reason = "Nothing to make";
            else if (item.kind == CraftKind.Tools && tools.Contains(item)) reason = "You have them";
            else if (item.kind == CraftKind.Tools && Ordered(item) > 0) reason = "Already being made";
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

        /// <summary>Orders something: its cost is taken from storage now, and crafters make it. Returns a message.</summary>
        public string Order(CraftItem item)
        {
            if (!CanOrder(item, out string reason)) return reason;
            var costs = new int[Inventory.TypeCount];
            for (int k = 0; k < costs.Length; k++) costs[k] = item.Cost((ResourceType)k);
            if (kingdom != null && !kingdom.TrySpend(costs)) return $"Needs {item.CostText()} in storage";
            orders.Add(new WorkOrder { Item = item });
            return $"Ordered: {item.displayName}. Crafters make it at a tool bench in a crafting room";
        }

        /// <summary>Takes back the last order of <paramref name="item"/> (what was paid goes back to storage).</summary>
        public void Cancel(CraftItem item)
        {
            int i = orders.FindLastIndex(o => o.Item == item);
            if (i < 0) return;
            orders.RemoveAt(i);
            if (kingdom != null)
            {
                var at = kingdom.Throne != null ? kingdom.Throne.Center : Vector2.zero;
                for (int k = 0; k < Inventory.TypeCount; k++) kingdom.Store((ResourceType)k, item.Cost((ResourceType)k), at);
            }
        }

        /// <summary>A crafter worked on the first order for <paramref name="seconds"/>.</summary>
        public void Work(float seconds)
        {
            if (orders.Count == 0) return;
            var order = orders[0];
            order.Work += seconds;
            if (order.Work < order.Item.craftSeconds) return;
            orders.RemoveAt(0);
            Add(order.Item);
            Made?.Invoke(order.Item);
        }

        void Add(CraftItem item)
        {
            switch (item.kind)
            {
                case CraftKind.Tools: tools.Add(item); break;
                case CraftKind.Bandages: bandages += Mathf.Max(1, item.amount); break;
                case CraftKind.Cure: cures += Mathf.Max(1, item.amount); break;
            }
            Recount();
        }

        // What the tools made so far add up to.
        void Recount()
        {
            gathering = building = 0f;
            foreach (var t in tools)
            {
                gathering += t.gathering;
                building += t.building;
            }
        }

        /// <summary>Everything on order made at once (the cheat menu).</summary>
        public void CheatFinish()
        {
            while (orders.Count > 0) Work(orders[0].Item.craftSeconds + 1f);
        }

        // ------------------------------------------------------------ saving --

        /// <summary>What a save needs to put the crafting room back.</summary>
        [Serializable]
        public sealed class Saved
        {
            public List<string> tools = new List<string>();
            public List<string> orders = new List<string>();
            public float work;
            public int bandages, cures;
        }

        public Saved Save()
        {
            var s = new Saved { work = orders.Count > 0 ? orders[0].Work : 0f, bandages = bandages, cures = cures };
            foreach (var t in tools) s.tools.Add(t.displayName);
            foreach (var o in orders) s.orders.Add(o.Item.displayName);
            return s;
        }

        public void Load(Saved s)
        {
            if (s == null) return;
            CraftItem Find(string name)
            {
                foreach (var item in Items) if (item != null && item.displayName == name) return item;
                return null;
            }
            tools.Clear();
            foreach (var n in s.tools) { var item = Find(n); if (item != null && item.kind == CraftKind.Tools) tools.Add(item); }
            orders.Clear();
            foreach (var n in s.orders) { var item = Find(n); if (item != null) orders.Add(new WorkOrder { Item = item }); }
            if (orders.Count > 0) orders[0].Work = s.work;
            bandages = Mathf.Max(0, s.bandages);
            cures = Mathf.Max(0, s.cures);
            Recount();
        }
    }
}
