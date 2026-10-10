using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.Villagers;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// Your kingdom: the throne, storehouses, blueprints and builder villagers.
    /// It decides where things may be built, hands out jobs to idle villagers
    /// (oldest blueprint first) and turns finished blueprints into real walls
    /// and buildings. One per game scene (<see cref="Current"/>).
    /// </summary>
    public sealed class Kingdom : MonoBehaviour
    {
        public static Kingdom Current { get; private set; }

        KingdomConfig config;
        VillagerConfig villagerConfig;
        PixelWorld world;
        BuildingConfig[] menu = new BuildingConfig[0];

        readonly List<Storage> storages = new List<Storage>();
        readonly List<Blueprint> blueprints = new List<Blueprint>();
        readonly List<Villager> villagers = new List<Villager>();
        readonly Dictionary<Vector2Int, Component> occupied = new Dictionary<Vector2Int, Component>();
        readonly Dictionary<Vector2Int, ResourceNode> nodes = new Dictionary<Vector2Int, ResourceNode>();
        readonly Dictionary<BuildingConfig, Sprite> looks = new Dictionary<BuildingConfig, Sprite>();
        readonly Dictionary<BuildingConfig, Sprite> shadows = new Dictionary<BuildingConfig, Sprite>();
        readonly List<Sprite> madeSprites = new List<Sprite>();
        Sprite throneLook, throneShadow, brazier;
        VillagerSprites villagerSprites;

        public KingdomConfig Config => config;
        public NavGrid Nav { get; private set; }
        public Throne Throne { get; private set; }
        public IReadOnlyList<Storage> Storages => storages;
        public IReadOnlyList<Blueprint> Blueprints => blueprints;
        public IReadOnlyList<Villager> Villagers => villagers;
        public IReadOnlyList<BuildingConfig> BuildMenu => menu;
        /// <summary>A plain white square sprite (for markers and bars).</summary>
        public Sprite White { get; private set; }
        /// <summary>A grid cell outline sprite (for placing buildings).</summary>
        public Sprite CellMarker { get; private set; }
        /// <summary>The ruler, so buildings don't appear on top of them.</summary>
        public Transform Ruler { get; set; }
        public float RulerRadius { get; set; } = 0.2f;

        void Awake()
        {
            if (Current != null && Current != this) Debug.LogWarning("There is more than one Kingdom in the scene.", this);
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            Nav?.Dispose();
            foreach (var sprite in madeSprites)
            {
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }

        /// <summary>
        /// Sets the kingdom up on a map of <paramref name="mapCells"/>. <paramref name="otherBuildings"/>
        /// are buildings that aren't on the build bar but can still appear (the first storehouse).
        /// </summary>
        public void Setup(KingdomConfig kingdomConfig, VillagerConfig villagers, BuildingConfig[] buildMenu, PixelWorld pixelWorld, RectInt mapCells,
            params BuildingConfig[] otherBuildings)
        {
            config = kingdomConfig != null ? kingdomConfig : ScriptableObject.CreateInstance<KingdomConfig>();
            villagerConfig = villagers != null ? villagers : ScriptableObject.CreateInstance<VillagerConfig>();
            menu = buildMenu ?? new BuildingConfig[0];
            world = pixelWorld;
            Nav = new NavGrid(mapCells, world);
            // Builders shouldn't stand on a planned building's spot while they work (it couldn't be finished).
            Nav.NoStopping = cell => occupied.TryGetValue(cell, out var thing) && thing is Blueprint;
            foreach (var node in ResourceNode.All)
                if (node != null) nodes[node.Cell] = node;
            MakeSprites(otherBuildings);
        }

        // The baked building and villager pictures, plus two small markers drawn here.
        void MakeSprites(BuildingConfig[] otherBuildings)
        {
            throneLook = ArtSprites.Get(ArtKeys.Throne);
            throneShadow = ArtSprites.Get(ArtKeys.Shadow(ArtKeys.Throne));
            brazier = ArtSprites.Get(ArtKeys.Brazier);
            White = Made(PixelTextures.ToSprite(BuildingArt.White(), "White"));
            CellMarker = Made(PixelTextures.ToSprite(BuildingArt.CellMarker(), "Cell marker"));

            var all = new List<BuildingConfig>(menu);
            if (otherBuildings != null) all.AddRange(otherBuildings);
            foreach (var b in all)
            {
                if (b == null || looks.ContainsKey(b)) continue;
                if (b.kind == BuildingKind.Storage)
                {
                    looks[b] = ArtSprites.Get(ArtKeys.Storehouse);
                    shadows[b] = ArtSprites.Get(ArtKeys.Shadow(ArtKeys.Storehouse));
                }
                else if (b.wallMaterial != null)
                {
                    looks[b] = ArtSprites.Get(ArtKeys.Wall(b.wallMaterial.name, 0)) ?? WallPicture(b.wallMaterial);
                }
            }

            var walking = new Sprite[CharacterArt.VillagerDirections, CharacterArt.WalkFrames];
            var carrying = new Sprite[CharacterArt.VillagerDirections, CharacterArt.WalkFrames];
            for (int d = 0; d < CharacterArt.VillagerDirections; d++)
                for (int f = 0; f < CharacterArt.WalkFrames; f++)
                {
                    walking[d, f] = ArtSprites.Get(ArtKeys.Villager(false, d, f));
                    carrying[d, f] = ArtSprites.Get(ArtKeys.Villager(true, d, f));
                }
            villagerSprites = new VillagerSprites { Walking = walking, Carrying = carrying, Shadow = ArtSprites.Get(ArtKeys.VillagerShadow) };
        }

        Sprite Made(Sprite sprite)
        {
            madeSprites.Add(sprite);
            return sprite;
        }

        // A wall picture drawn here, for a wall material the baked art doesn't know.
        Sprite WallPicture(StructureMaterial material)
        {
            var look = WallTiles.LookOf(material);
            var image = look.Logs ? WallArt.Logs(128, look.Gap, look.Colors, 0) : WallArt.Stone(128, look.Gap, look.Colors, 0);
            image.Pivot = Vector2.zero;
            return Made(PixelTextures.ToSprite(image, material.name, false, 128));
        }

        /// <summary>The picture for a building (its blueprint ghost uses it too).</summary>
        public Sprite LookOf(BuildingConfig b) => b != null && looks.TryGetValue(b, out var s) ? s : White;

        // --------------------------------------------------------- buildings --

        public Throne PlaceThrone(Vector2Int cell)
        {
            var cells = new RectInt(cell, new Vector2Int(2, 2));
            ClearNature(cells);
            var go = new GameObject("Throne");
            go.transform.SetParent(transform, false);
            Throne = go.AddComponent<Throne>();
            Throne.Setup(cells, throneLook, throneShadow, brazier);
            Occupy(cells, Throne);
            Nav.Block(cells);
            return Throne;
        }

        public Storage AddStorage(BuildingConfig b, Vector2Int cell)
        {
            var cells = new RectInt(cell, b != null ? b.size : new Vector2Int(2, 2));
            ClearNature(cells);
            var go = new GameObject(b != null ? b.displayName : "Storehouse");
            go.transform.SetParent(transform, false);
            var storage = go.AddComponent<Storage>();
            Sprite shadow = null;
            if (b != null) shadows.TryGetValue(b, out shadow);
            storage.Setup(b, cells, LookOf(b), shadow);
            storage.AddLantern(brazier);
            storages.Add(storage);
            Occupy(cells, storage);
            Nav.Block(cells);
            return storage;
        }

        public Villager AddVillager(Vector2 position)
        {
            var go = new GameObject("Villager");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var v = go.AddComponent<Villager>();
            v.Setup(villagerConfig, this, Nav, villagerSprites);
            villagers.Add(v);
            return v;
        }

        void Occupy(RectInt cells, Component by)
        {
            foreach (var c in cells.allPositionsWithin) occupied[c] = by;
        }

        void Free(RectInt cells, Component by)
        {
            foreach (var c in cells.allPositionsWithin)
                if (occupied.TryGetValue(c, out var o) && o == by) occupied.Remove(c);
        }

        // Removes trees, stumps, rocks and bushes standing in these cells.
        void ClearNature(RectInt cells)
        {
            foreach (var c in cells.allPositionsWithin)
            {
                if (!nodes.TryGetValue(c, out var node)) continue;
                nodes.Remove(c);
                if (node != null) node.Clear();
            }
        }

        // ------------------------------------------------------- resources --

        public int Stock(ResourceType type)
        {
            int total = 0;
            foreach (var s in storages) total += s.Stock.Get(type);
            return total;
        }

        public int StorageUsed
        {
            get { int total = 0; foreach (var s in storages) total += s.Stock.Total; return total; }
        }

        public int StorageCapacity
        {
            get { int total = 0; foreach (var s in storages) total += s.Stock.Capacity; return total; }
        }

        /// <summary>The closest storehouse that has some of <paramref name="type"/> not promised to anyone.</summary>
        public Storage NearestWith(ResourceType type, Vector2 from)
        {
            Storage best = null;
            float bestDistance = float.MaxValue;
            foreach (var s in storages)
            {
                if (s.Available(type) <= 0) continue;
                float d = (s.Center - from).sqrMagnitude;
                if (d < bestDistance) { best = s; bestDistance = d; }
            }
            return best;
        }

        /// <summary>The closest storehouse with free space.</summary>
        public Storage NearestWithSpace(Vector2 from)
        {
            Storage best = null;
            float bestDistance = float.MaxValue;
            foreach (var s in storages)
            {
                if (s.Stock.SpaceLeft <= 0) continue;
                float d = (s.Center - from).sqrMagnitude;
                if (d < bestDistance) { best = s; bestDistance = d; }
            }
            return best;
        }

        /// <summary>Puts resources into the nearest storehouses with room; returns how much fitted.</summary>
        public int Store(ResourceType type, int amount, Vector2 near)
        {
            int stored = 0;
            while (stored < amount)
            {
                var s = NearestWithSpace(near);
                if (s == null) break;
                stored += s.Deposit(type, amount - stored);
            }
            return stored;
        }

        // -------------------------------------------------------- placement --

        /// <summary>Whether <paramref name="b"/> can go with its bottom-left corner at <paramref name="cell"/>, and if not, why.</summary>
        public bool CanPlace(BuildingConfig b, Vector2Int cell, out string reason)
        {
            var cells = new RectInt(cell, b.size);
            var map = Nav.Cells;
            foreach (var c in cells.allPositionsWithin)
            {
                if (c.x < map.xMin + 1 || c.y < map.yMin + 1 || c.x >= map.xMax - 1 || c.y >= map.yMax - 1)
                {
                    reason = "Too close to the edge of the map";
                    return false;
                }
                if (occupied.TryGetValue(c, out var thing) && thing != null)
                {
                    reason = thing is Blueprint ? "Already planned here" : "Something is already built here";
                    return false;
                }
                if (world != null && world.ChunkAt(c) != null)
                {
                    reason = "There's a wall here";
                    return false;
                }
                if (nodes.TryGetValue(c, out var node) && node != null && !node.Empty)
                {
                    reason = node.Config != null && node.Config.look == NodeLook.Rock ? "A rock is in the way (gather it first: hold E)"
                        : node.Config != null && node.Config.look == NodeLook.Tree ? "A tree is in the way (chop it first: hold E)"
                        : "A bush is in the way (pick it first: hold E)";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        public Blueprint PlaceBlueprint(BuildingConfig b, Vector2Int cell)
        {
            if (b == null || !CanPlace(b, cell, out _)) return null;
            var cells = new RectInt(cell, b.size);
            var go = new GameObject($"Blueprint: {b.displayName}");
            go.transform.SetParent(transform, false);
            var blueprint = go.AddComponent<Blueprint>();
            blueprint.Setup(b, cells, LookOf(b), White);
            blueprints.Add(blueprint);
            Occupy(cells, blueprint);
            return blueprint;
        }

        public Blueprint BlueprintAt(Vector2Int cell) =>
            occupied.TryGetValue(cell, out var thing) ? thing as Blueprint : null;

        /// <summary>Removes a blueprint and puts what was already delivered back into storage. Returns a message for the player.</summary>
        public string Cancel(Blueprint blueprint)
        {
            if (blueprint == null || blueprint.Cancelled) return null;
            blueprint.MarkCancelled();
            blueprints.Remove(blueprint);
            Free(blueprint.Cells, blueprint);
            int lost = 0;
            for (int i = 0; i < Inventory.TypeCount; i++)
            {
                var type = (ResourceType)i;
                int back = Mathf.FloorToInt(blueprint.Delivered(type) * config.refundOnCancel);
                lost += back - Store(type, back, blueprint.Center);
            }
            Destroy(blueprint.gameObject);
            return lost > 0 ? $"Cancelled. Storage is full: {lost} resources were lost" : "Cancelled";
        }

        // ------------------------------------------------------------- jobs --

        /// <summary>Gives an idle villager something to do, if there is anything.</summary>
        public void AssignJob(Villager v)
        {
            float now = Time.time;

            // Carrying something left over: take it where it's needed, or back to storage.
            if (v.CarryAmount > 0)
            {
                foreach (var b in blueprints)
                {
                    if (b.Cancelled || b.UnreachableUntil > now || b.Need(v.CarryType) <= 0) continue;
                    b.AddIncoming(v.CarryType, v.CarryAmount);
                    if (v.StartDeliver(b)) return;
                }
                var home = NearestWithSpace(v.Position);
                if (home != null) v.StartReturn(home);
                return;
            }

            foreach (var b in blueprints)
            {
                if (b.Cancelled || b.UnreachableUntil > now) continue;
                if (!b.HasAllResources)
                {
                    for (int i = 0; i < Inventory.TypeCount; i++)
                    {
                        var type = (ResourceType)i;
                        int need = b.Need(type);
                        if (need <= 0) continue;
                        var from = NearestWith(type, v.Position);
                        if (from == null) continue;
                        int amount = from.Reserve(type, Mathf.Min(need, villagerConfig.carryAmount));
                        if (amount <= 0) continue;
                        b.AddIncoming(type, amount);
                        if (v.StartFetch(from, b, type, amount)) return;
                    }
                }
                else if (!b.WorkDone && b.Builders < config.buildersPerBlueprint)
                {
                    if (v.StartBuild(b)) return;
                }
            }
        }

        // ------------------------------------------------------- completion --

        void Update()
        {
            for (int i = blueprints.Count - 1; i >= 0; i--)
            {
                var b = blueprints[i];
                if (b == null) { blueprints.RemoveAt(i); continue; }
                b.Problem = null;
                if (!b.HasAllResources)
                {
                    for (int t = 0; t < Inventory.TypeCount; t++)
                    {
                        var type = (ResourceType)t;
                        int missing = b.Need(type);
                        if (missing > 0 && NearestWith(type, b.Center) == null)
                            b.Problem = $"Waiting for {missing} {type.ToString().ToLowerInvariant()} in storage";
                    }
                    if (b.Problem == null && b.UnreachableUntil > Time.time) b.Problem = "Builders can't find a way there";
                    continue;
                }
                if (b.WorkDone) TryComplete(b);
                else if (b.UnreachableUntil > Time.time) b.Problem = "Builders can't find a way there";
            }
        }

        void TryComplete(Blueprint b)
        {
            // Nobody may be standing where the building appears.
            var area = new Rect(b.Cells.position, b.Cells.size);
            bool blocked = false;
            foreach (var v in villagers)
            {
                if (v == null || !Overlaps(area, v.Position, NavGrid.WalkerRadius)) continue;
                if (!v.IsWalking) v.MakeRoom(b.Cells);   // walkers passing through are gone in a moment
                blocked = true;
            }
            if (Ruler != null && Overlaps(area, Ruler.position, RulerRadius))
            {
                b.Problem = "Finished, but you're standing on it: step off";
                return;
            }
            if (blocked)
            {
                b.Problem = "Finished: waiting for a villager to step off";
                return;
            }

            blueprints.Remove(b);
            Free(b.Cells, b);
            ClearNature(b.Cells);
            switch (b.Config.kind)
            {
                case BuildingKind.Storage:
                    AddStorage(b.Config, b.Cells.position);
                    break;
                default:
                    if (b.Config.wallMaterial == null || world == null)
                    {
                        Debug.LogWarning($"{b.Config.name} has no Wall Material, so nothing was built.", b.Config);
                        break;
                    }
                    var go = new GameObject(b.Config.displayName);
                    go.transform.SetParent(transform, false);
                    go.transform.position = new Vector3(b.Cells.x, b.Cells.y, 0f);
                    go.AddComponent<DestructibleStructure>().Build(world, b.Config.wallMaterial, b.Cells.size);
                    break;
            }
            Destroy(b.gameObject);
        }

        static bool Overlaps(Rect area, Vector2 point, float radius) =>
            point.x > area.xMin - radius && point.x < area.xMax + radius && point.y > area.yMin - radius && point.y < area.yMax + radius;
    }
}
