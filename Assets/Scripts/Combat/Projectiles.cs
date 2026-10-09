using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Combat
{
    /// <summary>
    /// Every bullet and rocket in flight. Each frame a shot checks the pixels
    /// along its path (<see cref="PixelWorld.Raycast"/>), so it flies through
    /// holes and hits the exact pixel it touches, then becomes a damage event.
    /// Sprites are pooled and reused.
    /// </summary>
    public sealed class Projectiles : MonoBehaviour
    {
        struct Shot
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float RangeLeft;
            public WeaponConfig Weapon;
        }

        [Tooltip("Most shots in flight at once; extra shots aren't fired.")]
        [SerializeField] int maxShots = 512;
        [SerializeField] int sortingOrder = 21000;

        readonly List<Shot> shots = new List<Shot>();
        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly Dictionary<WeaponConfig, Sprite> sprites = new Dictionary<WeaponConfig, Sprite>();

        public int Count => shots.Count;

        /// <summary>Fires one shot from <paramref name="from"/> in <paramref name="direction"/> (plus the weapon's spread).</summary>
        public bool Fire(WeaponConfig weapon, Vector2 from, Vector2 direction)
        {
            if (weapon == null || shots.Count >= maxShots) return false;
            float spread = Random.Range(-weapon.spreadDegrees, weapon.spreadDegrees);
            direction = Quaternion.Euler(0, 0, spread) * direction.normalized;

            shots.Add(new Shot
            {
                Position = from,
                Velocity = direction * weapon.projectileSpeed,
                RangeLeft = weapon.range,
                Weapon = weapon,
            });

            var r = RendererFor(shots.Count - 1);
            r.sprite = SpriteFor(weapon);
            r.enabled = true;
            Place(r, from, direction);
            return true;
        }

        void Update()
        {
            var world = PixelWorld.Current;
            float dt = Time.deltaTime;

            for (int i = 0; i < shots.Count; i++)
            {
                var shot = shots[i];
                var step = shot.Velocity * dt;
                float length = step.magnitude;
                bool lastStep = length >= shot.RangeLeft;
                if (lastStep) step *= shot.RangeLeft / Mathf.Max(length, 1e-6f);
                var to = shot.Position + step;

                if (world != null && world.Raycast(shot.Position, to, out var pixel, out _))
                {
                    Detonate(world, shot.Weapon, PixelWorld.PixelCenter(pixel.x, pixel.y));
                    RemoveAt(i--);
                    continue;
                }
                if (lastStep)
                {
                    if (shot.Weapon.explodeAtEndOfRange && world != null) Detonate(world, shot.Weapon, to);
                    RemoveAt(i--);
                    continue;
                }

                shot.Position = to;
                shot.RangeLeft -= length;
                shots[i] = shot;
                Place(renderers[i], to, shot.Velocity);
            }
        }

        static void Detonate(PixelWorld world, WeaponConfig weapon, Vector2 at)
        {
            if (weapon.damage != null && world.TryMakeEvent(weapon.damage, at, out var hit))
                world.ApplyDamage(hit);
        }

        void RemoveAt(int i)
        {
            int last = shots.Count - 1;
            if (i != last)
            {
                shots[i] = shots[last];
                var moved = renderers[last];
                renderers[last] = renderers[i];
                renderers[i] = moved;
            }
            shots.RemoveAt(last);
            renderers[last].enabled = false;
        }

        SpriteRenderer RendererFor(int index)
        {
            while (renderers.Count <= index)
            {
                var go = new GameObject("Shot");
                go.transform.SetParent(transform, false);
                var r = go.AddComponent<SpriteRenderer>();
                r.sortingOrder = sortingOrder;
                r.enabled = false;
                renderers.Add(r);
            }
            return renderers[index];
        }

        Sprite SpriteFor(WeaponConfig weapon)
        {
            if (!sprites.TryGetValue(weapon, out var sprite))
            {
                sprite = PixelTextures.ToSprite(
                    CharacterArt.Projectile(weapon.projectileLength, weapon.projectileThickness, weapon.projectileColor), weapon.name);
                sprites.Add(weapon, sprite);
            }
            return sprite;
        }

        static void Place(SpriteRenderer r, Vector2 position, Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            r.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 0, angle));
        }

        void OnDestroy()
        {
            foreach (var sprite in sprites.Values)
            {
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }
    }
}
