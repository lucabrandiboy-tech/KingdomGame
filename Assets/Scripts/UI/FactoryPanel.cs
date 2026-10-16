using System.Text;
using KingdomGame.Automation;
using KingdomGame.Building;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// E at a factory machine (Building/Factory.cs): an assembler's recipe to choose, a furnace's or drill's state, or
    /// what an arm moves (anything, or one item: a filter). It shows what's inside and why it's stopped. Esc or E closes it.
    /// </summary>
    public sealed class FactoryPanel : MonoBehaviour
    {
        Factory factory;
        Machine machine;
        int openedFrame = -1;
        Vector2 scroll;
        GUIStyle title, text, small, button;
        readonly StringBuilder line = new StringBuilder(256);

        public bool IsOpen { get; private set; }

        public void Setup(Factory kingdomFactory) => factory = kingdomFactory;

        public void Open(Machine m)
        {
            if (IsOpen || UiFocus.PanelOpen || m == null || factory == null) return;
            machine = m;
            IsOpen = true;
            openedFrame = Time.frameCount;
            scroll = Vector2.zero;
            UiFocus.PanelOpen = true;
        }

        void Close()
        {
            IsOpen = false;
            machine = null;
            UiFocus.PanelOpen = false;
        }

        void OnDestroy()
        {
            if (IsOpen) UiFocus.PanelOpen = false;
        }

        void Update()
        {
            if (!IsOpen) return;
            if (machine == null) { Close(); return; }   // (torn down while open)
            if (Time.frameCount == openedFrame) return;
            if (GameInput.Pressed(KeyCode.Escape) || GameInput.Pressed(KeyCode.E)) Close();
        }

        static string Name(ResourceType t) => ResourceText.Name(t);

        void OnGUI()
        {
            if (!IsOpen || machine == null || factory == null) return;
            int font = Mathf.Max(12, Screen.height / 55);
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { richText = true };
                text = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
                small = new GUIStyle(text);
                button = new GUIStyle(GUI.skin.button) { richText = true, wordWrap = true };
            }
            title.fontSize = Mathf.RoundToInt(font * 1.4f);
            text.fontSize = button.fontSize = font;
            small.fontSize = Mathf.RoundToInt(font * 0.85f);

            float w = Mathf.Min(Screen.width - 40f, font * 46f), h = Mathf.Min(Screen.height - 40f, font * 38f);
            var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none);
            float x = panel.x + font, y = panel.y + font * 0.6f, inner = w - font * 2f;
            var furniture = machine.GetComponent<Furniture>();
            string name = furniture != null ? furniture.Config.displayName : "Machine";
            GUI.Label(new Rect(x, y, inner, font * 2f), $"<b>{name}</b>   <size={font}>E or Esc closes this.</size>", title);
            y += font * 2.3f;

            var crafter = factory.CrafterOf(machine);
            var arm = factory.ArmOf(machine);
            var drill = factory.DrillOf(machine);
            if (crafter != null) DrawCrafter(crafter, x, ref y, inner, panel, font);
            else if (arm != null) DrawArm(arm, x, ref y, inner, panel, font);
            else if (drill != null) DrawDrill(drill, x, ref y, inner, font);
            else DrawPower(x, ref y, inner, font);
        }

        // A generator, power pole, accumulator or solar panel: its state, its network's, and how electricity works.
        void DrawPower(float x, ref float y, float inner, int font)
        {
            string state = factory.Describe(machine);
            if (state != null)
            {
                GUI.Label(new Rect(x, y, inner, font * 4f), state, text);
                y += font * 4.2f;
            }
            string how;
            if (factory.GeneratorOf(machine) != null)
                how = "It makes electricity for what's within its reach (the blue circle while you place something electric) and for every power pole wired to it. " +
                      "It burns only for what's used: coal from storage first, or wood (which holds a third as much); an arm can put fuel straight in. " +
                      "When more is wanted than all the network's generators give, every machine on it works slower.";
            else if (factory.AccumulatorOf(machine) != null)
                how = "It stores what its network has spare and gives it back when more is wanted than the generators and solar panels give (at night, for solar panels). " +
                      "It has to be in a power pole's area or a generator's reach.";
            else if (factory.SolarOf(machine) != null)
                how = "It makes electricity by day (less under clouds) and none at night, without fuel. An accumulator keeps things going through the night. " +
                      "It has to be in a power pole's area or a generator's reach.";
            else
                how = "A power pole wires itself to the nearest poles within its wire reach (up to five) and powers what's in the square round it. " +
                      "Poles wired together, and generators near them, are one network: they share every generator, solar panel and accumulator on it. " +
                      "While you place something electric, the blue areas show what's powered.";
            GUI.Label(new Rect(x, y, inner, font * 6f), how, text);
            y += font * 6.2f;
        }

        void DrawDrill(Drill d, float x, ref float y, float inner, int font)
        {
            int left = d.OreLeft(out var most);
            line.Clear();
            line.Append(d.Working ? $"<color=#9be29b>Digging {Name(most)}: {Mathf.FloorToInt(d.Progress / Mathf.Max(0.05f, d.SecondsPerItem) * 100f)}%</color>"
                : $"<color=#ffcc80>Stopped: {d.Problem ?? "starting"}</color>");
            line.Append($"   {60f / Mathf.Max(0.05f, d.SecondsPerItem):0.#} a minute{(d.Burner ? ", burns wood or coal" : ", electric")}");
            GUI.Label(new Rect(x, y, inner, font * 1.6f), line.ToString(), text);
            y += font * 1.8f;
            line.Clear();
            line.Append(left > 0 ? $"Ore left to dig: {left:N0} {Name(most)}" : "<color=#ff8a80>No ore left to dig here: move it to another field</color>");
            if (d.Holding) line.Append($".  Waiting to put out: 1 {Name(d.Held)}");
            if (d.Burner) line.Append(d.FuelCount > 0 ? $".  Fuel: {d.FuelCount} {Name(d.FuelType)}" : d.Energy > 0f ? "" : ".  <color=#ff8a80>No fuel</color>");
            GUI.Label(new Rect(x, y, inner, font * 2.6f), line.ToString(), small);
            y += font * 2.8f;
            int across = d.Size + d.Reach * 2;
            GUI.Label(new Rect(x, y, inner, font * 6f),
                $"It digs the ore field under it ({across} x {across} tiles{(d.Reach > 0 ? ", reaching past its sides" : "")}), a tile at a time in turn, " +
                "and puts what it digs out of its chute (the side it faces, R when you place it): onto a belt, or into a chest, a storehouse, " +
                "a furnace, or another burner drill (coal is its fuel)." +
                (d.Burner ? " It burns wood or coal: an arm (or another drill) puts it in." : "") +
                " Ore fields are bare patches of ore on the ground; point at one from the throne to see what's in it.", text);
            y += font * 6.2f;
        }

        void DrawCrafter(Crafter c, float x, ref float y, float inner, Rect panel, int font)
        {
            // What it's doing, and what's in it.
            line.Clear();
            if (c.Working) line.Append($"<color=#9be29b>Making {c.Recipe.name.ToLowerInvariant()}: {Mathf.FloorToInt(c.Progress / Mathf.Max(0.01f, c.Recipe.seconds) * 100f)}%</color>");
            else line.Append($"<color=#ffcc80>Stopped: {c.Problem ?? "idle"}</color>");
            line.Append($"   speed {c.Speed:0.##}x{(c.Burner ? ", burns wood or coal" : ", electric")}");
            GUI.Label(new Rect(x, y, inner, font * 1.6f), line.ToString(), text);
            y += font * 1.8f;
            line.Clear();
            line.Append("Inside: ");
            bool any = false;
            for (int i = 0; i < Inventory.TypeCount; i++)
            {
                int n = c.Input((ResourceType)i);
                if (n <= 0) continue;
                line.Append(any ? ", " : "").Append($"{n} {Name((ResourceType)i)}");
                any = true;
            }
            if (!any) line.Append("no ingredients");
            line.Append(c.Output > 0 ? $".  Made: {c.Output} {Name(c.OutputType)} (arms take it out)" : ".  Nothing made yet");
            if (c.Burner) line.Append(c.FuelCount > 0 ? $".  Fuel: {c.FuelCount} {Name(c.FuelType)}" : ".  <color=#ff8a80>No fuel</color>");
            GUI.Label(new Rect(x, y, inner, font * 2.6f), line.ToString(), small);
            y += font * 2.8f;

            if (c.Furnace)
            {
                line.Clear();
                line.Append("A furnace smelts whatever arms put in it: ");
                bool first = true;
                foreach (var r in factory.Recipes)
                {
                    if (!r.furnace) continue;
                    string age = Factory.Allowed(r) ? "" : " (a later age)";
                    line.Append(first ? "" : ", ").Append($"{Ingredients(r)} make {r.amount} {Name(r.output)}{age}");
                    first = false;
                }
                line.Append(". It takes one kind at a time: once it's empty it takes the next.");
                GUI.Label(new Rect(x, y, inner, font * 4f), line.ToString(), text);
                return;
            }

            // An assembler: its recipes, those of the kingdom's age first.
            GUI.Label(new Rect(x, y, inner, font * 1.6f), "<b>What it makes</b>  <color=#b8ad98>(changing it hands back what's in it)</color>", text);
            y += font * 1.8f;
            float row = font * 2.4f;
            int count = 0;
            foreach (var r in factory.Recipes) if (!r.furnace) count++;
            var view = new Rect(x, y, inner, panel.yMax - y - font * 0.6f);
            float cw = inner - font;
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, cw, row * count));
            float ry = 0f;
            foreach (var r in factory.Recipes)
            {
                if (r.furnace) continue;
                bool allowed = Factory.Allowed(r), chosen = c.Recipe == r;
                string label = $"<b>{r.name}</b>: {Ingredients(r)} make {r.amount}, {r.seconds / Mathf.Max(0.01f, c.Speed):0.#} s";
                var b = new Rect(0, ry, cw, row - font * 0.3f);
                if (chosen) GUI.Label(b, $"<color=#9be29b>{label}   (making this)</color>", text);
                else if (!allowed) GUI.Label(b, $"<color=#8a8070>{label}   (needs a later age)</color>", text);
                else if (GUI.Button(b, label, button)) factory.SetRecipe(machine, r);
                ry += row;
            }
            GUI.EndScrollView();
        }

        static string Ingredients(Recipe r)
        {
            if (r.inputs == null || r.inputs.Length == 0) return "nothing";
            var sb = new StringBuilder();
            for (int i = 0; i < r.inputs.Length; i++) sb.Append(i == 0 ? "" : " + ").Append($"{r.inputs[i].amount} {ResourceText.Name(r.inputs[i].type)}");
            return sb.ToString();
        }

        void DrawArm(Arm a, float x, ref float y, float inner, Rect panel, int font)
        {
            line.Clear();
            line.Append(a.Problem != null ? $"<color=#ffcc80>{a.Problem}</color>" : a.Holding ? $"Carrying {Name(a.Held)}" : "Waiting for something to pick up");
            line.Append(a.Burner ? $"   (burns wood or coal: {Mathf.Max(0f, a.Energy):0.#} MJ left)" : "   (electric)");
            GUI.Label(new Rect(x, y, inner, font * 1.6f), line.ToString(), text);
            y += font * 1.8f;
            GUI.Label(new Rect(x, y, inner, font * 2.6f),
                "It picks things up from the tile behind it and puts them down on the tile in front (the arrow on its base): a belt, a chest " +
                "or storehouse, or a machine (only what the machine needs). Choose what it moves:", small);
            y += font * 2.8f;
            var anything = new Rect(x, y, font * 10f, font * 1.9f);
            if (!a.HasFilter) GUI.Label(anything, "<color=#9be29b><b>Anything</b> (moving it)</color>", text);
            else if (GUI.Button(anything, "Anything", button)) a.HasFilter = false;
            y += font * 2.3f;
            // A grid of everything there is.
            float cell = font * 9.5f, rowH = font * 2.1f;
            int columns = Mathf.Max(1, Mathf.FloorToInt((inner - font) / cell));
            int rows = Mathf.CeilToInt(Inventory.TypeCount / (float)columns);
            var view = new Rect(x, y, inner, panel.yMax - y - font * 0.6f);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, inner - font, rows * rowH));
            for (int i = 0; i < Inventory.TypeCount; i++)
            {
                var t = (ResourceType)i;
                var r = new Rect(i % columns * cell, i / columns * rowH, cell - font * 0.3f, rowH - font * 0.3f);
                string label = char.ToUpperInvariant(Name(t)[0]) + Name(t).Substring(1);
                if (a.HasFilter && a.Filter == t) GUI.Label(r, $"<color=#9be29b><b>{label}</b></color>", text);
                else if (GUI.Button(r, label, button))
                {
                    a.HasFilter = true;
                    a.Filter = t;
                }
            }
            GUI.EndScrollView();
        }
    }
}
