using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// One kind of hit (bullet, explosion, brute smash): how big a hole it makes,
    /// how hard it hits, how many pixels fly off and how much the screen shakes.
    /// Edit the assets in Assets/Data/Phase1 to tune them.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Destruction/Damage Type", fileName = "DamageType")]
    public sealed class DamageType : ScriptableObject
    {
        [Header("Hole shape (32 pixels = 1 tile)")]
        [Tooltip("Radius of the hole in pixels.")]
        [Min(0)] public int radiusPixels = 3;
        [Tooltip("Damage to the pixel in the middle of the hit. A pixel breaks when its health reaches 0.")]
        [Range(1, 255)] public int centerDamage = 80;
        [Tooltip("Damage to pixels at the edge of the hole.")]
        [Range(0, 255)] public int edgeDamage = 30;
        [Tooltip("0 = perfectly round hole, 255 = very ragged edge.")]
        [Range(0, 255)] public int jaggedness = 100;
        [Tooltip("1 = ragged edge per pixel. 3-4 = big blocky chunks, like a smash.")]
        [Range(1, 8)] public int noiseBlock = 1;

        [Header("Flying pixels")]
        [Tooltip("Share of broken pixels that fly off. The rest turn to dust.")]
        [Range(0, 1)] public float debrisChance = 0.6f;
        [Tooltip("Sideways speed of flying pixels, in tiles per second.")]
        public Vector2 debrisSpeed = new Vector2(1.5f, 5f);
        [Tooltip("Upward speed of flying pixels (they arc up and land), in tiles per second.")]
        public Vector2 debrisLift = new Vector2(1f, 4f);

        [Header("Sparks (shown even when nothing breaks)")]
        [Tooltip("Bright pixels that burst out at the point of impact and vanish (no rubble).")]
        [Min(0)] public int sparks = 0;
        public Color32 sparkColor = new Color32(255, 196, 92, 255);

        [Header("Feel")]
        [Tooltip("How much the camera shakes, in tiles.")]
        [Min(0)] public float screenShake = 0.03f;

        public DamageShapeParams ToShape(int radiusOverride) => new DamageShapeParams
        {
            RadiusPx = radiusOverride > 0 ? radiusOverride : radiusPixels,
            CenterDamage = centerDamage,
            EdgeDamage = Mathf.Min(edgeDamage, centerDamage),
            Jaggedness = jaggedness,
            NoiseBlock = noiseBlock,
        };
    }
}
