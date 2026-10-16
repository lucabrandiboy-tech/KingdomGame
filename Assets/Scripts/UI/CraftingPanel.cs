using KingdomGame.Building;
using KingdomGame.Progression;
using KingdomGame.Villagers;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// The crafting screen (E at a tool bench in a crafting room): better tools for each age and supplies (bandages,
    /// cures for bites), what they cost and what you have. Ordering takes the cost from storage; crafter villagers make
    /// the orders at a tool bench, one at a time. Esc or E closes it.
    /// </summary>
    public sealed class CraftingPanel : MonoBehaviour
    {
        Crafting crafting;
        Kingdom kingdom;
        string message;
        float messageUntil;
        int openedFrame = -1;
        Vector2 scroll;
        GUIStyle title, text, small, button, heading;

        public bool IsOpen { get; private set; }

        public void Setup(Crafting kingdomCrafting, Kingdom owner)
        {
            crafting = kingdomCrafting;
            kingdom = owner;
            if (crafting != null) crafting.Made += item => Say(Ready(item));
        }

        static string Ready(CraftItem item)
        {
            switch (item.kind)
            {
                case CraftKind.Tools:
                    return $"{item.displayName} are ready: villagers gather {Mathf.RoundToInt(item.gathering * 100f)}% and build {Mathf.RoundToInt(item.building * 100f)}% faster";
                case CraftKind.Bandages: return $"{item.amount} bandages are ready (C heals you with one)";
                default: return $"{item.displayName} is ready";
            }
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

        void OnGUI()
        {
            int font = Mathf.Max(12, Screen.height / 55);
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { richText = true };
                heading = new GUIStyle(GUI.skin.label) { richText = true };
                text = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
                small = new GUIStyle(text);
                button = new GUIStyle(GUI.skin.button) { richText = true };
            }
            // The latest news, even with the screen closed.
            if (!IsOpen)
            {
                if (message != null && Time.unscaledTime < messageUntil)
                {
                    text.fontSize = font;
                    var content = new GUIContent($"<b>{message}</b>");
                    var size = GUI.skin.box.CalcSize(content);
                    GUI.Box(new Rect((Screen.width - size.x - font * 2) / 2f, Screen.height * 0.2f, size.x + font * 2, font * 2.2f), content);
                }
                return;
            }
            if (crafting == null) return;
            title.fontSize = Mathf.RoundToInt(font * 1.4f);
            heading.fontSize = Mathf.RoundToInt(font * 1.15f);
            text.fontSize = button.fontSize = font;
            small.fontSize = Mathf.RoundToInt(font * 0.85f);

            float w = Mathf.Min(Screen.width - 40f, font * 50f), h = Mathf.Min(Screen.height - 40f, font * 42f);
            var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none);
            float x = panel.x + font, y = panel.y + font * 0.6f, inner = w - font * 2f;
            GUI.Label(new Rect(x, y, inner, font * 2f), $"<b>Crafting room</b>   <size={font}>E or Esc closes this.</size>", title);
            y += font * 2.2f;

            int crafters = kingdom != null ? kingdom.CountOf(Job.Crafter) : 0;
            string status = crafting.Making != null
                ? $"Making <b>{crafting.Making.displayName}</b>: {Mathf.FloorToInt(crafting.MakingProgress * 100f)}%" +
                  (crafters == 0 ? "   <color=#ff8a80>No crafters: give villagers the Crafter job (V)</color>" : $"   ({crafters} crafter{(crafters == 1 ? "" : "s")})")
                : crafters == 0 ? "Order something below, then give villagers the Crafter job (V): they make it here."
                : "Order something below: your crafters make it here (after any weapons on order at an anvil).";
            GUI.Label(new Rect(x, y, inner, font * 1.6f), status, text);
            y += font * 1.8f;
            string have = $"Tools: villagers gather <b>+{Mathf.RoundToInt(crafting.Gathering * 100f)}%</b> and build <b>+{Mathf.RoundToInt(crafting.Building * 100f)}%</b>.   " +
                          $"Supplies: <b>{crafting.Bandages}</b> bandages, <b>{crafting.Cures}</b> cures.";
            GUI.Label(new Rect(x, y, inner, font * 1.6f), have, text);
            y += font * 1.8f;
            if (message != null && Time.unscaledTime < messageUntil)
                GUI.Label(new Rect(x, y, inner, font * 1.6f), $"<color=#f0d08a>{message}</color>", text);
            y += font * 1.8f;

            var items = crafting.Items;
            float row = font * 3.2f;
            var view = new Rect(x, y, inner, panel.yMax - y - font * 0.6f);
            float cw = inner - font;
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, cw, row * items.Count + font * 5.4f));
            float ry = 0f;
            var research = Research.Current;
            for (int pass = 0; pass < 2; pass++)
            {
                // Tools first, then supplies.
                GUI.Label(new Rect(0, ry, cw, font * 1.8f), pass == 0
                    ? "<b>Tools</b>  <size=" + Mathf.RoundToInt(font * 0.85f) + "><color=#b8ad98>(for good, for every villager; each set adds to the last)</color></size>"
                    : "<b>Supplies</b>", heading);
                ry += font * 2.2f;
                foreach (var item in items)
                {
                    if (item == null || (item.kind == CraftKind.Tools) != (pass == 0)) continue;
                    var age = research != null ? research.AgeAt(item.age) : null;
                    string ageName = age != null ? age.displayName : $"Age {item.age + 1}";
                    string what = item.kind == CraftKind.Tools
                        ? $"gather +{Mathf.RoundToInt(item.gathering * 100f)}%, build +{Mathf.RoundToInt(item.building * 100f)}%"
                        : item.kind == CraftKind.Bandages ? $"{item.amount} bandages, each heals {Mathf.RoundToInt(item.heal)}"
                        : $"{item.amount} cure{(item.amount == 1 ? "" : "s")}";
                    GUI.Label(new Rect(0, ry, cw * 0.62f, font * 1.5f), $"<b>{item.displayName}</b>  <color=#b8ad98>{ageName}: {what}</color>", text);
                    GUI.Label(new Rect(0, ry + font * 1.3f, cw * 0.62f, row - font * 1.3f),
                        $"{item.description}  <color=#b8ad98>{item.CostText()}, {Mathf.CeilToInt(item.craftSeconds)} s of work</color>", small);
                    var b = new Rect(cw * 0.64f, ry + font * 0.4f, cw * 0.34f, font * 2f);
                    int ordered = crafting.Ordered(item);
                    if (crafting.HasTools(item)) GUI.Label(b, "<color=#9be29b><b>You have them</b></color>", text);
                    else if (item.kind == CraftKind.Tools && ordered > 0)
                    {
                        if (GUI.Button(b, crafting.Making == item ? $"Making {Mathf.FloorToInt(crafting.MakingProgress * 100f)}% (cancel)" : "Ordered (cancel)", button))
                            crafting.Cancel(item);
                    }
                    else if (crafting.CanOrder(item, out string why))
                    {
                        // Supplies can be ordered again and again; the last one ordered can be taken back.
                        float half = ordered > 0 ? b.width * 0.62f : b.width;
                        if (GUI.Button(new Rect(b.x, b.y, half, b.height), ordered > 0 ? $"<b>Order more</b> ({ordered} on order)" : "<b>Order</b>", button)) Say(crafting.Order(item));
                        if (ordered > 0 && GUI.Button(new Rect(b.x + half + 4f, b.y, b.width - half - 4f, b.height), "Cancel one", button)) crafting.Cancel(item);
                    }
                    else
                    {
                        GUI.Label(b, $"<color=#ff8a80>{why}</color>{(ordered > 0 ? $"  ({ordered} on order)" : "")}", small);
                    }
                    ry += row;
                }
                ry += font * 0.4f;
            }
            GUI.EndScrollView();
        }
    }
}
