// Renders the code-drawn art (Assets/Scripts/Art) without Unity, into the folder
// given as the first argument:
//   scene.png  a 1280 x 720 mock screenshot at the game's size (64 pixels per tile),
//              with shadows and a rough version of the game's color grading
//   parts.png  every sprite on its own, enlarged
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using KingdomGame.Art;
using UnityEngine;

static class Program
{
    const int Ppu = 64;

    static void Main(string[] args)
    {
        string dir = args[0];
        Scene(Path.Combine(dir, "scene.png"));
        Parts(Path.Combine(dir, "parts.png"));
        Console.WriteLine("ok");
    }

    static readonly Color32[] Stone = { PixelImage.Rgb(112, 110, 104), PixelImage.Rgb(100, 98, 94), PixelImage.Rgb(122, 118, 110), PixelImage.Rgb(92, 92, 94) };
    static readonly Color32[] Wood = { PixelImage.Rgb(150, 112, 72), PixelImage.Rgb(138, 102, 66), PixelImage.Rgb(162, 122, 80), PixelImage.Rgb(128, 94, 60) };

    static void Scene(string path)
    {
        var canvas = new Canvas(1280, 720);
        var ground = GroundArt.Ground(1024, 12345u);
        for (int y = 0; y < canvas.H; y++)
            for (int x = 0; x < canvas.W; x++)
                canvas.Px[y * canvas.W + x] = ground.Pixels[(y % 1024) * 1024 + x % 1024];

        // World positions in tiles; (0, 0) is the bottom-left of the picture.
        var wallTiles = new List<(float x, float y, Color32[] px)>();
        for (int i = 0; i < 7; i++) wallTiles.Add((2 + i, 8, WallArt.Stone(64, PixelImage.Rgb(46, 44, 42), Stone, i % 4)));
        for (int i = 0; i < 4; i++) wallTiles.Add((9, 4 + i, WallArt.Logs(64, PixelImage.Rgb(30, 22, 16), Wood, i % 4)));
        foreach (var w in wallTiles) canvas.Draw(Tile(w.px), w.x + 0.5f + 0.12f, w.y + 0.5f - 0.12f, tint: (0, 0, 0, 0.4f));
        foreach (var w in wallTiles) canvas.Draw(Tile(w.px), w.x + 0.5f, w.y + 0.5f);

        void Shadowed(PixelImage img, float x, float y, float height, float rotation = 0, float alpha = 1)
        {
            canvas.Draw(img.Shadow(Math.Max(2, (int)(height * 18)), 0.55f), x + height * 0.6f, y - height * 0.7f, rotation: rotation);
            canvas.Draw(img, x, y, rotation: rotation, alpha: alpha);
        }

        Shadowed(BuildingArt.Throne(), 4f, 4f, 0.12f);
        Shadowed(BuildingArt.Brazier(), 2.8f, 3f, 0.1f);
        Shadowed(BuildingArt.Brazier(), 5.2f, 3f, 0.1f);
        Shadowed(BuildingArt.Storehouse(), 15f, 7.5f, 0.3f);
        Shadowed(NatureArt.Rock(0), 12f, 3f, 0.15f, 20);
        Shadowed(NatureArt.Rock(1), 17.6f, 2.2f, 0.15f, -70);
        Shadowed(NatureArt.Bush(0, true), 11.2f, 9.6f, 0.1f);
        Shadowed(NatureArt.Bush(1, false), 7f, 1.2f, 0.1f, 90);
        Shadowed(NatureArt.Stump(1), 13.5f, 5.5f, 0.05f);

        Shadowed(CharacterArt.RulerFrames()[1], 7.2f, 5.6f, 0.12f, rotation: 25);
        float aim = 25f * MathF.PI / 180f, hx = CharacterArt.RulerHand.x / Ppu, hy = CharacterArt.RulerHand.y / Ppu;
        canvas.Draw(CharacterArt.Rifle(), 7.2f + hx * MathF.Cos(aim) - hy * MathF.Sin(aim), 5.6f + hx * MathF.Sin(aim) + hy * MathF.Cos(aim), rotation: 25);
        Shadowed(CharacterArt.VillagerFrames(true)[0], 13f, 8.2f, 0.1f, rotation: 160);
        Shadowed(CharacterArt.VillagerFrames(false)[1], 6f, 7.3f, 0.1f, rotation: -60);

        var cubes = EffectArt.CubeTumble();
        var dice = new Dice(9);
        for (int i = 0; i < 28; i++)
        {
            float x = 10.3f + dice.Range(-1.4f, 1.4f), y = 8.4f + dice.Range(-0.8f, 0.9f);
            float h = dice.Range(0f, 0.6f), s = dice.Range(0.6f, 1.1f);
            var c = Stone[i % 4];
            canvas.Draw(EffectArt.SoftShadow(), x + h * 0.6f, y - h * 0.7f, scale: s * 0.8f, tint: (0, 0, 0, 0.45f));
            canvas.Draw(cubes[i % 8], x, y, scale: s * (1 + h * 0.3f), tint: (c.r / 235f * 1.1f, c.g / 235f * 1.1f, c.b / 235f * 1.1f, 1));
        }

        Shadowed(NatureArt.Canopy(0), 1.6f, 1.2f, 0.45f, 30);
        canvas.Draw(NatureArt.Trunk(2), 17.5f, 9.8f);
        Shadowed(NatureArt.Canopy(2), 17.5f, 9.8f, 0.45f, 200, alpha: 0.45f);
        Shadowed(NatureArt.Canopy(1), 19.5f, 5.5f, 0.45f, 110);

        canvas.Grade();
        canvas.Save(path, 1);
    }

