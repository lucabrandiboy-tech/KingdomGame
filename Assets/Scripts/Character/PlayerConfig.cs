using UnityEngine;

namespace KingdomGame.Character
{
    /// <summary>How the ruler moves and carries things. Gear will change these later (there are no character levels).</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Character/Player Config", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Tooltip("Top walking speed, in tiles per second.")]
        [Min(0)] public float moveSpeed = 3.2f;
        [Tooltip("How quickly the ruler speeds up and slows down, in tiles per second squared.")]
        [Min(0.1f)] public float acceleration = 30f;
        [Tooltip("Size of the ruler's body for bumping into things, in tiles. Holes must be wider than this to walk through.")]
        [Range(0.05f, 0.5f)] public float bodyRadius = 0.15f;
        [Tooltip("How many resources the backpack holds in total (all kinds together).")]
        [Min(1)] public int backpackCapacity = 250;
        [Tooltip("Steps per second of the walking animation at full speed.")]
        [Min(0.1f)] public float stepsPerSecond = 8f;
    }
}
