namespace KingdomGame
{
    /// <summary>
    /// The project's pixel density: the one place these numbers live.
    ///
    /// One grid tile = one Unity unit = 64 x 64 pixels (it was 32 until Phase 2).
    /// Every sprite in the game is drawn at this density so nothing looks blurrier
    /// or chunkier than anything else. Changing <see cref="PixelsPerUnit"/> means
    /// redrawing or re-importing all art, so treat it as fixed.
    ///
    /// The reference resolution is how many game pixels the camera aims to show:
    /// 1280 x 720 is 20 x 11.25 tiles. It scales up by a whole number on 1440p (2x)
    /// and 4K (3x) screens. A 1080p screen can't show it at a whole-number size,
    /// so it shows game pixels 1:1 and sees more of the world (30 x 17 tiles).
    /// Unlike the pixel density, this can be changed without touching the art.
    /// </summary>
    public static class PixelArt
    {
        public const int PixelsPerUnit = 64;
        public const int ReferenceWidth = 1280;
        public const int ReferenceHeight = 720;
    }
}
