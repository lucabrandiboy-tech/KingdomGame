using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>How a wall looks from above. (Saved as numbers, so Phase 2A's Bricks and Planks became Stone and Logs.)</summary>
    public enum WallPattern
    {
        Stone,
        Logs,
    }

    /// <summary>
    /// What a wall is made of: how tough each pixel is and how it looks.
    /// The wall pictures are drawn in code (<see cref="WallTiles"/>, <see cref="Art.WallArt"/>).
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

        [Header("Zombies")]
        [Tooltip("How much zombie battering one tile of this wall takes before it caves in (a walker does 10 a blow, a brute 60). Zombies' blows only scratch the picture; this is what wears the wall down. Less is left once explosions have broken pieces off.")]
        [Min(1)] public float siegeHealth = 400f;

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
        public WallPattern pattern = WallPattern.Stone;
        [Tooltip("Mortar (between stones) or the dark gaps between logs.")]
        public Color32 gapColor = new Color32(52, 46, 56, 255);
        [Tooltip("Stones or logs pick from these colors (the art weathers them a little).")]
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
