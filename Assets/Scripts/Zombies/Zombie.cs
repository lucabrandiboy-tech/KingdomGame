using KingdomGame.Art;
using KingdomGame.Character;
using UnityEngine;

namespace KingdomGame.Zombies
{
    /// <summary>The pictures one kind of zombie uses.</summary>
    public sealed class ZombieSprites
    {
        /// <summary>[direction, frame]: walking frames 0-3, 4 winding up a blow, 5 striking.</summary>
        public Sprite[,] Frames;
        public Sprite Shadow;
        /// <summary>Lying dead, one per direction (<see cref="CharacterArt.DeadDirections"/>).</summary>
        public Sprite[] Dead;
    }

    /// <summary>
    /// One zombie. It follows the <see cref="ZombieDirector"/>'s flow field towards
    /// the nearest person, smashes whatever is in the way when that's quicker than
    /// walking round (doors, furniture, walls), and claws at anyone within reach.
    /// It has no physics body: the director moves it (<see cref="Tick"/>), it slides
    /// along walls by itself, and shots find it through its <see cref="Health"/>.
    /// </summary>
    public sealed class Zombie : MonoBehaviour
    {
        /// <summary>Goes straight for someone it can see this close (tiles), instead of following the field.</summary>
        const float ChaseRange = 3f;
        const float TurnSpeed = 300f;      // degrees per second
        const float StepsPerTile = 3.2f;   // walking frames per tile walked
        /// <summary>A zombie that can't find a way to anyone for this long wanders off (so a wave can't get stuck).</summary>
        const float LostSeconds = 25f;

        ZombieDirector director;
        ZombieSprites sprites;
        Transform body;
        SpriteRenderer bodyRenderer, shadow;
        Vector2 position, offset;
        float angle, stepClock, hurtFlash, thinkAt;
        float attackClock = -1f;           // seconds into the current blow, or -1
        float lostFor;                     // seconds without a way to anyone
        bool struck;
        Health prey, victim;               // who it's after; who it's hitting
        Vector2Int smashing;               // the cell it's hitting when victim is null

        public ZombieConfig Config { get; private set; }
        public Health Health { get; private set; }
        public Vector2 Position => position;
        public float Radius => Config.radius;
        /// <summary>The push from the zombies crowding round it, in tiles per second (set by the director every frame).</summary>
        internal Vector2 Crowding;

        /// <summary>Sets it up at <paramref name="at"/>, with its health times <paramref name="toughness"/>.</summary>
        public void Setup(ZombieDirector owner, ZombieConfig config, ZombieSprites pictures, float toughness, Vector2 at)
        {
            director = owner;
            Config = config;
            sprites = pictures;
            position = at;
            transform.position = at;
            angle = Random.Range(0f, 360f);
            // Each zombie walks a slightly different line through every cell, so a crowd spreads out.
            offset = new Vector2(Random.Range(-0.22f, 0.22f), Random.Range(-0.22f, 0.22f));

            Health = gameObject.AddComponent<Health>();
            Health.Setup(Team.Zombie, config.health * Mathf.Max(0.1f, toughness), config.radius, true);
            Health.Hurt += OnHurt;
            Health.Died += OnDied;

            shadow = Look.AddShadow(transform, sprites.Shadow, 0.1f);
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            bodyRenderer = body.gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sortingOrder = SortOrder.Characters - 1;   // under the ruler and villagers
            Show(Vector2.zero, 0f);
        }

        /// <summary>One frame of thinking and moving (called by the director).</summary>
        public void Tick(float dt)
        {
            if (Health == null || Health.Dead) return;
            hurtFlash = Mathf.Max(0f, hurtFlash - dt);
            var before = position;

            if (attackClock >= 0f)
            {
                Attack(dt);
                Show(Vector2.zero, dt);
                return;
            }

            // Who to go for: someone close by that it can see, re-checked a few times a second.
            if (Time.time >= thinkAt)
            {
                thinkAt = Time.time + Random.Range(0.2f, 0.35f);
                prey = director.FindPrey(position, ChaseRange);
            }
            if (prey != null && prey.Dead) prey = null;

            Vector2 want = Vector2.zero;
            bool lost = false;
            if (prey != null)
            {
                if (director.Gap(prey, position) - Radius <= Config.reach)
                {
                    StartAttack(prey, default);
                    Show(Vector2.zero, dt);
                    return;
                }
                want = director.AimPoint(prey, position) - position;
            }
            else if (director.NextStep(position, out var next, out bool blocked))
            {
                var middle = (Vector2)next + new Vector2(0.5f, 0.5f);
                if (blocked)
                {
                    // Something to break in the way: go up to it, then hit it.
                    if (DistanceTo(next) - Radius <= Config.reach)
                    {
                        StartAttack(null, next);
                        Show(Vector2.zero, dt);
                        return;
                    }
                    want = middle - position;
                }
                else
                {
                    want = middle + offset - position;
                }
            }
            else
            {
                lost = true;
            }
            lostFor = lost ? lostFor + dt : 0f;
            if (lostFor > LostSeconds)
            {
                director.Vanish(this);
                return;
            }

            float speed = Config.moveSpeed * director.SpeedFactor;
            var move = (want.sqrMagnitude > 1e-6f ? want.normalized * speed : Vector2.zero) + Crowding;
            Move(move * dt);
            Show((position - before) / Mathf.Max(dt, 1e-5f), dt);
        }

