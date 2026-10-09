using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// The throne on its dais, with two burning braziers. Sit on it (E) to see
    /// your whole kingdom and place buildings. The world doesn't pause while
    /// you sit there.
    /// </summary>
    public sealed class Throne : MonoBehaviour
    {
        public RectInt Cells { get; private set; }
        public Vector2 Center => Cells.center;
        /// <summary>Where the ruler sits.</summary>
        public Vector2 Seat => Cells.center + new Vector2(0f, 0.45f);
        /// <summary>Where the ruler stands up to: in front of the dais.</summary>
        public Vector2 Front => new Vector2(Cells.center.x, Cells.yMin - 0.45f);

        public void Setup(RectInt cells, Sprite look, Sprite shadow, Sprite brazier)
        {
            Cells = cells;
            transform.position = (Vector3)(Vector2)cells.center;
            BuildingLook.Make(transform, look, shadow, 0.12f, SortOrder.Buildings);
            BuildingLook.MakeSolid(gameObject, cells.size);
            foreach (float side in new[] { -1f, 1f })
            {
                var go = new GameObject("Brazier");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(side * (cells.size.x * 0.5f - 0.2f), -(cells.size.y * 0.5f - 0.2f), 0f);
                go.AddComponent<Brazier>().Setup(brazier);
            }
        }
    }
}
