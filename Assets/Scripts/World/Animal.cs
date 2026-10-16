using KingdomGame.Art;
using KingdomGame.Character;
using UnityEngine;

namespace KingdomGame.World
{
    public enum AnimalKind { Deer, Wolf }

    /// <summary>The pictures of one kind of animal.</summary>
    public sealed class AnimalSprites
    {
        /// <summary>[direction, frame]: walking frames 0-3, then 4 (a deer grazing, a wolf lunging).</summary>
        public Sprite[,] Frames;
        /// <summary>Lying dead, one per direction (<see cref="CharacterArt.DeadDirections"/>).</summary>
        public Sprite[] Dead;
        public Sprite Shadow;
        /// <summary>Eyes that shine in the dark (wolves), or null.</summary>
        public Sprite Eyes;
    }

    /// <summary>
    /// One deer or wolf (<see cref="Wildlife"/> makes them). A deer grazes and wanders round its herd's spot and runs
    /// from anyone close (the herd with it). A wolf prowls round its pack's spot in the dark, stalks anyone out in the
    /// dark and rushes in to bite; it won't step into firelight, and it runs off at dawn or when badly hurt.
    /// No physics: it moves itself, sliding along walls, trees and rocks like a zombie; shots find it by its <see cref="Health"/>.
    /// </summary>
    public sealed class Animal : MonoBehaviour
    {
        const float TurnSpeed = 320f;      // degrees per second
        const float StepsPerTile = 2.4f;   // walking frames per tile
        enum State { Graze, Wander, Flee, Prowl, Stalk, Bite, Leave }

        Wildlife wild;
        WildlifeConfig config;
        AnimalSprites sprites;
        Transform body, eyes;
        SpriteRenderer bodyRenderer, shadow, eyeRenderer;
        Vector2 position, target, fleeFrom, sideStep;
        float angle, stepClock, hurtFlash, thinkAt, stateUntil, biteClock, stuckFor, sideStepUntil;
        bool bit, running;
        State state;
        Health prey;

        public AnimalKind Kind { get; private set; }
        public bool IsWolf => Kind == AnimalKind.Wolf;
        /// <summary>Its herd or pack.</summary>
        public int Group { get; private set; }
        public Health Health { get; private set; }
        public Vector2 Position => position;
        /// <summary>How fast it's going, in tiles per second (hunters aim ahead of it).</summary>
        public Vector2 Velocity { get; private set; }
        public float Radius => Health != null ? Health.Radius : 0.2f;
        /// <summary>The hunter after it (one at a time).</summary>
        public Component Hunter { get; set; }
        /// <summary>No hunter could find a way to it: left alone until then.</summary>
        public float UnreachableUntil { get; set; }
        /// <summary>Going away for good (a badly hurt wolf, or the wolves at dawn).</summary>
        public bool Leaving => state == State.Leave;
        /// <summary>A wolf going for someone (stalking or biting).</summary>
        public bool Hunting => state == State.Stalk || state == State.Bite;
        /// <summary>Seconds it has been leaving.</summary>
        public float LeftFor => state == State.Leave ? Time.time - leftAt : 0f;
        float leftAt;

        internal void Setup(Wildlife owner, AnimalKind kind, AnimalSprites pictures, Vector2 at, int group)
        {
            wild = owner;
            config = owner.Config;
            Kind = kind;
            sprites = pictures;
            Group = group;
            position = at;
            transform.position = at;
            angle = Random.Range(0f, 360f);

            Health = gameObject.AddComponent<Health>();
            Health.Setup(Team.Wild, IsWolf ? config.wolfHealth : config.deerHealth, CharacterArt.AnimalRadius(IsWolf ? "Wolf" : "Deer"), true);
            Health.Hurt += OnHurt;
            Health.Died += OnDied;

            shadow = Look.AddShadow(transform, sprites.Shadow, 0.08f);
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            bodyRenderer = body.gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sortingOrder = SortOrder.Characters - 2;   // under zombies, villagers and the ruler
            if (sprites.Eyes != null)
            {
                // A wolf's eyes catch the light at night: two glints coming out of the dark.
                eyes = new GameObject("Eyes").transform;
                eyes.SetParent(body, false);
                eyes.localScale = Vector3.one * 1.25f;
                eyeRenderer = eyes.gameObject.AddComponent<SpriteRenderer>();
                eyeRenderer.sprite = sprites.Eyes;
                eyeRenderer.sortingOrder = SortOrder.Characters - 1;
                Look.MakeGlow(eyeRenderer);
            }
            state = IsWolf ? State.Prowl : State.Graze;
            target = at;
            stateUntil = Time.time + Random.Range(1f, 6f);
            thinkAt = Time.time + Random.Range(0f, 0.3f);
            Show(Vector2.zero, 0f, -1);
        }

