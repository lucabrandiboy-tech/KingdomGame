using KingdomGame.Combat;
using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Character
{
    /// <summary>
    /// The player's ruler: twin-stick controls (WASD to move, mouse to aim),
    /// left mouse fires the main weapon, right mouse the second weapon, and
    /// Space smashes the wall in front (a stand-in for a brute zombie's hit).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RulerController : MonoBehaviour
    {
        [SerializeField] PlayerConfig config;
        [SerializeField] WeaponConfig primaryWeapon;
        [SerializeField] WeaponConfig secondaryWeapon;
        [SerializeField] DamageType smash;
        [SerializeField] Transform gunPivot;
        [SerializeField] Projectiles projectiles;

        Rigidbody2D body;
        Camera view;
        CameraRig rig;
        Vector2 aim = Vector2.right;
        Vector2 wantedVelocity;
        float primaryReady, secondaryReady, smashReady;

        public void Setup(PlayerConfig playerConfig, WeaponConfig primary, WeaponConfig secondary, DamageType smashType,
            Transform gun, Projectiles shots)
        {
            config = playerConfig;
            primaryWeapon = primary;
            secondaryWeapon = secondary;
            smash = smashType;
            gunPivot = gun;
            projectiles = shots;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        void Start()
        {
            view = Camera.main;
            if (view != null) rig = view.GetComponent<CameraRig>();
            if (config == null) Debug.LogError("RulerController has no Player Config.", this);
        }

        void Update()
        {
            if (config == null) return;
            wantedVelocity = GameInput.Move * config.moveSpeed;

            if (view != null)
            {
                Vector2 mouse = view.ScreenToWorldPoint(GameInput.PointerPosition);
                var toMouse = mouse - (Vector2)transform.position;
                if (toMouse.sqrMagnitude > 0.0001f) aim = toMouse.normalized;
            }
            if (gunPivot != null)
            {
                float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                gunPivot.rotation = Quaternion.Euler(0, 0, angle);
                // Hold the gun upright when aiming left.
                gunPivot.localScale = new Vector3(1, aim.x < 0 ? -1 : 1, 1);
            }

            float now = Time.time;
            if (primaryWeapon != null && (primaryWeapon.automatic ? GameInput.FireHeld : GameInput.FirePressed) && now >= primaryReady)
            {
                Shoot(primaryWeapon);
                primaryReady = now + 1f / primaryWeapon.fireRate;
            }
            if (secondaryWeapon != null && GameInput.AltFirePressed && now >= secondaryReady)
            {
                Shoot(secondaryWeapon);
                secondaryReady = now + 1f / secondaryWeapon.fireRate;
            }
            if (smash != null && GameInput.Pressed(KeyCode.Space) && now >= smashReady)
            {
                Smash();
                smashReady = now + config.smashCooldown;
            }
        }

        void FixedUpdate()
        {
            if (config == null) return;
            var velocity = Vector2.MoveTowards(Velocity, wantedVelocity, config.acceleration * Time.fixedDeltaTime);
            Velocity = velocity;
        }

        // Unity 6 renamed Rigidbody2D.velocity to linearVelocity.
        Vector2 Velocity
        {
#if UNITY_6000_0_OR_NEWER
            get => body.linearVelocity;
            set => body.linearVelocity = value;
#else
            get => body.velocity;
            set => body.velocity = value;
#endif
        }

        Vector2 Muzzle => gunPivot != null ? (Vector2)gunPivot.position + aim * 0.4f : (Vector2)transform.position + aim * 0.4f;

        void Shoot(WeaponConfig weapon)
        {
            if (projectiles == null) return;
            if (projectiles.Fire(weapon, Muzzle, aim) && rig != null)
                rig.AddShake(weapon.recoilShake);
        }

        void Smash()
        {
            var world = PixelWorld.Current;
            var at = (Vector2)transform.position + aim * config.smashReach;
            if (world != null && world.TryMakeEvent(smash, at, out var hit))
                world.ApplyDamage(hit);
        }
    }
}