    static PixelImage Tile(Color32[] px)
    {
        var img = new PixelImage(64, 64);
        Array.Copy(px, img.Pixels, px.Length);
        return img;
    }

    static void Parts(string path)
    {
        var items = new List<PixelImage>();
        items.AddRange(CharacterArt.RulerFrames());
        items.AddRange(CharacterArt.VillagerFrames(true));
        items.Add(CharacterArt.Rifle());
        items.Add(CharacterArt.C4(true));
        items.Add(CharacterArt.Projectile(8, 2, PixelImage.Rgb(255, 214, 102)));
        items.AddRange(EffectArt.CubeTumble());
        items.Add(EffectArt.Smoke(0));
        items.Add(EffectArt.Glow());
        items.Add(NatureArt.Canopy(0));
        items.Add(NatureArt.Trunk(0));
        items.Add(NatureArt.Stump(0));
        items.Add(NatureArt.Rock(2));
        items.Add(NatureArt.Bush(2, true));
        items.Add(Tile(WallArt.Stone(64, PixelImage.Rgb(46, 44, 42), Stone, 0)));
        items.Add(Tile(WallArt.Logs(64, PixelImage.Rgb(30, 22, 16), Wood, 0)));

        int w = 0, h = 0, rowW = 0, rowH = 0;
        foreach (var i in items)
        {
            if (rowW + i.Width + 4 > 900) { w = Math.Max(w, rowW); h += rowH + 4; rowW = 0; rowH = 0; }
            rowW += i.Width + 4; rowH = Math.Max(rowH, i.Height);
        }
        w = Math.Max(w, rowW); h += rowH;
        var canvas = new Canvas(w, h);
        for (int p = 0; p < canvas.Px.Length; p++) canvas.Px[p] = PixelImage.Rgb(60, 66, 52);
        int ox = 0, oy = h, rh = 0;
        foreach (var i in items)
        {
            if (ox + i.Width + 4 > 900) { ox = 0; oy -= rh + 4; rh = 0; }
            if (rh == 0) rh = 0;
            canvas.DrawPixels(i, ox, oy - i.Height);
            ox += i.Width + 4; rh = Math.Max(rh, i.Height);
        }
        canvas.Save(path, 2);
    }

    sealed class Canvas
    {
        public readonly int W, H;
        public readonly Color32[] Px; // y up, like Unity

        public Canvas(int w, int h) { W = w; H = h; Px = new Color32[w * h]; }

        public void DrawPixels(PixelImage img, int x0, int y0)
        {
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                    Blend(x0 + x, y0 + y, img.Pixels[y * img.Width + x], 1, 1, 1, 1);
        }

        /// <summary>Draws a sprite with its pivot at (x, y) tiles, rotated (degrees) and scaled, with bilinear sampling.</summary>
        public void Draw(PixelImage img, float x, float y, float rotation = 0, float scale = 1, float alpha = 1, (float r, float g, float b, float a)? tint = null)
        {
            var t = tint ?? (1f, 1f, 1f, 1f);
            float px = x * Ppu, py = y * Ppu;
            float a = rotation * MathF.PI / 180f, cos = MathF.Cos(a), sin = MathF.Sin(a);
            float pivotX = img.Pivot.x * img.Width, pivotY = img.Pivot.y * img.Height;
            float reach = MathF.Sqrt(img.Width * img.Width + img.Height * img.Height) * scale;
            for (int dy = (int)(py - reach); dy <= py + reach; dy++)
                for (int dx = (int)(px - reach); dx <= px + reach; dx++)
                {
                    float rx = (dx + 0.5f - px) / scale, ry = (dy + 0.5f - py) / scale;
                    float sx = rx * cos + ry * sin + pivotX, sy = -rx * sin + ry * cos + pivotY;
                    var c = Sample(img, sx - 0.5f, sy - 0.5f);
                    if (c.a == 0) continue;
                    Blend(dx, dy, c, t.Item1, t.Item2, t.Item3, t.Item4 * alpha);
                }
        }

