using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// A mine's entrance on the map: a timber headframe over a shaft. E at it climbs
    /// down into its <see cref="MineLevel"/>. Zombies break it down to get at whoever
    /// is hiding below; if it breaks, everyone comes up and the mine is lost.
    /// </summary>
    public sealed class MineShaft : MonoBehaviour, IBuilt
    {
        Kingdom kingdom;

        public BuildingConfig Config { get; private set; }
        public RectInt Cells { get; private set; }
        public int Rotation => 0;
        public Health Health { get; private set; }
        public MineLevel Level { get; private set; }
        public int Slot { get; private set; }
        public Vector2 Center => Cells.center;
        /// <summary>Where E works: the middle of its bottom edge.</summary>
        public Vector2 DoorPoint => new Vector2(Cells.center.x, Cells.yMin);
        public Vector2 Outside => DoorPoint + Vector2.down * 0.45f;
        public Vector2Int OutsideCell => FrontCell(Cells);

        /// <summary>The cell in front of a mine's entrance on <paramref name="cells"/> (it must stay clear).</summary>
        public static Vector2Int FrontCell(RectInt cells) => new Vector2Int(cells.x + cells.width / 2, cells.yMin - 1);

        public void Setup(BuildingConfig config, RectInt cells, Sprite look, Sprite shadow, int slot, Vector2Int levelOrigin, Kingdom owner)
        {
            Config = config;
            Cells = cells;
            Slot = slot;
            kingdom = owner;
            transform.position = (Vector3)(Vector2)cells.center;
            if (shadow != null) Look.AddShadow(transform, shadow, config.height);
            var go = new GameObject("Look");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = look;
            r.sortingOrder = SortOrder.Buildings;
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(cells.width - 0.05f, cells.height - 0.05f);
            Health = gameObject.AddComponent<Health>();
            Health.Setup(Team.Friendly, Mathf.Max(1f, config.health), Mathf.Min(cells.width, cells.height) * 0.45f, false);
            Health.Died += _ => kingdom?.Broke(this);

            // The underground lives far off the map.
            Level = new GameObject("Mine underground").AddComponent<MineLevel>();
            Level.transform.SetParent(owner != null ? owner.transform : null, false);
            Level.Setup(this, config.mine, levelOrigin, owner);
        }

        public void Remove()
        {
            if (Level != null) Destroy(Level.gameObject);
            Destroy(gameObject);
        }
    }
}
