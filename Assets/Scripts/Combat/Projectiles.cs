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
    /// Shots also hit zombies (<see cref="Health.Raycast"/>), whichever comes
    /// first, and fly over a wall right in front of the shooter
    /// (<see cref="WeaponConfig.overWalls"/>). Sprites are pooled and reused.
    /// </summary>
    public sealed class Projectiles : MonoBehaviour
    {
        struct Shot
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float RangeLeft;
            public float Flown;      // tiles so far
            public float OverWalls;  // flies over walls for this many tiles
            public WeaponConfig Weapon;
        }

        [Tooltip("Most shots in flight at once; extra shots aren't fired.")]
        [SerializeField] int maxShots = 512;

        readonly List<Shot> shots = new List<Shot>();
        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        readonly Dictionary<WeaponConfig, Sprite> sprites = new Dictionary<WeaponConfig, Sprite>();

        public int Count => shots.Count;

        /// <summary>
        /// Fires one shot from <paramref name="from"/> in <paramref name="direction"/> (plus the weapon's spread).
        /// <paramref name="overWalls"/> and <paramref name="range"/> replace the weapon's own when not negative
        /// (shots from up on a wall or a tower fly over the walls below; a mortar shell lands where it's aimed).
        /// </summary>
        public bool Fire(WeaponConfig weapon, Vector2 from, Vector2 direction, float overWalls = -1f, float range = -1f)
        {
            if (weapon == null || shots.Count >= maxShots) return false;
            float spread = Random.Range(-weapon.spreadDegrees, weapon.spreadDegrees);
            direction = Quaternion.Euler(0, 0, spread) * direction.normalized;

            shots.Add(new Shot
            {
                Position = from,
                Velocity = direction * weapon.projectileSpeed,
                RangeLeft = range >= 0f ? range : weapon.range,
                OverWalls = overWalls >= 0f ? overWalls : weapon.overWalls,
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
                float stepLength = step.magnitude;

                // Walls, except those it flies over just after leaving the barrel.
                bool hitWall = false;
                Vector2Int pixel = default;
                float wallAlong = 1f;
                float overWalls = shot.OverWalls;
                if (world != null && shot.Flown + stepLength > overWalls && stepLength > 1e-6f)
                {
                    var from = shot.Flown < overWalls ? shot.Position + step * ((overWalls - shot.Flown) / stepLength) : shot.Position;
                    if (world.Raycast(from, to, out pixel, out var wallPoint))
                    {
                        hitWall = true;
                        wallAlong = (wallPoint - shot.Position).magnitude / stepLength;
                    }
                }
                // A zombie in the way, if it's closer than the wall.
                if (Health.Raycast(shot.Position, to, Team.Zombie, out var zombie, out float along) && (!hitWall || along <= wallAlong))
                {
                    HitCreature(world, shot.Weapon, zombie, shot.Position + step * along, step / Mathf.Max(stepLength, 1e-6f));
                    RemoveAt(i--);
                    continue;
                }
                if (hitWall)
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
                shot.Flown += length;
                shots[i] = shot;
                Place(renderers[i], to, shot.Velocity);
            }
        }

        static void Detonate(PixelWorld world, WeaponConfig weapon, Vector2 at)
        {
            if (weapon.damage != null && world.TryMakeEvent(weapon.damage, at, out var hit))
                world.ApplyDamage(hit);
        }

        // A shot hits a zombie: it's hurt (a little blood flies), and explosive shots blow up right there.
        static void HitCreature(PixelWorld world, WeaponConfig weapon, Health target, Vector2 at, Vector2 direction)
        {
            target.Damage(weapon.hitDamage, at - direction * 0.3f);
            if (world == null) return;
            if (weapon.damage != null && weapon.damage.category == DamageCategory.Explosive) Detonate(world, weapon, at);
            else world.Burst(at, new Color32(96, 12, 14, 255), 5, 2f);
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
                r.sortingOrder = SortOrder.Projectiles;
                r.enabled = false;
                Look.MakeGlow(r); // shots glow, even at night
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
