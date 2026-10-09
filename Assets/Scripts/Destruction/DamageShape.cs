using System.Collections.Generic;

namespace KingdomGame.Destruction
{
    /// <summary>The size and feel of one hit, in whole pixels (see <see cref="DamageType"/>).</summary>
    public struct DamageShapeParams
    {
        public int RadiusPx;
        public int CenterDamage;
        public int EdgeDamage;
        /// <summary>0..255. How much the edge of the hole is roughened (0 = perfect circle).</summary>
        public int Jaggedness;
        /// <summary>1 = rough edge per pixel, 3-4 = blocky chunks (brute smashes).</summary>
        public int NoiseBlock;
    }

    public struct PixelDamage
    {
        public int X, Y, Damage;
    }

    /// <summary>
    /// Works out which pixels one hit damages and by how much.
    ///
    /// Only whole-number math, so every computer that receives the same damage
    /// event (position, radius, type, seed) removes exactly the same pixels.
    /// That is what lets multiplayer send small events instead of pixel data.
    /// No Unity types here, so it can be tested outside Unity.
    /// </summary>
    public static class DamageShape
    {
        /// <summary>
        /// Clears <paramref name="output"/> and fills it with every damaged pixel,
        /// in world pixel coordinates. The center pixel is always included.
        /// </summary>
        public static void Compute(int centerX, int centerY, in DamageShapeParams shape, uint seed, List<PixelDamage> output)
        {
            output.Clear();
            int r = shape.RadiusPx < 0 ? 0 : shape.RadiusPx;
            int block = shape.NoiseBlock < 1 ? 1 : shape.NoiseBlock;
            int jag = shape.Jaggedness < 0 ? 0 : shape.Jaggedness > 255 ? 255 : shape.Jaggedness;
            long r256 = r * 256L;

            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    long d2 = (long)dx * dx + (long)dy * dy;
                    if (d2 > (long)r * r) continue;

                    int x = centerX + dx;
                    int y = centerY + dy;

                    // Roughen the edge: shrink the radius by up to jag/255 of it,
                    // using noise tied to world position so neighbouring hits agree.
                    uint noise = Hash(seed, FloorDiv(x, block), FloorDiv(y, block)) & 255u;
                    long rEff256 = r256 - r256 * jag * noise / (255L * 255L);
                    long rEff2 = rEff256 * rEff256;           // radius squared, in 1/65536 pixels
                    long dist2 = d2 * 65536L;                 // same units
                    if (dist2 > rEff2) continue;

                    // Full damage in the middle, fading to EdgeDamage at the edge.
                    int damage = shape.CenterDamage;
                    if (rEff2 > 0)
                        damage -= (int)((long)(shape.CenterDamage - shape.EdgeDamage) * dist2 / rEff2);
                    if (damage <= 0) continue;

                    output.Add(new PixelDamage { X = x, Y = y, Damage = damage });
                }
            }
        }

        /// <summary>Rounds down for negative numbers too (-1 / 32 = -1, not 0).</summary>
        public static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        /// <summary>Same inputs always give the same number, on every computer.</summary>
        public static uint Hash(uint seed, int x, int y)
        {
            unchecked
            {
                uint h = seed * 0x9E3779B1u;
                h ^= (uint)x * 0x85EBCA77u;
                h = (h << 13) | (h >> 19);
                h ^= (uint)y * 0xC2B2AE3Du;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
