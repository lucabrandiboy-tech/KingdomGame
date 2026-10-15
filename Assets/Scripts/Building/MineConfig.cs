using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>How a mine works: how big it is underground, how long a tile of rock takes to dig, and what's in the rock.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Building/Mine Config", fileName = "MineConfig")]
    public sealed class MineConfig : ScriptableObject
    {
        [Tooltip("Tiles across underground (all rock except a small room at the bottom of the shaft).")]
        [Range(16, 96)] public int size = 64;
        [Tooltip("Tiles across the room dug out round the shaft at the start.")]
        [Range(3, 9)] public int startRoom = 5;
        [Tooltip("Seconds of work for one miner to dig out a tile of rock.")]
        [Min(0.5f)] public float digSeconds = 6f;
        [Tooltip("Stone from a tile of plain rock.")]
        [Min(0)] public int stonePerTile = 6;
        [Tooltip("Ore from a tile with ore in it (instead of stone).")]
        [Min(1)] public int orePerTile = 4;

        [Header("Ore (share of rock tiles with it; rarer ores lie further from the shaft)")]
        [Range(0, 0.5f)] public float copper = 0.08f;
        [Range(0, 0.5f)] public float iron = 0.06f;
        [Range(0, 0.5f)] public float gold = 0.025f;
        [Tooltip("Coal (for generators): found anywhere, near the shaft too.")]
        [Range(0, 0.5f)] public float coal = 0.07f;
        [Tooltip("Tiles from the shaft before iron starts showing up.")]
        [Min(0)] public float ironFrom = 8f;
        [Tooltip("Tiles from the shaft before gold starts showing up.")]
        [Min(0)] public float goldFrom = 16f;
    }
}
