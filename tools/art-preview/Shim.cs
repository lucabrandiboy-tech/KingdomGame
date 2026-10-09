// Minimal UnityEngine stand-ins so the art code runs outside Unity (preview only).
namespace UnityEngine
{
    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }
    public static class Mathf
    {
        public const float PI = (float)System.Math.PI;
        public static int FloorToInt(float v) => (int)System.Math.Floor(v);
        public static int RoundToInt(float v) => (int)System.Math.Round(v, System.MidpointRounding.ToEven);
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp(t, 0, 1);
        public static float Sin(float v) => (float)System.Math.Sin(v);
        public static float Cos(float v) => (float)System.Math.Cos(v);
        public static float Atan2(float y, float x) => (float)System.Math.Atan2(y, x);
        public static float Sqrt(float v) => (float)System.Math.Sqrt(v);
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
    }
}
