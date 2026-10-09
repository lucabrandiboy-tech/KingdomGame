using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Combat
{
    /// <summary>
    /// A thrown explosive like C4: it flies to where you aim, sticks to the first
    /// wall it touches (or lands on the ground) and blows up when its fuse runs out.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Combat/Explosive", fileName = "Explosive")]
    public sealed class ExplosiveConfig : ScriptableObject
    {
        [Tooltip("What the blast does to walls.")]
        public DamageType damage;
        [Tooltip("Seconds from landing until it explodes.")]
        [Min(0.1f)] public float fuseSeconds = 4f;
        [Tooltip("How far it can be thrown, in tiles.")]
        [Min(0.5f)] public float throwRange = 4f;
        [Tooltip("Flight speed, in tiles per second.")]
        [Min(0.5f)] public float throwSpeed = 9f;
        [Tooltip("Seconds before another one can be thrown.")]
        [Min(0)] public float cooldown = 1.5f;
    }
}
