using UnityEngine;

namespace KingdomGame.Destruction
{
    public enum WallPattern
    {
        Bricks,
        Planks,
    }

    /// <summary>
    /// What a wall is made of: how tough each pixel is and how it looks.
    /// Phase 1 draws the wall pictures in code (<see cref="WallTiles"/>);
    /// real pixel art replaces that later.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Destruction/Structure Material", fileName = "StructureMaterial")]
    public sealed class StructureMaterial : ScriptableObject
    {
        [Tooltip("Health of every pixel. Each hit subtracts its damage; at 0 the pixel breaks off.")]
        [Range(1, 255)] public int pixelHealth = 100;

        [Header("How much damage gets through, in percent (Rust-style)")]
        [Tooltip("Bullets. Stone walls should barely notice them.")]
        [Range(0, 400)] public int bulletPercent = 100;
        [Tooltip("Hits from tools, melee weapons and zombie claws.")]
        [Range(0, 400)] public int meleePercent = 100;
        [Tooltip("Rockets, C4 and other explosives: the way to break into a base.")]
        [Range(0, 400)] public int explosivePercent = 100;

        /// <summary>The percentage of a hit of this kind that gets through. Whole numbers, so every computer agrees.</summary>
        public int PercentFor(DamageCategory category)
        {
            switch (category)
            {
                case DamageCategory.Bullet: return bulletPercent;
                case DamageCategory.Melee: return meleePercent;
                default: return explosivePercent;
            }
        }

        [Header("Look")]
        public WallPattern pattern = WallPattern.Bricks;
        [Tooltip("Mortar (between bricks) or gaps (between planks).")]
        public Color32 gapColor = new Color32(52, 46, 56, 255);
        [Tooltip("Bricks or planks pick from these colors.")]
        public Color32[] colors =
        {
            new Color32(150, 82, 62, 255),
            new Color32(136, 72, 56, 255),
            new Color32(164, 94, 70, 255),
            new Color32(124, 68, 58, 255),
        };
        [Tooltip("How many slightly different tile pictures to make, so long walls don't repeat.")]
        [Range(1, 8)] public int variants = 4;
    }
}