        static Color32 Sample(PixelImage img, float x, float y)
        {
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float fx = x - x0, fy = y - y0;
            float r = 0, g = 0, b = 0, a = 0;
            for (int k = 0; k < 4; k++)
            {
                int sx = x0 + (k & 1), sy = y0 + (k >> 1);
                float w = ((k & 1) != 0 ? fx : 1 - fx) * ((k >> 1) != 0 ? fy : 1 - fy);
                var c = img.Get(sx, sy);
                float ca = c.a / 255f * w;
                r += c.r * ca; g += c.g * ca; b += c.b * ca; a += ca;
            }
            if (a <= 0.001f) return default;
            return new Color32((byte)Math.Clamp(r / a, 0, 255), (byte)Math.Clamp(g / a, 0, 255), (byte)Math.Clamp(b / a, 0, 255), (byte)Math.Clamp(a * 255, 0, 255));
        }

        void Blend(int x, int y, Color32 c, float tr, float tg, float tb, float ta)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            float a = c.a / 255f * ta;
            int i = y * W + x;
            var u = Px[i];
            Px[i] = new Color32(
                (byte)Math.Clamp(u.r + (c.r * tr - u.r) * a, 0, 255),
                (byte)Math.Clamp(u.g + (c.g * tg - u.g) * a, 0, 255),
                (byte)Math.Clamp(u.b + (c.b * tb - u.b) * a, 0, 255), 255);
        }

        /// <summary>Roughly what the game's post-processing does: more contrast, less color, dark corners.</summary>
        public void Grade()
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    var c = Px[i];
                    float r = c.r / 255f, g = c.g / 255f, b = c.b / 255f;
                    float l = r * 0.3f + g * 0.59f + b * 0.11f;
                    r = l + (r - l) * 0.85f; g = l + (g - l) * 0.85f; b = l + (b - l) * 0.85f;
                    r = (r - 0.4f) * 1.15f + 0.4f; g = (g - 0.4f) * 1.15f + 0.4f; b = (b - 0.4f) * 1.15f + 0.4f;
                    float dx = (x - W / 2f) / (W / 2f), dy = (y - H / 2f) / (H / 2f);
                    float v = 1f - Math.Clamp((dx * dx * 0.6f + dy * dy * 0.6f) - 0.15f, 0, 1) * 0.55f;
                    Px[i] = new Color32((byte)Math.Clamp(r * v * 255, 0, 255), (byte)Math.Clamp(g * v * 255, 0, 255), (byte)Math.Clamp(b * v * 255, 0, 255), 255);
                }
        }

        public void Save(string path, int scale)
        {
            var rgba = new byte[W * H * 4];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = Px[y * W + x];
                    int i = ((H - 1 - y) * W + x) * 4;
                    rgba[i] = c.r; rgba[i + 1] = c.g; rgba[i + 2] = c.b; rgba[i + 3] = 255;
                }
            WritePng(path, W, H, rgba, scale);
        }
    }

    static void WritePng(string path, int w, int h, byte[] rgba, int scale)
    {
        int W = w * scale, H = h * scale;
        var raw = new MemoryStream();
        for (int y = 0; y < H; y++)
        {
            raw.WriteByte(0);
            for (int x = 0; x < W; x++) { int i = ((y / scale) * w + x / scale) * 4; raw.Write(rgba, i, 4); }
        }
        var comp = new MemoryStream();
        using (var z = new ZLibStream(comp, CompressionLevel.Fastest, true)) { raw.Position = 0; raw.CopyTo(z); }
        using var f = File.Create(path);
        f.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        Chunk(f, "IHDR", BE(W), BE(H), new byte[] { 8, 6, 0, 0, 0 });
        Chunk(f, "IDAT", comp.ToArray());
        Chunk(f, "IEND");
    }
    static byte[] BE(int v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
    static void Chunk(Stream s, string type, params byte[][] parts)
    {
        var data = new MemoryStream(); foreach (var p in parts) data.Write(p);
        var d = data.ToArray(); var t = System.Text.Encoding.ASCII.GetBytes(type);
        s.Write(BE(d.Length)); s.Write(t); s.Write(d);
        s.Write(BE((int)Crc(t, d)));
    }
    static uint Crc(byte[] t, byte[] d)
    {
        uint c = 0xFFFFFFFF;
        foreach (var b in t) c = Step(c, b); foreach (var b in d) c = Step(c, b);
        return c ^ 0xFFFFFFFF;
        static uint Step(uint c, byte b) { c ^= b; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1; return c; }
    }
}
