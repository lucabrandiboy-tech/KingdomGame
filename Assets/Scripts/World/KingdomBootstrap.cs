using KingdomGame.Art;
using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.UI;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KingdomGame.World
{
    /// <summary>
    /// Builds the game when you press Play: the map, the ruler with their gear,
    /// the test outpost, day and night, and the on-screen info.
    /// </summary>
    public sealed class KingdomBootstrap : MonoBehaviour
    {
        [SerializeField] GameContent content;
        [SerializeField] DayNightCycle dayNight;

        public GameContent Content { get => content; set => content = value; }
        public DayNightCycle DayNight { get => dayNight; set => dayNight = value; }

        void Start()
        {
            var world = PixelWorld.Current;
            if (world == null || content == null || content.world == null)
            {
                Debug.LogError("The game scene needs a PixelWorld and its Content set. Run KingdomGame > Build Game Scenes again.", this);
                return;
            }

            int seed = content.world.seed != 0 ? content.world.seed : Random.Range(1, int.MaxValue);
            Debug.Log($"Map seed {seed}. Put this number in WorldConfig's Seed to get the same map again.");
            WorldGenerator.Generate(content, transform, seed);
            if (content.world.testOutpost) BuildOutpost(world);

            var ruler = SpawnRuler();
            var view = Camera.main;
            if (view != null)
            {
                var rig = view.GetComponent<CameraRig>();
                if (rig == null) rig = view.gameObject.AddComponent<CameraRig>();
                rig.Follow(ruler.transform);
            }

            var hud = gameObject.AddComponent<GameHud>();
            hud.Setup(ruler.GetComponent<RulerController>(), dayNight);
            var debug = gameObject.AddComponent<DebugHud>();
            debug.visible = false;
            debug.toggleKey = KeyCode.F3;
        }

        // A small walled yard east of the start: stone on two sides, wood on one,
        // and a stone tower, for trying bullets, rockets and C4.
        void BuildOutpost(PixelWorld world)
        {
            Wall(world, "Outpost north wall", content.stone, 5, 4, 9, 1);
            Wall(world, "Outpost south wall", content.stone, 5, -4, 9, 1);
            Wall(world, "Outpost east palisade", content.wood, 13, -3, 1, 7);
            Wall(world, "Outpost tower", content.stone, 8, -1, 2, 2);
        }

        void Wall(PixelWorld world, string wallName, StructureMaterial material, int x, int y, int width, int height)
        {
            if (material == null) return;
            var go = new GameObject(wallName);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(x, y, 0);
            go.AddComponent<DestructibleStructure>().Build(world, material, new Vector2Int(width, height));
        }

        GameObject SpawnRuler()
        {
            var shots = new GameObject("Projectiles").AddComponent<Projectiles>();
            var charges = new GameObject("Explosives").AddComponent<Explosives>();

            var ruler = new GameObject("Ruler");
            var bodyLook = ruler.AddComponent<SpriteRenderer>();

            var body = ruler.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var feet = ruler.AddComponent<CircleCollider2D>();
            feet.radius = content.player != null ? content.player.bodyRadius : 0.15f;
            feet.offset = new Vector2(0f, feet.radius); // the ruler's position is at their feet

            var gun = new GameObject("Gun").transform;
            gun.SetParent(ruler.transform, false);
            gun.localPosition = new Vector3(0f, 0.22f, 0f);
            var gunLook = gun.gameObject.AddComponent<SpriteRenderer>();
            gunLook.sprite = PixelTextures.ToSprite(CharacterArt.Rifle(), "Rifle");

            // A warm light that comes on at night.
            var torch = new GameObject("Night light").AddComponent<Light2D>();
            torch.transform.SetParent(ruler.transform, false);
            torch.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            torch.lightType = Light2D.LightType.Point;
            if (dayNight != null) dayNight.AddNightLight(torch);
            else torch.enabled = false;

            ruler.AddComponent<RulerLook>().Setup(bodyLook, gunLook);
            ruler.AddComponent<RulerController>().Setup(
                content.player, content.rifle, content.rocketLauncher, content.c4, gun, shots, charges);
            return ruler;
        }
    }
}
