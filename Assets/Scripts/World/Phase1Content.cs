using KingdomGame.Character;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>Which assets the Phase 1 test scene uses. Made by KingdomGame > Phase 1 > Create Test Scenes.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Phase 1/Content", fileName = "Phase1Content")]
    public sealed class Phase1Content : ScriptableObject
    {
        public PlayerConfig player;
        public WeaponConfig rifle;
        public WeaponConfig rocketLauncher;
        public DamageType smash;
        public StructureMaterial stone;
        public StructureMaterial wood;
    }
}
