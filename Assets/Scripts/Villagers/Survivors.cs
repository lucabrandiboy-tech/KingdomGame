using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Zombies;
using UnityEngine;

namespace KingdomGame.Villagers
{
    /// <summary>
    /// Survivors at the gate. After each wave (survival), or each morning (free build), people come out of the wild
    /// to your throne and ask to join, if you have room for them (each bed is a home). You choose: <b>Y</b> takes them
    /// in (as a villager doing what they did before), <b>K</b> keeps them out. Some have been bitten and hide it:
    /// often there's a sign (the message says what you notice), but not always, and healthy people can look ill too.
    /// Taken in, a bitten one turns into a zombie a few minutes later, inside your walls. One per game (<see cref="Current"/>).
    /// </summary>
    public sealed class Survivors : MonoBehaviour
    {
        static readonly string[] Names =
        {
            "Ada", "Aldo", "Ash", "Bran", "Bryn", "Cass", "Cora", "Dag", "Dell", "Edda", "Elm", "Fen", "Finn", "Gil", "Greer", "Hale",
            "Holt", "Ida", "Ives", "Jory", "Joss", "Kell", "Kit", "Lane", "Lark", "Mira", "Mott", "Ned", "Nell", "Odo", "Pip", "Quin",
            "Rook", "Sabe", "Tam", "Ulf", "Vale", "Wren", "Yara", "Zeb",
        };
        static readonly (Job job, string trade)[] Trades =
        {
            (Job.Builder, "a carpenter"), (Job.Woodcutter, "a woodcutter"), (Job.Miner, "a stonemason"), (Job.Forager, "a farmer"),
            (Job.Hunter, "a hunter"), (Job.Guard, "a soldier"), (Job.Crafter, "a blacksmith"),
        };
        // What you might notice: signs of a bite; things that look like it but aren't; nothing much.
        static readonly string[] BiteSigns =
        {
            "They keep one arm wrapped in a bloody rag.",
            "They're grey and sweating, and won't look you in the eye.",
            "There's a torn, dark-stained sleeve they keep pulling down.",
            "They cough, and wipe something dark from their lips.",
        };
        static readonly string[] FalseSigns =
        {
            "They're pale and thin: they say they haven't eaten in days.",
            "A bandage on their leg: they say they fell on the road.",
            "They cough from the cold.",
        };
        static readonly string[] Remarks =
        {
            "They look tired, but well.",
            "They carry their tools on their back.",
            "They say they've walked for three days.",
            "They ask only for a roof and a meal.",
            "They watch the dark behind them.",
        };

        SurvivalConfig config;
        Kingdom kingdom;
        World.DayNightCycle dayNight;
        bool survival;
        int dayChecked;
        readonly List<Survivor> here = new List<Survivor>();
        GUIStyle box, nameLabel, nameShadow;

        public static Survivors Current { get; private set; }
        public IReadOnlyList<Survivor> Here => here;

        /// <summary>The survivor waiting for an answer (the first to arrive), or null.</summary>
        public Survivor Asking
        {
            get
            {
                foreach (var s in here) if (s != null && s.Waiting) return s;
                return null;
            }
        }

