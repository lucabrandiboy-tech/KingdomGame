using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// Every settings asset the game scene uses, in one place.
    /// Made by KingdomGame > Build Game Scenes (Assets/Data).
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Game Content", fileName = "GameContent")]
    public sealed class GameContent : ScriptableObject
    {
        [Header("Ruler")]
        public PlayerConfig player;
        public WeaponConfig rifle;
        public WeaponConfig rocketLauncher;
        public ExplosiveConfig c4;

        [Header("Walls")]
        public StructureMaterial stone;
        public StructureMaterial wood;

        [Header("Nature")]
        public ResourceNodeConfig tree;
        public ResourceNodeConfig rock;
        public ResourceNodeConfig bush;

        [Header("World")]
        public WorldConfig world;
        public DayNightConfig dayNight;
    }
}
