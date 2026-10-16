using System;
using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.UI;
using KingdomGame.Zombies;
using UnityEngine;

namespace KingdomGame.Networking
{
    /// <summary>What <see cref="Coop"/> needs from the network: who's on, and messages to and from one player.</summary>
    public interface ICoopLink
    {
        /// <summary>Our player number on the server.</summary>
        int MyId { get; }
        /// <summary>The other players (number, name, kingdom).</summary>
        IEnumerable<PlayerInfo> Others { get; }
        /// <summary>A kingdom's spot, or null.</summary>
        KingdomInfo KingdomOf(int kingdomId);
        void SendDirect(int to, DirectKind kind, byte[] bytes, bool reliable);
        /// <summary>A player sent us something (from whom, what kind, the message).</summary>
        event Action<int, DirectKind, byte[]> Direct;
        /// <summary>A player left the game.</summary>
        event Action<int> PlayerGone;
    }

    /// <summary>
    /// Playing together online (O opens its panel):
    /// <list type="bullet">
    /// <item><b>Trading</b> with another player: what you give is taken from your storage and held until they answer;
    /// if they take it, what you asked for goes into your storage; if they say no (or don't answer, or leave), yours comes back.</item>
    /// <item><b>Defending each other</b>: every kingdom's zombies run on its own player's computer. The ones near another
    /// player are sent to that player's game many times a second and shown there; their shots and blows on them are
    /// sent back and land on the real ones; and since the others are stand-ins here (<see cref="RemotePlayers"/>), our
    /// zombies go for them too, and what they take is sent to their game.</item>
    /// <item><b>Riding</b> to a friend's kingdom (and home again) in a few seconds instead of minutes of walking.</item>
    /// </list>
    /// One per online game, on the host's computer and on each player's who joined.
    /// </summary>
    public sealed class Coop : MonoBehaviour
    {
        const float ShareSeconds = 0.12f, GhostTimeout = 0.8f, FadeSeconds = 0.5f;
        const int MaxOffersFromOne = 3, TradeStep = 10;
        /// <summary>What can be traded: wood, stone, food, copper, iron, gold, steel and coal.</summary>
        static readonly ResourceType[] Tradeable =
        {
            ResourceType.Wood, ResourceType.Stone, ResourceType.Food, ResourceType.Copper,
            ResourceType.Iron, ResourceType.Gold, ResourceType.Steel, ResourceType.Coal,
        };

        sealed class Offer
        {
            public int With;
            public TradeOffer Trade;
            public float At;
        }

        // Another player's zombie, shown here.
        sealed class Ghost
        {
            public int Owner;
            public ushort Id;
            public ZombieConfig Kind;
            public ZombieSprites Pictures;
            public Transform Root, Body, Eyes;
            public SpriteRenderer Look, Shadow;
            public Health Health;
            public Vector2 At, Target;
            public float Angle, TargetAngle, HeardAt;
            public int Frame;
        }

        ICoopLink link;
        Kingdom kingdom;
        RulerController ruler;
        RemotePlayers remotes;
        ZombieDirector director;
        ChatBox chat;
        NetworkConfig config;
        readonly Dictionary<int, Offer> sent = new Dictionary<int, Offer>();
        readonly List<Offer> received = new List<Offer>();
        readonly Dictionary<(int, ushort), Ghost> ghosts = new Dictionary<(int, ushort), Ghost>();
        readonly List<ZombieEntry> entries = new List<ZombieEntry>();
        readonly List<(int, ushort)> stale = new List<(int, ushort)>();
        // Others' zombies that died lately (a late update mustn't bring one back), and when.
        readonly Dictionary<(int, ushort), float> fallen = new Dictionary<(int, ushort), float>();
        int nextOfferId = 1;
        float shareAt;

        bool open;
        int tradeWith = -1;
        readonly int[] give = new int[Inventory.TypeCount], want = new int[Inventory.TypeCount];
        GUIStyle title, text, small, button;

