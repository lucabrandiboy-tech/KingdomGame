using System;
using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Automation;
using KingdomGame.Economy;
using KingdomGame.Progression;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// A kingdom's factory, Factorio-style: belts that carry items in their direction (you see each one ride), arms that
    /// move items between belts, chests, storehouses and machines, furnaces that smelt what's put in them and
    /// assemblers that make the recipe you choose. The simulation is plain C# (Automation/FactoryWorld.cs, unit-tested);
    /// this runs it, keeps it in step with what's built (chests and storehouses are its ports, houses' belt holes link
    /// belts through their walls), draws the items and the arms, and saves it.
    /// </summary>
    public sealed class Factory : MonoBehaviour
    {
        sealed class Piece
        {
            public Machine Machine;
            public MachineConfig Config;
            public Spot Origin;
            public Belt Belt;
            public bool ShownCurve;
            public int ShownTurn;
            public Arm Arm;
            public Transform Swing;
            public SpriteRenderer Hand;
            public Crafter Crafter;
            public SpriteRenderer Icon;
            public Recipe ShownRecipe;
            public bool ShownWorking = true;
        }

        // A chest or storehouse, as the factory sees it (arms take from and put into its stock).
        sealed class StoragePort : IItemPort
        {
            public readonly Storage Store;
            public readonly List<Spot> Cells = new List<Spot>();
            public StoragePort(Storage store)
            {
                Store = store;
                foreach (var c in store.Cells.allPositionsWithin) Cells.Add(SpotOf(c));
            }
            public bool Accepts(ResourceType item) => Store != null && Store.Stock.SpaceLeft > 0;
            public bool Insert(ResourceType item) => Store != null && Store.Deposit(item, 1) == 1;
            public bool Offer(Func<ResourceType, bool> want, out ResourceType item)
            {
                item = default;
                if (Store == null) return false;
                for (int i = 0; i < Inventory.TypeCount; i++)
                {
                    var t = (ResourceType)i;
                    if (Store.Available(t) > 0 && want(t)) { item = t; return true; }
                }
                return false;
            }
            public bool Extract(ResourceType item) => Store != null && Store.Available(item) > 0 && Store.Stock.TryRemove(item, 1);
        }

        // An older machine's goods (a sawmill's wood, a quarry's stone): arms can take them out too.
        sealed class HeldPort : IItemPort
        {
            public readonly Machine Machine;
            public HeldPort(Machine machine) => Machine = machine;
            public bool Accepts(ResourceType item) => false;
            public bool Insert(ResourceType item) => false;
            public bool Offer(Func<ResourceType, bool> want, out ResourceType item)
            {
                item = Machine != null ? Machine.Makes : default;
                return Machine != null && Machine.Held > 0 && want(item);
            }
            public bool Extract(ResourceType item) => Machine != null && item == Machine.Makes && Machine.TakeHeld();
        }

        FactoryConfig config;
        Kingdom kingdom;
        readonly Dictionary<Machine, Piece> pieces = new Dictionary<Machine, Piece>();
        readonly Dictionary<Machine, HeldPort> heldPorts = new Dictionary<Machine, HeldPort>();
        readonly Dictionary<Storage, StoragePort> storagePorts = new Dictionary<Storage, StoragePort>();
        readonly List<Recipe> furnaceRecipes = new List<Recipe>();
        readonly List<(Vector2Int Outside, Vector2Int Inside, bool In)> holes = new List<(Vector2Int, Vector2Int, bool)>();
        readonly List<(Spot From, Spot To)> links = new List<(Spot, Spot)>();
        readonly List<SpriteRenderer> itemPool = new List<SpriteRenderer>();
        readonly Dictionary<ResourceType, Sprite> itemSprites = new Dictionary<ResourceType, Sprite>();
        float clock, syncAt;
        int furnaceAge = -1;

        public FactoryWorld World { get; private set; }
        public FactoryConfig Config => config;
        /// <summary>Seconds between items an older machine puts out on a belt.</summary>
        public float OutputInterval => 1f / Mathf.Max(0.5f, config != null ? config.machineOutputRate : 4f);
        /// <summary>The recipes assemblers can make (all ages; the panel shows those of the kingdom's age).</summary>
        public IEnumerable<Recipe> Recipes
        {
            get
            {
                if (config == null || config.recipes == null) yield break;
                foreach (var r in config.recipes) if (r != null) yield return r;
            }
        }

        public void Setup(Kingdom owner, FactoryConfig factory)
        {
            kingdom = owner;
            config = factory;
            var fuels = new FuelTable(config != null ? config.fuels : RecipeBook.DefaultFuels());
            World = new FactoryWorld(fuels);
            // Anything built before the factory was there.
            foreach (var f in kingdom.AllFurniture)
            {
                var m = f != null ? f.GetComponent<Machine>() : null;
                if (m != null) Register(m);
            }
        }

        static Spot SpotOf(Vector2Int c) => new Spot(c.x, c.y);

        // ------------------------------------------------------------ pieces --

        /// <summary>A machine was built: a belt, arm, furnace or assembler joins the factory; an older machine's goods become something arms can take.</summary>
        public void Register(Machine m)
        {
            if (m == null || m.Config == null || World == null || pieces.ContainsKey(m) || heldPorts.ContainsKey(m)) return;
            var furniture = m.GetComponent<Furniture>();
            if (furniture == null) return;
            var cfg = m.Config;
            var cells = furniture.Cells;
            var piece = new Piece { Machine = m, Config = cfg, Origin = SpotOf(cells.position) };
            switch (cfg.kind)
            {
                case MachineKind.Conveyor:
                    piece.Belt = World.AddBelt(piece.Origin, furniture.Rotation, cfg.beltSpeed);
                    break;
                case MachineKind.Arm:
                    piece.Arm = new Arm(piece.Origin, furniture.Rotation)
                    {
                        SwingSeconds = cfg.swingSeconds, Burner = cfg.burner, EnergyUse = cfg.energyUse, Power = cfg.burner ? 1f : 0f,
                    };
                    World.Add(piece.Arm);
                    MakeArmLook(piece, furniture);
                    break;
                case MachineKind.Furnace:
                case MachineKind.Assembler:
                {
                    var c = new Crafter(cfg.kind == MachineKind.Furnace, World.Fuels)
                    {
                        Speed = cfg.craftSpeed, Burner = cfg.burner, EnergyUse = cfg.energyUse, Power = cfg.burner ? 1f : 0f,
                        FurnaceRecipes = furnaceRecipes,
                    };
                    piece.Crafter = c;
                    foreach (var cell in cells.allPositionsWithin) World.SetPort(SpotOf(cell), c, false);
                    World.Add(c);
                    if (cfg.kind == MachineKind.Assembler) MakeIcon(piece, furniture);
                    break;
                }
                case MachineKind.Sawmill:
                case MachineKind.Quarry:
                case MachineKind.Producer when cfg.toBelts:
                {
                    var port = new HeldPort(m);
                    heldPorts[m] = port;
                    foreach (var cell in cells.allPositionsWithin) World.SetPort(SpotOf(cell), port, false);
                    return;
                }
                default:
                    return;
            }
            pieces[m] = piece;
            syncAt = 0f;   // (curves and power at once)
        }

        /// <summary>A machine went (torn down, broken): what was on it or in it goes back to storage (or a bag on the ground).</summary>
        public void Unregister(Machine m)
        {
            // (not while the scene is closing: nothing goes back to storage then, and no bags are made)
            if (m == null || World == null || !m.gameObject.scene.isLoaded) return;
            var furniture = m.GetComponent<Furniture>();
            if (heldPorts.TryGetValue(m, out var port))
            {
                heldPorts.Remove(m);
                if (furniture != null) foreach (var cell in furniture.Cells.allPositionsWithin) World.ClearPort(SpotOf(cell), port);
            }
            if (!pieces.TryGetValue(m, out var piece)) return;
            pieces.Remove(m);
            var spill = new int[Inventory.TypeCount];
            var list = new List<ResourceType>();
            if (piece.Belt != null) World.RemoveBelt(piece.Origin, list);
            if (piece.Arm != null) World.Remove(piece.Arm, list);
            if (piece.Crafter != null)
            {
                piece.Crafter.TakeAll(spill, true);
                World.Remove(piece.Crafter);
                if (furniture != null) foreach (var cell in furniture.Cells.allPositionsWithin) World.ClearPort(SpotOf(cell), piece.Crafter);
            }
            foreach (var t in list) spill[(int)t]++;
            GiveBack(spill, m.transform.position);
        }

        // Things taken out of the factory go into storage; what doesn't fit is left in a bag on the ground.
        void GiveBack(int[] counts, Vector2 at)
        {
            if (kingdom == null) return;
            bool any = false;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] <= 0) continue;
                counts[i] -= kingdom.Store((ResourceType)i, counts[i], at);
                any |= counts[i] > 0;
            }
            if (any) LootBag.Drop(at, counts);
        }

        /// <summary>An assembler's new recipe: what was in it goes back to storage.</summary>
        public void SetRecipe(Machine m, Recipe recipe)
        {
            if (m == null || !pieces.TryGetValue(m, out var p) || p.Crafter == null || p.Crafter.Furnace) return;
            var spill = new int[Inventory.TypeCount];
            p.Crafter.SetRecipe(recipe, spill);
            GiveBack(spill, m.transform.position);
        }

        public Crafter CrafterOf(Machine m) => m != null && pieces.TryGetValue(m, out var p) ? p.Crafter : null;

        /// <summary>What a factory machine is doing, or why it's stopped, in a line (null: not a factory machine).</summary>
        public string Describe(Machine m)
        {
            if (m == null) return null;
            if (heldPorts.ContainsKey(m)) return m.Held > 0 ? $"Holding {m.Held} {ResourceText.Name(m.Makes)} to put out on a belt or into a chest touching it" : null;
            if (!pieces.TryGetValue(m, out var p)) return null;
            if (p.Belt != null)
                return $"Carries {p.Belt.Speed:0.#} tiles a second, {p.Belt.Items.Count} on it now" +
                       (p.Belt.Next == null ? ": the end of the line (a chest or storehouse it points at takes things in)" : "");
            if (p.Arm != null)
                return (p.Arm.Problem != null ? $"<color=#ffcc80>{p.Arm.Problem}</color>" : p.Arm.Holding ? $"Moving {ResourceText.Name(p.Arm.Held)}" : "Waiting for something to pick up") +
                       (p.Arm.HasFilter ? $"  (only {ResourceText.Name(p.Arm.Filter)})" : "") + "   E beside it: choose what it moves";
            if (p.Crafter != null)
                return (p.Crafter.Working ? $"Making {p.Crafter.Recipe.name.ToLowerInvariant()}" : $"<color=#ffcc80>{p.Crafter.Problem}</color>") +
                       (p.Crafter.Output > 0 ? $", {p.Crafter.Output} made" : "") + (p.Crafter.Furnace ? "" : "   E beside it: choose what it makes");
            return null;
        }
        public Arm ArmOf(Machine m) => m != null && pieces.TryGetValue(m, out var p) ? p.Arm : null;

        /// <summary>Whether a recipe can be made now (the kingdom has reached its age).</summary>
        public static bool Allowed(Recipe r)
        {
            var research = Research.Current;
            return r != null && (research == null || research.Mode == Research.Unlocking.Everything || research.Age >= r.age);
        }

        // ---------------------------------------------------------- running --

        void Update()
        {
            if (World == null || kingdom == null || Time.timeScale <= 0f) return;
            float now = Time.time;
            if (now >= syncAt)
            {
                syncAt = now + 0.25f;
                Sync();
            }
            float step = 1f / Mathf.Clamp(config != null ? config.ticksPerSecond : 30, 10, 60);
            clock = Mathf.Min(clock + Time.deltaTime, step * 4f);
            while (clock >= step)
            {
                clock -= step;
                World.Tick(step);
            }
        }

        void LateUpdate()
        {
            if (World == null) return;
            DrawItems();
            foreach (var p in pieces.Values) DrawPiece(p);
        }

        // Keeps the factory in step with the kingdom: its chests and storehouses, houses' belt holes, which recipes
        // furnaces know (by age), power for electric machines, and how belts look (straight or curved).
        void Sync()
        {
            // Chests and storehouses: arms take from and put into them; belts ending at one unload into it.
            var seen = new HashSet<Storage>();
            foreach (var s in kingdom.Storages)
            {
                if (s == null) continue;
                seen.Add(s);
                if (storagePorts.ContainsKey(s)) continue;
                var port = new StoragePort(s);
                storagePorts[s] = port;
                foreach (var c in port.Cells) World.SetPort(c, port, true);
            }
            // (gone: torn down or broken; a destroyed one is no longer in the kingdom's list)
            List<Storage> gone = null;
            foreach (var pair in storagePorts) if (pair.Key == null || !seen.Contains(pair.Key)) (gone ??= new List<Storage>()).Add(pair.Key);
            if (gone != null)
                foreach (var s in gone)
                {
                    var port = storagePorts[s];
                    storagePorts.Remove(s);
                    foreach (var c in port.Cells) World.ClearPort(c, port);
                }

            // Belts through houses' walls (red holes in, blue out).
            links.Clear();
            foreach (var h in kingdom.Houses)
            {
                if (h == null) continue;
                h.BeltHoles(holes);
                foreach (var hole in holes)
                    links.Add(hole.In ? (SpotOf(hole.Outside), SpotOf(hole.Inside)) : (SpotOf(hole.Inside), SpotOf(hole.Outside)));
            }
            World.SetLinks(links);

            // What furnaces can smelt, by the kingdom's age.
            var research = Research.Current;
            int age = research == null || research.Mode == Research.Unlocking.Everything ? 99 : research.Age;
            if (age != furnaceAge)
            {
                furnaceAge = age;
                furnaceRecipes.Clear();
                foreach (var r in Recipes) if (r.furnace && Allowed(r)) furnaceRecipes.Add(r);
            }

            // Electric machines work while a burning generator's power reaches them.
            foreach (var p in pieces.Values)
            {
                if (p.Config.burner || p.Machine == null) continue;
                float power = p.Config.needsPower ? (Machine.PoweredAt(p.Machine.PowerSpot) ? 1f : 0f) : 1f;
                if (p.Arm != null) p.Arm.Power = power;
                if (p.Crafter != null) p.Crafter.Power = power;
            }
        }

        // ---------------------------------------------------------- drawing --

        Sprite ItemSprite(ResourceType t)
        {
            if (itemSprites.TryGetValue(t, out var s)) return s;
            s = ArtSprites.Get(ArtKeys.Item(t));
            itemSprites[t] = s;
            return s;
        }

        // The items on the belts the camera can see, each a pooled picture.
        void DrawItems()
        {
            int used = 0;
            var view = Camera.main;
            if (view != null && view.orthographic)
            {
                Vector2 c = view.transform.position;
                float halfH = view.orthographicSize + 1f, halfW = view.orthographicSize * view.aspect + 1f;
                float size = config != null ? config.itemSize : 0.3f;
                foreach (var b in World.Belts)
                {
                    if (b.Items.Count == 0) continue;
                    float bx = b.At.X + 0.5f, by = b.At.Y + 0.5f;
                    if (Mathf.Abs(bx - c.x) > halfW || Mathf.Abs(by - c.y) > halfH) continue;
                    foreach (var it in b.Items)
                    {
                        b.Place(it.Pos, out float ox, out float oy);
                        var r = Pooled(used++);
                        r.sprite = ItemSprite(it.Type);
                        r.transform.position = new Vector3(bx + ox, by + oy, 0f);
                        r.transform.localScale = Vector3.one * size / 0.3f;
                    }
                }
            }
            for (int i = used; i < itemPool.Count; i++)
                if (itemPool[i].enabled) itemPool[i].enabled = false;
        }

        SpriteRenderer Pooled(int i)
        {
            while (itemPool.Count <= i)
            {
                var go = new GameObject("Item on a belt");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sortingOrder = SortOrder.Rugs + 20;
                itemPool.Add(r);
            }
            var pooled = itemPool[i];
            if (!pooled.enabled) pooled.enabled = true;
            return pooled;
        }

        void MakeArmLook(Piece p, Furniture furniture)
        {
            var go = new GameObject("Arm");
            go.transform.SetParent(furniture.transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = ArtSprites.Get(ArtKeys.ArmSwing(furniture.Config.art));
            r.sortingOrder = SortOrder.Furniture + 3;
            p.Swing = go.transform;
            var hand = new GameObject("In its hand");
            hand.transform.SetParent(go.transform, false);
            hand.transform.localPosition = new Vector3(ArmReach, 0f, 0f);
            p.Hand = hand.AddComponent<SpriteRenderer>();
            p.Hand.sortingOrder = SortOrder.Furniture + 4;
            p.Hand.enabled = false;
        }

        /// <summary>How far an arm's hand reaches from its middle, in tiles (to the middle of the next tile, nearly).</summary>
        public const float ArmReach = FurnitureArt.ArmReach;

        void MakeIcon(Piece p, Furniture furniture)
        {
            var go = new GameObject("Recipe");
            go.transform.SetParent(furniture.transform, false);
            var cells = furniture.Cells;
            go.transform.localPosition = new Vector3(cells.width * 0.5f - 0.45f, cells.height * 0.5f - 0.45f, 0f);
            go.transform.localScale = Vector3.one * 1.6f;
            p.Icon = go.AddComponent<SpriteRenderer>();
            p.Icon.sortingOrder = SortOrder.Furniture + 4;
            p.Icon.enabled = false;
        }

        void DrawPiece(Piece p)
        {
            if (p.Machine == null) return;
            if (p.Belt != null)
            {
                // Straight or curved (a belt fed only from one side turns the corner).
                int turn = p.Belt.Curve ? (p.Belt.Dir == Dirs.Wrap(p.Belt.CurveFrom + 1) ? 1 : 2) : 0;
                if (turn != p.ShownTurn || !p.ShownCurve)
                {
                    p.ShownCurve = true;
                    p.ShownTurn = turn;
                    var look = p.Machine.Look;
                    var art = p.Machine.GetComponent<Furniture>()?.Config.art;
                    if (look != null && art != null)
                    {
                        var sprite = ArtSprites.Get(ArtKeys.Turned(turn == 0 ? art : art + (turn == 1 ? "/CurveL" : "/CurveR"), p.Belt.Dir));
                        if (sprite != null) look.sprite = sprite;
                    }
                }
                return;
            }
            if (p.Arm != null && p.Swing != null)
            {
                // From over the pickup (behind) round to over the drop (in front).
                float facing = p.Arm.Dir * 90f;
                p.Swing.localRotation = Quaternion.Euler(0f, 0f, facing + 180f - 180f * p.Arm.Angle);
                bool holding = p.Arm.Holding;
                if (p.Hand.enabled != holding) p.Hand.enabled = holding;
                if (holding) p.Hand.sprite = ItemSprite(p.Arm.Held);
                return;
            }
            if (p.Crafter != null)
            {
                bool working = p.Crafter.Working;
                if (working != p.ShownWorking && p.Machine.Look != null)
                {
                    p.ShownWorking = working;
                    p.Machine.Look.color = working ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
                }
                if (p.Icon != null && p.ShownRecipe != p.Crafter.Recipe)
                {
                    p.ShownRecipe = p.Crafter.Recipe;
                    p.Icon.enabled = p.ShownRecipe != null;
                    if (p.ShownRecipe != null) p.Icon.sprite = ItemSprite(p.ShownRecipe.output);
                }
            }
        }

        // ------------------------------------------------------------ saving --

        /// <summary>What a save needs to put the factory back: items on belts, arms mid-swing, what's in the machines.</summary>
        [Serializable]
        public sealed class Saved
        {
            public List<BeltSave> belts = new List<BeltSave>();
            public List<ArmSave> arms = new List<ArmSave>();
            public List<CrafterSave> crafters = new List<CrafterSave>();
            public List<HeldSave> held = new List<HeldSave>();
        }

        [Serializable]
        public sealed class BeltSave
        {
            public int x, y;
            public int[] types;
            public float[] pos;
        }

        [Serializable]
        public sealed class ArmSave
        {
            public int x, y, held, state, filter = -1;
            public bool holding;
            public float angle, energy;
        }

        [Serializable]
        public sealed class CrafterSave
        {
            public int x, y, outputType, output, fuelType, fuelCount;
            public string recipe;
            public int[] inputs;
            public bool crafting;
            public float progress, energy;
        }

        [Serializable]
        public sealed class HeldSave
        {
            public int x, y, held;
        }

        public Saved Save()
        {
            var s = new Saved();
            foreach (var p in pieces.Values)
            {
                if (p.Belt != null && p.Belt.Items.Count > 0)
                {
                    var b = new BeltSave { x = p.Origin.X, y = p.Origin.Y, types = new int[p.Belt.Items.Count], pos = new float[p.Belt.Items.Count] };
                    for (int i = 0; i < p.Belt.Items.Count; i++)
                    {
                        b.types[i] = (int)p.Belt.Items[i].Type;
                        b.pos[i] = p.Belt.Items[i].Pos;
                    }
                    s.belts.Add(b);
                }
                if (p.Arm != null)
                    s.arms.Add(new ArmSave
                    {
                        x = p.Origin.X, y = p.Origin.Y, holding = p.Arm.Holding, held = (int)p.Arm.Held, state = p.Arm.State,
                        angle = p.Arm.Angle, energy = p.Arm.Energy, filter = p.Arm.HasFilter ? (int)p.Arm.Filter : -1,
                    });
                if (p.Crafter != null)
                {
                    var c = p.Crafter;
                    var inputs = new int[Inventory.TypeCount];
                    for (int i = 0; i < inputs.Length; i++) inputs[i] = c.Input((ResourceType)i);
                    s.crafters.Add(new CrafterSave
                    {
                        x = p.Origin.X, y = p.Origin.Y, recipe = c.Recipe != null ? c.Recipe.name : null, inputs = inputs,
                        outputType = (int)c.OutputType, output = c.Output, crafting = c.Crafting, progress = c.Progress,
                        fuelType = (int)c.FuelType, fuelCount = c.FuelCount, energy = c.Energy,
                    });
                }
            }
            foreach (var m in heldPorts.Keys)
                if (m != null && m.Held > 0)
                {
                    var f = m.GetComponent<Furniture>();
                    if (f != null) s.held.Add(new HeldSave { x = f.Cells.x, y = f.Cells.y, held = m.Held });
                }
            return s;
        }

        /// <summary>Puts back a saved factory (after the pieces themselves were rebuilt).</summary>
        public void Load(Saved s)
        {
            if (s == null) return;
            var at = new Dictionary<Spot, Piece>();
            foreach (var p in pieces.Values) at[p.Origin] = p;
            ResourceType Type(int i) => (ResourceType)Mathf.Clamp(i, 0, Inventory.TypeCount - 1);
            Recipe Named(string name)
            {
                foreach (var r in Recipes) if (r.name == name) return r;
                return null;
            }
            if (s.belts != null)
                foreach (var b in s.belts)
                {
                    if (b == null || b.types == null || b.pos == null || !at.TryGetValue(new Spot(b.x, b.y), out var p) || p.Belt == null) continue;
                    var items = new List<BeltItem>();
                    for (int i = 0; i < b.types.Length && i < b.pos.Length; i++) items.Add(new BeltItem { Type = Type(b.types[i]), Pos = b.pos[i] });
                    World.SetItems(p.Origin, items);
                }
            if (s.arms != null)
                foreach (var a in s.arms)
                {
                    if (a == null || !at.TryGetValue(new Spot(a.x, a.y), out var p) || p.Arm == null) continue;
                    p.Arm.Restore(a.holding, Type(a.held), a.angle, a.state, a.energy);
                    p.Arm.HasFilter = a.filter >= 0 && a.filter < Inventory.TypeCount;
                    if (p.Arm.HasFilter) p.Arm.Filter = Type(a.filter);
                }
            if (s.crafters != null)
                foreach (var c in s.crafters)
                {
                    if (c == null || !at.TryGetValue(new Spot(c.x, c.y), out var p) || p.Crafter == null) continue;
                    p.Crafter.Restore(Named(c.recipe), c.inputs, Type(c.outputType), c.output, c.crafting, c.progress, Type(c.fuelType), c.fuelCount, c.energy);
                }
            if (s.held != null)
                foreach (var h in s.held)
                    foreach (var m in heldPorts.Keys)
                    {
                        var f = m != null ? m.GetComponent<Furniture>() : null;
                        if (f != null && f.Cells.x == h.x && f.Cells.y == h.y) m.SetHeld(h.held);
                    }
        }
    }
}
