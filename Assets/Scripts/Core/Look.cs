using UnityEngine;

namespace KingdomGame
{
    /// <summary>
    /// The current <see cref="ViewConfig"/>'s look settings, in one place every
    /// script can reach: the glow material, and shadows. Set once when the game starts.
    /// </summary>
    public static class Look
    {
        public static Material Glow { get; private set; }
        public static float ShadowOpacity { get; private set; } = 0.45f;
        public static Vector2 ShadowDirection { get; private set; } = new Vector2(0.55f, -0.65f);
        public static ViewConfig View { get; private set; }

        public static void Configure(ViewConfig view)
        {
            View = view;
            if (view == null) return;
            Glow = view.glowMaterial;
            ShadowOpacity = view.shadowOpacity;
            ShadowDirection = view.shadowDirection;
        }

        /// <summary>Makes a renderer ignore lighting, so it stays bright at night (if a glow material is set).</summary>
        public static void MakeGlow(SpriteRenderer renderer)
        {
            if (Glow != null && renderer != null) renderer.sharedMaterial = Glow;
        }

        /// <summary>
        /// A shadow under something: a child of <paramref name="parent"/>, offset away
        /// from the light by <paramref name="height"/> tiles. The parent must not be
        /// rotated (turn the shadow itself instead).
        /// </summary>
        public static SpriteRenderer AddShadow(Transform parent, Sprite shadow, float height, float rotation = 0f)
        {
            var go = new GameObject("Shadow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (Vector3)(ShadowDirection * height);
            go.transform.localRotation = Quaternion.Euler(0, 0, rotation);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = shadow;
            r.color = new Color(1f, 1f, 1f, ShadowOpacity);
            r.sortingOrder = SortOrder.Shadows;
            return r;
        }
    }
}
