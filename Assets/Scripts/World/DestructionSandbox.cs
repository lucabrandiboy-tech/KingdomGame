using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.UI;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// Builds the Phase 1 test area when you press Play: a few stone and wood
    /// walls, the ruler with a rifle and a rocket launcher, the camera and the
    /// debug display.
    /// </summary>
    public sealed class DestructionSandbox : MonoBehaviour
    {
        [SerializeField] Phase1Content content;

        public Phase1Content Content { get => content; set => content = value; }

        void Start()
        {
            var world = PixelWorld.Current;
            if (world == null || content == null)
            {
                Debug.LogError("The sandbox needs a PixelWorld in the scene and its Content set. " +
                               "Run KingdomGame > Phase 1 > Create Test Scenes again.", this);
                return;
            }

            // (x, y) is the bottom-left tile; the ruler starts at 0, 0.
            Wall(world, "North wall", content.stone, -6, 3, 12, 1);
            Wall(world, "East wall", content.stone, 7, -3, 1, 6);
            Wall(world, "Wood palisade", content.wood, -7, -4, 8, 1);
            Wall(world, "Tower", content.stone, -12, -1, 3, 3);
            Wall(world, "Thick wall", content.stone, 10, 2, 2, 4);

            var ruler = SpawnRuler();
            var view = Camera.main;
            if (view != null)
            {
                var rig = view.GetComponent<CameraRig>();
                if (rig == null) rig = view.gameObject.AddComponent<CameraRig>();
                rig.Follow(ruler.transform);
            }

            var hud = gameObject.AddComponent<DebugHud>();
            hud.help = "WASD: move   Mouse: aim\nLeft click: rifle   Right click: rocket   Space: smash";
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

            var ruler = new GameObject("Ruler");
            ruler.transform.position = Vector3.zero;
            var look = ruler.AddComponent<SpriteRenderer>();
            look.sprite = SpriteFactory.Ruler();
            look.sortingOrder = 5;

            var body = ruler.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var circle = ruler.AddComponent<CircleCollider2D>();
            circle.radius = content.player != null ? content.player.bodyRadius : 0.25f;

            var gun = new GameObject("Gun").transform;
            gun.SetParent(ruler.transform, false);
            gun.localPosition = new Vector3(0f, -0.05f, 0f);
            var gunLook = gun.gameObject.AddComponent<SpriteRenderer>();
            gunLook.sprite = SpriteFactory.Gun();
            gunLook.sortingOrder = 6;

            ruler.AddComponent<RulerController>().Setup(
                content.player, content.rifle, content.rocketLauncher, content.smash, gun, shots);
            return ruler;
        }
    }
}
