using System.Collections.Generic;
using KingdomGame.Destruction;
using KingdomGame.UI;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// Destruction stress test: a field of walls with explosions going off all
    /// the time, and the frame rate on screen and in the Console.
    /// T = pause/resume the explosions, X = blow up every wall at once,
    /// R = rebuild all walls.
    /// </summary>
    public sealed class StressTest : MonoBehaviour
    {
        [SerializeField] StressTestConfig config;

        public StressTestConfig Config { get => config; set => config = value; }

        readonly List<DestructibleStructure> walls = new List<DestructibleStructure>();
        PixelWorld world;
        DebugHud hud;
        bool running = true;
        float explosionDebt;
        float nextReport;
        int explosionsThisReport;
        Transform wallRoot;

        void Start()
        {
            world = PixelWorld.Current;
            if (world == null || config == null || config.explosion == null)
            {
                Debug.LogError("The stress test needs a PixelWorld in the scene and a Stress Test Config with an explosion. " +
                               "Run KingdomGame > Phase 1 > Create Test Scenes again.", this);
                enabled = false;
                return;
            }

            hud = gameObject.AddComponent<DebugHud>();
            hud.help = "T: pause/resume explosions   X: blow up everything   R: rebuild walls";
            hud.extraLines = Status;
            BuildWalls();
            nextReport = Time.unscaledTime + config.reportEverySeconds;
        }

        void Update()
        {
            if (GameInput.Pressed(KeyCode.T)) running = !running;
            if (GameInput.Pressed(KeyCode.R)) BuildWalls();
            if (GameInput.Pressed(KeyCode.X)) BlowUpEverything();

            if (running)
            {
                explosionDebt += config.explosionsPerSecond * Time.deltaTime;
                while (explosionDebt >= 1f)
                {
                    explosionDebt -= 1f;
                    if (!ExplodeRandomWall()) { explosionDebt = 0; break; }
                }
            }

            if (Time.unscaledTime >= nextReport)
            {
                Debug.Log($"[Stress test] {hud.AverageFps:0} FPS, worst frame {hud.WorstFrameMs:0.0} ms, " +
                          $"{explosionsThisReport} explosions in the last {config.reportEverySeconds:0} s, " +
                          $"{world.DebrisCount} flying pixels, {world.DamagedChunkCount}/{world.ChunkCount} chunks damaged, " +
                          $"{StandingWalls()} of {walls.Count} walls standing, {world.PixelsDestroyed:N0} pixels destroyed.");
                explosionsThisReport = 0;
                nextReport = Time.unscaledTime + config.reportEverySeconds;
            }
        }

        string Status() =>
            $"Stress test: {(running ? $"{config.explosionsPerSecond:0} explosions/s" : "paused")}   " +
            $"walls standing: {StandingWalls()} / {walls.Count}";

        void BuildWalls()
        {
            // Take the old walls out of the world right away, so the new ones can use the same cells.
            foreach (var wall in walls)
                if (wall != null) wall.Remove();
            walls.Clear();
            world.ClearEffects();
            if (wallRoot == null) wallRoot = new GameObject("Walls").transform;

            int width = config.columns * (config.wallLength + config.gap) - config.gap;
            int height = (config.rows - 1) * config.rowSpacing + 1;
            int left = -width / 2;
            int bottom = -height / 2;

            for (int row = 0; row < config.rows; row++)
            {
                var material = config.materials.Length > 0 ? config.materials[row % config.materials.Length] : null;
                if (material == null) continue;
                for (int column = 0; column < config.columns; column++)
                {
                    var go = new GameObject($"Wall {row},{column}");
                    go.transform.SetParent(wallRoot, false);
                    go.transform.position = new Vector3(left + column * (config.wallLength + config.gap), bottom + row * config.rowSpacing, 0);
                    var wall = go.AddComponent<DestructibleStructure>();
                    wall.Build(world, material, new Vector2Int(config.wallLength, 1));
                    walls.Add(wall);
                }
            }
        }

        bool ExplodeRandomWall()
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var wall = walls.Count > 0 ? walls[Random.Range(0, walls.Count)] : null;
                if (wall == null || wall.Collapsed || wall.World == null) continue;
                Explode(wall);
                return true;
            }
            return StandingWalls() > 0;
        }

        void BlowUpEverything()
        {
            foreach (var wall in walls)
            {
                if (wall == null || wall.Collapsed || wall.World == null) continue;
                for (int i = 0; i < config.blastAllPerWall; i++) Explode(wall);
            }
        }

        void Explode(DestructibleStructure wall)
        {
            var cells = wall.Cells;
            var at = new Vector2(Random.Range(cells.xMin, (float)cells.xMax), Random.Range(cells.yMin, (float)cells.yMax));
            if (world.TryMakeEvent(config.explosion, at, out var hit))
            {
                world.ApplyDamage(hit);
                explosionsThisReport++;
            }
        }

        int StandingWalls()
        {
            int count = 0;
            foreach (var wall in walls)
                if (wall != null && !wall.Collapsed) count++;
            return count;
        }
    }
}
