using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>How fires start, burn, spread and are put out (see <see cref="Fires"/>).</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Building/Fire Config", fileName = "FireConfig")]
    public sealed class FireConfig : ScriptableObject
    {
        [Tooltip("Off: nothing ever catches fire.")]
        public bool fire = true;

        [Header("Burning")]
        [Tooltip("How fast a fire grows to full heat (heat a second; 0.15 = about 7 seconds).")]
        [Min(0.01f)] public float heatPerSecond = 0.15f;
        [Tooltip("Wear on a burning wall tile a second at full heat (a wood wall has 200).")]
        [Min(0)] public float wallDamagePerSecond = 12f;
        [Tooltip("Damage a second to a burning door, piece of furniture or chest at full heat.")]
        [Min(0)] public float buildingDamagePerSecond = 12f;
        [Tooltip("Damage a second to a burning house at full heat (houses have 600 and more).")]
        [Min(0)] public float houseDamagePerSecond = 20f;

        [Header("Spreading")]
        [Tooltip("Chance a second that a fire at full heat sets a wooden thing next to it alight.")]
        [Range(0, 1)] public float spreadPerSecond = 0.1f;
        [Tooltip("How close (tiles from the burning thing's edge) something must be to catch.")]
        [Min(0)] public float spreadReach = 0.6f;
        [Tooltip("In deep snow fires spread this much as fast.")]
        [Range(0, 1)] public float snowSpread = 0.35f;

        [Header("Putting it out")]
        [Tooltip("Heat a bucket of water from a well takes off (a fire at full heat has 1: a bucket puts out one burning thing; the fight is against the spreading).")]
        [Min(0)] public float bucketDouse = 1.2f;
        [Tooltip("Heat the ruler beats out a second (hold E at it).")]
        [Min(0)] public float rulerDousePerSecond = 0.3f;
        [Tooltip("Heat heavy rain takes off a second.")]
        [Min(0)] public float rainDousePerSecond = 0.12f;
        [Tooltip("Most villagers bringing water to one fire at a time.")]
        [Min(1)] public int firefightersPerFire = 2;

        [Header("What starts fires (chances, 0-1)")]
        [Range(0, 1)] public float explosionIgnites = 0.15f;
        [Range(0, 1)] public float lightningIgnites = 1f;
        [Tooltip("A flamethrower's shot hitting something wooden.")]
        [Range(0, 1)] public float flameShotIgnites = 0.3f;
        [Tooltip("A napalm cloud going off, for each wooden thing in it.")]
        [Range(0, 1)] public float napalmIgnites = 0.3f;
        [Tooltip("A torch, brazier, lamp or fireplace knocked over (broken), for each wooden thing next to it.")]
        [Range(0, 1)] public float knockedOverIgnites = 0.6f;
        [Tooltip("A burning zombie's blow on something wooden.")]
        [Range(0, 1)] public float burningBlowIgnites = 0.35f;

        [Tooltip("Most fires burning at once (more don't start).")]
        [Min(1)] public int maxFires = 300;
    }
}
