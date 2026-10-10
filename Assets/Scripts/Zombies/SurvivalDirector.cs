using System.Collections.Generic;
using KingdomGame.Building;
using UnityEngine;

namespace KingdomGame.Zombies
{
    /// <summary>
    /// The survival game: time to build first, then wave after wave of zombies
    /// that never stops growing, with a short calm between waves when survivors
    /// come to join you (if you have beds for them). Each wave comes out of the
    /// dark from one or more sides of the map. It's over when the ruler falls.
    /// N calls the next wave early.
    /// </summary>
    public sealed class SurvivalDirector : MonoBehaviour
    {
        public enum Phase
        {
            /// <summary>Building before the first wave.</summary>
            Preparing,
            /// <summary>A wave is coming or still alive.</summary>
            Fighting,
            /// <summary>The calm between waves.</summary>
            Calm,
            /// <summary>The ruler fell.</summary>
            Over,
        }

        SurvivalConfig config;
        ZombieDirector zombies;
        Kingdom kingdom;
        readonly List<float> entries = new List<float>();
        int[] firstWaves = new int[0];
        float[] weights = new float[0];
        float spawnClock, announceUntil;
        int spawned;

        public Phase Now { get; private set; }
        /// <summary>The current wave (or the last one, between waves). 0 before the first.</summary>
        public int Wave { get; private set; }
        /// <summary>Seconds until the next wave (while preparing or calm).</summary>
        public float TimeLeft { get; private set; }
        /// <summary>Zombies of this wave still to come out of the dark.</summary>
        public int ToCome { get; private set; }
        /// <summary>Zombies of this wave still to come or still alive.</summary>
        public int ZombiesLeft => ToCome + (zombies != null ? zombies.Alive : 0);
        public int Kills => zombies != null ? zombies.Kills : 0;
        public float SecondsSurvived { get; private set; }
        /// <summary>A big message for the middle of the screen ("Wave 3 is coming from the north!"), or null.</summary>
        public string Announcement { get; private set; }

        public void Setup(SurvivalConfig survival, ZombieDirector director, Kingdom owner)
        {
            config = survival;
            zombies = director;
            kingdom = owner;
            var kinds = config.zombies ?? new ZombieConfig[0];
            firstWaves = new int[kinds.Length];
            weights = new float[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                firstWaves[i] = kinds[i] != null ? kinds[i].firstWave : int.MaxValue;
                weights[i] = kinds[i] != null ? kinds[i].weight : 0f;
            }
            if (kinds.Length == 0) Debug.LogError("The Survival Config has no zombies. Run KingdomGame > Build Game Scenes again.", config);
            Now = Phase.Preparing;
            TimeLeft = config.prepSeconds;
            Announce($"Build your defences! The dead come in {Mathf.CeilToInt(TimeLeft)} seconds.", 6f);
        }

        void Update()
        {
            if (config == null || Now == Phase.Over) return;
            float dt = Time.deltaTime;
            SecondsSurvived += dt;
            if (Time.time > announceUntil) Announcement = null;

            switch (Now)
            {
                case Phase.Preparing:
                case Phase.Calm:
                    if (GameInput.Pressed(KeyCode.N) && Time.timeScale > 0f) TimeLeft = 0f;
                    TimeLeft -= dt;
                    if (TimeLeft <= 0f) StartWave(Wave + 1);
                    break;
                case Phase.Fighting:
                    SpawnSome(dt);
                    if (ToCome <= 0 && zombies.Alive == 0) EndWave();
                    break;
            }
        }

        /// <summary>The ruler fell: the game is over.</summary>
        public void RulerFell()
        {
            if (Now == Phase.Over) return;
            Now = Phase.Over;
            Announcement = null;
        }

