using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// A planned building: a see-through picture on the ground. Builders bring
    /// the resources it needs from storage, then work on it until it's done
    /// and the real building (or wall) appears. The bar under it shows the work.
    /// </summary>
    public sealed class Blueprint : MonoBehaviour
    {
        static readonly Color Waiting = new Color(0.55f, 0.7f, 1f, 0.32f);
        static readonly Color Ready = new Color(0.65f, 0.85f, 1f, 0.6f);

        readonly int[] delivered = new int[Inventory.TypeCount];
        readonly int[] incoming = new int[Inventory.TypeCount];

        SpriteRenderer ghost, barBack, barFill;
        float shownProgress = -1f;
        bool shownReady;

        public BuildingConfig Config { get; private set; }
        public RectInt Cells { get; private set; }
        public Vector2 Center => Cells.center;
        /// <summary>Seconds of work done so far.</summary>
        public float Work { get; private set; }
        /// <summary>Until this time builders skip it (nobody could find a way there).</summary>
        public float UnreachableUntil { get; set; }
        /// <summary>Villagers working on it right now (counted by the villagers themselves).</summary>
        public int Builders { get; set; }
        /// <summary>Why it isn't finished yet, for the throne view's info box (null = no problem).</summary>
        public string Problem { get; set; }
        public bool Cancelled { get; private set; }

        public void Setup(BuildingConfig config, RectInt cells, Sprite look, Sprite white)
        {
            Config = config;
            Cells = cells;
            transform.position = (Vector3)(Vector2)cells.center;

            ghost = Part("Ghost", look, SortOrder.Blueprints);
            barBack = Part("Bar", white, SortOrder.Markers);
            barFill = Part("Bar fill", white, SortOrder.Markers + 1);
            barBack.color = new Color(0f, 0f, 0f, 0.6f);
            barFill.color = new Color(0.95f, 0.8f, 0.35f, 1f);
            Refresh();
        }

        SpriteRenderer Part(string partName, Sprite sprite, int order)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        public int Required(ResourceType type) => Config.Cost(type);
        public int Delivered(ResourceType type) => delivered[(int)type];
        /// <summary>Still to be brought (not counting what builders are carrying here now).</summary>
        public int Need(ResourceType type) => Required(type) - delivered[(int)type] - incoming[(int)type];
        /// <summary>Still to be brought, counting what's on the way.</summary>
        public int Missing(ResourceType type) => Required(type) - delivered[(int)type];

        public bool HasAllResources
        {
            get
            {
                for (int i = 0; i < Inventory.TypeCount; i++)
                    if (Missing((ResourceType)i) > 0) return false;
                return true;
            }
        }

        public bool WorkDone => Work >= Config.buildSeconds;

        public void AddIncoming(ResourceType type, int amount) => incoming[(int)type] += amount;
        public void RemoveIncoming(ResourceType type, int amount) => incoming[(int)type] = Mathf.Max(0, incoming[(int)type] - amount);

        /// <summary>Takes resources in (from a builder or the ruler's backpack); returns how many it needed.</summary>
        public int Deliver(ResourceType type, int amount)
        {
            int used = Mathf.Clamp(amount, 0, Missing(type));
            delivered[(int)type] += used;
            Refresh();
            return used;
        }

        /// <summary>Adds building work (only counts once all resources are here).</summary>
        public void AddWork(float seconds)
        {
            if (!HasAllResources) return;
            Work = Mathf.Min(Config.buildSeconds, Work + seconds);
            Refresh();
        }

        /// <summary>Marks it cancelled; the kingdom refunds and removes it.</summary>
        public void MarkCancelled() => Cancelled = true;

        void Refresh()
        {
            bool ready = HasAllResources;
            float progress = Mathf.Clamp01(Work / Mathf.Max(0.01f, Config.buildSeconds));
            if (ready == shownReady && Mathf.Abs(progress - shownProgress) < 0.005f) return;
            shownReady = ready;
            shownProgress = progress;

            ghost.color = ready ? Ready : Waiting;
            bool showBar = ready;
            barBack.enabled = barFill.enabled = showBar;
            if (!showBar) return;

            // The white sprite is 4 x 4 pixels: scale it to the bar's size in tiles.
            float width = Mathf.Min(1.2f, Cells.width * 0.8f), height = 0.09f;
            float y = -Cells.height * 0.5f + 0.12f;
            const float spritePixels = 4f;
            float unit = PixelArt.PixelsPerUnit / spritePixels;
            barBack.transform.localPosition = new Vector3(0f, y, 0f);
            barBack.transform.localScale = new Vector3(width * unit, height * unit, 1f);
            barFill.transform.localPosition = new Vector3(-width * 0.5f * (1f - progress), y, 0f);
            barFill.transform.localScale = new Vector3(width * progress * unit, height * 0.7f * unit, 1f);
        }
    }
}
