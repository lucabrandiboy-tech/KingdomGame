using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame
{
    /// <summary>
    /// Goes on the Main Camera. Two modes:
    /// <list type="bullet">
    /// <item>Follow: smoothly follows the ruler at walking zoom.</item>
    /// <item>Free (the throne view): stays where it's moved with <see cref="Pan"/>, zoomed far out.</item>
    /// </list>
    /// The mouse wheel zooms within the current mode's limits. Shakes when walls
    /// get hit (each damage type has its own shake amount).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] ViewConfig view;
        [SerializeField] Transform target;
        [Tooltip("Shaking calms down this quickly.")]
        [SerializeField] float shakeFade = 5f;

        Camera cam;
        Vector2 center;
        float shake;
        float zoom;          // wanted half-height in tiles
        bool free;
        PixelWorld subscribed;

        /// <summary>The area the camera's middle may move in (the map), or zero size for no limit.</summary>
        public Rect Bounds { get; set; }
        public bool IsFree => free;
        public float Zoom => zoom;
        /// <summary>Set by other scripts (the HUD) while the mouse is over buttons, so the wheel doesn't zoom.</summary>
        public bool BlockScroll { get; set; }

        public void Setup(ViewConfig viewConfig)
        {
            view = viewConfig;
            if (view != null) zoom = view.walkZoom;
        }

        /// <summary>Follows a target at walking zoom.</summary>
        public void Follow(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            free = false;
            zoom = Mathf.Clamp(View.walkZoom, View.walkZoomMin, View.walkZoomMax);
            if (snap && target != null) center = target.position;
        }

        /// <summary>Stops following and zooms out around <paramref name="at"/> (the throne view).</summary>
        public void FreeLook(Vector2 at)
        {
            target = null;
            free = true;
            center = at;
            zoom = Mathf.Clamp(View.throneZoom, View.throneZoomMin, View.throneZoomMax);
        }

        /// <summary>Moves the view by <paramref name="direction"/> (length 0..1) for this frame, scaled by the zoom.</summary>
        public void Pan(Vector2 direction) => center += direction * View.panSpeed * zoom * 2f * Time.unscaledDeltaTime;

        public void AddShake(float amount) => shake = Mathf.Min(View.maxShake, shake + amount);

        ViewConfig View
        {
            get
            {
                if (view == null) view = ScriptableObject.CreateInstance<ViewConfig>();
                return view;
            }
        }

        void Awake()
        {
            cam = GetComponent<Camera>();
            if (zoom <= 0f) zoom = cam.orthographicSize > 0f ? cam.orthographicSize : View.walkZoom;
        }

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

        void OnDamaged(DamageEvent hit, DamageType type, Vector2 position, int broken)
        {
            // Far-away explosions shake less.
            float distance = Vector2.Distance(position, center);
            AddShake(type.screenShake * Mathf.Clamp01(1.2f - distance / (zoom * 2.5f)));
        }

        void LateUpdate()
        {
            Subscribe(); // in case the PixelWorld was created after this camera
            var v = View;
            float dt = Time.unscaledDeltaTime;

            float scroll = BlockScroll ? 0f : GameInput.Scroll;
            if (scroll != 0f)
            {
                float min = free ? v.throneZoomMin : v.walkZoomMin;
                float max = free ? v.throneZoomMax : v.walkZoomMax;
                zoom = Mathf.Clamp(zoom * Mathf.Pow(v.zoomStep, -scroll), min, max);
            }

            if (!free && target != null)
                center = Vector2.Lerp(center, target.position, 1f - Mathf.Exp(-v.followSharpness * dt));
            if (Bounds.width > 0f && Bounds.height > 0f)
                center = new Vector2(Mathf.Clamp(center.x, Bounds.xMin, Bounds.xMax), Mathf.Clamp(center.y, Bounds.yMin, Bounds.yMax));

            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, zoom, 1f - Mathf.Exp(-v.zoomSharpness * dt));

            Vector2 offset = shake > 0.0001f ? Random.insideUnitCircle * shake : Vector2.zero;
            shake = Mathf.Max(0f, shake - shake * shakeFade * dt - 0.01f * dt);
            transform.position = new Vector3(center.x + offset.x, center.y + offset.y, transform.position.z);
        }
    }
}
