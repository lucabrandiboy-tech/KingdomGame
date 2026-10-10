using System;
using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.Villagers;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>A room: an area closed off by walls and doors, named after what's in it.</summary>
    public sealed class Room
    {
        public string Name;
        public readonly List<Vector2Int> Cells = new List<Vector2Int>();
        /// <summary>A spot near the middle that's inside the room, for its label.</summary>
        public Vector2 LabelAt;
        public int Size => Cells.Count;
    }

    /// <summary>
    /// Your kingdom: the throne, storehouses, blueprints, builder villagers, and
    /// everything built (floors, rugs, doors, furniture). It decides where things
    /// may be built, hands out jobs to idle villagers (oldest blueprint first),
    /// turns finished blueprints into real walls and buildings, tears things down
    /// when asked, and finds the rooms. One per game scene (<see cref="Current"/>).
    ///
    /// Things live on three layers so they can share a cell: floors at the bottom,
    /// rugs on them, and everything else (walls aside: those are in the
    /// <see cref="PixelWorld"/>) on top.
    /// </summary>
    public sealed class Kingdom : MonoBehaviour
    {
        /// <summary>Enclosed areas bigger than this count as open ground, not a room.</summary>
        public const int MaxRoomSize = 300;
        /// <summary>How many different pictures each kind of floor has.</summary>
        public const int FloorVariants = ArtCatalog.FloorVariants;

        public static Kingdom Current { get; private set; }

        KingdomConfig config;
        VillagerConfig villagerConfig;
        PixelWorld world;
        BuildingConfig[] menu = new BuildingConfig[0];

        readonly List<Storage> storages = new List<Storage>();
        readonly List<Blueprint> blueprints = new List<Blueprint>();
        readonly List<Villager> villagers = new List<Villager>();
        readonly List<Door> doors = new List<Door>();
        readonly List<Furniture> furniture = new List<Furniture>();
        // The three layers (see the class summary).
        readonly Dictionary<Vector2Int, Component> occupied = new Dictionary<Vector2Int, Component>();
        readonly Dictionary<Vector2Int, Component> floors = new Dictionary<Vector2Int, Component>();
        readonly Dictionary<Vector2Int, Component> rugs = new Dictionary<Vector2Int, Component>();
        readonly Dictionary<Vector2Int, ResourceNode> nodes = new Dictionary<Vector2Int, ResourceNode>();

        readonly Dictionary<BuildingConfig, Sprite[]> looks = new Dictionary<BuildingConfig, Sprite[]>();
        readonly Dictionary<BuildingConfig, Sprite[]> openLooks = new Dictionary<BuildingConfig, Sprite[]>();
        readonly Dictionary<BuildingConfig, Sprite[]> shadows = new Dictionary<BuildingConfig, Sprite[]>();
        readonly List<Sprite> madeSprites = new List<Sprite>();
        Sprite throneLook, throneShadow, brazier, flame;
        VillagerSprites villagerSprites;

        readonly List<Room> rooms = new List<Room>();
        readonly Dictionary<Vector2Int, Room> roomAt = new Dictionary<Vector2Int, Room>();
        bool roomsDirty = true;
        float roomsCheckAt;

        public KingdomConfig Config => config;
        public NavGrid Nav { get; private set; }
        public Throne Throne { get; private set; }
        public IReadOnlyList<Storage> Storages => storages;
        public IReadOnlyList<Blueprint> Blueprints => blueprints;
        public IReadOnlyList<Villager> Villagers => villagers;
        public IReadOnlyList<Door> Doors => doors;
        public IReadOnlyList<Furniture> AllFurniture => furniture;
        public IReadOnlyList<BuildingConfig> BuildMenu => menu;
        public IReadOnlyList<Room> Rooms => rooms;
        /// <summary>A plain white square sprite (for markers and bars).</summary>
        public Sprite White { get; private set; }
        /// <summary>A grid cell outline sprite (for placing buildings).</summary>
        public Sprite CellMarker { get; private set; }
        /// <summary>The ruler, so buildings don't appear on top of them.</summary>
        public Transform Ruler { get; set; }
        public float RulerRadius { get; set; } = 0.2f;
        /// <summary>Villagers that beds give a home to.</summary>
        public int Beds { get; private set; }
        /// <summary>How many villagers can live here: the starting ones, plus one per bed.</summary>
        public int VillagerCapacity => config.startVillagers + Beds;
        /// <summary>Something was built, torn down or broken (rooms and zombie paths change).</summary>
        public event Action Changed;

        void Awake()
        {
            if (Current != null && Current != this) Debug.LogWarning("There is more than one Kingdom in the scene.", this);
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            Nav?.Dispose();
            if (world != null) world.CellChanged -= OnWallChanged;
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
            Nav.NoStopping = cell => occupied.TryGetValue(cell, out var thing) && thing is Blueprint b && NeedsClearSpot(b.Config);
            foreach (var node in ResourceNode.All)
                if (node != null) nodes[node.Cell] = node;
            if (world != null) world.CellChanged += OnWallChanged;
            MakeSprites(otherBuildings);
        }

        void OnWallChanged(Vector2Int cell, bool added)
        {
            roomsDirty = true;
            Changed?.Invoke();
        }

        // ----------------------------------------------------------- pictures --

        // The baked pictures of everything that can be built, plus two small markers drawn here.
        void MakeSprites(BuildingConfig[] otherBuildings)
        {
            throneLook = ArtSprites.Get(ArtKeys.Throne);
            throneShadow = ArtSprites.Get(ArtKeys.Shadow(ArtKeys.Throne));
            brazier = ArtSprites.Get(ArtKeys.Brazier);
            flame = ArtSprites.Get(ArtKeys.Flame);
            White = Made(PixelTextures.ToSprite(BuildingArt.White(), "White"));
            CellMarker = Made(PixelTextures.ToSprite(BuildingArt.CellMarker(), "Cell marker"));

            var all = new List<BuildingConfig>(menu);
            if (otherBuildings != null) all.AddRange(otherBuildings);
            foreach (var b in all)
            {
                if (b == null || looks.ContainsKey(b)) continue;
                int turns = Mathf.Clamp(b.rotations, 1, 4);
                var look = new Sprite[4];
                var shadow = new Sprite[4];
                switch (b.kind)
                {
                    case BuildingKind.Wall:
                        if (b.wallMaterial != null) look[0] = ArtSprites.Get(ArtKeys.Wall(b.wallMaterial.name, 0)) ?? WallPicture(b.wallMaterial);
                        break;
                    case BuildingKind.Floor:
                        for (int v = 0; v < FloorVariants; v++) look[v] = ArtSprites.Get(ArtKeys.FloorTile(b.art, v));
                        break;
                    case BuildingKind.Door:
                    {
                        var open = new Sprite[4];
                        for (int r = 0; r < turns; r++)
                        {
                            look[r] = ArtSprites.Get(ArtKeys.DoorClosed(b.art, r));
                            open[r] = ArtSprites.Get(ArtKeys.DoorOpen(b.art, r));
                            shadow[r] = ArtSprites.Get(ArtKeys.Shadow(ArtKeys.DoorClosed(b.art, r)));
                        }
                        openLooks[b] = open;
                        break;
                    }
                    case BuildingKind.Storage when string.IsNullOrEmpty(b.art):
                        look[0] = ArtSprites.Get(ArtKeys.Storehouse);
                        shadow[0] = ArtSprites.Get(ArtKeys.Shadow(ArtKeys.Storehouse));
                        break;
                    default:
                        for (int r = 0; r < turns; r++)
                        {
                            look[r] = ArtSprites.Get(ArtKeys.Turned(b.art, r));
                            shadow[r] = ArtSprites.Get(ArtKeys.Shadow(ArtKeys.Turned(b.art, r)));
                        }
                        break;
                }
                for (int r = 1; r < 4; r++)
                {
                    // Directions it can't face use its first picture.
                    if (look[r] == null && b.kind != BuildingKind.Floor) look[r] = look[0];
                    if (shadow[r] == null) shadow[r] = shadow[0];
                }
                looks[b] = look;
                shadows[b] = shadow;
            }

            var walking = new Sprite[CharacterArt.VillagerDirections, CharacterArt.WalkFrames];
            var carrying = new Sprite[CharacterArt.VillagerDirections, CharacterArt.WalkFrames];
            for (int d = 0; d < CharacterArt.VillagerDirections; d++)
                for (int f = 0; f < CharacterArt.WalkFrames; f++)
                {
                    walking[d, f] = ArtSprites.Get(ArtKeys.Villager(false, d, f));
                    carrying[d, f] = ArtSprites.Get(ArtKeys.Villager(true, d, f));
                }
            var dead = new Sprite[CharacterArt.DeadDirections];
            for (int d = 0; d < dead.Length; d++) dead[d] = ArtSprites.Get(ArtKeys.VillagerDead(d));
            villagerSprites = new VillagerSprites
            {
                Walking = walking,
                Carrying = carrying,
                Shadow = ArtSprites.Get(ArtKeys.VillagerShadow),
                Dead = dead,
            };
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

        /// <summary>The picture for a building facing <paramref name="rotation"/> (its blueprint ghost uses it too).</summary>
        public Sprite LookOf(BuildingConfig b, int rotation = 0)
        {
            if (b == null || !looks.TryGetValue(b, out var s)) return White;
            var sprite = s[b.kind == BuildingKind.Floor ? 0 : b.ClampRotation(rotation)];
            return sprite != null ? sprite : White;
        }

        Sprite ShadowOf(BuildingConfig b, int rotation) =>
            b != null && shadows.TryGetValue(b, out var s) ? s[b.ClampRotation(rotation)] : null;

        // --------------------------------------------------------- buildings --

        public Throne PlaceThrone(Vector2Int cell)
        {
            var cells = new RectInt(cell, new Vector2Int(2, 2));
            ClearNature(cells);
            var go = new GameObject("Throne");
            go.transform.SetParent(transform, false);
            Throne = go.AddComponent<Throne>();
            Throne.Setup(cells, throneLook, throneShadow, brazier);
            Occupy(occupied, cells, Throne);
            Nav.Block(cells);
            roomsDirty = true;
            return Throne;
        }

        public Storage AddStorage(BuildingConfig b, Vector2Int cell, int rotation = 0)
        {
            rotation = b != null ? b.ClampRotation(rotation) : 0;
            var cells = new RectInt(cell, b != null ? b.SizeFacing(rotation) : new Vector2Int(2, 2));
            ClearNature(cells);
            var go = new GameObject(b != null ? b.displayName : "Storehouse");
            go.transform.SetParent(transform, false);
            var storage = go.AddComponent<Storage>();
            storage.Setup(b, cells, LookOf(b, rotation), ShadowOf(b, rotation), rotation);
            // The big storehouse has a lantern on its porch.
            if (b == null || string.IsNullOrEmpty(b.art)) storage.AddLantern(brazier);
            storages.Add(storage);
            Occupy(occupied, cells, storage);
            Nav.Block(cells);
            roomsDirty = true;
            Changed?.Invoke();
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

        /// <summary>A villager died (zombies): it's no longer one of yours.</summary>
        public void Lost(Villager v) => villagers.Remove(v);

        static void Occupy(Dictionary<Vector2Int, Component> layer, RectInt cells, Component by)
        {
            foreach (var c in cells.allPositionsWithin) layer[c] = by;
        }

        static void Free(Dictionary<Vector2Int, Component> layer, RectInt cells, Component by)
        {
            foreach (var c in cells.allPositionsWithin)
                if (layer.TryGetValue(c, out var o) && o == by) layer.Remove(c);
        }

        // Which layer a building goes on.
        Dictionary<Vector2Int, Component> LayerOf(BuildingConfig b) =>
            b.kind == BuildingKind.Floor ? floors : b.kind == BuildingKind.Furniture && b.flat ? rugs : occupied;

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

        // ------------------------------------------------------------ queries --

        /// <summary>What stands on the top layer of a cell: the throne, a storehouse, a door, furniture or a blueprint (null = nothing).</summary>
        public Component ThingAt(Vector2Int cell) => occupied.TryGetValue(cell, out var thing) ? thing : null;

        /// <summary>A tree, rock or bush that blocks this cell, or null.</summary>
        public ResourceNode NodeAt(Vector2Int cell) => nodes.TryGetValue(cell, out var node) && node != null && node.Blocks ? node : null;

        /// <summary>The room this cell is in, or null (outside).</summary>
        public Room RoomAt(Vector2Int cell)
        {
            UpdateRooms(true);
            return roomAt.TryGetValue(cell, out var room) ? room : null;
        }

        /// <summary>What tearing down would remove at this cell (furniture, a door, a rug, a floor or a chest), or null.</summary>
        public IBuilt BuiltAt(Vector2Int cell)
        {
            if (occupied.TryGetValue(cell, out var thing) && thing is IBuilt top && !(thing is Blueprint)) return top;
            if (rugs.TryGetValue(cell, out var rug) && rug is IBuilt r && !(rug is Blueprint)) return r;
            if (floors.TryGetValue(cell, out var floor) && floor is IBuilt f && !(floor is Blueprint)) return f;
            return null;
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
                int put = s.Deposit(type, amount - stored);
                if (put <= 0) break;
                stored += put;
            }
            return stored;
        }

        // -------------------------------------------------------- placement --

        public bool CanPlace(BuildingConfig b, Vector2Int cell, out string reason) => CanPlace(b, cell, 0, out reason);

        /// <summary>Whether <paramref name="b"/> facing <paramref name="rotation"/> can go with its bottom-left corner at <paramref name="cell"/>, and if not, why.</summary>
        public bool CanPlace(BuildingConfig b, Vector2Int cell, int rotation, out string reason)
        {
            var cells = new RectInt(cell, b.SizeFacing(b.ClampRotation(rotation)));
            var map = Nav.Cells;
            var layer = LayerOf(b);
            foreach (var c in cells.allPositionsWithin)
            {
                if (c.x < map.xMin + 1 || c.y < map.yMin + 1 || c.x >= map.xMax - 1 || c.y >= map.yMax - 1)
                {
                    reason = "Too close to the edge of the map";
                    return false;
                }
                if (layer.TryGetValue(c, out var thing) && thing != null)
                {
                    reason = thing is Blueprint ? "Already planned here"
                        : b.kind == BuildingKind.Floor ? "There's already a floor here"
                        : b.kind == BuildingKind.Furniture && b.flat ? "There's already a rug here"
                        : "Something is already built here";
                    return false;
                }
                // Floors and rugs go under furniture and doors, but not under the throne or a storehouse.
                if (layer != occupied && occupied.TryGetValue(c, out var top) && (top is Throne || top is Storage))
                {
                    reason = "Something is already built here";
                    return false;
                }
                if (world != null && world.ChunkAt(c) != null && !(b.kind == BuildingKind.Door && SingleWallAt(c)))
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

        public Blueprint PlaceBlueprint(BuildingConfig b, Vector2Int cell) => PlaceBlueprint(b, cell, 0);

        public Blueprint PlaceBlueprint(BuildingConfig b, Vector2Int cell, int rotation)
        {
            if (b == null) return null;
            rotation = b.ClampRotation(rotation);
            if (!CanPlace(b, cell, rotation, out _)) return null;
            var cells = new RectInt(cell, b.SizeFacing(rotation));
            var go = new GameObject($"Blueprint: {b.displayName}");
            go.transform.SetParent(transform, false);
            var blueprint = go.AddComponent<Blueprint>();
            blueprint.Setup(b, cells, rotation, LookOf(b, rotation), White);
            blueprints.Add(blueprint);
            Occupy(LayerOf(b), cells, blueprint);
            return blueprint;
        }

        /// <summary>The blueprint at a cell (top layer first, then rugs, then floors), or null.</summary>
        public Blueprint BlueprintAt(Vector2Int cell)
        {
            if (occupied.TryGetValue(cell, out var top) && top is Blueprint a) return a;
            if (rugs.TryGetValue(cell, out var rug) && rug is Blueprint b) return b;
            return floors.TryGetValue(cell, out var floor) ? floor as Blueprint : null;
        }

        /// <summary>Removes a blueprint and puts what was already delivered back into storage. Returns a message for the player.</summary>
        public string Cancel(Blueprint blueprint)
        {
            if (blueprint == null || blueprint.Cancelled) return null;
            blueprint.MarkCancelled();
            blueprints.Remove(blueprint);
            Free(LayerOf(blueprint.Config), blueprint.Cells, blueprint);
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

        // ------------------------------------------------------ tearing down --

        /// <summary>What tearing down the thing at <paramref name="cell"/> would do, for the info box (null = nothing to tear down).</summary>
        public string TearDownText(Vector2Int cell)
        {
            var thing = BuiltAt(cell);
            if (thing != null) return $"Right click: tear down the {thing.Config.displayName} (half its cost back)";
            var wall = world != null ? world.ChunkAt(cell) : null;
            return wall != null ? "Right click: tear down this wall (half its cost back)" : null;
        }

        /// <summary>Tears down what's at <paramref name="cell"/> and gives half its cost back. Returns a message for the player.</summary>
        public string TearDown(Vector2Int cell)
        {
            var thing = BuiltAt(cell);
            if (thing != null)
            {
                if (thing is Storage storage)
                {
                    if (storages.Count <= 1) return "You need at least one storehouse";
                    // Move what's inside to the other storehouses first.
                    storages.Remove(storage);
                    int lostGoods = 0;
                    for (int i = 0; i < Inventory.TypeCount; i++)
                    {
                        var type = (ResourceType)i;
                        int have = storage.Stock.Get(type);
                        lostGoods += have - Store(type, have, storage.Center);
                    }
                    storages.Add(storage);
                    if (lostGoods > 0) Debug.Log($"{lostGoods} resources didn't fit in the other storehouses and were lost.");
                }
                Refund(thing.Config, 1, thing.Cells.center);
                Forget(thing);
                thing.Remove();
                return $"Tore down the {thing.Config.displayName}";
            }

            var chunk = world != null ? world.ChunkAt(cell) : null;
            if (chunk == null) return null;
            var structure = chunk.Owner;
            var wallConfig = WallConfigOf(structure.Material);
            if (wallConfig != null) Refund(wallConfig, structure.Cells.width * structure.Cells.height, structure.Cells.center);
            structure.Remove();
            return "Tore down the wall";
        }

        // A wall tile on its own (not part of a bigger structure like the test outpost's): a door can replace it.
        bool SingleWallAt(Vector2Int cell)
        {
            var chunk = world != null ? world.ChunkAt(cell) : null;
            return chunk != null && chunk.Owner != null && chunk.Owner.Cells.width == 1 && chunk.Owner.Cells.height == 1;
        }

        BuildingConfig WallConfigOf(StructureMaterial material)
        {
            foreach (var b in menu)
                if (b != null && b.kind == BuildingKind.Wall && b.wallMaterial == material) return b;
            return null;
        }

        /// <summary>About the wall at <paramref name="cell"/> for the info box, like "Stone wall: 420 / 600 against zombies", or null.</summary>
        public string WallInfo(Vector2Int cell)
        {
            var chunk = world != null ? world.ChunkAt(cell) : null;
            var wall = chunk != null ? chunk.Owner : null;
            if (wall == null || wall.Material == null) return null;
            var config = WallConfigOf(wall.Material);
            float full = wall.Material.siegeHealth * wall.Cells.width * wall.Cells.height;
            return $"{(config != null ? config.displayName : "Wall")}: {Mathf.CeilToInt(wall.SiegeHealthLeft)} / {Mathf.CeilToInt(full)} against zombies";
        }

        /// <summary>
        /// Which way a door at <paramref name="cell"/> should face to fit the walls (or wall
        /// blueprints) beside it: 0 in a wall running left to right, 1 up and down, or
        /// <paramref name="otherwise"/> if that's not clear.
        /// </summary>
        public int DoorFacing(Vector2Int cell, int otherwise)
        {
            bool sides = WallLike(cell + Vector2Int.left) || WallLike(cell + Vector2Int.right);
            bool ends = WallLike(cell + Vector2Int.up) || WallLike(cell + Vector2Int.down);
            return sides && !ends ? 0 : ends && !sides ? 1 : otherwise;
        }

        bool WallLike(Vector2Int cell)
        {
            if (world != null && world.ChunkAt(cell) != null) return true;
            if (!occupied.TryGetValue(cell, out var thing)) return false;
            return thing is Door || (thing is Blueprint b && (b.Config.kind == BuildingKind.Wall || b.Config.kind == BuildingKind.Door));
        }

        // Gives back half the cost of <paramref name="count"/> of a building.
        void Refund(BuildingConfig b, int count, Vector2 near)
        {
            for (int i = 0; i < Inventory.TypeCount; i++)
            {
                var type = (ResourceType)i;
                Store(type, b.Cost(type) * count / 2, near);
            }
        }

        /// <summary>A door or piece of furniture was smashed (zombies, explosions): it's gone, with a burst of splinters.</summary>
        public void Broke(IBuilt thing)
        {
            if (thing == null) return;
            if (world != null)
            {
                var c = thing.Config;
                var splinters = c.stoneCost > c.woodCost ? new Color32(120, 116, 110, 255) : new Color32(120, 86, 54, 255);
                world.Burst(thing.Cells.center, splinters, 16, 3f);
            }
            Forget(thing);
            thing.Remove();
        }

        // Takes a built thing off the map's books (layers, paths, lists).
        void Forget(IBuilt thing)
        {
            var component = thing as Component;
            Free(LayerOf(thing.Config), thing.Cells, component);
            if (thing is Storage s)
            {
                storages.Remove(s);
                Nav.Unblock(thing.Cells);
            }
            else if (thing is Door d)
            {
                doors.Remove(d);
            }
            else if (thing is Furniture f)
            {
                furniture.Remove(f);
                if (f.Config.blocksMovement && !f.Config.flat) Nav.Unblock(thing.Cells);
                if (f.Config.sleeps > 0) Beds = Mathf.Max(0, Beds - f.Config.sleeps);
            }
            roomsDirty = true;
            Changed?.Invoke();
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
                            b.Problem = $"Waiting for {missing} {(type == ResourceType.Food && b.Config.kind == BuildingKind.Furniture ? "cloth (food)" : type.ToString().ToLowerInvariant())} in storage";
                    }
                    if (b.Problem == null && b.UnreachableUntil > Time.time) b.Problem = "Builders can't find a way there";
                    continue;
                }
                if (b.WorkDone) TryComplete(b);
                else if (b.UnreachableUntil > Time.time) b.Problem = "Builders can't find a way there";
            }
            UpdateRooms(false);
        }

        // Walls, storehouses and things you can't walk through can't appear on top of someone.
        static bool NeedsClearSpot(BuildingConfig b) =>
            b.kind == BuildingKind.Wall || b.kind == BuildingKind.Storage || (b.kind == BuildingKind.Furniture && b.blocksMovement && !b.flat);

        void TryComplete(Blueprint b)
        {
            if (NeedsClearSpot(b.Config))
            {
                // Nobody may be standing where it appears.
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
                foreach (var h in Health.Creatures)
                    if (h != null && h.Team == Team.Zombie && Overlaps(area, h.Position, h.Radius))
                    {
                        b.Problem = "Finished, but zombies are standing on it";
                        return;
                    }
                if (blocked)
                {
                    b.Problem = "Finished: waiting for a villager to step off";
                    return;
                }
            }

            blueprints.Remove(b);
            Free(LayerOf(b.Config), b.Cells, b);
            ClearNature(b.Cells);
            Build(b.Config, b.Cells, b.Rotation);
            Destroy(b.gameObject);
        }

        /// <summary>Puts up a finished building (also used for things the game places itself).</summary>
        public Component Build(BuildingConfig config, RectInt cells, int rotation)
        {
            rotation = config.ClampRotation(rotation);
            Component made = null;
            switch (config.kind)
            {
                case BuildingKind.Storage:
                    made = AddStorage(config, cells.position, rotation);
                    break;
                case BuildingKind.Floor:
                    foreach (var c in cells.allPositionsWithin)
                    {
                        var go = new GameObject(config.displayName);
                        go.transform.SetParent(transform, false);
                        var tile = go.AddComponent<FloorTile>();
                        uint pick = DamageShape.Hash(0xF1u, c.x, c.y) % (uint)FloorVariants;
                        var sprites = looks.TryGetValue(config, out var s) ? s : null;
                        tile.Setup(config, c, sprites != null && sprites[pick] != null ? sprites[pick] : LookOf(config));
                        Occupy(floors, new RectInt(c, Vector2Int.one), tile);
                        made = tile;
                    }
                    break;
                case BuildingKind.Door:
                {
                    // A door built into a wall replaces that wall tile (half its cost comes back).
                    foreach (var c in cells.allPositionsWithin)
                    {
                        if (!SingleWallAt(c)) continue;
                        var wall = world.ChunkAt(c).Owner;
                        var wallConfig = WallConfigOf(wall.Material);
                        if (wallConfig != null) Refund(wallConfig, 1, wall.Cells.center);
                        wall.Remove();
                    }
                    var go = new GameObject(config.displayName);
                    go.transform.SetParent(transform, false);
                    var door = go.AddComponent<Door>();
                    var open = openLooks.TryGetValue(config, out var o) ? o[rotation] : null;
                    door.Setup(config, cells, rotation, LookOf(config, rotation), open, ShadowOf(config, rotation), this);
                    doors.Add(door);
                    Occupy(occupied, cells, door);
                    made = door;
                    break;
                }
                case BuildingKind.Furniture:
                {
                    var go = new GameObject(config.displayName);
                    go.transform.SetParent(transform, false);
                    var piece = go.AddComponent<Furniture>();
                    piece.Setup(config, cells, rotation, LookOf(config, rotation), ShadowOf(config, rotation), flame, this);
                    furniture.Add(piece);
                    Occupy(LayerOf(config), cells, piece);
                    if (config.blocksMovement && !config.flat) Nav.Block(cells);
                    Beds += config.sleeps;
                    made = piece;
                    break;
                }
                default:
                {
                    if (config.wallMaterial == null || world == null)
                    {
                        Debug.LogWarning($"{config.name} has no Wall Material, so nothing was built.", config);
                        break;
                    }
                    var go = new GameObject(config.displayName);
                    go.transform.SetParent(transform, false);
                    go.transform.position = new Vector3(cells.x, cells.y, 0f);
                    var wall = go.AddComponent<DestructibleStructure>();
                    wall.Build(world, config.wallMaterial, cells.size);
                    made = wall;
                    break;
                }
            }
            roomsDirty = true;
            Changed?.Invoke();
            return made;
        }

        static bool Overlaps(Rect area, Vector2 point, float radius) =>
            point.x > area.xMin - radius && point.x < area.xMax + radius && point.y > area.yMin - radius && point.y < area.yMax + radius;

        // ------------------------------------------------------------- rooms --

        // Finds the rooms again if something changed (at most four times a second, unless asked now).
        void UpdateRooms(bool now)
        {
            if (!roomsDirty || Nav == null || (!now && Time.time < roomsCheckAt)) return;
            roomsDirty = false;
            roomsCheckAt = Time.time + 0.25f;

            var map = Nav.Cells;
            int w = map.width, h = map.height;
            var walls = new bool[w * h];
            if (world != null)
                foreach (var cell in world.ChunkCells)
                    if (map.Contains(cell)) walls[(cell.y - map.yMin) * w + (cell.x - map.xMin)] = true;
            foreach (var d in doors)
                if (d != null)
                    foreach (var cell in d.Cells.allPositionsWithin)
                        if (map.Contains(cell)) walls[(cell.y - map.yMin) * w + (cell.x - map.xMin)] = true;

            rooms.Clear();
            roomAt.Clear();
            var seen = new HashSet<Component>();
            var tags = new Dictionary<RoomTag, int>();
            foreach (var cellsOfRoom in RoomFinder.Find(walls, w, h, MaxRoomSize))
            {
                var room = new Room();
                var sum = Vector2.zero;
                bool throne = false;
                seen.Clear();
                tags.Clear();
                foreach (int i in cellsOfRoom)
                {
                    var cell = new Vector2Int(map.xMin + i % w, map.yMin + i / w);
                    room.Cells.Add(cell);
                    roomAt[cell] = room;
                    sum += (Vector2)cell + Vector2.one * 0.5f;
                    foreach (var layer in new[] { occupied, rugs })
                    {
                        if (!layer.TryGetValue(cell, out var thing) || thing == null || !seen.Add(thing)) continue;
                        if (thing is Throne) throne = true;
                        else if (thing is IBuilt built && !(thing is Blueprint) && built.Config != null)
                            tags[built.Config.roomTag] = (tags.TryGetValue(built.Config.roomTag, out int n) ? n : 0) + 1;
                    }
                }
                room.Name = NameOf(room.Size, throne, tags);
                // Label it at the cell nearest the middle.
                var middle = sum / room.Size;
                float best = float.MaxValue;
                foreach (var cell in room.Cells)
                {
                    var at = (Vector2)cell + Vector2.one * 0.5f;
                    float d = (at - middle).sqrMagnitude;
                    if (d < best) { best = d; room.LabelAt = at; }
                }
                rooms.Add(room);
            }
        }

        static string NameOf(int size, bool throne, Dictionary<RoomTag, int> tags)
        {
            int Count(RoomTag tag) => tags.TryGetValue(tag, out int n) ? n : 0;
            if (throne) return "Throne room";
            if (Count(RoomTag.Bed) >= 3) return "Barracks";
            if (Count(RoomTag.Bed) > 0) return "Bedroom";
            if (Count(RoomTag.Table) > 0 && Count(RoomTag.Seat) > 0) return size >= 24 ? "Great hall" : "Dining hall";
            if (Count(RoomTag.Books) > 0) return "Library";
            if (Count(RoomTag.Weapons) > 0) return "Armory";
            if (Count(RoomTag.Cooking) > 0) return "Kitchen";
            if (Count(RoomTag.Workshop) > 0) return "Workshop";
            if (Count(RoomTag.Storage) > 0) return "Storeroom";
            return size <= 4 ? "Small room" : "Empty room";
        }
    }
}