        void StartWave(int wave)
        {
            Wave = wave;
            Now = Phase.Fighting;
            TimeLeft = 0f;
            ToCome = WaveMath.Count(wave, config.firstWaveZombies, config.extraZombiesPerWave, config.growth);
            spawned = 0;
            spawnClock = 1f;   // the first one straight away
            // Bigger waves come from more sides at once.
            entries.Clear();
            int sides = Mathf.Clamp(1 + (wave - 1) / 3, 1, 4);
            float first = Random.value;
            for (int i = 0; i < sides; i++) entries.Add(Mathf.Repeat(first + i / (float)sides + Random.Range(-0.08f, 0.08f), 1f));
            Announce($"Wave {wave}: {ToCome} zombies are coming from the {Sides()}!", 5f);
        }

        void EndWave()
        {
            Now = Phase.Calm;
            TimeLeft = config.breakSeconds;
            string news = $"Wave {Wave} survived!";
            if (kingdom != null && kingdom.Throne != null)
            {
                int room = kingdom.VillagerCapacity - kingdom.Villagers.Count;
                int joining = Mathf.Clamp(room, 0, config.survivorsPerWave);
                for (int i = 0; i < joining; i++)
                {
                    var spot = kingdom.Throne.Front + new Vector2((i - (joining - 1) * 0.5f) * 0.8f, -1.2f);
                    if (!kingdom.Nav.Walkable(spot)) kingdom.Nav.NearestFreeSpot(spot, new RectInt(0, 0, 0, 0), out spot);
                    kingdom.AddVillager(spot);
                }
                if (joining > 0) news += joining == 1 ? " A survivor joined you." : $" {joining} survivors joined you.";
                else if (room <= 0 && config.survivorsPerWave > 0) news += " Survivors are out there: build beds so they can join you.";
            }
            // Every few waves the kingdom moves into the next age: new walls, traps and towers to build.
            var research = Progression.Research.Current;
            if (research != null && research.Mode == Progression.Research.Unlocking.ByAge && research.Config != null
                && Wave % Mathf.Max(1, research.Config.survivalWavesPerAge) == 0 && research.AdvanceAge())
                news += $" Your kingdom enters the {research.CurrentAge.displayName}: new walls, traps and towers to build!";
            Announce(news, 7f);
        }

        // Zombies come out of the dark a few a second, from the wave's sides in turn.
        void SpawnSome(float dt)
        {
            spawnClock = Mathf.Min(spawnClock + dt * config.spawnPerSecond, 3f);
            var kinds = config.zombies;
            if (kinds == null || kinds.Length == 0)
            {
                ToCome = 0;
                return;
            }
            float toughness = WaveMath.Toughness(Wave, config.toughnessPerWave);
            while (spawnClock >= 1f && ToCome > 0 && zombies.Alive < config.maxAlive)
            {
                spawnClock -= 1f;
                int pick = WaveMath.Pick(Wave, firstWaves, weights, Random.value);
                var kind = pick >= 0 ? kinds[pick] : System.Array.Find(kinds, k => k != null);
                if (kind == null)
                {
                    ToCome = 0;   // no zombies set up at all
                    break;
                }
                float along = entries.Count > 0 ? entries[spawned % entries.Count] : Random.value;
                if (!zombies.FindSpawnPoint(along + Random.Range(-0.012f, 0.012f), config.spawnDistance, out var at))
                    break;   // nowhere to come from right now: try again next frame
                zombies.Spawn(kind, at, toughness);
                spawned++;
                ToCome--;
            }
        }

        // "north", "north and east", ...: where this wave's sides are (0..1 round the edge from the bottom-left corner, anticlockwise).
        string Sides()
        {
            var names = new List<string>(4);
            foreach (float along in entries)
            {
                string name = along < 0.25f ? "south" : along < 0.5f ? "east" : along < 0.75f ? "north" : "west";
                if (!names.Contains(name)) names.Add(name);
            }
            if (names.Count == 0) return "dark";
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.GetRange(0, names.Count - 1)) + " and " + names[names.Count - 1];
        }

        void Announce(string text, float seconds)
        {
            Announcement = text;
            announceUntil = Time.time + seconds;
        }
    }
}
