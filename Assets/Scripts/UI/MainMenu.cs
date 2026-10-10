using UnityEngine;
using UnityEngine.SceneManagement;

namespace KingdomGame.UI
{
    /// <summary>
    /// The first screen: carry on with a saved game, start survival (endless zombies) or
    /// free build (no zombies), play online (host your own game for friends to join, or
    /// join someone's), or quit. Simple placeholder buttons; a proper menu comes with the
    /// themed UI. Started with -server, the game goes straight to being a dedicated server.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        GUIStyle title, subtitle, text, button, field;
        string survivalSave, freeBuildSave, hostedSurvival, hostedFreeBuild;
        bool online;
        string playerName, address, problem;

        void Start()
        {
            Time.timeScale = 1f;
            if (GameSession.Role == NetRole.Server)
            {
                SceneManager.LoadScene(GameSession.GameScene);
                return;
            }
            GameSession.Role = NetRole.Offline;
            survivalSave = World.SaveFiles.Describe(GameMode.Survival);
            freeBuildSave = World.SaveFiles.Describe(GameMode.FreeBuild);
            hostedSurvival = World.SaveFiles.Describe(GameMode.Survival, true);
            hostedFreeBuild = World.SaveFiles.Describe(GameMode.FreeBuild, true);
            playerName = GameSession.PlayerName;
            address = PlayerPrefs.GetString("JoinAddress", "127.0.0.1");
        }

        void OnGUI()
        {
            int font = Mathf.Max(14, Screen.height / 38);
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
                subtitle = new GUIStyle(title);
                text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, richText = true, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { richText = true, wordWrap = true };
                field = new GUIStyle(GUI.skin.textField);
            }
            title.fontSize = font * 3;
            subtitle.fontSize = Mathf.RoundToInt(font * 1.1f);
            text.fontSize = Mathf.RoundToInt(font * 0.8f);
            button.fontSize = field.fontSize = font;

