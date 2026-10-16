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
        GUIStyle box, hint, button, selectedButton, announce, announceShadow, roomLabel, roomShadow, barLabel, field, tip;
        Rect buildBar, searchBox;
        bool searchFocused;
        const string SearchControl = "BuildSearch";
        float shownHealth = -1f, flash;
        readonly StringBuilder text = new StringBuilder(256);

        const string WalkControls =
            "WASD  walk          Mouse  aim          Wheel  zoom\n" +
            "Left click  rifle (barely scratches stone)\n" +
            "Right click  rocket          G  throw C4\n" +
            "Your shots fly over a wall you stand right behind\n" +
            "E  use what's next to you: the throne, a house's door, a storehouse,\n" +
            "    a ladder, a blueprint (hold), a tree/rock/bush (hold to gather)\n" +
            "In a house: B furnish it, U upgrade it (once you reach the next age)\n" +
            "On the walls: walk along them, shoot over them; E climbs down\n" +
            "Walk over loot bags to pick them up\n" +
            "V  villagers' jobs          T  research\n" +
            "Esc  pause          F3  performance info          H  hide this\n" +
            "F10  cheats, for testing (offline games)";

        const string SurvivalKeys = "\nN  call the next wave now";
        const string FreeBuildKeys = "\nF9  send 8 zombies (to try your traps and towers)";

        const string ThroneControls =
            "Tab or click a tab  walls, floors & roofs, furniture, lights, storage, traps, towers\n" +
            "[ and ]  earlier / later ages          T  research          V  villagers' jobs\n" +
            "Search box (top right of the bar): find anything you can build; hover one to see what it does\n" +
            "1-9 or click  pick something (again = put away)     R  turn it\n" +
            "Left click  place  (walls: drag for a line, floors and roofs: drag for an area)\n" +
            "Right click  stop placing / cancel a blueprint / tear down\n" +
            "Shift + right click  take a roof off\n" +
            "Over a house:  F look inside and furnish it,  U upgrade it\n" +
            "A house: walls and a door round a room, furniture in it, a roof on top\n" +
            "WASD  move the view          Wheel  zoom\n" +
            "E or Esc  stand up          H  hide this";

        const string FurnishControls =
            "Building inside a house or mine: furniture, lights, chests, floors and rugs\n" +
            "Tab or click a tab  the tabs          [ and ]  earlier / later ages\n" +
            "1-9 or click  pick something (again = put away)     R  turn it\n" +
            "Left click  place  (floors: drag for an area)\n" +
            "Right click  stop placing / cancel a blueprint / take something out\n" +
            "Builders come in through the door to build it\n" +
            "In a mine: Mining tab, Dig, drag over rock: miners dig it out (stone, copper, iron, gold)\n" +
            "WASD  move the view          Wheel  zoom\n" +
            "Esc (or B, or E)  done          H  hide this";

        public void Setup(RulerController player, DayNightCycle cycle, Kingdom owner, BuildController build, CameraRig cameraRig,
            SurvivalDirector survival = null)
        {
            ruler = player;
            dayNight = cycle;
            kingdom = owner;
            builder = build;
            rig = cameraRig;
            waves = survival;
            if (kingdom != null) kingdom.Notice += text => { notice = text; noticeUntil = Time.unscaledTime + 6f; };
        }

        string notice;
        float noticeUntil;

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

        void OnDisable()
        {
            if (searchFocused) UiFocus.Typing = false;
            searchFocused = false;
        }

        void OnGUI()
        {
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, wordWrap = false };
                hint = new GUIStyle(box) { alignment = TextAnchor.MiddleCenter };
                button = new GUIStyle(GUI.skin.button) { richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                selectedButton = new GUIStyle(button);
                selectedButton.normal.textColor = selectedButton.hover.textColor = new Color(1f, 0.85f, 0.4f);
                announce = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
                announceShadow = new GUIStyle(announce);
                announceShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
                roomLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
                roomShadow = new GUIStyle(roomLabel);
                roomShadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
                barLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
                field = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleLeft };
                tip = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, wordWrap = true };
            }
            int font = Mathf.Max(12, Screen.height / 50);
            box.fontSize = hint.fontSize = button.fontSize = selectedButton.fontSize = barLabel.fontSize = field.fontSize = tip.fontSize = font;
            announce.fontSize = announceShadow.fontSize = Mathf.RoundToInt(font * 1.6f);
            roomLabel.fontSize = roomShadow.fontSize = Mathf.RoundToInt(font * 0.9f);

            if (flash > 0f) DrawFlash();
            bool throne = builder != null && builder.Active;
            if (throne) DrawRoomNames();

            DrawStatus();
            DrawPlayer(font);
            DrawAnnouncement(font);

            if (throne) DrawBuildBar(font);
            else
            {
                buildBar = Rect.zero;
                LetGoOfSearch();
            }

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
                string controls = throne ? (builder.Furnishing != null ? FurnishControls : ThroneControls)
                    : WalkControls + (waves != null ? SurvivalKeys : FreeBuildKeys);
                Draw(controls, box, Corner.BottomLeft);
            }
        }

        void DrawStatus()
        {
            text.Clear();
            if (dayNight != null) text.Append("<b>").Append(dayNight.ClockText()).Append("</b>").Append(dayNight.IsNight ? "  (night)" : "").Append('\n');
            string season = Seasons.Current != null ? Seasons.Current.Describe() : null;
            if (season != null) text.Append(season).Append('\n');
            if (ruler != null && ruler.Backpack != null)
            {
                var pack = ruler.Backpack;
                text.Append($"Backpack: {Economy.ResourceText.Describe(pack)}  ({pack.Total}/{pack.Capacity})\n");
            }
            if (kingdom != null)
            {
                text.Append($"Storage: wood {kingdom.Stock(ResourceType.Wood)}, stone {kingdom.Stock(ResourceType.Stone)}, food {kingdom.Stock(ResourceType.Food)}");
                // Ores once you've dug some up.
                for (int k = (int)ResourceType.Copper; k < Economy.Inventory.TypeCount; k++)
                {
                    var ore = (ResourceType)k;
                    int n = kingdom.Stock(ore);
                    if (n > 0) text.Append($", {Economy.ResourceText.Name(ore)} {n}");
                }
                text.Append($"  ({kingdom.StorageUsed}/{kingdom.StorageCapacity})\n");
                int busy = 0;
                foreach (var v in kingdom.Villagers) if (v != null && !v.IsIdle) busy++;
                text.Append($"Villagers: {kingdom.Villagers.Count} (homes for {kingdom.VillagerCapacity}), {busy} busy   Blueprints: {kingdom.Blueprints.Count}");
                // Fire!
                var fires = Fires.Current;
                if (fires != null && fires.Count > 0)
                    text.Append($"\n<color=#ffa060>Fire! {fires.Count} thing{(fires.Count == 1 ? " is" : "s are")} burning" +
                                (kingdom.HasWell ? ": villagers are bringing water from the well" : ": build a well so villagers can put fires out (or hold E at one)") + "</color>");
                // Wolves about at night.
                var wild = World.Wildlife.Current;
                int wolves = wild != null ? wild.WolvesOut : 0;
                if (wolves > 0)
                    text.Append($"\n<color=#c8b8ff>Wolves about ({wolves}): they hunt anyone out in the dark, but keep out of firelight</color>");
                // Winter without firewood.
                if (kingdom.Cold) text.Append("\n<color=#9fd0ff>Out of firewood: your villagers are cold and slow (they burn wood in winter)</color>");
                // Going hungry with nothing to eat.
                if (kingdom.Stock(ResourceType.Food) <= 0)
                {
                    int hungry = 0;
                    foreach (var v in kingdom.Villagers) if (v != null && v.Fed < Villagers.NeedsMath.EatBelow) hungry++;
                    if (hungry > 0) text.Append($"\n<color=#ff8a80>No food: {hungry} villager{(hungry == 1 ? " is" : "s are")} going hungry (foragers or hunters: V)</color>");
                }
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
                if (ruler.Armory != null)
                {
                    GUI.Box(new Rect(x, y, width, height), $"In hand: <b>{ruler.WeaponName}</b>   <color=#b8ad98>(Q / 1-9)</color>", box);
                    y += height + 6f;
                }
            }
            // The age, and what's being researched (T opens the research screen).
            var research = Progression.Research.Current;
            if (research != null && research.CurrentAge != null)
            {
                text.Clear();
                text.Append("<b>").Append(research.CurrentAge.displayName).Append("</b>");
                if (research.Mode == Progression.Research.Unlocking.ByResearch)
                {
                    var now = research.Researching;
                    if (now == null) text.Append("   Nothing researched: press T");
                    else if (research.Waiting != null) text.Append($"   {now.displayName}: <color=#ff8a80>{research.Waiting}</color>");
                    else text.Append($"   {now.displayName}: {TechScreen.Duration(research.SecondsLeft)} left");
                }
                text.Append("   (T: research)");
                var ageContent = new GUIContent(text.ToString());
                var ageSize = box.CalcSize(ageContent) + new Vector2(10, 6);
                GUI.Box(new Rect(x, y, ageSize.x, ageSize.y), ageContent, box);
                y += ageSize.y + 6f;
            }
            if (waves == null) return;

            text.Clear();
            switch (waves.Now)
            {
                case SurvivalDirector.Phase.Preparing when waves.WaitingForNight:
                case SurvivalDirector.Phase.Calm when waves.WaitingForNight:
                    text.Append($"<b>Day: the next horde comes at nightfall, in {Clock(waves.TimeLeft)}</b>\nBuild and repair!  (N: call it now)");
                    break;
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
            // The kingdom's own news (a zombie base, an upgrade) when there's no wave news.
            if (string.IsNullOrEmpty(message) && notice != null && Time.unscaledTime < noticeUntil) message = notice;
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

        // The build bar: at the top the age page (with arrows to the other ages), "show locked" and the search box; under
        // them a row of tabs, and the open tab's buildings (more rows if needed). Hovering one shows what it costs and does.
        void DrawBuildBar(int font)
        {
            var tabs = builder.Categories;
            if (tabs.Count == 0) return;
            var e = Event.current;
            // Clicking anywhere but the search box lets go of it (before any button takes the click).
            if (searchFocused && e.type == EventType.MouseDown && !searchBox.Contains(e.mousePosition)) LetGoOfSearch();

            var items = builder.Items;
            float gap = font * 0.4f;
            float tabWidth = font * 7.5f, tabHeight = font * 1.9f;
            float width = font * 9.5f, height = font * 3.2f;
            int perRow = Mathf.Max(1, Mathf.Min(Mathf.Max(1, items.Count), Mathf.FloorToInt((Screen.width - font * 4f) / (width + gap))));
            int rows = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)perRow));
            float rowWidth = Mathf.Max(width * 2f, perRow * width + (perRow - 1) * gap);
            float tabsWidth = tabs.Count * tabWidth + (tabs.Count - 1) * gap;
            var research = Progression.Research.Current;
            bool pages = research != null && builder.LastAgePage > 0;
            bool lockSwitch = research != null && research.Mode == Progression.Research.Unlocking.ByResearch;
            float pagerWidth = font * 18f, sideWidth = font * 11f;
            float topWidth = pagerWidth + (sideWidth + gap) * 2f;
            float total = Mathf.Max(Mathf.Max(rowWidth, tabsWidth), topWidth);
            float bottom = Screen.height - font;
            float itemsTop = bottom - rows * height - (rows - 1) * gap;
            float tabsTop = itemsTop - gap - tabHeight;
            float topRow = tabsTop - gap - tabHeight;
            float left = (Screen.width - total) / 2f;
            buildBar = new Rect(left - gap, topRow - gap, total + gap * 2, bottom - topRow + gap * 2);
            GUI.Box(buildBar, GUIContent.none);

            float pagerLeft = (Screen.width - pagerWidth) / 2f;
            if (pages)
            {
                var age = research.AgeAt(builder.AgePage);
                float arrow = font * 2.4f;
                GUI.enabled = builder.AgePage > 0;
                if (GUI.Button(new Rect(pagerLeft, topRow, arrow, tabHeight), "<b>[ <</b>", button)) builder.SelectAge(builder.AgePage - 1);
                GUI.enabled = builder.AgePage < builder.LastAgePage;
                if (GUI.Button(new Rect(pagerLeft + pagerWidth - arrow, topRow, arrow, tabHeight), "<b>> ]</b>", button)) builder.SelectAge(builder.AgePage + 1);
                GUI.enabled = true;
                GUI.Label(new Rect(pagerLeft + arrow, topRow, pagerWidth - arrow * 2f, tabHeight), $"<b>{(age != null ? age.displayName : "")}</b>", barLabel);
            }
            if (lockSwitch)
            {
                bool show = builder.ShowLocked;
                if (GUI.Button(new Rect(pagerLeft - gap - sideWidth, topRow, sideWidth, tabHeight), show ? "<b>Showing locked</b>" : "Show locked", show ? selectedButton : button))
                    builder.ShowLocked = !show;
            }
            SearchBox(new Rect(pagerLeft + pagerWidth + gap, topRow, sideWidth, tabHeight));

            bool searching = builder.Search.Trim().Length > 0;
            float tabsLeft = (Screen.width - tabsWidth) / 2f;
            for (int i = 0; i < tabs.Count; i++)
            {
                bool open = !searching && tabs[i] == builder.Category;
                if (GUI.Button(new Rect(tabsLeft + i * (tabWidth + gap), tabsTop, tabWidth, tabHeight), $"<b>{TabName(tabs[i])}</b>", open ? selectedButton : button))
                    builder.SelectCategory(tabs[i]);
            }
            float rowLeft = (Screen.width - rowWidth) / 2f;
            if (items.Count == 0)
            {
                string why = searching ? "Nothing you can build matches that"
                    : builder.HiddenLocked > 0 ? $"Nothing researched on this tab in this age yet ({builder.HiddenLocked} to research: T, or Show locked)"
                    : "Nothing on this tab in this age";
                GUI.Label(new Rect(left, itemsTop, total, height), $"<color=#a09888>{why}</color>", barLabel);
            }
            for (int i = 0; i < items.Count; i++)
            {
                var b = items[i];
                if (b == null) continue;
                int row = i / perRow, column = i % perRow;
                bool selected = builder.SelectedIndex == i;
                string key = i < 9 ? $"{i + 1}  " : "";
                // Not researched yet: greyed out, saying what it needs.
                string locked = research != null ? research.LockedReason(b) : null;
                string label = locked == null ? $"<b>{key}{b.displayName}</b>\n{b.CostText()}"
                    : $"<color=#8a8478><b>{key}{b.displayName}</b>\n{locked}</color>";
                var rect = new Rect(rowLeft + column * (width + gap), itemsTop + row * (height + gap), width, height);
                // (the tooltip is its number: the box above the bar says the rest)
                if (GUI.Button(rect, new GUIContent(label, i.ToString()), selected ? selectedButton : button))
                    builder.Select(i);
            }
            if (int.TryParse(GUI.tooltip, out int hovered) && hovered >= 0 && hovered < items.Count && items[hovered] != null)
                DrawTip(items[hovered], research, font);
        }

        // The search box: type to find anything you can build. Enter or a click elsewhere lets go of it, Esc clears it.
        void SearchBox(Rect r)
        {
            searchBox = r;
            var e = Event.current;
            if (searchFocused && e.type == EventType.KeyDown && (e.keyCode == KeyCode.Escape || e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                if (e.keyCode == KeyCode.Escape) builder.SetSearch("");
                LetGoOfSearch();
                e.Use();
            }
            GUI.SetNextControlName(SearchControl);
            string typed = GUI.TextField(r, builder.Search, 30, field);
            if (typed != builder.Search) builder.SetSearch(typed);
            bool focused = GUI.GetNameOfFocusedControl() == SearchControl;
            if (!focused && builder.Search.Length == 0)
                GUI.Label(new Rect(r.x + 6f, r.y, r.width - 6f, r.height), "<color=#8a8478>Search...</color>", barLabel);
            if (focused != searchFocused)
            {
                // While typing, keys are letters (WASD doesn't move the view, numbers don't pick).
                searchFocused = focused;
                UiFocus.Typing = focused;
            }
        }

        void LetGoOfSearch()
        {
            if (!searchFocused) return;
            searchFocused = false;
            UiFocus.Typing = false;
            GUI.FocusControl(null);
        }

        // Above the build bar: what the thing under the mouse costs and does (and what it needs, if it's locked).
        void DrawTip(BuildingConfig b, Progression.Research research, int font)
        {
            string body = BuildInfo.Describe(b);
            string locked = research != null ? research.LockedReason(b) : null;
            if (locked != null) body += $"\n<color=#f0b060>{locked}</color>";
            var content = new GUIContent(body);
            float w = Mathf.Min(font * 30f, Screen.width - 20f);
            float h = tip.CalcHeight(content, w);
            float x = Mathf.Clamp(Event.current.mousePosition.x - w / 2f, 10f, Screen.width - w - 10f);
            var rect = new Rect(x, buildBar.yMin - h - font * 0.4f, w, h);
            GUI.Box(rect, GUIContent.none);   // twice as dark
            GUI.Box(rect, content, tip);
        }

        static string TabName(BuildCategory c)
        {
            switch (c)
            {
                case BuildCategory.Walls: return "Walls & doors";
                case BuildCategory.Floors: return "Floors & roofs";
                case BuildCategory.Furniture: return "Furniture";
                case BuildCategory.Lights: return "Lights";
                case BuildCategory.Traps: return "Traps";
                case BuildCategory.Towers: return "Towers";
                case BuildCategory.Houses: return "Houses";
                case BuildCategory.Farming: return "Farming";
                case BuildCategory.Mining: return "Mining";
                case BuildCategory.Industry: return "Industry";
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
