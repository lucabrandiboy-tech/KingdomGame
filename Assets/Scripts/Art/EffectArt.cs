using UnityEngine;

namespace KingdomGame.Art
{
    /// <summary>
    /// Pictures for effects: little stone and wood cubes that fly off broken
    /// walls (drawn as real 3D cubes from above, in grey, so the game can tint
    /// them any color), their shadows, sparks, smoke and flashes.
    /// </summary>
    public static class EffectArt
    {
        public const int CubeFrames = 8;
        /// <summary>Width of the cube pictures, and the cube's edge length in them, in pixels.</summary>
        public const int CubeImageSize = 16, CubeEdge = 8;

        static readonly Vector3 Light = new Vector3(-0.55f, 0.6f, 0.58f);

        /// <summary>
        /// A cube tumbling around one of its own edges-axes, as <see cref="CubeFrames"/>
        /// frames that loop. Grey shades so the game can tint it with the wall's color.
        /// </summary>
        public static PixelImage[] CubeTumble()
        {
            var frames = new PixelImage[CubeFrames];
            for (int f = 0; f < CubeFrames; f++)
                frames[f] = Cube(f / (float)CubeFrames * 90f);
            return frames;
        }

        static PixelImage Cube(float spinDegrees)
        {
            var img = new PixelImage(CubeImageSize, CubeImageSize);
            float half = CubeEdge * 0.5f;
            // Corners of the cube, then rotate: spin around its own X axis, then a fixed tilt.
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) != 0 ? half : -half, (i & 2) != 0 ? half : -half, (i & 4) != 0 ? half : -half);
            float spin = spinDegrees * Mathf.PI / 180f;
            for (int i = 0; i < 8; i++)
            {
                var p = RotateX(corners[i], spin);
                p = RotateZ(RotateY(p, 0.52f), 0.35f);
                corners[i] = p;
            }

            // Faces as corner indices, going around each face.
            int[][] faces =
            {
                new[] { 0, 2, 6, 4 }, new[] { 1, 5, 7, 3 },   // -X, +X
                new[] { 0, 4, 5, 1 }, new[] { 2, 3, 7, 6 },   // -Y, +Y
                new[] { 0, 1, 3, 2 }, new[] { 4, 6, 7, 5 },   // -Z, +Z
            };
            var light = Light.normalized;
            float c = CubeImageSize / 2f;
            const int samples = 4;
            for (int y = 0; y < CubeImageSize; y++)
                for (int x = 0; x < CubeImageSize; x++)
                {
                    float r = 0, hits = 0;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float px = x + (sx + 0.5f) / samples - c, py = y + (sy + 0.5f) / samples - c;
                            // The visible face under this point is the one closest to the camera (highest z).
                            float bestZ = float.MinValue, value = 0;
                            foreach (var face in faces)
                            {
                                // The cube sits at the origin, so a face's middle points straight out of it.
                                var middle = (corners[face[0]] + corners[face[1]] + corners[face[2]] + corners[face[3]]) * 0.25f;
                                var normal = middle.normalized;
                                if (normal.z <= 0.001f) continue;                 // facing away from the camera
                                if (!Inside(corners, face, px, py)) continue;
                                float z = middle.z;
                                if (z < bestZ) continue;
                                bestZ = z;
                                value = 0.38f + 0.72f * Mathf.Max(0f, Vector3.Dot(normal, light));
                            }
                            if (bestZ == float.MinValue) continue;
                            r += value;
                            hits++;
                        }
                    if (hits == 0) continue;
                    byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(r / hits * 235f), 0, 255);
                    img.Set(x, y, new Color32(v, v, v, (byte)Mathf.RoundToInt(hits / (samples * samples) * 255f)));
                }
            return img;
        }

        static bool Inside(Vector3[] corners, int[] face, float x, float y)
        {
            float sign = 0;
            for (int i = 0; i < 4; i++)
            {
                var a = corners[face[i]];
                var b = corners[face[(i + 1) % 4]];
                float cross = (b.x - a.x) * (y - a.y) - (b.y - a.y) * (x - a.x);
                if (Mathf.Abs(cross) < 1e-6f) continue;
                if (sign == 0) sign = Mathf.Sign(cross);
                else if (Mathf.Sign(cross) != sign) return false;
            }
            return true;
        }

        static Vector3 RotateX(Vector3 p, float a) => new Vector3(p.x, p.y * Mathf.Cos(a) - p.z * Mathf.Sin(a), p.y * Mathf.Sin(a) + p.z * Mathf.Cos(a));
        static Vector3 RotateY(Vector3 p, float a) => new Vector3(p.x * Mathf.Cos(a) + p.z * Mathf.Sin(a), p.y, -p.x * Mathf.Sin(a) + p.z * Mathf.Cos(a));
        static Vector3 RotateZ(Vector3 p, float a) => new Vector3(p.x * Mathf.Cos(a) - p.y * Mathf.Sin(a), p.x * Mathf.Sin(a) + p.y * Mathf.Cos(a), p.z);

        /// <summary>A soft round shadow (black; the game sets how dark).</summary>
        public static PixelImage SoftShadow(int size = 16)
        {
            var img = new PixelImage(size, size);
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - c) / c, dy = (y + 0.5f - c) / c;
                    float a = PixelImage.Smooth(1f, 0.25f, Mathf.Sqrt(dx * dx + dy * dy));
                    img.Set(x, y, new Color32(0, 0, 0, (byte)Mathf.RoundToInt(a * 255f)));
                }
            return img;
        }

        /// <summary>A glowing dot: white middle, soft edge (tinted in the game).</summary>
        public static PixelImage Glow(int size = 12)
        {
            var img = new PixelImage(size, size);
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - c) / c, dy = (y + 0.5f - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a *= a;
                    img.Set(x, y, new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f)));
                }
            return img;
        }

        /// <summary>A puff of smoke (white, tinted grey in the game).</summary>
        public static PixelImage Smoke(int variant)
        {
            const int size = 48;
            var img = new PixelImage(size, size);
            uint seed = 600u + (uint)variant * 17u;
            float c = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - c) / c, dy = (y + 0.5f - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float billow = Noise.Fractal(x / 7f, y / 7f, seed, 3);
                    float a = Mathf.Clamp01((1f - d) * 1.6f) * (0.35f + billow * 0.9f);
                    float lit = 0.8f + (dx * -0.55f + dy * 0.6f) * 0.15f + (billow - 0.5f) * 0.3f;
                    byte v = (byte)Mathf.Clamp(Mathf.RoundToInt(lit * 230f), 0, 255);
                    img.Set(x, y, new Color32(v, v, v, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255)));
                }
            return img;
        }
    }
}
