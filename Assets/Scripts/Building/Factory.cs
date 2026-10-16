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
    /// move items between belts, chests, storehouses and machines, furnaces that smelt what's put in them,
    /// assemblers that make the recipe you choose, and drills that dig the ore fields (Economy/OreMap.cs) under them;
    /// and its electricity (Automation/PowerGrid.cs): generators, power poles wired together, accumulators and solar
    /// panels, feeding the electric machines, lamps and older machines that need power; and the pollution its machines
    /// make (Automation/PollutionMap.cs), which spreads round it, is absorbed by trees, and makes the hordes harder.
    /// The simulation is plain C# (Automation/FactoryWorld.cs and PowerGrid.cs, unit-tested);
    /// this runs it, keeps it in step with what's built (chests and storehouses are its ports, houses' belt holes link
    /// belts through their walls), draws the items, the arms and the wires, and saves it.
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
            public Drill Drill;
            public Transform Head;
            public float HeadAngle;
            public Generator Generator;
            public PowerPole Pole;
            public Accumulator Accumulator;
            public SolarPanel Solar;
            public IPowerUser User;   // an electric arm, furnace, assembler or drill
            public Vector2 Spot;      // where it is for power and pollution (a machine in a house: its door)
        }

        // A lamp or an older machine on the power grid: it wants its Power Use while it can work.
        sealed class LegacyUser : IPowerUser
        {
            public readonly Machine Machine;
            readonly float use;

            public LegacyUser(Machine machine, float megawatts)
            {
                Machine = machine;
                use = megawatts;
            }

            public float PowerDemand()
            {
                if (Machine == null || Machine.Config == null) return 0f;
                return Machine.Held >= Machine.Config.holds ? use * PowerGrid.IdleShare : use;   // (full: it waits)
            }

            public void SetPower(float share)
            {
                if (Machine != null) Machine.PowerShare = share;
            }
        }

        // The ore in the ground (Economy/OreMap.cs), as drills see it.
        sealed class OreGround : IOreGround
        {
            public bool OreAt(int x, int y, out ResourceType item, out int left)
            {
                item = default;
                left = 0;
                var map = OreMap.Current;
                if (map == null) return false;
                var kind = map.At(x, y, out left);
                if (kind == OreKind.None) return false;
                item = OreMap.Item(kind);
                return true;
            }

            public bool Mine(int x, int y, out ResourceType item)
            {
                item = default;
                var map = OreMap.Current;
                if (map == null || !map.Mine(x, y, out var kind)) return false;
                item = OreMap.Item(kind);
                return true;
            }
        }

        static readonly OreGround ground = new OreGround();

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
        readonly Dictionary<Machine, LegacyUser> legacyUsers = new Dictionary<Machine, LegacyUser>();
        readonly List<SpriteRenderer> wirePool = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> areaPool = new List<SpriteRenderer>();
        int shownWires = -1, areasFrame = -1;
        float pollutionClock;
        int recounts;   // squares whose trees may still be counted this second (so it never stalls a frame)
        readonly Dictionary<long, (float At, int Trees)> treeCounts = new Dictionary<long, (float, int)>();
        readonly List<SpriteRenderer> smogPool = new List<SpriteRenderer>();
        Sprite unit, disc;
        readonly Dictionary<Storage, StoragePort> storagePorts = new Dictionary<Storage, StoragePort>();
        readonly List<Recipe> furnaceRecipes = new List<Recipe>();
        readonly List<(Vector2Int Outside, Vector2Int Inside, bool In)> holes = new List<(Vector2Int, Vector2Int, bool)>();
        readonly List<(Spot From, Spot To)> links = new List<(Spot, Spot)>();
        readonly List<SpriteRenderer> itemPool = new List<SpriteRenderer>();
        readonly Dictionary<ResourceType, Sprite> itemSprites = new Dictionary<ResourceType, Sprite>();
        float clock, syncAt;
        int furnaceAge = -1;

        public FactoryWorld World { get; private set; }
        /// <summary>The kingdom's electricity: poles, generators, accumulators, solar panels and what they power.</summary>
        public PowerGrid Power { get; private set; }
        /// <summary>The pollution in the air round the kingdom.</summary>
        public PollutionMap Pollution { get; private set; }
        /// <summary>P: the pollution is shown over the world (red squares).</summary>
        public bool ShowingPollution { get; private set; }

        /// <summary>Survival: how much bigger the hordes are for the pollution in the air.</summary>
        public float WaveScale => Pollution == null || config == null ? 1f
            : Zombies.WaveMath.PollutionWaves(Pollution.Total, config.pollutionPerDoubleWave, config.pollutionMostWaves);
        /// <summary>Survival: how many waves further on the zombies are (kinds and toughness) for all the pollution ever made.</summary>
        public int Evolution => Pollution == null || config == null ? 0
            : Zombies.WaveMath.Evolution(Pollution.Produced, config.pollutionPerEvolution, config.pollutionMostEvolution);
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
            Power = new PowerGrid();
            Pollution = new PollutionMap
            {
                SpreadShare = config != null ? config.pollutionSpread : 0.02f,
                SpreadAbove = config != null ? config.pollutionSpreadAbove : 15f,
                Absorb = config != null ? config.pollutionAbsorb : 0.05f,
                ExtraAbsorb = TreesAbsorb,
            };
            // Anything built before the factory was there.
            foreach (var f in kingdom.AllFurniture)
            {
                var m = f != null ? f.GetComponent<Machine>() : null;
                if (m != null) Register(m);
            }
        }

        static Spot SpotOf(Vector2Int c) => new Spot(c.x, c.y);

        // Where something electric is, for the power grid: its cells, or for something inside a house (far off the map)
        // its door's cell outside.
        Footprint PowerFootprint(Machine m, RectInt cells)
        {
            var place = kingdom != null ? kingdom.PlaceAt((Vector2)m.transform.position) : null;
            if (place != null)
            {
                var door = place.OutsideCell;
                return new Footprint(door.x, door.y, 1, 1);
            }
            return new Footprint(cells.x, cells.y, cells.width, cells.height);
        }

        // A generator's fuel when nobody has put any in: coal from storage, or wood.
        bool TakeFuel(out ResourceType item)
        {
            foreach (var t in new[] { ResourceType.Coal, ResourceType.Wood })
            {
                var cost = new int[Inventory.TypeCount];
                cost[(int)t] = 1;
                if (kingdom == null || !kingdom.TrySpend(cost)) continue;
                item = t;
                return true;
            }
            item = default;
            return false;
        }

        // ------------------------------------------------------------ pieces --

        /// <summary>A machine was built: a belt, arm, furnace or assembler joins the factory; an older machine's goods become something arms can take.</summary>
        public void Register(Machine m)
        {
            if (m == null || m.Config == null || World == null || pieces.ContainsKey(m) || heldPorts.ContainsKey(m) || legacyUsers.ContainsKey(m)) return;
            var furniture = m.GetComponent<Furniture>();
            if (furniture == null) return;
            var cfg = m.Config;
            var cells = furniture.Cells;
            var piece = new Piece { Machine = m, Config = cfg, Origin = SpotOf(cells.position) };
            var foot = PowerFootprint(m, cells);
            piece.Spot = new Vector2(foot.CenterX, foot.CenterY);
            // Lamps and the older machines that need power: on the grid (and, below, maybe a port for their goods).
            if (m.UsesPower)
            {
                var user = new LegacyUser(m, cfg.powerUse);
                legacyUsers[m] = user;
                Power.AddUser(user, foot);
            }
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
                    if (!cfg.burner) Power.AddUser(piece.User = piece.Arm, foot);
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
                    if (!cfg.burner) Power.AddUser(piece.User = c, foot);
                    if (cfg.kind == MachineKind.Assembler) MakeIcon(piece, furniture);
                    break;
                }
                case MachineKind.Drill:
                {
                    var d = new Drill(World, ground, piece.Origin, Mathf.Max(cells.width, cells.height), furniture.Rotation, cfg.mineReach)
                    {
                        SecondsPerItem = cfg.mineSeconds, Burner = cfg.burner, EnergyUse = cfg.energyUse, Power = cfg.burner ? 1f : 0f,
                    };
                    piece.Drill = d;
                    foreach (var cell in cells.allPositionsWithin) World.SetPort(SpotOf(cell), d, false);
                    World.Add(d);
                    if (!cfg.burner) Power.AddUser(piece.User = d, foot);
                    MakeDrillLook(piece, furniture);
                    break;
                }
                case MachineKind.Generator:
                {
                    // It powers what's within its reach itself (its own wiring: a circle), and power poles take it further.
                    var g = new Generator(foot, World.Fuels) { MaxOutput = cfg.powerOutput, TakeFromStore = TakeFuel };
                    piece.Generator = g;
                    Power.Add(g);
                    piece.Pole = new PowerPole(foot, cfg.reach, cfg.reach, true);
                    Power.Add(piece.Pole);
                    foreach (var cell in cells.allPositionsWithin) World.SetPort(SpotOf(cell), g, false);   // (arms can put fuel in)
                    break;
                }
                case MachineKind.PowerPole:
                    piece.Pole = new PowerPole(foot, cfg.wireReach, cfg.supplyArea);
                    Power.Add(piece.Pole);
                    break;
                case MachineKind.Accumulator:
                    piece.Accumulator = new Accumulator(foot) { Capacity = cfg.storeMJ, Rate = cfg.powerOutput };
                    Power.Add(piece.Accumulator);
                    break;
                case MachineKind.Solar:
                    piece.Solar = new SolarPanel(foot) { MaxOutput = cfg.powerOutput };
                    Power.Add(piece.Solar);
                    break;
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
            if (legacyUsers.TryGetValue(m, out var legacy))
            {
                legacyUsers.Remove(m);
                Power.RemoveUser(legacy);
            }
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
            if (piece.Drill != null)
            {
                piece.Drill.TakeAll(spill);
                World.Remove(piece.Drill);
                if (furniture != null) foreach (var cell in furniture.Cells.allPositionsWithin) World.ClearPort(SpotOf(cell), piece.Drill);
            }
            if (piece.User != null) Power.RemoveUser(piece.User);
            if (piece.Generator != null)
            {
                piece.Generator.TakeAll(spill);
                Power.Remove(piece.Generator);
                if (furniture != null) foreach (var cell in furniture.Cells.allPositionsWithin) World.ClearPort(SpotOf(cell), piece.Generator);
            }
            if (piece.Pole != null) Power.Remove(piece.Pole);
            if (piece.Accumulator != null) Power.Remove(piece.Accumulator);
            if (piece.Solar != null) Power.Remove(piece.Solar);
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

        /// <summary>What a factory machine is doing, or why it's stopped, and how its power is (null: not a factory machine).</summary>
        public string Describe(Machine m)
        {
            if (m == null) return null;
            string work = DescribeWork(m), power = PowerText(m);
            return power == null ? work : work == null ? power : work + "\n" + power;
        }

        string DescribeWork(Machine m)
        {
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
            if (p.Drill != null)
            {
                int left = p.Drill.OreLeft(out var most);
                return (p.Drill.Working ? $"Digging {ResourceText.Name(most)}" : $"<color=#ffcc80>{p.Drill.Problem ?? "starting"}</color>") +
                       (left > 0 ? $"  ({left:N0} {ResourceText.Name(most)} left to dig)" : "") + "   E beside it: see what it's doing";
            }
            return null;
        }

        public Drill DrillOf(Machine m) => m != null && pieces.TryGetValue(m, out var p) ? p.Drill : null;
        public Generator GeneratorOf(Machine m) => m != null && pieces.TryGetValue(m, out var p) ? p.Generator : null;
        public Accumulator AccumulatorOf(Machine m) => m != null && pieces.TryGetValue(m, out var p) ? p.Accumulator : null;
        public SolarPanel SolarOf(Machine m) => m != null && pieces.TryGetValue(m, out var p) ? p.Solar : null;
        public PowerPole PoleOf(Machine m) => m != null && pieces.TryGetValue(m, out var p) ? p.Pole : null;

        /// <summary>Whether something uses or makes electricity (its power areas show while you place it).</summary>
        public static bool IsElectric(MachineConfig c) => c != null && (c.kind == MachineKind.Generator || c.kind == MachineKind.PowerPole
            || c.kind == MachineKind.Accumulator || c.kind == MachineKind.Solar || c.kind == MachineKind.Lamp
            || (!c.burner && (c.kind == MachineKind.Arm || c.kind == MachineKind.Furnace || c.kind == MachineKind.Assembler || c.kind == MachineKind.Drill))
            || (c.needsPower && (c.kind == MachineKind.Sawmill || c.kind == MachineKind.Quarry || c.kind == MachineKind.Producer)));

        /// <summary>MW or kW, whichever reads better.</summary>
        public static string Watts(float megawatts) => megawatts >= 1f ? $"{megawatts:0.##} MW" : $"{megawatts * 1000f:0} kW";

        /// <summary>A network's state in a line: what's used of what it could give, what makes it, what's stored.</summary>
        public static string NetworkText(PowerNetwork n)
        {
            if (n == null) return "<color=#ff8a80>Not on a power network: no generator or wired pole reaches it</color>";
            var parts = new List<string>();
            if (n.GeneratorCount > 0) parts.Add($"{n.GeneratorCount} generator{(n.GeneratorCount == 1 ? "" : "s")}");
            if (n.PanelCount > 0) parts.Add($"{n.PanelCount} solar panel{(n.PanelCount == 1 ? "" : "s")}");
            if (n.AccumulatorCount > 0) parts.Add($"{n.AccumulatorCount} accumulator{(n.AccumulatorCount == 1 ? "" : "s")} ({n.Stored:0.#} of {n.StoreCapacity:0.#} MJ stored)");
            string makers = parts.Count > 0 ? string.Join(", ", parts) : "nothing making power";
            string state = n.Satisfaction >= 0.999f ? "" : $"   <color=#ffcc80>only {n.Satisfaction * 100f:0}% of what's wanted: machines work slower</color>";
            return $"Power network: {Watts(n.Supplied)} used of {Watts(n.Capacity)} ({makers}, {n.UserCount} using it){state}";
        }

        // How an electric thing's power is, or a generator's, pole's, accumulator's or panel's state (null: nothing to say).
        string PowerText(Machine m)
        {
            if (legacyUsers.TryGetValue(m, out var legacy)) return UserPower(Power.NetworkOf(legacy));
            if (!pieces.TryGetValue(m, out var p)) return null;
            if (p.User != null) return UserPower(Power.NetworkOf(p.User));
            if (p.Generator != null)
            {
                var g = p.Generator;
                string fuel = g.FuelCount > 0 ? $", {g.FuelCount} {ResourceText.Name(g.FuelType)} in it" : ", burns coal (or wood) from storage";
                return (g.Problem != null ? $"<color=#ffcc80>{g.Problem}</color>" : $"Giving {Watts(g.Output)} of {Watts(g.MaxOutput)}{fuel}") + "\n" + NetworkText(Power.NetworkOf(g));
            }
            if (p.Accumulator != null)
            {
                var a = p.Accumulator;
                string flow = a.Flow > 0.0001f ? $", charging {Watts(a.Flow)}" : a.Flow < -0.0001f ? $", giving {Watts(-a.Flow)}" : "";
                return $"Stored {a.Stored:0.##} of {a.Capacity:0.#} MJ{flow}\n" + NetworkText(Power.NetworkOf(a));
            }
            if (p.Solar != null) return $"Giving {Watts(p.Solar.Output)} of {Watts(p.Solar.MaxOutput)} (in full sun)\n" + NetworkText(Power.NetworkOf(p.Solar));
            if (p.Pole != null) return NetworkText(p.Pole.Network);
            return null;
        }

        static string UserPower(PowerNetwork n)
        {
            if (n == null) return "<color=#ff8a80>No power: put it within a generator's reach or a power pole's area</color>";
            if (n.Satisfaction >= 0.999f) return null;
            return n.Capacity <= 0f ? "<color=#ff8a80>No power: the network's generators have no fuel (coal or wood)</color>"
                : $"<color=#ffcc80>Power {n.Satisfaction * 100f:0}%: more is wanted than the network gives, so it works slower</color>";
        }

        /// <summary>
        /// The ore a drill would dig on <paramref name="cells"/> (and <paramref name="reach"/> tiles round them), like
        /// "2,400 iron ore and 600 coal"; null if there's none.
        /// </summary>
        public static string OreUnder(RectInt cells, int reach)
        {
            var map = OreMap.Current;
            if (map == null) return null;
            var total = new int[5];
            for (int y = cells.yMin - reach; y < cells.yMax + reach; y++)
                for (int x = cells.xMin - reach; x < cells.xMax + reach; x++)
                {
                    var kind = map.At(x, y, out int left);
                    if (kind != OreKind.None) total[(int)kind] += left;
                }
            var parts = new List<string>();
            for (int k = 1; k < total.Length; k++)
                if (total[k] > 0) parts.Add($"{total[k]:N0} {OreMap.Name((OreKind)k)}");
            if (parts.Count == 0) return null;
            return parts.Count == 1 ? parts[0] : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
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
            // Solar panels: the daylight, less under clouds.
            var sky = kingdom.Clock;
            Power.Daylight = sky != null ? sky.Daylight * (1f - 0.7f * Mathf.Clamp01(sky.Overcast)) : 1f;
            float step = 1f / Mathf.Clamp(config != null ? config.ticksPerSecond : 30, 10, 60);
            clock = Mathf.Min(clock + Time.deltaTime, step * 4f);
            while (clock >= step)
            {
                clock -= step;
                Power.Tick(step);
                World.Tick(step);
            }
            // Pollution: once a second, what the machines working now put out, then it spreads and is absorbed.
            pollutionClock += Time.deltaTime;
            if (pollutionClock >= 1f)
            {
                pollutionClock = Mathf.Min(pollutionClock - 1f, 1f);
                Pollute(1f);
                recounts = 24;
                Pollution.Tick(1f);
            }
            if (GameInput.Pressed(KeyCode.P)) ShowingPollution = !ShowingPollution;
        }

        void LateUpdate()
        {
            if (World == null) return;
            DrawItems();
            DrawWires();
            DrawPollution();
            if (areasFrame != Time.frameCount) HideAreas(0);
            foreach (var p in pieces.Values) DrawPiece(p);
        }

        // Keeps the factory in step with the kingdom: its chests and storehouses, houses' belt holes, and which recipes
        // furnaces know (by age).
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
        }

        // --------------------------------------------------------- pollution --

        // What each machine working now puts out over <seconds> (its Pollution a minute; generators by how hard they work;
        // assemblers making Craftables four times as much), where it stands (a machine in a house: at its door).
        void Pollute(float seconds)
        {
            float perSecond = seconds / 60f;
            foreach (var p in pieces.Values)
            {
                float rate = 0f;
                if (p.Crafter != null && p.Crafter.Working) rate = p.Config.pollution * (p.Crafter.Recipe != null ? p.Crafter.Recipe.pollution : 1f);
                else if (p.Drill != null && p.Drill.Working) rate = p.Config.pollution;
                else if (p.Generator != null) rate = p.Config.pollution * p.Generator.Load;
                if (rate > 0f) Pollution.Emit(p.Spot.x, p.Spot.y, rate * perSecond);
            }
        }

        // What a square's trees absorb a second (counted from the nature map now and then: cut them down and it absorbs less).
        float TreesAbsorb(int cx, int cy)
        {
            if (config == null || config.pollutionPerTree <= 0f) return 0f;
            long key = ((long)cx << 32) | (uint)cy;
            float now = Time.time;
            bool counted = treeCounts.TryGetValue(key, out var known);
            if (counted && (now - known.At < 60f || recounts <= 0)) return known.Trees * config.pollutionPerTree;
            if (recounts <= 0) return 0f;   // (counted in a moment: a few squares a second)
            recounts--;
            int trees = 0;
            var nature = NatureMap.Current;
            if (nature != null)
            {
                int x0 = cx * PollutionMap.Chunk, y0 = cy * PollutionMap.Chunk;
                for (int y = y0; y < y0 + PollutionMap.Chunk; y++)
                    for (int x = x0; x < x0 + PollutionMap.Chunk; x++)
                    {
                        var kind = nature.KindAt(new Vector2Int(x, y));
                        if (kind != null && kind.resource == ResourceType.Wood) trees++;
                    }
            }
            if (treeCounts.Count > 4000) treeCounts.Clear();
            treeCounts[key] = (now, trees);
            return trees * config.pollutionPerTree;
        }

        static readonly Color Smog = new Color(0.85f, 0.22f, 0.12f, 1f);

        // P: the pollution as red squares over the world (stronger where there's more), the ones in view.
        void DrawPollution()
        {
            int used = 0;
            var view = Camera.main;
            if (ShowingPollution && view != null && view.orthographic)
            {
                Vector2 c = view.transform.position;
                float halfH = view.orthographicSize + PollutionMap.Chunk, halfW = view.orthographicSize * view.aspect + PollutionMap.Chunk;
                foreach (var (x, y, amount) in Pollution.Chunks())
                {
                    float mx = (x + 0.5f) * PollutionMap.Chunk, my = (y + 0.5f) * PollutionMap.Chunk;
                    if (Mathf.Abs(mx - c.x) > halfW || Mathf.Abs(my - c.y) > halfH) continue;
                    var r = Pooled(smogPool, used++, "Pollution", SortOrder.Markers - 3);
                    r.sprite = Unit();
                    r.color = new Color(Smog.r, Smog.g, Smog.b, 0.06f + 0.4f * Mathf.Clamp01(amount / 300f));
                    r.transform.position = new Vector3(mx, my, 0f);
                    r.transform.rotation = Quaternion.identity;
                    r.transform.localScale = new Vector3(PollutionMap.Chunk, PollutionMap.Chunk, 1f);
                }
            }
            for (int i = used; i < smogPool.Count; i++)
                if (smogPool[i].enabled) smogPool[i].enabled = false;
        }

        /// <summary>A line about pollution for the screen (null: there's none to speak of).</summary>
        public string PollutionText(bool survival)
        {
            if (Pollution == null || Pollution.Total < 1f) return null;
            string text = $"Pollution: {Pollution.Total:N0} in the air  (P: {(ShowingPollution ? "hide" : "show")} it)";
            if (!survival) return text;
            float scale = WaveScale;
            int further = Evolution;
            if (scale > 1.01f || further > 0)
                text += $"\n<color=#ff9a7a>Hordes x{scale:0.0}" + (further > 0 ? $", zombies {further} wave{(further == 1 ? "" : "s")} further on" : "") + " (your pollution)</color>";
            return text;
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

        Sprite Unit()
        {
            if (unit == null) unit = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return unit;
        }

        // A soft-edged white circle a tile across (a generator's reach).
        Sprite Disc()
        {
            if (disc != null) return disc;
            const int n = 128;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Power reach", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float a = Mathf.Clamp01((0.5f - Mathf.Sqrt(dx * dx + dy * dy)) * n * 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            texture.SetPixels32(px);
            texture.Apply(false, true);
            disc = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return disc;
        }

        static readonly Color WireColor = new Color(0.17f, 0.13f, 0.1f, 1f), WireShadow = new Color(0f, 0f, 0f, 0.3f);
        static readonly Color AreaColor = new Color(0.45f, 0.7f, 1f, 0.12f), PlacingArea = new Color(0.55f, 0.85f, 1f, 0.2f);

        // The wires between poles (high up, over roofs), and their shadows on the ground. Only laid out again when they change.
        void DrawWires()
        {
            var wires = Power.Wires;
            if (Power.Version == shownWires) return;
            shownWires = Power.Version;
            int used = 0;
            var shadow = new Vector2(0.45f, -0.55f);
            foreach (var (a, b) in wires)
            {
                var pa = new Vector2(a.At.CenterX, a.At.CenterY);
                var pb = new Vector2(b.At.CenterX, b.At.CenterY);
                Line(wirePool, used++, "Wire", pa, pb, 0.035f, WireColor, SortOrder.Roofs + 10);
                Line(wirePool, used++, "Wire shadow", pa + shadow, pb + shadow, 0.05f, WireShadow, SortOrder.Shadows);
            }
            for (int i = used; i < wirePool.Count; i++)
                if (wirePool[i].enabled) wirePool[i].enabled = false;
        }

        void Line(List<SpriteRenderer> pool, int i, string what, Vector2 from, Vector2 to, float width, Color color, int order)
        {
            var r = Pooled(pool, i, what, order);
            var d = to - from;
            r.sprite = Unit();
            r.color = color;
            r.sortingOrder = order;
            r.transform.position = (from + to) * 0.5f;
            r.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(d.magnitude, width, 1f);
        }

        SpriteRenderer Pooled(List<SpriteRenderer> pool, int i, string what, int order)
        {
            while (pool.Count <= i)
            {
                var go = new GameObject(what);
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sortingOrder = order;
                pool.Add(r);
            }
            var pooled = pool[i];
            if (!pooled.enabled) pooled.enabled = true;
            return pooled;
        }

        /// <summary>
        /// Shows what's powered while something electric is being placed (Factorio's blue areas): every pole's supply
        /// square and every generator's reach, and the one being placed at <paramref name="placingAt"/> (its cells).
        /// The build controller calls it every frame it wants them; they go by themselves on a frame it doesn't.
        /// </summary>
        public void ShowPowerAreas(RectInt placingAt, MachineConfig placing)
        {
            areasFrame = Time.frameCount;
            int used = 0;
            if (Power != null)
            {
                foreach (var pole in Power.Poles) Area(used++, pole.At.CenterX, pole.At.CenterY, pole.Supply, pole.Round, AreaColor);
                if (placing != null && (placing.kind == MachineKind.PowerPole || placing.kind == MachineKind.Generator))
                {
                    bool round = placing.kind == MachineKind.Generator;
                    Area(used++, placingAt.center.x, placingAt.center.y, round ? placing.reach : placing.supplyArea, round, PlacingArea);
                }
            }
            HideAreas(used);
        }

        void HideAreas(int from)
        {
            for (int i = from; i < areaPool.Count; i++)
                if (areaPool[i].enabled) areaPool[i].enabled = false;
        }

        void Area(int i, float x, float y, float size, bool round, Color color)
        {
            var r = Pooled(areaPool, i, "Power area", SortOrder.Markers - 2);
            r.sprite = round ? Disc() : Unit();
            r.color = color;
            r.transform.position = new Vector3(x, y, 0f);
            r.transform.rotation = Quaternion.identity;
            float across = round ? size * 2f : size;
            r.transform.localScale = new Vector3(across, across, 1f);
        }

        // A drill's bit, turning while it digs (in the middle of its picture).
        void MakeDrillLook(Piece p, Furniture furniture)
        {
            var sprite = ArtSprites.Get(ArtKeys.DrillHead(furniture.Config.art));
            if (sprite == null) return;
            var go = new GameObject("Drill bit");
            go.transform.SetParent(furniture.transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = SortOrder.Furniture + 3;
            p.Head = go.transform;
        }

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
            if (p.Generator != null)
            {
                bool on = p.Generator.Output > 0.0001f;
                if (on != p.ShownWorking && p.Machine.Look != null)
                {
                    p.ShownWorking = on;
                    p.Machine.Look.color = on ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
                }
                return;
            }
            if (p.Drill != null)
            {
                bool digging = p.Drill.Working;
                if (digging != p.ShownWorking && p.Machine.Look != null)
                {
                    p.ShownWorking = digging;
                    p.Machine.Look.color = digging ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
                }
                if (digging && p.Head != null)
                {
                    p.HeadAngle = Mathf.Repeat(p.HeadAngle - Time.deltaTime * 240f, 360f);
                    p.Head.localRotation = Quaternion.Euler(0f, 0f, p.HeadAngle);
                }
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

        /// <summary>What a save needs to put the factory back: items on belts, arms mid-swing, what's in the machines and drills.</summary>
        [Serializable]
        public sealed class Saved
        {
            public List<BeltSave> belts = new List<BeltSave>();
            public List<ArmSave> arms = new List<ArmSave>();
            public List<CrafterSave> crafters = new List<CrafterSave>();
            public List<HeldSave> held = new List<HeldSave>();
            public List<DrillSave> drills = new List<DrillSave>();
            public List<PowerSave> power = new List<PowerSave>();
            // The pollution in the air (each square's x, y and amount), and all ever made.
            public List<int> smogX = new List<int>();
            public List<int> smogY = new List<int>();
            public List<float> smog = new List<float>();
            public double smogMade;
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

        /// <summary>A generator's fuel and energy, or an accumulator's charge.</summary>
        [Serializable]
        public sealed class PowerSave
        {
            public int x, y, fuelType, fuelCount;
            public float energy, stored;
        }

        [Serializable]
        public sealed class DrillSave
        {
            public int x, y, held, fuelType, fuelCount;
            public bool holding;
            public float progress, energy;
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
                if (p.Generator != null)
                    s.power.Add(new PowerSave { x = p.Origin.X, y = p.Origin.Y, energy = p.Generator.Energy, fuelType = (int)p.Generator.FuelType, fuelCount = p.Generator.FuelCount });
                if (p.Accumulator != null) s.power.Add(new PowerSave { x = p.Origin.X, y = p.Origin.Y, stored = p.Accumulator.Stored });
                if (p.Drill != null)
                {
                    var d = p.Drill;
                    s.drills.Add(new DrillSave
                    {
                        x = p.Origin.X, y = p.Origin.Y, holding = d.Holding, held = (int)d.Held, progress = d.Progress,
                        fuelType = (int)d.FuelType, fuelCount = d.FuelCount, energy = d.Energy,
                    });
                }
            }
            foreach (var m in heldPorts.Keys)
                if (m != null && m.Held > 0)
                {
                    var f = m.GetComponent<Furniture>();
                    if (f != null) s.held.Add(new HeldSave { x = f.Cells.x, y = f.Cells.y, held = m.Held });
                }
            Pollution.Save(s.smogX, s.smogY, s.smog, out s.smogMade);
            return s;
        }

        /// <summary>Puts back a saved factory (after the pieces themselves were rebuilt).</summary>
        public void Load(Saved s)
        {
            if (s == null) return;
            Pollution.Load(s.smogX, s.smogY, s.smog, s.smogMade);
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
            if (s.power != null)
                foreach (var e in s.power)
                {
                    if (e == null || !at.TryGetValue(new Spot(e.x, e.y), out var p)) continue;
                    p.Generator?.Restore(e.energy, Type(e.fuelType), e.fuelCount);
                    if (p.Accumulator != null) p.Accumulator.Stored = Mathf.Clamp(e.stored, 0f, p.Accumulator.Capacity);
                }
            if (s.drills != null)
                foreach (var d in s.drills)
                {
                    if (d == null || !at.TryGetValue(new Spot(d.x, d.y), out var p) || p.Drill == null) continue;
                    p.Drill.Restore(d.holding, Type(d.held), d.progress, Type(d.fuelType), d.fuelCount, d.energy);
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
