using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame
{
    /// <summary>Who is on whose side. Zombies attack the Friendly team; your shots only hit zombies.</summary>
    public enum Team
    {
        Friendly,
        Zombie,
    }

    /// <summary>
    /// Hit points for anything that can be hurt or broken: the ruler, villagers,
    /// zombies (creatures), and doors and furniture (structures). Walls don't use
    /// this: they break pixel by pixel. Keeps lists of everything alive, so shots
    /// and explosions can find what they hit.
    /// </summary>
    public sealed class Health : MonoBehaviour
    {
        static readonly List<Health> creatures = new List<Health>();
        static readonly List<Health> structures = new List<Health>();

        /// <summary>Every living creature (the ruler, villagers, zombies).</summary>
        public static IReadOnlyList<Health> Creatures => creatures;
        /// <summary>Every breakable door and piece of furniture.</summary>
        public static IReadOnlyList<Health> Structures => structures;

        bool registered;

        public Team Team { get; private set; }
        public float Max { get; private set; }
        public float Current { get; private set; }
        /// <summary>Size of the body, in tiles from its middle (for shots and blasts).</summary>
        public float Radius { get; private set; }
        public bool IsCreature { get; private set; }
        /// <summary>Up on a wall (or somewhere else zombies can't climb): zombies don't go for it and doors don't open for it.</summary>
        public bool OutOfReach { get; set; }
        /// <summary>Taken off every hit (armored zombies); a hit always does at least a fifth of its damage.</summary>
        public float Armor { get; set; }
        public bool Dead => Current <= 0f;
        public float Fraction => Max > 0f ? Current / Max : 0f;
        /// <summary>When it was last hurt (Time.time), for healing only after a while.</summary>
        public float LastHurt { get; private set; } = -999f;
        public Vector2 Position => transform.position;

        /// <summary>It was hurt: how much, and where the hit came from.</summary>
        public event Action<Health, float, Vector2> Hurt;
        public event Action<Health> Died;

        /// <summary>Sets it up full. Call once, right after AddComponent.</summary>
        public void Setup(Team team, float max, float radius, bool creature)
        {
            Team = team;
            Max = Mathf.Max(1f, max);
            Current = Max;
            Radius = radius;
            IsCreature = creature;
            Register();
        }

        /// <summary>Takes damage. Returns true if this hit killed it.</summary>
        public bool Damage(float amount, Vector2 from)
        {
            if (Dead || amount <= 0f || Max <= 0f) return false;
            if (Armor > 0f) amount = Mathf.Max(amount * 0.2f, amount - Armor);
            Current = Mathf.Max(0f, Current - amount);
            LastHurt = Time.time;
            Hurt?.Invoke(this, amount, from);
            if (Current > 0f) return false;
            Unregister();
            Died?.Invoke(this);
            return true;
        }

        public void Heal(float amount)
        {
            if (Dead || amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
        }

        /// <summary>Back to full health (respawning).</summary>
        public void Revive()
        {
            Current = Max;
            Register();
        }

        /// <summary>Sets what's left (loading a saved game): at least 1, at most <see cref="Max"/>.</summary>
        public void SetCurrent(float value)
        {
            if (Max <= 0f) return;
            Current = Mathf.Clamp(value, 1f, Max);
        }

        /// <summary>A new maximum (tougher zombies in later waves), filling it up.</summary>
        public void SetMax(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
        }

        void OnEnable()
        {
            if (Max > 0f && !Dead) Register();
        }

        void OnDisable() => Unregister();

        void Register()
        {
            if (registered || Max <= 0f) return;
            registered = true;
            (IsCreature ? creatures : structures).Add(this);
        }

        void Unregister()
        {
            if (!registered) return;
            registered = false;
            creatures.Remove(this);
            structures.Remove(this);
        }

        // ----------------------------------------------------------- queries --

        /// <summary>The closest living creature of <paramref name="team"/> within <paramref name="maxDistance"/> tiles, or null.</summary>
        public static Health Nearest(Vector2 at, Team team, float maxDistance)
        {
            Health best = null;
            float bestDistance = maxDistance * maxDistance;
            foreach (var h in creatures)
            {
                if (h.Team != team || h.Dead) continue;
                float d = (h.Position - at).sqrMagnitude;
                if (d < bestDistance) { best = h; bestDistance = d; }
            }
            return best;
        }

        /// <summary>
        /// The first creature of <paramref name="team"/> that the straight line from
        /// <paramref name="from"/> to <paramref name="to"/> passes through, and how far
        /// along the line it is hit (0..1).
        /// </summary>
        public static bool Raycast(Vector2 from, Vector2 to, Team team, out Health hit, out float along)
        {
            hit = null;
            along = 1f;
            var line = to - from;
            float length2 = line.sqrMagnitude;
            if (length2 < 1e-10f) return false;
            foreach (var h in creatures)
            {
                if (h.Team != team || h.Dead) continue;
                // Where the line passes closest to the middle, and whether that's inside the body.
                var toCenter = h.Position - from;
                float t = Mathf.Clamp01(Vector2.Dot(toCenter, line) / length2);
                var closest = from + line * t;
                float miss2 = (h.Position - closest).sqrMagnitude, r2 = h.Radius * h.Radius;
                if (miss2 > r2) continue;
                // Step back to where the line enters the body.
                float back = Mathf.Sqrt(Mathf.Max(0f, r2 - miss2) / length2);
                float enter = Mathf.Max(0f, t - back);
                if (enter < along)
                {
                    along = enter;
                    hit = h;
                }
            }
            return hit != null;
        }

        /// <summary>
        /// An explosion: everything within <paramref name="radius"/> tiles takes damage,
        /// <paramref name="damage"/> in the middle falling to a quarter at the edge: zombies
        /// all of it, the friendly side (the ruler, villagers, doors, furniture, storehouses)
        /// <paramref name="friendlyShare"/> of it.
        /// </summary>
        public static void Blast(Vector2 at, float radius, float damage, float friendlyShare = 1f)
        {
            if (radius <= 0f || damage <= 0f) return;
            // Copy first: dying removes things from the lists.
            var hit = new List<Health>();
            foreach (var h in creatures) if (InBlast(h, at, radius)) hit.Add(h);
            foreach (var h in structures) if (InBlast(h, at, radius)) hit.Add(h);
            foreach (var h in hit)
            {
                float distance = Mathf.Max(0f, Vector2.Distance(h.Position, at) - h.Radius);
                float falloff = Mathf.Lerp(1f, 0.25f, Mathf.Clamp01(distance / radius));
                h.Damage(damage * falloff * (h.Team == Team.Friendly ? Mathf.Clamp01(friendlyShare) : 1f), at);
            }
        }

        static bool InBlast(Health h, Vector2 at, float radius) =>
            !h.Dead && (h.Position - at).sqrMagnitude <= (radius + h.Radius) * (radius + h.Radius);
    }
}
