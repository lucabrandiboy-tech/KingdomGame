using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// A storehouse or a chest: the kingdom's resources live here. Builders take
    /// what blueprints need from it; the ruler empties their backpack into it (E).
    /// Builders reserve what they're coming to fetch, so two of them never walk
    /// over for the same stones.
    /// </summary>
    public sealed class Storage : MonoBehaviour, IBuilt
    {
        readonly int[] reserved = new int[Inventory.TypeCount];

        public BuildingConfig Config { get; private set; }
        public RectInt Cells { get; private set; }
        public int Rotation { get; private set; }
        public Inventory Stock { get; private set; }
        public Vector2 Center => Cells.center;

        public void Setup(BuildingConfig config, RectInt cells, Sprite look, Sprite shadow, int rotation = 0)
        {
            Config = config;
            Cells = cells;
            Rotation = rotation;
            Stock = new Inventory(config != null ? config.storageCapacity : 1000);
            transform.position = (Vector3)(Vector2)cells.center;
            float height = config != null && config.kind == BuildingKind.Storage && config.size.x * config.size.y <= 1 ? config.height : 0.3f;
            BuildingLook.Make(transform, look, shadow, height, SortOrder.Buildings);
            BuildingLook.MakeSolid(gameObject, cells.size);
        }

        /// <summary>Takes it away (torn down). What's inside is lost unless the kingdom moved it first.</summary>
        public void Remove() => Destroy(gameObject);

        /// <summary>A lantern on the porch (a small fire).</summary>
        public void AddLantern(Sprite fire)
        {
            if (fire == null) return;
            var go = new GameObject("Lantern");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(Cells.width * 0.18f, -Cells.height * 0.36f, 0f);
            go.AddComponent<Brazier>().Setup(fire, 0.45f, 0.75f);   // small, but lights the porch
        }

        /// <summary>How much of a resource is here and not yet promised to a builder.</summary>
        public int Available(ResourceType type) => Stock.Get(type) - reserved[(int)type];

        /// <summary>Promises up to <paramref name="amount"/> to a builder on the way; returns how much was promised.</summary>
        public int Reserve(ResourceType type, int amount)
        {
            int promised = Mathf.Clamp(amount, 0, Available(type));
            reserved[(int)type] += promised;
            return promised;
        }

        public void Unreserve(ResourceType type, int amount) =>
            reserved[(int)type] = Mathf.Max(0, reserved[(int)type] - amount);

        /// <summary>A builder picks up what was promised to them. Returns how much they got.</summary>
        public int TakeReserved(ResourceType type, int amount)
        {
            Unreserve(type, amount);
            int taken = Mathf.Min(amount, Stock.Get(type));
            if (taken > 0) Stock.TryRemove(type, taken);
            return taken;
        }

        /// <summary>Puts resources in; returns how much fitted.</summary>
        public int Deposit(ResourceType type, int amount) => Stock.Add(type, amount);
    }
}
