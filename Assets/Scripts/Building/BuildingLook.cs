using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>Shared bits for buildings: their picture, shadow and solid body.</summary>
    public static class BuildingLook
    {
        /// <summary>The building's picture (pivot in the middle) and its shadow, as children of <paramref name="parent"/>.</summary>
        public static SpriteRenderer Make(Transform parent, Sprite look, Sprite shadow, float height, int order)
        {
            if (shadow != null) Look.AddShadow(parent, shadow, height);
            var go = new GameObject("Look");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = look;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>A static collider the size of the building (in tiles), so people walk around it.</summary>
        public static void MakeSolid(GameObject go, Vector2Int size)
        {
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(size.x - 0.1f, size.y - 0.1f);
        }

        /// <summary>The distance from <paramref name="from"/> to the nearest point of a rectangle of cells (0 inside).</summary>
        public static float Distance(RectInt cells, Vector2 from)
        {
            float dx = Mathf.Max(0f, Mathf.Max(cells.xMin - from.x, from.x - cells.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(cells.yMin - from.y, from.y - cells.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
