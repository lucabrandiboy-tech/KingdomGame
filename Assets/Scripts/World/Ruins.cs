using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Building;
using KingdomGame.Economy;
using KingdomGame.Zombies;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// Ruins out in the endless world (<see cref="RuinLayout"/> says where and what): abandoned villages, wrecked
    /// caravans and fallen watchtowers, each with chests or crates to search (E) and a camp of zombies guarding it.
    /// They appear as the ruler comes near and go again behind them; what's been searched and how many guards are
    /// left are saved. Loot is richer the further out a ruin is; some hold a lost blueprint (half a research's time).
    /// Guards only come inside the kingdom's lands (zombies can't go further); further out, ruins are unguarded.
    /// One per game (<see cref="Current"/>), in your own game or one you host.
    /// </summary>
    public sealed class Ruins : MonoBehaviour
    {
        const float CheckSeconds = 0.5f;
        const float ShowWithin = 55f, HideBeyond = 75f;      // tiles past a ruin's edge
        const float GuardsWithin = 40f, GuardsGoBeyond = 70f;
        const float KeepClear = 55f;                          // no ruins this close to your throne
        const float SearchReach = 1.4f;
        const int ClearXp = 25, BlueprintXp = 60;

        /// <summary>What's happened at a ruin (saved).</summary>
        [System.Serializable]
        public sealed class State
        {
            public string id;
            /// <summary>Guards still there (-1: as many as it started with).</summary>
            public int guardsLeft = -1;
            /// <summary>Which of its chests and crates have been searched (one bit each).</summary>
            public int searched;
            /// <summary>The ruler has been there.</summary>
            public bool found;
        }

        sealed class Shown
        {
            public RuinSite Site;
            public State State;
            public GameObject Root;
            public SpriteRenderer[] Caches;
            public readonly List<Zombie> Guards = new List<Zombie>();
            public bool Watched;   // in the zombies' window (KeepInView)
        }

        uint seed;
        Kingdom kingdom;
        ZombieDirector director;
        SurvivalDirector waves;
        float checkAt;
        readonly Dictionary<Vector2Int, RuinSite> sites = new Dictionary<Vector2Int, RuinSite>();
        readonly Dictionary<string, State> states = new Dictionary<string, State>();
        readonly Dictionary<string, Shown> shown = new Dictionary<string, Shown>();
        readonly List<string> scratch = new List<string>();
        Shown nearSite;
        int nearCache = -1;
        GUIStyle hint;

        public static Ruins Current { get; private set; }

        public void Setup(int worldSeed, Kingdom owner, ZombieDirector zombies, SurvivalDirector survival)
        {
            seed = (uint)worldSeed;
            kingdom = owner;
            director = zombies;
            waves = survival;
            Current = this;
            if (director != null) director.ZombieDied += OnZombieDied;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (director != null) director.ZombieDied -= OnZombieDied;
        }

        // ---------------------------------------------------------- saving --

        /// <summary>What's happened at the ruins so far (for the save).</summary>
        public List<State> Save()
        {
            var list = new List<State>();
            foreach (var s in states.Values) if (s.found || s.searched != 0 || s.guardsLeft >= 0) list.Add(s);
            return list;
        }

        /// <summary>Puts back what a saved game says (before anything is shown).</summary>
        public void Load(List<State> saved)
        {
            if (saved == null) return;
            foreach (var s in saved) if (s != null && !string.IsNullOrEmpty(s.id)) states[s.id] = s;
            foreach (var pair in shown)
                if (states.TryGetValue(pair.Key, out var s)) pair.Value.State = s;
        }

        State StateOf(RuinSite site)
        {
            if (!states.TryGetValue(site.Id, out var s)) states[site.Id] = s = new State { id = site.Id };
            return s;
        }

        // ------------------------------------------------------------ frame --

        void Update()
        {
            var ruler = kingdom != null ? kingdom.Ruler : null;
            if (ruler == null) return;
            Vector2 at = ruler.position;
            if (Time.time >= checkAt)
            {
                checkAt = Time.time + CheckSeconds;
                if (NatureMap.Inside(new Vector2Int(Mathf.FloorToInt(at.x), Mathf.FloorToInt(at.y)))) LookAround(at);
            }
            Search(at);
        }

        // Shows the ruins near the ruler, hides those far behind, and sends out or calls back their guards.
        void LookAround(Vector2 at)
        {
            var region = RuinLayout.RegionOf(at);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var site = SiteAt(new Vector2Int(region.x + dx, region.y + dy));
                    if (site == null || shown.ContainsKey(site.Id)) continue;
                    if ((site.Center - at).magnitude - site.Radius < ShowWithin) Show(site);
                }
            scratch.Clear();
            foreach (var pair in shown)
            {
                var s = pair.Value;
                float d = (s.Site.Center - at).magnitude - s.Site.Radius;
                if (d > HideBeyond) { scratch.Add(pair.Key); continue; }
                if (!s.State.found && d < 25f)
                {
                    s.State.found = true;
                    kingdom.Report($"You've found {s.Site.Name}. Search it with E" + (GuardsLeft(s) > 0 && Guarded(s) ? ", but zombies guard it." : "."));
                }
                if (s.Guards.Count == 0 && GuardsLeft(s) > 0 && d < GuardsWithin) SendGuards(s);
                else if (s.Guards.Count > 0 && d > GuardsGoBeyond) CallBackGuards(s);
            }
            foreach (var id in scratch) Hide(shown[id]);
        }

        // The ruin in a region (worked out once), or null: none there, or too close to the throne.
        RuinSite SiteAt(Vector2Int region)
        {
            if (sites.TryGetValue(region, out var site)) return site;
            site = RuinLayout.At(seed, region);
            if (site != null && kingdom != null && kingdom.Throne != null && (site.Center - kingdom.Throne.Center).magnitude - site.Radius < KeepClear) site = null;
            sites[region] = site;
            return site;
        }

        void Show(RuinSite site)
        {
            var s = new Shown { Site = site, State = StateOf(site) };
            // Trees and rocks don't grow through ruins.
            var nature = NatureMap.Current;
            if (nature != null) foreach (var cell in site.Footprint) if (nature.Blocks(cell)) nature.Clear(cell);
            s.Root = new GameObject($"Ruin {site.Id} ({site.Kind})");
            s.Root.transform.SetParent(transform, false);
            foreach (var p in site.Pieces) Place(s.Root.transform, p.Art, p.Rotation, p.At, p.Layer, p.Height);
            s.Caches = new SpriteRenderer[site.Caches.Count];
            for (int i = 0; i < site.Caches.Count; i++)
            {
                var c = site.Caches[i];
                s.Caches[i] = Place(s.Root.transform, c.Art, c.Rotation, c.At, RuinLayer.Standing, 0.3f);
                if (Searched(s, i) && s.Caches[i] != null) s.Caches[i].color = new Color(0.55f, 0.52f, 0.5f);   // searched: opened and dark
            }
            shown[site.Id] = s;
        }

        SpriteRenderer Place(Transform parent, string art, int rotation, Vector2 at, RuinLayer layer, float height)
        {
            string key = ArtKeys.Turned(art, rotation);
            var go = new GameObject(art);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = ArtSprites.Get(key);
            r.sortingOrder = layer == RuinLayer.Floor ? SortOrder.Floors - 5 : layer == RuinLayer.Low ? SortOrder.Nature - 1 : SortOrder.Furniture;
            if (layer != RuinLayer.Floor) Look.AddShadow(go.transform, ArtSprites.Get(ArtKeys.Shadow(key)), height);
            return r;
        }

        void Hide(Shown s)
        {
            CallBackGuards(s);
            if (s.Root != null) Destroy(s.Root);
            shown.Remove(s.Site.Id);
            if (nearSite == s) nearSite = null;
        }

        // ----------------------------------------------------------- guards --

        int GuardsLeft(Shown s) => s.State.guardsLeft < 0 ? s.Site.Guards : s.State.guardsLeft;

        // Whether zombies can be there: inside the kingdom's lands (the zombies' paths don't reach further).
        bool Guarded(Shown s) => director != null && director.Covers(s.Site.Center);

        void SendGuards(Shown s)
        {
            if (!Guarded(s)) return;
            director.KeepInView.Add(s.Site.Center);
            s.Watched = true;
            director.Rewindow();
            int n = GuardsLeft(s);
            for (int i = 0; i < n; i++)
            {
                var kind = GuardKind();
                if (kind == null) break;
                var spot = s.Site.Center + Random.insideUnitCircle * Mathf.Min(5f, s.Site.Radius);
                if (!director.Walkable(director.CellOf(spot))) spot = director.NearestOpenSpot(spot);
                var z = director.Spawn(kind, spot, waves != null ? WaveMath.Toughness(Mathf.Max(1, waves.Wave), 0.06f) : 1f);
                if (z == null) continue;
                z.Camp(s.Site.Center, Mathf.Clamp(s.Site.Radius * 0.6f, 4f, 8f), 9f, true);
                s.Guards.Add(z);
            }
        }

        // The ruler went far away: the guards go back into hiding (they're still there when you come back).
        void CallBackGuards(Shown s)
        {
            foreach (var z in s.Guards) if (z != null && z.Health != null && !z.Health.Dead) director.Vanish(z);
            s.Guards.Clear();
            Unwatch(s);
        }

        void Unwatch(Shown s)
        {
            if (!s.Watched || director == null) return;
            s.Watched = false;
            director.KeepInView.Remove(s.Site.Center);
            director.Rewindow();
        }

        // What guards a ruin: the kinds the waves have got to (survival), or the first few kinds (free build).
        ZombieConfig GuardKind()
        {
            if (waves != null) return waves.PickKind(Mathf.Max(3, waves.Wave + 1));
            var kinds = director.Kinds;
            if (kinds == null || kinds.Count == 0) return null;
            for (int tries = 0; tries < 8; tries++)
            {
                var k = kinds[Random.Range(0, Mathf.Min(3, kinds.Count))];
                if (k != null && !k.boss) return k;
            }
            return director.TurnedKind;
        }

        void OnZombieDied(Zombie z)
        {
            foreach (var s in shown.Values)
            {
                if (!s.Guards.Remove(z)) continue;
                s.State.guardsLeft = Mathf.Max(0, GuardsLeft(s) - 1);
                if (s.State.guardsLeft == 0)
                {
                    Unwatch(s);
                    kingdom.AddXp(ClearXp, $"The {s.Site.Kind.ToString().ToLowerInvariant()} ruins are clear");
                }
                return;
            }
        }

        // ---------------------------------------------------------- searching --

        bool Searched(Shown s, int cache) => (s.State.searched & (1 << cache)) != 0;

        // The chest or crate the ruler is next to (if not searched yet), and E to search it.
        void Search(Vector2 at)
        {
            nearSite = null;
            nearCache = -1;
            foreach (var s in shown.Values)
            {
                if ((s.Site.Center - at).sqrMagnitude > (s.Site.Radius + 2f) * (s.Site.Radius + 2f)) continue;
                for (int i = 0; i < s.Site.Caches.Count; i++)
                    if (!Searched(s, i) && (s.Site.Caches[i].At - at).sqrMagnitude < SearchReach * SearchReach)
                    {
                        nearSite = s;
                        nearCache = i;
                    }
            }
            if (nearSite != null && Time.timeScale > 0f && GameInput.Pressed(KeyCode.E)) Open(nearSite, nearCache);
        }

        void Open(Shown s, int index)
        {
            var cache = s.Site.Caches[index];
            s.State.searched |= 1 << index;
            if (s.Caches[index] != null) s.Caches[index].color = new Color(0.55f, 0.52f, 0.5f);
            // Richer the further out it is.
            float far = kingdom != null && kingdom.Throne != null ? (s.Site.Center - kingdom.Throne.Center).magnitude : 100f;
            float m = 1f + far / 200f;
            var counts = new int[Inventory.TypeCount];
            int R(float a, float b) => Mathf.RoundToInt(Random.Range(a, b) * m);
            switch (s.Site.Kind)
            {
                case RuinKind.Village:
                    counts[(int)ResourceType.Wood] = R(20, 45);
                    counts[(int)ResourceType.Stone] = R(20, 45);
                    counts[(int)ResourceType.Food] = R(10, 30);
                    if (Random.value < 0.35f) counts[(int)(Random.value < 0.5f ? ResourceType.Copper : ResourceType.Iron)] = R(8, 20);
                    break;
                case RuinKind.Caravan:
                    counts[(int)ResourceType.Food] = R(25, 50);
                    counts[(int)new[] { ResourceType.Copper, ResourceType.Iron, ResourceType.Gold, ResourceType.Coal }[Random.Range(0, 4)]] = R(10, 25);
                    if (Random.value < 0.3f) counts[(int)ResourceType.Steel] = R(5, 12);
                    break;
                default:
                    counts[(int)ResourceType.Iron] = R(15, 35);
                    counts[(int)ResourceType.Gold] = R(6, 14);
                    counts[(int)ResourceType.Stone] = R(20, 40);
                    break;
            }
            LootBag.Drop(cache.At + new Vector2(0f, -0.7f), counts);
            Audio.Sounds.Play(Audio.Sound.Loot, cache.At);
            string found = "You search it and find supplies (the bag at your feet)";
            if (cache.Blueprint)
            {
                var research = Progression.Research.Current;
                if (research != null && research.Mode == Progression.Research.Unlocking.ByResearch)
                {
                    research.AddBlueprint();
                    found += ", and a lost blueprint! It halves your next research (T)";
                }
                else
                {
                    kingdom.AddXp(BlueprintXp, "A lost blueprint");
                    found += ", and a lost blueprint (worth XP here)";
                }
            }
            kingdom.Report(found + ".");
        }

        void OnGUI()
        {
            if (nearSite == null || nearCache < 0) return;
            int font = Mathf.Max(12, Screen.height / 50);
            if (hint == null) hint = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, richText = true };
            hint.fontSize = font;
            string what = nearSite.Site.Caches[nearCache].Art == FurnitureArt.Crates ? "the crates" : "the old chest";
            var content = new GUIContent($"<b>E</b>: search {what}");
            var size = hint.CalcSize(content) + new Vector2(font, font * 0.5f);
            GUI.Box(new Rect((Screen.width - size.x) / 2f, Screen.height * 0.72f, size.x, size.y), content, hint);
        }
    }
}
