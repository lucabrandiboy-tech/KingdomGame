using KingdomGame.Building;
using KingdomGame.Networking;
using KingdomGame.World;
using KingdomGame.Zombies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomGame.UI
{
    /// <summary>
    /// Esc pauses the game (not while sitting on the throne: there Esc stands you
    /// up): resume, save, load the last save, start again, go back to the main menu
    /// (saving first) or quit. When the ruler falls in survival it shows how long
    /// you lasted instead.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        BuildController builder;
        SurvivalDirector survival;
        SaveSystem saves;
        NetHost host;
        NetClient client;
        GUIStyle title, text, button;
        string saveMessage;
        bool confirmLoad, settings;

        public bool IsOpen { get; private set; }

        /// <summary>Online (<paramref name="hosting"/> or <paramref name="joined"/>) the world doesn't stop while the menu is open.</summary>
        public void Setup(BuildController build, SurvivalDirector director, SaveSystem saveSystem, NetHost hosting, NetClient joined)
        {
            builder = build;
            survival = director;
            saves = saveSystem;
            host = hosting;
            client = joined;
        }

        bool Online => host != null || client != null;

        void Update()
        {
            if (survival != null && survival.Now == SurvivalDirector.Phase.Over)
            {
                if (IsOpen) Close();
                return;
            }
            if (!GameInput.Pressed(KeyCode.Escape) || UiFocus.TakesEscape) return;
            if (builder != null && (builder.Active || builder.LeftFrame == Time.frameCount)) return;
            if (IsOpen) Close();
            else Open();
        }

        void Open()
        {
            IsOpen = true;
            if (!Online) Time.timeScale = 0f;
            saveMessage = null;
            confirmLoad = settings = false;
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
            GameSession.LoadSave = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Throws this game away and loads the saved one of the same mode.</summary>
        static void LoadSaved(GameMode mode)
        {
            Time.timeScale = 1f;
            GameSession.Chosen = mode;
            GameSession.LoadSave = true;   // (a hosted game stays hosted: the role is kept)
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        /// <summary>Back to the main menu (or starts again, if there's no menu scene in the build).</summary>
        public static void ToMainMenu()
        {
            Time.timeScale = 1f;
            GameSession.Role = NetRole.Offline;
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
            float y = Screen.height * 0.2f;
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
                if (saves != null && saves.HasSave)
                {
                    if (GUI.Button(new Rect(x, y, width, row), "Load the last save", button)) LoadSaved(saves.Mode);
                    y += row + font * 0.6f;
                }
                if (GUI.Button(new Rect(x, y, width, row), "Main menu", button)) ToMainMenu();
                return;
            }

            if (settings)
            {
                if (SettingsPanel.Draw(x, Screen.height * 0.18f, width, font, title, text, button)) settings = false;
                return;
            }
            GUI.Label(new Rect(x - width, y, width * 3f, font * 3f), Online ? "<b>Menu</b>" : "<b>Paused</b>", title);
            y += font * 3.6f;
            if (GUI.Button(new Rect(x, y, width, row), "<b>Resume</b>   (Esc)", button)) Close();
            y += row + font * 0.6f;
            if (GUI.Button(new Rect(x, y, width, row), "Settings (graphics, sound)", button)) settings = true;
            y += row + font * 0.6f;
            if (client != null)
            {
                // In someone else's game: nothing to save here (it's theirs).
                if (client.Status != null) GUI.Label(new Rect(x - width * 0.5f, y, width * 2f, font * 2.4f), client.Status, text);
                y += font * 2.6f;
                if (GUI.Button(new Rect(x, y, width, row), "Leave the game", button)) client.Leave();
                y += row + font * 0.6f;
                if (GUI.Button(new Rect(x, y, width, row), "Quit the game", button)) Quit();
                return;
            }
            if (host != null)
            {
                string how = host.HowToJoin();
                if (how != null) GUI.Label(new Rect(x - width * 0.6f, y, width * 2.2f, font * 4.2f), how, text);
                y += font * 4.4f;
            }
            if (saves != null)
            {
                if (GUI.Button(new Rect(x, y, width, row), "Save the game", button))
                {
                    saveMessage = saves.Save();
                    confirmLoad = false;
                }
                y += row + font * 0.6f;
                if (saves.HasSave)
                {
                    string load = confirmLoad ? "<color=#f0b060>Sure? What isn't saved is lost. Click again</color>" : "Load the last save";
                    if (GUI.Button(new Rect(x, y, width, row), load, button))
                    {
                        if (confirmLoad) LoadSaved(saves.Mode);
                        else confirmLoad = true;
                    }
                    y += row + font * 0.6f;
                }
            }
            if (host == null)
            {
                if (GUI.Button(new Rect(x, y, width, row), "Start again", button)) Restart();
                y += row + font * 0.6f;
            }
            if (GUI.Button(new Rect(x, y, width, row), saves != null ? (host != null ? "Save, stop hosting, main menu" : "Save and go to the main menu") : "Main menu", button))
            {
                // Not a brand-new game over the saved one, nor a game whose save couldn't be loaded (see SaveSystem).
                if (saves != null && saves.SafeToOverwrite()) saves.Save(true);
                ToMainMenu();
            }
            y += row + font * 0.6f;
            if (GUI.Button(new Rect(x, y, width, row), "Quit the game", button)) Quit();
            y += row + font * 0.6f;
            if (saveMessage != null) GUI.Label(new Rect(x - width * 0.5f, y, width * 2f, font * 2f), $"<color=#e8dcc0>{saveMessage}</color>", text);
        }
    }
}
