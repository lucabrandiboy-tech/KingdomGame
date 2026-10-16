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
        /// <summary>Eyes that glow faintly in the dark, and where they are in each frame (tiles, facing right).</summary>
        public Sprite Eyes;
        public Vector2[] EyesAt;
        /// <summary>How big this kind is drawn compared to a walker (the eyes are drawn at a walker's size).</summary>
        public float Scale = 1f;
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
        Transform body, eyes;
        SpriteRenderer bodyRenderer, shadow;
        Vector2 position, offset;
        float angle, stepClock, hurtFlash, thinkAt;
        float attackClock = -1f;           // seconds into the current blow, or -1
        float lostFor;                     // seconds without a way to anyone
        float stuckFor, chaseBlockedUntil; // going straight for someone isn't getting anywhere: follow the field
        bool struck;
        Health prey, victim;               // who it's after; who it's hitting (or spitting or screaming at)
        enum Blow { Person, Cell, Spit, Scream, Burst }
        Blow blow;                         // what the current blow is
        Vector2 spitTo;                    // where its acid will land
        float abilityAt;                   // when it can next spit or scream (or look for someone to)
        float boostUntil, boostSpeed = 1f; // sped up by a screamer's scream
        float groanAt;                     // when it next groans
        float heldUntil;                   // caught in a trap until then
        float slowKeep = 1f;               // share of its speed it keeps (stakes, wire), this frame
        int slowFrame = -10;
        Vector2Int smashing;
        Color skin = Color.white;           // its tint (a burning one is charred)

        Vector2 campAt, wanderTo;          // camping (round a base, or besieging the kingdom): where, and where it's wandering to
        float campRadius, sight, wanderAt;

        /// <summary>Camping: it stays round a spot instead of making for the kingdom, going only for people it sees.</summary>
        public bool Camping => campRadius > 0f;
        /// <summary>Camping at its zombie base (far from the kingdom), not besieging it.</summary>
        public bool AtBase { get; private set; }

        /// <summary>
        /// Stay round <paramref name="at"/> (within <paramref name="radius"/> tiles), going for anyone it can see within
        /// <paramref name="seeing"/> tiles (and only if it can walk straight to them: it doesn't break in).
        /// </summary>
        public void Camp(Vector2 at, float radius, float seeing, bool atBase = false)
        {
            AtBase = atBase;
            campAt = at;
            campRadius = Mathf.Max(1f, radius);
            sight = Mathf.Max(ChaseRange, seeing);
            wanderAt = 0f;
        }

        /// <summary>Stops camping: it makes for the kingdom like the rest.</summary>
        public void StopCamping()
        {
            campRadius = 0f;
            AtBase = false;
        }

        public ZombieConfig Config { get; private set; }
        public Health Health { get; private set; }
        public Vector2 Position => position;
        public float Radius => Config.radius;
        /// <summary>Caught in a trap: it can't move or hit anything.</summary>
        public bool Held => Time.time < heldUntil;
        /// <summary>It blew itself up (a bloater reaching a wall or someone): not a kill, and no loot.</summary>
        public bool BurstItself { get; private set; }

        /// <summary>A screamer's scream: <paramref name="factor"/> times as fast for <paramref name="seconds"/>.</summary>
        public void SpeedUp(float factor, float seconds)
        {
            boostSpeed = Mathf.Max(1f, factor);
            boostUntil = Mathf.Max(boostUntil, Time.time + seconds);
        }

        /// <summary>Caught in a trap for <paramref name="seconds"/>: no moving, no hitting (a blow it was winding up is lost).</summary>
        public void Hold(float seconds)
        {
            heldUntil = Mathf.Max(heldUntil, Time.time + seconds);
            attackClock = -1f;
        }

        /// <summary>Slowed this frame to <paramref name="keep"/> of its speed (the slowest of everything it's in counts).</summary>
        public void Slow(float keep)
        {
            if (slowFrame != Time.frameCount)
            {
                slowFrame = Time.frameCount;
                slowKeep = 1f;
            }
            slowKeep = Mathf.Min(slowKeep, Mathf.Clamp01(keep));
        }

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
            Health.Armor = config.armor;
            Health.Hurt += OnHurt;
            Health.Died += OnDied;
            abilityAt = Time.time + Random.Range(0.5f, 2f);   // not all at once
            groanAt = Time.time + Random.Range(1f, 10f);

            shadow = Look.AddShadow(transform, sprites.Shadow, 0.1f);
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            bodyRenderer = body.gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sortingOrder = SortOrder.Characters - 1;   // under the ruler and villagers
            if (sprites.Eyes != null)
            {
                // Eyes glinting in the dark, so you can see them coming at night.
                eyes = new GameObject("Eyes").transform;
                eyes.SetParent(body, false);
                eyes.localScale = Vector3.one * sprites.Scale;
                var eyeRenderer = eyes.gameObject.AddComponent<SpriteRenderer>();
                eyeRenderer.sprite = sprites.Eyes;
                eyeRenderer.sortingOrder = SortOrder.Characters;
                eyeRenderer.color = config.boss ? new Color(1f, 0.42f, 0.32f, 1f) : new Color(1f, 1f, 1f, 0.85f);   // a boss's burn red
                Look.MakeGlow(eyeRenderer);
            }
            if (config.ability == ZombieAbility.Burn)
            {
                // On fire: charred, with flames on it (and their light).
                skin = new Color(0.62f, 0.5f, 0.44f);
                var fire = ArtSprites.Get(ArtKeys.Flame);
                if (fire != null)
                {
                    var flames = new GameObject("Flames").AddComponent<Building.Flames>();
                    flames.transform.SetParent(body, false);
                    flames.Setup(fire, new[] { new Vector2(0.05f, 0.1f), new Vector2(-0.08f, -0.08f), new Vector2(0.12f, -0.06f) }, 0.9f, 0.55f, SortOrder.Characters + 1);
                }
            }
            Show(Vector2.zero, 0f);
        }

        /// <summary>One frame of thinking and moving (called by the director).</summary>
        public void Tick(float dt)
        {
            if (Health == null || Health.Dead) return;
            hurtFlash = Mathf.Max(0f, hurtFlash - dt);
            if (Time.time >= groanAt)
            {
                groanAt = Time.time + Random.Range(6f, 16f);
                Audio.Sounds.Play(Audio.Sound.Groan, position, 0.8f);
            }
            var before = position;
            if (Held)
            {
                Show(Vector2.zero, dt);
                return;
            }

            if (attackClock >= 0f)
            {
                Attack(dt);
                Show(Vector2.zero, dt);
                return;
            }

            // Spitters and screamers: now and then, at someone they can see further off.
            if ((Config.ability == ZombieAbility.Spit || Config.ability == ZombieAbility.Scream) && Time.time >= abilityAt && UseAbility())
            {
                Show(Vector2.zero, dt);
                return;
            }

            // Who to go for: someone close by that it can see, re-checked a few times a second.
            if (Time.time >= thinkAt)
            {
                thinkAt = Time.time + Random.Range(0.2f, 0.35f);
                prey = director.FindPrey(position, Camping ? sight : ChaseRange);
            }
            if (prey != null && prey.Dead) prey = null;

            Vector2 want = Vector2.zero;
            bool lost = false;
            if (prey != null && director.Gap(prey, position) - Radius <= Config.reach)
            {
                StartAttack(prey, default);
                Show(Vector2.zero, dt);
                return;
            }
            if (prey != null && Time.time >= chaseBlockedUntil)
            {
                want = director.AimPoint(prey, position) - position;
                // Furniture or a door right in the way (a table, a barricade): smash through it.
                if (want.sqrMagnitude > 1e-4f)
                {
                    var ahead = director.CellOf(position + want.normalized * (Radius + 0.25f));
                    if (!director.Walkable(ahead) && director.BreakableThing(ahead) && DistanceTo(ahead) - Radius <= Config.reach)
                    {
                        StartAttack(null, ahead);
                        Show(Vector2.zero, dt);
                        return;
                    }
                }
            }
            else if (Config.ability == ZombieAbility.Ram && Time.time >= chaseBlockedUntil && !Camping && director.RamGoal(position, out var goal))
            {
                // A rammer: straight for the throne, smashing whatever is in the way (a tree or a house: round it, for a moment).
                want = goal - position;
                if (director.SmashAhead(position, want.normalized, Radius * 0.8f, out var wall, out bool stopped))
                {
                    if (DistanceTo(wall) - Radius <= Config.reach)
                    {
                        StartAttack(null, wall);
                        Show(Vector2.zero, dt);
                        return;
                    }
                }
                else if (stopped) chaseBlockedUntil = Time.time + 2.5f;
            }
            else if (Camping)
            {
                // Milling about its spot: a new place to shuffle to now and then.
                if (Time.time >= wanderAt || (wanderTo - position).sqrMagnitude < 0.25f)
                {
                    wanderAt = Time.time + Random.Range(3f, 7f);
                    wanderTo = campAt + Random.insideUnitCircle * campRadius;
                }
                want = (wanderTo - position) * 0.5f;
                if (want.sqrMagnitude > 0.25f) want = want.normalized * 0.5f;
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

            // Slowed by what it walked through last frame (the defences run after the zombies move).
            float slow = Time.frameCount - slowFrame <= 1 ? slowKeep : 1f;
            float speed = Config.moveSpeed * director.SpeedFactor * slow * (Time.time < boostUntil ? boostSpeed : 1f);
            var move = (want.sqrMagnitude > 1e-6f ? want.normalized * speed : Vector2.zero) + Crowding;
            Move(move * dt);
            // Hardly moving while trying to walk (pressed into a corner, stuck in a crowd): follow the field for a while.
            bool trying = want.sqrMagnitude > 0.01f;
            stuckFor = trying && (position - before).sqrMagnitude < speed * dt * speed * dt * 0.04f ? stuckFor + dt : 0f;
            if (stuckFor > 0.6f)
            {
                stuckFor = 0f;
                chaseBlockedUntil = Time.time + 2f;
            }
            Show((position - before) / Mathf.Max(dt, 1e-5f), dt);
        }

        // Goes for someone (target) or what's in a cell. A bloater doesn't claw: it bursts against it.
        void StartAttack(Health target, Vector2Int cell)
        {
            smashing = cell;
            StartBlow(Config.ability == ZombieAbility.Burst ? Blow.Burst : target != null ? Blow.Person : Blow.Cell, target);
        }

        void StartBlow(Blow kind, Health target)
        {
            blow = kind;
            victim = target;
            attackClock = 0f;
            struck = false;
        }

        // Spits at or screams at someone it can see, if there's anyone: returns whether it started.
        bool UseAbility()
        {
            abilityAt = Time.time + 0.4f;   // nobody about: look again in a moment
            bool spit = Config.ability == ZombieAbility.Spit;
            var target = spit ? director.FindSpitTarget(position, Config.abilityRange) : director.FindPrey(position, Config.abilityRange);
            if (target == null) return false;
            // Close enough to claw them: no need to spit.
            if (spit && !target.OutOfReach && director.Gap(target, position) - Radius <= Config.reach) return false;
            abilityAt = Time.time + Config.abilitySeconds;
            spitTo = director.AimPoint(target, position);
            StartBlow(spit ? Blow.Spit : Blow.Scream, target);
            return true;
        }

        // A blow (or a spit, a scream, a burst): winding up for the first half, it lands half way, then a moment to recover.
        void Attack(float dt)
        {
            // Someone else got them first: look for the next one.
            if (blow == Blow.Person && !struck && (victim == null || victim.Dead))
            {
                attackClock = -1f;
                return;
            }
            attackClock += dt;
            float length = Config.attackSeconds;
            if (!struck && attackClock >= length * 0.5f)
            {
                struck = true;
                float damage = director.DamageFactor;
                switch (blow)
                {
                    case Blow.Person:
                        // It only lands if they're still within reach (step back in time and it misses).
                        if (director.Gap(victim, position) - Radius <= Config.reach + 0.15f)
                        {
                            victim.Damage(Config.damage * damage, position);
                            Audio.Sounds.Play(Audio.Sound.Claw, position);
                        }
                        break;
                    case Blow.Cell:
                        director.HitCell(smashing, this);
                        break;
                    case Blow.Spit:
                        director.Spit(position, spitTo, Config.spitDamage * damage);
                        break;
                    case Blow.Scream:
                        director.Scream(position, Config);
                        break;
                    case Blow.Burst:
                        // Into the wall or door it reached, then it bursts (the director sets off the blast when it dies).
                        if (victim == null) director.HitCell(smashing, this, Config.burstWallDamage);
                        BurstItself = true;
                        Health.Damage(1e9f, position);
                        return;
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
                // Something was built where it stands: step out to the nearest open cell (never through it).
                var cell = director.CellOf(position);
                var to = director.Walkable(cell) ? (Vector2)cell + new Vector2(0.5f, 0.5f) : director.NearestOpenSpot(position);
                position = Vector2.MoveTowards(position, to, Mathf.Max(delta.magnitude, Config.moveSpeed * Time.deltaTime));
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
                if (blow == Blow.Cell || (blow == Blow.Burst && victim == null)) look = (Vector2)smashing + new Vector2(0.5f, 0.5f) - position;
                else if (victim != null && !victim.Dead) look = director.AimPoint(victim, position) - position;
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
            if (eyes != null) eyes.localPosition = sprites.EyesAt[frame] * sprites.Scale;
            bodyRenderer.color = hurtFlash > 0f ? new Color(1f, 0.55f, 0.5f)
                : attackClock >= 0f && blow == Blow.Burst ? Color.Lerp(Color.white, new Color(1f, 1f, 0.45f), Mathf.PingPong(attackClock * 6f, 1f))   // swelling up to burst
                : Time.time < boostUntil ? new Color(1f, 0.8f, 0.72f) * skin   // worked up by a scream
                : skin;
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
