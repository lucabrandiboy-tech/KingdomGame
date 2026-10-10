using UnityEngine;

namespace KingdomGame
{
    /// <summary>The ways to play.</summary>
    public enum GameMode
    {
        /// <summary>Endless waves of zombies; the game ends when the ruler falls.</summary>
        Survival,
        /// <summary>No zombies: build in peace (the ruler comes back at the throne if they die).</summary>
        FreeBuild,
    }

    /// <summary>
    /// What the main menu chose, carried into the game scene. When the game scene
    /// is played straight from the editor (no menu), the scene's own default is used.
    /// </summary>
    public static class GameSession
    {
        public const string MenuScene = "MainMenu";
        public const string GameScene = "Kingdom";

        /// <summary>The mode picked in the main menu, or null if the game scene was started directly.</summary>
        public static GameMode? Chosen { get; set; }
        /// <summary>Load the saved game of the chosen mode instead of starting a new one (the game scene clears it once read).</summary>
        public static bool LoadSave { get; set; }

        // Statics survive between plays when Unity's "Enter Play Mode Options" skip reloading scripts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Chosen = null;
            LoadSave = false;
        }
    }
}
