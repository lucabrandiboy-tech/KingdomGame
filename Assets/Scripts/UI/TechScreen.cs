using System;
using System.Text;
using KingdomGame.Progression;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// The research screen (T): one page per age with its techs. Each card says what
    /// it lets you build, what it costs and how long it takes (real time: research
    /// carries on while the game is closed). Click one to research it, or to queue it
    /// behind what's being researched. The game keeps running while it's open.
    /// </summary>
    public sealed class TechScreen : MonoBehaviour
    {
        Research research;
        int page = -1;
        GUIStyle title, text, small, card, button, tab, tabOpen;
        readonly StringBuilder line = new StringBuilder(256);

        public bool IsOpen { get; private set; }

        public void Setup(Research kingdomResearch) => research = kingdomResearch;

        public void Toggle()
        {
            IsOpen = !IsOpen;
            UiFocus.PanelOpen = IsOpen;
            if (IsOpen && research != null) page = research.Age;
        }

        void Update()
        {
            if (research == null || Time.timeScale <= 0f) return;
            if (UiFocus.PanelOpen && !IsOpen) return;   // another panel is open
            if (GameInput.Pressed(KeyCode.T) || (IsOpen && GameInput.Pressed(KeyCode.Escape))) Toggle();
        }

        void OnDisable()
        {
            if (IsOpen) UiFocus.PanelOpen = false;
        }

        /// <summary>Like "2 d 3 h", "4 h 10 min" or "12 min".</summary>
        public static string Duration(double seconds)
        {
            if (seconds < 60) return $"{Math.Max(0, Math.Ceiling(seconds))} s";
            var span = TimeSpan.FromSeconds(seconds);
            if (span.TotalDays >= 1) return $"{(int)span.TotalDays} d {span.Hours} h";
            if (span.TotalHours >= 1) return $"{(int)span.TotalHours} h {span.Minutes} min";
            return $"{(int)Math.Ceiling(span.TotalMinutes)} min";
        }

        void OnGUI()
        {
            if (!IsOpen || research == null || research.AgeCount == 0) return;
            int font = Mathf.Max(12, Screen.height / 55);
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleLeft };
                text = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true, alignment = TextAnchor.UpperLeft };
                small = new GUIStyle(text);
                card = new GUIStyle(GUI.skin.box);
                button = new GUIStyle(GUI.skin.button) { richText = true };
                tab = new GUIStyle(GUI.skin.button) { richText = true };
                tabOpen = new GUIStyle(tab);
                tabOpen.normal.textColor = tabOpen.hover.textColor = new Color(1f, 0.85f, 0.4f);
            }
            title.fontSize = Mathf.RoundToInt(font * 1.5f);
            text.fontSize = button.fontSize = tab.fontSize = tabOpen.fontSize = font;
            small.fontSize = Mathf.RoundToInt(font * 0.85f);
            page = Mathf.Clamp(page, 0, research.AgeCount - 1);

            float w = Mathf.Min(Screen.width - 40f, font * 70f), h = Mathf.Min(Screen.height - 40f, font * 44f);
            var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none);   // twice: darker
            float x = panel.x + font, y = panel.y + font * 0.6f, inner = w - font * 2f;

            var age = research.CurrentAge;
            GUI.Label(new Rect(x, y, inner, font * 2f), $"<b>Research</b>   <size={font}>Your kingdom is in the <b>{age?.displayName}</b>.  T or Esc closes this.</size>", title);
            if (GUI.Button(new Rect(panel.xMax - font * 3f, y, font * 2.2f, font * 1.8f), "X", button)) Toggle();
            y += font * 2.3f;
            GUI.Label(new Rect(x, y, inner, font * 2.6f), Status(), text);
            y += font * 2.8f;

            // A tab per age; ages not reached yet can be looked at too.
            float tabW = (inner - (research.AgeCount - 1) * 4f) / research.AgeCount;
            for (int a = 0; a < research.AgeCount; a++)
            {
                var ac = research.AgeAt(a);
                string name = ac != null ? ac.displayName : $"Age {a + 1}";
                string label = a <= research.Age ? $"<b>{name}</b>" : $"<color=#8a8478>{name}</color>";
                if (GUI.Button(new Rect(x + a * (tabW + 4f), y, tabW, font * 2f), label, a == page ? tabOpen : tab)) page = a;
            }
            y += font * 2.5f;

            var shown = research.AgeAt(page);
            if (shown == null) return;
            if (!string.IsNullOrEmpty(shown.description))
            {
                GUI.Label(new Rect(x, y, inner, font * 2.4f), $"<i>{shown.description}</i>", small);
                y += font * 2.4f;
            }

            // The techs as cards, four to a row.
            var techs = shown.techs;
            int columns = 4, rows = Mathf.Max(1, Mathf.CeilToInt(techs.Length / (float)columns));
            float gap = font * 0.5f;
            float cw = (inner - gap * (columns - 1)) / columns;
            float ch = Mathf.Min(font * 12.5f, (panel.yMax - font - y - gap * (rows - 1)) / rows);
            for (int i = 0; i < techs.Length; i++)
            {
                var t = techs[i];
                if (t == null) continue;
                var r = new Rect(x + (i % columns) * (cw + gap), y + (i / columns) * (ch + gap), cw, ch);
                DrawCard(t, r, font);
            }
        }

        string Status()
        {
            line.Clear();
            switch (research.Mode)
            {
                case Research.Unlocking.Everything:
                    return "<b>Free build:</b> everything in every age is unlocked, so you can try it all.";
                case Research.Unlocking.ByAge:
                    return $"<b>Survival:</b> no research. Your kingdom moves into the next age after every {research.Config.survivalWavesPerAge} waves you survive.";
            }
            var now = research.Researching;
            if (now == null) line.Append("Nothing is being researched. Click a tech to start.");
            else if (research.Waiting != null) line.Append($"<b>{now.displayName}</b>: <color=#ff8a80>{research.Waiting}</color>");
            else line.Append($"Researching <b>{now.displayName}</b>: {Mathf.FloorToInt(research.Progress * 100f)}%, {Duration(research.SecondsLeft)} left (it carries on while the game is closed).");
            if (research.Queue.Count > 1)
            {
                line.Append("\nNext: ");
                for (int i = 1; i < research.Queue.Count; i++) line.Append(i > 1 ? ", " : "").Append(research.Queue[i].displayName);
            }
            if (research.Blueprints > 0)
                line.Append($"\n<color=#f0d08a>Lost blueprints: {research.Blueprints} (from ruins): each halves the next research.</color>");
            if (research.Config.allowCheats) line.Append("   <color=#8a8478>(F8: finish it now, for testing)</color>");
            return line.ToString();
        }

        void DrawCard(TechConfig t, Rect r, int font)
        {
            var state = research.StateOf(t);
            GUI.Box(r, GUIContent.none, card);
            string mark = state == TechState.Done ? "<color=#9be29b>Done</color>"
                : state == TechState.Researching ? "<color=#f0d08a>Researching</color>"
                : state == TechState.Queued ? "<color=#f0d08a>Queued</color>" : "";
            float x = r.x + font * 0.5f, w = r.width - font;
            float y = r.y + font * 0.3f;
            GUI.Label(new Rect(x, y, w, font * 1.6f), $"<b>{t.displayName}</b>  {mark}", text);
            y += font * 1.5f;

            line.Clear();
            if (!string.IsNullOrEmpty(t.description)) line.Append(t.description).Append('\n');
            if (t.unlocks.Length > 0)
            {
                line.Append("<b>Builds:</b> ");
                for (int i = 0; i < t.unlocks.Length; i++)
                    if (t.unlocks[i] != null) line.Append(i > 0 ? ", " : "").Append(t.unlocks[i].displayName);
                line.Append('\n');
            }
            if (t.advancesAge) line.Append("<b>Leads to the next age.</b>\n");
            line.Append($"{Research.Cost(t)},  {Duration(research.SecondsFor(t))}");
            float buttonH = font * 1.9f;
            GUI.Label(new Rect(x, y, w, r.yMax - y - buttonH - font * 0.4f), line.ToString(), small);

            var b = new Rect(x, r.yMax - buttonH - font * 0.3f, w, buttonH);
            switch (state)
            {
                case TechState.Available:
                    if (GUI.Button(b, research.Researching == null ? "<b>Research</b>" : "Queue it", button)) research.Enqueue(t, out _);
                    break;
                case TechState.Researching:
                case TechState.Queued:
                    if (GUI.Button(b, "Cancel", button)) research.Dequeue(t);
                    break;
                case TechState.Locked:
                    research.CanQueue(t, out string why);
                    GUI.Label(b, $"<color=#a09888>{why}</color>", small);
                    break;
            }
        }
    }
}
