using KingdomGame.Building;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// The rooms guide (I): every kind of room you can make, what makes one and what it does, and how many your
    /// kingdom has. A room is a space closed in by walls and doors; what's in it decides its kind (Building/RoomTypes.cs).
    /// </summary>
    public sealed class RoomsPanel : MonoBehaviour
    {
        Kingdom kingdom;
        bool open;
        Vector2 scroll;
        GUIStyle title, text, small;

        public void Setup(Kingdom owner) => kingdom = owner;

        void Toggle()
        {
            open = !open;
            UiFocus.PanelOpen = open;
        }

        void OnDestroy()
        {
            if (open) UiFocus.PanelOpen = false;
        }

        void Update()
        {
            if ((!UiFocus.PanelOpen || open) && GameInput.Pressed(KeyCode.I)) Toggle();
            else if (open && GameInput.Pressed(KeyCode.Escape)) Toggle();
        }

        void OnGUI()
        {
            if (!open || kingdom == null) return;
            int font = Mathf.Max(12, Screen.height / 55);
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { richText = true };
                text = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
                small = new GUIStyle(text);
            }
            title.fontSize = Mathf.RoundToInt(font * 1.4f);
            text.fontSize = font;
            small.fontSize = Mathf.RoundToInt(font * 0.85f);

            float w = Mathf.Min(Screen.width - 40f, font * 50f), h = Mathf.Min(Screen.height - 40f, font * 42f);
            var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none);
            float x = panel.x + font, y = panel.y + font * 0.6f, inner = w - font * 2f;
            GUI.Label(new Rect(x, y, inner, font * 2f), $"<b>Rooms</b>   <size={font}>I or Esc closes this.</size>", title);
            y += font * 2.2f;
            GUI.Label(new Rect(x, y, inner, font * 3.2f),
                $"A room is a space closed in all round by walls and doors (up to {Kingdom.MaxRoomSize} tiles; give it a door so " +
                "villagers can get in). What you put in it decides what kind of room it is, and its name shows on the floor when you " +
                "build from the throne. The first kind on this list that fits wins: keep beds out of a dining hall, and weapon racks out of a training room.", small);
            y += font * 3.4f;

            var kinds = RoomTypes.Guide;
            float diningBonus = kingdom.VillagerSettings != null ? kingdom.VillagerSettings.contentBonus : 0.1f;
            float row = font * 3.3f;
            var view = new Rect(x, y, inner, panel.yMax - y - font * 0.6f);
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, inner - font, row * kinds.Length));
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                float ry = i * row;
                int have = kingdom.CountRooms(kind);
                string count = have > 0 ? $"<color=#9be29b>you have {have}</color>" : "<color=#8a8070>none yet</color>";
                GUI.Label(new Rect(0, ry, inner - font, font * 1.5f), $"<b>{RoomTypes.Name(kind)}</b>   {count}", text);
                GUI.Label(new Rect(0, ry + font * 1.35f, inner - font, row - font * 1.35f),
                    $"<color=#b8ad98>Needs {RoomTypes.How(kind)}.</color>  {Capital(RoomTypes.Does(kind, kingdom.Config, diningBonus))}", small);
            }
            GUI.EndScrollView();
        }

        static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
