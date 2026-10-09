using System.Collections.Generic;
using KingdomGame.Character;
using KingdomGame.World;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// The throne view. While the ruler sits on the throne the camera pulls far
    /// out and you plan buildings on the grid:
    /// <list type="bullet">
    /// <item>1-5 (or the buttons at the bottom) pick a building; pick it again to put it away.</item>
    /// <item>Left click places it. For walls, hold and drag to place a whole straight line.</item>
    /// <item>Right click: stop placing, or cancel the blueprint under the mouse (you get its resources back).</item>
    /// <item>WASD moves the view, the mouse wheel zooms, E or Esc stands up.</item>
    /// </list>
    /// The world keeps running while you sit there.
    /// </summary>
    public sealed class BuildController : MonoBehaviour
    {
        static readonly Color Good = new Color(0.45f, 1f, 0.55f, 0.55f);
        static readonly Color Bad = new Color(1f, 0.35f, 0.3f, 0.55f);
        static readonly KeyCode[] NumberKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5 };

        Kingdom kingdom;
        RulerController ruler;
        CameraRig rig;
        Camera view;
        CanopyFader canopies;
        ViewConfig viewConfig;

        int selected = -1;
        bool dragging;
        Vector2Int dragStart;
        int enteredFrame;
        readonly List<Vector2Int> placements = new List<Vector2Int>();
        readonly List<bool> valid = new List<bool>();
        readonly List<SpriteRenderer> ghosts = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> outlines = new List<SpriteRenderer>();
        float messageUntil;

        public bool Active { get; private set; }
        public int SelectedIndex => selected;
        public BuildingConfig Selected => selected >= 0 && selected < kingdom.BuildMenu.Count ? kingdom.BuildMenu[selected] : null;
        /// <summary>About whatever is under the mouse, for the info box.</summary>
        public string Hover { get; private set; }
        /// <summary>A short message after an action (like "Placed 6 blueprints").</summary>
        public string Message { get; private set; }
        /// <summary>Set by the HUD each frame while the mouse is over its buttons, so clicks there don't place anything.</summary>
        public bool PointerOverHud { get; set; }

        public void Setup(Kingdom owner, RulerController rulerController, CameraRig cameraRig, CanopyFader fader, ViewConfig look)
        {
            kingdom = owner;
            ruler = rulerController;
            rig = cameraRig;
            canopies = fader;
            viewConfig = look;
            view = rig != null ? rig.GetComponent<Camera>() : Camera.main;
        }

        /// <summary>Sits the ruler on the throne and pulls the camera out.</summary>
        public void Enter()
        {
            if (Active || kingdom == null || kingdom.Throne == null) return;
            Active = true;
            enteredFrame = Time.frameCount;
            selected = -1;
            dragging = false;
            ruler.Sit(kingdom.Throne);
            if (rig != null) rig.FreeLook(kingdom.Throne.Center);
            if (canopies != null && viewConfig != null) canopies.FarAlpha = viewConfig.throneCanopyAlpha;
        }

        /// <summary>Stands the ruler up and brings the camera back to them.</summary>
        public void Leave()
        {
            if (!Active) return;
            Active = false;
            selected = -1;
            dragging = false;
            ShowPlacements(0);
            Hover = null;
            ruler.StandUp(kingdom.Throne);
            if (rig != null) rig.Follow(ruler.transform, false);
            if (canopies != null) canopies.FarAlpha = 1f;
        }

        /// <summary>Picks building number <paramref name="index"/> from the build bar (again = put it away).</summary>
        public void Select(int index)
        {
            selected = selected == index || index < 0 || index >= kingdom.BuildMenu.Count ? -1 : index;
            dragging = false;
        }

        void Say(string text)
        {
            Message = text;
            messageUntil = Time.unscaledTime + 3f;
        }

        void Update()
        {
            if (Time.unscaledTime > messageUntil) Message = null;
            if (!Active || kingdom == null || view == null) return;

            bool firstFrame = Time.frameCount == enteredFrame;
            if (!firstFrame && GameInput.Pressed(KeyCode.Escape))
            {
                if (selected >= 0) Select(-1);
                else { Leave(); return; }
            }
            if (!firstFrame && GameInput.Pressed(KeyCode.E))
            {
                Leave();
                return;
            }

            if (rig != null) rig.Pan(GameInput.Move);
            for (int i = 0; i < NumberKeys.Length && i < kingdom.BuildMenu.Count; i++)
                if (GameInput.Pressed(NumberKeys[i])) Select(i);

            Vector2 mouse = view.ScreenToWorldPoint(GameInput.PointerPosition);
            var b = Selected;
            if (b != null) Placing(b, mouse, firstFrame);
            else Inspecting(mouse);
        }

        void Placing(BuildingConfig b, Vector2 mouse, bool firstFrame)
        {
            // The building's middle follows the mouse.
            var origin = new Vector2Int(Mathf.RoundToInt(mouse.x - b.size.x * 0.5f), Mathf.RoundToInt(mouse.y - b.size.y * 0.5f));

            if (GameInput.AltFirePressed)
            {
                if (dragging) dragging = false;
                else Select(-1);
                ShowPlacements(0);
                return;
            }
            if (!firstFrame && GameInput.FirePressed && !PointerOverHud)
            {
                dragging = true;
                dragStart = origin;
            }

            placements.Clear();
            if (dragging && b.dragToPlaceLines) Line(dragStart, origin, b.size, kingdom.Config.maxLine, placements);
            else placements.Add(dragging ? dragStart : origin);

            valid.Clear();
            string firstProblem = null;
            int good = 0;
            foreach (var cell in placements)
            {
                bool ok = kingdom.CanPlace(b, cell, out string reason);
                valid.Add(ok);
                if (ok) good++;
                else if (firstProblem == null) firstProblem = reason;
            }
            ShowPlacements(PointerOverHud && !dragging ? 0 : placements.Count, b);

            string cost = good > 1
                ? $"{good} x {b.displayName}: {b.woodCost * good} wood, {b.stoneCost * good} stone"
                : $"{b.displayName}: {b.CostText()}";
            Hover = firstProblem != null ? $"{cost}\n<color=#ff8a80>{firstProblem}</color>" : cost;

            if (dragging && (GameInput.FireReleased || !GameInput.FireHeld))
            {
                dragging = false;
                int placed = 0;
                for (int i = 0; i < placements.Count; i++)
                    if (valid[i] && kingdom.PlaceBlueprint(b, placements[i]) != null) placed++;
                if (placed > 0) Say(placed == 1 ? $"Planned: {b.displayName}" : $"Planned {placed} x {b.displayName}");
                else if (firstProblem != null) Say(firstProblem);
            }
        }

        void Inspecting(Vector2 mouse)
        {
            ShowPlacements(0);
            var cell = new Vector2Int(Mathf.FloorToInt(mouse.x), Mathf.FloorToInt(mouse.y));
            var blueprint = kingdom.BlueprintAt(cell);
            if (blueprint != null)
            {
                Hover = Describe(blueprint) + "\nRight click: cancel it (resources go back to storage)";
                if (!PointerOverHud && (GameInput.AltFirePressed || GameInput.Pressed(KeyCode.Delete)))
                    Say(kingdom.Cancel(blueprint));
                return;
            }
            foreach (var storage in kingdom.Storages)
            {
                if (!storage.Cells.Contains(cell)) continue;
                var stock = storage.Stock;
                Hover = $"{storage.name}: {stock.Get(Economy.ResourceType.Wood)} wood, {stock.Get(Economy.ResourceType.Stone)} stone, " +
                        $"{stock.Get(Economy.ResourceType.Food)} food ({stock.Total} / {stock.Capacity})";
                return;
            }
            Hover = null;
        }

        public static string Describe(Blueprint b)
        {
            if (b.Problem != null) return $"{b.Config.displayName}: {b.Problem}";
            if (!b.HasAllResources)
            {
                var parts = new List<string>();
                foreach (var type in new[] { Economy.ResourceType.Wood, Economy.ResourceType.Stone })
                    if (b.Required(type) > 0) parts.Add($"{b.Delivered(type)}/{b.Required(type)} {type.ToString().ToLowerInvariant()}");
                return $"{b.Config.displayName}: builders are bringing resources ({string.Join(", ", parts)})";
            }
            int percent = Mathf.FloorToInt(b.Work / Mathf.Max(0.01f, b.Config.buildSeconds) * 100f);
            return $"{b.Config.displayName}: being built, {percent}% ({b.Builders} builder{(b.Builders == 1 ? "" : "s")})";
        }

        // A straight line of placements from a to b along whichever way is longer, spaced by the building's size.
        static void Line(Vector2Int a, Vector2Int b, Vector2Int size, int max, List<Vector2Int> result)
        {
            var d = b - a;
            bool horizontal = Mathf.Abs(d.x) >= Mathf.Abs(d.y);
            int stepSize = horizontal ? size.x : size.y;
            int length = (horizontal ? Mathf.Abs(d.x) : Mathf.Abs(d.y)) / Mathf.Max(1, stepSize);
            int sign = (horizontal ? d.x : d.y) >= 0 ? 1 : -1;
            for (int i = 0; i <= length && result.Count < max; i++)
                result.Add(horizontal ? new Vector2Int(a.x + i * sign * stepSize, a.y) : new Vector2Int(a.x, a.y + i * sign * stepSize));
        }

        void ShowPlacements(int count, BuildingConfig b = null)
        {
            while (ghosts.Count < count)
            {
                ghosts.Add(MakeRenderer("Placing", SortOrder.Markers));
                outlines.Add(MakeRenderer("Placing outline", SortOrder.Markers + 1));
            }
            for (int i = 0; i < ghosts.Count; i++)
            {
                bool show = i < count && b != null;
                ghosts[i].enabled = show;
                outlines[i].enabled = show;
                if (!show) continue;
                var center = (Vector2)placements[i] + (Vector2)b.size * 0.5f;
                var tint = valid[i] ? Good : Bad;
                ghosts[i].sprite = kingdom.LookOf(b);
                ghosts[i].transform.position = center;
                ghosts[i].color = tint;
                outlines[i].sprite = kingdom.CellMarker;
                outlines[i].transform.position = center;
                outlines[i].transform.localScale = new Vector3(b.size.x, b.size.y, 1f);
                outlines[i].color = new Color(tint.r, tint.g, tint.b, 0.9f);
            }
        }

        SpriteRenderer MakeRenderer(string rendererName, int order)
        {
            var go = new GameObject(rendererName);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order;
            r.enabled = false;
            return r;
        }
    }
}
