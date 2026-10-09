using UnityEngine;

namespace KingdomGame.Character
{
    /// <summary>How the ruler moves. Gear will change these later (there are no character levels).</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Character/Player Config", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : ScriptableObject
    {
        [Tooltip("Top walking speed, in tiles per second.")]
        [Min(0)] public float moveSpeed = 5f;
        [Tooltip("How quickly the ruler speeds up and slows down, in tiles per second squared.")]
        [Min(0.1f)] public float acceleration = 45f;
        [Tooltip("Size of the ruler's body for bumping into walls, in tiles. Holes must be wider than this to walk through.")]
        [Range(0.05f, 0.5f)] public float bodyRadius = 0.25f;
        [Tooltip("Seconds between smashes (the brute-zombie test attack).")]
        [Min(0)] public float smashCooldown = 0.4f;
        [Tooltip("How far in front of the ruler a smash lands, in tiles.")]
        [Min(0)] public float smashReach = 0.8f;
    }
}
