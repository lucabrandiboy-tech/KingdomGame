using KingdomGame.Art;
using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// The pieces that fly off when walls break. Only for looks: every computer
    /// makes its own.
    ///
    /// Each piece is a little cube. It's a flat sprite, but drawn as a 3D cube
    /// that tumbles (it flips through <see cref="EffectArt.CubeFrames"/> pictures
    /// while it spins), grows as it rises towards the camera, and has its own
    /// shadow on the ground that drifts away from it the higher it flies. It pops
    /// up, falls, bounces, slides to a stop and then becomes rubble on the ground.
    ///
    /// Sparks are glowing dots that vanish instead. Everything is pooled: the
    /// sprites are made once and reused, up to Max Debris in the Destruction Settings.
    /// </summary>
    public sealed class PixelDebris
    {
        struct Particle
        {
            public Vector2 Position;   // on the ground
            public Vector2 Velocity;
            public float Height;       // how far above the ground, in tiles (pretend 3D)
            public float Lift;         // upward speed
            public float Age;
            public float Size;         // in tiles
            public float Angle;        // degrees
            public float Spin;         // degrees per second
            public float Tumble;       // which cube picture, counts up while flying
            public float TumbleSpeed;  // pictures per second
            public bool Spark;         // a glowing dot
            public bool Vanish;        // disappears when it stops instead of becoming rubble
        }

        readonly DestructionSettings settings;
        readonly Transform root;
        readonly Sprite[] cubeFrames;
        readonly Sprite shadowSprite, glowSprite;

        Particle[] particles = new Particle[0];
        Color32[] colors = new Color32[0];
        SpriteRenderer[] cubes = new SpriteRenderer[0];    // slot i shows particle i
        SpriteRenderer[] shadows = new SpriteRenderer[0];
        int pooled; // how many slots have sprites so far
        uint seed = 1;

        public int Count { get; private set; }

        public PixelDebris(Transform parent, DestructionSettings settings)
        {
            this.settings = settings;
            root = new GameObject("Flying Pieces").transform;
            root.SetParent(parent, false);

            // All the pictures in one texture, so every piece can be drawn in one batch.
            var atlas = new SpriteAtlasBuilder();
            var frames = EffectArt.CubeTumble();
            foreach (var frame in frames) atlas.Add(frame, "Cube");
            int shadowIndex = atlas.Add(EffectArt.SoftShadow(), "Cube shadow");
            int glowIndex = atlas.Add(EffectArt.Glow(), "Spark");
            var sprites = atlas.Build("Flying pieces");
            cubeFrames = new Sprite[frames.Length];
            System.Array.Copy(sprites, cubeFrames, frames.Length);
            shadowSprite = sprites[shadowIndex];
            glowSprite = sprites[glowIndex];

            // Make all the sprites up front, so the first big explosion doesn't stutter.
            EnsureCapacity(settings.maxDebris);
        }

        /// <summary>
        /// Starts a flying piece <paramref name="sizePixels"/> across (0 = random from the settings).
        /// Returns false when the limit is reached. Sparks, and pieces with <paramref name="vanish"/>,
        /// disappear when they stop instead of becoming rubble.
        /// </summary>
        public bool Launch(Vector2 position, Vector2 velocity, float lift, Color32 color, bool spark = false, float sizePixels = 0f, bool vanish = false)
        {
            if (Count >= settings.maxDebris) return false;
            EnsureCapacity(Count + 1); // only grows if Max Debris was raised during Play

            float r1 = Random01(), r2 = Random01(), r3 = Random01();
            if (sizePixels <= 0f)
                sizePixels = spark ? Mathf.Lerp(5f, 9f, r1) : Mathf.Lerp(settings.cubeSize.x, settings.cubeSize.y, r1 * r1);

            int i = Count++;
            particles[i] = new Particle
            {
                Position = position,
                Velocity = velocity,
                Lift = lift,
                Height = 0.01f,
                Size = sizePixels / PixelArt.PixelsPerUnit,
                Angle = r2 * 360f,
                Spin = (r3 - 0.5f) * 900f,
                Tumble = r2 * EffectArt.CubeFrames,
                TumbleSpeed = Mathf.Lerp(8f, 22f, r3) * (r1 < 0.5f ? 1f : -1f),
                Spark = spark,
                Vanish = spark || vanish,
            };
            colors[i] = color;
            Show(i);
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
                        // Landed: bounce a little, lose some sideways speed and spin.
                        p.Height = 0f;
                        p.Lift = -p.Lift * settings.bounce;
                        if (p.Lift < 1f) p.Lift = 0f;
                        p.Velocity *= 0.6f;
                        p.Spin *= 0.5f;
                        p.TumbleSpeed *= 0.4f;
                    }
                    p.Velocity *= airKeep;
                    p.Tumble += p.TumbleSpeed * deltaTime;
                }
                else
                {
                    p.Velocity *= groundKeep;
                    p.Spin *= groundKeep;
                }
                p.Position += p.Velocity * deltaTime;
                p.Angle += p.Spin * deltaTime;

                bool stopped = p.Height <= 0f && p.Lift <= 0f && p.Velocity.sqrMagnitude < settle2;
                if (stopped || p.Age > settings.maxDebrisSeconds)
                {
                    if (!p.Vanish) rubble?.Stamp(p.Position, colors[i], p.Size * PixelArt.PixelsPerUnit);
                    RemoveAt(i);
                    i--; // the last particle moved into slot i
                    continue;
                }
                Place(i);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++) Hide(i);
            Count = 0;
        }

        public void Dispose()
        {
            if (root != null) Object.Destroy(root.gameObject);
            if (shadowSprite != null)
            {
                Object.Destroy(shadowSprite.texture);
                foreach (var s in cubeFrames) Object.Destroy(s);
                Object.Destroy(shadowSprite);
                Object.Destroy(glowSprite);
            }
        }

        void RemoveAt(int i)
        {
            int last = --Count;
            if (i != last)
            {
                particles[i] = particles[last];
                colors[i] = colors[last];
                Show(i);
            }
            Hide(last);
        }

        // Sets up slot i's sprites for the particle now in it.
        void Show(int i)
        {
            var p = particles[i];
            var cube = cubes[i];
            if (p.Spark)
            {
                cube.sprite = glowSprite;
                cube.color = colors[i];
                cube.sortingOrder = SortOrder.Sparks;
                if (Look.Glow != null) cube.sharedMaterial = Look.Glow;
                shadows[i].enabled = false;
            }
            else
            {
                // The cube pictures are grey; brighten the tint a little so the lit face shows the true color.
                var c = colors[i];
                cube.color = new Color(Mathf.Min(1f, c.r / 255f * 1.15f), Mathf.Min(1f, c.g / 255f * 1.15f), Mathf.Min(1f, c.b / 255f * 1.15f), 1f);
                cube.sortingOrder = SortOrder.Debris;
                if (defaultMaterial != null) cube.sharedMaterial = defaultMaterial;
                shadows[i].enabled = true;
            }
            cube.enabled = true;
            Place(i);
        }

        void Hide(int i)
        {
            cubes[i].enabled = false;
            shadows[i].enabled = false;
        }

        void Place(int i)
        {
            var p = particles[i];
            float lift = 1f + p.Height * settings.heightScale;
            var cube = cubes[i].transform;
            cube.SetPositionAndRotation(p.Position, Quaternion.Euler(0f, 0f, p.Angle));
            if (p.Spark)
            {
                float fade = Mathf.Clamp01(1f - p.Age / Mathf.Max(0.05f, settings.maxDebrisSeconds * 0.5f));
                cube.localScale = Vector3.one * (p.Size * PixelArt.PixelsPerUnit / 12f) * lift * (0.4f + 0.6f * fade);
                return;
            }
            int frame = ((int)Mathf.Floor(p.Tumble) % EffectArt.CubeFrames + EffectArt.CubeFrames) % EffectArt.CubeFrames;
            cubes[i].sprite = cubeFrames[frame];
            cube.localScale = Vector3.one * (p.Size * PixelArt.PixelsPerUnit / EffectArt.CubeEdge) * lift;

            // The shadow stays on the ground, drifts away from the light as the cube rises, and fades.
            var shadow = shadows[i];
            shadow.transform.position = p.Position + Look.ShadowDirection * p.Height;
            shadow.transform.localScale = Vector3.one * (p.Size * PixelArt.PixelsPerUnit / 10f) * (1f + p.Height * 0.5f);
            shadow.color = new Color(0f, 0f, 0f, Look.ShadowOpacity * 1.2f / (1f + p.Height * 2f));
        }

        Material defaultMaterial;

        void EnsureCapacity(int needed)
        {
            if (needed > particles.Length)
            {
                System.Array.Resize(ref particles, needed);
                System.Array.Resize(ref colors, needed);
                System.Array.Resize(ref cubes, needed);
                System.Array.Resize(ref shadows, needed);
            }
            while (pooled < needed)
            {
                var go = new GameObject("Piece");
                go.transform.SetParent(root, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = cubeFrames[0];
                r.sortingOrder = SortOrder.Debris;
                r.enabled = false;
                if (defaultMaterial == null) defaultMaterial = r.sharedMaterial;
                cubes[pooled] = r;

                var shadowGo = new GameObject("Piece shadow");
                shadowGo.transform.SetParent(root, false);
                var s = shadowGo.AddComponent<SpriteRenderer>();
                s.sprite = shadowSprite;
                s.sortingOrder = SortOrder.Shadows + 1;
                s.enabled = false;
                shadows[pooled++] = s;
            }
        }

        // Cheap random numbers for the look of pieces (not used for game rules).
        float Random01()
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return (seed & 0xFFFFFF) / 16777216f;
        }
    }
}
