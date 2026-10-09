using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.Villagers;
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

        [Header("Camera and look")]
        public ViewConfig view;

        [Header("Kingdom")]
        public KingdomConfig kingdom;
        public VillagerConfig villager;
        [Tooltip("The storehouse your kingdom starts with.")]
        public BuildingConfig storehouse;
        [Tooltip("What you can build from the throne, in build-bar order (keys 1-5).")]
        public BuildingConfig[] buildMenu = new BuildingConfig[0];
    }
}
