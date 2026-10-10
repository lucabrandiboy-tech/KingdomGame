namespace KingdomGame
{
    /// <summary>
    /// Whether a big panel (the tech screen) is open over the game, so clicks and
    /// keys go to it: the ruler doesn't shoot or use things, the throne view doesn't place.
    /// </summary>
    public static class UiFocus
    {
        static bool open;
        static int closedFrame = -1;

        public static bool PanelOpen
        {
            get => open;
            set
            {
                if (open && !value) closedFrame = UnityEngine.Time.frameCount;
                open = value;
            }
        }

        /// <summary>Typing in the chat: keys are letters, not game keys (GameInput ignores them meanwhile).</summary>
        public static bool Typing { get; set; }

        /// <summary>A panel is open, or closed this frame: its Esc mustn't also pause the game or stand the ruler up.</summary>
        public static bool TakesEscape => open || closedFrame == UnityEngine.Time.frameCount;

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            open = false;
            closedFrame = -1;
            Typing = false;
        }
    }
}