        // Riding: from when, where to, and who to.
        bool riding, moved, wasInvulnerable;
        float rideStart;
        Vector2 rideTo;
        string rideName;

        public static Coop Current { get; private set; }
        public bool IsOpen => open;

        float RideSeconds => config != null ? config.rideSeconds : 6f;
        float ShareWithin => config != null ? config.shareZombiesWithin : 40f;
        float TradeSeconds => config != null ? config.tradeSeconds : 60f;

        public void Setup(ICoopLink network, Kingdom owner, RulerController rulerController, RemotePlayers others, ZombieDirector zombies,
            ChatBox chatBox, NetworkConfig settings)
        {
            link = network;
            kingdom = owner;
            ruler = rulerController;
            remotes = others;
            director = zombies;
            chat = chatBox;
            config = settings;
            Current = this;
            link.Direct += OnDirect;
            link.PlayerGone += OnPlayerGone;
            remotes.Hurt += OnStandInHurt;
            if (director != null) director.ZombieDied += OnOwnZombieDied;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (link != null)
            {
                link.Direct -= OnDirect;
                link.PlayerGone -= OnPlayerGone;
            }
            if (remotes != null) remotes.Hurt -= OnStandInHurt;
            if (director != null) director.ZombieDied -= OnOwnZombieDied;
            if (open) UiFocus.PanelOpen = false;
        }

        /// <summary>What's held for offers not answered yet (one amount per resource type), or null: saves keep it as a bag by the throne.</summary>
        public int[] HeldBack()
        {
            if (sent.Count == 0) return null;
            var held = new int[Inventory.TypeCount];
            foreach (var o in sent.Values)
            {
                var give = Fit(o.Trade.Give);
                for (int i = 0; i < held.Length; i++) held[i] += give[i];
            }
            return held;
        }

        string NameOf(int player)
        {
            if (link != null) foreach (var p in link.Others) if (p.Id == player) return p.Name;
            return "Someone";
        }

        void Say(string line)
        {
            if (chat != null) chat.Notice(line);
            else if (kingdom != null) kingdom.Report(line);
        }

        // ---------------------------------------------------------- messages --

