using KingdomGame.Art;
using KingdomGame.Villagers;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// The inside of a house: a room of its own, far off the map, that you go into
    /// through the house's door. It's bigger than the house looks from outside, and
    /// grows when the house is upgraded (the house on the map stays the same size).
    /// The door is in the middle of the bottom wall and never moves, so the room
    /// grows left, right and up, and everything inside stays where it was.
    /// Furniture inside is built like anywhere else; villagers walk in through the door.
    /// </summary>
    public sealed class Interior : Place
    {
        /// <summary>Each house's inside gets a square of this many tiles (the biggest room inside must fit).</summary>
        public const int SlotSize = 40;

        Kingdom kingdom;
        Vector2Int origin;
        GameObject built;   // the floor, walls and door; made again when the room grows

        public House House { get; private set; }
        /// <summary>The floor: where things can be built.</summary>
        public RectInt Inner { get; private set; }
        /// <summary>The floor and the walls round it.</summary>
        public override RectInt Bounds => new RectInt(Inner.x - 1, Inner.y - 1, Inner.width + 2, Inner.height + 2);
        public override string Name => House != null ? House.Name : "House";
        public override Vector2Int ExitCell => DoorCell;
        public override Vector2 Outside => House.Outside;
        public override Vector2Int OutsideCell => House.OutsideCell;
        public override Vector2 DoorPoint => House.DoorPoint;
        public override IBuilt Building => House;
        public override Health BuildingHealth => House != null ? House.Health : null;
        public override bool CanBuildAt(Vector2Int cell) => Inner.Contains(cell);
        /// <summary>The door, in the middle of the bottom wall.</summary>
        public Vector2Int DoorCell => new Vector2Int(origin.x + SlotSize / 2, origin.y + 1);
        /// <summary>The cell just inside the door (kept clear so you can always get in and out).</summary>
        public override Vector2Int EntranceCell => DoorCell + Vector2Int.up;
        /// <summary>Where the walking paths inside are found (made again when the room grows).</summary>
        public override NavGrid Nav => nav;
        NavGrid nav;
        public override Vector2 Center => Inner.center;

        public void Setup(House house, Vector2Int slotOrigin, Kingdom owner)
        {
            House = house;
            origin = slotOrigin;
            kingdom = owner;
        }

        /// <summary>Makes the room <paramref name="size"/> tiles inside, looking like <paramref name="level"/>.</summary>
        public void Build(HouseLevel level, Vector2Int size)
        {
            int w = Mathf.Clamp(size.x | 1, 3, SlotSize - 4);   // odd, so the door is in the middle
            int h = Mathf.Clamp(size.y, 2, SlotSize - 4);
            Inner = new RectInt(DoorCell.x - w / 2, DoorCell.y + 1, w, h);

            if (built != null) Destroy(built);
            built = new GameObject("Inside");
            built.transform.SetParent(transform, false);

            // The floor.
            for (int y = Inner.yMin; y < Inner.yMax; y++)
                for (int x = Inner.xMin; x < Inner.xMax; x++)
                {
                    uint pick = Destruction.DamageShape.Hash(0xF1u, x, y) % (uint)ArtCatalog.FloorVariants;
                    var tile = ArtSprites.Get(ArtKeys.FloorTile(level.floorArt, (int)pick)) ?? ArtSprites.Get(ArtKeys.FloorTile("Floor/Wood", (int)pick));
                    Part("Floor", tile, new Vector2(x + 0.5f, y + 0.5f), SortOrder.Floors - 10);
                }

            // The walls round it (just pictures and colliders: nothing breaks in here), and the door.
            var b = Bounds;
            foreach (var cell in b.allPositionsWithin)
            {
                if (Inner.Contains(cell)) continue;
                if (cell == DoorCell)
                {
                    var door = ArtSprites.Get(ArtKeys.DoorClosed(level.doorArt, 0)) ?? ArtSprites.Get(ArtKeys.DoorClosed("Door/Wood", 0));
                    Part("Door", door, new Vector2(cell.x + 0.5f, cell.y + 0.5f), SortOrder.Doors);
                    continue;
                }
                var wall = kingdom != null ? kingdom.WallSprite(level.walls, cell) : null;
                if (wall != null) Part("Wall", wall, cell, SortOrder.Walls);
            }
            var solid = new GameObject("Walls");
            solid.transform.SetParent(built.transform, false);
            solid.transform.position = Vector3.zero;   // the colliders below are placed in world tiles
            var body = solid.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            Box(solid, new Rect(b.xMin, b.yMin, 1, b.height));                       // left
            Box(solid, new Rect(b.xMax - 1, b.yMin, 1, b.height));                   // right
            Box(solid, new Rect(b.xMin, b.yMax - 1, b.width, 1));                    // top
            Box(solid, new Rect(b.xMin, b.yMin, b.width, 1));                        // bottom, door and all (go out with E)

            // Walking paths inside, with what's already built here in the way again.
            nav?.Dispose();
            nav = new NavGrid(b, null);
            foreach (var cell in b.allPositionsWithin)
                if (!Inner.Contains(cell)) Nav.Block(new RectInt(cell, Vector2Int.one));
            if (kingdom != null) kingdom.BlockInside(this);
        }

        void Part(string partName, Sprite sprite, Vector2 at, int order)
        {
            if (sprite == null) return;
            var go = new GameObject(partName);
            go.transform.SetParent(built.transform, false);
            go.transform.position = at;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
        }

        static void Box(GameObject on, Rect r)
        {
            var box = on.AddComponent<BoxCollider2D>();
            box.offset = r.center;
            box.size = r.size;
        }

        void OnDestroy() => nav?.Dispose();
    }
}