        public void Setup(SurvivalConfig survivalConfig, Kingdom owner, World.DayNightCycle clock, bool survivalGame)
        {
            config = survivalConfig;
            kingdom = owner;
            dayNight = clock;
            survival = survivalGame;
            dayChecked = clock != null ? clock.Day : 1;
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>
        /// Up to <paramref name="count"/> survivors set out for the throne, as many as there's room for (<paramref name="wave"/>:
        /// survival's wave, later ones more often bitten; <paramref name="bitten"/> forces it, for testing). Returns how many.
        /// </summary>
        public int Arrive(int count, int wave, bool? bitten = null)
        {
            if (config == null || kingdom == null || kingdom.Throne == null || kingdom.VillagerLooks == null) return 0;
            int coming = 0;
            foreach (var s in here) if (s != null && !s.Leaving) coming++;
            int room = kingdom.VillagerCapacity - kingdom.Villagers.Count - coming;
            int n = Mathf.Clamp(count, 0, room);
            float chance = Mathf.Min(Mathf.Max(config.infectedChance, config.mostInfected), config.infectedChance + 0.02f * Mathf.Max(0, wave - 1));
            int made = 0;
            for (int i = 0; i < n; i++)
            {
                if (!FindStart(out var at)) break;
                bool infected = bitten ?? Random.value < chance;
                string remark = infected
                    ? (Random.value < config.infectedSigns ? Pick(BiteSigns) : Pick(Remarks))
                    : (Random.value < config.falseSigns ? Pick(FalseSigns) : Pick(Remarks));
                var trade = Trades[Random.Range(0, Trades.Length)];
                var go = new GameObject("Survivor");
                go.transform.SetParent(transform, false);
                var s = go.AddComponent<Survivor>();
                s.Setup(this, kingdom, at + Random.insideUnitCircle * 0.6f, Pick(Names), trade.job, infected, remark, config.survivorPatience);
                here.Add(s);
                made++;
            }
            return made;
        }

        /// <summary>Takes them in: a villager doing what they did before (a bitten one turns later).</summary>
        public void Accept(Survivor s)
        {
            if (s == null) return;
            if (kingdom.Villagers.Count >= kingdom.VillagerCapacity)
            {
                kingdom.Report("No room for them: build a bed first (each bed is a home for one more villager).");
                return;
            }
            var v = kingdom.AddVillager(s.Position, s.Trade);
            v.Name = s.Name;
            if (s.Infected) v.Infect(Random.Range(config.turnSeconds.x, Mathf.Max(config.turnSeconds.x, config.turnSeconds.y)));
            kingdom.Report($"{s.Name} joins you as a {Jobs.Name(s.Trade).ToLowerInvariant()} (V to change).");
            Audio.Sounds.PlayHere(Audio.Sound.Chime);
            here.Remove(s);
            Destroy(s.gameObject);
        }

        /// <summary>Keeps them out: they go back into the wild.</summary>
        public void TurnAway(Survivor s)
        {
            if (s == null) return;
            kingdom.Report($"{s.Name} turns and walks back into the wild.");
            s.Leave();
        }

        internal void GaveUp(Survivor s) => kingdom.Report($"{s.Name} gave up waiting and went away.");
        internal void Gone(Survivor s) => here.Remove(s);

        internal void Killed(Survivor s)
        {
            here.Remove(s);
            kingdom.Report($"{s.Name} was killed before you could take them in.");
        }

        void Update()
        {
            here.RemoveAll(s => s == null);
            // Free build: someone comes each morning (if there's room).
            if (!survival && config != null && dayNight != null && dayNight.Day != dayChecked && dayNight.Hour >= 7f && dayNight.Hour < 18f)
            {
                dayChecked = dayNight.Day;
                if (Arrive(config.survivorsPerDay, 0) > 0) kingdom.Report("A survivor is coming to your throne to ask to join you.");
            }
            var asking = Asking;
            if (asking == null || Time.timeScale <= 0f) return;
            if (GameInput.Pressed(KeyCode.Y)) Accept(asking);
            else if (GameInput.Pressed(KeyCode.K)) TurnAway(asking);
        }

        // Somewhere 16-24 tiles from the throne in the kingdom's lands, out in the open, with a way to the throne if there is one.
        bool FindStart(out Vector2 at)
        {
            var center = kingdom.Throne.Center;
            var nav = kingdom.Nav;
            var path = new List<Vector2>();
            Vector2? fallback = null;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), d = Random.Range(16f, 24f);
                var p = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                if (!nav.Cells.Contains(NavGrid.CellAt(p)) || !nav.Walkable(p)) continue;
                if (kingdom.PlaceAt(NavGrid.CellAt(p)) != null) continue;
                fallback = fallback ?? p;
                if (attempt < 16 && !nav.FindPath(p, kingdom.Throne.Cells, true, path)) continue;
                at = p;
                return true;
            }
            at = fallback ?? default;
            return fallback.HasValue;
        }

        static string Pick(string[] list) => list[Random.Range(0, list.Length)];

        // ---------------------------------------------------------------- screen --

        void OnGUI()
        {
            if (here.Count == 0) return;
            int font = Mathf.Max(12, Screen.height / 50);
            if (box == null)
            {
                box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, wordWrap = true };
                nameLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true };
                nameShadow = new GUIStyle(nameLabel);
                nameShadow.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            }
            box.fontSize = font;
            nameLabel.fontSize = nameShadow.fontSize = Mathf.RoundToInt(font * 0.85f);

            // Their names over their heads.
            var view = Camera.main;
            if (view != null)
                foreach (var s in here)
                {
                    if (s == null || s.Leaving) continue;
                    var p = view.WorldToScreenPoint((Vector3)(s.Position + new Vector2(0f, 0.6f)));
                    if (p.z < 0f) continue;
                    var rect = new Rect(p.x - font * 5f, Screen.height - p.y - font, font * 10f, font * 1.4f);
                    string text = s.Waiting ? $"<b>{s.Name}</b> (Y / K)" : $"<b>{s.Name}</b>";
                    GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, nameShadow);
                    GUI.Label(rect, $"<color=#f0e0b0>{text}</color>", nameLabel);
                }

            var asking = Asking;
            if (asking == null) return;
            int waiting = 0;
            foreach (var s in here) if (s != null && s.Waiting) waiting++;
            string trade = "someone";
            foreach (var t in Trades) if (t.job == asking.Trade) trade = t.trade;
            bool room = kingdom.Villagers.Count < kingdom.VillagerCapacity;
            string message = $"<b>{asking.Name}</b>, {trade}, is at your throne asking to join you.\n<i>{asking.Remark}</i>\n\n" +
                             (room ? "<b>Y</b>: take them in      <b>K</b>: keep them out" : "<color=#ff8a80>No room: build a bed first.</color>   <b>K</b>: keep them out") +
                             $"\n<size={Mathf.RoundToInt(font * 0.85f)}><color=#b8ad98>They'll wait {Mathf.CeilToInt(asking.PatienceLeft)} s" +
                             (waiting > 1 ? $". {waiting - 1} more waiting after them." : ".") + "</color></size>";
            float w = Mathf.Min(Screen.width * 0.4f, font * 26f);
            float h = box.CalcHeight(new GUIContent(message), w) + font * 0.4f;
            var area = new Rect(Screen.width - w - 10f, Screen.height * 0.32f, w, h);
            GUI.Box(area, GUIContent.none);
            GUI.Box(area, message, box);
        }
    }
}
