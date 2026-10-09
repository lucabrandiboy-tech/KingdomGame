using System.Text;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Economy;
using KingdomGame.World;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// The game's on-screen info: clock, backpack and kingdom storage (top
    /// right), what you can do right now (bottom middle), the controls (bottom
    /// left, H hides them), and on the throne the build bar (bottom middle).
    /// Simple placeholder boxes; a proper themed UI comes later (see the brief:
    /// it changes style with your era).
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        RulerController ruler;
        DayNightCycle dayNight;
        Kingdom kingdom;
        BuildController builder;
        CameraRig rig;
        bool showControls = true;
        GUIStyle box, hint, button, selectedButton;
        Rect buildBar;
        readonly StringBuilder text = new StringBuilder(256);

        const string WalkControls =
            "WASD  walk          Mouse  aim          Wheel  zoom\n" +
            "Left click  rifle (barely scratches stone)\n" +
            "Right click  rocket          G  throw C4\n" +
            "E  use what's next to you: the throne, a storehouse,\n" +
            "    a blueprint (hold), a tree/rock/bush (hold to gather)\n" +
            "F3  performance info          H  hide this";

        const string ThroneControls =
            "1-5 or click  pick a building (again = put away)\n" +
            "Left click  place  (walls: hold and drag for a line)\n" +
            "Right click  stop placing / cancel a blueprint\n" +
            "WASD  move the view          Wheel  zoom\n" +
            "E or Esc  stand up          H  hide this";

        public void Setup(RulerController player, DayNightCycle cycle, Kingdom owner, BuildController build, CameraRig cameraRig)
        {
            ruler = player;
            dayNight = cycle;
            kingdom = owner;
            builder = build;
            rig = cameraRig;
        }

        void Update()
        {
            if (GameInput.Pressed(KeyCode.H)) showControls = !showControls;

            // Tell the build controller and the camera when the mouse is over the build bar.
            var mouse = GameInput.PointerPosition;
            bool over = builder != null && builder.Active && buildBar.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
            if (builder != null) builder.PointerOverHud = over;
            if (rig != null) rig.BlockScroll = over;
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
            }
            int font = Mathf.Max(12, Screen.height / 50);
            box.fontSize = hint.fontSize = button.fontSize = selectedButton.fontSize = font;

            DrawStatus();
            bool throne = builder != null && builder.Active;

            // Bottom middle: what you can do right now, or about what's under the mouse on the throne.
            string now = null;
            if (throne)
            {
                if (!string.IsNullOrEmpty(builder.Message)) now = builder.Message;
                else now = builder.Hover;
            }
            else if (ruler != null)
            {
                now = !string.IsNullOrEmpty(ruler.Notice) ? ruler.Notice : ruler.Hint;
            }
            if (!string.IsNullOrEmpty(now)) Draw(now, hint, Corner.BottomMiddle, throne ? font * 5 : 0);

            if (throne) DrawBuildBar(font);
            else buildBar = Rect.zero;

            if (showControls) Draw(throne ? ThroneControls : WalkControls, box, Corner.BottomLeft, 0);
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
                text.Append($"Builders: {busy} of {kingdom.Villagers.Count} busy   Blueprints: {kingdom.Blueprints.Count}");
            }
            Draw(text.ToString(), box, Corner.TopRight, 0);
        }

        void DrawBuildBar(int font)
        {
            var menu = kingdom.BuildMenu;
            if (menu.Count == 0) return;
            float width = font * 11f, height = font * 3.4f, gap = font * 0.5f;
            float total = menu.Count * width + (menu.Count - 1) * gap;
            float x = (Screen.width - total) / 2f, y = Screen.height - height - font;
            buildBar = new Rect(x - gap, y - gap, total + gap * 2, height + gap * 2);
            GUI.Box(buildBar, GUIContent.none);
            for (int i = 0; i < menu.Count; i++)
            {
                var b = menu[i];
                if (b == null) continue;
                bool selected = builder.SelectedIndex == i;
                string label = $"<b>{i + 1}  {b.displayName}</b>\n{b.CostText()}";
                if (GUI.Button(new Rect(x + i * (width + gap), y, width, height), label, selected ? selectedButton : button))
                    builder.Select(i);
            }
        }

        enum Corner { TopRight, BottomLeft, BottomMiddle }

        static void Draw(string message, GUIStyle style, Corner corner, float raise)
        {
            var content = new GUIContent(message);
            var size = style.CalcSize(content) + new Vector2(10, 6);
            const float margin = 10;
            Vector2 at;
            switch (corner)
            {
                case Corner.TopRight: at = new Vector2(Screen.width - size.x - margin, margin); break;
                case Corner.BottomLeft: at = new Vector2(margin, Screen.height - size.y - margin); break;
                default: at = new Vector2((Screen.width - size.x) / 2f, Screen.height - size.y - Screen.height * 0.12f - raise); break;
            }
            GUI.Box(new Rect(at, size), content, style);
        }
    }
}
