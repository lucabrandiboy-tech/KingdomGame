using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>How a new kingdom starts: where the throne and first storehouse go, and what's in it.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Building/Kingdom Config", fileName = "KingdomConfig")]
    public sealed class KingdomConfig : ScriptableObject
    {
        [Header("Start")]
        [Tooltip("Bottom-left grid cell of the throne (2 x 2 tiles). The ruler starts just below it.")]
        public Vector2Int throneCell = new Vector2Int(-1, 2);
        [Tooltip("Bottom-left grid cell of the first storehouse.")]
        public Vector2Int storageCell = new Vector2Int(-6, -1);
        [Tooltip("Tiles across the kingdom's lands (a square round the throne): it builds there, and its villagers and the zombies that come for it find their way there. The world itself goes on.")]
        [Range(128, 2048)] public int landsSize = 512;
        [Min(0)] public int startWood = 80;
        [Min(0)] public int startStone = 60;
        [Min(0)] public int startFood = 60;
        [Tooltip("Builder villagers at the start.")]
        [Min(0)] public int startVillagers = 3;

        [Header("Building")]
        [Tooltip("Share of the resources already delivered that goes back to storage when you cancel a blueprint.")]
        [Range(0, 1)] public float refundOnCancel = 1f;
        [Tooltip("Most builders working on one blueprint at once.")]
        [Min(1)] public int buildersPerBlueprint = 2;
        [Tooltip("How close (in tiles) the ruler must be to the throne, a storehouse or a blueprint to use it with E.")]
        [Min(0.1f)] public float reach = 0.9f;
        [Tooltip("Most blueprints placed with one drag.")]
        [Min(1)] public int maxLine = 40;
        [Tooltip("A roof needs a wall or door at most this many tiles away to hold it up (it caves in when they're gone).")]
        [Min(1)] public int roofReach = 4;
        [Tooltip("Pixels per tile of the roof pictures (drawn while playing, for each roof's shape). Lower saves video memory.")]
        [Range(16, 128)] public int roofDetail = 64;

        [Header("Rooms (I shows them all)")]
        [Tooltip("Villagers and soldiers hurt below this share of their health go to an infirmary's bed (if there's one free)...")]
        [Range(0, 1)] public float recoverBelow = 0.6f;
        [Tooltip("...and get back this many hit points a second there (a bed elsewhere heals only slowly, at night).")]
        [Min(0)] public float infirmaryHealPerSecond = 4f;
        [Tooltip("Soldiers asleep in a barracks' beds heal this many times as fast.")]
        [Min(1)] public float barracksHealing = 2f;
        [Tooltip("A library makes research this much faster (0.1 = 10%).")]
        [Range(0, 1)] public float libraryResearch = 0.1f;
        [Tooltip("A workshop makes crafters this much faster (0.15 = 15%).")]
        [Range(0, 1)] public float workshopCrafting = 0.15f;
    }
}
