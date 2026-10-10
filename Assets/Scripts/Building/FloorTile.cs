using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>One tile of built floor (wooden boards or flagstones), drawn under everything.</summary>
    public sealed class FloorTile : MonoBehaviour, IBuilt
    {
        public BuildingConfig Config { get; private set; }
        public RectInt Cells { get; private set; }
        public int Rotation => 0;

        public void Setup(BuildingConfig config, Vector2Int cell, Sprite look)
        {
            Config = config;
            Cells = new RectInt(cell, Vector2Int.one);
            transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
            var r = gameObject.AddComponent<SpriteRenderer>();
            r.sprite = look;
            r.sortingOrder = SortOrder.Floors;
        }

        public void Remove() => Destroy(gameObject);
    }
}
