using UnityEngine;

namespace KingdomGame.Economy
{
    public enum NodeLook
    {
        Tree,
        Rock,
        BerryBush,
    }

    /// <summary>A kind of thing you can gather from: trees give wood, rocks stone, berry bushes food.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Economy/Resource Node", fileName = "ResourceNode")]
    public sealed class ResourceNodeConfig : ScriptableObject
    {
        public NodeLook look = NodeLook.Tree;
        public ResourceType resource = ResourceType.Wood;
        [Tooltip("How much it holds before it's used up.")]
        [Min(1)] public int amount = 60;
        [Tooltip("How much each swing gathers.")]
        [Min(1)] public int perHit = 5;
        [Tooltip("Seconds between swings while gathering.")]
        [Min(0.05f)] public float secondsPerHit = 0.5f;
        [Tooltip("Seconds until it's full again after being used up. 0 = never (trees leave a stump, rocks disappear).")]
        [Min(0)] public float regrowSeconds = 0f;
        [Tooltip("Planted (a tree farm): when it's used up, a young one grows in its place over Regrow Seconds, instead of leaving a stump. Woodcutters go for these first.")]
        public bool replant;
        [Tooltip("Size of the solid part you bump into, in tiles (the trunk, not the leaves).")]
        [Min(0.05f)] public float blockRadius = 0.2f;
        [Tooltip("How close the ruler must be to gather, in tiles from the solid part's edge.")]
        [Min(0.1f)] public float reach = 0.6f;
        [Tooltip("Tints its picture (white = as drawn): tells the farmed kinds apart (fruit trees, pines, oaks...).")]
        public Color32 tint = new Color32(255, 255, 255, 255);
        [Tooltip("Color of the chips that fly off each swing.")]
        public Color32 chipColor = new Color32(150, 110, 70, 255);
    }
}
