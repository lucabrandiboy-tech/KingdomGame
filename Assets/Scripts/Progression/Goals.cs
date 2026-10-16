using System.Collections.Generic;
using KingdomGame.Building;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Progression
{
    /// <summary>
    /// The goals of each age (<see cref="AgeConfig.goals"/>): a short list on screen of what to aim for next (J hides it),
    /// checked every second; each one done pays its reward into storage (and XP). The list is the earliest age's goals
    /// that aren't all done yet, up to the age you've reached (free build: any age). What's done is saved.
    /// One per game (<see cref="Current"/>).
    /// </summary>
    public sealed class Goals : MonoBehaviour
    {
        ProgressionConfig config;
        Kingdom kingdom;
        Research research;
        World.DayNightCycle dayNight;
        Zombies.ZombieDirector zombies;
        Zombies.SurvivalDirector waves;
        World.Wildlife watching;
        readonly HashSet<string> done = new HashSet<string>();
        float checkAt;
        bool visible = true;
        GUIStyle box;

        public static Goals Current { get; private set; }
        /// <summary>Deer killed this game (by anyone).</summary>
        public int DeerHunted { get; private set; }
        /// <summary>Bosses killed this game.</summary>
        public int BossesKilled { get; private set; }

        public void Setup(ProgressionConfig progression, Kingdom owner, Research ages, World.DayNightCycle clock,
            Zombies.ZombieDirector director, Zombies.SurvivalDirector survival)
        {
            config = progression;
            kingdom = owner;
            research = ages;
            dayNight = clock;
            zombies = director;
            waves = survival;
            Current = this;
            if (zombies != null) zombies.ZombieDied += OnZombieDied;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (zombies != null) zombies.ZombieDied -= OnZombieDied;
            if (watching != null) watching.Killed -= OnAnimalKilled;
        }

        void OnZombieDied(Zombies.Zombie z)
        {
            if (z != null && z.Config.boss) BossesKilled++;
        }

        void OnAnimalKilled(World.Animal a)
        {
            if (a != null && !a.IsWolf) DeerHunted++;
        }

        // ------------------------------------------------------------ saving --

        [System.Serializable]
        public sealed class Saved
        {
            public List<string> done = new List<string>();
            public int deer, bosses;
        }

        public Saved Save()
        {
            var s = new Saved { deer = DeerHunted, bosses = BossesKilled };
            s.done.AddRange(done);
            return s;
        }

        public void Load(Saved s)
        {
            if (s == null) return;
            done.Clear();
            if (s.done != null) foreach (var id in s.done) if (!string.IsNullOrEmpty(id)) done.Add(id);
            DeerHunted = Mathf.Max(0, s.deer);
            BossesKilled = Mathf.Max(0, s.bosses);
        }

        // ------------------------------------------------------------- goals --

        int AgeCount => config != null && config.ages != null ? config.ages.Length : 0;

        // The last age whose goals count: the one you've reached (free build: all of them).
        int Reached => research == null ? 0 : research.Mode == Research.Unlocking.Everything ? AgeCount - 1 : research.Age;

        static string Id(int age, int index) => $"{age}:{index}";

        // Goals about waves only count in survival.
        bool Applies(AgeGoal g) => g != null && (g.kind != GoalKind.Waves || waves != null);

        bool Done(int age, int index) => done.Contains(Id(age, index));

        /// <summary>The age whose goals are on screen: the earliest one reached that still has goals to do, or -1.</summary>
        public int ShownAge
        {
            get
            {
                for (int a = 0; a <= Reached && a < AgeCount; a++)
                {
                    var goals = config.ages[a] != null ? config.ages[a].goals : null;
                    if (goals == null) continue;
                    for (int i = 0; i < goals.Length; i++) if (Applies(goals[i]) && !Done(a, i)) return a;
                }
                return -1;
            }
        }

        /// <summary>How far along a goal is (it's done at its amount).</summary>
        public int Progress(AgeGoal g)
        {
            switch (g.kind)
            {
                case GoalKind.Villagers: return kingdom.Villagers.Count;
                case GoalKind.Stock: return kingdom.Stock(g.resource);
                case GoalKind.Built:
                {
                    int n = 0;
                    foreach (var b in kingdom.AllBuilt()) if (b != null && (g.building == null || b.Config == g.building)) n++;
                    return n;
                }
                case GoalKind.WallTiles:
                {
                    var world = Destruction.PixelWorld.Current;
                    if (world == null) return 0;
                    int n = 0;
                    foreach (var cell in world.ChunkCells)
                    {
                        var wall = world.ChunkAt(cell)?.Owner;
                        if (wall != null && !wall.Remote) n++;
                    }
                    return n;
                }
                case GoalKind.Rooms: return kingdom.Rooms.Count;
                case GoalKind.Towers:
                {
                    int n = 0;
                    foreach (var d in Defenses.Defense.All) if (d != null && d.Config != null && d.Config.kind == Defenses.DefenseKind.Tower) n++;
                    return n;
                }
                case GoalKind.Kills: return zombies != null ? zombies.Kills : 0;
                case GoalKind.Bosses: return BossesKilled;
                case GoalKind.Nights: return dayNight != null ? dayNight.Day - 1 : 0;
                case GoalKind.Waves:
                    if (waves == null) return 0;
                    return waves.Now == Zombies.SurvivalDirector.Phase.Fighting ? waves.Wave - 1 : waves.Wave;
                case GoalKind.Ruins: return World.Ruins.Current != null ? World.Ruins.Current.SearchedCount : 0;
                case GoalKind.Deer: return DeerHunted;
                default: return 0;
            }
        }

        void Update()
        {
            if (GameInput.Pressed(KeyCode.J)) visible = !visible;
            if (watching == null && World.Wildlife.Current != null)
            {
                watching = World.Wildlife.Current;
                watching.Killed += OnAnimalKilled;
            }
            if (config == null || kingdom == null || Time.time < checkAt) return;
            checkAt = Time.time + 1f;
            for (int a = 0; a <= Reached && a < AgeCount; a++)
            {
                var goals = config.ages[a] != null ? config.ages[a].goals : null;
                if (goals == null) continue;
                for (int i = 0; i < goals.Length; i++)
                {
                    var g = goals[i];
                    if (!Applies(g) || Done(a, i) || Progress(g) < g.amount) continue;
                    Complete(a, i, g);
                }
            }
        }

        // A goal is done: its reward goes into storage (what doesn't fit is left in a bag by the throne).
        void Complete(int age, int index, AgeGoal g)
        {
            done.Add(Id(age, index));
            var near = kingdom.Throne != null ? kingdom.Throne.Front : Vector2.zero;
            var left = new int[Inventory.TypeCount];
            void Give(ResourceType type, int amount)
            {
                if (amount <= 0) return;
                left[(int)type] += amount - kingdom.Store(type, amount, near);
            }
            Give(ResourceType.Wood, g.wood);
            Give(ResourceType.Stone, g.stone);
            Give(ResourceType.Food, g.food);
            Give(g.extra, g.extraAmount);
            LootBag.Drop(near, left);
            if (g.xp > 0) kingdom.AddXp(g.xp, $"Goal done: {g.text}");
            kingdom.Report($"Goal done: {g.text}! Reward: {g.Reward()}.");
            Audio.Sounds.PlayHere(Audio.Sound.Chime);
        }

        // ------------------------------------------------------------ screen --

        void OnGUI()
        {
            if (!visible || config == null) return;
            int age = ShownAge;
            if (age < 0) return;
            var goals = config.ages[age].goals;
            int font = Mathf.Max(11, Screen.height / 60);
            if (box == null) box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, wordWrap = true };
            box.fontSize = font;
            var text = new System.Text.StringBuilder();
            text.Append($"<b>Goals: {config.ages[age].displayName}</b>   <color=#8a8478>(J hides)</color>");
            for (int i = 0; i < goals.Length; i++)
            {
                var g = goals[i];
                if (!Applies(g) || Done(age, i)) continue;
                int have = Mathf.Min(Progress(g), g.amount);
                text.Append($"\n• {g.text}  <color=#f0d08a>{have}/{g.amount}</color>  <color=#8a8478>({g.Reward()})</color>");
            }
            float w = Mathf.Min(Screen.width * 0.32f, font * 26f);
            var content = new GUIContent(text.ToString());
            float h = box.CalcHeight(content, w) + font * 0.3f;
            var rect = new Rect(10f, Screen.height * 0.27f, w, h);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, content, box);
        }
    }
}