        void OnDirect(int from, DirectKind kind, byte[] bytes)
        {
            try
            {
                switch (kind)
                {
                    case DirectKind.TradeOffer: Offered(from, TradeOffer.Read(bytes)); break;
                    case DirectKind.TradeReply: Answered(from, TradeReply.Read(bytes)); break;
                    case DirectKind.Zombies: HeardZombies(from, bytes); break;
                    case DirectKind.ZombiesGone: ZombiesGone(from, bytes); break;
                    case DirectKind.ZombieHit:
                    {
                        var hit = ZombieHit.Read(bytes);
                        var z = director != null ? director.WithNetId(hit.Id) : null;
                        if (z != null && z.Health != null && !z.Health.Dead) z.Health.Damage(hit.Damage, new Vector2(hit.FromX, hit.FromY));
                        break;
                    }
                    case DirectKind.PlayerHit:
                    {
                        var hit = PlayerHit.Read(bytes);
                        if (ruler != null && ruler.Health != null && !ruler.Dead && !riding) ruler.Health.Damage(hit.Damage, new Vector2(hit.FromX, hit.FromY));
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"A {kind} message from player {from} didn't read right ({e.GetType().Name}): ignored.");
            }
        }

        void OnPlayerGone(int player)
        {
            // Offers to them: what was held comes back. Offers from them: gone.
            var back = new List<int>();
            foreach (var pair in sent) if (pair.Value.With == player) back.Add(pair.Key);
            foreach (int id in back)
            {
                Store(sent[id].Trade.Give);
                sent.Remove(id);
                Say("They left before answering your trade: your goods are back in storage.");
            }
            received.RemoveAll(o => o.With == player);
            if (tradeWith == player) tradeWith = -1;
            stale.Clear();
            foreach (var key in ghosts.Keys) if (key.Item1 == player) stale.Add(key);
            foreach (var key in stale) RemoveGhost(key, false);
        }

        // ------------------------------------------------------------ trading --

        /// <summary>Offers <paramref name="to"/> what's in <paramref name="giving"/> for what's in <paramref name="wanting"/>; false (and why) if it can't.</summary>
        public bool SendOffer(int to, int[] giving, int[] wanting, out string why)
        {
            var trade = new TradeOffer { Id = nextOfferId++, Give = (int[])giving.Clone(), Want = (int[])wanting.Clone() };
            if (trade.Empty) { why = "Choose something to give or to ask for"; return false; }
            if (!kingdom.TrySpend(trade.Give)) { why = "You don't have that much in storage"; return false; }
            sent[trade.Id] = new Offer { With = to, Trade = trade, At = Time.unscaledTime };
            link.SendDirect(to, DirectKind.TradeOffer, trade.Write(), true);
            why = null;
            return true;
        }

        void Offered(int from, TradeOffer trade)
        {
            if (trade.Empty) return;
            int fromThem = 0;
            foreach (var o in received) if (o.With == from) fromThem++;
            if (fromThem >= MaxOffersFromOne)
            {
                link.SendDirect(from, DirectKind.TradeReply, new TradeReply { Id = trade.Id, Accepted = false }.Write(), true);
                return;
            }
            received.Add(new Offer { With = from, Trade = trade, At = Time.unscaledTime });
            Say($"{NameOf(from)} offers you a trade: press O to see it.");
            Audio.Sounds.PlayHere(Audio.Sound.Chime);
        }

        /// <summary>Takes an offer (if we have what they ask for): theirs goes into storage, ours to them.</summary>
        public bool Accept(int index, out string why)
        {
            why = null;
            if (index < 0 || index >= received.Count) return false;
            var o = received[index];
            var asked = Fit(o.Trade.Want);
            if (!kingdom.TrySpend(asked)) { why = "You don't have what they ask for in storage"; return false; }
            received.RemoveAt(index);
            Store(o.Trade.Give);
            link.SendDirect(o.With, DirectKind.TradeReply, new TradeReply { Id = o.Trade.Id, Accepted = true }.Write(), true);
            Say($"Trade done with {NameOf(o.With)}: you got {Describe(o.Trade.Give)}.");
            return true;
        }

        public void Decline(int index)
        {
            if (index < 0 || index >= received.Count) return;
            var o = received[index];
            received.RemoveAt(index);
            link.SendDirect(o.With, DirectKind.TradeReply, new TradeReply { Id = o.Trade.Id, Accepted = false }.Write(), true);
        }

        void Answered(int from, TradeReply reply)
        {
            if (!sent.TryGetValue(reply.Id, out var o) || o.With != from) return;
            sent.Remove(reply.Id);
            if (reply.Accepted)
            {
                Store(o.Trade.Want);
                Say($"{NameOf(from)} took your trade: you got {Describe(o.Trade.Want)}.");
            }
            else
            {
                Store(o.Trade.Give);
                Say($"{NameOf(from)} turned your trade down: your goods are back in storage.");
            }
        }

        // An amount for each resource type of ours (an offer from another game may list more or fewer).
        static int[] Fit(int[] amounts)
        {
            var fitted = new int[Inventory.TypeCount];
            for (int i = 0; i < fitted.Length && i < amounts.Length; i++) fitted[i] = Mathf.Max(0, amounts[i]);
            return fitted;
        }

        // Into storage; what doesn't fit is left in a bag by the throne.
        void Store(int[] amounts)
        {
            if (kingdom == null) return;
            var near = kingdom.Throne != null ? kingdom.Throne.Front : (ruler != null ? (Vector2)ruler.transform.position : Vector2.zero);
            var left = new int[Inventory.TypeCount];
            var fitted = Fit(amounts);
            for (int i = 0; i < fitted.Length; i++)
                if (fitted[i] > 0) left[i] = fitted[i] - kingdom.Store((ResourceType)i, fitted[i], near);
            LootBag.Drop(near, left);
        }

        static string Describe(int[] amounts)
        {
            var parts = new List<string>();
            for (int i = 0; i < amounts.Length && i < Inventory.TypeCount; i++)
                if (amounts[i] > 0) parts.Add($"{amounts[i]} {ResourceText.Name((ResourceType)i)}");
            return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
        }

        // ------------------------------------------------------- our zombies --

        // Our zombies near each other player, sent to them a few times a second (only the latest matter).
        void ShareZombies()
        {
            if (director == null || director.Alive == 0) return;
            float r2 = ShareWithin * ShareWithin;
            foreach (int id in remotes.Ids)
            {
                if (!remotes.Latest(id, out var at, out bool outside) || !outside) continue;
                entries.Clear();
                foreach (var z in director.Zombies)
                {
                    if (z == null || z.Health == null || z.Health.Dead || (z.Position - at).sqrMagnitude > r2) continue;
                    entries.Add(new ZombieEntry
                    {
                        Id = z.NetId, Kind = director.KindNumber(z.Config), X = z.Position.x, Y = z.Position.y,
                        Angle = z.Angle, Frame = (byte)z.Frame, Health = z.Health.Fraction,
                    });
                }
                for (int start = 0; start < entries.Count; start += ZombieBatch.PerMessage)
                    link.SendDirect(id, DirectKind.Zombies, ZombieBatch.Write(entries, start, ZombieBatch.PerMessage), false);
            }
        }

        // One of ours died: the players near it see it fall (sent so it isn't missed).
        void OnOwnZombieDied(Zombie z)
        {
            if (z == null) return;
            float r2 = ShareWithin * ShareWithin;
            var one = new List<ZombieEntry> { new ZombieEntry { Id = z.NetId, Kind = director.KindNumber(z.Config), X = z.Position.x, Y = z.Position.y, Angle = z.Angle } };
            foreach (int id in remotes.Ids)
                if (remotes.Latest(id, out var at, out bool outside) && outside && (z.Position - at).sqrMagnitude <= r2)
                    link.SendDirect(id, DirectKind.ZombiesGone, ZombieBatch.Write(one, 0, 1), true);
        }

        // Our zombie (or wolf) hurt another player's stand-in: it's their game that takes the hit.
        void OnStandInHurt(int player, float amount, Vector2 from) =>
            link.SendDirect(player, DirectKind.PlayerHit, new PlayerHit { Damage = amount, FromX = from.x, FromY = from.y }.Write(), true);

        // ----------------------------------------------------- their zombies --

        void HeardZombies(int owner, byte[] bytes)
        {
            ZombieBatch.Read(bytes, entries);
            foreach (var e in entries)
            {
                var key = (owner, e.Id);
                if (fallen.ContainsKey(key)) continue;
                if (!ghosts.TryGetValue(key, out var g))
                {
                    g = MakeGhost(owner, e);
                    if (g == null) continue;
                    ghosts[key] = g;
                }
                g.Target = new Vector2(e.X, e.Y);
                g.TargetAngle = e.Angle;
                g.Frame = e.Frame;
                g.HeardAt = Time.unscaledTime;
            }
        }

        void ZombiesGone(int owner, byte[] bytes)
        {
            ZombieBatch.Read(bytes, entries);
            foreach (var e in entries)
            {
                fallen[(owner, e.Id)] = Time.unscaledTime;
                RemoveGhost((owner, e.Id), true, new Vector2(e.X, e.Y), e.Angle);
            }
        }

        Ghost MakeGhost(int owner, ZombieEntry e)
        {
            var kind = director != null ? director.KindOf(e.Kind) : null;
            var pictures = kind != null ? director.Pictures(kind) : null;
            if (pictures == null || pictures.Frames == null) return null;
            var go = new GameObject($"{NameOf(owner)}'s {kind.displayName}");
            go.transform.SetParent(transform, false);
            var at = new Vector2(e.X, e.Y);
            go.transform.position = at;
            var g = new Ghost { Owner = owner, Id = e.Id, Kind = kind, Pictures = pictures, Root = go.transform, At = at, Target = at, Angle = e.Angle, TargetAngle = e.Angle };
            g.Shadow = Look.AddShadow(go.transform, pictures.Shadow, 0.1f);
            g.Body = new GameObject("Body").transform;
            g.Body.SetParent(go.transform, false);
            g.Look = g.Body.gameObject.AddComponent<SpriteRenderer>();
            g.Look.sortingOrder = SortOrder.Characters - 1;
            if (pictures.Eyes != null)
            {
                g.Eyes = new GameObject("Eyes").transform;
                g.Eyes.SetParent(g.Body, false);
                g.Eyes.localScale = Vector3.one * pictures.Scale;
                var eyes = g.Eyes.gameObject.AddComponent<SpriteRenderer>();
                eyes.sprite = pictures.Eyes;
                eyes.sortingOrder = SortOrder.Characters;
                eyes.color = kind.boss ? new Color(1f, 0.42f, 0.32f, 1f) : new Color(1f, 1f, 1f, 0.85f);
                Look.MakeGlow(eyes);
            }
            // Shots and blows here find it like any zombie; what it takes is sent to its owner's game (where it's real).
            g.Health = go.AddComponent<Health>();
            g.Health.Setup(Team.Zombie, 1e6f, kind.radius, true);
            g.Health.Hurt += (h, amount, from) =>
            {
                link.SendDirect(owner, DirectKind.ZombieHit, new ZombieHit { Id = e.Id, Damage = amount, FromX = from.x, FromY = from.y }.Write(), true);
                h.Heal(amount);
            };
            ShowGhost(g, 0f);
            return g;
        }

        void ShowGhost(Ghost g, float dt)
        {
            if (g.Root == null) return;
            if ((g.Target - g.At).sqrMagnitude > 16f) g.At = g.Target;   // a big jump: no gliding across
            else g.At = Vector2.Lerp(g.At, g.Target, 1f - Mathf.Exp(-dt * 12f));
            g.Angle = Mathf.LerpAngle(g.Angle, g.TargetAngle, 1f - Mathf.Exp(-dt * 12f));
            var frames = g.Pictures.Frames;
            int frame = Mathf.Clamp(g.Frame, 0, frames.GetLength(1) - 1);
            g.Look.sprite = frames[RulerLook.Direction(g.Angle, frames.GetLength(0)), frame];
            if (g.Eyes != null && g.Pictures.EyesAt != null && frame < g.Pictures.EyesAt.Length) g.Eyes.localPosition = g.Pictures.EyesAt[frame] * g.Pictures.Scale;
            var turn = Quaternion.Euler(0f, 0f, g.Angle);
            g.Body.localRotation = turn;
            if (g.Shadow != null) g.Shadow.transform.localRotation = turn;
            g.Root.position = g.At;
        }

        void RemoveGhost((int, ushort) key, bool fell, Vector2 at = default, float angle = 0f)
        {
            if (!ghosts.TryGetValue(key, out var g)) return;
            ghosts.Remove(key);
            if (fell)
            {
                var remains = Remains.Current;
                if (remains != null && g.Pictures.Dead != null && g.Pictures.Dead.Length > 0)
                {
                    float a = angle + 180f + UnityEngine.Random.Range(-30f, 30f);
                    remains.Body(g.Pictures.Dead[RulerLook.Direction(a, g.Pictures.Dead.Length)], at, a);
                    remains.Blood(at, 1.1f);
                }
                Audio.Sounds.Play(Audio.Sound.ZombieDie, at);
            }
            if (g.Root != null) Destroy(g.Root.gameObject);
        }

        // -------------------------------------------------------------- riding --

        /// <summary>Rides to <paramref name="to"/> (a friend's kingdom, or home); false (and why) if the ruler can't go now.</summary>
        public bool RideTo(Vector2 to, string where, out string why)
        {
            why = null;
            if (riding) { why = "You're already on your way"; return false; }
            if (ruler == null || ruler.Dead) { why = "Not while you're down"; return false; }
            if (ruler.Inside != null) { why = "Go outside first"; return false; }
            if (ruler.OnWalls) { why = "Get down off the walls first"; return false; }
            if (ruler.Seated) { why = "Get up from the throne first"; return false; }
            riding = true;
            moved = false;
            rideStart = Time.unscaledTime;
            rideTo = OpenSpotNear(to);
            rideName = where;
            ruler.Riding = true;
            wasInvulnerable = ruler.Health.Invulnerable;
            ruler.Health.Invulnerable = true;   // (nobody can hurt you on the road)
            if (open) Toggle();
            return true;
        }

        void Ride()
        {
            if (!riding) return;
            float t = Time.unscaledTime - rideStart;
            if (!moved && t >= FadeSeconds)
            {
                moved = true;
                ruler.ForceMove(rideTo);
            }
            if (t < FadeSeconds * 2f + RideSeconds) return;
            riding = false;
            ruler.Riding = false;
            ruler.Health.Invulnerable = wasInvulnerable;
            Say($"You've arrived at {rideName}.");
        }

        // An open cell near a spot (no wall, tree or rock): where the ruler arrives.
        static Vector2 OpenSpotNear(Vector2 at)
        {
            var world = PixelWorld.Current;
            var nature = NatureMap.Current;
            var start = new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y));
            for (int r = 0; r <= 8; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var c = new Vector2Int(start.x + dx, start.y + dy);
                        if (world != null && world.ChunkAt(c) != null) continue;
                        if (nature != null && nature.Blocks(c)) continue;
                        return (Vector2)c + new Vector2(0.5f, 0.5f);
                    }
            return at;
        }

