using System.Collections.Generic;
using KingdomGame.Art;
using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.World;
using UnityEngine;

namespace KingdomGame.Networking
{
    /// <summary>
    /// Online: the other players' kingdoms as they're shown on this computer, from what their games send (see
    /// <see cref="KingdomMirror"/>). Their walls are real walls (they can't be damaged here: they're theirs), their
    /// buildings are pictures that block the way like the real ones; doors open as you walk up, lamps glow at
    /// night, tower tops show. Their villagers walk about. Nothing here works or is saved: it's what their kingdoms
    /// look like. (Roofs and the insides of houses and mines aren't shown.) Only the kingdoms near the camera are
    /// made (a few hundred pieces a frame as you come near), so many big kingdoms don't slow every computer down.
    /// </summary>
    public sealed class RemoteKingdoms : MonoBehaviour
    {
        sealed class View
        {
            /// <summary>Everything the kingdom has built (kept even while it's too far away to show).</summary>
            public readonly HashSet<BuiltPiece> Data = new HashSet<BuiltPiece>();
            /// <summary>What's made here now (only while it's near).</summary>
            public readonly Dictionary<BuiltPiece, GameObject> Shown = new Dictionary<BuiltPiece, GameObject>();
            public readonly Dictionary<int, Person> Folk = new Dictionary<int, Person>();
            public bool Near, HasCenter;
            public Vector2 Center;
        }

        sealed class Person
        {
            public Transform Body;
            public SpriteRenderer Look;
            public GameObject Root;
            public readonly MotionBuffer Buffer = new MotionBuffer();
            public float Step, HeardAt;
            public bool Shown;
        }

        sealed class Door
        {
            public SpriteRenderer Look;
            public Sprite Closed, Open;
            public RectInt Cells;
        }

        /// <summary>A villager not heard of for this long (gone inside, or out of range) is hidden.</summary>
        const float FolkTimeout = 2f;
        const float DoorReach = 1.6f;
        /// <summary>A kingdom is shown while the camera is within this many tiles of its throne (its lands are 512 across).</summary>
        const float ShowRadius = 420f;
        /// <summary>Most pieces made in one frame.</summary>
        const int MakePerFrame = 300;

        readonly Dictionary<int, View> views = new Dictionary<int, View>();
        readonly Dictionary<GameObject, Door> doors = new Dictionary<GameObject, Door>();
        readonly Dictionary<string, BuildingConfig> configs = new Dictionary<string, BuildingConfig>();
        readonly Dictionary<string, StructureMaterial> materials = new Dictionary<string, StructureMaterial>();
        Kingdom local;
        PixelWorld world;
        Transform ruler;
        Sprite[,] walking, carrying;
        Sprite folkShadow, flame;
        float stepsPerSecond = 6f, checkAt;
        readonly List<(View View, BuiltPiece Piece)> toMake = new List<(View, BuiltPiece)>();
        int makeNext;

        /// <summary>The time to show villagers at (server seconds), set each frame by whoever runs the network.</summary>
        public double RenderTime { get; set; }

        /// <summary>
        /// <paramref name="localKingdom"/>: this computer's own kingdom (its pictures are used for the others' buildings).
        /// <paramref name="localRuler"/>: doors open for them.
        /// </summary>
        public void Setup(GameContent content, Kingdom localKingdom, PixelWorld pixelWorld, Transform localRuler)
        {
            local = localKingdom;
            world = pixelWorld;
            ruler = localRuler;
            void Add(BuildingConfig b)
            {
                if (b != null && !configs.ContainsKey(b.name)) configs[b.name] = b;
            }
            Add(content.storehouse);
            if (content.buildMenu != null) foreach (var b in content.buildMenu) Add(b);
            foreach (var m in content.WallMaterials())
                if (m != null && !materials.ContainsKey(m.name)) materials[m.name] = m;
            if (content.villager != null) stepsPerSecond = content.villager.stepsPerSecond;

            walking = new Sprite[CharacterArt.VillagerDirections, CharacterArt.WalkFrames];
            carrying = new Sprite[CharacterArt.VillagerDirections, CharacterArt.WalkFrames];
            for (int d = 0; d < CharacterArt.VillagerDirections; d++)
                for (int f = 0; f < CharacterArt.WalkFrames; f++)
                {
                    walking[d, f] = ArtSprites.Get(ArtKeys.Villager(false, d, f));
                    carrying[d, f] = ArtSprites.Get(ArtKeys.Villager(true, d, f));
                }
            folkShadow = ArtSprites.Get(ArtKeys.VillagerShadow);
            flame = ArtSprites.Get(ArtKeys.Flame);
        }

        View ViewOf(int kingdom)
        {
            if (!views.TryGetValue(kingdom, out var v)) views[kingdom] = v = new View();
            return v;
        }

