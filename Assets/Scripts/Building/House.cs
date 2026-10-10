using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// A house on the map: a building you go into through its door (E). Inside is a
    /// room of its own (<see cref="Interior"/>), bigger than the house looks, to put
    /// anything in. Once the kingdom reaches the next age the house can be upgraded
    /// (U): builders bring what it costs and work on it, and when they're done the
    /// room inside is bigger and the house has the new age's look. The house on the
    /// map stays the same size. Zombies can break it down, and go for it when
    /// someone is hiding inside.
    /// </summary>
    public sealed class House : MonoBehaviour, IBuilt
    {
        Kingdom kingdom;
        SpriteRenderer look, shadow;

        public BuildingConfig Config { get; private set; }
        public RectInt Cells { get; private set; }
        public int Rotation { get; private set; }
        /// <summary>Its level: 0 is the first (see <see cref="HouseConfig.levels"/>).</summary>
        public int Level { get; private set; }
        public HouseLevel Current => Config.house != null ? Config.house.Level(Level) : new HouseLevel();
        /// <summary>Its level's name, like "Hut".</summary>
        public string Name => Current.displayName;
        public Health Health { get; private set; }
        public Interior Interior { get; private set; }
        /// <summary>The builders' job of upgrading it, while there is one.</summary>
        public Blueprint Upgrade { get; internal set; }
        /// <summary>Which of the inside squares (far off the map) it has.</summary>
        public int Slot { get; private set; }
        public Vector2 Center => Cells.center;
        /// <summary>The way the door faces (out of the house).</summary>
        public Vector2 Front => Facing.Turn(Vector2.down, Rotation);
        /// <summary>The middle of the door, on the house's edge.</summary>
        public Vector2 DoorPoint => Center + Front * (Mathf.Abs(Front.x) > 0.5f ? Cells.width : Cells.height) * 0.5f;
        /// <summary>Where you stand when you've just come out.</summary>
        public Vector2 Outside => DoorPoint + Front * 0.45f;
        /// <summary>The cell in front of the door (it must stay clear).</summary>
        public Vector2Int OutsideCell => FrontCell(Cells, Rotation);
        /// <summary>The next level, or null if it's as good as it gets.</summary>
        public HouseLevel Next => Config.house != null && Level + 1 < Config.house.Count ? Config.house.Level(Level + 1) : null;

        /// <summary>The cell just outside the door of a house on <paramref name="cells"/> facing <paramref name="rotation"/>.</summary>
        public static Vector2Int FrontCell(RectInt cells, int rotation)
        {
            var front = Facing.Turn(Vector2.down, rotation);
            var point = cells.center + front * ((Mathf.Abs(front.x) > 0.5f ? cells.width : cells.height) * 0.5f + 0.5f);
            return new Vector2Int(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
        }

        public void Setup(BuildingConfig config, RectInt cells, int rotation, int slot, Vector2Int slotOrigin, Kingdom owner, int level = 0)
        {
            Config = config;
            Cells = cells;
            Rotation = rotation;
            Slot = slot;
            kingdom = owner;
            transform.position = (Vector3)(Vector2)cells.center;

            shadow = Part("Shadow", SortOrder.Shadows);
            look = Part("Look", SortOrder.Buildings);
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(cells.width - 0.05f, cells.height - 0.05f);

            Health = gameObject.AddComponent<Health>();
            Health.Setup(Team.Friendly, 1f, Mathf.Min(cells.width, cells.height) * 0.45f, false);
            Health.Died += _ => kingdom?.Broke(this);

            // The inside lives far off the map, not under the house.
            Interior = new GameObject($"{config.displayName} inside").AddComponent<Interior>();
            Interior.transform.SetParent(owner != null ? owner.transform : null, false);
            Interior.Setup(this, slotOrigin, owner);
            SetLevel(level);
        }

        SpriteRenderer Part(string partName, int order)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order;
            return r;
        }

        /// <summary>Makes it level <paramref name="level"/>: its look, its strength (full again) and the room inside.</summary>
        public void SetLevel(int level)
        {
            Level = Config.house != null ? Mathf.Clamp(level, 0, Mathf.Max(0, Config.house.Count - 1)) : 0;
            var current = Current;
            if (kingdom != null)
            {
                look.sprite = kingdom.HouseLook(Config, Level, Rotation);
                shadow.sprite = kingdom.HouseShadow(Config, Rotation);
            }
            shadow.transform.localPosition = (Vector3)(Look.ShadowDirection * (Art.RoofArt.Height(current.roof) + 0.2f));
            shadow.color = new Color(1f, 1f, 1f, Look.ShadowOpacity);
            Health.SetMax(current.health);
            name = current.displayName;
            Interior.Build(current, current.inside);
            ShowBeltHoles();
        }

        /// <summary>
        /// The conveyor holes: on each wall an input (red: things go in) just left of / below the middle and an
        /// output (blue: things come out) just right of / above it. Each joins the tile just outside the wall
        /// with the edge tile just inside it.
        /// </summary>
        public void BeltHoles(List<(Vector2Int Outside, Vector2Int Inside, bool In)> into)
        {
            into.Clear();
            if (Interior == null) return;
            RectInt o = Cells, i = Interior.Inner;
            for (int k = 0; k < 2; k++)
            {
                int d = k == 0 ? -1 : 1;
                bool input = k == 0;
                into.Add((new Vector2Int(o.xMax, o.y + o.height / 2 + d), new Vector2Int(i.xMax - 1, i.y + i.height / 2 + d), input));
                into.Add((new Vector2Int(o.x - 1, o.y + o.height / 2 + d), new Vector2Int(i.x, i.y + i.height / 2 + d), input));
                into.Add((new Vector2Int(o.x + o.width / 2 + d, o.yMax), new Vector2Int(i.x + i.width / 2 + d, i.yMax - 1), input));
                into.Add((new Vector2Int(o.x + o.width / 2 + d, o.y - 1), new Vector2Int(i.x + i.width / 2 + d, i.y), input));
            }
        }

        static Sprite dot;
        readonly List<GameObject> holeMarks = new List<GameObject>();
        readonly List<(Vector2Int Outside, Vector2Int Inside, bool In)> holes = new List<(Vector2Int, Vector2Int, bool)>();

        // Small red and blue squares on the holes' tiles, outside and inside.
        void ShowBeltHoles()
        {
            foreach (var g in holeMarks) if (g != null) Destroy(g);
            holeMarks.Clear();
            if (dot == null) dot = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            BeltHoles(holes);
            foreach (var h in holes)
                foreach (var cell in new[] { h.Outside, h.Inside })
                {
                    var go = new GameObject(h.In ? "Belt hole in" : "Belt hole out");
                    go.transform.SetParent(cell == h.Inside ? Interior.transform : transform, true);
                    go.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
                    go.transform.localScale = Vector3.one * 0.3f;
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sprite = dot;
                    r.color = h.In ? new Color(0.9f, 0.15f, 0.12f, 0.85f) : new Color(0.15f, 0.4f, 0.95f, 0.85f);
                    r.sortingOrder = SortOrder.Rugs + 1;
                    holeMarks.Add(go);
                }
        }

        public void Remove()
        {
            if (Interior != null) Destroy(Interior.gameObject);
            Destroy(gameObject);
        }
    }
}
