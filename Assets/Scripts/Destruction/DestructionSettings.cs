using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// Global settings for wall destruction: limits that keep it fast, how
    /// flying pixels move, and when damaged walls crumble.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Destruction/Destruction Settings", fileName = "DestructionSettings")]
    public sealed class DestructionSettings : ScriptableObject
    {
        [Header("Flying pixels (performance limits)")]
        [Tooltip("Most flying pixels at once. Extra broken pixels land straight away as rubble.")]
        [Min(0)] public int maxDebris = 2500;
        [Tooltip("Most new flying pixels per frame, so one huge collapse can't freeze the game.")]
        [Min(0)] public int maxNewDebrisPerFrame = 800;

        [Header("Flying pixels (movement)")]
        [Tooltip("Pulls pixels back down after they pop up, in tiles per second squared.")]
        public float gravity = 30f;
        [Tooltip("Slows pixels while in the air.")]
        public float airDrag = 0.8f;
        [Tooltip("Slows pixels sliding on the ground.")]
        public float groundFriction = 9f;
        [Tooltip("How much of their fall speed pixels keep when they bounce.")]
        [Range(0, 1)] public float bounce = 0.35f;
        [Tooltip("Below this speed a landed pixel settles into rubble, in tiles per second.")]
        public float settleSpeed = 0.25f;
        [Tooltip("Pixels settle after this many seconds no matter what.")]
        public float maxDebrisSeconds = 3f;

        [Header("Rubble")]
        [Tooltip("Settled pixels stay on the ground as rubble.")]
        public bool rubble = true;
        [Tooltip("How dark rubble is compared to the wall it came from (1 = same color).")]
        [Range(0, 1)] public float rubbleBrightness = 0.6f;

        [Header("Crumbling")]
        [Tooltip("Pieces cut off from the rest of a wall that are smaller than this many pixels crumble.")]
        [Min(0)] public int minStandingPixels = 200;
        [Tooltip("A wall with less than this share of its pixels left collapses completely.")]
        [Range(0, 1)] public float collapseBelow = 0.3f;

        [Header("Drawing order (higher = drawn on top)")]
        public int rubbleSortingOrder = -10;
        public int wallSortingOrder = 0;
        [Tooltip("Above the ruler and trees (which are sorted by height on screen, around 5000).")]
        public int debrisSortingOrder = 20000;
    }
}