        void StartAttack(Health target, Vector2Int cell)
        {
            victim = target;
            smashing = cell;
            attackClock = 0f;
            struck = false;
        }

        // A blow: winding up for the first half, the strike lands half way, then a moment to recover.
        void Attack(float dt)
        {
            attackClock += dt;
            float length = Config.attackSeconds;
            if (!struck && attackClock >= length * 0.5f)
            {
                struck = true;
                float damage = director.DamageFactor;
                if (victim != null)
                {
                    // It only lands if they're still within reach (step back in time and it misses).
                    if (!victim.Dead && director.Gap(victim, position) - Radius <= Config.reach + 0.15f)
                        victim.Damage(Config.damage * damage, position);
                }
                else
                {
                    director.HitCell(smashing, this);
                }
            }
            if (attackClock >= length) attackClock = -1f;
        }

        // Moves, sliding along walls: each direction on its own, only where its body fits.
        void Move(Vector2 delta)
        {
            float box = Radius * 0.8f;
            if (!director.Fits(position, box))
            {
                // Something was built on top of it: let it walk out.
                position += delta;
                return;
            }
            var p = position;
            var x = new Vector2(p.x + delta.x, p.y);
            if (director.Fits(x, box)) p = x;
            var y = new Vector2(p.x, p.y + delta.y);
            if (director.Fits(y, box)) p = y;
            position = p;
        }

        float DistanceTo(Vector2Int cell)
        {
            float dx = Mathf.Max(Mathf.Max(cell.x - position.x, 0f), position.x - (cell.x + 1));
            float dy = Mathf.Max(Mathf.Max(cell.y - position.y, 0f), position.y - (cell.y + 1));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // Picks the picture for the direction it faces and what it's doing.
        void Show(Vector2 velocity, float dt)
        {
            int frame = 0;
            Vector2 look = Vector2.zero;
            if (attackClock >= 0f)
            {
                float t = attackClock / Config.attackSeconds;
                frame = t < 0.5f ? 4 : t < 0.75f ? 5 : 0;
                var target = victim != null ? director.AimPoint(victim, position) : (Vector2)smashing + new Vector2(0.5f, 0.5f);
                look = target - position;
            }
            else if (velocity.sqrMagnitude > 0.0025f)
            {
                stepClock += dt * velocity.magnitude * StepsPerTile;
                frame = Mathf.FloorToInt(stepClock) % CharacterArt.WalkFrames;
                look = velocity;
            }
            if (look.sqrMagnitude > 1e-6f)
                angle = Mathf.MoveTowardsAngle(angle, Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg, TurnSpeed * Mathf.Max(dt, 0.02f));

            int direction = RulerLook.Direction(angle, sprites.Frames.GetLength(0));
            bodyRenderer.sprite = sprites.Frames[direction, frame];
            bodyRenderer.color = hurtFlash > 0f ? new Color(1f, 0.55f, 0.5f) : Color.white;
            var turn = Quaternion.Euler(0f, 0f, angle);
            body.localRotation = turn;
            if (shadow != null) shadow.transform.localRotation = turn;
            transform.position = position;
        }

        void OnHurt(Health h, float amount, Vector2 from)
        {
            hurtFlash = 0.12f;
            var remains = Remains.Current;
            if (remains != null && Random.value < 0.35f) remains.Blood(position + Random.insideUnitCircle * 0.2f, 0.6f);
        }

        void OnDied(Health h)
        {
            director.Died(this, angle, sprites);
            Destroy(gameObject);
        }
    }
}
