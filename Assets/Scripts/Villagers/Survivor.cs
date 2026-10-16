using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Zombies;
using UnityEngine;

namespace KingdomGame.Villagers
{
    /// <summary>
    /// Someone who has come to the kingdom asking to join (<see cref="Survivors"/> sends them): they walk up to the
    /// throne, wait there for an answer, and go away again if they're turned away or kept waiting too long. Zombies and
    /// wolves can kill them on the way. Some have been bitten and are hiding it.
    /// </summary>
    public sealed class Survivor : MonoBehaviour
    {
        enum State { Coming, Waiting, Leaving }

        const float Speed = 2.2f, StepsPerSecond = 7f, FadeSeconds = 1.5f;

        Survivors owner;
        Kingdom kingdom;
        VillagerSprites sprites;
        Transform body;
        SpriteRenderer bodyRenderer, shadow;
        readonly List<Vector2> path = new List<Vector2>();
        int pathIndex;
        State state;
        float waitUntil, patience, stepClock, angle, gone = -1f;
        Vector2 cameFrom, facing = Vector2.down;

        public string Name { get; private set; }
        /// <summary>What they did before (the job they take if they join).</summary>
        public Job Trade { get; private set; }
        /// <summary>Bitten: taken in, they turn into a zombie a few minutes later.</summary>
        public bool Infected { get; private set; }
        /// <summary>What you notice about them (sometimes a sign of a bite).</summary>
        public string Remark { get; private set; }
        public Health Health { get; private set; }
        /// <summary>At the throne waiting for an answer.</summary>
        public bool Waiting => state == State.Waiting;
        public bool Leaving => state == State.Leaving;
        /// <summary>Seconds they'll still wait.</summary>
        public float PatienceLeft => state == State.Waiting ? Mathf.Max(0f, waitUntil - Time.time) : patience;
        public Vector2 Position => transform.position;

        internal void Setup(Survivors survivors, Kingdom k, Vector2 at, string name, Job trade, bool infected, string remark, float wait)
        {
            owner = survivors;
            kingdom = k;
            sprites = k.VillagerLooks;
            Name = name;
            Trade = trade;
            Infected = infected;
            Remark = remark;
            patience = wait;
            cameFrom = at;
            transform.position = at;

            Health = gameObject.AddComponent<Health>();
            Health.Setup(Team.Friendly, 50f, 0.18f, true);
            Health.Died += OnDied;
            if (sprites != null)
            {
                shadow = Look.AddShadow(transform, sprites.Shadow, 0.1f);
                body = new GameObject("Body").transform;
                body.SetParent(transform, false);
                bodyRenderer = body.gameObject.AddComponent<SpriteRenderer>();
                bodyRenderer.sortingOrder = SortOrder.Characters;
            }

            // Up to the throne (to the front of it); with no way there, they wait where they are and call out.
            state = State.Coming;
            var throne = kingdom.Throne;
            if (throne == null || !kingdom.Nav.FindPath(at, throne.Cells, true, path)) Wait();
            pathIndex = 0;
        }

        /// <summary>Turned away (or tired of waiting): back the way they came, and gone.</summary>
        public void Leave()
        {
            if (state == State.Leaving) return;
            state = State.Leaving;
            pathIndex = 0;
            if (!kingdom.Nav.FindPath(Position, new RectInt(NavGrid.CellAt(cameFrom), Vector2Int.one), false, path)) path.Clear();
            gone = path.Count == 0 ? Time.time : -1f;   // no way back: they just fade away
        }

        void Wait()
        {
            state = State.Waiting;
            waitUntil = Time.time + patience;
            path.Clear();
            pathIndex = 0;
            if (kingdom.Throne != null)
            {
                var to = kingdom.Throne.Center - Position;
                if (to.sqrMagnitude > 1e-4f) facing = to.normalized;
            }
        }

        void Update()
        {
            if (Health == null || Health.Dead) return;
            Vector2 before = transform.position;
            switch (state)
            {
                case State.Coming:
                    if (!Follow()) Wait();
                    break;
                case State.Waiting:
                    if (Time.time >= waitUntil)
                    {
                        owner.GaveUp(this);
                        Leave();
                    }
                    break;
                case State.Leaving:
                    if (gone < 0f && !Follow()) gone = Time.time;
                    if (gone >= 0f)
                    {
                        // Off into the wild: fade and go.
                        float left = 1f - (Time.time - gone) / FadeSeconds;
                        if (left <= 0f)
                        {
                            owner.Gone(this);
                            Destroy(gameObject);
                            return;
                        }
                        if (bodyRenderer != null) bodyRenderer.color = new Color(1f, 1f, 1f, left);
                        if (shadow != null) shadow.enabled = false;
                    }
                    break;
            }
            Animate(((Vector2)transform.position - before) / Mathf.Max(Time.deltaTime, 1e-5f));
        }

        // Along the path; false once at its end (or if something was built in the way and there's no other way).
        bool Follow()
        {
            if (pathIndex >= path.Count) return false;
            var target = path[pathIndex];
            if (!kingdom.Nav.Walkable(target))
            {
                var goal = state == State.Leaving ? new RectInt(NavGrid.CellAt(cameFrom), Vector2Int.one) : kingdom.Throne != null ? kingdom.Throne.Cells : new RectInt();
                bool beside = state != State.Leaving;
                pathIndex = 0;
                if (goal.width <= 0 || !kingdom.Nav.FindPath(Position, goal, beside, path)) return false;
                if (path.Count == 0) return false;
                target = path[0];
            }
            Vector2 here = transform.position;
            float step = Speed * (World.Seasons.Current != null ? World.Seasons.Current.MoveFactor : 1f) * Time.deltaTime;
            var to = target - here;
            if (to.magnitude <= step)
            {
                transform.position = target;
                pathIndex++;
            }
            else transform.position = here + to.normalized * step;
            return true;
        }

        void Animate(Vector2 velocity)
        {
            if (bodyRenderer == null) return;
            float speed = velocity.magnitude;
            if (speed > 0.1f)
            {
                facing = velocity / speed;
                stepClock += Time.deltaTime * StepsPerSecond;
            }
            else stepClock = 0f;
            // They carry what they have left on their back.
            var frames = sprites.Carrying;
            float wanted = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            angle = Mathf.MoveTowardsAngle(angle, wanted, 540f * Time.deltaTime);
            int direction = Character.RulerLook.Direction(angle, frames.GetLength(0));
            bodyRenderer.sprite = frames[direction, Mathf.FloorToInt(stepClock) % frames.GetLength(1)];
            body.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (shadow != null) shadow.transform.localRotation = body.localRotation;
        }

        void OnDied(Health h)
        {
            var remains = Remains.Current;
            if (remains != null && sprites != null && sprites.Dead != null && sprites.Dead.Length > 0)
            {
                float a = angle + 180f + Random.Range(-30f, 30f);
                remains.Body(sprites.Dead[Character.RulerLook.Direction(a, sprites.Dead.Length)], Position, a);
                remains.Blood(Position, 1.1f);
            }
            owner.Killed(this);
            Destroy(gameObject);
        }
    }
}
