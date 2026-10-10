using KingdomGame.Building;
using KingdomGame.Combat;
using KingdomGame.Progression;
using KingdomGame.Villagers;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// The weapons screen (E at an anvil): every weapon through the ages, what it costs and
    /// whether you have it. Ordering one takes its cost from storage; crafter villagers make
    /// it at an anvil, one at a time. Esc or E closes it.
    /// </summary>
    public sealed class ArmoryPanel : MonoBehaviour
    {
        Armory armory;
        Kingdom kingdom;
        string message;
        float messageUntil;
        int openedFrame = -1;
        Vector2 scroll;
        GUIStyle title, text, small, button;

        public bool IsOpen { get; private set; }

        public void Setup(Armory kingdomArmory, Kingdom owner)
        {
            armory = kingdomArmory;
            kingdom = owner;
            if (armory != null) armory.Made += item => Say($"{item.displayName} is ready: it's in your hand (Q switches)");
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
            if (armory == null) return;
            title.fontSize = Mathf.RoundToInt(font * 1.4f);
            text.fontSize = button.fontSize = font;
            small.fontSize = Mathf.RoundToInt(font * 0.85f);

            float w = Mathf.Min(Screen.width - 40f, font * 48f), h = Mathf.Min(Screen.height - 40f, font * 40f);
            var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none);
            float x = panel.x + font, y = panel.y + font * 0.6f, inner = w - font * 2f;
            GUI.Label(new Rect(x, y, inner, font * 2f), $"<b>Weapons</b>   <size={font}>E or Esc closes this.   In hand: {(armory.Selected != null ? armory.Selected.displayName : "nothing")} (Q or 1-9 switches)</size>", title);
            y += font * 2.2f;

            int crafters = kingdom != null ? kingdom.CountOf(Job.Crafter) : 0;
            string status = armory.Making != null
                ? $"Making <b>{armory.Making.displayName}</b>: {Mathf.FloorToInt(armory.MakingProgress * 100f)}%" +
                  (crafters == 0 ? "   <color=#ff8a80>No crafters: give villagers the Crafter job (V)</color>" : $"   ({crafters} crafter{(crafters == 1 ? "" : "s")})")
                : crafters == 0 ? "Order a weapon below, then give villagers the Crafter job (V): they make it at an anvil." : "Order a weapon below: your crafters make it at an anvil.";
            GUI.Label(new Rect(x, y, inner, font * 1.6f), status, text);
            y += font * 1.8f;
            if (message != null && Time.unscaledTime < messageUntil)
            {
                GUI.Label(new Rect(x, y, inner, font * 1.6f), $"<color=#f0d08a>{message}</color>", text);
            }
            y += font * 1.8f;

            var research = Research.Current;
            var items = armory.Items;
            float row = font * 3.4f;
            var view = new Rect(x, y, inner, panel.yMax - y - font * 0.6f);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, inner - font, row * items.Count));
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                float ry = i * row;
                var age = research != null ? research.AgeAt(item.age) : null;
                string ageName = age != null ? age.displayName : $"Age {item.age + 1}";
                string kind = item.heavy ? "right mouse button" : item.explosive != null ? "G throws it" : item.weapon != null && item.weapon.melee ? "close up" : "left mouse button";
                GUI.Label(new Rect(0, ry, inner * 0.62f, font * 1.5f), $"<b>{item.displayName}</b>  <color=#b8ad98>{ageName}, {kind}</color>", text);
                GUI.Label(new Rect(0, ry + font * 1.3f, inner * 0.62f, row - font * 1.3f),
                    $"{item.description}  <color=#b8ad98>{item.CostText()}, {Mathf.CeilToInt(item.craftSeconds)} s of work</color>", small);
                var b = new Rect(inner * 0.66f, ry + font * 0.4f, inner * 0.3f, font * 2f);
                if (armory.Owns(item)) GUI.Label(b, "<color=#9be29b><b>You have it</b></color>", text);
                else if (armory.IsOrdered(item))
                {
                    if (GUI.Button(b, armory.Making == item ? $"Making {Mathf.FloorToInt(armory.MakingProgress * 100f)}% (cancel)" : "Ordered (cancel)", button)) armory.Cancel(item);
                }
                else if (armory.CanOrder(item, out string why))
                {
                    if (GUI.Button(b, "<b>Order it</b>", button)) Say(armory.Order(item));
                }
                else GUI.Label(b, $"<color=#ff8a80>{why}</color>", small);
            }
            GUI.EndScrollView();
        }
    }
}
