using System.Text;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Economy;
using KingdomGame.World;
using KingdomGame.Zombies;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// The game's on-screen info: your health and (in survival) the waves (top
    /// left), clock, backpack and kingdom (top right), big announcements (top
    /// middle), what you can do right now (bottom middle), the controls (bottom
    /// left, H hides them), a red flash when you're hurt, and on the throne the
    /// build bar and the names of your rooms. Simple placeholder boxes; a proper
    /// themed UI comes later (see the brief: it changes style with your era).
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        RulerController ruler;
        DayNightCycle dayNight;
        Kingdom kingdom;
        BuildController builder;
        CameraRig rig;
        SurvivalDirector waves;
        bool showControls = true;
        GUIStyle box, hint, button, selectedButton, announce, announceShadow, roomLabel, roomShadow, barLabel;
        Rect buildBar;
        float shownHealth = -1f, flash;
        readonly StringBuilder text = new StringBuilder(256);

        const string WalkControls =
            "WASD  walk          Mouse  aim          Wheel  zoom\n" +
            "Left click  rifle (barely scratches stone)\n" +
            "Right click  rocket          G  throw C4\n" +
            "Your shots fly over a wall you stand right behind\n" +
            "E  use what's next to you: the throne, a storehouse, a ladder,\n" +
            "    a blueprint (hold), a tree/rock/bush (hold to gather)\n" +
            "On the walls: walk along them, shoot over them; E climbs down\n" +
            "Walk over loot bags to pick them up\n" +
            "Esc  pause          F3  performance info          H  hide this";

        const string SurvivalKeys = "\nN  call the next wave now";

        const string ThroneControls =
            "Tab or click a tab  walls, floors, furniture, lights, storage\n" +
            "1-9 or click  pick something (again = put away)     R  turn it\n" +
            "Left click  place  (walls: drag for a line, floors: drag for an area)\n" +
            "Right click  stop placing / cancel a blueprint / tear down\n" +
            "Rooms: close an area with walls and doors; furniture names it\n" +
            "WASD  move the view          Wheel  zoom\n" +
            "E or Esc  stand up          H  hide this";

        public void Setup(RulerController player, DayNightCycle cycle, Kingdom owner, BuildController build, CameraRig cameraRig,
            SurvivalDirector survival = null)
        {
            ruler = player;
            dayNight = cycle;
            kingdom = owner;
            builder = build;
            rig = cameraRig;
            waves = survival;
        }

        void Update()
        {
            if (GameInput.Pressed(KeyCode.H)) showControls = !showControls;

            // Tell the build controller and the camera when the mouse is over the build bar.
            var mouse = GameInput.PointerPosition;
            bool over = builder != null && builder.Active && buildBar.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            if (builder != null) builder.PointerOverHud = over;
            if (rig != null) rig.BlockScroll = over;

            // A red flash when the ruler is hurt.
            var health = ruler != null ? ruler.Health : null;
            if (health != null)
            {
                if (shownHealth >= 0f && health.Current < shownHealth - 0.01f) flash = Mathf.Min(0.45f, flash + (shownHealth - health.Current) / health.Max * 2.5f + 0.12f);
                shownHealth = health.Current;
            }
            flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 0.9f);
        }

        void OnGUI()
        {
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, wordWrap = false };
                hint = new GUIStyle(box) { alignment = TextAnchor.MiddleCenter };
                button = new GUIStyle(GUI.skin.button) { richText = true, alignment = TextAnchor.MiddleCenter };
                selectedButton = new GUIStyle(button);
                selectedButton.normal.textColor = selectedButton.hover.textColor = new Color(1f, 0.85f, 0.4f);
                announce = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
                announceShadow = new GUIStyle(announce);
                announceShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
                roomLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
                roomShadow = new GUIStyle(roomLabel);
                roomShadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
                barLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            }
            int font = Mathf.Max(12, Screen.height / 50);
            box.fontSize = hint.fontSize = button.fontSize = selectedButton.fontSize = barLabel.fontSize = font;
            announce.fontSize = announceShadow.fontSize = Mathf.RoundToInt(font * 1.6f);
            roomLabel.fontSize = roomShadow.fontSize = Mathf.RoundToInt(font * 0.9f);

            if (flash > 0f) DrawFlash();
            bool throne = builder != null && builder.Active;
            if (throne) DrawRoomNames();

            DrawStatus();
            DrawPlayer(font);
            DrawAnnouncement(font);

            if (throne) DrawBuildBar(font);
            else buildBar = Rect.zero;

            // Bottom middle (above the build bar on the throne): what you can do right now, or about what's under the mouse.
            string now = null;
            if (throne) now = !string.IsNullOrEmpty(builder.Message) ? builder.Message : builder.Hover;
            else if (ruler != null) now = !string.IsNullOrEmpty(ruler.Notice) ? ruler.Notice : ruler.Hint;
            if (!string.IsNullOrEmpty(now))
            {
                var content = new GUIContent(now);
                var size = hint.CalcSize(content) + new Vector2(10, 6);
                float bottom = throne && buildBar.height > 0f ? buildBar.yMin - font * 0.5f : Screen.height * 0.88f;
                GUI.Box(new Rect((Screen.width - size.x) / 2f, bottom - size.y, size.x, size.y), content, hint);
            }

            if (showControls)
            {
                string controls = throne ? ThroneControls : WalkControls + (waves != null ? SurvivalKeys : "");
                Draw(controls, box, Corner.BottomLeft);
            }
        }

        void DrawStatus()
        {
            text.Clear();
            if (dayNight != null) text.Append("<b>").Append(dayNight.ClockText()).Append("</b>").Append(dayNight.IsNight ? "  (night)" : "").Append('\n');
            if (ruler != null && ruler.Backpack != null)
            {
                var pack = ruler.Backpack;
                text.Append($"Backpack: wood {pack.Get(ResourceType.Wood)}, stone {pack.Get(ResourceType.Stone)}, food {pack.Get(ResourceType.Food)}  ({pack.Total}/{pack.Capacity})\n");
            }
            if (kingdom != null)
            {
                text.Append($"Storage: wood {kingdom.Stock(ResourceType.Wood)}, stone {kingdom.Stock(ResourceType.Stone)}, food {kingdom.Stock(ResourceType.Food)}  ({kingdom.StorageUsed}/{kingdom.StorageCapacity})\n");
                int busy = 0;
                foreach (var v in kingdom.Villagers) if (v != null && !v.IsIdle) busy++;
                text.Append($"Villagers: {kingdom.Villagers.Count} (homes for {kingdom.VillagerCapacity}), {busy} busy   Blueprints: {kingdom.Blueprints.Count}");
            }
            Draw(text.ToString(), box, Corner.TopRight);
        }

        // Top left: the ruler's health, and in survival the wave.
        void DrawPlayer(int font)
        {
            var health = ruler != null ? ruler.Health : null;
            float x = 10f, y = 10f, width = font * 16f, height = font * 1.4f;
            if (health != null)
            {
                GUI.Box(new Rect(x, y, width, height), GUIContent.none);
                float fill = Mathf.Clamp01(health.Fraction);
                var old = GUI.color;
                GUI.color = Color.Lerp(new Color(0.75f, 0.12f, 0.1f), new Color(0.35f, 0.7f, 0.3f), fill);
                GUI.DrawTexture(new Rect(x + 3, y + 3, (width - 6) * fill, height - 6), Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(new Rect(x, y, width, height), health.Dead ? "<b>Fallen</b>" : $"<b>Health {Mathf.CeilToInt(health.Current)} / {Mathf.CeilToInt(health.Max)}</b>", barLabel);
                y += height + 6f;
            }
            if (waves == null) return;

            text.Clear();
            switch (waves.Now)
            {
                case SurvivalDirector.Phase.Preparing:
                    text.Append($"<b>The first wave comes in {Clock(waves.TimeLeft)}</b>\nBuild walls and doors!  (N: call it now)");
                    break;
                case SurvivalDirector.Phase.Fighting:
                    text.Append($"<b>Wave {waves.Wave}</b>: {waves.ZombiesLeft} zombie{(waves.ZombiesLeft == 1 ? "" : "s")} left");
                    break;
                case SurvivalDirector.Phase.Calm:
                    text.Append($"<b>Wave {waves.Wave + 1} comes in {Clock(waves.TimeLeft)}</b>  (N: call it now)");
                    break;
                default:
                    text.Append($"<b>Fallen on wave {Mathf.Max(1, waves.Wave)}</b>");
                    break;
            }
            text.Append($"\nZombies killed: {waves.Kills}");
            var content = new GUIContent(text.ToString());
            var size = box.CalcSize(content) + new Vector2(10, 6);
            GUI.Box(new Rect(x, y, size.x, size.y), content, box);
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        void DrawAnnouncement(int font)
        {
            string message = waves != null ? waves.Announcement : null;
            if (string.IsNullOrEmpty(message)) return;
            float width = Mathf.Min(Screen.width - font * 4f, font * 40f);
            var rect = new Rect((Screen.width - width) / 2f, Screen.height * 0.14f, width, font * 5f);
            // A dark outline so it reads on any background.
            string plain = $"<b>{message}</b>";
            for (int i = 0; i < 4; i++)
                GUI.Label(new Rect(rect.x + (i % 2 == 0 ? -2 : 2), rect.y + (i < 2 ? -2 : 2), rect.width, rect.height), plain, announceShadow);
            GUI.Label(rect, $"<b><color=#f0d08a>{message}</color></b>", announce);
        }

        void DrawFlash()
        {
            var old = GUI.color;
            GUI.color = new Color(0.7f, 0f, 0f, flash);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        // The names of the rooms, over their middles (throne view).
        void DrawRoomNames()
        {
            if (kingdom == null || rig == null) return;
            var view = rig.GetComponent<Camera>();
            if (view == null) return;
            foreach (var room in kingdom.Rooms)
            {
                var screen = view.WorldToScreenPoint(room.LabelAt);
                if (screen.z < 0f || screen.x < 0f || screen.x > Screen.width || screen.y < 0f || screen.y > Screen.height) continue;
                var rect = new Rect(screen.x - 150f, Screen.height - screen.y - 12f, 300f, 24f);
                string label = $"<b>{room.Name}</b>";
                GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), label, roomShadow);
                GUI.Label(rect, $"<color=#f2e2b8>{label}</color>", roomLabel);
            }
        }

        // The build bar: a row of tabs, and the open tab's buildings under them (more rows if needed).
        void DrawBuildBar(int font)
        {
            var tabs = builder.Categories;
            var items = builder.Items;
            if (tabs.Count == 0) return;
            float gap = font * 0.4f;
            float tabWidth = font * 7.5f, tabHeight = font * 1.9f;
            float width = font * 9.5f, height = font * 3.2f;
            int perRow = Mathf.Max(1, Mathf.Min(items.Count, Mathf.FloorToInt((Screen.width - font * 4f) / (width + gap))));
            int rows = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)perRow));
            float rowWidth = perRow * width + (perRow - 1) * gap;
            float tabsWidth = tabs.Count * tabWidth + (tabs.Count - 1) * gap;
            float total = Mathf.Max(rowWidth, tabsWidth);
            float bottom = Screen.height - font;
            float itemsTop = bottom - rows * height - (rows - 1) * gap;
            float tabsTop = itemsTop - gap - tabHeight;
            float left = (Screen.width - total) / 2f;
            buildBar = new Rect(left - gap, tabsTop - gap, total + gap * 2, bottom - tabsTop + gap * 2);
            GUI.Box(buildBar, GUIContent.none);

            float tabsLeft = (Screen.width - tabsWidth) / 2f;
            for (int i = 0; i < tabs.Count; i++)
            {
                bool open = tabs[i] == builder.Category;
                if (GUI.Button(new Rect(tabsLeft + i * (tabWidth + gap), tabsTop, tabWidth, tabHeight), $"<b>{TabName(tabs[i])}</b>", open ? selectedButton : button))
                    builder.SelectCategory(tabs[i]);
            }
            float rowLeft = (Screen.width - rowWidth) / 2f;
            for (int i = 0; i < items.Count; i++)
            {
                var b = items[i];
                if (b == null) continue;
                int row = i / perRow, column = i % perRow;
                bool selected = builder.SelectedIndex == i;
                string key = i < 9 ? $"{i + 1}  " : "";
                string label = $"<b>{key}{b.displayName}</b>\n{b.CostText()}";
                var rect = new Rect(rowLeft + column * (width + gap), itemsTop + row * (height + gap), width, height);
                if (GUI.Button(rect, label, selected ? selectedButton : button))
                    builder.Select(i);
            }
        }

        static string TabName(BuildCategory c)
        {
            switch (c)
            {
                case BuildCategory.Walls: return "Walls & doors";
                case BuildCategory.Floors: return "Floors & rugs";
                case BuildCategory.Furniture: return "Furniture";
                case BuildCategory.Lights: return "Lights";
                default: return "Storage";
            }
        }

        enum Corner { TopRight, BottomLeft }

        static void Draw(string message, GUIStyle style, Corner corner)
        {
            var content = new GUIContent(message);
            var size = style.CalcSize(content) + new Vector2(10, 6);
            const float margin = 10;
            var at = corner == Corner.TopRight
                ? new Vector2(Screen.width - size.x - margin, margin)
                : new Vector2(margin, Screen.height - size.y - margin);
            GUI.Box(new Rect(at, size), content, style);
        }
    }
}
