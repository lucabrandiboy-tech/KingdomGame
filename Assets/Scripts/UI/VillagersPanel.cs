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
            float w = Mathf.Min(Screen.width - 40f, font * 40f), h = font * 7f + row * (jobs.Length + 1);
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
        }
    }
}
