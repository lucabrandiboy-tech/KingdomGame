using UnityEngine;

namespace KingdomGame
{
    /// <summary>
    /// The player's settings, kept on this computer (PlayerPrefs): graphics (how sharp the pictures are,
    /// which is most of the video memory the game uses) and sound volume. Set from the Settings screen in
    /// the main menu and the pause menu.
    /// </summary>
    public static class GameSettings
    {
        public enum Graphics { Low, Medium, High }

        const string GraphicsKey = "Graphics", VolumeKey = "Volume", AmbienceKey = "Ambience";

        /// <summary>
        /// Low: pictures at a quarter of their size (a sixteenth of the video memory); Medium: half (a quarter);
        /// High: full. The first time, it's picked from the graphics card's memory.
        /// </summary>
        public static Graphics Quality
        {
            get => (Graphics)Mathf.Clamp(PlayerPrefs.GetInt(GraphicsKey, (int)Suggested()), 0, 2);
            set
            {
                PlayerPrefs.SetInt(GraphicsKey, (int)value);
                PlayerPrefs.Save();
                Apply();
            }
        }

        /// <summary>Sound volume, 0 (off) to 1.</summary>
        public static float Volume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.8f));
            set
            {
                PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
                Apply();
            }
        }

        /// <summary>The background sounds (wind, night, birds), 0 (off) to 1, times <see cref="Volume"/>.</summary>
        public static float Ambience
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(AmbienceKey, 0.6f));
            set => PlayerPrefs.SetFloat(AmbienceKey, Mathf.Clamp01(value));
        }

        public static string Name(Graphics g) => g == Graphics.Low ? "Low" : g == Graphics.Medium ? "Medium" : "High";

        public static string Describe(Graphics g) =>
            g == Graphics.Low ? "blurrier, for older or small graphics cards"
            : g == Graphics.Medium ? "a little softer, a quarter of the video memory"
            : "sharpest (needs about 1 GB of video memory)";

        // From the graphics card's memory: High from 3 GB, Medium from 1.5 GB, otherwise Low.
        static Graphics Suggested()
        {
            int mb = SystemInfo.graphicsMemorySize;
            if (mb <= 0 || mb >= 3000) return Graphics.High;
            return mb >= 1500 ? Graphics.Medium : Graphics.Low;
        }

        /// <summary>Puts the settings into effect (at the start, and whenever one changes).</summary>
        public static void Apply()
        {
            // Every baked sheet has mipmaps: dropping the sharpest one or two levels halves or quarters the pictures.
            int skip = 2 - (int)Quality;
#if UNITY_6000_0_OR_NEWER
            if (QualitySettings.globalTextureMipmapLimit != skip) QualitySettings.globalTextureMipmapLimit = skip;
#else
            if (QualitySettings.masterTextureLimit != skip) QualitySettings.masterTextureLimit = skip;
#endif
            AudioListener.volume = Volume;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyAtStart() => Apply();
    }
}
