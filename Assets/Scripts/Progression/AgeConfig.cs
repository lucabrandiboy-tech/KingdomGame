using KingdomGame.Building;
using UnityEngine;

namespace KingdomGame.Progression
{
    /// <summary>One age of the kingdom (Stone Age to World War 2): what you can build straight away, and its techs.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Progression/Age", fileName = "Age")]
    public sealed class AgeConfig : ScriptableObject
    {
        public string displayName = "Stone Age";
        [TextArea(2, 4)] public string description = "";
        [Tooltip("Its colour on the tech screen and the build bar.")]
        public Color color = new Color(0.8f, 0.7f, 0.5f);
        [Tooltip("What you can build as soon as you reach this age, without research.")]
        public BuildingConfig[] startUnlocks = new BuildingConfig[0];
        [Tooltip("Its techs; the one that Advances Age leads to the next age.")]
        public TechConfig[] techs = new TechConfig[0];
        [Tooltip("Goals to reach in this age, each with a reward (shown on screen, J hides them).")]
        public AgeGoal[] goals = new AgeGoal[0];
    }
}
