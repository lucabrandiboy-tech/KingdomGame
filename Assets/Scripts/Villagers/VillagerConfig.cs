using UnityEngine;

namespace KingdomGame.Villagers
{
    /// <summary>How builder villagers move and work.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Villagers/Villager Config", fileName = "VillagerConfig")]
    public sealed class VillagerConfig : ScriptableObject
    {
        [Tooltip("Walking speed, in tiles per second.")]
        [Min(0.1f)] public float moveSpeed = 2.4f;
        [Tooltip("How much they carry per trip.")]
        [Min(1)] public int carryAmount = 20;
        [Tooltip("Seconds of building work done per second (1 = normal).")]
        [Min(0.1f)] public float workRate = 1f;
        [Tooltip("How often an idle villager looks for work, in seconds.")]
        [Min(0.05f)] public float thinkSeconds = 0.5f;
        [Tooltip("Steps per second of the walking animation.")]
        [Min(0.1f)] public float stepsPerSecond = 7f;

        [Header("Danger")]
        [Min(1)] public float maxHealth = 60f;
        [Tooltip("Villagers drop their work and run when they see a zombie this close (tiles).")]
        [Min(0)] public float fleeDistance = 6f;
    }
}
