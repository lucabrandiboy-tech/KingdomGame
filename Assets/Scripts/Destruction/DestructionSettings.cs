using UnityEngine;

namespace KingdomGame.Destruction
{
    /// <summary>
    /// Global settings for wall destruction: limits that keep it fast, how
    /// the flying pieces move and look, and when damaged walls crumble.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Destruction/Destruction Settings", fileName = "DestructionSettings")]
    public sealed class DestructionSettings : ScriptableObject
    {
        [Header("Flying pieces (performance limits)")]
        [Tooltip("Most flying pieces at once. Extra broken pixels land straight away as rubble.")]
        [Min(0)] public int maxDebris = 1500;
        [Tooltip("Most new flying pieces per frame, so one huge collapse can't freeze the game.")]
        [Min(0)] public int maxNewDebrisPerFrame = 400;

        [Header("Flying pieces (look)")]
        [Tooltip("Flying pieces are little cubes (drawn to look 3D) this many pixels across, picked at random.")]
        public Vector2 cubeSize = new Vector2(4f, 9f);
        [Tooltip("Share of broken pixels that become cubes, on top of each damage type's Debris Chance. Cubes are bigger than pixels, so fewer are needed.")]
        [Range(0, 1)] public float cubeDensity = 0.3f;
        [Tooltip("How much bigger a cube looks per tile of height (it's closer to the camera).")]
        [Min(0)] public float heightScale = 0.35f;

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

        [Header("Walls")]
        [Tooltip("How tall walls look: their shadows fall this many tiles away.")]
        [Min(0)] public float wallHeight = 0.22f;

        [Header("Explosions")]
        [Tooltip("Scorch marks left on the ground, as a share of the blast radius (0 = none).")]
        [Range(0, 2)] public float scorchSize = 0.9f;
        [Tooltip("Smoke puffs per explosion, per 10 pixels of blast radius.")]
        [Min(0)] public float smokePerTenPixels = 1.5f;
        public Color smokeColor = new Color(0.22f, 0.21f, 0.2f, 0.75f);
        [Tooltip("Color of the flash of light when something explodes.")]
        public Color flashColor = new Color(1f, 0.62f, 0.3f);
    }
}