        // ------------------------------------------------------------- frame --

        void Update()
        {
            if (link == null) return;
            float dt = Time.deltaTime;
            if (Time.unscaledTime >= shareAt)
            {
                shareAt = Time.unscaledTime + ShareSeconds;
                ShareZombies();
            }
            stale.Clear();
            foreach (var pair in ghosts)
            {
                if (Time.unscaledTime - pair.Value.HeardAt > GhostTimeout) stale.Add(pair.Key);
                else ShowGhost(pair.Value, dt);
            }
            foreach (var key in stale) RemoveGhost(key, false);
            if (fallen.Count > 0)
            {
                stale.Clear();
                foreach (var pair in fallen) if (Time.unscaledTime - pair.Value > 3f) stale.Add(pair.Key);
                foreach (var key in stale) fallen.Remove(key);
            }
            // Offers nobody answered in time: turned down.
            for (int i = received.Count - 1; i >= 0; i--)
                if (Time.unscaledTime - received[i].At > TradeSeconds) Decline(i);
            Ride();
            if ((!UiFocus.PanelOpen || open) && !riding && GameInput.Pressed(KeyCode.O)) Toggle();
            if (open && GameInput.Pressed(KeyCode.Escape)) Toggle();
        }

        public void Toggle()
        {
            open = !open;
            UiFocus.PanelOpen = open;
            if (!open) tradeWith = -1;
        }

