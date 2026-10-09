using System;
using System.Collections.Generic;

namespace KingdomGame.Destruction
{
    /// <summary>A pixel rectangle: X, Y is its bottom-left pixel.</summary>
    public struct PixelRect
    {
        public int X, Y, Width, Height;
    }

    /// <summary>Anything that can say whether a world pixel is solid wall.</summary>
    public interface IPixelSolid
    {
        bool IsSolid(int pixelX, int pixelY);
    }

    /// <summary>
    /// Grid algorithms for destructible walls. Masks are row-major byte arrays
    /// (index = y * width + x) where any value above 0 means "solid pixel".
    /// Plain C# with no Unity types, so they are unit-tested outside Unity
    /// (tools/tests).
    /// </summary>
    public static class PixelAlgorithms
    {
        /// <summary>
        /// Covers every solid pixel of a mask with rectangles (grow right, then
        /// up), for rebuilding a damaged chunk's collider so units can walk
        /// through holes. <paramref name="visited"/> must be width*height long.
        /// </summary>
        public static void Rectangles(byte[] mask, int width, int height, bool[] visited, List<PixelRect> output)
        {
            output.Clear();
            Array.Clear(visited, 0, width * height);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int start = y * width + x;
                    if (visited[start] || mask[start] == 0) continue;

                    int w = 1;
                    while (x + w < width && !visited[start + w] && mask[start + w] != 0) w++;

                    int h = 1;
                    while (y + h < height && RowIsFree(mask, visited, (y + h) * width + x, w)) h++;

                    for (int yy = y; yy < y + h; yy++)
                        Array.Fill(visited, true, yy * width + x, w);

                    output.Add(new PixelRect { X = x, Y = y, Width = w, Height = h });
                }
            }
        }

        static bool RowIsFree(byte[] mask, bool[] visited, int rowStart, int width)
        {
            for (int i = rowStart; i < rowStart + width; i++)
                if (visited[i] || mask[i] == 0) return false;
            return true;
        }

        /// <summary>
        /// Splits the solid pixels of a mask into separate pieces. Pixels that
        /// touch only at a corner count as separate pieces. Writes each pixel's
        /// piece number into <paramref name="labels"/> (-1 for empty) and each
        /// piece's pixel count into <paramref name="sizes"/>. <paramref name="labels"/>
        /// and <paramref name="stack"/> must be width*height long.
        /// Returns the number of pieces.
        /// </summary>
        public static int Islands(byte[] mask, int width, int height, int[] labels, int[] stack, List<int> sizes)
        {
            sizes.Clear();
            int count = width * height;
            Array.Fill(labels, -1, 0, count);

            for (int seed = 0; seed < count; seed++)
            {
                if (labels[seed] != -1 || mask[seed] == 0) continue;

                int island = sizes.Count;
                int size = 0;
                int top = 0;
                stack[top++] = seed;
                labels[seed] = island;

                while (top > 0)
                {
                    int i = stack[--top];
                    size++;
                    int x = i % width;

                    int n = i - 1;
                    if (x > 0 && labels[n] == -1 && mask[n] != 0) { labels[n] = island; stack[top++] = n; }
                    n = i + 1;
                    if (x < width - 1 && labels[n] == -1 && mask[n] != 0) { labels[n] = island; stack[top++] = n; }
                    n = i - width;
                    if (n >= 0 && labels[n] == -1 && mask[n] != 0) { labels[n] = island; stack[top++] = n; }
                    n = i + width;
                    if (n < count && labels[n] == -1 && mask[n] != 0) { labels[n] = island; stack[top++] = n; }
                }
                sizes.Add(size);
            }
            return sizes.Count;
        }

        /// <summary>
        /// Walks a straight line through the pixel grid from (x0, y0) to (x1, y1),
        /// in pixel units, and stops at the first solid pixel. Bullets use it, so
        /// they fly through holes and hit exactly the pixel they touch.
        /// <paramref name="t"/> is how far along the line the hit is, 0 to 1.
        /// </summary>
        public static bool Raycast<T>(T world, double x0, double y0, double x1, double y1,
            out int hitX, out int hitY, out double t) where T : IPixelSolid
        {
            int x = (int)Math.Floor(x0);
            int y = (int)Math.Floor(y0);
            double dx = x1 - x0;
            double dy = y1 - y0;

            int stepX = dx > 0 ? 1 : dx < 0 ? -1 : 0;
            int stepY = dy > 0 ? 1 : dy < 0 ? -1 : 0;
            double tDeltaX = stepX != 0 ? 1.0 / Math.Abs(dx) : double.PositiveInfinity;
            double tDeltaY = stepY != 0 ? 1.0 / Math.Abs(dy) : double.PositiveInfinity;
            double tMaxX = stepX > 0 ? (x + 1 - x0) / dx : stepX < 0 ? (x0 - x) / -dx : double.PositiveInfinity;
            double tMaxY = stepY > 0 ? (y + 1 - y0) / dy : stepY < 0 ? (y0 - y) / -dy : double.PositiveInfinity;

            // Exactly this many steps go from the first pixel to the last one.
            int steps = Math.Abs((int)Math.Floor(x1) - x) + Math.Abs((int)Math.Floor(y1) - y);
            double entry = 0;
            for (int i = 0; ; i++)
            {
                if (world.IsSolid(x, y))
                {
                    hitX = x;
                    hitY = y;
                    t = Math.Min(Math.Max(entry, 0), 1);
                    return true;
                }
                if (i == steps) break;

                if (tMaxX < tMaxY)
                {
                    x += stepX;
                    entry = tMaxX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    y += stepY;
                    entry = tMaxY;
                    tMaxY += tDeltaY;
                }
            }

            hitX = hitY = 0;
            t = 1;
            return false;
        }
    }
}
