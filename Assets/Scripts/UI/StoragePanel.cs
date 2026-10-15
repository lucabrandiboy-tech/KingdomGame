using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// A storehouse or chest (E at one): put everything in your backpack into it, or take
    /// resources out into your backpack (10, 50 or all of a kind, as much as fits). What
    /// builders have already set aside for a blueprint can't be taken. E, Esc or walking
    /// away closes it.
    /// </summary>
    public sealed class StoragePanel : MonoBehaviour
    {
        Storage storage;
        RulerController ruler;
        int openedFrame = -1;
        string message;
        GUIStyle title, text, button;
        readonly System.Collections.Generic.List<ResourceType> shown = new System.Collections.Generic.List<ResourceType>();

        public bool IsOpen => storage != null;

        public void Setup(RulerController rulerController) => ruler = rulerController;

        public void Open(Storage store)
        {
            if (store == null || UiFocus.PanelOpen) return;
            storage = store;
            openedFrame = Time.frameCount;
            message = null;
            UiFocus.PanelOpen = true;
        }

        void Close()
        {
            storage = null;
            UiFocus.PanelOpen = false;
        }

        void OnDestroy()
        {
            if (IsOpen) UiFocus.PanelOpen = false;
        }

        void Update()
        {
            if (!IsOpen) return;
            // Gone (broken, torn down), or the ruler walked off or fell.
            if (storage == null || ruler == null || ruler.Dead || BuildingLook.Distance(storage.Cells, ruler.transform.position) > 2.5f)
            {
                Close();
                return;
            }
            if (Time.frameCount == openedFrame) return;
            if (GameInput.Pressed(KeyCode.Escape) || GameInput.Pressed(KeyCode.E)) Close();
        }

        void PutAllIn()
        {
            var pack = ruler.Backpack;
            int stored = 0, kept = 0;
            for (int i = 0; i < Inventory.TypeCount; i++)
            {
                var type = (ResourceType)i;
                int have = pack.Get(type);
                if (have <= 0) continue;
                int put = storage.Deposit(type, have);
                pack.TryRemove(type, put);
                stored += put;
                kept += have - put;
            }
            message = kept > 0 ? $"Stored {stored}. It's full: {kept} still in your backpack" : stored > 0 ? $"Stored {stored}" : "Your backpack is empty";
        }

        void Take(ResourceType type, int want)
        {
            var pack = ruler.Backpack;
            int n = Mathf.Min(want, storage.Available(type), pack.SpaceLeft);
            if (n <= 0)
            {
                message = pack.SpaceLeft <= 0 ? "Your backpack is full" : $"No {ResourceText.Name(type)} to take";
                return;
            }
            if (!storage.Stock.TryRemove(type, n)) return;
            int added = pack.Add(type, n);
            if (added < n) storage.Deposit(type, n - added);   // (didn't all fit after all)
            message = $"Took {added} {ResourceText.Name(type)}";
        }

        void OnGUI()
        {
            if (!IsOpen || ruler == null || ruler.Backpack == null) return;
            int font = Mathf.Max(12, Screen.height / 52);
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { richText = true };
                text = new GUIStyle(GUI.skin.label) { richText = true };
                button = new GUIStyle(GUI.skin.button) { richText = true };
            }
            title.fontSize = Mathf.RoundToInt(font * 1.3f);
            text.fontSize = button.fontSize = font;

            float row = font * 2.1f;
            // Wood, stone and food always; the rest only when there's some here.
            shown.Clear();
            for (int i = 0; i < Inventory.TypeCount; i++)
                if (i < 3 || storage.Stock.Get((ResourceType)i) > 0) shown.Add((ResourceType)i);
            float w = Mathf.Min(Screen.width - 40f, font * 34f), h = font * 9f + row * shown.Count;
            var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none);
            float x = panel.x + font, y = panel.y + font * 0.6f, inner = w - font * 2f;
            var stock = storage.Stock;
            var pack = ruler.Backpack;
            string name = storage.Config != null ? storage.Config.displayName : "Storehouse";
            GUI.Label(new Rect(x, y, inner, font * 2f), $"<b>{name}</b>   <size={font}>{stock.Total} / {stock.Capacity}   (E or Esc closes)</size>", title);
            y += font * 2.2f;
            GUI.Label(new Rect(x, y, inner, font * 1.6f), $"Backpack: {ResourceText.Describe(pack)}  ({pack.Total} / {pack.Capacity})", text);
            y += font * 1.8f;
            if (GUI.Button(new Rect(x, y, inner, font * 2f), "<b>Put everything in my backpack in here</b>", button)) PutAllIn();
            y += font * 2.6f;

            foreach (var type in shown)
            {
                int have = stock.Get(type), free = storage.Available(type);
                string kept = have > free ? $" <color=#b8ad98>({have - free} set aside for building)</color>" : "";
                string label = ResourceText.Name(type);
                label = char.ToUpperInvariant(label[0]) + label.Substring(1);
                GUI.Label(new Rect(x, y, inner * 0.5f, row), $"<b>{label}</b>: {have}{kept}", text);
                float bx = x + inner * 0.52f, bw = (inner * 0.48f - font) / 3f;
                GUI.enabled = free > 0 && pack.SpaceLeft > 0;
                if (GUI.Button(new Rect(bx, y, bw, font * 1.8f), "Take 10", button)) Take(type, 10);
                if (GUI.Button(new Rect(bx + bw + font * 0.5f, y, bw, font * 1.8f), "Take 50", button)) Take(type, 50);
                if (GUI.Button(new Rect(bx + (bw + font * 0.5f) * 2f, y, bw, font * 1.8f), "Take all", button)) Take(type, int.MaxValue);
                GUI.enabled = true;
                y += row;
            }
            if (message != null) GUI.Label(new Rect(x, y + font * 0.3f, inner, font * 1.6f), $"<color=#f0d08a>{message}</color>", text);
        }
    }
}
