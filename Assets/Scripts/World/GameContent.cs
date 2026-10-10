using KingdomGame.Building;
using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using KingdomGame.Economy;
using KingdomGame.Villagers;
using KingdomGame.Zombies;
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
        [Tooltip("The baked art (made by KingdomGame > Build Game Scenes).")]
        public ArtLibrary art;

        [Header("Kingdom")]
        public KingdomConfig kingdom;
        public VillagerConfig villager;
        [Tooltip("The storehouse your kingdom starts with.")]
        public BuildingConfig storehouse;
        [Tooltip("What you can build from the throne. Each goes on its category's tab of the build bar, in this order (keys 1-9).")]
        public BuildingConfig[] buildMenu = new BuildingConfig[0];

        [Header("Survival")]
        [Tooltip("The waves of zombies, and the kinds of zombie.")]
        public SurvivalConfig survival;

        [Tooltip("Bumped by Build Game Scenes when it updates older settings to new defaults.")]
        public int settingsVersion;

        /// <summary>Every wall material the game uses (for drawing and baking wall tiles).</summary>
        public System.Collections.Generic.List<StructureMaterial> WallMaterials()
        {
            var list = new System.Collections.Generic.List<StructureMaterial>();
            void Add(StructureMaterial m) { if (m != null && !list.Contains(m)) list.Add(m); }
            Add(stone);
            Add(wood);
            if (buildMenu != null)
                foreach (var b in buildMenu)
                    if (b != null) Add(b.wallMaterial);
            return list;
        }
    }
}
