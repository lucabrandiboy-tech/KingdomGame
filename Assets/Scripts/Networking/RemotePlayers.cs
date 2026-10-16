using System.Collections.Generic;
using KingdomGame.Character;
using UnityEngine;

namespace KingdomGame.Networking
{
    /// <summary>
    /// The other players, as they're shown here: each one's ruler (moving smoothly between
    /// the positions heard, a moment in the past), a name over their head, and a marker
    /// with their name on each kingdom that isn't a real one on this computer.
    /// Each has a stand-in <see cref="Health"/> on the friendly side, so this game's zombies (and wolves) go for
    /// them like anyone else: what they'd take is reported (<see cref="Hurt"/>) and sent to that player's game, which
    /// decides whether they fall. Used by the host and by players who joined.
    /// </summary>
    public sealed class RemotePlayers : MonoBehaviour
    {
        sealed class Remote
        {
            public string Name;
            public Transform Body;
            public RulerLook Look;
            public readonly MotionBuffer Buffer = new MotionBuffer();
            public Vector2 Last;
            public bool Shown, Dead;
            public Health Stand;   // the stand-in this game's zombies hit
        }

        sealed class Marker
        {
            public KingdomInfo Kingdom;
            public Transform Throne;
        }

        readonly Dictionary<int, Remote> remotes = new Dictionary<int, Remote>();
        readonly Dictionary<int, Marker> markers = new Dictionary<int, Marker>();
        float moveSpeed = 3.2f, stepsPerSecond = 8f;
        GUIStyle label;

        /// <summary>The time to show others at (server seconds), set each frame by whoever runs the network.</summary>
        public double RenderTime { get; set; }
        /// <summary>A player was hurt here (by this game's zombies or wolves): who, how much, and where from.</summary>
        public event System.Action<int, float, Vector2> Hurt;

        /// <summary>Every other player shown here.</summary>
        public IEnumerable<int> Ids => remotes.Keys;

        /// <summary>Where a player was last heard to be (and whether they're out on the map, alive). False if nothing was heard.</summary>
        public bool Latest(int id, out Vector2 at, out bool outside)
        {
            at = default;
            outside = false;
            if (!remotes.TryGetValue(id, out var r) || !r.Buffer.Newest(out var m)) return false;
            at = new Vector2(m.X, m.Y);
            outside = (m.Flags & (MotionFlags.Dead | MotionFlags.Inside)) == 0;
            return true;
        }

        public void Setup(PlayerConfig player)
        {
            if (player != null)
            {
                moveSpeed = player.moveSpeed;
                stepsPerSecond = player.stepsPerSecond;
            }
        }

        public void Add(int id, string playerName)
        {
            if (remotes.TryGetValue(id, out var existing))
            {
                existing.Name = playerName;
                return;
            }
            var go = new GameObject($"Player {playerName}");
            go.transform.SetParent(transform, false);
            var look = go.AddComponent<RulerLook>();
            look.Build();
            var stand = go.AddComponent<Health>();
            stand.Setup(Team.Friendly, 1e6f, 0.15f, true);
            stand.Hurt += (h, amount, from) =>
            {
                Hurt?.Invoke(id, amount, from);
                h.Heal(amount);   // (it never falls here: their own game decides)
            };
            go.SetActive(false);
            remotes[id] = new Remote { Name = playerName, Body = go.transform, Look = look, Stand = stand };
        }

        public void Remove(int id)
        {
            if (!remotes.TryGetValue(id, out var r)) return;
            if (r.Body != null) Destroy(r.Body.gameObject);
            remotes.Remove(id);
        }

        /// <summary>Where player <paramref name="id"/> was at server time <paramref name="time"/>.</summary>
        public void Heard(int id, double time, Motion motion)
        {
            if (remotes.TryGetValue(id, out var r)) r.Buffer.Add(time, motion);
        }

        public string NameOf(int id) => remotes.TryGetValue(id, out var r) ? r.Name : null;

