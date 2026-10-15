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
    /// of day, game speed, no damage for the ruler, building and research finished at once. Only in games
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

        void Say(string what)
        {
            message = what;
            messageUntil = Time.unscaledTime + 4f;
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

            Ages();

            Heading("Building and research");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Finish all building now", button)) FinishBuilding();
            GUI.enabled = research != null && research.Researching != null;
            if (GUILayout.Button("Finish research now", button)) research.CheatFinishResearch();
            GUI.enabled = true;
            GUILayout.EndHorizontal();

            Heading("Villagers");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Feed and rest everyone", button))
                foreach (var v in kingdom.Villagers) if (v != null) v.SetNeeds(1f, 1f);
            if (GUILayout.Button("Make everyone hungry and tired", button))
                foreach (var v in kingdom.Villagers) if (v != null) v.SetNeeds(0.1f, 0.1f);
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
