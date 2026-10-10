using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// The little flames of a torch, candelabra or fireplace: bright pictures that
    /// ignore the darkness and flicker, plus one fire light for all of them.
    /// </summary>
    public sealed class Flames : MonoBehaviour
    {
        SpriteRenderer[] flames;
        Vector3[] baseScale;
        float seed;

        /// <summary>
        /// Puts a flame at each offset (tiles from the middle, already turned the way the
        /// thing faces), <paramref name="size"/> times a torch flame, and a fire light of
        /// <paramref name="lightSize"/> (1 = a brazier's) in the middle of them. With no
        /// offsets there are no flames to see, just the light (embers under a cauldron).
        /// </summary>
        public void Setup(Sprite flame, Vector2[] offsets, float size, float lightSize, int order)
        {
            seed = Random.value * 100f;
            if (offsets == null || offsets.Length == 0)
            {
                flames = new SpriteRenderer[0];
                if (lightSize > 0f) FireLight.Create(transform, Vector2.zero, lightSize);
                return;
            }
            flames = new SpriteRenderer[offsets.Length];
            baseScale = new Vector3[offsets.Length];
            var middle = Vector2.zero;
            for (int i = 0; i < offsets.Length; i++)
            {
                var go = new GameObject("Flame");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = offsets[i];
                go.transform.localScale = Vector3.one * size;
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = flame;
                r.sortingOrder = order;
                Look.MakeGlow(r);
                flames[i] = r;
                baseScale[i] = go.transform.localScale;
                middle += offsets[i];
            }
            if (lightSize > 0f) FireLight.Create(transform, middle / offsets.Length, lightSize);
        }

        void Update()
        {
            if (flames == null) return;
            float t = Time.time;
            for (int i = 0; i < flames.Length; i++)
            {
                if (flames[i] == null) continue;
                float f = Mathf.PerlinNoise(t * 7f + seed + i * 3.1f, seed);
                flames[i].transform.localScale = baseScale[i] * (0.85f + 0.3f * f);
                flames[i].color = new Color(1f, 0.85f + 0.15f * f, 0.7f + 0.3f * f, 1f);
            }
        }
    }
}
