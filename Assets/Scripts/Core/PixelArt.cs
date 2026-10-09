namespace KingdomGame
{
    /// <summary>
    /// The project's pixel density: the one place these numbers live.
    ///
    /// One grid tile = one Unity unit = 32 x 32 pixels. Every sprite in the game
    /// is drawn at this density so nothing looks blurrier or chunkier than
    /// anything else. Changing <see cref="PixelsPerUnit"/> later means redrawing
    /// or re-importing all art, so treat it as fixed.
    ///
    /// The reference resolution is how many game pixels the camera shows.
    /// 640 x 360 is 20 x 11.25 tiles, and it scales up by a whole number on every
    /// common 16:9 screen (2x on 720p, 3x on 1080p, 4x on 1440p, 6x on 4K), so
    /// pixels stay perfectly square and every player sees the same amount of the
    /// world. Unlike the pixel density, this can be changed at any time without
    /// touching the art (960 x 540 is the next option if the view feels cramped).
    /// </summary>
    public static class PixelArt
    {
        public const int PixelsPerUnit = 32;
        public const int ReferenceWidth = 640;
        public const int ReferenceHeight = 360;
    }
}
