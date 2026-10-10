using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// A piece of furniture: a table, bed, shelf, torch, rug and so on. Some block
    /// the way (tables, beds), some can be walked over (chairs, rugs), some give
    /// light (torches, candelabras, fireplaces). Zombies can smash it if it has
    /// hit points.
    /// </summary>
    public sealed class Furniture : MonoBehaviour, IBuilt
    {
        Kingdom kingdom;

        public BuildingConfig Config { get; private set; }
        public RectInt Cells { get; private set; }
        public int Rotation { get; private set; }
        /// <summary>Null if it can't be broken.</summary>
        public Health Health { get; private set; }

        public void Setup(BuildingConfig config, RectInt cells, int rotation, Sprite look, Sprite shadow, Sprite flame, Kingdom owner)
        {
            Config = config;
            Cells = cells;
            Rotation = rotation;
            kingdom = owner;
            transform.position = (Vector3)(Vector2)cells.center;

            if (!config.flat && shadow != null) Look.AddShadow(transform, shadow, config.height);
            var go = new GameObject("Look");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = look;
            r.sortingOrder = config.flat ? SortOrder.Rugs : SortOrder.Furniture;
            // A trap or tower: it works on zombies by itself (or with a guard).
            if (config.defense != null) gameObject.AddComponent<Defenses.Defense>().Setup(this, config.defense, r, owner);

            if (config.blocksMovement)
            {
                var body = gameObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(cells.width - 0.1f, cells.height - 0.1f);
            }

            if (config.lightSize > 0f || (config.flames != null && config.flames.Length > 0))
            {
                var offsets = new Vector2[config.flames != null ? config.flames.Length : 0];
                for (int i = 0; i < offsets.Length; i++) offsets[i] = Facing.Turn(config.flames[i], rotation);
                var flames = new GameObject("Flames").AddComponent<Flames>();
                flames.transform.SetParent(transform, false);
                flames.Setup(flame, offsets, config.flameSize, config.lightSize, SortOrder.Furniture + 1);
            }

            if (config.health > 0f)
            {
                Health = gameObject.AddComponent<Health>();
                Health.Setup(Team.Friendly, config.health, Mathf.Min(cells.width, cells.height) * 0.45f, false);
                Health.Died += _ => kingdom?.Broke(this);
            }
        }

        public void Remove() => Destroy(gameObject);
    }
}
