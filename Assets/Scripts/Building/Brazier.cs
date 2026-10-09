using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KingdomGame.Building
{
    /// <summary>An iron bowl of burning coals: glows, and its light flickers.</summary>
    public sealed class Brazier : MonoBehaviour
    {
        Light2D fireLight;
        SpriteRenderer coals;
        float seed;

        public void Setup(Sprite sprite)
        {
            coals = gameObject.AddComponent<SpriteRenderer>();
            coals.sprite = sprite;
            coals.sortingOrder = SortOrder.Buildings + 1;
            Look.MakeGlow(coals);

            fireLight = new GameObject("Fire light").AddComponent<Light2D>();
            fireLight.transform.SetParent(transform, false);
            fireLight.lightType = Light2D.LightType.Point;
            fireLight.color = new Color(1f, 0.6f, 0.28f);
            fireLight.pointLightInnerRadius = 0.3f;
            fireLight.pointLightOuterRadius = 3.2f;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time * 7f + seed;
            float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(t, seed);
            fireLight.intensity = 1.1f * flicker;
            coals.color = new Color(1f, 0.85f + 0.15f * flicker, 0.8f + 0.2f * flicker);
        }
    }
}
