using KingdomGame.Building;
using KingdomGame.Economy;
using KingdomGame.Villagers;
using UnityEngine;

namespace KingdomGame.UI
{
    /// <summary>
    /// The villagers panel (V): how many villagers do each job, with - and + to move
    /// them between a job and "no job". New survivors start with no job. The game
    /// keeps running while it's open.
    /// </summary>
    public sealed class VillagersPanel : MonoBehaviour
    {
        Kingdom kingdom;
        GUIStyle title, text, small, button;

        public bool IsOpen { get; private set; }

        public void Setup(Kingdom owner) => kingdom = owner;

        public void Toggle()
        {
            IsOpen = !IsOpen;
            UiFocus.PanelOpen = IsOpen;
        }

        void Update()
        {
            if (kingdom == null || Time.timeScale <= 0f) return;
            // Not while another panel has the keys (the research screen).
            if (UiFocus.PanelOpen && !IsOpen) return;
            if (GameInput.Pressed(KeyCode.V) || (IsOpen && GameInput.Pressed(KeyCode.Escape))) Toggle();
        }

        void OnDisable()
        {
            if (IsOpen) UiFocus.PanelOpen = false;
        }

        // How fed and rested the villagers are, and what helps: food in storage, a kitchen, beds, a dining hall.
        void DrawNeeds(float x, float y, float width, int font)
        {
            int fedOk = 0, hungry = 0, starving = 0, restedOk = 0, tired = 0, exhausted = 0, n = 0;
            foreach (var v in kingdom.Villagers)
            {
                if (v == null) continue;
                n++;
                if (v.Fed <= 0f) starving++;
                else if (v.Fed < NeedsMath.Low) hungry++;
                else fedOk++;
                if (v.Rested <= 0f) exhausted++;
                else if (v.Rested < NeedsMath.Low) tired++;
                else restedOk++;
            }
            string Bad(int count, string what, string color) => count > 0 ? $", <color={color}>{count} {what}</color>" : "";
            GUI.Label(new Rect(x, y, width, font * 1.8f),
                $"<b>Food and sleep</b>   Fed: {fedOk}{Bad(hungry, "hungry", "#ffcc80")}{Bad(starving, "starving", "#ff8a80")}" +
                $"   Rested: {restedOk}{Bad(tired, "tired", "#ffcc80")}{Bad(exhausted, "exhausted", "#ff8a80")}", text);
            y += font * 1.9f;

            var settings = kingdom.VillagerSettings;
            int meal = kingdom.MealFood, food = kingdom.Stock(ResourceType.Food);
            // Meals a day each: one whenever they've gone from full down to hungry.
            float perDay = n * meal * 24f / Mathf.Max(1f, settings.hungerHours * (1f - NeedsMath.EatBelow));
            string lasts = n == 0 ? "" : perDay <= 0f ? "" : food <= 0 ? ": <color=#ff8a80>none left, send foragers</color>" : $", enough for about {food / perDay:0.#} days";
            string kitchen = kingdom.HasKitchen ? "cooked: you have a kitchen" : $"build a cauldron or hearth and cooked meals take {settings.cookedMealFood}";
            int ground = Mathf.Max(0, n - kingdom.Beds);
            string beds = ground > 0 ? $"Beds for {kingdom.Beds}: <color=#ffcc80>{ground} sleep on the ground</color> by the throne and wake up tired."
                : $"Beds for {kingdom.Beds}: everyone has one (sleeping in a bed heals too).";
            string hall = kingdom.HasDiningHall ? "Dining hall: fed, rested villagers work 10% faster."
                : "A dining hall (a room with a table and a seat) makes fed, rested villagers work 10% faster.";
            float firewood = kingdom.FirewoodPerDay;
            string winter = firewood > 0f
                ? (kingdom.Cold ? $" <color=#9fd0ff>Winter: they burn {firewood:0} wood a day to keep warm, and it's run out: they're cold and slow.</color>"
                                : $" Winter: they burn {firewood:0} wood a day to keep warm.")
                : "";
            GUI.Label(new Rect(x, y, width, font * 4.2f),
                $"Meals take {meal} food ({kitchen}). Food in storage: {food}{lasts}. They sleep at night (guards by day). {beds} {hall}{winter}", small);
        }

        void OnGUI()
        {
            if (!IsOpen || kingdom == null) return;
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

            var jobs = Jobs.Listed;
            float row = font * 2.6f;
            bool needs = kingdom.VillagerSettings != null && kingdom.VillagerSettings.needs;
            float w = Mathf.Min(Screen.width - 40f, font * 40f), h = font * 7f + row * (jobs.Length + 1) + font * 2.6f * (Kingdom.UpgradeKinds + 1)
                + (needs ? font * 6.2f : 0f);
            var panel = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(panel, GUIContent.none);
            GUI.Box(panel, GUIContent.none);   // twice: darker
            float x = panel.x + font, y = panel.y + font * 0.6f, inner = w - font * 2f;

            int total = kingdom.Villagers.Count, idle = kingdom.CountOf(Job.Idle);
            GUI.Label(new Rect(x, y, inner, font * 2f), $"<b>Villagers: {total}</b>   <size={font}>(homes for {kingdom.VillagerCapacity})   V or Esc closes this</size>", title);
            y += font * 2.4f;

            foreach (var job in jobs)
            {
                int n = kingdom.CountOf(job);
                GUI.Label(new Rect(x, y, font * 9f, row * 0.5f), $"<b>{Jobs.Name(job, true)}</b>", text);
                if (GUI.Button(new Rect(x + font * 9f, y, font * 2f, font * 1.6f), "-", button) && n > 0) kingdom.ChangeJob(job, Job.Idle);
                GUI.Label(new Rect(x + font * 11.4f, y, font * 2f, font * 1.6f), $"<b>{n}</b>", text);
                bool canAdd = idle > 0;
                GUI.enabled = canAdd;
                if (GUI.Button(new Rect(x + font * 13f, y, font * 2f, font * 1.6f), "+", button)) kingdom.ChangeJob(Job.Idle, job);
                GUI.enabled = true;
                string about = Jobs.Describe(job);
                if (n > 0 && Jobs.Gathers(job, out var type))
                {
                    string problem = kingdom.GatherProblem(type);
                    if (problem != null) about = $"<color=#ff8a80>Stuck: {problem}</color>";
                }
                if (n > 0 && job == Job.Guard)
                {
                    string problem = kingdom.GuardProblem();
                    if (problem != null) about = $"<color=#ff8a80>{problem}</color>";
                }
                if (n > 0 && job == Job.Hunter)
                {
                    string problem = kingdom.HuntProblem();
                    if (problem != null) about = $"<color=#ffcc80>{problem}</color>";
                }
                if (n > 0 && job == Job.Soldier)
                {
                    string summary = kingdom.SoldierSummary();
                    if (summary != null) about = summary;
                }
                if (n > 0 && job == Job.Crafter)
                {
                    string problem = kingdom.CraftProblem();
                    if (problem != null) about = $"<color=#ffcc80>{problem}</color>";
                }
                GUI.Label(new Rect(x + font * 15.6f, y - font * 0.2f, inner - font * 15.6f, row), about, small);
                y += row;
            }

            GUI.Label(new Rect(x, y, inner, font * 1.6f), idle > 0
                ? $"<b>No job: {idle}</b>   Press + by a job to give one of them that job."
                : "<b>No job: 0</b>   Press - by a job to free someone up for another.", text);
            y += font * 2f;
            GUI.Label(new Rect(x, y, inner, font * 3f),
                "Gatherers carry what they get to the nearest storehouse or chest. Plant trees and berry bushes on the " +
                "Farming tab (throne view) so the wood and food keep coming: planted trees grow back after they're cut.", small);
            y += font * 3.2f;

            if (needs)
            {
                DrawNeeds(x, y, inner, font);
                y += font * 6.2f;
            }

            // Upgrades bought with experience (XP, from destroying zombie bases): 5% better each level, dearer each time.
            GUI.Label(new Rect(x, y, inner, font * 1.8f), $"<b>Upgrades</b>   XP: <b>{kingdom.Xp}</b>   <size={Mathf.RoundToInt(font * 0.85f)}>(destroy zombie bases for XP)</size>", text);
            y += font * 2f;
            for (int i = 0; i < Kingdom.UpgradeKinds; i++)
            {
                var u = (Kingdom.Upgrade)i;
                int level = kingdom.Level(u), cost = kingdom.UpgradeCost(u);
                string what = u == Kingdom.Upgrade.Speed ? "Walking speed" : u == Kingdom.Upgrade.Gathering ? "Gathering and digging" : "Crafting";
                GUI.Label(new Rect(x, y, inner * 0.55f, font * 1.8f), $"<b>{what}</b>: +{Mathf.RoundToInt(Kingdom.PerLevel * 100f * level)}% (level {level})", text);
                GUI.enabled = kingdom.Xp >= cost;
                if (GUI.Button(new Rect(x + inner * 0.58f, y, inner * 0.42f, font * 1.8f), $"+5% for {cost} XP", button)) kingdom.BuyUpgrade(u);
                GUI.enabled = true;
                y += font * 2.2f;
            }
        }
    }
}
