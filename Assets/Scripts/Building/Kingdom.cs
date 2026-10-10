using System;
using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.Progression;
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
        readonly List<House> houses = new List<House>();
        readonly List<MineShaft> mines = new List<MineShaft>();
        readonly HashSet<int> usedMineSlots = new HashSet<int>();
        readonly List<Place> places = new List<Place>();
        readonly HashSet<int> usedSlots = new HashSet<int>();
        readonly Dictionary<(BuildingConfig, int, int), Sprite> houseLooks = new Dictionary<(BuildingConfig, int, int), Sprite>();
        readonly Dictionary<(BuildingConfig, int), Sprite> houseShadows = new Dictionary<(BuildingConfig, int), Sprite>();
        readonly Dictionary<(BuildingConfig, int), BuildingConfig> upgradeJobs = new Dictionary<(BuildingConfig, int), BuildingConfig>();
        readonly Dictionary<StructureMaterial, Sprite> wallPictures = new Dictionary<StructureMaterial, Sprite>();
        // The three layers (see the class summary).
        readonly Dictionary<Vector2Int, Component> occupied = new Dictionary<Vector2Int, Component>();
        readonly Dictionary<Vector2Int, Component> floors = new Dictionary<Vector2Int, Component>();
        readonly Dictionary<Vector2Int, Component> rugs = new Dictionary<Vector2Int, Component>();
        readonly Dictionary<Vector2Int, Component> roofs = new Dictionary<Vector2Int, Component>();
        // Walls and doors that went (roofs near them may have lost what held them up).
        readonly HashSet<Vector2Int> supportLost = new HashSet<Vector2Int>();
        RoofView roofView;
        // Every tree, rock and bush on the map.
        NatureMap nature;
        // Loading a saved game: the inside or mine square the next house or mine gets (-1: the first free one).
        int restoreSlot = -1;

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
        public IReadOnlyList<House> Houses => houses;
        /// <summary>Every place you go into: houses' insides and mines.</summary>
        public IReadOnlyList<Place> Places => places;
        /// <summary>Where the insides of houses are: past the world's edge (south), one square each.</summary>
        public static readonly Vector2Int InsideOrigin = new Vector2Int(0, -36000);
        /// <summary>Where the mines are (further south), one square of <see cref="MineSlot"/> tiles each.</summary>
        public static readonly Vector2Int MineOrigin = new Vector2Int(0, -40000);
        public const int MineSlot = 100;
        public IReadOnlyList<MineShaft> Mines => mines;
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
        /// <summary>Fades tree leaves near the ruler (planted trees join it).</summary>
        public World.CanopyFader Canopies { get; set; }

        /// <summary>The ruler is on the throne building (roofs turn see-through).</summary>
        public bool ThroneView { get; set; }
        public RoofView Roofs => roofView;

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
            foreach (var job in upgradeJobs.Values)
                if (job != null) Destroy(job);
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
            nature = NatureMap.Current;
            Nav = new NavGrid(mapCells, world, nature);
            // Builders shouldn't stand on a planned building's spot while they work (it couldn't be finished).
            Nav.NoStopping = cell => occupied.TryGetValue(cell, out var thing) && thing is Blueprint b && NeedsClearSpot(b.Config);
            if (world != null) world.CellChanged += OnWallChanged;
            MakeSprites(otherBuildings);
            roofView = new GameObject("Roofs").AddComponent<RoofView>();
            roofView.transform.SetParent(transform, false);
            roofView.Setup(this, config.roofDetail);
        }

        void OnWallChanged(Vector2Int cell, bool added)
        {
            roomsDirty = true;
            if (!added) supportLost.Add(cell);
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
                    case BuildingKind.House:
                        for (int r = 0; r < turns; r++) look[r] = HouseLook(b, 0, r);
                        break;
                    case BuildingKind.Plant:
                        if (b.plant != null)
                            look[0] = b.plant.look == NodeLook.Tree ? ArtSprites.Get(ArtKeys.TreeLeaves(0)) : ArtSprites.Get(ArtKeys.BushBerries(0));
                        break;
                    case BuildingKind.Roof:
                        // A sample of the roof (the ghost while placing it); the roofs themselves are drawn for their shape.
                        look[0] = Made(PixelTextures.ToSprite(RoofArt.Swatch(64, b.roofStyle), b.name, false, 64));
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

        /// <summary>A house of level <paramref name="level"/> facing <paramref name="rotation"/> (drawn the first time it's needed, then shared).</summary>
        public Sprite HouseLook(BuildingConfig b, int level, int rotation)
        {
            if (b == null) return White;
            if (houseLooks.TryGetValue((b, level, rotation), out var sprite)) return sprite;
            var l = b.house != null ? b.house.Level(level) : new HouseLevel();
            bool heavy = l.doorArt == FurnitureArt.HeavyDoor;
            var image = HouseArt.Exterior(b.size, rotation, l.roof, l.chimney, heavy ? PixelImage.Rgb(84, 70, 58) : PixelImage.Rgb(104, 72, 44), 64, 17u + (uint)level * 7u);
            sprite = Made(PixelTextures.ToSprite(image, $"{l.displayName} {rotation}", false, 64));
            houseLooks[(b, level, rotation)] = sprite;
            return sprite;
        }

        /// <summary>A house's shadow on the ground.</summary>
        public Sprite HouseShadow(BuildingConfig b, int rotation)
        {
            if (b == null) return null;
            if (houseShadows.TryGetValue((b, rotation), out var sprite)) return sprite;
            sprite = Made(PixelTextures.ToSprite(HouseArt.Shadow(b.size, rotation, 8), $"{b.displayName} shadow {rotation}", false, 8));
            houseShadows[(b, rotation)] = sprite;
            return sprite;
        }

        /// <summary>A wall tile's picture for the walls inside houses (the baked one, or one drawn here).</summary>
        public Sprite WallSprite(StructureMaterial material, Vector2Int cell)
        {
            if (material == null) return null;
            int variants = Mathf.Clamp(material.variants, 1, 8);
            var baked = ArtSprites.Get(ArtKeys.Wall(material.name, (int)(DamageShape.Hash(0x51u, cell.x, cell.y) % (uint)variants)));
            if (baked != null) return baked;
            if (!wallPictures.TryGetValue(material, out var sprite)) wallPictures[material] = sprite = WallPicture(material);
            return sprite;
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
            storage.Setup(b, cells, LookOf(b, rotation), ShadowOf(b, rotation), rotation, this);
            // The big storehouse has a lantern on its porch.
            if (b == null || string.IsNullOrEmpty(b.art)) storage.AddLantern(brazier);
            storages.Add(storage);
            Occupy(occupied, cells, storage);
            NavFor(cells).Block(cells);
            roomsDirty = true;
            Changed?.Invoke();
            return storage;
        }

        public Villager AddVillager(Vector2 position, Job job = Job.Builder)
        {
            var go = new GameObject("Villager");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var v = go.AddComponent<Villager>();
            v.Setup(villagerConfig, this, Nav, villagerSprites);
            v.Job = job;
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
            b.kind == BuildingKind.Floor ? floors : b.kind == BuildingKind.Roof ? roofs : b.kind == BuildingKind.Furniture && b.flat ? rugs : occupied;

        // Removes trees, stumps, rocks and bushes standing in these cells.
        void ClearNature(RectInt cells)
        {
            if (nature == null) return;
            foreach (var c in cells.allPositionsWithin) nature.Clear(c);
        }

        // ------------------------------------------------------------ queries --

        /// <summary>What stands on the top layer of a cell: the throne, a storehouse, a door, furniture or a blueprint (null = nothing).</summary>
        public Component ThingAt(Vector2Int cell) => occupied.TryGetValue(cell, out var thing) ? thing : null;

        /// <summary>The house inside or mine that this cell is in, or null on the map.</summary>
        public Place PlaceAt(Vector2Int cell)
        {
            if (NatureMap.Inside(cell)) return null;   // in the world, not in a house or mine
            foreach (var s in places)
                if (s != null && s.Contains(cell)) return s;
            return null;
        }

        public Place PlaceAt(Vector2 position) => PlaceAt(NavGrid.CellAt(position));

        /// <summary>A house inside or mine joins (or leaves) the kingdom's places.</summary>
        public void AddPlace(Place space) { if (space != null && !places.Contains(space)) places.Add(space); }
        public void RemovePlace(Place space) => places.Remove(space);

        /// <summary>A free square far off the map for a new inside or mine (they're all the same size).</summary>
        public Vector2Int TakeSlot(out int slot)
        {
            slot = 0;
            if (restoreSlot >= 0 && !usedSlots.Contains(restoreSlot)) slot = restoreSlot;
            while (usedSlots.Contains(slot)) slot++;
            usedSlots.Add(slot);
            return InsideOrigin + new Vector2Int(slot % 64, slot / 64) * Interior.SlotSize;
        }

        public void FreeSlot(int slot) => usedSlots.Remove(slot);

        /// <summary>The house standing on this cell of the map, or null.</summary>
        public House HouseAt(Vector2Int cell) => occupied.TryGetValue(cell, out var thing) ? thing as House : null;

        /// <summary>Where walkers find their way in these cells: inside a house, or the map.</summary>
        public NavGrid NavFor(RectInt cells)
        {
            var inside = PlaceAt(cells.position);
            return inside != null && inside.Nav != null ? inside.Nav : Nav;
        }

        /// <summary>A house's room was made (again): what's built in it stands in the way of walkers there.</summary>
        public void BlockInside(Place inside)
        {
            if (inside == null || inside.Nav == null) return;
            foreach (var f in furniture)
                if (f != null && f.Config.blocksMovement && !f.Config.flat && inside.Contains(f.Cells.position)) inside.Nav.Block(f.Cells);
            foreach (var s in storages)
                if (s != null && inside.Contains(s.Cells.position)) inside.Nav.Block(s.Cells);
        }

        /// <summary>Whether a roof of this kind is planned (not built yet) at a cell.</summary>
        public bool RoofPlanned(Vector2Int cell, BuildingConfig config) =>
            roofs.TryGetValue(cell, out var thing) && thing is Blueprint b && b.Config == config;

        /// <summary>The roof over a cell (built, not planned), or null.</summary>
        public RoofTile RoofAt(Vector2Int cell) => roofs.TryGetValue(cell, out var r) ? r as RoofTile : null;

        /// <summary>A tree, rock or bush that blocks this cell, or null.</summary>
        public ResourceNode NodeAt(Vector2Int cell)
        {
            var node = nature != null ? nature.Live(cell) : null;
            return node != null && node.Blocks ? node : null;
        }

        /// <summary>The room this cell is in, or null (outside).</summary>
        public Room RoomAt(Vector2Int cell)
        {
            UpdateRooms(true);
            return roomAt.TryGetValue(cell, out var room) ? room : null;
        }

        /// <summary>What tearing down would remove at this cell (furniture, a door, a rug, a floor or a chest), or null.</summary>
        public IBuilt BuiltAt(Vector2Int cell)
        {
            // A wall built over a floor comes first (walls aren't on these layers: they're in the PixelWorld).
            if (world != null && world.ChunkAt(cell) != null) return null;
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

        /// <summary>How much of <paramref name="type"/> is in storage and not promised to a builder.</summary>
        public int Available(ResourceType type)
        {
            int total = 0;
            foreach (var s in storages) total += s.Available(type);
            return total;
        }

        /// <summary>
        /// Takes resources out of storage (research costs), never what builders have been
        /// promised. Takes nothing and returns false if there isn't enough of everything.
        /// </summary>
        /// <summary>Takes <paramref name="amounts"/> (one per resource type) out of storage, if it's all there.</summary>
        public bool TrySpend(int[] amounts)
        {
            for (int i = 0; i < amounts.Length && i < Inventory.TypeCount; i++)
                if (Available((ResourceType)i) < amounts[i]) return false;
            for (int i = 0; i < amounts.Length && i < Inventory.TypeCount; i++)
                if (amounts[i] > 0) Take((ResourceType)i, amounts[i]);
            return true;
        }

        public bool TrySpend(int wood, int stone, int food)
        {
            if (Available(ResourceType.Wood) < wood || Available(ResourceType.Stone) < stone || Available(ResourceType.Food) < food) return false;
            Take(ResourceType.Wood, wood);
            Take(ResourceType.Stone, stone);
            Take(ResourceType.Food, food);
            return true;
        }

        void Take(ResourceType type, int amount)
        {
            foreach (var s in storages)
            {
                if (amount <= 0) return;
                int take = Mathf.Min(amount, s.Available(type));
                if (take > 0 && s.Stock.TryRemove(type, take)) amount -= take;
            }
        }

        /// <summary>
        /// About how far it is to walk from <paramref name="a"/> to <paramref name="b"/>: straight there in the
        /// same place, otherwise out through the house doors in between (insides of houses are far off the map).
        /// </summary>
        public float Travel(Vector2 a, Vector2 b)
        {
            var ia = PlaceAt(a);
            var ib = PlaceAt(b);
            if (ia == ib) return (a - b).magnitude;
            float d = 0f;
            if (ia != null) { d += (a - ia.Entrance).magnitude; a = ia.Outside; }
            if (ib != null) { d += (b - ib.Entrance).magnitude; b = ib.Outside; }
            return d + (a - b).magnitude;
        }

        /// <summary>The closest storehouse that has some of <paramref name="type"/> not promised to anyone.</summary>
        public Storage NearestWith(ResourceType type, Vector2 from)
        {
            Storage best = null;
            float bestDistance = float.MaxValue;
            foreach (var s in storages)
            {
                if (s.Available(type) <= 0) continue;
                float d = Travel(from, s.Center);
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
                float d = Travel(from, s.Center);
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
            var inside = PlaceAt(cell);
            if (b.kind == BuildingKind.Dig)
            {
                if (inside is MineLevel mine) return mine.CanMark(cell, out reason);
                reason = "Dig inside a mine: climb down and press B";
                return false;
            }
            if (inside != null) return CanPlaceInside(b, cells, inside, out reason);
            if (b.kind == BuildingKind.Roof) return CanRoof(cells, out reason);
            var map = Nav.Cells;
            var layer = LayerOf(b);
            if (b.kind == BuildingKind.House || b.kind == BuildingKind.Mine)
            {
                var front = b.kind == BuildingKind.Mine ? MineShaft.FrontCell(cells) : House.FrontCell(cells, b.ClampRotation(rotation));
                if (!Nav.Walkable(front) || occupied.ContainsKey(front))
                {
                    reason = "The door would open onto something (R turns it)";
                    return false;
                }
            }
            foreach (var c in cells.allPositionsWithin)
            {
                if (c.x < map.xMin + 1 || c.y < map.yMin + 1 || c.x >= map.xMax - 1 || c.y >= map.yMax - 1)
                {
                    reason = "Too far from your throne: your kingdom's lands end here";
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
                if (NeedsClearSpot(b) && InFrontOfHouse(c))
                {
                    reason = "Keep the house's door clear";
                    return false;
                }
                if (b.climb && !WallLike(LeansOn(cells, b.ClampRotation(rotation))))
                {
                    reason = "A ladder has to lean on a wall (R turns it)";
                    return false;
                }
                if (nature != null && nature.Standing(c, out var look))
                {
                    reason = look == NodeLook.Rock ? "A rock is in the way (gather it first: hold E)"
                        : look == NodeLook.Tree ? "A tree is in the way (chop it first: hold E)"
                        : "A bush is in the way (pick it first: hold E)";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        // The cell in front of a house's door (walls and furniture mustn't block it).
        bool InFrontOfHouse(Vector2Int cell)
        {
            foreach (var h in houses)
                if (h != null && h.OutsideCell == cell) return true;
            foreach (var m in mines)
                if (m != null && m.OutsideCell == cell) return true;
            foreach (var b in blueprints)
            {
                if (b == null || b.Cancelled || b.Upgrades != null) continue;
                if (b.Config.kind == BuildingKind.House && House.FrontCell(b.Cells, b.Rotation) == cell) return true;
                if (b.Config.kind == BuildingKind.Mine && MineShaft.FrontCell(b.Cells) == cell) return true;
            }
            return false;
        }

        /// <summary>Whether something can be built inside a house (furniture, lights, chests, floors and rugs can).</summary>
        public static bool BuildsInside(BuildingConfig b) =>
            b != null && !b.climb && b.defense == null && (b.kind == BuildingKind.Furniture || b.kind == BuildingKind.Floor
                || (b.kind == BuildingKind.Storage && !string.IsNullOrEmpty(b.art)));

        bool CanPlaceInside(BuildingConfig b, RectInt cells, Place inside, out string reason)
        {
            if (!BuildsInside(b))
            {
                reason = $"A {b.displayName.ToLowerInvariant()} can't go inside a house";
                return false;
            }
            var layer = LayerOf(b);
            foreach (var c in cells.allPositionsWithin)
            {
                if (!inside.CanBuildAt(c))
                {
                    reason = inside.CantBuildReason(c);
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
                if (c == inside.EntranceCell && NeedsClearSpot(b))
                {
                    reason = "Keep the way in clear";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        // A roof can go over anything but the big storehouse (it has its own) and trees, as long as walls hold it up.
        bool CanRoof(RectInt cells, out string reason)
        {
            var map = Nav.Cells;
            foreach (var c in cells.allPositionsWithin)
            {
                if (c.x < map.xMin + 1 || c.y < map.yMin + 1 || c.x >= map.xMax - 1 || c.y >= map.yMax - 1)
                {
                    reason = "Too far from your throne: your kingdom's lands end here";
                    return false;
                }
                if (roofs.TryGetValue(c, out var thing) && thing != null)
                {
                    reason = thing is Blueprint ? "Already planned here" : "There's already a roof here";
                    return false;
                }
                if (occupied.TryGetValue(c, out var top) && top is Storage store && (store.Config == null || string.IsNullOrEmpty(store.Config.art)))
                {
                    reason = "The storehouse has its own roof";
                    return false;
                }
                if (nature != null && nature.Standing(c, out var look) && look == NodeLook.Tree)
                {
                    reason = "A tree is in the way (chop it first: hold E)";
                    return false;
                }
                if (!RoofHeld(c, true))
                {
                    reason = $"A roof needs a wall or door within {config.roofReach} tiles to hold it up";
                    return false;
                }
            }
            reason = null;
            return true;
        }

        /// <summary>Whether a wall or door (or with <paramref name="planned"/>, one that's planned) is close enough to hold up a roof at <paramref name="cell"/>.</summary>
        public bool RoofHeld(Vector2Int cell, bool planned)
        {
            int reach = Mathf.Max(1, config.roofReach);
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    var c = new Vector2Int(cell.x + dx, cell.y + dy);
                    if (planned ? WallLike(c) : WallTopAt(c)) return true;
                }
            return false;
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

        /// <summary>The blueprint at a cell (top layer first, then rugs, then floors, then roofs), or null.</summary>
        public Blueprint BlueprintAt(Vector2Int cell)
        {
            if (occupied.TryGetValue(cell, out var top) && top is Blueprint a) return a;
            if (rugs.TryGetValue(cell, out var rug) && rug is Blueprint b) return b;
            if (floors.TryGetValue(cell, out var floor) && floor is Blueprint f) return f;
            return roofs.TryGetValue(cell, out var roof) ? roof as Blueprint : null;
        }

        /// <summary>Removes a blueprint and puts what was already delivered back into storage. Returns a message for the player.</summary>
        public string Cancel(Blueprint blueprint)
        {
            if (blueprint == null || blueprint.Cancelled) return null;
            blueprint.MarkCancelled();
            blueprints.Remove(blueprint);
            if (blueprint.Upgrades != null) blueprint.Upgrades.Upgrade = null;
            if (blueprint.Config.kind == BuildingKind.Roof && roofView != null)
                foreach (var c in blueprint.Cells.allPositionsWithin) roofView.Touch(c);
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
            if (thing is House house) return $"Right click: tear down the {house.Name} and everything inside (half the cost back)";
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
                string spilled = "";
                if (thing is Storage storage)
                {
                    if (storages.Count <= 1) return "You need at least one storehouse";
                    // Move what's inside to the other storehouses first; what doesn't fit is left in a bag.
                    storages.Remove(storage);
                    var left = new int[Inventory.TypeCount];
                    for (int i = 0; i < Inventory.TypeCount; i++)
                    {
                        var type = (ResourceType)i;
                        int have = storage.Stock.Get(type);
                        left[i] = have - Store(type, have, storage.Center);
                    }
                    if (LootBag.Drop(storage.Center, left) != null)
                        spilled = ". The other storehouses are full: the rest is in a bag on the ground";
                }
                if (thing is MineShaft mineShaft)
                {
                    int dugUp = ClearInside(mineShaft.Level, mineShaft.Outside, true);
                    Forget(mineShaft);
                    Refund(mineShaft.Config, 1, mineShaft.Center);
                    mineShaft.Remove();
                    return "Filled in the mine" + (dugUp > 0 ? $" and took out the {dugUp} things in it (half their cost back)" : "");
                }
                if (thing is House house)
                {
                    int things = ClearInside(house, true);
                    Forget(house);
                    RefundHouse(house);
                    house.Remove();
                    return $"Tore down the {house.Name}" + (things > 0 ? $" and the {things} things inside (half their cost back)" : "");
                }
                // Off the books first, so the refund doesn't go into the thing being torn down.
                Forget(thing);
                Refund(thing.Config, 1, thing.Cells.center);
                thing.Remove();
                return $"Tore down the {thing.Config.displayName}{spilled}";
            }

            var chunk = world != null ? world.ChunkAt(cell) : null;
            if (chunk == null) return null;
            var structure = chunk.Owner;
            var wallConfig = WallConfigOf(structure.Material);
            if (wallConfig != null) Refund(wallConfig, structure.Cells.width * structure.Cells.height, structure.Cells.center);
            structure.Remove();
            return "Tore down the wall";
        }

        // Half of what a house cost: the house itself and every upgrade it had.
        void RefundHouse(House house)
        {
            Refund(house.Config, 1, house.Center);
            if (house.Config.house == null) return;
            for (int l = 1; l <= house.Level; l++)
            {
                var level = house.Config.house.Level(l);
                Store(ResourceType.Wood, level.woodCost / 2, house.Center);
                Store(ResourceType.Stone, level.stoneCost / 2, house.Center);
                Store(ResourceType.Food, level.foodCost / 2, house.Center);
            }
        }

        /// <summary>
        /// Empties a house that's going (torn down or broken): whoever is inside comes out of the
        /// door, plans inside are cancelled, and what's built inside goes (half its cost back if
        /// <paramref name="refund"/>; what chests held is left in a bag by the door either way).
        /// Returns how many things went.
        /// </summary>
        int ClearInside(House house, bool refund)
        {
            if (house.Upgrade != null) Cancel(house.Upgrade);
            return ClearInside(house.Interior, house.Outside, refund);
        }

        /// <summary>Empties a house inside or a mine (see the house version): people out, plans cancelled, what's built gone.</summary>
        public int ClearInside(Place inside, Vector2 outside, bool refund)
        {
            if (inside == null) return 0;
            var bounds = inside.Bounds;
            foreach (var v in villagers)
                if (v != null && bounds.Contains(NavGrid.CellAt(v.Position))) v.PutOutside(outside);
            foreach (var b in blueprints.ToArray())
                if (b != null && bounds.Contains(b.Cells.position)) Cancel(b);
            var things = new List<IBuilt>();
            foreach (var f in furniture) if (f != null && bounds.Contains(f.Cells.position)) things.Add(f);
            foreach (var s in storages) if (s != null && bounds.Contains(s.Cells.position)) things.Add(s);
            foreach (var layer in new[] { floors, rugs })
                foreach (var pair in layer)
                    if (bounds.Contains(pair.Key) && pair.Value is IBuilt built && !(pair.Value is Blueprint) && !things.Contains(built)) things.Add(built);
            var spilled = new int[Inventory.TypeCount];
            foreach (var thing in things)
            {
                if (thing is Storage chest && chest.Stock != null)
                    for (int i = 0; i < spilled.Length; i++) spilled[i] += chest.Stock.Get((ResourceType)i);
                Forget(thing);
                if (refund) Refund(thing.Config, 1, outside);
                thing.Remove();
            }
            LootBag.Drop(outside, spilled);
            return things.Count;
        }

        /// <summary>
        /// Starts upgrading a house to its next level: builders bring what it costs and work on it.
        /// Returns a message for the player (why not, or that it's started).
        /// </summary>
        public string StartUpgrade(House house) => StartUpgrade(house, false);

        // `restoring`: a saved game had it started, so the age it needs doesn't matter.
        string StartUpgrade(House house, bool restoring)
        {
            if (house == null) return null;
            if (house.Upgrade != null) return $"The {house.Name} is already being upgraded";
            var next = house.Next;
            if (next == null) return $"The {house.Name} is as good as it gets";
            string wait = restoring ? null : UpgradeLockedReason(house);
            if (wait != null) return wait;
            var job = UpgradeJob(house.Config, house.Level + 1);
            var go = new GameObject($"Upgrade: {house.Name}");
            go.transform.SetParent(transform, false);
            var blueprint = go.AddComponent<Blueprint>();
            blueprint.Setup(job, house.Cells, house.Rotation, HouseLook(house.Config, house.Level + 1, house.Rotation), White);
            blueprint.Upgrades = house;
            house.Upgrade = blueprint;
            blueprints.Add(blueprint);
            return $"Builders will upgrade the {house.Name} to a {next.displayName} ({job.CostText()})";
        }

        /// <summary>Why a house can't be upgraded yet (its next level is from an age the kingdom hasn't reached), or null.</summary>
        public string UpgradeLockedReason(House house)
        {
            var next = house != null ? house.Next : null;
            var research = Research.Current;
            if (next == null || research == null || research.AgeCount == 0 || research.Mode == Research.Unlocking.Everything) return null;
            if (research.Age >= next.age) return null;
            var age = research.AgeAt(next.age);
            return $"A {next.displayName} needs the {(age != null ? age.displayName : "next age")}";
        }

        // The builders' job for upgrading to `level`: its cost and work, as a building.
        BuildingConfig UpgradeJob(BuildingConfig houseConfig, int level)
        {
            if (upgradeJobs.TryGetValue((houseConfig, level), out var job) && job != null) return job;
            var l = houseConfig.house.Level(level);
            job = ScriptableObject.CreateInstance<BuildingConfig>();
            job.name = $"{houseConfig.name} upgrade {level}";
            job.displayName = $"Upgrade to {l.displayName}";
            job.kind = BuildingKind.House;
            job.category = houseConfig.category;
            job.size = houseConfig.size;
            job.woodCost = l.woodCost;
            job.stoneCost = l.stoneCost;
            job.foodCost = l.foodCost;
            job.buildSeconds = l.buildSeconds;
            job.health = 0f;
            upgradeJobs[(houseConfig, level)] = job;
            return job;
        }

        /// <summary>Takes off the whole roof over <paramref name="cell"/> (every touching cell of it) and gives half its cost back.</summary>
        public string TearDownRoof(Vector2Int cell)
        {
            var first = RoofAt(cell);
            if (first == null) return null;
            var all = ConnectedRoof(first);
            foreach (var tile in all)
            {
                Forget(tile);
                tile.Remove();
            }
            Refund(first.Config, all.Count, cell + new Vector2(0.5f, 0.5f));
            return $"Took the {first.Config.displayName.ToLowerInvariant()} off ({all.Count} tiles)";
        }

        /// <summary>Every roof cell touching <paramref name="first"/> of the same kind (one roof).</summary>
        public List<RoofTile> ConnectedRoof(RoofTile first)
        {
            var result = new List<RoofTile> { first };
            var seen = new HashSet<Vector2Int> { first.Cell };
            for (int i = 0; i < result.Count; i++)
            {
                var c = result[i].Cell;
                foreach (var n in new[] { c + Vector2Int.left, c + Vector2Int.right, c + Vector2Int.down, c + Vector2Int.up })
                {
                    if (!seen.Add(n)) continue;
                    var other = RoofAt(n);
                    if (other != null && other.Config == first.Config) result.Add(other);
                }
            }
            return result;
        }

        // Roofs that nothing holds up any more (their walls caved in or were torn down) fall in.
        void CollapseRoofs()
        {
            if (supportLost.Count == 0) return;
            int reach = Mathf.Max(1, config.roofReach);
            var falling = new List<RoofTile>();
            foreach (var lost in supportLost)
                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        var roof = RoofAt(new Vector2Int(lost.x + dx, lost.y + dy));
                        if (roof != null && !falling.Contains(roof) && !RoofHeld(roof.Cell, false)) falling.Add(roof);
                    }
            supportLost.Clear();
            foreach (var roof in falling)
            {
                if (world != null) world.Burst(roof.Cells.center, new Color32(110, 96, 74, 255), 6, 2f);
                Forget(roof);
                roof.Remove();
            }
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

        /// <summary>The cell a ladder or stairs on <paramref name="cells"/> facing <paramref name="rotation"/> leans on (the wall you climb onto).</summary>
        public static Vector2Int LeansOn(RectInt cells, int rotation)
        {
            var up = Facing.Turn(Vector2.up, rotation);
            return new Vector2Int(cells.x + Mathf.RoundToInt(up.x), cells.y + Mathf.RoundToInt(up.y));
        }

        /// <summary>Which way a ladder at <paramref name="cell"/> should face to lean on a wall beside it, or <paramref name="otherwise"/>.</summary>
        public int LadderFacing(Vector2Int cell, int otherwise)
        {
            var one = new RectInt(cell, Vector2Int.one);
            if (WallLike(LeansOn(one, otherwise))) return otherwise;
            for (int r = 0; r < 4; r++)
                if (WallLike(LeansOn(one, r))) return r;
            return otherwise;
        }

        /// <summary>Whether the ruler can stand on top of this cell: a wall or a door (gates are part of the wall walk).</summary>
        public bool WallTopAt(Vector2Int cell)
        {
            if (world != null && world.ChunkAt(cell) != null) return true;
            return occupied.TryGetValue(cell, out var thing) && thing is Door;
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

        /// <summary>A mine went off: it's gone, and builders set a new one in the same place (it costs again).</summary>
        public void Rebuild(Furniture spent)
        {
            if (spent == null) return;
            var config = spent.Config;
            var cell = spent.Cells.position;
            int rotation = spent.Rotation;
            Forget(spent);
            spent.Remove();
            PlaceBlueprint(config, cell, rotation);
        }

        /// <summary>A door or piece of furniture was smashed (zombies, explosions): it's gone, with a burst of splinters.</summary>
        public void Broke(IBuilt thing)
        {
            if (thing == null) return;
            if (thing is MineShaft caved)
            {
                ClearInside(caved.Level, caved.Outside, false);
                if (world != null) world.Burst(caved.Center, new Color32(110, 92, 70, 255), 40, 4f);
                Forget(caved);
                caved.Remove();
                return;
            }
            if (thing is House house)
            {
                ClearInside(house, false);
                if (world != null) world.Burst(house.Center, new Color32(110, 92, 70, 255), 40, 4f);
                Forget(house);
                house.Remove();
                return;
            }
            // What was inside a broken storehouse or chest spills out in a bag.
            if (thing is Storage storage && storage.Stock != null)
            {
                var stock = storage.Stock;
                LootBag.DropAll(storage.Center, stock);
            }
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
                NavFor(thing.Cells).Unblock(thing.Cells);
            }
            else if (thing is MineShaft shaft)
            {
                mines.Remove(shaft);
                RemovePlace(shaft.Level);
                usedMineSlots.Remove(shaft.Slot);
                Nav.Unblock(thing.Cells);
            }
            else if (thing is House h)
            {
                houses.Remove(h);
                RemovePlace(h.Interior);
                usedSlots.Remove(h.Slot);
                Nav.Unblock(thing.Cells);
            }
            else if (thing is Door d)
            {
                doors.Remove(d);
                foreach (var c in d.Cells.allPositionsWithin) supportLost.Add(c);
            }
            else if (thing is RoofTile roof)
            {
                if (roofView != null) roofView.Touch(roof.Cell);
            }
            else if (thing is Furniture f)
            {
                furniture.Remove(f);
                if (f.Config.blocksMovement && !f.Config.flat) NavFor(thing.Cells).Unblock(thing.Cells);
                if (f.Config.sleeps > 0) Beds = Mathf.Max(0, Beds - f.Config.sleeps);
            }
            roomsDirty = true;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------- jobs --

        /// <summary>How many villagers have this job.</summary>
        public int CountOf(Job job)
        {
            int n = 0;
            foreach (var v in villagers) if (v != null && v.Job == job) n++;
            return n;
        }

        /// <summary>
        /// Moves one villager from job <paramref name="from"/> to <paramref name="to"/> (the villagers panel's
        /// + and -). Picks one that isn't busy if it can. False if nobody has job <paramref name="from"/>.
        /// </summary>
        public bool ChangeJob(Job from, Job to)
        {
            Villager pick = null;
            foreach (var v in villagers)
            {
                if (v == null || v.Job != from) continue;
                if (pick == null || (v.IsIdle && !pick.IsIdle)) pick = v;
            }
            if (pick == null) return false;
            pick.Job = to;
            return true;
        }

        /// <summary>Why guards are standing about, for the villagers panel (null if every guard has a tower).</summary>
        public string GuardProblem()
        {
            int towers = 0;
            foreach (var d in Defenses.Defense.All) if (d != null && d.NeedsGuard) towers++;
            if (towers == 0) return "no towers to stand on: build an archer tower, ballista or cannon tower (Towers tab)";
            int guards = CountOf(Job.Guard);
            return guards > towers ? $"{guards - towers} more guards than towers" : null;
        }

        /// <summary>Why gatherers of this kind are standing about, for the villagers panel (null if they're fine).</summary>
        public string GatherProblem(ResourceType type)
        {
            if (NearestWithSpace(Throne != null ? Throne.Center : Vector2.zero) == null) return "storage is full";
            foreach (var n in ResourceNode.All)
                if (n != null && !n.Empty && n.Config != null && n.Config.resource == type && Nav.Cells.Contains(n.Cell)) return null;
            if (nature != null && Throne != null && nature.FindNearest(Throne.Center, type, SearchRadius, c => !Nav.Cells.Contains(c), out _)) return null;
            return type == ResourceType.Wood ? "no trees left: plant some (Farming tab)" : type == ResourceType.Food ? "no berries left: plant bushes (Farming tab)" : "no rocks left";
        }

        // Trees, rocks and bushes a gatherer couldn't find a way to (by cell: their objects come and go), and until when.
        readonly Dictionary<Vector2Int, float> unreachableNodes = new Dictionary<Vector2Int, float>();
        /// <summary>How far gatherers look for trees, rocks and bushes, in tiles.</summary>
        const int SearchRadius = 128;
        // Towers a guard couldn't find a way to, and until when to leave them be.
        readonly Dictionary<Defenses.Defense, float> unreachableTowers = new Dictionary<Defenses.Defense, float>();

        /// <summary>Marks rock in a mine to be dug out. False if it can't be (with why in <paramref name="reason"/>).</summary>
        public bool MarkDig(Vector2Int cell, out string reason)
        {
            if (PlaceAt(cell) is MineLevel mine)
            {
                if (!mine.CanMark(cell, out reason)) return false;
                mine.Mark(cell);
                return true;
            }
            reason = "Dig inside a mine";
            return false;
        }

        // Miners: dig out marked rock in the mines first (carrying the stone or ore to storage when their hands are full).
        bool Dig(Villager v)
        {
            int full = villagerConfig.carryAmount;
            if (v.CarryAmount >= full)
            {
                var home = NearestWithSpace(v.Position);
                if (home != null) v.StartReturn(home);
                return home != null;
            }
            MineLevel best = null;
            Vector2Int bestCell = default;
            float bestDistance = float.MaxValue;
            foreach (var m in mines)
            {
                if (m == null || m.Level == null || m.Level.Marked <= 0) continue;
                var level = m.Level;
                var from = PlaceAt(v.Position) == level ? v.Position : level.Entrance;
                if (!level.NextToDig(from, v, out var cell)) continue;
                float d = Travel(v.Position, (Vector2)cell + new Vector2(0.5f, 0.5f));
                if (d < bestDistance) { bestDistance = d; best = level; bestCell = cell; }
            }
            if (best == null) return false;
            // Carrying something else (or a full load soon): take it to storage first.
            var ore = best.OreAt(bestCell);
            var comes = ore == Ore.Copper ? ResourceType.Copper : ore == Ore.Iron ? ResourceType.Iron : ore == Ore.Gold ? ResourceType.Gold : ResourceType.Stone;
            int amount = ore == Ore.None ? best.Config.stonePerTile : best.Config.orePerTile;
            if (v.CarryAmount > 0 && (v.CarryType != comes || v.CarryAmount + amount > full))
            {
                // Nowhere to put it: don't dig (what's in hand would be lost).
                var home = NearestWithSpace(v.Position);
                if (home != null) v.StartReturn(home);
                return home != null;
            }
            if (v.StartDig(best, bestCell)) return true;
            best.CantReach(bestCell);
            return false;
        }

        // Woodcutters, miners and foragers: gather until their hands are full, then take it to storage.
        void Gather(Villager v, ResourceType type)
        {
            int full = villagerConfig.carryAmount;
            if (v.CarryAmount > 0 && (v.CarryType != type || v.CarryAmount >= full))
            {
                var home = NearestWithSpace(v.Position);
                if (home != null) v.StartReturn(home);
                return;
            }
            float now = Time.time;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                var node = NearestNode(type, v.Position, now);
                if (node == null) break;
                if (v.StartGather(node)) return;
                unreachableNodes[node.Cell] = now + 15f;   // no way there (deep in a forest, walled in): try others for a while
            }
            if (v.CarryAmount > 0)
            {
                var home = NearestWithSpace(v.Position);
                if (home != null) v.StartReturn(home);
            }
        }

        // The closest tree, rock or bush of this kind that nobody else is at; planted ones count as a little closer.
        // Then, if none of those are near, the nearest one on the map (its square of the map is made real for it).
        ResourceNode NearestNode(ResourceType type, Vector2 from, float now)
        {
            if (unreachableNodes.Count > 256)
            {
                // Forget the ones whose time is up.
                var old = new List<Vector2Int>();
                foreach (var pair in unreachableNodes) if (pair.Value <= now) old.Add(pair.Key);
                foreach (var c in old) unreachableNodes.Remove(c);
            }
            ResourceNode best = null;
            float bestDistance = float.MaxValue;
            foreach (var n in ResourceNode.All)
            {
                if (n == null || n.Empty || n.Config == null || n.Config.resource != type || n.Gatherer != null) continue;
                if (unreachableNodes.TryGetValue(n.Cell, out float until) && until > now) continue;
                if (!Nav.Cells.Contains(n.Cell)) continue;   // past the kingdom's lands (near the ruler out exploring)
                float d = Travel(from, n.transform.position) - (n.Config.replant ? 6f : 0f);
                if (d < bestDistance) { best = n; bestDistance = d; }
            }
            if (best != null && bestDistance < 32f) return best;
            if (nature == null || Streamer == null) return best;
            bool Skip(Vector2Int c)
            {
                if (!Nav.Cells.Contains(c)) return true;
                if (unreachableNodes.TryGetValue(c, out float t) && t > now) return true;
                var live = nature.Live(c);
                return live != null && live.Gatherer != null;
            }
            if (!nature.FindNearest(from, type, SearchRadius, Skip, out var cell)) return best;
            if (best != null && Travel(from, (Vector2)cell + new Vector2(0.5f, 0.5f)) >= bestDistance) return best;
            var far = Streamer.ShowAt(cell);
            return far != null && !far.Empty ? far : best;
        }

        /// <summary>The ruler's weapons and the order book crafters work on (null if there's no armory).</summary>
        public Combat.Armory Armory { get; set; }

        /// <summary>The anvils (workshop furniture) crafters work at.</summary>
        Furniture NearestAnvil(Vector2 from)
        {
            Furniture best = null;
            float bestDistance = float.MaxValue;
            foreach (var f in furniture)
            {
                if (f == null || f.Config.roomTag != RoomTag.Workshop) continue;
                float d = Travel(from, f.Cells.center);
                if (d < bestDistance) { bestDistance = d; best = f; }
            }
            return best;
        }

        /// <summary>Why crafters are standing about, for the villagers panel (null if they're fine).</summary>
        public string CraftProblem()
        {
            if (NearestAnvil(Throne != null ? Throne.Center : Vector2.zero) == null) return "no anvil to work at: build one (Furniture tab)";
            if (Armory == null || !Armory.HasWork) return "nothing ordered: order weapons at an anvil (walk up, E)";
            return null;
        }

        /// <summary>Gives an idle villager something to do, if there is anything.</summary>
        public void AssignJob(Villager v)
        {
            float now = Time.time;
            if (v.Job == Job.Crafter && v.CarryAmount == 0)
            {
                if (Armory != null && Armory.HasWork)
                {
                    var anvil = NearestAnvil(v.Position);
                    if (anvil != null) v.StartCraft(anvil);
                }
                return;
            }
            if (v.Job == Job.Miner && Dig(v)) return;
            if (Jobs.Gathers(v.Job, out var gathers))
            {
                Gather(v, gathers);
                return;
            }
            if (v.Job == Job.Guard && v.CarryAmount == 0)
            {
                // Up onto the nearest tower that has nobody on it.
                Defenses.Defense post = null;
                float best = float.MaxValue;
                foreach (var d in Defenses.Defense.All)
                {
                    if (d == null || !d.NeedsGuard || (d.Guard != null && d.Guard != v)) continue;
                    if (unreachableTowers.TryGetValue(d, out float until) && until > now) continue;
                    float distance = Travel(v.Position, d.Center);
                    if (distance < best) { best = distance; post = d; }
                }
                if (post != null && !v.StartGuard(post)) unreachableTowers[post] = now + 15f;
                return;
            }
            if (v.Job != Job.Builder)
            {
                // No work: just put back anything in hand.
                if (v.CarryAmount > 0)
                {
                    var store = NearestWithSpace(v.Position);
                    if (store != null) v.StartReturn(store);
                }
                return;
            }

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
            CollapseRoofs();
            UpdateRooms(false);
        }

        static bool BlocksZombies(BuildingConfig b) => NeedsClearSpot(b) || b.kind == BuildingKind.Door;

        // Walls, storehouses and things you can't walk through can't appear on top of someone.
        static bool NeedsClearSpot(BuildingConfig b) =>
            b.kind == BuildingKind.Wall || b.kind == BuildingKind.Storage || b.kind == BuildingKind.House || b.kind == BuildingKind.Plant
            || b.kind == BuildingKind.Mine
            || (b.kind == BuildingKind.Furniture && b.blocksMovement && !b.flat);

        void TryComplete(Blueprint b)
        {
            if (b.Upgrades != null)
            {
                // An upgrade: the house gets its next level (a bigger room inside, the new look).
                var house = b.Upgrades;
                blueprints.Remove(b);
                house.Upgrade = null;
                house.SetLevel(house.Level + 1);
                Destroy(b.gameObject);
                roomsDirty = true;
                Changed?.Invoke();
                return;
            }
            if (b.Config.kind == BuildingKind.Roof)
            {
                foreach (var c in b.Cells.allPositionsWithin)
                    if (!RoofHeld(c, false))
                    {
                        b.Problem = $"Finished, but a roof needs a wall or door within {config.roofReach} tiles to hold it up";
                        return;
                    }
            }
            // Nothing zombies can't walk through may appear on top of them (doors included).
            if (BlocksZombies(b.Config))
            {
                var area = new Rect(b.Cells.position, b.Cells.size);
                foreach (var h in Health.Creatures)
                    if (h != null && h.Team == Team.Zombie && Overlaps(area, h.Position, h.Radius))
                    {
                        b.Problem = "Finished, but zombies are in the way";
                        return;
                    }
            }
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

                if (blocked)
                {
                    b.Problem = "Finished: waiting for a villager to step off";
                    return;
                }
            }

            if (b.Config.kind == BuildingKind.House && !Nav.Walkable(House.FrontCell(b.Cells, b.Rotation)))
            {
                b.Problem = "Finished, but something blocks the way to its door";
                return;
            }
            blueprints.Remove(b);
            Free(LayerOf(b.Config), b.Cells, b);
            if (b.Config.kind != BuildingKind.Roof) ClearNature(b.Cells);   // a roof goes over rocks and bushes
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
                case BuildingKind.Mine:
                {
                    var go = new GameObject(config.displayName);
                    go.transform.SetParent(transform, false);
                    var shaft = go.AddComponent<MineShaft>();
                    int slot = restoreSlot >= 0 && !usedMineSlots.Contains(restoreSlot) ? restoreSlot : 0;
                    while (usedMineSlots.Contains(slot)) slot++;
                    usedMineSlots.Add(slot);
                    var origin = MineOrigin + new Vector2Int(slot % 32, slot / 32) * MineSlot;
                    mines.Add(shaft);
                    shaft.Setup(config, cells, LookOf(config), ShadowOf(config, 0), slot, origin, this);
                    AddPlace(shaft.Level);
                    Occupy(occupied, cells, shaft);
                    Nav.Block(cells);
                    made = shaft;
                    break;
                }
                case BuildingKind.Plant:
                    if (config.plant == null)
                    {
                        Debug.LogWarning($"{config.name} has nothing to plant (its Plant is empty).", config);
                        break;
                    }
                    foreach (var c in cells.allPositionsWithin) made = Plant(config.plant, c, config.growSeconds);
                    break;
                case BuildingKind.House:
                {
                    var go = new GameObject(config.displayName);
                    go.transform.SetParent(transform, false);
                    var house = go.AddComponent<House>();
                    var origin = TakeSlot(out int slot);
                    // On the books before its room is made, so the room finds its own floor.
                    houses.Add(house);
                    house.Setup(config, cells, rotation, slot, origin, this);
                    AddPlace(house.Interior);
                    Occupy(occupied, cells, house);
                    Nav.Block(cells);
                    made = house;
                    break;
                }
                case BuildingKind.Roof:
                    foreach (var c in cells.allPositionsWithin)
                    {
                        var go = new GameObject(config.displayName);
                        go.transform.SetParent(transform, false);
                        var tile = go.AddComponent<RoofTile>();
                        tile.Setup(config, c);
                        Occupy(roofs, new RectInt(c, Vector2Int.one), tile);
                        if (roofView != null) roofView.Touch(c);
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
                    if (config.blocksMovement && !config.flat) NavFor(cells).Block(cells);
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

        /// <summary>A young tree or bush of <paramref name="kind"/> at <paramref name="cell"/>, fully grown in <paramref name="growSeconds"/>.</summary>
        /// <summary>(Its object, if that part of the map is shown: the kingdom's area always is.)</summary>
        public ResourceNode Plant(ResourceNodeConfig kind, Vector2Int cell, float growSeconds)
        {
            if (nature == null || kind == null) return null;
            int variant = (int)(DamageShape.Hash(0x7Au, cell.x, cell.y) % (uint)NatureArt.Variants);
            return nature.Plant(cell, kind, variant, growSeconds);
        }

        /// <summary>
        /// Where this kingdom's villagers work: round the throne and each building (not one box round all of
        /// them: a mine far off would make everything in between real). The trees and rocks there are
        /// always real objects. Adds the areas to <paramref name="into"/>.
        /// </summary>
        public void ActiveAreas(List<RectInt> into)
        {
            const int margin = 16;
            if (Throne == null) return;
            if (Time.time >= areasAt)
            {
                areasAt = Time.time + 3f;
                areas.Clear();
                void Add(RectInt r)
                {
                    if (Nav == null || !Nav.Cells.Contains(r.position)) return;   // insides and mines are off the map
                    areas.Add(new RectInt(r.x - margin, r.y - margin, r.width + margin * 2, r.height + margin * 2));
                }
                var t = Throne.Cells;
                areas.Add(new RectInt(t.x - margin * 2, t.y - margin * 2, t.width + margin * 4, t.height + margin * 4));
                foreach (var s in storages) if (s != null) Add(s.Cells);
                foreach (var h in houses) if (h != null) Add(h.Cells);
                foreach (var m in mines) if (m != null) Add(m.Cells);
                foreach (var f in furniture) if (f != null) Add(f.Cells);
                foreach (var b in blueprints) if (b != null) Add(b.Cells);
            }
            into.AddRange(areas);
        }

        readonly List<RectInt> areas = new List<RectInt>();
        float areasAt = -999f;

        /// <summary>Makes trees and rocks far from anyone real when a villager heads for one.</summary>
        public World.NatureStreamer Streamer { get; set; }

        // ------------------------------------------------------------ saving --

        /// <summary>
        /// Everything finished that the kingdom built (not the throne), for saving: houses and mines
        /// first, since what's built inside them needs them there when a save is loaded.
        /// </summary>
        public List<IBuilt> AllBuilt()
        {
            var all = new List<IBuilt>();
            foreach (var h in houses) if (h != null) all.Add(h);
            foreach (var m in mines) if (m != null) all.Add(m);
            foreach (var s in storages) if (s != null) all.Add(s);
            foreach (var d in doors) if (d != null) all.Add(d);
            foreach (var f in furniture) if (f != null) all.Add(f);
            var seen = new HashSet<IBuilt>(all);
            foreach (var layer in new[] { floors, roofs })
                foreach (var thing in layer.Values)
                    if (thing != null && !(thing is Blueprint) && thing is IBuilt built && seen.Add(built)) all.Add(built);
            return all;
        }

        /// <summary>Puts up a finished building from a saved game; a house or mine gets inside square <paramref name="slot"/> again.</summary>
        public Component Restore(BuildingConfig config, RectInt cells, int rotation, int slot)
        {
            // In case the map came out a little different (its settings changed): no trees inside buildings.
            if (config != null && config.kind != BuildingKind.Roof && Nav != null && Nav.Cells.Contains(cells.position)) ClearNature(cells);
            restoreSlot = slot;
            try { return Build(config, cells, rotation); }
            finally { restoreSlot = -1; }
        }

        /// <summary>Puts back a blueprint from a saved game, without the checks for placing one.</summary>
        public Blueprint RestoreBlueprint(BuildingConfig b, RectInt cells, int rotation)
        {
            if (b == null) return null;
            rotation = b.ClampRotation(rotation);
            var go = new GameObject($"Blueprint: {b.displayName}");
            go.transform.SetParent(transform, false);
            var blueprint = go.AddComponent<Blueprint>();
            blueprint.Setup(b, cells, rotation, LookOf(b, rotation), White);
            blueprints.Add(blueprint);
            Occupy(LayerOf(b), cells, blueprint);
            return blueprint;
        }

        /// <summary>Puts back a house's upgrade from a saved game (even if the age it needs is gone).</summary>
        public Blueprint RestoreUpgrade(House house)
        {
            StartUpgrade(house, true);
            return house != null ? house.Upgrade : null;
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

            // Rooms can only be inside the walls and doors, so only the box round them (and a ring
            // of open ground) is searched, not the whole map.
            var map = WallBox();
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

            // Each house's inside is a room of its own, named after the house and what's in it.
            foreach (var house in houses)
            {
                var inside = house != null ? house.Interior : null;
                if (inside == null) continue;
                var room = new Room();
                seen.Clear();
                tags.Clear();
                foreach (var cell in inside.Inner.allPositionsWithin)
                {
                    room.Cells.Add(cell);
                    roomAt[cell] = room;
                    foreach (var layer in new[] { occupied, rugs })
                    {
                        if (!layer.TryGetValue(cell, out var thing) || thing == null || !seen.Add(thing)) continue;
                        if (thing is IBuilt built && !(thing is Blueprint) && built.Config != null)
                            tags[built.Config.roomTag] = (tags.TryGetValue(built.Config.roomTag, out int n) ? n : 0) + 1;
                    }
                }
                string what = NameOf(room.Size, false, tags);
                room.Name = what == "Empty room" || what == "Small room" ? house.Name : $"{house.Name}: {what.ToLowerInvariant()}";
                room.LabelAt = inside.Inner.center;
                rooms.Add(room);
            }
        }

        // The walls' and doors' bounding box on the map, one cell bigger all round (empty when there are none).
        RectInt WallBox()
        {
            var cells = Nav.Cells;
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            void Add(Vector2Int c)
            {
                if (!cells.Contains(c)) return;
                if (c.x < x0) x0 = c.x;
                if (c.y < y0) y0 = c.y;
                if (c.x > x1) x1 = c.x;
                if (c.y > y1) y1 = c.y;
            }
            if (world != null) foreach (var c in world.ChunkCells) Add(c);
            foreach (var d in doors) if (d != null) foreach (var c in d.Cells.allPositionsWithin) Add(c);
            if (x0 > x1) return new RectInt(cells.x, cells.y, 0, 0);
            x0 = Mathf.Max(cells.xMin, x0 - 1); y0 = Mathf.Max(cells.yMin, y0 - 1);
            x1 = Mathf.Min(cells.xMax - 1, x1 + 1); y1 = Mathf.Min(cells.yMax - 1, y1 + 1);
            return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        static string NameOf(int size, bool throne, Dictionary<RoomTag, int> tags)
        {
            int Count(RoomTag tag) => tags.TryGetValue(tag, out int n) ? n : 0;
            if (throne) return "Throne room";
            if (Count(RoomTag.Bed) >= 3) return "Barracks";
            if (Count(RoomTag.Bed) > 0 && (Count(RoomTag.Cooking) > 0 || Count(RoomTag.Table) > 0) && size <= 30) return "Cottage";
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
