using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// A door in a wall. It swings open when the ruler or a villager walks up to
    /// it and shuts once they've gone and the doorway is clear (so you can stand
    /// beside it, behind the wall, without letting zombies in). It never opens for
    /// zombies: they have to break it down (it has hit points).
    /// </summary>
    public sealed class Door : MonoBehaviour, IBuilt
    {
        const float OpenDistance = 0.85f;   // a friend this near (tiles from the middle of a one-tile door, so 0.35 from its edge) opens it

        Sprite closedLook, openLook;
        SpriteRenderer look, shadow;
        BoxCollider2D block;
        Kingdom kingdom;

        public BuildingConfig Config { get; private set; }
        public RectInt Cells { get; private set; }
        public int Rotation { get; private set; }
        public Health Health { get; private set; }
        public bool IsOpen { get; private set; }
        public Vector2 Center => Cells.center;

        public void Setup(BuildingConfig config, RectInt cells, int rotation, Sprite closed, Sprite open, Sprite closedShadow, Kingdom owner)
        {
            Config = config;
            Cells = cells;
            Rotation = rotation;
            closedLook = closed;
            openLook = open != null ? open : closed;
            kingdom = owner;
            transform.position = (Vector3)(Vector2)cells.center;

            if (closedShadow != null) shadow = Look.AddShadow(transform, closedShadow, config.height);
            var go = new GameObject("Look");
            go.transform.SetParent(transform, false);
            look = go.AddComponent<SpriteRenderer>();
            look.sprite = closedLook;
            look.sortingOrder = SortOrder.Doors;

            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            block = gameObject.AddComponent<BoxCollider2D>();
            block.size = new Vector2(cells.width, cells.height);

            Health = gameObject.AddComponent<Health>();
            Health.Setup(Team.Friendly, Mathf.Max(1f, config.health), 0.5f, false);
            Health.Died += _ => kingdom?.Broke(this);
        }

        /// <summary>A lever gate: opens it if it's shut, shuts it if it's open. Returns whether it's open now.</summary>
        public bool Toggle()
        {
            SetOpen(!IsOpen);
            return IsOpen;
        }

        void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            look.sprite = open ? openLook : closedLook;
            block.enabled = !open;
            if (shadow != null) shadow.enabled = !open;
        }

        void Update()
        {
            if (Config.leverGate) return;   // (only its lever opens and shuts it)
            // Open while a friend is near; shut once they've gone and nothing stands in the doorway.
            bool friendNear = false, doorwayBusy = false;
            // (Measured from the door's edge, so a wide gate opens for someone at either end of it.)
            foreach (var h in Health.Creatures)
            {
                if (h == null || h.Dead || h.OutOfReach) continue;
                float d = BuildingLook.Distance(Cells, h.Position);
                if (h.Team == Team.Friendly && d < OpenDistance - 0.5f) friendNear = true;
                if (d < h.Radius) doorwayBusy = true;
            }
            SetOpen(friendNear || (IsOpen && doorwayBusy));
        }

        public void Remove() => Destroy(gameObject);
    }
}
