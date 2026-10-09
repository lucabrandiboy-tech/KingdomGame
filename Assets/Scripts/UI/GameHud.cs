using System.Text;
using KingdomGame.Character;
using KingdomGame.Economy;
using KingdomGame.World;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// The game's on-screen info: the clock and backpack (top right), what you
    /// can do right now (bottom middle) and the controls (bottom left, H hides them).
    /// Simple placeholder boxes; a proper themed UI comes later (see the brief: it
    /// changes style with your era).
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        RulerController ruler;
        DayNightCycle dayNight;
        bool showControls = true;
        GUIStyle box, hint;
        readonly StringBuilder text = new StringBuilder(256);

        const string Controls =
            "WASD  walk          Mouse  aim\n" +
            "Left click  rifle (barely scratches stone)\n" +
            "Right click  rocket          G  throw C4\n" +
            "Hold E  gather wood, stone or food\n" +
            "F3  performance info          H  hide this";

        public void Setup(RulerController player, DayNightCycle cycle)
        {
            ruler = player;
            dayNight = cycle;
        }

        void Update()
        {
            if (GameInput.Pressed(KeyCode.H)) showControls = !showControls;
        }

        void OnGUI()
        {
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, wordWrap = false };
                hint = new GUIStyle(box) { alignment = TextAnchor.MiddleCenter };
            }
            int font = Mathf.Max(12, Screen.height / 50);
            box.fontSize = font;
            hint.fontSize = font;

            // Top right: clock and backpack.
            text.Clear();
            if (dayNight != null) text.Append("<b>").Append(dayNight.ClockText()).Append("</b>").Append(dayNight.IsNight ? "  (night)" : "").Append('\n');
            if (ruler != null && ruler.Backpack != null)
            {
                var pack = ruler.Backpack;
                text.Append($"Wood {pack.Get(ResourceType.Wood)}   Stone {pack.Get(ResourceType.Stone)}   Food {pack.Get(ResourceType.Food)}\n");
                text.Append($"Backpack {pack.Total} / {pack.Capacity}");
            }
            Draw(text.ToString(), box, Corner.TopRight);

            // Bottom middle: what you can do right now.
            string now = null;
            if (ruler != null)
            {
                if (!string.IsNullOrEmpty(ruler.Notice)) now = ruler.Notice;
                else if (ruler.GatherTarget != null) now = $"Hold E to gather {Name(ruler.GatherTarget.Config.resource)}";
            }
            if (now != null) Draw(now, hint, Corner.BottomMiddle);

            if (showControls) Draw(Controls, box, Corner.BottomLeft);
        }

        static string Name(ResourceType type) => type.ToString().ToLowerInvariant();

        enum Corner { TopRight, BottomLeft, BottomMiddle }

        static void Draw(string message, GUIStyle style, Corner corner)
        {
            var content = new GUIContent(message);
            var size = style.CalcSize(content) + new Vector2(10, 6);
            const float margin = 10;
            Vector2 at;
            switch (corner)
            {
                case Corner.TopRight: at = new Vector2(Screen.width - size.x - margin, margin); break;
                case Corner.BottomLeft: at = new Vector2(margin, Screen.height - size.y - margin); break;
                default: at = new Vector2((Screen.width - size.x) / 2f, Screen.height - size.y - Screen.height * 0.12f); break;
            }
            GUI.Box(new Rect(at, size), content, style);
        }
    }
}
