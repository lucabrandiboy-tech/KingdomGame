using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Economy;
using KingdomGame.Progression;
using KingdomGame.World;
using KingdomGame.Zombies;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// The cheat menu (F10), for testing: resources and XP, any age, any wave, any kind of zombie, the time
    /// of day, game speed, no damage for the ruler, building and research finished at once, where the ore fields are. Only in games
    /// played offline, and only while the Progression Config's Allow Cheats is on (turn it off for a
    /// finished game). The game keeps running while it's open.
    /// </summary>
    public sealed class CheatMenu : MonoBehaviour
    {
        Kingdom kingdom;
        RulerController ruler;
        Research research;
        DayNightCycle dayNight;
        SurvivalDirector waves;
        ZombieDirector zombies;
        ZombieConfig[] kinds;
        GUIStyle title, text, button;
        Vector2 scroll;
        int waveToStart = 1;
        float speed = 1f;
        string message;
        float messageUntil;

        static readonly float[] Speeds = { 1f, 2f, 4f };

        public bool IsOpen { get; private set; }

        public void Setup(Kingdom owner, RulerController rulerController, Research progress, DayNightCycle clock,
            SurvivalDirector survival, ZombieDirector director, ZombieConfig[] zombieKinds)
        {
            kingdom = owner;
            ruler = rulerController;
            research = progress;
            dayNight = clock;
            waves = survival;
            zombies = director;
            kinds = zombieKinds ?? new ZombieConfig[0];
        }

        void Toggle()
        {
            IsOpen = !IsOpen;
            UiFocus.PanelOpen = IsOpen;
        }

        void Update()
        {
            // Game speed: kept up while nothing has paused the game (the pause menu puts it back to normal when it closes).
            if (Time.timeScale > 0f && !Mathf.Approximately(Time.timeScale, speed)) Time.timeScale = speed;
            if (UiFocus.PanelOpen && !IsOpen) return;   // another panel has the keys
            if (GameInput.Pressed(KeyCode.F10) || (IsOpen && GameInput.Pressed(KeyCode.Escape))) Toggle();
        }

        void OnDisable()
        {
            if (IsOpen) UiFocus.PanelOpen = false;
            if (Time.timeScale > 0f) Time.timeScale = 1f;
        }

        void Say(string what, float seconds = 4f)
        {
            message = what;
            messageUntil = Time.unscaledTime + seconds;
        }

        // The nearest ore field of each kind (Economy/OreMap.cs), and which way it is from the ruler.
        string OreDirections()
        {
            var map = OreMap.Current;
            if (map == null) return "There are no ore fields in this world";
            Vector2 from = ruler != null ? (Vector2)ruler.transform.position : Vector2.zero;
            if (Mathf.Abs(from.x) >= OreMap.Limit || Mathf.Abs(from.y) >= OreMap.Limit)
                from = kingdom.Throne != null ? kingdom.Throne.Center : Vector2.zero;   // (in a house or a mine: from the throne)
            var fields = new List<OreMap.Field>();
            int reach = OreMap.Region * 3;
            OreMap.FieldsNear(map.Seed, Mathf.FloorToInt(from.x) - reach, Mathf.FloorToInt(from.y) - reach, Mathf.FloorToInt(from.x) + reach, Mathf.FloorToInt(from.y) + reach, fields);
            var parts = new List<string>();
            foreach (var kind in new[] { OreKind.Iron, OreKind.Copper, OreKind.Coal, OreKind.Stone })
            {
                float best = float.MaxValue;
                var to = Vector2.zero;
                foreach (var f in fields)
                {
                    if (f.Kind != kind) continue;
                    float d = (new Vector2(f.X, f.Y) - from).sqrMagnitude;
                    if (d < best) { best = d; to = new Vector2(f.X, f.Y); }
                }
                if (best == float.MaxValue) continue;
                var way = to - from;
                string[] compass = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
                int point = Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg, 360f) / 45f) % 8;
                parts.Add($"{OreMap.Name(kind)}: {Mathf.RoundToInt(way.magnitude)} tiles {compass[point]}");
            }
            return parts.Count == 0 ? "No ore fields near you" : "Nearest ore fields (their middles) - " + string.Join(", ", parts);
        }

        void OnGUI()
        {
            if (!IsOpen || kingdom == null) return;
            int font = Mathf.Max(12, Screen.height / 60);
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { richText = true };
                text = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { richText = true };
            }
            title.fontSize = Mathf.RoundToInt(font * 1.3f);
            text.fontSize = button.fontSize = font;

            float w = Mathf.Min(Screen.width - 20f, font * 30f), h = Screen.height - 20f;
            var panel = new Rect(10f, 10f, w, h);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none);   // twice: darker
            GUILayout.BeginArea(new Rect(panel.x + font * 0.6f, panel.y + font * 0.4f, w - font * 1.2f, h - font * 0.8f));
            GUILayout.Label("<b>Cheats</b>  <size=" + font + ">for testing.  F10 or Esc closes this; F3 shows the frame rate</size>", title);
            if (message != null && Time.unscaledTime < messageUntil) GUILayout.Label($"<color=#ffd27a>{message}</color>", text);
            scroll = GUILayout.BeginScrollView(scroll);

            Heading("Resources");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1,000 of everything", button)) Give(1000);
            if (GUILayout.Button("+10,000 of everything", button)) Give(10000);
            if (GUILayout.Button("+500 XP", button)) kingdom.AddXp(500, "Cheat");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Factory: +300 iron ore, copper ore and coal", button))
            {
                var near = kingdom.Throne != null ? kingdom.Throne.Center : Vector2.zero;
                int stored = 0;
                foreach (var t in new[] { ResourceType.IronOre, ResourceType.CopperOre, ResourceType.Coal }) stored += kingdom.Store(t, 300, near);
                Say(stored < 900 ? "The storehouses are full: build more storehouses to hold more." : "+300 iron ore, copper ore and coal (for furnaces)");
            }
            if (GUILayout.Button("Where are the ore fields?", button)) Say(OreDirections(), 15f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            var smog = kingdom.Factory != null ? kingdom.Factory.Pollution : null;
            GUI.enabled = smog != null && ruler != null;
            if (GUILayout.Button("Pollution: +2,000 where you stand", button))
            {
                smog.Emit(ruler.transform.position.x, ruler.transform.position.y, 2000f);
                Say("+2,000 pollution: it spreads from here (P shows it)");
            }
            if (GUILayout.Button("Clear the pollution", button))
            {
                smog.Clear();
                Say("The air is clean (the zombies stay as far on as they were)");
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Ages();

            Heading("Building and research");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Finish all building now", button)) FinishBuilding();
            GUI.enabled = research != null && research.Researching != null;
            if (GUILayout.Button("Finish research now", button)) research.CheatFinishResearch();
            GUI.enabled = kingdom.Crafting != null && kingdom.Crafting.HasWork;
            if (GUILayout.Button("Finish crafting now", button)) kingdom.Crafting.CheatFinish();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Heading("Fire");
            GUILayout.BeginHorizontal();
            var fires = Fires.Current;
            GUI.enabled = fires != null && ruler != null;
            if (GUILayout.Button("Set fire to wood near you", button))
                Say(fires.IgniteNear(ruler.transform.position, 3f, 1f) > 0 ? "Fire!" : "Nothing wooden near you to burn (wood walls, wooden furniture)");
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Heading("Wildlife");
            GUILayout.BeginHorizontal();
            var wild = World.Wildlife.Current;
            GUI.enabled = wild != null;
            if (GUILayout.Button("A herd of deer near you", button))
                Say(wild.CallDeer(12f) ? "Deer! They run if you get within 7 tiles" : "No open ground near you for them");
            if (GUILayout.Button("Wolves now (for 3 minutes, even by day)", button))
                Say(wild.CallWolves(180f, 16f) ? "Wolves are coming (they keep out of firelight)" : "No dark open ground near you for them");
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Heading("Villagers");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Feed and rest everyone", button))
                foreach (var v in kingdom.Villagers) if (v != null) v.SetNeeds(1f, 1f);
            if (GUILayout.Button("Make everyone hungry and tired", button))
                foreach (var v in kingdom.Villagers) if (v != null) v.SetNeeds(0.1f, 0.1f);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            var gate = Villagers.Survivors.Current;
            GUI.enabled = gate != null;
            if (GUILayout.Button("A survivor comes", button))
                Say(gate.Arrive(1, 0, false) > 0 ? "A survivor is coming to your throne (healthy)" : "No room (build a bed) or nowhere for them to come from");
            if (GUILayout.Button("A bitten survivor comes", button))
                Say(gate.Arrive(1, 0, true) > 0 ? "A bitten survivor is coming: take them in to see them turn" : "No room (build a bed) or nowhere for them to come from");
            GUI.enabled = true;
            if (GUILayout.Button("Bitten villagers turn in 10 s", button))
            {
                int n = 0;
                foreach (var v in kingdom.Villagers) if (v != null && v.TurnsIn >= 0f) { v.Infect(10f); n++; }
                Say(n > 0 ? $"{n} villager{(n == 1 ? "" : "s")} will turn in 10 seconds" : "Nobody here is bitten");
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Hurt everyone (to try the infirmary)", button))
                foreach (var v in kingdom.Villagers) if (v != null && v.Health != null && !v.Health.Dead) v.Health.SetCurrent(v.Health.Max * 0.25f);
            if (GUILayout.Button("Soldiers +100 experience", button))
            {
                int n = 0;
                foreach (var v in kingdom.Villagers) if (v != null && v.IsSoldier) { v.GiveXp(100f); n++; }
                Say(n > 0 ? $"{n} soldier{(n == 1 ? "" : "s")} got 100 experience" : "No soldiers (V: give villagers the Soldier job)");
            }
            GUILayout.EndHorizontal();

            Heading("You");
            GUILayout.BeginHorizontal();
            var health = ruler != null ? ruler.Health : null;
            GUI.enabled = health != null;
            bool safe = health != null && health.Invulnerable;
            if (GUILayout.Button(safe ? "No damage: <b>ON</b>" : "No damage: off", button)) health.Invulnerable = !safe;
            if (GUILayout.Button("Heal", button)) health.Heal(health.Max);
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Heading("Time");
            if (dayNight != null)
            {
                GUILayout.Label($"Day {dayNight.Day}, {dayNight.ClockText()}", text);
                GUILayout.BeginHorizontal();
                foreach (var (name, hour) in new[] { ("Morning", 7f), ("Noon", 12f), ("Evening", 18.5f), ("Midnight", 0f) })
                    if (GUILayout.Button(name, button)) dayNight.SetTime(dayNight.Day, hour);
                GUILayout.EndHorizontal();
            }
            var seasons = World.Seasons.Current;
            if (seasons != null && seasons.On && dayNight != null)
            {
                GUILayout.Label(seasons.Describe(), text);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Next season", button)) dayNight.SetTime(dayNight.Day + seasons.DaysLeft + 1, dayNight.Hour);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Weather:", text, GUILayout.Width(font * 7f));
                if (GUILayout.Button(seasons.Forced == null ? "<b>The sky's</b>" : "The sky's", button)) seasons.Forced = null;
                foreach (World.Weather sky in System.Enum.GetValues(typeof(World.Weather)))
                    if (GUILayout.Button(seasons.Forced == sky ? $"<b>{sky}</b>" : sky.ToString(), button)) seasons.Forced = sky;
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Game speed:", text, GUILayout.Width(font * 7f));
            foreach (float s in Speeds)
                if (GUILayout.Button(Mathf.Approximately(speed, s) ? $"<b>{s:0}x</b>" : $"{s:0}x", button)) speed = s;
            GUILayout.EndHorizontal();

            Waves();
            Zombies();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void Heading(string name)
        {
            GUILayout.Space(text.fontSize * 0.6f);
            GUILayout.Label($"<b>{name}</b>", text);
        }

        void Ages()
        {
            Heading("Age");
            if (research == null || research.AgeCount == 0)
            {
                GUILayout.Label("No ages set up.", text);
                return;
            }
            if (research.Mode == Research.Unlocking.Everything)
            {
                GUILayout.Label("Free build: everything is unlocked already.", text);
                return;
            }
            GUILayout.Label($"Now: {research.CurrentAge.displayName}. Jump to (everything before it is researched):", text);
            for (int i = 0; i < research.AgeCount; i += 2)
            {
                GUILayout.BeginHorizontal();
                for (int a = i; a < i + 2 && a < research.AgeCount; a++)
                {
                    var age = research.AgeAt(a);
                    string name = age != null ? age.displayName : $"Age {a + 1}";
                    if (GUILayout.Button(a == research.Age ? $"<b>{name}</b>" : name, button))
                    {
                        research.CheatToAge(a);
                        Say($"Now in the {name}.");
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        void Waves()
        {
            if (waves == null) return;
            Heading("Waves (survival)");
            GUILayout.Label($"Wave now: {waves.Wave}   ({waves.Now})", text);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("-", button, GUILayout.Width(text.fontSize * 2.5f))) waveToStart = Mathf.Max(1, waveToStart - 1);
            GUILayout.Label($"<b>{waveToStart}</b>", text, GUILayout.Width(text.fontSize * 3f));
            if (GUILayout.Button("+", button, GUILayout.Width(text.fontSize * 2.5f))) waveToStart++;
            if (GUILayout.Button($"Start wave {waveToStart} now", button)) waves.CheatStartWave(waveToStart);
            GUILayout.EndHorizontal();
        }

        void Zombies()
        {
            if (zombies == null) return;
            Heading("Zombies");
            GUILayout.Label($"Alive: {zombies.Alive}. Sent ones come from the edge of the dark, like a wave.", text);
            if (GUILayout.Button("Remove all zombies (no kills, no bursts)", button))
                foreach (var z in new List<Zombie>(zombies.Zombies)) zombies.Vanish(z);
            var boss = zombies.BossKind;
            if (boss != null && GUILayout.Button($"+1 {boss.displayName} (the boss)", button)) Send(boss, 1);
            for (int i = 0; i < kinds.Length; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (int k = i; k < i + 3 && k < kinds.Length; k++)
                {
                    var kind = kinds[k];
                    if (kind != null && GUILayout.Button($"+5 {kind.displayName}", button)) Send(kind, 5);
                }
                GUILayout.EndHorizontal();
            }
        }

        // Fills the storehouses (as far as they have room) with some of everything.
        void Give(int amount)
        {
            var near = kingdom.Throne != null ? kingdom.Throne.Center : Vector2.zero;
            bool full = false;
            for (int i = 0; i < Inventory.TypeCount; i++)
                if (kingdom.Store((ResourceType)i, amount, near) < amount) full = true;
            Say(full ? "The storehouses are full: build more storehouses to hold more." : $"+{amount:N0} of everything.");
        }

        // Every blueprint gets its resources and its work, so the kingdom finishes them straight away.
        void FinishBuilding()
        {
            int n = 0;
            foreach (var b in new List<Blueprint>(kingdom.Blueprints))
            {
                if (b == null || b.Cancelled) continue;
                for (int i = 0; i < Inventory.TypeCount; i++) b.Deliver((ResourceType)i, b.Missing((ResourceType)i));
                b.AddWork(b.Config.buildSeconds);
                n++;
            }
            Say(n == 0 ? "Nothing is waiting to be built." : $"{n} finished.");
        }

        void Send(ZombieConfig kind, int count)
        {
            if (!zombies.FindSpawnPoint(Random.value, 14f, out var spot))
            {
                Say("There's nowhere for them to come from right now.");
                return;
            }
            for (int i = 0; i < count; i++) zombies.Spawn(kind, spot + Random.insideUnitCircle * 0.8f, 1f);
            Say($"{count} {kind.displayName}s are coming.");
        }
    }
}
