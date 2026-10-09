using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// The one place that knows every destructible chunk in the scene, by grid
    /// cell. It applies damage events, answers "is this pixel solid?" for
    /// bullets, and once per frame (LateUpdate) crumbles broken structures,
    /// moves the flying pixels and uploads changed chunks.
    ///
    /// Put exactly one in each scene that has destructible walls.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class PixelWorld : MonoBehaviour, IPixelSolid
    {
        [SerializeField] DestructionSettings settings;
        [Tooltip("Every damage type that can hit walls. A damage event refers to one by its position in this list.")]
        [SerializeField] DamageType[] damageTypes = new DamageType[0];

        public static PixelWorld Current { get; private set; }

        /// <summary>A hit was applied: the event, its type, its world position and how many pixels broke.</summary>
        public event Action<DamageEvent, DamageType, Vector2, int> Damaged;

        readonly Dictionary<Vector2Int, PixelChunk> chunks = new Dictionary<Vector2Int, PixelChunk>();
        readonly List<PixelChunk> toFlush = new List<PixelChunk>();
        readonly List<DestructibleStructure> toCheck = new List<DestructibleStructure>();
        readonly List<PixelDamage> damaged = new List<PixelDamage>();

        PixelDebris debris;
        RubbleLayer rubble;
        uint seedCounter;
        int newDebrisThisFrame;

        // Last chunk looked up by IsSolid, because bullets ask about many pixels in a row.
        Vector2Int cachedCell = new Vector2Int(int.MinValue, int.MinValue);
        PixelChunk cachedChunk;

        public DestructionSettings Settings => settings;
        public WallTiles Tiles { get; } = new WallTiles();

        // Numbers for the debug display.
        public int ChunkCount => chunks.Count;
        public int DamagedChunkCount { get; private set; }
        public long PixelsDestroyed { get; private set; }
        public int DebrisCount => debris?.Count ?? 0;
        public int RubbleTileCount => rubble?.TileCount ?? 0;

        /// <summary>Sets the assets this world uses. For the scene builder (KingdomGame > Phase 1 > Create Test Scenes).</summary>
        public void Setup(DestructionSettings destructionSettings, DamageType[] types)
        {
            settings = destructionSettings;
            damageTypes = types;
        }

        void Awake()
        {
            if (Current != null && Current != this)
                Debug.LogWarning("There is more than one PixelWorld in the scene. Only the newest one is used.", this);
            Current = this;

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DestructionSettings>();
                Debug.LogWarning("PixelWorld has no Destruction Settings asset, using defaults.", this);
            }
            debris = new PixelDebris(transform, settings);
            rubble = new RubbleLayer(transform, settings);
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            debris?.Dispose();
            rubble?.Dispose();
            Tiles.DestroyAll();
        }

        // ------------------------------------------------------------ chunks --

        public PixelChunk ChunkAt(Vector2Int cell) => chunks.TryGetValue(cell, out var chunk) ? chunk : null;

        internal void Register(PixelChunk chunk)
        {
            chunks.Add(chunk.Cell, chunk);
            cachedCell = new Vector2Int(int.MinValue, int.MinValue);
        }

        internal void Unregister(PixelChunk chunk)
        {
            if (chunk.Removed) return;
            chunk.Removed = true;
            if (chunks.TryGetValue(chunk.Cell, out var found) && found == chunk)
                chunks.Remove(chunk.Cell);
            if (chunk.IsDamaged) DamagedChunkCount--;
            chunk.Queued = false;
            toFlush.Remove(chunk);
            cachedCell = new Vector2Int(int.MinValue, int.MinValue);
            chunk.Destroy();
        }

        internal void OnChunkFirstDamaged() => DamagedChunkCount++;

        /// <summary>True if this world pixel is part of a standing wall.</summary>
        public bool IsSolid(int pixelX, int pixelY)
        {
            var cell = new Vector2Int(DamageShape.FloorDiv(pixelX, PixelChunk.Size), DamageShape.FloorDiv(pixelY, PixelChunk.Size));
            if (cell != cachedCell)
            {
                cachedCell = cell;
                chunks.TryGetValue(cell, out cachedChunk);
            }
            return cachedChunk != null &&
                   cachedChunk.IsSolid(pixelX - cell.x * PixelChunk.Size, pixelY - cell.y * PixelChunk.Size);
        }

        /// <summary>
        /// The first wall pixel on the straight line from <paramref name="from"/>
        /// to <paramref name="to"/> (world positions), if there is one.
        /// </summary>
        public bool Raycast(Vector2 from, Vector2 to, out Vector2Int hitPixel, out Vector2 hitPoint)
        {
            const double ppu = PixelArt.PixelsPerUnit;
            if (PixelAlgorithms.Raycast(this, from.x * ppu, from.y * ppu, to.x * ppu, to.y * ppu,
                    out int x, out int y, out double t))
            {
                hitPixel = new Vector2Int(x, y);
                hitPoint = Vector2.Lerp(from, to, (float)t);
                return true;
            }
            hitPixel = default;
            hitPoint = to;
            return false;
        }

        public static Vector2Int WorldToPixel(Vector2 position) => new Vector2Int(
            Mathf.FloorToInt(position.x * PixelArt.PixelsPerUnit),
            Mathf.FloorToInt(position.y * PixelArt.PixelsPerUnit));

        public static Vector2 PixelCenter(int pixelX, int pixelY) =>
            new Vector2((pixelX + 0.5f) / PixelArt.PixelsPerUnit, (pixelY + 0.5f) / PixelArt.PixelsPerUnit);

        // ------------------------------------------------------------ damage --

        /// <summary>Position of a damage type in this world's list, or -1.</summary>
        public int TypeIndex(DamageType type) => Array.IndexOf(damageTypes, type);

        /// <summary>
        /// Builds a damage event at a world position. In multiplayer only the
        /// server will do this; clients just receive the events.
        /// </summary>
        public bool TryMakeEvent(DamageType type, Vector2 position, out DamageEvent damageEvent)
        {
            int index = TypeIndex(type);
            var pixel = WorldToPixel(position);
            damageEvent = new DamageEvent
            {
                PixelX = pixel.x,
                PixelY = pixel.y,
                TypeIndex = (byte)Mathf.Max(index, 0),
                Seed = DamageShape.Hash(++seedCounter, pixel.x, pixel.y),
            };
            if (index >= 0 && index <= byte.MaxValue) return true;
            Debug.LogWarning($"Damage type '{(type != null ? type.name : "none")}' isn't in the PixelWorld's Damage Types list.", this);
            return false;
        }

        /// <summary>
        /// Applies one hit. Returns how many pixels broke off. The same event
        /// always breaks the same pixels; the flying pixels are just for looks.
        /// </summary>
        public int ApplyDamage(in DamageEvent hit)
        {
            if (hit.TypeIndex >= damageTypes.Length || damageTypes[hit.TypeIndex] == null)
            {
                Debug.LogWarning($"Damage event has unknown damage type {hit.TypeIndex}.", this);
                return 0;
            }
            var type = damageTypes[hit.TypeIndex];
            DamageShape.Compute(hit.PixelX, hit.PixelY, type.ToShape(hit.RadiusPx), hit.Seed, damaged);

            var random = new FastRandom(hit.Seed);
            var center = PixelCenter(hit.PixelX, hit.PixelY);
            int broken = 0;

            foreach (var pixel in damaged)
            {
                var cell = new Vector2Int(DamageShape.FloorDiv(pixel.X, PixelChunk.Size), DamageShape.FloorDiv(pixel.Y, PixelChunk.Size));
                if (!chunks.TryGetValue(cell, out var chunk)) continue;
                if (!chunk.Damage(pixel.X - cell.x * PixelChunk.Size, pixel.Y - cell.y * PixelChunk.Size, pixel.Damage, out var color))
                {
                    QueueFlush(chunk); // the picture may have cracked
                    continue;
                }

                broken++;
                QueueFlush(chunk);
                chunk.Owner.OnPixelRemoved();
                if (random.Value() < type.debrisChance)
                {
                    var position = PixelCenter(pixel.X, pixel.Y);
                    var away = position - center;
                    var direction = away.sqrMagnitude > 1e-8f ? away.normalized : random.Direction();
                    direction = (direction + random.Direction() * 0.6f).normalized;
                    float speed = Mathf.Lerp(type.debrisSpeed.x, type.debrisSpeed.y, random.Value());
                    float lift = Mathf.Lerp(type.debrisLift.x, type.debrisLift.y, random.Value());
                    LaunchOrDrop(position, direction * speed, lift, color);
                }
            }

            // Sparks: a short burst of bright pixels so every hit is visible.
            for (int i = 0; i < type.sparks && newDebrisThisFrame < settings.maxNewDebrisPerFrame; i++)
            {
                var velocity = random.Direction() * Mathf.Lerp(type.debrisSpeed.x, type.debrisSpeed.y * 1.3f, random.Value());
                if (!debris.Launch(center, velocity, Mathf.Lerp(type.debrisLift.x, type.debrisLift.y, random.Value()), type.sparkColor, spark: true))
                    break;
                newDebrisThisFrame++;
            }

            PixelsDestroyed += broken;
            Damaged?.Invoke(hit, type, center, broken);
            return broken;
        }

        internal void QueueIntegrityCheck(DestructibleStructure structure)
        {
            if (structure.CheckQueued) return;
            structure.CheckQueued = true;
            toCheck.Add(structure);
        }

        /// <summary>Called by a structure for every pixel that crumbles off it.</summary>
        internal void OnPixelCrumbled(PixelChunk chunk, int pixelX, int pixelY, Color32 color, Vector2 pushedFrom)
        {
            PixelsDestroyed++;
            QueueFlush(chunk);
            var position = PixelCenter(pixelX, pixelY);
            var away = position - pushedFrom;
            var direction = away.sqrMagnitude > 1e-8f ? away.normalized : Vector2.down;
            var random = new FastRandom(DamageShape.Hash(0xC0u, pixelX, pixelY));
            direction = (direction + random.Direction() * 0.8f).normalized;
            LaunchOrDrop(position, direction * Mathf.Lerp(0.3f, 2f, random.Value()), Mathf.Lerp(0.5f, 2.5f, random.Value()), color);
        }

        internal void OnStructureCollapsed(DestructibleStructure structure)
        {
            // The chunks are empty now; LateUpdate removes them. The structure's
            // GameObject is destroyed at the end of this frame.
            Destroy(structure.gameObject);
        }

        // A flying pixel if the limits allow one, otherwise it lands right away.
        void LaunchOrDrop(Vector2 position, Vector2 velocity, float lift, Color32 color)
        {
            if (newDebrisThisFrame < settings.maxNewDebrisPerFrame && debris.Launch(position, velocity, lift, color))
            {
                newDebrisThisFrame++;
                return;
            }
            if (settings.rubble)
                rubble.Drop(position + velocity * 0.15f, color);
        }

        void QueueFlush(PixelChunk chunk)
        {
            if (chunk.Queued) return;
            chunk.Queued = true;
            toFlush.Add(chunk);
        }

        /// <summary>Removes all rubble and flying pixels (used by the stress test reset).</summary>
        public void ClearEffects()
        {
            debris.Clear();
            rubble.Clear();
        }

        // ------------------------------------------------------------- frame --

        void LateUpdate()
        {
            // 1. Crumble cut-off pieces and collapse badly damaged structures.
            for (int i = 0; i < toCheck.Count; i++)
            {
                var structure = toCheck[i];
                if (structure == null) continue;
                structure.CheckQueued = false;
                structure.CheckIntegrity();
            }
            toCheck.Clear();

            // 2. Move the flying pixels; the ones that stop become rubble.
            debris.Update(Time.deltaTime, settings.rubble ? rubble : null);
            newDebrisThisFrame = 0;

            // 3. Upload changed chunk pictures and rebuild their colliders.
            for (int i = 0; i < toFlush.Count; i++)
            {
                var chunk = toFlush[i];
                chunk.Queued = false;
                if (chunk.SolidCount == 0)
                {
                    Unregister(chunk);
                    i--; // Unregister removed it from this list
                    continue;
                }
                chunk.Flush();
            }
            toFlush.Clear();

            rubble.Flush();
        }
    }

    /// <summary>Tiny fast random numbers for the look of debris (not used for game rules).</summary>
    internal struct FastRandom
    {
        uint state;

        public FastRandom(uint seed) => state = seed != 0 ? seed : 0x9E3779B9u;

        public float Value()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / 16777216f;
        }

        public Vector2 Direction()
        {
            float angle = Value() * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
    }
}