        // ------------------------------------------------------------ screen --

        void OnGUI()
        {
            int font = Mathf.Max(12, Screen.height / 55);
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { richText = true };
                text = new GUIStyle(GUI.skin.label) { richText = true, alignment = TextAnchor.MiddleLeft };
                small = new GUIStyle(text) { wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { richText = true };
            }
            title.fontSize = Mathf.RoundToInt(font * 1.4f);
            text.fontSize = button.fontSize = font;
            small.fontSize = Mathf.RoundToInt(font * 0.85f);

            if (riding) DrawRide(font);
            else if (open) DrawPanel(font);
            else if (received.Count > 0)
            {
                // A reminder while the panel is closed.
                var content = new GUIContent($"<b>{NameOf(received[0].With)}</b> offers a trade: press <b>O</b>");
                var size = text.CalcSize(content) + new Vector2(font, font * 0.6f);
                var r = new Rect(Screen.width - size.x - 10f, Screen.height * 0.24f, size.x, size.y);
                GUI.Box(r, GUIContent.none);
                GUI.Label(new Rect(r.x + font * 0.5f, r.y, r.width, r.height), content, text);
            }
        }

        // On the road: the screen goes dark, and lightens again where you arrive.
        void DrawRide(int font)
        {
            float t = Time.unscaledTime - rideStart, total = FadeSeconds * 2f + RideSeconds;
            float dark = t < FadeSeconds ? t / FadeSeconds : t > total - FadeSeconds ? (total - t) / FadeSeconds : 1f;
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(dark));
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;
            if (dark > 0.6f)
                GUI.Label(new Rect(0, Screen.height * 0.45f, Screen.width, font * 3f),
                    $"<color=#f0d08a><b>Riding to {rideName}...</b></color>", new GUIStyle(title) { alignment = TextAnchor.MiddleCenter });
        }

        void DrawPanel(int font)
        {
            float w = Mathf.Min(Screen.width - 40f, font * 44f), h = Mathf.Min(Screen.height - 40f, font * 40f);
            var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none);
            float x = panel.x + font, y = panel.y + font * 0.6f, inner = w - font * 2f, row = font * 1.9f;
            GUI.Label(new Rect(x, y, inner, font * 2f), "<b>Playing together</b>   <size=" + font + ">O or Esc closes this</size>", title);
            y += font * 2.4f;

            // Home, if you're away.
            if (kingdom != null && kingdom.Throne != null && ruler != null && ((Vector2)ruler.transform.position - kingdom.Throne.Front).sqrMagnitude > 900f)
            {
                if (GUI.Button(new Rect(x, y, font * 12f, font * 1.6f), "Ride home", button) && !RideTo(kingdom.Throne.Front, "your kingdom", out var why)) Say(why);
                y += row;
            }

            // The others: trade with them, or ride over (to help them on a horde night).
            int count = 0;
            foreach (var p in link.Others)
            {
                count++;
                var k = link.KingdomOf(p.KingdomId);
                string far = "";
                if (k != null && ruler != null) far = $"   <color=#8a8478>their kingdom is {Mathf.RoundToInt(Vector2.Distance(ruler.transform.position, new Vector2(k.X, k.Y)))} tiles away</color>";
                GUI.Label(new Rect(x, y, inner - font * 22f, font * 1.6f), $"<b>{p.Name}</b>{far}", text);
                if (GUI.Button(new Rect(x + inner - font * 21f, y, font * 9f, font * 1.6f), tradeWith == p.Id ? "<b>Trading</b>" : "Trade", button))
                {
                    tradeWith = tradeWith == p.Id ? -1 : p.Id;
                    Array.Clear(give, 0, give.Length);
                    Array.Clear(want, 0, want.Length);
                }
                GUI.enabled = k != null;
                if (GUI.Button(new Rect(x + inner - font * 11.5f, y, font * 11.5f, font * 1.6f), "Ride there", button) && k != null
                    && !RideTo(new Vector2(k.X + 1f, k.Y - 1.5f), $"{p.Name}'s kingdom", out var why)) Say(why);
                GUI.enabled = true;
                y += row;
            }
            if (count == 0)
            {
                GUI.Label(new Rect(x, y, inner, font * 1.6f), "<color=#8a8478>Nobody else is on right now.</color>", text);
                y += row;
            }

            if (tradeWith >= 0) y = DrawTrade(x, y + font * 0.4f, inner, font);
            y = DrawOffers(x, y + font * 0.4f, inner, font);
        }

        float DrawTrade(float x, float y, float inner, int font)
        {
            GUI.Label(new Rect(x, y, inner, font * 1.6f), $"<b>Trade with {NameOf(tradeWith)}</b>   <color=#8a8478>(Shift: steps of 100)</color>", text);
            y += font * 1.8f;
            int step = GameInput.Shift ? TradeStep * 10 : TradeStep;
            float col = font * 9f;
            GUI.Label(new Rect(x + col, y, font * 12f, font * 1.4f), "You give", small);
            GUI.Label(new Rect(x + col + font * 13f, y, font * 12f, font * 1.4f), "You want", small);
            y += font * 1.4f;
            foreach (var type in Tradeable)
            {
                int i = (int)type, have = kingdom.Stock(type);
                GUI.Label(new Rect(x, y, col, font * 1.5f), $"{ResourceText.Name(type)} <color=#8a8478>({have})</color>", small);
                if (GUI.Button(new Rect(x + col, y, font * 2f, font * 1.4f), "-", button)) give[i] = Mathf.Max(0, give[i] - step);
                GUI.Label(new Rect(x + col + font * 2.3f, y, font * 4f, font * 1.4f), $"<b>{give[i]}</b>", text);
                if (GUI.Button(new Rect(x + col + font * 6.3f, y, font * 2f, font * 1.4f), "+", button)) give[i] = Mathf.Min(have, give[i] + step);
                if (GUI.Button(new Rect(x + col + font * 13f, y, font * 2f, font * 1.4f), "-", button)) want[i] = Mathf.Max(0, want[i] - step);
                GUI.Label(new Rect(x + col + font * 15.3f, y, font * 4f, font * 1.4f), $"<b>{want[i]}</b>", text);
                if (GUI.Button(new Rect(x + col + font * 19.3f, y, font * 2f, font * 1.4f), "+", button)) want[i] = Mathf.Min(TradeOffer.MaxAmount, want[i] + step);
                y += font * 1.55f;
            }
            if (GUI.Button(new Rect(x, y + font * 0.3f, font * 12f, font * 1.7f), "Send the offer", button))
            {
                if (SendOffer(tradeWith, give, want, out var why))
                {
                    Say($"Offer sent to {NameOf(tradeWith)}: your goods are held until they answer.");
                    tradeWith = -1;
                }
                else Say(why);
            }
            return y + font * 2.4f;
        }

        float DrawOffers(float x, float y, float inner, int font)
        {
            for (int i = 0; i < received.Count; i++)
            {
                var o = received[i];
                int left = Mathf.CeilToInt(TradeSeconds - (Time.unscaledTime - o.At));
                GUI.Label(new Rect(x, y, inner - font * 18f, font * 2.8f),
                    $"<b>{NameOf(o.With)}</b> gives {Describe(o.Trade.Give)} for {Describe(o.Trade.Want)} <color=#8a8478>({left} s)</color>", small);
                if (GUI.Button(new Rect(x + inner - font * 17f, y, font * 8f, font * 1.6f), "Accept", button) && !Accept(i, out var why)) Say(why);
                if (GUI.Button(new Rect(x + inner - font * 8.5f, y, font * 8.5f, font * 1.6f), "Decline", button)) Decline(i);
                y += font * 2.9f;
            }
            foreach (var o in sent.Values)
            {
                GUI.Label(new Rect(x, y, inner, font * 2.6f),
                    $"<color=#8a8478>Waiting for {NameOf(o.With)}: you give {Describe(o.Trade.Give)} for {Describe(o.Trade.Want)}</color>", small);
                y += font * 2.6f;
            }
            return y;
        }
    }
}
