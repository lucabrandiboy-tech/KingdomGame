using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// The pixels that fly off when walls break. Only for looks: every computer
    /// makes its own.
    ///
    /// Each pixel pops up, falls, bounces, slides to a stop and then turns into
    /// rubble on the ground. They are pooled: the on-screen sprites are made
    /// once and reused, and there is a hard limit (Max Debris in the
    /// Destruction Settings).
    /// </summary>
    public sealed class PixelDebris
    {
        struct Particle
        {
            public Vector2 Position;   // on the ground
            public Vector2 Velocity;
            public float Height;       // how far above the ground (pretend 3D)
            public float Lift;         // upward speed
            public float Age;
            public bool Spark;         // vanishes instead of becoming rubble
        }

        readonly DestructionSettings settings;
        readonly Transform root;
        readonly Texture2D pixelTexture;
        readonly Sprite pixelSprite;

        Particle[] particles = new Particle[0];
        Color32[] colors = new Color32[0];
        SpriteRenderer[] renderers = new SpriteRenderer[0]; // slot i shows particle i
        int pooled; // how many renderers exist so far

        public int Count { get; private set; }

        public PixelDebris(Transform parent, DestructionSettings settings)
        {
            this.settings = settings;
            root = new GameObject("Flying Pixels").transform;
            root.SetParent(parent, false);

            pixelTexture = PixelTextures.NewTexture(1, 1, "Debris Pixel");
            pixelTexture.SetPixel(0, 0, Color.white);
            pixelTexture.Apply(false, true);
            pixelSprite = PixelTextures.NewSprite(pixelTexture, new Vector2(0.5f, 0.5f));

            // Make all the sprites up front, so the first big explosion doesn't stutter.
            EnsureCapacity(settings.maxDebris);
        }

        /// <summary>
        /// Starts a flying pixel. Returns false when the limit is reached.
        /// Sparks vanish when they stop instead of becoming rubble.
        /// </summary>
        public bool Launch(Vector2 position, Vector2 velocity, float lift, Color32 color, bool spark = false)
        {
            if (Count >= settings.maxDebris) return false;
            EnsureCapacity(Count + 1); // only grows if Max Debris was raised during Play

            int i = Count++;
            particles[i] = new Particle { Position = position, Velocity = velocity, Lift = lift, Height = 0.01f, Spark = spark };
            colors[i] = color;
            var r = renderers[i];
            r.color = color;
            r.enabled = true;
            Place(i);
            return true;
        }

        public void Update(float deltaTime, RubbleLayer rubble)
        {
            float gravity = settings.gravity;
            float airKeep = 1f / (1f + settings.airDrag * deltaTime);
            float groundKeep = 1f / (1f + settings.groundFriction * deltaTime);
            float settle2 = settings.settleSpeed * settings.settleSpeed;

            for (int i = 0; i < Count; i++)
            {
                ref var p = ref particles[i];
                p.Age += deltaTime;

                bool airborne = p.Height > 0f || p.Lift > 0f;
                if (airborne)
                {
                    p.Lift -= gravity * deltaTime;
                    p.Height += p.Lift * deltaTime;
                    if (p.Height <= 0f)
                    {
                        // Landed: bounce a little, lose some sideways speed.
                        p.Height = 0f;
                        p.Lift = -p.Lift * settings.bounce;
                        if (p.Lift < 1f) p.Lift = 0f;
                        p.Velocity *= 0.6f;
                    }
                    p.Velocity *= airKeep;
                }
                else
                {
                    p.Velocity *= groundKeep;
                }
                p.Position += p.Velocity * deltaTime;

                bool stopped = p.Height <= 0f && p.Lift <= 0f && p.Velocity.sqrMagnitude < settle2;
                if (stopped || p.Age > settings.maxDebrisSeconds)
                {
                    if (!p.Spark) rubble?.Drop(p.Position, colors[i]);
                    RemoveAt(i);
                    i--; // the last particle moved into slot i
                    continue;
                }
                Place(i);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++) renderers[i].enabled = false;
            Count = 0;
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root.gameObject);
            Object.Destroy(pixelSprite);
            Object.Destroy(pixelTexture);
        }

        void RemoveAt(int i)
        {
            int last = --Count;
            if (i != last)
            {
                particles[i] = particles[last];
                colors[i] = colors[last];
                renderers[i].color = colors[i];
            }
            renderers[last].enabled = false;
        }

        // Draws the pixel snapped to the pixel grid, raised by its height.
        void Place(int i)
        {
            const float ppu = PixelArt.PixelsPerUnit;
            var p = particles[i];
            float x = (Mathf.Floor(p.Position.x * ppu) + 0.5f) / ppu;
            float y = (Mathf.Floor((p.Position.y + p.Height) * ppu) + 0.5f) / ppu;
            renderers[i].transform.position = new Vector3(x, y, 0f);
        }

        void EnsureCapacity(int needed)
        {
            if (needed > particles.Length)
            {
                System.Array.Resize(ref particles, needed);
                System.Array.Resize(ref colors, needed);
                System.Array.Resize(ref renderers, needed);
            }
            while (pooled < needed)
            {
                var go = new GameObject("Pixel");
                go.transform.SetParent(root, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = pixelSprite;
                r.sortingOrder = settings.debrisSortingOrder;
                r.enabled = false;
                renderers[pooled++] = r;
            }
        }
    }
}
