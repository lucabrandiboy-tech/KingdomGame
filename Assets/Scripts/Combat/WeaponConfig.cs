using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Combat
{
    /// <summary>A gun: how fast it shoots, how its shots fly and what they do on impact.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Combat/Weapon", fileName = "Weapon")]
    public sealed class WeaponConfig : ScriptableObject
    {
        [Tooltip("What a shot does to walls when it hits.")]
        public DamageType damage;
        [Tooltip("Shots per second.")]
        [Min(0.1f)] public float fireRate = 8f;
        [Tooltip("Keep firing while the button is held.")]
        public bool automatic = true;
        [Tooltip("Tiles per second.")]
        [Min(0.1f)] public float projectileSpeed = 28f;
        [Tooltip("How far a shot flies, in tiles.")]
        [Min(0.1f)] public float range = 16f;
        [Tooltip("Random aim error, in degrees either way.")]
        [Range(0, 45)] public float spreadDegrees = 2f;
        [Tooltip("Rockets explode at the end of their flight even if they hit nothing.")]
        public bool explodeAtEndOfRange;

        [Header("Look")]
        [Min(1)] public int projectileLength = 4;
        [Min(1)] public int projectileThickness = 1;
        public Color32 projectileColor = new Color32(255, 214, 102, 255);
        [Tooltip("Camera shake for each shot, in tiles.")]
        [Min(0)] public float recoilShake = 0.01f;
    }
}