        /// <summary>Runs from <paramref name="from"/> for a few seconds (<paramref name="alarm"/>: and tells its herd).</summary>
        public void Startle(Vector2 from, bool alarm)
        {
            if (IsWolf || Health == null || Health.Dead) return;
            bool already = state == State.Flee;
            state = State.Flee;
            fleeFrom = from;
            stateUntil = Time.time + Random.Range(3f, 5f);
            if (alarm && !already) wild.Alarm(this, from);
        }

        /// <summary>A wolf goes away (dawn, or badly hurt), away from <paramref name="from"/>.</summary>
        public void Leave(Vector2 from)
        {
            if (!IsWolf || state == State.Leave) return;
            state = State.Leave;
            leftAt = Time.time;
            fleeFrom = from;
            prey = null;
            // Hurt: it runs. At dawn: it trots off.
            running = Health != null && Health.Fraction < config.wolfFleeBelow;
        }

        void Update()
        {
            if (Health == null || Health.Dead || wild == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            hurtFlash = Mathf.Max(0f, hurtFlash - dt);
            var before = position;
            if (Time.time >= thinkAt)
            {
                thinkAt = Time.time + Random.Range(0.2f, 0.35f);
                if (IsWolf) ThinkWolf();
                else ThinkDeer();
            }

            Vector2 want = Vector2.zero;
            float speed = 0f;
            int pose = -1;   // a frame to hold instead of walking
            switch (state)
            {
                case State.Graze:
                    if (!IsWolf) pose = 4;   // head down at the grass
                    break;
                case State.Wander:
                case State.Prowl:
                    want = target - position;
                    if (want.sqrMagnitude < 0.04f) want = Vector2.zero;
                    speed = IsWolf ? config.wolfWalkSpeed : config.deerWalkSpeed;
                    break;
                case State.Flee:
                    want = position - fleeFrom;
                    speed = config.deerRunSpeed;
                    break;
                case State.Leave:
                    want = position - fleeFrom;
                    speed = running ? config.wolfRunSpeed : config.wolfWalkSpeed * 1.3f;
                    break;
                case State.Stalk:
                {
                    if (prey == null || prey.Dead) { state = State.Prowl; break; }
                    want = prey.Position - position;
                    float gap = want.magnitude - prey.Radius - Radius;
                    if (gap <= config.biteReach)
                    {
                        state = State.Bite;
                        biteClock = 0f;
                        bit = false;
                        want = Vector2.zero;
                        break;
                    }
                    speed = gap > config.wolfRushDistance ? config.wolfWalkSpeed * 1.3f : config.wolfRunSpeed;
                    break;
                }
                case State.Bite:
                    pose = Bite(dt);
                    break;
            }
            if (speed > 0f && want.sqrMagnitude > 1e-6f)
            {
                speed *= Seasons.Current != null ? Seasons.Current.MoveFactor : 1f;   // deep snow
                var dir = Time.time < sideStepUntil ? sideStep : want.normalized;
                Move(dir * speed * dt);
                // Pressed against a tree or a wall: step aside for a moment.
                float moved = (position - before).magnitude;
                stuckFor = moved < speed * dt * 0.25f ? stuckFor + dt : 0f;
                if (stuckFor > 0.4f)
                {
                    stuckFor = 0f;
                    float turn = (Random.value < 0.5f ? 1f : -1f) * Random.Range(60f, 110f);
                    sideStep = Quaternion.Euler(0f, 0f, turn) * want.normalized;
                    sideStepUntil = Time.time + Random.Range(0.5f, 1f);
                }
            }
            Velocity = (position - before) / dt;
            Show(Velocity, dt, pose);
        }

        // ------------------------------------------------------------- deer --

        void ThinkDeer()
        {
            bool danger = wild.Danger(this, out var from);
            if (state == State.Flee)
            {
                // Still too close: keep running, from whatever is nearest now.
                if (danger)
                {
                    fleeFrom = from;
                    if (Time.time >= stateUntil) stateUntil = Time.time + 1.5f;
                    return;
                }
                if (Time.time < stateUntil) return;
                wild.Regroup(Group, position);   // the herd gathers where they stopped
                state = State.Wander;
                target = wild.SpotNear(wild.HomeOf(Group), 4f, false, position);
                stateUntil = Time.time + Random.Range(4f, 9f);
                return;
            }
            // Someone (or something) close: run, and the herd with it.
            if (danger)
            {
                Startle(from, true);
                return;
            }
            if (Time.time < stateUntil) return;
            if (state == State.Graze)
            {
                state = State.Wander;
                target = wild.SpotNear(wild.HomeOf(Group), 4f, false, position);
                stateUntil = Time.time + Random.Range(4f, 9f);
            }
            else
            {
                state = State.Graze;
                stateUntil = Time.time + Random.Range(3f, 9f);
            }
        }

        // ------------------------------------------------------------ wolves --

        void ThinkWolf()
        {
            if (state == State.Bite || state == State.Leave) return;
            if (!wild.Night || Health.Fraction < config.wolfFleeBelow)
            {
                Leave(wild.Focus);
                return;
            }
            // Anyone out in the dark close enough: after them.
            prey = wild.PreyFor(this);
            if (prey != null)
            {
                state = State.Stalk;
                return;
            }
            if (state != State.Prowl || Time.time >= stateUntil || (target - position).sqrMagnitude < 0.25f)
            {
                state = State.Prowl;
                target = wild.SpotNear(wild.HomeOf(Group), 5f, true, position);
                stateUntil = Time.time + Random.Range(4f, 8f);
            }
        }

        // A bite: crouching, the lunge (half way: it lands if they're still in reach), and back. Returns the frame to show.
        int Bite(float dt)
        {
            if (prey == null || prey.Dead)
            {
                state = State.Prowl;
                return -1;
            }
            biteClock += dt;
            float t = biteClock / config.biteSeconds;
            var to = prey.Position - position;
            if (to.sqrMagnitude > 1e-6f) TurnTo(to, dt);
            if (!bit && t >= 0.5f)
            {
                bit = true;
                if (to.magnitude - prey.Radius - Radius <= config.biteReach + 0.15f)
                {
                    prey.Damage(config.biteDamage, position);
                    Audio.Sounds.Play(Audio.Sound.Snarl, position);
                }
            }
            if (t >= 1f) state = State.Stalk;
            return t >= 0.3f && t < 0.75f ? 4 : 0;
        }

        // ---------------------------------------------------------- moving --

        // Moves, sliding along what's in the way; a wolf won't step into firelight (out of it, if it's in it).
        void Move(Vector2 delta)
        {
            const float box = 0.14f;
            if (!wild.Fits(position, box))
            {
                // Something was built where it stands: out to the nearest open spot.
                position = Vector2.MoveTowards(position, wild.NearestOpenSpot(position), Mathf.Max(delta.magnitude, 2f * Time.deltaTime));
                return;
            }
            bool fearsLight = IsWolf && state != State.Leave && !wild.Lit(position);
            var p = position;
            var x = new Vector2(p.x + delta.x, p.y);
            if (wild.Fits(x, box) && !(fearsLight && wild.Lit(x))) p = x;
            var y = new Vector2(p.x, p.y + delta.y);
            if (wild.Fits(y, box) && !(fearsLight && wild.Lit(y))) p = y;
            position = p;
        }

        void TurnTo(Vector2 look, float dt) =>
            angle = Mathf.MoveTowardsAngle(angle, Mathf.Atan2(look.y, look.x) * Mathf.Rad2Deg, TurnSpeed * Mathf.Max(dt, 0.02f));

        // The picture for the way it faces and what it's doing.
        void Show(Vector2 velocity, float dt, int pose)
        {
            int frame = 0;
            if (pose >= 0) frame = pose;
            else if (velocity.sqrMagnitude > 0.0025f)
            {
                stepClock += dt * velocity.magnitude * StepsPerTile;
                frame = Mathf.FloorToInt(stepClock) % CharacterArt.WalkFrames;
                TurnTo(velocity, dt);
            }
            int direction = RulerLook.Direction(angle, sprites.Frames.GetLength(0));
            bodyRenderer.sprite = sprites.Frames[direction, frame];
            bodyRenderer.color = hurtFlash > 0f ? new Color(1f, 0.55f, 0.5f) : Color.white;
            if (eyes != null)
            {
                // At the front of its head (further forward in a lunge); they only shine in the dark.
                eyes.localPosition = new Vector3(frame == 4 ? 0.49f : 0.39f, 0f, 0f);
                eyeRenderer.color = new Color(1f, 0.78f, 0.42f, 0.9f * Mathf.Clamp01(FireLight.Darkness));
            }
            var turn = Quaternion.Euler(0f, 0f, angle);
            body.localRotation = turn;
            if (shadow != null) shadow.transform.localRotation = turn;
            transform.position = position;
        }

        void OnHurt(Health h, float amount, Vector2 from)
        {
            hurtFlash = 0.12f;
            var remains = Zombies.Remains.Current;
            if (remains != null && Random.value < 0.4f) remains.Blood(position + Random.insideUnitCircle * 0.2f, 0.5f);
            if (!IsWolf) Startle(from, true);
            else if (h.Fraction < config.wolfFleeBelow) Leave(from);
        }

        void OnDied(Health h)
        {
            wild.Died(this, angle, sprites);
            Destroy(gameObject);
        }
    }
}
