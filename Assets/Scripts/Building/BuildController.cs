using System.Collections.Generic;
using KingdomGame.Character;
using KingdomGame.Progression;
using KingdomGame.World;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// The throne view. While the ruler sits on the throne the camera pulls far
    /// out and you plan buildings on the grid:
    /// <list type="bullet">
    /// <item>The build bar has tabs (walls and doors, floors, furniture, lights, storage): click one, or Tab for the next.
    /// Each age's buildings are on a page of their own: [ and ] (or the arrows) turn the pages. Things not
    /// researched yet are shown greyed out.</item>
    /// <item>1-9 (or the buttons) pick something on the tab; pick it again to put it away. R turns it.</item>
    /// <item>Left click places it. Walls: hold and drag for a straight line. Floors: drag for a whole area.</item>
    /// <item>Right click: stop placing; with nothing picked, cancel the blueprint under the mouse or tear down what's built there (half its cost back).</item>
    /// <item>WASD moves the view, the mouse wheel zooms, E or Esc stands up.</item>
    /// <item>Over a house: F looks inside it to furnish it, U upgrades it.</item>
    /// </list>
    /// The same build bar furnishes a house from inside (<see cref="Furnish"/>): B in a house,
    /// or F over it on the throne. Then only what fits in a house is on it.
    /// The world keeps running while you sit there.
    /// </summary>
    public sealed class BuildController : MonoBehaviour
    {
        static readonly Color Good = new Color(0.45f, 1f, 0.55f, 0.55f);
        static readonly Color Bad = new Color(1f, 0.35f, 0.3f, 0.55f);
        static readonly KeyCode[] NumberKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9,
        };
        /// <summary>Most blueprints one floor drag places.</summary>
        const int MaxArea = 400;

        Kingdom kingdom;
        RulerController ruler;
        CameraRig rig;
        Camera view;
        CanopyFader canopies;
        ViewConfig viewConfig;

        BuildCategory category = BuildCategory.Walls;
        readonly List<BuildingConfig> items = new List<BuildingConfig>();
        readonly List<BuildCategory> categories = new List<BuildCategory>();
        int agePage;
        int shownAge = -1;
        int selected = -1;
        int rotation;
        bool turnedByHand;   // R was pressed: don't turn doors to fit the walls by themselves
        bool dragging;
        Vector2Int dragStart;
        int enteredFrame;
        readonly List<Vector2Int> placements = new List<Vector2Int>();
        readonly List<bool> valid = new List<bool>();
        readonly List<SpriteRenderer> ghosts = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> outlines = new List<SpriteRenderer>();
        float messageUntil;
        Place furnishing;          // the house inside or mine being built in, or null
        bool furnishFromThrone;    // ...looked into from the throne (Esc goes back there)

        public bool Active { get; private set; }
        /// <summary>The house inside or mine being built in, or null.</summary>
        public Place Furnishing => furnishing;
        /// <summary>The frame the ruler last stood up (so the Esc that did it doesn't also open the pause menu).</summary>
        public int LeftFrame { get; private set; } = -1;
        /// <summary>The build bar's tabs: only the ones that have something on them.</summary>
        public IReadOnlyList<BuildCategory> Categories => categories;
        public BuildCategory Category => category;
        /// <summary>What's on the open tab, on the open age page.</summary>
        public IReadOnlyList<BuildingConfig> Items => items;
        /// <summary>The age whose buildings the build bar shows.</summary>
        public int AgePage => agePage;
        /// <summary>The last age page you can turn to (the kingdom's age; every age in free build).</summary>
        public int LastAgePage => Research.Current != null ? Research.Current.Age : 0;
        // The page the bar opens on: the newest age (free build: the first, since every age is open).
        int OpeningPage => Research.Current != null && Research.Current.Mode == Research.Unlocking.Everything ? 0 : LastAgePage;
        public int SelectedIndex => selected;
        public BuildingConfig Selected => selected >= 0 && selected < items.Count ? items[selected] : null;
        /// <summary>Which way the building being placed faces (R turns it).</summary>
        public int Rotation => rotation;
        /// <summary>About whatever is under the mouse, for the info box.</summary>
        public string Hover { get; private set; }
        /// <summary>A short message after an action (like "Placed 6 blueprints").</summary>
        public string Message { get; private set; }
        /// <summary>Set by the HUD each frame while the mouse is over its buttons, so clicks there don't place anything.</summary>
        public bool PointerOverHud { get; set; }
        /// <summary>The build bar's search: when not empty, it shows what matches from every tab and every age reached.</summary>
        public string Search { get; private set; } = "";
        /// <summary>How many things on the open tab and page aren't shown because they still need researching.</summary>
        public int HiddenLocked { get; private set; }

        /// <summary>Whether the build bar shows things that still need researching (greyed). Kept on this computer.</summary>
        public bool ShowLocked
        {
            get => PlayerPrefs.GetInt("ShowLocked", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("ShowLocked", value ? 1 : 0);
                RefreshItems();
            }
        }

        /// <summary>Searches the build bar (from the HUD's search box).</summary>
        public void SetSearch(string text)
        {
            text = text ?? "";
            if (text == Search) return;
            Search = text;
            RefreshItems();
        }

        public void Setup(Kingdom owner, RulerController rulerController, CameraRig cameraRig, CanopyFader fader, ViewConfig look)
        {
            kingdom = owner;
            ruler = rulerController;
            rig = cameraRig;
            canopies = fader;
            viewConfig = look;
            view = rig != null ? rig.GetComponent<Camera>() : Camera.main;
            agePage = OpeningPage;
            RefreshCategories();
        }

        // The tabs that have something on them (in a house: only what fits in a house).
        void RefreshCategories()
        {
            categories.Clear();
            foreach (BuildCategory c in System.Enum.GetValues(typeof(BuildCategory)))
                foreach (var b in kingdom.BuildMenu)
                    if (b != null && b.category == c && Fits(b)) { categories.Add(c); break; }
            if (categories.Count == 0) { items.Clear(); selected = -1; return; }
            SelectCategory(categories.Contains(category) ? category : categories[0]);
        }

        // On the map: everything but Dig. In a house: furniture and the like. In a mine: that, and Dig.
        bool Fits(BuildingConfig b) =>
            furnishing == null ? b.kind != BuildingKind.Dig
            : Kingdom.BuildsInside(b) || (b.kind == BuildingKind.Dig && furnishing is MineLevel);

        /// <summary>
        /// Opens the build bar on a house's inside: from inside it (B: the ruler stands still meanwhile),
        /// or from the throne (F over the house). Esc goes back.
        /// </summary>
        public void Furnish(Place house, bool fromThrone)
        {
            if (house == null || kingdom == null) return;
            if (!fromThrone)
            {
                if (Active) return;
                Active = true;
                enteredFrame = Time.frameCount;
                ruler.SetBusy(true);
            }
            furnishing = house;
            furnishFromThrone = fromThrone;
            kingdom.ThroneView = true;
            selected = -1;
            dragging = false;
            ShowPlacements(0);
            Hover = null;
            var inside = house;
            if (rig != null)
            {
                var b = inside.Bounds;
                rig.Inside = new Rect(b.xMin, b.yMin, b.width, b.height);
                float aspect = view != null ? Mathf.Max(0.5f, view.aspect) : 1.6f;
                rig.FreeLook(inside.Center, Mathf.Max(b.height * 0.5f + 1.5f, b.width * 0.5f / aspect + 1f));
            }
            RefreshCategories();
            Say(house is Interior room ? $"Furnishing the {house.Name.ToLowerInvariant()}: {room.Inner.width} x {room.Inner.height} tiles inside"
                : $"Building in the {house.Name.ToLowerInvariant()}: mark rock to dig on the Mining tab");
        }

        /// <summary>Turns the build bar to an age's page (0 = the first age).</summary>
        public void SelectAge(int age)
        {
            agePage = Mathf.Clamp(age, 0, LastAgePage);
            SelectCategory(category);
        }

        /// <summary>Whether a building has been researched (or comes with its age).</summary>
        public static bool Unlocked(BuildingConfig b) => Research.Current == null || Research.Current.IsUnlocked(b);

        /// <summary>Sits the ruler on the throne and pulls the camera out.</summary>
        public void Enter()
        {
            if (Active || kingdom == null || kingdom.Throne == null) return;
            Active = true;
            enteredFrame = Time.frameCount;
            kingdom.ThroneView = true;
            selected = -1;
            dragging = false;
            ruler.Sit(kingdom.Throne);
            if (rig != null) rig.FreeLook(kingdom.Throne.Center);
            if (canopies != null && viewConfig != null) canopies.FarAlpha = viewConfig.throneCanopyAlpha;
        }

        /// <summary>Closes everything (a house being furnished from the throne, then the throne view) and stands the ruler up.</summary>
        public void LeaveAll()
        {
            if ((object)furnishing != null && furnishFromThrone) Leave();
            Leave();
        }

        /// <summary>Stands the ruler up and brings the camera back to them (furnishing a house: back to the throne, or to walking about inside).</summary>
        public void Leave()
        {
            if (!Active) return;
            if ((object)furnishing != null)
            {
                furnishing = null;
                selected = -1;
                dragging = false;
                ShowPlacements(0);
                Hover = null;
                if (furnishFromThrone)
                {
                    if (rig != null)
                    {
                        rig.Inside = default;
                        if (kingdom.Throne != null) rig.FreeLook(kingdom.Throne.Center);
                    }
                    RefreshCategories();
                    return;
                }
                Active = false;
                kingdom.ThroneView = false;
                LeftFrame = Time.frameCount;
                ruler.SetBusy(false);
                if (rig != null) rig.Follow(ruler.transform, true);   // still inside: the camera keeps to the room
                RefreshCategories();
                return;
            }
            Active = false;
            kingdom.ThroneView = false;
            LeftFrame = Time.frameCount;
            selected = -1;
            dragging = false;
            ShowPlacements(0);
            Hover = null;
            ruler.StandUp(kingdom.Throne);
            if (rig != null) rig.Follow(ruler.transform, false);
            if (canopies != null) canopies.FarAlpha = 1f;
        }

        /// <summary>Opens a tab of the build bar (and clears the search).</summary>
        public void SelectCategory(BuildCategory c)
        {
            category = c;
            Search = "";
            RefreshItems();
        }

        // What the bar shows: the open tab on the open age page, or, searching, whatever matches from every tab and
        // every age reached. Things still to research only if Show Locked is on.
        void RefreshItems()
        {
            items.Clear();
            HiddenLocked = 0;
            selected = -1;
            dragging = false;
            if (kingdom == null) return;
            var research = Research.Current;
            string find = Search.Trim();
            foreach (var b in kingdom.BuildMenu)
            {
                if (b == null || !Fits(b)) continue;
                if (find.Length > 0)
                {
                    if (b.displayName == null || b.displayName.IndexOf(find, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (research != null && research.AgeOf(b) > LastAgePage) continue;   // ages not reached yet stay a surprise
                }
                else if (b.category != category || (research != null && research.AgeOf(b) != agePage)) continue;
                if (!Unlocked(b) && !ShowLocked)
                {
                    HiddenLocked++;
                    continue;
                }
                items.Add(b);
            }
        }

        /// <summary>Picks item number <paramref name="index"/> on the open tab (again = put it away).</summary>
        public void Select(int index)
        {
            selected = selected == index || index < 0 || index >= items.Count ? -1 : index;
            dragging = false;
            turnedByHand = false;
            if (selected >= 0 && !Unlocked(items[selected]))
            {
                Say(Research.Current.LockedReason(items[selected]));
                selected = -1;
            }
            var b = Selected;
            if (b != null) rotation = b.ClampRotation(rotation);
        }

        void Say(string text)
        {
            Message = text;
            messageUntil = Time.unscaledTime + 3f;
        }

        Research listening;   // the research whose news refreshes the bar

        // Something was researched: what it unlocked shows on the bar straight away (what's picked stays picked).
        void OnResearchChanged()
        {
            var keep = Selected;
            RefreshItems();
            if (keep != null) selected = items.IndexOf(keep);
        }

        void OnDestroy()
        {
            if (listening != null) listening.Changed -= OnResearchChanged;
        }

        void Update()
        {
            if (Time.unscaledTime > messageUntil) Message = null;
            if (listening != Research.Current)
            {
                if (listening != null) listening.Changed -= OnResearchChanged;
                listening = Research.Current;
                if (listening != null) listening.Changed += OnResearchChanged;
            }
            // A new age: the build bar opens on its page.
            if (Research.Current != null && Research.Current.Age != shownAge)
            {
                shownAge = Research.Current.Age;
                agePage = OpeningPage;
                if (kingdom != null) SelectCategory(category);
            }
            if (!Active || kingdom == null || view == null || Time.timeScale <= 0f || UiFocus.TakesEscape) return;
            // The house being furnished was torn down or broken.
            if ((object)furnishing != null && furnishing == null)
            {
                Leave();
                return;
            }

            if (furnishing != null && rig != null)
            {
                // The room grows when the house is upgraded: the view may go further.
                var room = furnishing.Bounds;
                rig.Inside = new Rect(room.xMin, room.yMin, room.width, room.height);
            }

            bool firstFrame = Time.frameCount == enteredFrame;
            if (!firstFrame && furnishing != null && !furnishFromThrone && GameInput.Pressed(KeyCode.B))
            {
                Leave();
                return;
            }
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
            if (GameInput.Pressed(KeyCode.Tab) && categories.Count > 0)
                SelectCategory(categories[(categories.IndexOf(category) + 1) % categories.Count]);
            if (GameInput.Pressed(KeyCode.LeftBracket)) SelectAge(agePage - 1);
            if (GameInput.Pressed(KeyCode.RightBracket)) SelectAge(agePage + 1);
            for (int i = 0; i < NumberKeys.Length && i < items.Count; i++)
                if (GameInput.Pressed(NumberKeys[i])) Select(i);
            if (GameInput.Pressed(KeyCode.R) && Selected != null)
            {
                rotation = Selected.ClampRotation(rotation + 1);
                turnedByHand = true;
            }

            Vector2 mouse = view.ScreenToWorldPoint(GameInput.PointerPosition);
            var b = Selected;
            if (b != null) Placing(b, mouse, firstFrame);
            else Inspecting(mouse);
        }

        void Placing(BuildingConfig b, Vector2 mouse, bool firstFrame)
        {
            // The building's middle follows the mouse.
            var size = b.SizeFacing(rotation);
            var origin = new Vector2Int(Mathf.RoundToInt(mouse.x - size.x * 0.5f), Mathf.RoundToInt(mouse.y - size.y * 0.5f));
            // Doors turn to fit the wall they go in, ladders to lean on one (unless you turned them yourself).
            if (b.kind == BuildingKind.Door && !turnedByHand && !dragging) rotation = b.ClampRotation(kingdom.DoorFacing(origin, rotation));
            if (b.climb && !turnedByHand && !dragging) rotation = b.ClampRotation(kingdom.LadderFacing(origin, rotation));

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

            // Belts point the way you drag them (Factorio-style).
            bool belt = b.machine != null && b.machine.kind == Economy.MachineKind.Conveyor && b.rotations >= 4;
            if (dragging && belt && origin != dragStart)
            {
                var d = origin - dragStart;
                rotation = b.ClampRotation(Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? (d.x >= 0 ? 0 : 2) : (d.y >= 0 ? 1 : 3));
            }

            placements.Clear();
            if (dragging && b.dragToPlaceLines && (b.kind == BuildingKind.Floor || b.kind == BuildingKind.Roof || b.kind == BuildingKind.Plant || b.kind == BuildingKind.Dig)) Area(dragStart, origin, MaxArea, placements);
            else if (dragging && b.dragToPlaceLines) Line(dragStart, origin, size, kingdom.Config.maxLine, placements);
            else placements.Add(dragging ? dragStart : origin);

            valid.Clear();
            string firstProblem = null;
            int good = 0;
            foreach (var cell in placements)
            {
                bool ok = kingdom.CanPlace(b, cell, rotation, out string reason);
                valid.Add(ok);
                if (ok) good++;
                else if (firstProblem == null) firstProblem = reason;
            }
            ShowPlacements(PointerOverHud && !dragging ? 0 : placements.Count, b);

            string cost = good > 1 ? $"{good} x {b.displayName}: {b.CostText(good)}" : $"{b.displayName}: {b.CostText()}";
            if (b.rotations > 1) cost += belt ? "   (R: turn it, or drag: it points the way you drag)" : "   (R: turn it)";
            // Something electric: Factorio's blue areas (what poles and generators power), and the new pole's or generator's.
            if (kingdom.Factory != null && Factory.IsElectric(b.machine) && placements.Count > 0)
                kingdom.Factory.ShowPowerAreas(new RectInt(placements[0], size), b.machine);
            // A drill: the ore it would dig there.
            if (b.machine != null && b.machine.kind == Economy.MachineKind.Drill && placements.Count > 0)
            {
                string ore = Factory.OreUnder(new RectInt(placements[0], size), b.machine.mineReach);
                cost += ore != null ? $"\nIt would dig {ore}   (what it digs comes out of its chute: R turns it)"
                    : "\n<color=#ff8a80>No ore here: drills go on ore fields (bare patches of ore on the ground)</color>";
            }
            Hover = firstProblem != null ? $"{cost}\n<color=#ff8a80>{firstProblem}</color>" : cost;

            if (dragging && (GameInput.FireReleased || !GameInput.FireHeld))
            {
                dragging = false;
                int placed = 0;
                for (int i = 0; i < placements.Count; i++)
                {
                    if (!valid[i]) continue;
                    // Dig marks rock for the miners; everything else is a blueprint for the builders.
                    if (b.kind == BuildingKind.Dig ? kingdom.MarkDig(placements[i], out _) : kingdom.PlaceBlueprint(b, placements[i], rotation) != null) placed++;
                }
                if (b.kind == BuildingKind.Dig && placed > 0)
                {
                    Say($"Marked {placed} tile{(placed == 1 ? "" : "s")} to dig: villagers with the Miner job dig them out (V)");
                    return;
                }
                if (placed > 0) Say(placed == 1 ? $"Planned: {b.displayName}" : $"Planned {placed} x {b.displayName}");
                else if (firstProblem != null) Say(firstProblem);
            }
        }

        void Inspecting(Vector2 mouse)
        {
            ShowPlacements(0);
            var cell = new Vector2Int(Mathf.FloorToInt(mouse.x), Mathf.FloorToInt(mouse.y));
            bool rightClick = !PointerOverHud && (GameInput.AltFirePressed || GameInput.Pressed(KeyCode.Delete));
            var room = kingdom.RoomAt(cell);
            string roomText = room != null ? $"\n{room.Name} ({room.Size} tiles)" : "";
            // Burning?
            var burning = Fires.Current != null ? Fires.Current.BlazeAt(cell) : null;
            if (burning != null)
                roomText += $"\n<color=#ffa060>On fire ({Mathf.RoundToInt(burning.Heat * 100f)}%)" +
                            (burning.Fighters > 0 ? $": {burning.Fighters} villager{(burning.Fighters == 1 ? "" : "s")} bringing water" : kingdom.HasWell ? "" : ": no well to fetch water from") + "</color>";
            // The roof overhead: Shift + right click takes the whole roof off.
            var roof = kingdom.RoofAt(cell);
            if (roof != null)
                roomText += $"\n{roof.Config.displayName} overhead.  Shift + right click: take the whole roof off (half its cost back)";
            if (rightClick && GameInput.Shift)
            {
                // Shift is only ever for roofs: never tear down the wall under a roof's edge by mistake.
                Say(roof != null ? kingdom.TearDownRoof(cell) : "No roof here (Shift + right click takes roofs off)");
                rightClick = false;
            }

            // In a mine: rock (what's in it), and taking a dig mark off.
            if (furnishing is MineLevel mine && !mine.IsDug(cell) && mine.Bounds.Contains(cell))
            {
                var ore = mine.OreAt(cell);
                string what = ore == Ore.None ? "Rock (stone)" : $"Rock with {ore.ToString().ToLowerInvariant()} in it";
                if (mine.IsMarked(cell))
                {
                    Hover = what + "\nMarked to dig.  Right click: take the mark off";
                    if (rightClick) mine.Unmark(cell);
                }
                else Hover = what + "\nMining tab: Dig, then drag over rock to mark it";
                return;
            }

            // A villager under the mouse: what they're doing, and how fed and rested they are.
            Villagers.Villager person = null;
            float nearest = 0.45f * 0.45f;
            foreach (var v in kingdom.Villagers)
            {
                if (v == null || v.InBed) continue;
                float d = (v.Position - mouse).sqrMagnitude;
                if (d < nearest) { nearest = d; person = v; }
            }
            if (person != null)
            {
                string needs = kingdom.VillagerSettings != null && kingdom.VillagerSettings.needs
                    ? $"\nFed {Mathf.RoundToInt(person.Fed * 100f)}%, rested {Mathf.RoundToInt(person.Rested * 100f)}%" +
                      (person.Pace < 0.99f ? $": working at {Mathf.RoundToInt(person.Pace * 100f)}% speed" : person.Pace > 1.01f ? $": content, working at {Mathf.RoundToInt(person.Pace * 100f)}% speed" : "")
                    : "";
                // A soldier: its rank and gear.
                string who = person.IsSoldier
                    ? $"{person.RankName} ({(person.Weapon != null ? person.Weapon.displayName.ToLowerInvariant() : "no weapon")}, {(person.Armor != null ? person.Armor.displayName.ToLowerInvariant() : "no armor")})"
                    : Villagers.Jobs.Name(person.Job);
                Hover = $"{who}: {person.Doing}  (health {Mathf.CeilToInt(person.Health.Current)}/{Mathf.CeilToInt(person.Health.Max)}){needs}";
                return;
            }

            var house = kingdom.HouseAt(cell);
            if (house != null)
            {
                Hover = AboutHouse(house, cell) + roomText;
                if (GameInput.Pressed(KeyCode.F))
                {
                    Furnish(house.Interior, true);
                    return;
                }
                if (GameInput.Pressed(KeyCode.U)) Say(kingdom.StartUpgrade(house));
                if (rightClick) Say(house.Upgrade != null ? kingdom.Cancel(house.Upgrade) : kingdom.TearDown(cell));
                return;
            }

            var blueprint = kingdom.BlueprintAt(cell);
            if (blueprint != null)
            {
                Hover = Describe(blueprint) + "\nRight click: cancel it (resources go back to storage)" + roomText;
                if (rightClick) Say(kingdom.Cancel(blueprint));
                return;
            }
            foreach (var storage in kingdom.Storages)
            {
                if (!storage.Cells.Contains(cell)) continue;
                var stock = storage.Stock;
                string tearDown = storage.Config != null && kingdom.Storages.Count > 1 ? "\n" + kingdom.TearDownText(cell) : "";
                Hover = $"{storage.name}: {Economy.ResourceText.Describe(stock)} ({stock.Total} / {stock.Capacity}){tearDown}{roomText}";
                if (rightClick && tearDown.Length > 0) Say(kingdom.TearDown(cell));
                return;
            }
            var growing = kingdom.NodeAt(cell);
            if (growing != null && growing.Growing)
            {
                Hover = $"A young {(growing.Config.look == Economy.NodeLook.Tree ? "tree" : "bush")}: {Mathf.FloorToInt(growing.Grown * 100f)}% grown" + roomText;
                return;
            }
            string teardown = kingdom.TearDownText(cell);
            if (teardown != null)
            {
                var built = kingdom.BuiltAt(cell);
                string health = built is Door door ? $" ({Mathf.CeilToInt(door.Health.Current)}/{Mathf.CeilToInt(door.Health.Max)})"
                    : built is Furniture f && f.Health != null ? $" ({Mathf.CeilToInt(f.Health.Current)}/{Mathf.CeilToInt(f.Health.Max)})" : "";
                string about = built != null ? built.Config.displayName + health : kingdom.WallInfo(cell);
                // A factory machine: what it's doing, or why it's stopped.
                var works = built is Furniture piece ? piece.GetComponent<Machine>() : null;
                string state = works != null && kingdom.Factory != null ? kingdom.Factory.Describe(works) : null;
                if (state != null) about += "\n" + state;
                Hover = (about != null ? about + "\n" : "") + teardown + roomText;
                if (rightClick) Say(kingdom.TearDown(cell));
                return;
            }
            // P: the pollution here.
            if (kingdom.Factory != null && kingdom.Factory.ShowingPollution)
                roomText = $"\nPollution here: {kingdom.Factory.Pollution.AtPosition(mouse.x, mouse.y):N0} (in this 32-tile square)" + roomText;
            // An ore field: what's in the ground here.
            var ores = Economy.OreMap.Current;
            if (ores != null && ores.At(cell.x, cell.y, out int oreLeft) is var oreKind && oreKind != Economy.OreKind.None)
                roomText = $"\nOre field: {oreLeft:N0} {Economy.OreMap.Name(oreKind)} in this tile (a drill on it digs it)" + roomText;
            Hover = roomText.Length > 0 ? roomText.TrimStart('\n') : null;
        }

        string AboutHouse(House house, Vector2Int cell)
        {
            var inside = house.Interior.Inner;
            string text = $"{house.Name} ({inside.width} x {inside.height} tiles inside, {Mathf.CeilToInt(house.Health.Current)}/{Mathf.CeilToInt(house.Health.Max)})";
            if (house.Upgrade != null) return text + "\n" + Describe(house.Upgrade) + "\nF: look inside and furnish it\nRight click: cancel the upgrade (what was brought goes back to storage)";
            var next = house.Next;
            text += "\nF: look inside and furnish it";
            if (next == null) text += "\nIt's as good as it gets";
            else text += "\n" + (kingdom.UpgradeLockedReason(house)
                ?? $"U: upgrade to a {next.displayName.ToLowerInvariant()} ({next.inside.x} x {next.inside.y} inside, {next.CostText()})");
            return text + "\n" + kingdom.TearDownText(cell);
        }

        public static string Describe(Blueprint b)
        {
            if (b.Problem != null) return $"{b.Config.displayName}: {b.Problem}";
            if (!b.HasAllResources)
            {
                var parts = new List<string>();
                foreach (var type in new[] { Economy.ResourceType.Wood, Economy.ResourceType.Stone, Economy.ResourceType.Food })
                    if (b.Required(type) > 0)
                        parts.Add($"{b.Delivered(type)}/{b.Required(type)} {(type == Economy.ResourceType.Food ? "cloth" : Economy.ResourceText.Name(type))}");
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

        // Every cell of the rectangle between a and b (floors).
        static void Area(Vector2Int a, Vector2Int b, int max, List<Vector2Int> result)
        {
            int x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1 && result.Count < max; x++)
                    result.Add(new Vector2Int(x, y));
        }

        void ShowPlacements(int count, BuildingConfig b = null)
        {
            while (ghosts.Count < count)
            {
                ghosts.Add(MakeRenderer("Placing", SortOrder.Markers));
                outlines.Add(MakeRenderer("Placing outline", SortOrder.Markers + 1));
            }
            var size = b != null ? b.SizeFacing(rotation) : Vector2Int.one;
            for (int i = 0; i < ghosts.Count; i++)
            {
                bool show = i < count && b != null;
                ghosts[i].enabled = show;
                outlines[i].enabled = show;
                if (!show) continue;
                var center = (Vector2)placements[i] + (Vector2)size * 0.5f;
                var tint = valid[i] ? Good : Bad;
                var look = kingdom.LookOf(b, rotation);
                ghosts[i].sprite = look;
                ghosts[i].transform.position = look != null ? center - (Vector2)look.bounds.center : center;
                ghosts[i].color = tint;
                outlines[i].sprite = kingdom.CellMarker;
                outlines[i].transform.position = center;
                outlines[i].transform.localScale = new Vector3(size.x, size.y, 1f);
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
