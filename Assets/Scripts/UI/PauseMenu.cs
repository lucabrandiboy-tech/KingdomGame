using KingdomGame.Building;
using KingdomGame.Zombies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomGame.UI
{
    /// <summary>
    /// Esc pauses the game (not while sitting on the throne: there Esc stands you
    /// up): resume, start again, go back to the main menu or quit. When the ruler
    /// falls in survival it shows how long you lasted instead.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        BuildController builder;
        SurvivalDirector survival;
        GUIStyle title, text, button;

        public bool IsOpen { get; private set; }

        public void Setup(BuildController build, SurvivalDirector director)
        {
            builder = build;
            survival = director;
        }

        void Update()
        {
            if (survival != null && survival.Now == SurvivalDirector.Phase.Over)
            {
                if (IsOpen) Close();
                return;
            }
            if (!GameInput.Pressed(KeyCode.Escape)) return;
            if (builder != null && (builder.Active || builder.LeftFrame == Time.frameCount)) return;
            if (IsOpen) Close();
            else Open();
        }

        void Open()
        {
            IsOpen = true;
            Time.timeScale = 0f;
        }

        void Close()
        {
            IsOpen = false;
            Time.timeScale = 1f;
        }

        void OnDestroy()
        {
            if (IsOpen) Time.timeScale = 1f;
        }

        /// <summary>Starts this game again from the beginning (same mode).</summary>
        public static void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Back to the main menu (or starts again, if there's no menu scene in the build).</summary>
        public static void ToMainMenu()
        {
            Time.timeScale = 1f;
            if (Application.CanStreamedLevelBeLoaded(GameSession.MenuScene)) SceneManager.LoadScene(GameSession.MenuScene);
            else Restart();
        }

        public static void Quit()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void OnGUI()
        {
            bool over = survival != null && survival.Now == SurvivalDirector.Phase.Over;
            if (!IsOpen && !over) return;
            int font = Mathf.Max(14, Screen.height / 40);
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
                text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { richText = true };
            }
            title.fontSize = font * 2;
            text.fontSize = button.fontSize = font;

            // Darken the game behind the menu.
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, over ? 0.55f : 0.45f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;

            float width = font * 22f, row = font * 2.6f;
            float x = (Screen.width - width) / 2f;
            float y = Screen.height * 0.28f;
            if (over)
            {
                GUI.Label(new Rect(x - width, y, width * 3f, font * 3f), "<b><color=#e05040>You have fallen</color></b>", title);
                y += font * 3.2f;
                int minutes = Mathf.FloorToInt(survival.SecondsSurvived / 60f), seconds = Mathf.FloorToInt(survival.SecondsSurvived % 60f);
                string waves = survival.Wave <= 1 ? "before the second wave" : $"on wave {survival.Wave}";
                GUI.Label(new Rect(x - width * 0.5f, y, width * 2f, font * 3.5f),
                    $"You fell {waves}, after {minutes}:{seconds:00}, with {survival.Kills} zombies killed.", text);
                y += font * 4f;
                if (GUI.Button(new Rect(x, y, width, row), "<b>Try again</b>", button)) Restart();
                y += row + font * 0.6f;
                if (GUI.Button(new Rect(x, y, width, row), "Main menu", button)) ToMainMenu();
                return;
            }

            GUI.Label(new Rect(x - width, y, width * 3f, font * 3f), "<b>Paused</b>", title);
            y += font * 3.6f;
            if (GUI.Button(new Rect(x, y, width, row), "<b>Resume</b>   (Esc)", button)) Close();
            y += row + font * 0.6f;
            if (GUI.Button(new Rect(x, y, width, row), "Start again", button)) Restart();
            y += row + font * 0.6f;
            if (GUI.Button(new Rect(x, y, width, row), "Main menu", button)) ToMainMenu();
            y += row + font * 0.6f;
            if (GUI.Button(new Rect(x, y, width, row), "Quit the game", button)) Quit();
        }
    }
}
