using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>The Settings screen, in the main menu and the pause menu: graphics and sound (see <see cref="GameSettings"/>).</summary>
    public static class SettingsPanel
    {
        /// <summary>
        /// Draws it from <paramref name="y"/> down, <paramref name="width"/> wide from <paramref name="x"/>. Returns true
        /// when Back is clicked.
        /// </summary>
        public static bool Draw(float x, float y, float width, int font, GUIStyle title, GUIStyle text, GUIStyle button)
        {
            GUI.Label(new Rect(x - width, y, width * 3f, font * 2.6f), "<b>Settings</b>", title);
            y += font * 3.2f;

            GUI.Label(new Rect(x, y, width, font * 1.6f), "<b>Graphics</b>", text);
            y += font * 1.9f;
            var quality = GameSettings.Quality;
            float gap = font * 0.5f, third = (width - gap * 2f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var g = (GameSettings.Graphics)i;
                string label = g == quality ? $"<b><color=#e8c27a>{GameSettings.Name(g)}</color></b>" : GameSettings.Name(g);
                if (GUI.Button(new Rect(x + i * (third + gap), y, third, font * 2.2f), label, button)) GameSettings.Quality = g;
            }
            y += font * 2.6f;
            GUI.Label(new Rect(x - width * 0.25f, y, width * 1.5f, font * 1.6f), $"<color=#b8ad98>{GameSettings.Describe(quality)}</color>", text);
            y += font * 2.6f;

            y = Slider(x, y, width, font, text, "Sound", GameSettings.Volume, v => GameSettings.Volume = v);
            y = Slider(x, y, width, font, text, "Background sounds (wind, night, birds)", GameSettings.Ambience, v => GameSettings.Ambience = v);

            y += font * 0.6f;
            return GUI.Button(new Rect(x, y, width, font * 2.4f), "<b>Back</b>", button);
        }

        static float Slider(float x, float y, float width, int font, GUIStyle text, string name, float value, System.Action<float> set)
        {
            GUI.Label(new Rect(x, y, width, font * 1.6f), $"<b>{name}</b>: {Mathf.RoundToInt(value * 100f)}%", text);
            y += font * 1.8f;
            float changed = GUI.HorizontalSlider(new Rect(x, y + font * 0.3f, width, font * 1.2f), value, 0f, 1f);
            if (!Mathf.Approximately(changed, value)) set(changed);
            return y + font * 2.2f;
        }
    }
}
