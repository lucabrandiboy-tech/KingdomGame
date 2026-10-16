using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>The wild animals: herds of deer to hunt for food, and wolves that come at night (see <see cref="Wildlife"/>).</summary>
    [CreateAssetMenu(menuName = "KingdomGame/World/Wildlife Config", fileName = "WildlifeConfig")]
    public sealed class WildlifeConfig : ScriptableObject
    {
        [Tooltip("Off: no deer and no wolves.")]
        public bool wildlife = true;

        [Header("Where they turn up")]
        [Tooltip("New herds and packs appear this many tiles from the ruler (always out of sight)...")]
        [Min(10)] public float spawnNear = 45f;
        [Tooltip("...and at most this far.")]
        [Min(15)] public float spawnFar = 65f;
        [Tooltip("Animals further than this from the ruler (and out of sight) go away; new ones come instead.")]
        [Min(30)] public float goneBeyond = 110f;
        [Tooltip("No herd or pack appears closer than this to your throne.")]
        [Min(0)] public float kingdomClear = 30f;

        [Header("Deer")]
        [Tooltip("How many deer are about at once (round the ruler).")]
        [Min(0)] public int deer = 12;
        [Tooltip("In winter only this share of them.")]
        [Range(0, 1)] public float winterDeer = 0.5f;
        [Tooltip("Smallest and biggest herd.")]
        public Vector2Int herdSize = new Vector2Int(3, 5);
        [Min(1)] public float deerHealth = 40f;
        [Tooltip("Tiles per second, wandering while they graze...")]
        [Min(0.1f)] public float deerWalkSpeed = 0.9f;
        [Tooltip("...and running away.")]
        [Min(0.1f)] public float deerRunSpeed = 5f;
        [Tooltip("They run from people and zombies this close (and from wolves a little further off), and the whole herd runs with them.")]
        [Min(1)] public float deerShyDistance = 7f;
        [Tooltip("Food a deer leaves when it's killed (a bag where it falls: walk over it, or a hunter fetches it).")]
        [Min(0)] public int deerMeat = 20;

        [Header("Wolves")]
        [Tooltip("Packs about at night at most.")]
        [Min(0)] public int wolfPacks = 2;
        [Tooltip("Smallest and biggest pack.")]
        public Vector2Int packSize = new Vector2Int(2, 3);
        [Tooltip("The first night wolves come (day 1 is the first day).")]
        [Min(1)] public int firstWolfNight = 2;
        [Tooltip("Seconds between one pack arriving and the next (shortest, longest).")]
        public Vector2 packSeconds = new Vector2(60f, 150f);
        [Min(1)] public float wolfHealth = 55f;
        [Tooltip("Tiles per second, prowling...")]
        [Min(0.1f)] public float wolfWalkSpeed = 1.8f;
        [Tooltip("...and rushing in for the kill (the ruler walks at 3.2, so you can't outrun them).")]
        [Min(0.1f)] public float wolfRunSpeed = 4.2f;
        [Tooltip("They go for anyone out in the dark this close.")]
        [Min(1)] public float wolfSight = 20f;
        [Tooltip("They rush in from this close.")]
        [Min(1)] public float wolfRushDistance = 8f;
        [Tooltip("Damage per bite, to the ruler or a villager.")]
        [Min(0)] public float biteDamage = 7f;
        [Tooltip("Seconds per bite (it lands half way).")]
        [Min(0.2f)] public float biteSeconds = 1.2f;
        [Tooltip("How far a bite reaches past its body, in tiles.")]
        [Min(0)] public float biteReach = 0.3f;
        [Tooltip("Wolves keep out of firelight: out of this share of a fire's light (braziers, torches, campfires, burning buildings).")]
        [Range(0, 1)] public float fireFear = 0.6f;
        [Tooltip("A wolf this badly hurt (share of its health left) runs off for good.")]
        [Range(0, 1)] public float wolfFleeBelow = 0.35f;
        [Tooltip("Food a wolf leaves (0: none).")]
        [Min(0)] public int wolfMeat = 6;
    }
}
