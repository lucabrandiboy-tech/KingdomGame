// Writes ruler.png, nature.png, walls.png and ground.png into the folder given as the first argument.
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using KingdomGame.Art;
using UnityEngine;

static class Program
{
    static void Main(string[] args)
    {
        string dir = args[0];
        var frames = CharacterArt.RulerFrames();
        Sheet(Path.Combine(dir, "ruler.png"), new List<PixelImage>(frames) { CharacterArt.Rifle(), CharacterArt.C4(true), CharacterArt.C4(false), CharacterArt.Projectile(6, 2, PixelImage.Rgb(255,214,102)), CharacterArt.Projectile(12, 4, PixelImage.Rgb(232,96,52)) }, 8);
        Sheet(Path.Combine(dir, "nature.png"), new List<PixelImage> { NatureArt.Tree(0), NatureArt.Tree(1), NatureArt.Tree(2), NatureArt.Stump(0), NatureArt.Rock(0), NatureArt.Rock(1), NatureArt.Bush(0, true), NatureArt.Bush(1, false) }, 4);
        var stone = new[] { PixelImage.Rgb(128,126,122), PixelImage.Rgb(114,112,110), PixelImage.Rgb(140,136,128), PixelImage.Rgb(104,104,108) };
        var wood = new[] { PixelImage.Rgb(142,96,58), PixelImage.Rgb(126,84,50), PixelImage.Rgb(156,108,66), PixelImage.Rgb(118,80,48) };
        var walls = new List<PixelImage>();
        for (int v = 0; v < 2; v++) walls.Add(FromPixels(64, WallArt.Bricks(64, PixelImage.Rgb(54,52,58), stone, v)));
        for (int v = 0; v < 2; v++) walls.Add(FromPixels(64, WallArt.Planks(64, PixelImage.Rgb(48,32,22), wood, v)));
        Sheet(Path.Combine(dir, "walls.png"), walls, 4, gap: 0);
        var ground = GroundArt.Grass(8, 12345u);
        Sheet(Path.Combine(dir, "ground.png"), new List<PixelImage> { ground, ground }, 1, gap: 0);
        Console.WriteLine("ok");
    }

    static PixelImage FromPixels(int size, Color32[] px)
    {
        var img = new PixelImage(size, size);
        Array.Copy(px, img.Pixels, px.Length);
        return img;
    }

    static void Sheet(string path, List<PixelImage> images, int scale, int gap = 4)
    {
        int w = 0, h = 0;
        foreach (var i in images) { w += i.Width + gap; h = Math.Max(h, i.Height); }
        var rgba = new byte[w * h * 4];
        for (int p = 0; p < w * h; p++) { rgba[p*4]=74; rgba[p*4+1]=100; rgba[p*4+2]=58; rgba[p*4+3]=255; }
        int ox = 0;
        foreach (var img in images)
        {
            for (int y = 0; y < img.Height; y++)
                for (int x = 0; x < img.Width; x++)
                {
                    var c = img.Pixels[y * img.Width + x];
                    int ty = h - 1 - y; int i = (ty * w + ox + x) * 4;
                    float a = c.a / 255f;
                    rgba[i] = (byte)(rgba[i] * (1 - a) + c.r * a);
                    rgba[i+1] = (byte)(rgba[i+1] * (1 - a) + c.g * a);
                    rgba[i+2] = (byte)(rgba[i+2] * (1 - a) + c.b * a);
                }
            ox += img.Width + gap;
        }
        WritePng(path, w, h, rgba, scale);
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