        /// <summary>Where a kingdom's throne is (it's shown while the camera is near it).</summary>
        public void Place(KingdomInfo kingdom)
        {
            if (kingdom == null) return;
            var view = ViewOf(kingdom.Id);
            view.HasCenter = true;
            view.Center = new Vector2(kingdom.X + 1f, kingdom.Y + 1f);
            checkAt = 0f;   // look again now
        }

        /// <summary>A kingdom built or took away something (or, with <paramref name="replaceAll"/>, this is all it has).</summary>
        public void Apply(int kingdom, bool replaceAll, IReadOnlyList<BuiltPiece> removed, IReadOnlyList<BuiltPiece> added)
        {
            var view = ViewOf(kingdom);
            if (replaceAll)
            {
                view.Data.Clear();
                foreach (var go in view.Shown.Values) Take(go);
                view.Shown.Clear();
            }
            foreach (var p in removed)
            {
                view.Data.Remove(p);
                if (view.Shown.TryGetValue(p, out var go))
                {
                    Take(go);
                    view.Shown.Remove(p);
                }
            }
            foreach (var p in added)
                if (view.Data.Add(p) && view.Near) toMake.Add((view, p));
        }

        // Every half second: kingdoms the camera has come near are made, ones it's left are taken away (their
        // pieces are remembered). Then a few hundred waiting pieces are made.
        void ShowNear()
        {
            float now = Time.unscaledTime;
            if (now >= checkAt)
            {
                checkAt = now + 0.5f;
                var view = Camera.main;
                Vector2 at = view != null ? (Vector2)view.transform.position : ruler != null ? (Vector2)ruler.position : Vector2.zero;
                foreach (var v in views.Values)
                {
                    bool near = !v.HasCenter || (v.Center - at).sqrMagnitude <= ShowRadius * ShowRadius;
                    if (near == v.Near) continue;
                    v.Near = near;
                    if (near)
                        foreach (var p in v.Data) toMake.Add((v, p));
                    else
                    {
                        foreach (var go in v.Shown.Values) Take(go);
                        v.Shown.Clear();
                    }
                }
            }
            int made = 0;
            while (makeNext < toMake.Count && made < MakePerFrame)
            {
                var (v, p) = toMake[makeNext++];
                if (!v.Near || !v.Data.Contains(p) || v.Shown.ContainsKey(p)) continue;
                var go = Make(p);
                if (go != null) v.Shown[p] = go;
                made++;
            }
            if (makeNext >= toMake.Count)
            {
                toMake.Clear();
                makeNext = 0;
            }
        }

        /// <summary>Where a kingdom's villagers are, as of server time <paramref name="time"/>.</summary>
        public void HeardFolk(int kingdom, double time, IReadOnlyList<(int Index, Motion Motion)> list)
        {
            var view = ViewOf(kingdom);
            float now = Time.unscaledTime;
            foreach (var (index, motion) in list)
            {
                if (!view.Folk.TryGetValue(index, out var person)) view.Folk[index] = person = NewPerson(kingdom, index);
                person.Buffer.Add(time, motion);
                person.HeardAt = now;
            }
        }

        // ---------------------------------------------------------- building --

        void Take(GameObject go)
        {
            if (go == null) return;
            doors.Remove(go);
            var wall = go.GetComponent<DestructibleStructure>();
            if (wall != null) wall.Remove();
            else Destroy(go);
        }

