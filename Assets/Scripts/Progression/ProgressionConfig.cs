using UnityEngine;

namespace KingdomGame.Progression
{
    /// <summary>
    /// How the kingdom moves through the ages: the ages in order, how fast research
    /// goes (a server setting later), and how survival advances them instead.
    /// </summary>
    [CreateAssetMenu(menuName = "KingdomGame/Progression/Progression Config", fileName = "ProgressionConfig")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        [Tooltip("The ages, first to last.")]
        public AgeConfig[] ages = new AgeConfig[0];
        [Tooltip("Research goes this many times faster (2 = half the time). At 1, each new age takes days of real time: about four weeks to World War 2.")]
        [Min(0.01f)] public float researchSpeed = 1f;
        [Tooltip("How many techs can wait in the research queue (they carry on one after another, even while the game is closed).")]
        [Range(1, 10)] public int queueLength = 5;
        [Tooltip("Survival: off (the default), the ages come by research like everywhere else (days each). On, the kingdom moves into the next age after every Survival Waves Per Age waves instead.")]
        public bool survivalAgesByWaves;
        [Tooltip("Survival with Survival Ages By Waves on: a new age after every this many waves.")]
        [Min(1)] public int survivalWavesPerAge = 3;
        [Tooltip("For testing: F8 finishes the current research at once.")]
        public bool allowCheats = true;
    }
}
