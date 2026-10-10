using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// The underground of a mine, painted a tile at a time into one big picture (the game
    /// paints it again round each tile that's dug): lumpy cracked rock lit from the
    /// top-left, with flecks of ore in it and a pale cut face where it meets the floor,
    /// and packed earth floor, darker in the rock's shadow.
    /// </summary>
    public static class MineArt
    {
        public const int Copper = 1, Iron = 2, Gold = 3;

        /// <summary>
        /// Paints one tile into <paramref name="pixels"/> (<paramref name="width"/> pixels across, bottom row first)
        /// at pixel (<paramref name="ox"/>, <paramref name="oy"/>), <paramref name="detail"/> pixels across.
        /// <paramref name="wx"/>, <paramref name="wy"/>: the tile's place in the world (for the noise).
        /// The open sides are where there's dug floor next to it.
        /// </summary>
        public static void PaintCell(Color32[] pixels, int width, int detail, int ox, int oy, int wx, int wy, uint seed,
            bool dug, int ore, bool openLeft, bool openRight, bool openUp, bool openDown)
        {
            for (int py = 0; py < detail; py++)
                for (int px = 0; px < detail; px++)
                {
                    float u = (px + 0.5f) / detail, v = (py + 0.5f) / detail;
                    float x = wx + u, y = wy + v;
                    Color32 c;
                    if (dug)
                    {
                        // Packed earth, darker in the shadow of rock above and to the left (the light is top-left).
                        float grit = Noise.Hash01(seed + 7u, wx * detail + px, wy * detail + py);
                        float patch = Noise.Value(x * 1.3f, y * 1.3f, seed + 8u);
                        float shade = 0.82f + patch * 0.22f + (grit - 0.5f) * 0.1f;
                        if (!openUp) shade *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01((1f - v) / 0.3f));
                        if (!openLeft) shade *= Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(u / 0.25f));
                        c = PixelImage.Scale(PixelImage.Rgb(74, 62, 50), shade);
                    }
                    else
                    {
                        float lumps = Noise.Value(x * 3.2f, y * 3.2f, seed + 1u);
                        float fine = Noise.Hash01(seed + 2u, wx * detail + px, wy * detail + py);
                        float crack = Mathf.Abs(Noise.Value(x * 5f, y * 5f, seed + 3u) - 0.5f);
                        float shade = 0.78f + lumps * 0.3f + (fine - 0.5f) * 0.08f;
                        if (crack < 0.025f) shade *= 0.6f;
                        c = PixelImage.Scale(PixelImage.Rgb(96, 90, 82), shade);
                        // The cut face where it meets the floor: bright facing the light, dark away from it.
                        float edge = Mathf.Min(Mathf.Min(openLeft ? u : 1f, openRight ? 1f - u : 1f), Mathf.Min(openUp ? 1f - v : 1f, openDown ? v : 1f));
                        if (edge < 0.14f)
                        {
                            bool lit = (openLeft && u < 0.14f) || (openUp && 1f - v < 0.14f);
                            c = PixelImage.Scale(c, lit ? 1.25f : 0.7f);
                        }
                        if (ore != 0)
                        {
                            // Nuggets of ore set in the rock: rounded, lit from the top-left, with a glint.
                            var oreColor = ore == Copper ? PixelImage.Rgb(196, 112, 58) : ore == Iron ? PixelImage.Rgb(150, 96, 84) : PixelImage.Rgb(236, 190, 64);
                            for (int k = 0; k < 7; k++)
                            {
                                float nx = 0.12f + Noise.Hash01(seed + 20u + (uint)k, wx, wy) * 0.76f;
                                float ny = 0.12f + Noise.Hash01(seed + 40u + (uint)k, wx, wy) * 0.76f;
                                float nr = 0.045f + Noise.Hash01(seed + 60u + (uint)k, wx, wy) * 0.06f;
                                float dx = (u - nx) / nr, dy = (v - ny) / nr;
                                float d2 = dx * dx + dy * dy;
                                if (d2 >= 1f) continue;
                                float nz = Mathf.Sqrt(1f - d2);
                                float lit = Mathf.Clamp01(-dx * 0.5f + dy * 0.55f + nz * 0.67f);
                                c = PixelImage.Scale(oreColor, 0.55f + lit * 0.7f);
                                if (lit > 0.92f) c = PixelImage.Mix(c, PixelImage.Rgb(255, 248, 220), 0.6f);
                                break;
                            }
                        }
                    }
                    c.a = 255;
                    pixels[(oy + py) * width + ox + px] = c;
                }
        }
    }
}
