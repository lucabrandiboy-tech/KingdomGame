using System;
using System.Collections.Generic;
using KingdomGame.Building;
using UnityEngine;

namespace KingdomGame.Progression
{
    /// <summary>What a tech is doing, for the tech screen.</summary>
    public enum TechState
    {
        Done,
        Researching,
        Queued,
        /// <summary>Can be researched (or queued) now.</summary>
        Available,
        /// <summary>Needs something first (an earlier age, or other techs).</summary>
        Locked,
    }

    /// <summary>
    /// The kingdom's knowledge: which age it's in, what's been researched, and the
    /// research going on now. Research runs on real time (the clock on your computer),
    /// so it carries on while the game is closed; queued techs follow one after
    /// another. Each one's cost is taken from storage when it starts (it waits if
    /// storage is short). The last tech of an age moves the kingdom into the next.
    ///
    /// How things unlock depends on the mode: in the saved kingdom by research; in
    /// survival a new age every few waves (everything in it at once); in free build
    /// everything from the start. One per game scene (<see cref="Current"/>).
    /// </summary>
    public sealed class Research : MonoBehaviour
    {
        public enum Unlocking
        {
            /// <summary>Research each tech (the saved kingdom).</summary>
            ByResearch,
            /// <summary>A whole age at a time (survival: every few waves).</summary>
            ByAge,
            /// <summary>Everything (free build).</summary>
            Everything,
        }

        ProgressionConfig config;
        Kingdom kingdom;
        readonly HashSet<TechConfig> done = new HashSet<TechConfig>();
        readonly List<TechConfig> queue = new List<TechConfig>();
        readonly Dictionary<BuildingConfig, (int age, TechConfig tech)> source = new Dictionary<BuildingConfig, (int, TechConfig)>();
        readonly Dictionary<TechConfig, int> ageOfTech = new Dictionary<TechConfig, int>();

        public static Research Current { get; private set; }

        public ProgressionConfig Config => config;
        public Unlocking Mode { get; private set; }
        /// <summary>The age the kingdom is in (0 = the first).</summary>
        public int Age { get; private set; }
        public int AgeCount => config != null && config.ages != null ? config.ages.Length : 0;
        public AgeConfig CurrentAge => AgeAt(Age);
        /// <summary>The tech being researched now (it may be waiting for resources: see <see cref="Waiting"/>), or null.</summary>
        public TechConfig Researching => queue.Count > 0 ? queue[0] : null;
        public IReadOnlyList<TechConfig> Queue => queue;
        /// <summary>When the current research started (UTC), or null if it hasn't started (waiting for resources).</summary>
        public DateTime? StartedUtc { get; private set; }
        /// <summary>Why the current research hasn't started, or null.</summary>
        public string Waiting { get; private set; }
        /// <summary>Something was researched, queued or the age changed (the build bar and the tech screen redraw).</summary>
        public event Action Changed;
        /// <summary>A tech finished (for a message on screen).</summary>
        public event Action<TechConfig> Finished;
        float retryAt;   // waiting for resources: when to look in storage again

