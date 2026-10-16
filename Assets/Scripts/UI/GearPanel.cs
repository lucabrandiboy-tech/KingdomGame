using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Combat;
using KingdomGame.Progression;
using KingdomGame.Villagers;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// The gear screen (E at a weapon rack or armor stand in an armory room): armor for the ruler, and weapons and
    /// armor for the soldiers, one at a time or all at once. Paid for from storage and handed out at once (a soldier's
    /// weapon is a copy of one you have, at part of its cost). Esc or E closes it.
    /// </summary>
    public sealed class GearPanel : MonoBehaviour
    {
        Armory armory;
        Kingdom kingdom;
        string message;
        float messageUntil;
        int openedFrame = -1;
        int chosen = -1;   // which soldier (-1: all of them)
        Vector2 scroll;
        GUIStyle title, text, small, button, heading;
        readonly List<Villager> soldiers = new List<Villager>();

        public bool IsOpen { get; private set; }

        public void Setup(Armory kingdomArmory, Kingdom owner)
        {
            armory = kingdomArmory;
            kingdom = owner;
        }

        public void Open()
        {
            if (IsOpen || UiFocus.PanelOpen) return;
            IsOpen = true;
            openedFrame = Time.frameCount;
            UiFocus.PanelOpen = true;
        }

        void Close()
        {
            IsOpen = false;
            UiFocus.PanelOpen = false;
        }

        void OnDestroy()
        {
            if (IsOpen) UiFocus.PanelOpen = false;
        }

        void Say(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 4f;
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;
            if (GameInput.Pressed(KeyCode.Escape) || GameInput.Pressed(KeyCode.E)) Close();
        }

        // The soldiers it hands gear to: the one chosen, or all of them.
        List<Villager> Targets()
        {
            var list = new List<Villager>();
            if (chosen >= 0 && chosen < soldiers.Count) list.Add(soldiers[chosen]);
            else list.AddRange(soldiers);
            return list;
        }

        static string AgeName(int age)
        {
            var research = Research.Current;
            var a = research != null ? research.AgeAt(age) : null;
            return a != null ? a.displayName : $"Age {age + 1}";
        }

        void OnGUI()
        {
            if (!IsOpen || armory == null || kingdom == null) return;
            int font = Mathf.Max(12, Screen.height / 55);
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { richText = true };
                heading = new GUIStyle(GUI.skin.label) { richText = true };
                text = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
                small = new GUIStyle(text);
                button = new GUIStyle(GUI.skin.button) { richText = true };
            }
            title.fontSize = Mathf.RoundToInt(font * 1.4f);
            heading.fontSize = Mathf.RoundToInt(font * 1.15f);
            text.fontSize = button.fontSize = font;
            small.fontSize = Mathf.RoundToInt(font * 0.85f);

            soldiers.Clear();
            foreach (var v in kingdom.Villagers)
                if (v != null && v.IsSoldier && v.Health != null && !v.Health.Dead) soldiers.Add(v);
            if (chosen >= soldiers.Count) chosen = -1;

            float w = Mathf.Min(Screen.width - 40f, font * 52f), h = Mathf.Min(Screen.height - 40f, font * 44f);
            var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none);
            float x = panel.x + font, y = panel.y + font * 0.6f, inner = w - font * 2f;
            GUI.Label(new Rect(x, y, inner, font * 2f), $"<b>Armory: gear up</b>   <size={font}>E or Esc closes this.</size>", title);
            y += font * 2.2f;
            string line = message != null && Time.unscaledTime < messageUntil
                ? $"<color=#f0d08a>{message}</color>"
                : "Paid for from storage, handed out at once. New weapons are made at an anvil first (E there); soldiers get copies.";
            GUI.Label(new Rect(x, y, inner, font * 1.6f), line, small);
            y += font * 1.8f;

            var kinds = armory.ArmorKinds;
            var weapons = armory.HandWeapons();
            float row = font * 2.5f;
            float contentHeight = font * 2f + row * kinds.Count + font * 6.5f + (soldiers.Count > 0 ? font * 2f + row * (weapons.Count + kinds.Count) + font * 2f : font * 3f);
            var view = new Rect(x, y, inner, panel.yMax - y - font * 0.6f);
            float cw = inner - font;
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, cw, contentHeight));
            float ry = 0f;

            // ---- You.
            var worn = armory.RulerArmor;
            GUI.Label(new Rect(0, ry, cw, font * 1.8f), $"<b>You</b>   <size={font}>wearing: {(worn != null ? $"{worn.displayName} ({Stats(worn)})" : "no armor")}</size>", heading);
            ry += font * 2f;
            foreach (var a in kinds)
            {
                if (a == null) continue;
                ArmorRow(a, ry, cw, font, row, () =>
                {
                    if (worn == a) return ("<color=#9be29b><b>Wearing it</b></color>", false);
                    return armory.CanBuy(a, 1, out string why) ? ("<b>Wear it</b>", true) : ($"<color=#ff8a80>{why}</color>", false);
                }, () =>
                {
                    if (!armory.BuyArmor(a, 1)) { Say($"Needs {a.CostText()} in storage"); return; }
                    armory.Wear(a);
                    Say($"You put on the {a.displayName.ToLowerInvariant()}");
                });
                ry += row;
            }
            ry += font * 0.6f;

            // ---- Soldiers.
            GUI.Label(new Rect(0, ry, cw, font * 1.8f), $"<b>Soldiers: {soldiers.Count}</b>", heading);
            ry += font * 2f;
            if (soldiers.Count == 0)
            {
                GUI.Label(new Rect(0, ry, cw, font * 3f),
                    "No soldiers yet. Give villagers the Soldier job (V): they fight zombies and wolves, and get their weapon and armor here.", text);
                GUI.EndScrollView();
                return;
            }
            // Who gets the gear: all of them, or one (the arrows go through them).
            if (GUI.Button(new Rect(0, ry, font * 2.4f, font * 1.7f), "<", button)) chosen = chosen <= -1 ? soldiers.Count - 1 : chosen - 1;
            if (GUI.Button(new Rect(font * 2.6f, ry, font * 2.4f, font * 1.7f), ">", button)) chosen = chosen >= soldiers.Count - 1 ? -1 : chosen + 1;
            string who = chosen < 0
                ? $"<b>All {soldiers.Count} soldiers</b> (the arrows pick one)"
                : $"<b>{Who(soldiers[chosen], chosen)}</b>: {soldiers[chosen].RankName.ToLowerInvariant()}, {(soldiers[chosen].Weapon != null ? soldiers[chosen].Weapon.displayName.ToLowerInvariant() : "no weapon")}, " +
                  $"{(soldiers[chosen].Armor != null ? soldiers[chosen].Armor.displayName.ToLowerInvariant() : "no armor")}, " +
                  $"{Mathf.CeilToInt(soldiers[chosen].Health.Current)}/{Mathf.CeilToInt(soldiers[chosen].Health.Max)} health";
            GUI.Label(new Rect(font * 5.4f, ry, cw - font * 5.4f, font * 1.8f), who, text);
            ry += font * 2.2f;
            var targets = Targets();

            GUI.Label(new Rect(0, ry, cw, font * 1.6f), "<b>Weapons</b>  <color=#b8ad98>(copies of the ones you have)</color>", text);
            ry += font * 1.8f;
            foreach (var item in weapons)
            {
                int need = 0;
                foreach (var v in targets) if (v.Weapon != item) need++;
                var wpn = item.weapon;
                string kind = wpn != null && wpn.melee ? "close up" : $"range {Mathf.RoundToInt(wpn != null ? wpn.range : 0f)}";
                GUI.Label(new Rect(0, ry, cw * 0.62f, font * 1.4f), $"<b>{item.displayName}</b>  <color=#b8ad98>{kind}, {Mathf.RoundToInt(wpn != null ? wpn.hitDamage : 0f)} a hit</color>", text);
                GUI.Label(new Rect(0, ry + font * 1.2f, cw * 0.62f, row - font * 1.2f), $"<color=#b8ad98>{(need > 0 ? armory.CopyCostText(item, need) : "")}</color>", small);
                var b = new Rect(cw * 0.64f, ry + font * 0.3f, cw * 0.34f, font * 1.8f);
                if (need == 0) GUI.Label(b, targets.Count == 1 ? "<color=#9be29b><b>Has it</b></color>" : "<color=#9be29b><b>They all have it</b></color>", text);
                else if (armory.CanCopy(item, need, out string why))
                {
                    if (GUI.Button(b, need == 1 ? "<b>Give it</b>" : $"<b>Give it to {need}</b>", button))
                    {
                        if (armory.BuyCopies(item, need))
                        {
                            foreach (var v in targets) if (v.Weapon != item) v.Arm(item);
                            Say(need == 1 ? $"Handed out a {item.displayName.ToLowerInvariant()}" : $"Handed out {need} of the {item.displayName.ToLowerInvariant()}");
                        }
                    }
                }
                else GUI.Label(b, $"<color=#ff8a80>{why}</color>", small);
                ry += row;
            }
            ry += font * 0.4f;

            GUI.Label(new Rect(0, ry, cw, font * 1.6f), "<b>Armor</b>  <color=#b8ad98>(their old armor is thrown away)</color>", text);
            ry += font * 1.8f;
            foreach (var a in kinds)
            {
                if (a == null) continue;
                int need = 0;
                foreach (var v in targets) if (v.Armor != a) need++;
                ArmorRow(a, ry, cw, font, row, () =>
                {
                    if (need == 0) return (targets.Count == 1 ? "<color=#9be29b><b>Wearing it</b></color>" : "<color=#9be29b><b>They all wear it</b></color>", false);
                    return armory.CanBuy(a, need, out string why) ? (need == 1 ? "<b>Give it</b>" : $"<b>Give it to {need}</b>", true) : ($"<color=#ff8a80>{why}</color>", false);
                }, () =>
                {
                    if (!armory.BuyArmor(a, need)) { Say($"Needs {a.CostText(need)} in storage"); return; }
                    foreach (var v in targets) if (v.Armor != a) v.Wear(a);
                    Say(need == 1 ? $"Handed out the {a.displayName.ToLowerInvariant()}" : $"Handed out {need} suits of {a.displayName.ToLowerInvariant()}");
                }, need);
                ry += row;
            }
            GUI.EndScrollView();
        }

        // One kind of armor: its name, age and numbers, its cost, and a button (or why not).
        void ArmorRow(ArmorItem a, float ry, float cw, int font, float row, System.Func<(string label, bool button)> state, System.Action buy, int times = 1)
        {
            GUI.Label(new Rect(0, ry, cw * 0.62f, font * 1.4f), $"<b>{a.displayName}</b>  <color=#b8ad98>{AgeName(a.age)}: {Stats(a)}</color>", text);
            GUI.Label(new Rect(0, ry + font * 1.2f, cw * 0.62f, row - font * 1.2f), $"<color=#b8ad98>{(times > 0 ? a.CostText(times) : "")}</color>", small);
            var b = new Rect(cw * 0.64f, ry + font * 0.3f, cw * 0.34f, font * 1.8f);
            var (label, isButton) = state();
            if (isButton)
            {
                if (GUI.Button(b, label, button)) buy();
            }
            else GUI.Label(b, label, small);
        }

        static string Stats(ArmorItem a) => $"-{a.armor:0.#} a hit, +{Mathf.RoundToInt(a.health)} health";

        static string Who(Villager v, int index) => string.IsNullOrEmpty(v.Name) ? $"Soldier {index + 1}" : v.Name;
    }
}
