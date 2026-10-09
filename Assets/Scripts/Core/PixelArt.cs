namespace KingdomGame
{
    /// <summary>
    /// The project's art density: the one place this number lives.
    ///
    /// One grid tile = one Unity unit = 64 x 64 texture pixels. Every sprite is
    /// drawn at this density so nothing looks blurrier or chunkier than anything
    /// else, and wall destruction works on these pixels. Changing it means
    /// redrawing or re-importing all art, so treat it as fixed.
    ///
    /// Since the switch to the dark realistic look (Phase 2) the camera zooms
    /// freely and textures are smoothly filtered; there is no pixel-perfect
    /// camera any more. The view is set in the ViewConfig asset (walking zoom 12
    /// = 24 tiles from top to bottom, about 45 screen pixels per tile on a
    /// 1080p screen); mipmaps keep the art smooth when it's shown smaller.
    /// </summary>
    public static class PixelArt
    {
        public const int PixelsPerUnit = 64;
    }
}
