using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>An iron bowl of burning coals: it glows, and lights its surroundings with a flickering fire light.</summary>
    public sealed class Brazier : MonoBehaviour
    {
        SpriteRenderer coals;
        float seed;

        /// <param name="size">1 = a full brazier; smaller for a torch or lantern.</param>
        /// <param name="lightSize">How far and bright its light is (1 = a full brazier's); the same as <paramref name="size"/> if left out.</param>
        public void Setup(Sprite sprite, float size = 1f, float lightSize = -1f)
        {
            coals = gameObject.AddComponent<SpriteRenderer>();
            coals.sprite = sprite;
            coals.sortingOrder = SortOrder.Buildings + 1;
            transform.localScale = Vector3.one * size;
            Look.MakeGlow(coals);
            FireLight.Create(transform.parent != null ? transform.parent : transform, transform.localPosition, lightSize > 0f ? lightSize : size);
            seed = Random.value * 100f;
        }

        void Update()
        {
            // The coals glow brighter and dimmer with the flames.
            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 5f + seed, seed);
            coals.color = new Color(1f, 0.9f + 0.1f * flicker, 0.85f + 0.15f * flicker);
        }
    }
}