        /// <summary>A kingdom that's only a marker here (a throne and its owner's name), not a real one.</summary>
        public void AddKingdom(KingdomInfo kingdom)
        {
            if (kingdom == null) return;
            if (markers.TryGetValue(kingdom.Id, out var existing))
            {
                existing.Kingdom = kingdom;
                return;
            }
            var go = new GameObject($"{kingdom.Owner}'s kingdom");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(kingdom.X + 1f, kingdom.Y + 1f, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = ArtSprites.Get(Art.ArtKeys.Throne);
            r.sortingOrder = SortOrder.Buildings;
            var shadow = ArtSprites.Get(Art.ArtKeys.Shadow(Art.ArtKeys.Throne));
            if (shadow != null) Look.AddShadow(go.transform, shadow, 0.4f);
            markers[kingdom.Id] = new Marker { Kingdom = kingdom, Throne = go.transform };
        }

        void Update()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            foreach (var r in remotes.Values)
            {
                if (r.Body == null) continue;
                if (!r.Buffer.Sample(RenderTime - MotionBuffer.Delay, out var m))
                {
                    if (r.Shown) r.Body.gameObject.SetActive(r.Shown = false);
                    continue;
                }
                if (!r.Shown) r.Body.gameObject.SetActive(r.Shown = true);
                var at = new Vector2(m.X, m.Y);
                var velocity = (at - r.Last) / dt;
                if (velocity.sqrMagnitude > 400f) velocity = Vector2.zero;   // a jump, not a run
                r.Last = at;
                r.Body.position = at;
                bool dead = (m.Flags & MotionFlags.Dead) != 0;
                // Zombies can only go for them out on the map, alive, and not up on a wall.
                if (r.Stand != null)
                {
                    bool target = !dead && (m.Flags & MotionFlags.Inside) == 0;
                    if (r.Stand.enabled != target) r.Stand.enabled = target;
                    r.Stand.OutOfReach = (m.Flags & MotionFlags.OnWalls) != 0;
                }
                if (dead != r.Dead)
                {
                    r.Dead = dead;
                    if (dead) r.Look.ShowDead(m.Aim);
                    else r.Look.ShowAlive();
                }
                if (dead) continue;
                r.Look.SetRaised((m.Flags & MotionFlags.OnWalls) != 0);
                float a = m.Aim * Mathf.Deg2Rad;
                bool seated = (m.Flags & MotionFlags.Seated) != 0;
                r.Look.Show((m.Flags & MotionFlags.Walking) != 0 ? velocity : Vector2.zero,
                    seated ? Vector2.down : new Vector2(Mathf.Cos(a), Mathf.Sin(a)), moveSpeed, stepsPerSecond, holdingGun: !seated);
            }
        }

        // Names over heads and on kingdoms.
        void OnGUI()
        {
            var view = Camera.main;
            if (view == null) return;
            if (label == null) label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
            label.fontSize = Mathf.Max(11, Screen.height / 60);
            foreach (var r in remotes.Values)
                if (r.Shown && r.Body != null) Name(view, (Vector2)r.Body.position + new Vector2(0f, 0.75f), $"<b>{r.Name}</b>", "#f2e2b8");
            foreach (var k in markers.Values)
                if (k.Throne != null) Name(view, (Vector2)k.Throne.position + new Vector2(0f, 1.6f), $"{k.Kingdom.Owner}'s kingdom", "#c8b890");
        }

        void Name(Camera view, Vector2 at, string text, string color)
        {
            var p = view.WorldToScreenPoint(at);
            if (p.z < 0f || p.x < -100 || p.x > Screen.width + 100 || p.y < -50 || p.y > Screen.height + 50) return;
            var rect = new Rect(p.x - 150f, Screen.height - p.y - 12f, 300f, 24f);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, label);
            GUI.color = old;
            GUI.Label(rect, $"<color={color}>{text}</color>", label);
        }
    }
}
