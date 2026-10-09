using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame
{
    /// <summary>
    /// Goes on the Main Camera: follows a target smoothly and shakes when walls
    /// get hit (each damage type has its own shake amount). The Pixel Perfect
    /// Camera then snaps the view to whole pixels.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [Tooltip("How quickly the camera catches up with the target.")]
        [SerializeField] float followSharpness = 10f;
        [Tooltip("The most the camera can shake, in tiles.")]
        [SerializeField] float maxShake = 0.35f;
        [Tooltip("How quickly shaking calms down.")]
        [SerializeField] float shakeFade = 5f;

        Vector2 center;
        float shake;
        PixelWorld subscribed;

        public void Follow(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            if (snap && target != null) center = target.position;
        }

        public void AddShake(float amount) => shake = Mathf.Min(maxShake, shake + amount);

        void Start()
        {
            center = target != null ? (Vector2)target.position : (Vector2)transform.position;
        }

        void OnEnable() => Subscribe();

        void OnDisable()
        {
            if (subscribed != null) subscribed.Damaged -= OnDamaged;
            subscribed = null;
        }

        void Subscribe()
        {
            var world = PixelWorld.Current;
            if (world == subscribed || world == null) return;
            if (subscribed != null) subscribed.Damaged -= OnDamaged;
            subscribed = world;
            subscribed.Damaged += OnDamaged;
        }

        void OnDamaged(DamageEvent hit, DamageType type, Vector2 position, int broken) => AddShake(type.screenShake);

        void LateUpdate()
        {
            Subscribe(); // in case the PixelWorld was created after this camera

            if (target != null)
                center = Vector2.Lerp(center, target.position, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));

            Vector2 offset = shake > 0.0001f ? Random.insideUnitCircle * shake : Vector2.zero;
            shake = Mathf.Max(0f, shake - shake * shakeFade * Time.deltaTime - 0.01f * Time.deltaTime);

            // Whole pixels only, so the pixel art doesn't shimmer while the camera moves.
            var position = (center + offset) * PixelArt.PixelsPerUnit;
            transform.position = new Vector3(
                Mathf.Round(position.x) / PixelArt.PixelsPerUnit,
                Mathf.Round(position.y) / PixelArt.PixelsPerUnit,
                transform.position.z);
        }
    }
}
