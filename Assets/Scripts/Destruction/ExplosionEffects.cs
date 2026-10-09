using KingdomGame.Art;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// What an explosion looks like apart from the hole: a flash of light that
    /// lights up the walls around it, a fireball, smoke that drifts and fades,
    /// and a scorch mark on the ground. Only for looks. Pooled, so explosions
    /// don't create or destroy objects.
    /// </summary>
    public sealed class ExplosionEffects
    {
        const int Flashes = 6;
        const int Puffs = 96;
        const float FlashSeconds = 0.35f;

        struct Puff
        {
            public Vector2 Position, Velocity;
            public float Age, Life, StartSize, EndSize, Spin;
            public bool Fire;
        }

        readonly DestructionSettings settings;
        readonly Transform root;
        readonly Light2D[] lights = new Light2D[Flashes];
        readonly float[] lightAge = new float[Flashes];
        readonly float[] lightPower = new float[Flashes];
        readonly SpriteRenderer[] puffRenderers = new SpriteRenderer[Puffs];
        readonly Puff[] puffs = new Puff[Puffs];
        readonly Sprite[] smokeSprites;
        readonly Sprite fireSprite;
        int nextLight, nextPuff;
        uint seed = 11;

        public ExplosionEffects(Transform parent, DestructionSettings settings)
        {
            this.settings = settings;
            root = new GameObject("Explosion Effects").transform;
            root.SetParent(parent, false);

            var atlas = new SpriteAtlasBuilder();
            for (int i = 0; i < 3; i++) atlas.Add(EffectArt.Smoke(i), $"Smoke {i}");
            atlas.Add(EffectArt.Glow(32), "Fireball");
            var sprites = atlas.Build("Explosion effects");
            smokeSprites = new[] { sprites[0], sprites[1], sprites[2] };
            fireSprite = sprites[3];

            for (int i = 0; i < Flashes; i++)
            {
                var light = new GameObject("Flash").AddComponent<Light2D>();
                light.transform.SetParent(root, false);
                light.lightType = Light2D.LightType.Point;
                light.enabled = false;
                lights[i] = light;
                lightAge[i] = FlashSeconds;
            }
            for (int i = 0; i < Puffs; i++)
            {
                var go = new GameObject("Puff");
                go.transform.SetParent(root, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.enabled = false;
                if (i == 0) defaultMaterial = r.sharedMaterial;
                puffRenderers[i] = r;
                puffs[i].Age = puffs[i].Life = 1f;
            }
        }

        /// <summary>Shows an explosion of this radius (in pixels) at a world position.</summary>
        public void Explode(Vector2 at, float radiusPixels, RubbleLayer rubble)
        {
            float radius = radiusPixels / PixelArt.PixelsPerUnit;

            // Flash of light.
            int li = nextLight++ % Flashes;
            var light = lights[li];
            light.transform.position = at;
            light.color = settings.flashColor;
            light.pointLightOuterRadius = Mathf.Max(1.5f, radius * 6f);
            light.pointLightInnerRadius = radius;
            lightAge[li] = 0f;
            lightPower[li] = Mathf.Clamp(radius * 3f, 1f, 3f);
            light.enabled = true;

            // Fireball: a glowing ball that swells and fades quickly.
            Spawn(at, Vector2.zero, 0.25f, radius * 1.4f, radius * 2.6f, true);

            // Smoke.
            int count = Mathf.RoundToInt(radiusPixels / 10f * settings.smokePerTenPixels);
            for (int k = 0; k < count; k++)
            {
                float a = Random01() * Mathf.PI * 2f;
                var direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var start = at + direction * radius * Random01() * 0.6f;
                var velocity = direction * Mathf.Lerp(0.2f, 1.2f, Random01()) * Mathf.Max(0.5f, radius) + new Vector2(0.15f, 0.1f);
                Spawn(start, velocity, Mathf.Lerp(1.6f, 3.2f, Random01()), radius * 0.7f, radius * Mathf.Lerp(1.6f, 2.6f, Random01()), false);
            }

            if (settings.scorchSize > 0f && rubble != null) rubble.Scorch(at, radiusPixels * settings.scorchSize);
        }

        void Spawn(Vector2 at, Vector2 velocity, float life, float startSize, float endSize, bool fire)
        {
            int i = nextPuff++ % Puffs;
            puffs[i] = new Puff
            {
                Position = at, Velocity = velocity, Age = 0f, Life = life,
                StartSize = Mathf.Max(0.05f, startSize), EndSize = Mathf.Max(0.1f, endSize),
                Spin = (Random01() - 0.5f) * 60f, Fire = fire,
            };
            var r = puffRenderers[i];
            r.enabled = true;
            if (fire)
            {
                r.sprite = fireSprite;
                r.sortingOrder = SortOrder.Sparks + 1;
                Look.MakeGlow(r);
            }
            else
            {
                r.sprite = smokeSprites[i % smokeSprites.Length];
                r.sortingOrder = SortOrder.Smoke;
                if (defaultMaterial != null) r.sharedMaterial = defaultMaterial;
            }
            r.transform.rotation = Quaternion.Euler(0, 0, Random01() * 360f);
        }

        Material defaultMaterial;

        public void Update(float deltaTime)
        {
            for (int i = 0; i < Flashes; i++)
            {
                if (lightAge[i] >= FlashSeconds) continue;
                lightAge[i] += deltaTime;
                float t = Mathf.Clamp01(lightAge[i] / FlashSeconds);
                lights[i].intensity = lightPower[i] * (1f - t) * (1f - t);
                if (t >= 1f) lights[i].enabled = false;
            }

            for (int i = 0; i < Puffs; i++)
            {
                ref var p = ref puffs[i];
                if (p.Age >= p.Life) continue;
                p.Age += deltaTime;
                var r = puffRenderers[i];
                if (p.Age >= p.Life)
                {
                    r.enabled = false;
                    continue;
                }
                float t = p.Age / p.Life;
                p.Velocity *= 1f / (1f + 2.5f * deltaTime);
                p.Position += p.Velocity * deltaTime;
                float size = Mathf.Lerp(p.StartSize, p.EndSize, 1f - (1f - t) * (1f - t));
                r.transform.position = p.Position;
                r.transform.Rotate(0f, 0f, p.Spin * deltaTime);
                // Sprites are 48 (smoke) or 32 (fire) pixels across; size is the wanted width in tiles.
                float pixels = p.Fire ? 32f : 48f;
                r.transform.localScale = Vector3.one * (size * PixelArt.PixelsPerUnit / pixels);
                if (p.Fire)
                {
                    r.color = new Color(1f, Mathf.Lerp(0.85f, 0.45f, t), Mathf.Lerp(0.55f, 0.15f, t), (1f - t) * 0.95f);
                }
                else
                {
                    var c = settings.smokeColor;
                    float alpha = c.a * Mathf.Clamp01(t * 8f) * (1f - t);
                    r.color = new Color(c.r, c.g, c.b, alpha);
                }
            }
        }

        public void Clear()
        {
            for (int i = 0; i < Flashes; i++) { lightAge[i] = FlashSeconds; lights[i].enabled = false; }
            for (int i = 0; i < Puffs; i++) { puffs[i].Age = puffs[i].Life; puffRenderers[i].enabled = false; }
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root.gameObject);
            if (fireSprite != null)
            {
                Object.Destroy(fireSprite.texture);
                foreach (var s in smokeSprites) Object.Destroy(s);
                Object.Destroy(fireSprite);
            }
        }

        float Random01()
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return (seed & 0xFFFFFF) / 16777216f;
        }
    }
}