            float width = font * 24f, row = font * 3.4f;
            float x = (Screen.width - width) / 2f;
            bool saves = survivalSave != null || freeBuildSave != null;
            float y = Screen.height * (online || saves ? 0.06f : 0.16f);
            GUI.Label(new Rect(0, y, Screen.width, font * 4f), "<b><color=#e8c27a>KINGDOM</color></b>", title);
            y += font * 4f;
            GUI.Label(new Rect(0, y, Screen.width, font * 1.6f), "<color=#b8ad98>Build your castle. Hold the line.</color>", subtitle);
            y += font * 2.6f;
            if (online) Online(x, y, width, row, font);
            else Offline(x, y, width, row, font);
        }

        string Small(int font) => "<size=" + Mathf.RoundToInt(font * 0.75f) + ">";

        void Offline(float x, float y, float width, float row, int font)
        {
            string small = Small(font);
            if (survivalSave != null)
            {
                if (GUI.Button(new Rect(x, y, width, row), $"<b>Continue survival</b>\n{small}{survivalSave}</size>", button)) Play(GameMode.Survival, true, NetRole.Offline);
                y += row + font * 0.7f;
            }
            if (freeBuildSave != null)
            {
                if (GUI.Button(new Rect(x, y, width, row), $"<b>Continue free build</b>\n{small}{freeBuildSave}</size>", button)) Play(GameMode.FreeBuild, true, NetRole.Offline);
                y += row + font * 0.7f;
            }
            if (GUI.Button(new Rect(x, y, width, row), $"<b>{(survivalSave != null ? "New survival game" : "Survival")}</b>\n{small}" +
                    (survivalSave != null ? "replaces your saved one once it's played a minute" : "endless waves of zombies") + "</size>", button))
                Play(GameMode.Survival, false, NetRole.Offline);
            y += row + font * 0.7f;
            if (GUI.Button(new Rect(x, y, width, row), $"<b>{(freeBuildSave != null ? "New free build" : "Free build")}</b>\n{small}" +
                    (freeBuildSave != null ? "replaces your saved one once it's played a minute" : "no zombies: build in peace") + "</size>", button))
                Play(GameMode.FreeBuild, false, NetRole.Offline);
            y += row + font * 0.7f;
            if (GUI.Button(new Rect(x, y, width, row), $"<b>Play online</b>\n{small}host a game for friends, or join one</size>", button))
            {
                online = true;
                problem = null;
            }
            y += row + font * 0.7f;
            if (GUI.Button(new Rect(x, y, width, font * 2.2f), "Quit", button)) PauseMenu.Quit();
        }

        void Online(float x, float y, float width, float row, int font)
        {
            string small = Small(font);
            float label = font * 7f;
            GUI.Label(new Rect(x, y, label, font * 1.8f), "Your name:", text);
            playerName = GUI.TextField(new Rect(x + label, y, width - label, font * 1.8f), playerName ?? "", Networking.Protocol.MaxNameLength, field);
            y += font * 2.6f;

            // Hosting: your own game, with friends able to join it.
            GUI.Label(new Rect(x - font * 4f, y, width + font * 8f, font * 1.6f), "<b><color=#e8c27a>Host a game</color></b> <color=#8f877a>(you play, friends join you)</color>", text);
            y += font * 1.8f;
            float half = (width - font * 0.6f) / 2f;
            if (GUI.Button(new Rect(x, y, half, row), $"<b>{(hostedSurvival != null ? "Continue survival" : "Host survival")}</b>\n{small}{hostedSurvival ?? "zombie waves"}</size>", button))
                Play(GameMode.Survival, hostedSurvival != null, NetRole.Host);
            if (GUI.Button(new Rect(x + half + font * 0.6f, y, half, row), $"<b>{(hostedFreeBuild != null ? "Continue free build" : "Host free build")}</b>\n{small}{hostedFreeBuild ?? "no zombies"}</size>", button))
                Play(GameMode.FreeBuild, hostedFreeBuild != null, NetRole.Host);
            y += row + font * 0.5f;
            if (hostedSurvival != null || hostedFreeBuild != null)
            {
                if (hostedSurvival != null && GUI.Button(new Rect(x, y, half, font * 1.8f), $"{small}New hosted survival</size>", button)) Play(GameMode.Survival, false, NetRole.Host);
                if (hostedFreeBuild != null && GUI.Button(new Rect(x + half + font * 0.6f, y, half, font * 1.8f), $"{small}New hosted free build</size>", button)) Play(GameMode.FreeBuild, false, NetRole.Host);
                y += font * 2.2f;
            }
            y += font * 0.8f;

            // Joining someone's game.
            GUI.Label(new Rect(x - font * 4f, y, width + font * 8f, font * 1.6f), "<b><color=#e8c27a>Join a game</color></b> <color=#8f877a>(their address, from their Esc menu)</color>", text);
            y += font * 1.8f;
            address = GUI.TextField(new Rect(x, y, width - font * 7.6f, font * 2.2f), address ?? "", 100, field);
            if (GUI.Button(new Rect(x + width - font * 7f, y, font * 7f, font * 2.2f), "<b>Join</b>", button))
            {
                if (!GameSession.SetAddress(address)) problem = "That address isn't right: it's like 192.168.1.20 or 81.2.69.160:7777";
                else
                {
                    PlayerPrefs.SetString("JoinAddress", address.Trim());
                    Play(GameMode.FreeBuild, false, NetRole.Client);
                }
            }
            y += font * 2.8f;
            if (problem != null)
            {
                GUI.Label(new Rect(x - font * 4f, y, width + font * 8f, font * 2.4f), $"<color=#ff8a80>{problem}</color>", text);
                y += font * 2.4f;
            }
            if (GUI.Button(new Rect(x, y, width, font * 2.2f), "Back", button)) online = false;
            y += font * 3f;
            GUI.Label(new Rect(x - font * 4f, y, width + font * 8f, font * 5f),
                "<color=#8f877a>Friends on your home network join with your computer's address. Over the internet you also need " +
                "the port forwarded on your router: see docs/ONLINE-TEST.md. Joined players walk, chat and get a kingdom spot; " +
                "building together comes in the next update.</color>", text);
        }

        void Play(GameMode mode, bool continueSaved, NetRole role)
        {
            if (role != NetRole.Offline)
            {
                GameSession.PlayerName = playerName;
                PlayerPrefs.Save();
            }
            GameSession.Chosen = mode;
            GameSession.LoadSave = continueSaved;
            GameSession.Role = role;
            SceneManager.LoadScene(GameSession.GameScene);
        }
    }
}
