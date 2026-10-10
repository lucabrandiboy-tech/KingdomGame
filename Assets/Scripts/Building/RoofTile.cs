using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// One cell of built roof. It draws nothing itself: <see cref="RoofView"/> draws each
    /// roof (all the touching cells of the same kind) as one picture made for its shape.
    /// </summary>
    public sealed class RoofTile : MonoBehaviour, IBuilt
    {
        public BuildingConfig Config { get; private set; }
        public RectInt Cells { get; private set; }
        public int Rotation => 0;
        public Vector2Int Cell => Cells.position;

        public void Setup(BuildingConfig config, Vector2Int cell)
        {
            Config = config;
            Cells = new RectInt(cell, Vector2Int.one);
            transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
        }

        public void Remove() => Destroy(gameObject);
    }
}
