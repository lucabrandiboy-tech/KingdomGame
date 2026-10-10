using System.Collections.Generic;
using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// A wall, tower or building made of destructible pixel chunks, one per grid
    /// cell. It snaps to whole grid cells. After damage it checks itself:
    /// pieces cut off from the rest that are too small crumble, and if too
    /// little is left the whole thing collapses.
    ///
    /// Place one in a scene (set Material and Size), or create it from code
    /// with <see cref="Build"/>.
    /// </summary>
    public sealed class DestructibleStructure : MonoBehaviour
    {
        [SerializeField] StructureMaterial material;
        [Tooltip("Size in grid tiles (1 tile = 64 x 64 pixels).")]
        [SerializeField] Vector2Int size = new Vector2Int(4, 1);

        // Shared scratch buffers for integrity checks (main thread only).
        static byte[] mask = new byte[0];
        static int[] labels = new int[0];
        static int[] stack = new int[0];
        static readonly List<int> islandSizes = new List<int>();

        PixelChunk[] chunks;
        bool built;

        public PixelWorld World { get; private set; }
        public StructureMaterial Material => material;
        /// <summary>The grid cells this structure covers.</summary>
        public RectInt Cells { get; private set; }
        public int SolidCount { get; private set; }
        public int InitialSolidCount { get; private set; }
        public bool Collapsed { get; private set; }
        /// <summary>Zombie damage taken so far (see <see cref="Batter"/>).</summary>
        public float Battered { get; private set; }

        /// <summary>
        /// How much more zombie battering it takes before it caves in: its material's
        /// Siege Health per tile, less what explosions already broke off and what
        /// zombies already did.
        /// </summary>
        public float SiegeHealthLeft
        {
            get
            {
                if (Collapsed || material == null || InitialSolidCount <= 0 || World == null) return 0f;
                float collapseAt = World.Settings.collapseBelow * InitialSolidCount;
                float standing = Mathf.Clamp01((SolidCount - collapseAt) / Mathf.Max(1f, InitialSolidCount - collapseAt));
                return Mathf.Max(0f, material.siegeHealth * Cells.width * Cells.height * standing - Battered);
            }
        }

        /// <summary>
        /// A zombie's blow. Zombies don't break walls pixel by pixel (their claws would
        /// take forever on stone); each blow wears the wall down, and when there's
        /// nothing left (<see cref="SiegeHealthLeft"/>) the whole thing caves in.
        /// </summary>
        public void Batter(float amount)
        {
            if (Collapsed || amount <= 0f) return;
            Battered += amount;
            if (SiegeHealthLeft <= 0f) Collapse();
        }

        /// <summary>Puts back zombie damage from a saved game (it doesn't cave in from this).</summary>
        public void RestoreBattered(float amount) => Battered = Mathf.Max(0f, amount);

        /// <summary>Set by <see cref="PixelWorld"/> while an integrity check is queued.</summary>
        internal bool CheckQueued;

        /// <summary>Creates the chunks. Call once, right after AddComponent.</summary>
        public void Build(PixelWorld world, StructureMaterial wallMaterial, Vector2Int sizeInTiles)
        {
            material = wallMaterial;
            size = sizeInTiles;
            Build(world);
        }

        void Start()
        {
            if (!built) Build(PixelWorld.Current);
        }

        void Build(PixelWorld world)
        {
            built = true;
            if (world == null || material == null || size.x < 1 || size.y < 1)
            {
                Debug.LogError($"{name}: can't build. It needs a PixelWorld in the scene, a Material and a size of at least 1 x 1.", this);
                return;
            }

            World = world;
            var origin = new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.y));
            transform.position = new Vector3(origin.x, origin.y, transform.position.z);
            Cells = new RectInt(origin, size);
            chunks = new PixelChunk[size.x * size.y];

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    var cell = new Vector2Int(origin.x + x, origin.y + y);
                    if (world.ChunkAt(cell) != null)
                    {
                        Debug.LogWarning($"{name}: cell {cell} already has a wall in it, skipping it.", this);
                        continue;
                    }
                    var chunk = new PixelChunk(cell, this, world.Tiles.Get(material, cell), world.Settings.wallHeight);
                    chunks[y * size.x + x] = chunk;
                    world.Register(chunk);
                    InitialSolidCount += chunk.SolidCount;
                }
            }
            SolidCount = InitialSolidCount;
        }

        internal void OnPixelRemoved()
        {
            SolidCount--;
            World.QueueIntegrityCheck(this);
        }

        /// <summary>
        /// Crumbles pieces that were cut off and are too small to stand, and
        /// collapses the whole structure if too little of it is left.
        /// Runs at most once per frame per damaged structure.
        /// </summary>
        internal void CheckIntegrity()
        {
            if (Collapsed || chunks == null) return;
            var settings = World.Settings;

            if (SolidCount < settings.collapseBelow * InitialSolidCount)
            {
                Collapse();
                return;
            }
            if (settings.minStandingPixels <= 0) return;

            int width = size.x * PixelChunk.Size;
            int height = size.y * PixelChunk.Size;
            int area = width * height;
            if (mask.Length < area)
            {
                mask = new byte[area];
                labels = new int[area];
                stack = new int[area];
            }
            System.Array.Clear(mask, 0, area);
            for (int i = 0; i < chunks.Length; i++)
            {
                if (chunks[i] == null || chunks[i].Removed) continue;
                chunks[i].CopyMask(mask, width, (i % size.x) * PixelChunk.Size, (i / size.x) * PixelChunk.Size);
            }

            int count = PixelAlgorithms.Islands(mask, width, height, labels, stack, islandSizes);
            if (count == 0) return;

            bool anySmall = false;
            for (int i = 0; i < count; i++)
                if (islandSizes[i] < settings.minStandingPixels) anySmall = true;
            if (!anySmall) return;

            for (int p = 0; p < area; p++)
            {
                int island = labels[p];
                if (island < 0 || islandSizes[island] >= settings.minStandingPixels) continue;
                BreakPixel(p % width, p / width);
            }

            if (SolidCount < settings.collapseBelow * InitialSolidCount)
                Collapse();
        }

        /// <summary>Everything that's left crumbles into flying pixels and rubble.</summary>
        public void Collapse()
        {
            if (Collapsed || chunks == null) return;
            Collapsed = true;
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];
                if (chunk == null || chunk.Removed) continue;
                for (int y = 0; y < PixelChunk.Size; y++)
                    for (int x = 0; x < PixelChunk.Size; x++)
                        if (chunk.IsSolid(x, y))
                            BreakPixel((i % size.x) * PixelChunk.Size + x, (i / size.x) * PixelChunk.Size + y);
            }
            World.OnStructureCollapsed(this);
        }

        /// <summary>Takes the structure out of the world immediately and destroys it (no debris).</summary>
        public void Remove()
        {
            if (chunks != null && World != null)
                foreach (var chunk in chunks)
                    if (chunk != null) World.Unregister(chunk);
            Collapsed = true;
            Destroy(gameObject);
        }

        // (x, y) are pixels from this structure's bottom-left corner.
        void BreakPixel(int x, int y)
        {
            var chunk = chunks[(y / PixelChunk.Size) * size.x + x / PixelChunk.Size];
            if (chunk == null || chunk.Removed || !chunk.Break(x % PixelChunk.Size, y % PixelChunk.Size, out var color)) return;
            SolidCount--;
            World.OnPixelCrumbled(chunk, Cells.x * PixelChunk.Size + x, Cells.y * PixelChunk.Size + y, color, Center);
        }

        Vector2 Center => Cells.center;

        void OnDestroy()
        {
            if (chunks == null || World == null) return;
            foreach (var chunk in chunks)
                if (chunk != null) World.Unregister(chunk);
        }
    }
}
