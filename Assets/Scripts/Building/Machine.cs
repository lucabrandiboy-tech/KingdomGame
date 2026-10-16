using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// A machine working by itself (see <see cref="MachineKind"/>). The smelter takes iron and
    /// wood from storage and puts steel back. Sawmills, quarries and producers that need power
    /// work while the kingdom's electricity reaches them (<see cref="PowerShare"/>, set by the
    /// factory's power grid: a generator's reach or a power pole's area). They put what they make
    /// out, an item at a time, onto the conveyor belts touching them (Building/Factory.cs carries
    /// it along) or into a storehouse or chest touching them; with nowhere to put it they fill up
    /// and stop. Belts, arms, furnaces, assemblers, drills, generators, power poles, accumulators
    /// and solar panels are run by the kingdom's <see cref="Factory"/>. A machine that isn't
    /// working is drawn darker. An electric lamp shines only while it has power.
    /// </summary>
    public sealed class Machine : MonoBehaviour
    {
        static readonly HashSet<Vector2Int> belts = new HashSet<Vector2Int>();

        Furniture piece;
        MachineConfig config;
        Kingdom kingdom;
        SpriteRenderer look;
        float workAt, pushAt;
        FireLight lamp;
        int held, pushTurn;
        bool working;

        public MachineConfig Config => config;
        /// <summary>Its picture (the factory swaps a belt's for a curve, and dims machines that aren't working).</summary>
        public SpriteRenderer Look => look;
        /// <summary>What it has made and is waiting to put out (sawmills, quarries, drills).</summary>
        public int Held => held;

        /// <summary>One of what it holds is taken (by an arm). False if it holds none.</summary>
        public bool TakeHeld()
        {
            if (held <= 0) return false;
            held--;
            return true;
        }

        /// <summary>Puts back what it held (a saved game).</summary>
        public void SetHeld(int amount) => held = Mathf.Clamp(amount, 0, config != null ? config.holds : 60);

        /// <summary>Run by the factory (belts, arms, furnaces, assemblers, drills, generators, poles, accumulators, solar panels), not by itself.</summary>
        public bool InFactory => config != null && (config.kind == MachineKind.Conveyor || config.kind == MachineKind.Arm
            || config.kind == MachineKind.Furnace || config.kind == MachineKind.Assembler || config.kind == MachineKind.Drill
            || config.kind == MachineKind.Generator || config.kind == MachineKind.PowerPole || config.kind == MachineKind.Accumulator
            || config.kind == MachineKind.Solar);

        /// <summary>Lamps and older machines that need power: the share of what they want that the power grid gives them (Building/Factory.cs).</summary>
        public float PowerShare { get; set; }
        /// <summary>Has enough power to work (half or more of what it wants).</summary>
        public bool Powered => PowerShare >= 0.5f;
        /// <summary>Uses electricity (lamps, and sawmills, quarries and producers marked Needs Power): the factory's power grid feeds it.</summary>
        public bool UsesPower => config != null && (config.kind == MachineKind.Lamp
            || (config.needsPower && (config.kind == MachineKind.Sawmill || config.kind == MachineKind.Quarry || config.kind == MachineKind.Producer)));
        float progress, lastWorked = -99f;

        /// <summary>A crafting table that needs a crafter: storage is short of what it makes and the ingredients are there.</summary>
        public bool WantsWork => config.kind == MachineKind.Workbench && kingdom != null
            && kingdom.Stock(config.makes) < config.keep && HasInputs();

        bool HasInputs()
        {
            if (config.inputs == null) return true;
            foreach (var i in config.inputs) if (i.amount > 0 && kingdom.Available(i.type) < i.amount) return false;
            return true;
        }

        bool SpendInputs()
        {
            if (config.inputs == null || config.inputs.Length == 0) return true;
            var cost = new int[Inventory.TypeCount];
            foreach (var i in config.inputs) if (i.amount > 0) cost[(int)i.type] += i.amount;
            return kingdom.TrySpend(cost);
        }

        /// <summary>A crafter worked at this crafting table for <paramref name="seconds"/>: a batch is made each Work Seconds.</summary>
        public void Work(float seconds)
        {
            lastWorked = Time.time;
            progress += seconds;
            if (progress < config.workSeconds) return;
            progress = 0f;
            if (!SpendInputs()) return;
            int stored = kingdom.Store(config.makes, config.output, transform.position);
            if (stored < config.output) kingdom.Report($"Storage is full: {config.output - stored} {ResourceText.Name(config.makes)} were lost");
        }

        public void Setup(Furniture furniture, MachineConfig machine, SpriteRenderer picture, Kingdom owner)
        {
            piece = furniture;
            config = machine;
            look = picture;
            kingdom = owner;
            workAt = Time.time + config.workSeconds;
            if (config.kind == MachineKind.Conveyor) foreach (var c in furniture.Cells.allPositionsWithin) belts.Add(c);
            if (InFactory) enabled = false;   // (the factory runs it)
            if (config.kind == MachineKind.Lamp)
            {
                // A cold white electric light, off until there's power.
                lamp = FireLight.Create(transform, Vector2.zero, new Color(0.9f, 0.95f, 1f),
                    FireLight.DefaultRadius * config.reach, FireLight.DefaultIntensity * 1.15f);
                lamp.gameObject.SetActive(false);
                workAt = Time.time + Random.value * 0.5f;   // (lamps don't all check on the same frame)
            }
            if (kingdom != null && kingdom.Factory != null) kingdom.Factory.Register(this);
        }

        void OnDestroy()
        {
            if (config == null) return;
            if (config.kind == MachineKind.Conveyor && piece != null) foreach (var c in piece.Cells.allPositionsWithin) belts.Remove(c);
            if (kingdom != null && kingdom.Factory != null) kingdom.Factory.Unregister(this);
        }

        void Update()
        {
            if (kingdom == null) return;
            float now = Time.time;
            if (config.kind == MachineKind.Lamp)
            {
                if (now < workAt) return;
                workAt = now + 0.5f;
                // (inside a house, far off the map: powered if the house's door is in a generator's reach or a pole's area)
                bool on = Powered;
                if (lamp != null && lamp.gameObject.activeSelf != on) lamp.gameObject.SetActive(on);
                if (look != null) look.color = on ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
                return;
            }
            if (config.kind == MachineKind.Workbench)
            {
                // Worked by a crafter (see Work): drawn darker while nobody is.
                if (look != null) look.color = now - lastWorked < 1f ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
                return;
            }
            if (now >= workAt)
            {
                workAt = now + config.workSeconds;
                working = Work();
            }
            if (held > 0) PushOut(now);
            if (look != null) look.color = working ? Color.white : new Color(0.62f, 0.62f, 0.62f, 1f);
        }

        // One round of work; false if it couldn't (no power, nothing to use, full).
        bool Work()
        {
            Vector2 at = transform.position;
            switch (config.kind)
            {
                case MachineKind.Smelter:
                {
                    if (kingdom.Available(ResourceType.Iron) < config.ironIn || kingdom.Available(ResourceType.Wood) < config.woodIn) return false;
                    var cost = new int[Inventory.TypeCount];
                    cost[(int)ResourceType.Iron] = config.ironIn;
                    cost[(int)ResourceType.Wood] = config.woodIn;
                    if (!kingdom.TrySpend(cost)) return false;
                    int stored = kingdom.Store(ResourceType.Steel, config.output, at);
                    if (stored < config.output) kingdom.Store(ResourceType.Iron, config.ironIn, at);   // storage full: the iron goes back
                    return stored > 0;
                }

                case MachineKind.TreeFarm:
                    return PlantTree();

                case MachineKind.Producer:
                {
                    if (config.needsPower && !Powered) return false;
                    // Auto-crafters stop when there's plenty; anything made to go out on belts waits while it's full.
                    if (config.keep > 0 && kingdom.Stock(config.makes) >= config.keep && config.inputs != null && config.inputs.Length > 0) return true;
                    if (config.toBelts && held >= config.holds) return false;
                    if (!HasInputs() || !SpendInputs()) return false;
                    if (config.toBelts)
                    {
                        held += config.output;
                        return true;
                    }
                    int stored = kingdom.Store(config.makes, config.output, at);
                    return stored > 0;
                }

                case MachineKind.Sawmill:
                case MachineKind.Quarry:
                {
                    if (held >= config.holds || (config.needsPower && !Powered)) return false;
                    if (config.kind == MachineKind.Quarry)
                    {
                        held += config.output;
                        return true;
                    }
                    // The nearest tree with wood left within reach (one nobody is cutting).
                    ResourceNode best = null;
                    float bestD = config.reach * config.reach;
                    foreach (var n in ResourceNode.All)
                    {
                        if (n == null || n.Empty || n.Config == null || n.Config.resource != ResourceType.Wood || n.Gatherer != null) continue;
                        float d = ((Vector2)n.transform.position - at).sqrMagnitude;
                        if (d <= bestD) { best = n; bestD = d; }
                    }
                    if (best == null) return false;
                    int got = 0;
                    while (got < config.output && !best.Empty)
                    {
                        int g = best.Gather(config.output - got);
                        if (g <= 0) break;
                        got += g;
                    }
                    held += got;
                    return got > 0;
                }
            }
            return false;
        }

        // Tree farm: counts the trees round it and plants a young one on open ground if there are fewer than it keeps.
        bool PlantTree()
        {
            var nature = NatureMap.Current;
            if (nature == null || config.plant == null || kingdom.Nav == null) return false;
            var cells = piece.Cells;
            int r = Mathf.CeilToInt(config.reach), trees = 0;
            var open = new List<Vector2Int>();
            for (int y = cells.yMin - r; y < cells.yMax + r; y++)
                for (int x = cells.xMin - r; x < cells.xMax + r; x++)
                {
                    var c = new Vector2Int(x, y);
                    if (cells.Contains(c)) continue;
                    var kind = nature.KindAt(c);
                    if (kind != null)
                    {
                        if (kind.resource == ResourceType.Wood) trees++;
                        continue;
                    }
                    // Open ground on the kingdom's lands: nothing built, no floor, no belt, no wall.
                    if (!kingdom.Nav.Cells.Contains(c) || !kingdom.Nav.Walkable(c) || kingdom.ThingAt(c) != null || kingdom.BuiltAt(c) != null
                        || kingdom.BlueprintAt(c) != null || belts.Contains(c)) continue;
                    // Every other cell only, so there's always room to walk between the trees.
                    if (((x + y) & 1) != 0 || (x & 1) != 0) continue;
                    open.Add(c);
                }
            if (trees >= config.output) return true;   // all planted: nothing to do (not stuck)
            if (open.Count == 0) return false;
            kingdom.Plant(config.plant, open[Random.Range(0, open.Count)], config.growSeconds);
            return true;
        }

        /// <summary>Where power is measured from: here, or for a machine inside a house (far off the map), the house's door.</summary>
        public Vector2 PowerSpot
        {
            get
            {
                var place = kingdom != null ? kingdom.PlaceAt((Vector2)transform.position) : null;
                return place != null ? place.Outside : (Vector2)transform.position;
            }
        }

        /// <summary>What it makes (sawmills wood, quarries stone, producers their own).</summary>
        public ResourceType Makes => config.kind == MachineKind.Quarry ? ResourceType.Stone : config.kind == MachineKind.Producer ? config.makes : ResourceType.Wood;

        // What it holds goes out an item at a time onto a belt touching it (one not pointing into it), or into a chest
        // or storehouse touching it. With no factory (an old scene), it goes straight into storage.
        void PushOut(float now)
        {
            if (now < pushAt) return;
            var factory = kingdom.Factory;
            if (factory == null || factory.World == null)
            {
                held -= kingdom.Store(Makes, held, transform.position);
                pushAt = now + 1f;
                return;
            }
            pushAt = now + factory.OutputInterval;
            var c = piece.Cells;
            if (factory.World.PushOut(c.x, c.y, c.width, c.height, Makes, ref pushTurn)) held--;
        }
    }
}