        void Awake()
        {
            if (Current != null && Current != this) Debug.LogWarning("There is more than one Research in the scene.", this);
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        public void Setup(ProgressionConfig progression, Kingdom owner, Unlocking unlocking)
        {
            config = progression;
            kingdom = owner;
            Mode = unlocking;
            source.Clear();
            ageOfTech.Clear();
            if (config != null && config.ages != null)
                for (int a = 0; a < config.ages.Length; a++)
                {
                    var age = config.ages[a];
                    if (age == null) continue;
                    foreach (var b in age.startUnlocks)
                        if (b != null && !source.ContainsKey(b)) source[b] = (a, null);
                    foreach (var t in age.techs)
                    {
                        if (t == null) continue;
                        ageOfTech[t] = a;
                        foreach (var b in t.unlocks)
                            if (b != null && !source.ContainsKey(b)) source[b] = (a, t);
                    }
                }
            Age = Mode == Unlocking.Everything ? Mathf.Max(0, AgeCount - 1) : 0;
        }

        public AgeConfig AgeAt(int index) => config != null && config.ages != null && index >= 0 && index < config.ages.Length ? config.ages[index] : null;

        /// <summary>The age a building belongs to (the first, if no age lists it).</summary>
        public int AgeOf(BuildingConfig b) => b != null && source.TryGetValue(b, out var s) ? s.age : 0;

        /// <summary>The tech that unlocks a building (null if it comes with its age).</summary>
        public TechConfig TechFor(BuildingConfig b) => b != null && source.TryGetValue(b, out var s) ? s.tech : null;

        public int AgeOf(TechConfig t) => t != null && ageOfTech.TryGetValue(t, out int a) ? a : 0;

        public bool IsDone(TechConfig t) => t != null && (done.Contains(t) || Mode == Unlocking.Everything || (Mode == Unlocking.ByAge && AgeOf(t) < Age));

        /// <summary>Whether a building can be built (things no age lists are always allowed).</summary>
        public bool IsUnlocked(BuildingConfig b)
        {
            if (b == null || Mode == Unlocking.Everything || !source.TryGetValue(b, out var s)) return true;
            if (s.age > Age) return false;
            if (Mode == Unlocking.ByAge || s.tech == null) return true;
            return done.Contains(s.tech);
        }

        /// <summary>Why a building can't be built yet, like "Research Pit traps (Stone Age)", or null.</summary>
        public string LockedReason(BuildingConfig b)
        {
            if (IsUnlocked(b)) return null;
            var s = source[b];
            var age = AgeAt(s.age);
            if (s.age > Age) return $"Reach the {(age != null ? age.displayName : "next age")} first";
            return $"Research {s.tech.displayName} first";
        }

        public TechState StateOf(TechConfig t)
        {
            if (IsDone(t)) return TechState.Done;
            int i = queue.IndexOf(t);
            if (i == 0) return TechState.Researching;
            if (i > 0) return TechState.Queued;
            return CanQueue(t, out _) ? TechState.Available : TechState.Locked;
        }

        /// <summary>Whether <paramref name="t"/> can go in the queue now, and if not, why.</summary>
        public bool CanQueue(TechConfig t, out string reason)
        {
            reason = null;
            if (t == null) { reason = "Nothing to research"; return false; }
            if (Mode != Unlocking.ByResearch) { reason = Mode == Unlocking.Everything ? "Everything is unlocked in free build" : "In survival, a new age comes every few waves"; return false; }
            if (IsDone(t)) { reason = "Already researched"; return false; }
            if (queue.Contains(t)) { reason = "Already in the queue"; return false; }
            int age = AgeOf(t);
            // The next age's techs can be queued behind the tech that leads to it.
            bool nextAgeQueued = age == Age + 1 && queue.Exists(q => q.advancesAge && AgeOf(q) == Age);
            if (age > Age && !nextAgeQueued) { reason = $"Reach the {AgeAt(age)?.displayName} first"; return false; }
            foreach (var r in t.requires)
                if (r != null && !IsDone(r) && !queue.Contains(r)) { reason = $"Needs {r.displayName}"; return false; }
            if (queue.Count >= (config != null ? config.queueLength : 5)) { reason = "The research queue is full"; return false; }
            return true;
        }

        /// <summary>Puts a tech in the research queue (it starts at once if nothing else is being researched).</summary>
        public bool Enqueue(TechConfig t, out string reason)
        {
            if (!CanQueue(t, out reason)) return false;
            queue.Add(t);
            if (queue.Count == 1) TryStart(DateTime.UtcNow);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Takes a tech out of the queue (and anything queued after it that needs it). A started one gives its cost back.</summary>
        public void Dequeue(TechConfig t)
        {
            int i = queue.IndexOf(t);
            if (i < 0) return;
            if (i == 0 && StartedUtc.HasValue && kingdom != null)
            {
                kingdom.Store(Economy.ResourceType.Wood, t.woodCost, kingdom.Throne != null ? kingdom.Throne.Center : Vector2.zero);
                kingdom.Store(Economy.ResourceType.Stone, t.stoneCost, kingdom.Throne != null ? kingdom.Throne.Center : Vector2.zero);
                kingdom.Store(Economy.ResourceType.Food, t.foodCost, kingdom.Throne != null ? kingdom.Throne.Center : Vector2.zero);
                StartedUtc = null;
            }
            queue.RemoveAt(i);
            // Whatever needed it can't stay queued.
            for (int k = queue.Count - 1; k >= i; k--)
                if (Needs(queue[k], t)) queue.RemoveAt(k);
            if (i == 0 && queue.Count > 0) TryStart(DateTime.UtcNow);
            Changed?.Invoke();
        }

        bool Needs(TechConfig t, TechConfig what)
        {
            if (t == null) return false;
            foreach (var r in t.requires) if (r == what) return true;
            return what.advancesAge && AgeOf(t) == AgeOf(what) + 1;
        }

        /// <summary>Real seconds a tech takes at the current research speed.</summary>
        public double SecondsFor(TechConfig t) => t == null ? 0 : t.researchHours * 3600.0 / Math.Max(0.01, config != null ? config.researchSpeed : 1f);

        /// <summary>Seconds of the current research left (its whole length if it hasn't started).</summary>
        public double SecondsLeft
        {
            get
            {
                var t = Researching;
                if (t == null) return 0;
                if (!StartedUtc.HasValue) return SecondsFor(t);
                return Math.Max(0, SecondsFor(t) - (DateTime.UtcNow - StartedUtc.Value).TotalSeconds);
            }
        }

        /// <summary>0..1 of the current research.</summary>
        public float Progress
        {
            get
            {
                var t = Researching;
                if (t == null || !StartedUtc.HasValue) return 0f;
                double total = Math.Max(1, SecondsFor(t));
                return Mathf.Clamp01((float)((DateTime.UtcNow - StartedUtc.Value).TotalSeconds / total));
            }
        }

        // Starts the front of the queue, paying for it, at time `when` (later than now when catching up on time away).
        bool TryStart(DateTime when)
        {
            var t = Researching;
            if (t == null || StartedUtc.HasValue) return false;
            if (kingdom != null && !kingdom.TrySpend(t.woodCost, t.stoneCost, t.foodCost))
            {
                Waiting = $"Waiting for {Cost(t)} in storage";
                return false;
            }
            Waiting = null;
            StartedUtc = when;
            return true;
        }

        public static string Cost(TechConfig t)
        {
            var parts = new List<string>(3);
            if (t.woodCost > 0) parts.Add($"{t.woodCost} wood");
            if (t.stoneCost > 0) parts.Add($"{t.stoneCost} stone");
            if (t.foodCost > 0) parts.Add($"{t.foodCost} food");
            return parts.Count > 0 ? string.Join(", ", parts) : "free";
        }

        void Update()
        {
            if (Mode != Unlocking.ByResearch) return;
            if (config != null && config.allowCheats && GameInput.Pressed(KeyCode.F8) && Researching != null)
            {
                if (!StartedUtc.HasValue) TryStart(DateTime.UtcNow);
                if (StartedUtc.HasValue) StartedUtc = DateTime.UtcNow.AddSeconds(-SecondsFor(Researching) - 1);
            }
            CatchUp();
        }

        /// <summary>Finishes everything whose time has come (also all that finished while the game was closed), starting the next in the queue.</summary>
        public void CatchUp()
        {
            for (int guard = 0; guard < 100 && Researching != null; guard++)
            {
                if (!StartedUtc.HasValue)
            {
                if (Time.unscaledTime < retryAt) return;
                retryAt = Time.unscaledTime + 0.5f;
                if (!TryStart(DateTime.UtcNow)) return;
            }
                var t = Researching;
                var end = StartedUtc.Value.AddSeconds(SecondsFor(t));
                if (end > DateTime.UtcNow) return;
                queue.RemoveAt(0);
                StartedUtc = null;
                Complete(t);
                // The next one starts when this one finished (if storage can pay for it).
                if (Researching != null) TryStart(end);
            }
        }

        void Complete(TechConfig t)
        {
            done.Add(t);
            if (t.advancesAge && AgeOf(t) == Age && Age < AgeCount - 1) Age++;
            Finished?.Invoke(t);
            Changed?.Invoke();
        }

        /// <summary>Survival: moves straight into the next age (everything in it unlocks). Returns false at the last age.</summary>
        public bool AdvanceAge()
        {
            if (Age >= AgeCount - 1) return false;
            Age++;
            Changed?.Invoke();
            return true;
        }

        // ------------------------------------------------------------ saving --

        /// <summary>What a save needs to restore research.</summary>
        [Serializable]
        public sealed class Saved
        {
            public int age;
            public List<string> done = new List<string>();
            public List<string> queue = new List<string>();
            /// <summary>When the current research started, as UTC ticks (0 = not started).</summary>
            public long startedTicks;
        }

        public Saved Save()
        {
            var s = new Saved { age = Age, startedTicks = StartedUtc.HasValue ? StartedUtc.Value.Ticks : 0 };
            foreach (var t in done) if (t != null) s.done.Add(t.name);
            foreach (var t in queue) if (t != null) s.queue.Add(t.name);
            return s;
        }

        public void Load(Saved s)
        {
            if (s == null) return;
            var byName = new Dictionary<string, TechConfig>();
            foreach (var t in ageOfTech.Keys) byName[t.name] = t;
            done.Clear();
            queue.Clear();
            foreach (var n in s.done) if (n != null && byName.TryGetValue(n, out var t)) done.Add(t);
            foreach (var n in s.queue) if (n != null && byName.TryGetValue(n, out var t) && !done.Contains(t)) queue.Add(t);
            Age = Mathf.Clamp(s.age, 0, Mathf.Max(0, AgeCount - 1));
            StartedUtc = s.startedTicks > 0 && queue.Count > 0 ? new DateTime(s.startedTicks, DateTimeKind.Utc) : (DateTime?)null;
            CatchUp();
            Changed?.Invoke();
        }
    }
}
