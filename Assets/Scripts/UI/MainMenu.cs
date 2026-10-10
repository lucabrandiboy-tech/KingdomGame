using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomGame.UI
{
    /// <summary>
    /// The first screen: pick survival (endless zombies) or free build (no zombies),
    /// or quit. Simple placeholder buttons; a proper menu comes with the themed UI.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        GUIStyle title, subtitle, text, button;

        void Start() => Time.timeScale = 1f;

        void OnGUI()
        {
            int font = Mathf.Max(14, Screen.height / 38);
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
                subtitle = new GUIStyle(title);
                text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, richText = true, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { richText = true, wordWrap = true };
            }
            title.fontSize = font * 3;
            subtitle.fontSize = Mathf.RoundToInt(font * 1.1f);
            text.fontSize = Mathf.RoundToInt(font * 0.8f);
            button.fontSize = font;

            float width = font * 24f, row = font * 3.4f;
            float x = (Screen.width - width) / 2f;
            float y = Screen.height * 0.16f;
            GUI.Label(new Rect(0, y, Screen.width, font * 4f), "<b><color=#e8c27a>KINGDOM</color></b>", title);
            y += font * 4f;
            GUI.Label(new Rect(0, y, Screen.width, font * 1.6f), "<color=#b8ad98>Build your castle. Hold the line.</color>", subtitle);
            y += font * 3.4f;

            if (GUI.Button(new Rect(x, y, width, row), "<b>Survival</b>\n<size=" + Mathf.RoundToInt(font * 0.75f) + ">endless waves of zombies</size>", button))
                Play(GameMode.Survival);
            y += row + font * 0.7f;
            if (GUI.Button(new Rect(x, y, width, row), "<b>Free build</b>\n<size=" + Mathf.RoundToInt(font * 0.75f) + ">no zombies: build in peace</size>", button))
                Play(GameMode.FreeBuild);
            y += row + font * 0.7f;
            if (GUI.Button(new Rect(x, y, width, font * 2.2f), "Quit", button)) PauseMenu.Quit();
            y += font * 3.4f;

            GUI.Label(new Rect(x - font * 4f, y, width + font * 8f, font * 6f),
                "<color=#8f877a>Survival: build walls, doors and rooms before night falls. The dead come in waves that never stop, " +
                "and every wave is bigger. Beds bring survivors to help. Co-op comes later.</color>", text);
        }

        static void Play(GameMode mode)
        {
            GameSession.Chosen = mode;
            SceneManager.LoadScene(GameSession.GameScene);
        }
    }
}
