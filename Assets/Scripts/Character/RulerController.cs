using KingdomGame.Combat;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.Character
{
    /// <summary>
    /// The player's ruler: twin-stick controls (WASD to move, mouse to aim).
    /// Left mouse fires the rifle, right mouse a rocket, G throws C4,
    /// and holding E gathers from the tree, rock or bush you're standing next to.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class RulerController : MonoBehaviour
    {
        [SerializeField] PlayerConfig config;
        [SerializeField] WeaponConfig primaryWeapon;
        [SerializeField] WeaponConfig secondaryWeapon;
        [SerializeField] ExplosiveConfig explosive;
        [SerializeField] Transform gunPivot;
        [SerializeField] Projectiles projectiles;
        [SerializeField] Explosives explosives;

        Rigidbody2D body;
        RulerLook look;
        Camera view;
        CameraRig rig;
        Vector2 aim = Vector2.right;
        Vector2 wantedVelocity;
        float primaryReady, secondaryReady, explosiveReady, gatherReady;

        public Inventory Backpack { get; private set; }
        /// <summary>The node you'd gather from if you held E now, or null.</summary>
        public ResourceNode GatherTarget { get; private set; }
        /// <summary>A short message for the HUD, like "Backpack full".</summary>
        public string Notice { get; private set; }
        float noticeUntil;

        public void Setup(PlayerConfig playerConfig, WeaponConfig primary, WeaponConfig secondary, ExplosiveConfig thrown,
            Transform gun, Projectiles shots, Explosives charges)
        {
            config = playerConfig;
            primaryWeapon = primary;
            secondaryWeapon = secondary;
            explosive = thrown;
            gunPivot = gun;
            projectiles = shots;
            explosives = charges;
            Backpack = new Inventory(config != null ? config.backpackCapacity : 250);
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            look = GetComponent<RulerLook>();
        }

        void Start()
        {
            view = Camera.main;
            if (view != null) rig = view.GetComponent<CameraRig>();
            if (config == null) Debug.LogError("RulerController has no Player Config.", this);
            if (Backpack == null) Backpack = new Inventory(config != null ? config.backpackCapacity : 250);
        }

        void Update()
        {
            if (config == null) return;
            wantedVelocity = GameInput.Move * config.moveSpeed;

            if (view != null)
            {
                Vector2 mouse = view.ScreenToWorldPoint(GameInput.PointerPosition);
                var toMouse = mouse - (Vector2)(gunPivot != null ? gunPivot.position : transform.position);
                if (toMouse.sqrMagnitude > 0.0001f) aim = toMouse.normalized;
            }
            if (gunPivot != null)
            {
                gunPivot.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
                gunPivot.localScale = new Vector3(1, aim.x < 0 ? -1 : 1, 1); // keep the gun upright when aiming left
            }
            if (look != null) look.Show(Velocity, aim, config.moveSpeed, config.stepsPerSecond);

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
            if (explosive != null && explosives != null && GameInput.Pressed(KeyCode.G) && now >= explosiveReady && view != null)
            {
                Vector2 target = view.ScreenToWorldPoint(GameInput.PointerPosition);
                explosives.Throw(explosive, Muzzle, target);
                explosiveReady = now + explosive.cooldown;
            }

            UpdateGathering(now);
            if (now > noticeUntil) Notice = null;
        }

        void FixedUpdate()
        {
            if (config == null) return;
            Velocity = Vector2.MoveTowards(Velocity, wantedVelocity, config.acceleration * Time.fixedDeltaTime);
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

        Vector2 Muzzle => gunPivot != null ? (Vector2)gunPivot.position + aim * 0.35f : (Vector2)transform.position;

        void Shoot(WeaponConfig weapon)
        {
            if (projectiles == null) return;
            if (projectiles.Fire(weapon, Muzzle, aim) && rig != null)
                rig.AddShake(weapon.recoilShake);
        }

        void UpdateGathering(float now)
        {
            GatherTarget = ClosestNode();
            if (GatherTarget == null || !GameInput.Held(KeyCode.E) || now < gatherReady) return;

            if (Backpack.SpaceLeft <= 0)
            {
                Say("Backpack full");
                return;
            }
            int got = GatherTarget.Gather(Backpack.SpaceLeft);
            Backpack.Add(GatherTarget.Config.resource, got);
            gatherReady = now + GatherTarget.Config.secondsPerHit;
        }

        ResourceNode ClosestNode()
        {
            ResourceNode best = null;
            float bestDistance = float.MaxValue;
            Vector2 here = transform.position;
            foreach (var node in ResourceNode.All)
            {
                if (node == null || node.Empty || node.Config == null) continue;
                float d = node.DistanceFrom(here);
                if (d <= node.Config.reach && d < bestDistance)
                {
                    best = node;
                    bestDistance = d;
                }
            }
            return best;
        }

        void Say(string text)
        {
            Notice = text;
            noticeUntil = Time.time + 1.5f;
        }
    }
}