        GameObject Make(BuiltPiece p)
        {
            var cells = new RectInt(p.X, p.Y, Mathf.Max(1, p.W), Mathf.Max(1, p.H));
            if (!NatureMap.Inside(cells.position) || local == null) return null;
            var nature = NatureMap.Current;
            if (p.Wall)
            {
                if (world == null || !materials.TryGetValue(p.Name ?? "", out var material) || world.ChunkAt(cells.position) != null) return null;
                if (nature != null) nature.Clear(cells.position);
                var go = new GameObject($"{p.Name} (another kingdom)");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(cells.x, cells.y, 0f);
                var wall = go.AddComponent<DestructibleStructure>();
                wall.Remote = true;
                wall.Build(world, material, Vector2Int.one);
                return go;
            }
            if (!configs.TryGetValue(p.Name ?? "", out var config)) return null;
            if (config.kind == BuildingKind.Roof || config.kind == BuildingKind.Plant || config.kind == BuildingKind.Dig || config.kind == BuildingKind.Wall) return null;
            if (nature != null) foreach (var c in cells.allPositionsWithin) nature.Clear(c);

            var root = new GameObject($"{config.displayName} (another kingdom)");
            root.transform.SetParent(transform, false);
            root.transform.position = (Vector3)(Vector2)cells.center;
            Sprite look, shadow;
            int order;
            switch (config.kind)
            {
                case BuildingKind.House:
                    look = local.HouseLook(config, p.Level, p.Rotation);
                    shadow = local.HouseShadow(config, p.Rotation);
                    order = SortOrder.Buildings;
                    break;
                case BuildingKind.Floor:
                    look = local.FloorLook(config, cells.position);
                    shadow = null;
                    order = SortOrder.Floors;
                    break;
                case BuildingKind.Door:
                    look = local.LookOf(config, p.Rotation);
                    shadow = local.ShadowOf(config, p.Rotation);
                    order = SortOrder.Doors;
                    break;
                case BuildingKind.Storage:
                    look = local.LookOf(config, p.Rotation);
                    shadow = local.ShadowOf(config, p.Rotation);
                    order = SortOrder.Buildings;
                    break;
                default:
                    look = local.LookOf(config, p.Rotation);
                    shadow = config.flat ? null : local.ShadowOf(config, p.Rotation);
                    order = config.flat ? SortOrder.Rugs : SortOrder.Furniture;
                    break;
            }
            var r = BuildingLook.Make(root.transform, look, shadow, config.height, order);
            if (config.kind == BuildingKind.Door)
            {
                // Open as you come up to it (you can't walk through a shut door of theirs, nor are you locked out).
                var open = ArtSprites.Get(ArtKeys.DoorOpen(config.art, config.ClampRotation(p.Rotation) % 2));
                doors[root] = new Door { Look = r, Closed = look, Open = open != null ? open : look, Cells = cells };
            }
            else if (config.blocksMovement && config.kind != BuildingKind.Floor)
                BuildingLook.MakeSolid(root, cells.size);

            // A tower's turning top, facing its first way.
            if (config.defense != null && !string.IsNullOrEmpty(config.art))
            {
                var head = ArtSprites.Get(ArtKeys.Head(config.art, 0));
                if (head != null)
                {
                    var h = new GameObject("Head");
                    h.transform.SetParent(root.transform, false);
                    var hr = h.AddComponent<SpriteRenderer>();
                    hr.sprite = head;
                    hr.sortingOrder = order + 2;
                }
            }
            // Fires and lamps glow at night.
            if (config.lightSize > 0f)
            {
                FireLight.Create(root.transform, Vector2.zero, config.lightSize);
                if (flame != null && config.flames != null)
                    foreach (var at in config.flames)
                    {
                        var f = new GameObject("Flame");
                        f.transform.SetParent(root.transform, false);
                        f.transform.localPosition = Facing.Turn(at, p.Rotation);
                        f.transform.localScale = Vector3.one * Mathf.Max(0.1f, config.flameSize);
                        var fr = f.AddComponent<SpriteRenderer>();
                        fr.sprite = flame;
                        fr.sortingOrder = order + 1;
                        Look.MakeGlow(fr);
                    }
            }
            return root;
        }

        // ---------------------------------------------------------- villagers --

        Person NewPerson(int kingdom, int index)
        {
            var go = new GameObject($"Villager {index} of kingdom {kingdom}");
            go.transform.SetParent(transform, false);
            if (folkShadow != null) Look.AddShadow(go.transform, folkShadow, 0.1f);
            var body = new GameObject("Body").transform;
            body.SetParent(go.transform, false);
            var r = body.gameObject.AddComponent<SpriteRenderer>();
            r.sortingOrder = SortOrder.Characters;
            go.SetActive(false);
            return new Person { Root = go, Body = body, Look = r };
        }

        void Update()
        {
            ShowNear();
            float now = Time.unscaledTime, dt = Mathf.Max(Time.deltaTime, 1e-4f);
            foreach (var view in views.Values)
                foreach (var person in view.Folk.Values)
                {
                    bool show = now - person.HeardAt < FolkTimeout && person.Buffer.Sample(RenderTime - MotionBuffer.Delay, out var m);
                    if (show != person.Shown) person.Root.SetActive(person.Shown = show);
                    if (!show) continue;
                    person.Buffer.Sample(RenderTime - MotionBuffer.Delay, out m);
                    person.Root.transform.position = new Vector3(m.X, m.Y, 0f);
                    bool moving = (m.Flags & MotionFlags.Walking) != 0;
                    person.Step = moving ? person.Step + dt * stepsPerSecond : 0f;
                    var frames = (m.Flags & MotionFlags.Carrying) != 0 ? carrying : walking;
                    if (frames != null)
                    {
                        int direction = RulerLook.Direction(m.Aim, frames.GetLength(0));
                        person.Look.sprite = frames[direction, Mathf.FloorToInt(person.Step) % frames.GetLength(1)];
                    }
                    person.Body.localRotation = Quaternion.Euler(0f, 0f, m.Aim);
                }

            // Their doors: open while you're at them.
            if (ruler == null || doors.Count == 0) return;
            Vector2 at = ruler.position;
            foreach (var door in doors.Values)
            {
                if (door.Look == null) continue;
                var want = BuildingLook.Distance(door.Cells, at) < DoorReach ? door.Open : door.Closed;
                if (door.Look.sprite != want) door.Look.sprite = want;
            }
        }
    }
}
