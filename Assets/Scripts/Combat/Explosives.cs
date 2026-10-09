using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Combat
{
    /// <summary>
    /// Thrown explosives in the world. A charge flies in a short arc, sticks to
    /// the first wall pixel it touches, blinks faster and faster, then turns into
    /// a damage event at the pixel it's stuck to.
    /// </summary>
    public sealed class Explosives : MonoBehaviour
    {
        struct Charge
        {
            public ExplosiveConfig Config;
            public Vector2 Start, Target, Position;
            public float Flight, FlightTime;  // seconds flown, seconds the flight takes
            public bool Landed;
            public bool OnWall;
            public Vector2Int WallPixel;
            public float Fuse;
        }

        [SerializeField] int sortingOrder = 15000;

        readonly List<Charge> charges = new List<Charge>();
        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        Sprite lightOn, lightOff;

        public int Count => charges.Count;

        void Awake()
        {
            lightOn = PixelTextures.ToSprite(CharacterArt.C4(true), "C4 on");
            lightOff = PixelTextures.ToSprite(CharacterArt.C4(false), "C4 off");
        }

        /// <summary>Throws a charge from <paramref name="from"/> towards <paramref name="target"/> (capped at its range).</summary>
        public void Throw(ExplosiveConfig config, Vector2 from, Vector2 target)
        {
            if (config == null) return;
            var offset = Vector2.ClampMagnitude(target - from, config.throwRange);
            charges.Add(new Charge
            {
                Config = config,
                Start = from,
                Target = from + offset,
                Position = from,
                FlightTime = Mathf.Max(0.05f, offset.magnitude / config.throwSpeed),
                Fuse = config.fuseSeconds,
            });
            var r = RendererFor(charges.Count - 1);
            r.enabled = true;
            r.sprite = lightOff;
            r.transform.position = from;
        }

        void Update()
        {
            var world = PixelWorld.Current;
            float dt = Time.deltaTime;

            for (int i = 0; i < charges.Count; i++)
            {
                var c = charges[i];
                float height = 0f;

                if (!c.Landed)
                {
                    c.Flight += dt;
                    float t = Mathf.Clamp01(c.Flight / c.FlightTime);
                    var next = Vector2.Lerp(c.Start, c.Target, t);
                    if (world != null && world.Raycast(c.Position, next, out var pixel, out var hitPoint))
                    {
                        // Stuck to the wall: sit just in front of the face it hit.
                        var back = (c.Position - next).normalized * 0.08f;
                        c.Position = hitPoint + back;
                        c.OnWall = true;
                        c.WallPixel = pixel;
                        c.Landed = true;
                    }
                    else
                    {
                        c.Position = next;
                        c.Landed = t >= 1f;
                        height = 4f * t * (1f - t) * 0.5f; // a little arc while it flies
                    }
                }
                else
                {
                    c.Fuse -= dt;
                    if (c.Fuse <= 0f)
                    {
                        Detonate(world, c);
                        RemoveAt(i--);
                        continue;
                    }
                }

                charges[i] = c;
                var r = renderers[i];
                r.transform.position = new Vector3(c.Position.x, c.Position.y + height, 0f);
                // Blinks once a second, then faster and faster near the end.
                float period = c.Landed ? Mathf.Lerp(0.12f, 1f, Mathf.Clamp01(c.Fuse / c.Config.fuseSeconds)) : 1f;
                r.sprite = c.Landed && (c.Fuse % period) < period * 0.5f ? lightOn : lightOff;
            }
        }

        static void Detonate(PixelWorld world, Charge c)
        {
            if (world == null || c.Config.damage == null) return;
            var at = c.OnWall ? PixelWorld.PixelCenter(c.WallPixel.x, c.WallPixel.y) : c.Position;
            if (world.TryMakeEvent(c.Config.damage, at, out var hit)) world.ApplyDamage(hit);
        }

        void RemoveAt(int i)
        {
            int last = charges.Count - 1;
            if (i != last)
            {
                charges[i] = charges[last];
                (renderers[i], renderers[last]) = (renderers[last], renderers[i]);
            }
            charges.RemoveAt(last);
            renderers[last].enabled = false;
        }

        SpriteRenderer RendererFor(int index)
        {
            while (renderers.Count <= index)
            {
                var go = new GameObject("C4");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sortingOrder = sortingOrder;
                r.enabled = false;
                renderers.Add(r);
            }
            return renderers[index];
        }

        void OnDestroy()
        {
            foreach (var sprite in new[] { lightOn, lightOff })
            {
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }
    }
}
