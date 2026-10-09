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
