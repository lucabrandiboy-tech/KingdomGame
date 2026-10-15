using System;
using System.IO;
using KingdomGame.Audio;

// Writes each sound (every version) and each background loop as a 16-bit mono .wav file.
static class Program
{
    static int Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : "sounds";
        Directory.CreateDirectory(dir);
        foreach (Sound s in Enum.GetValues(typeof(Sound)))
            for (int v = 0; v < SoundSynth.Variants(s); v++)
                Write(Path.Combine(dir, $"{s}{(SoundSynth.Variants(s) > 1 ? "_" + v : "")}.wav"), SoundSynth.Make(s, v));
        foreach (Loop l in Enum.GetValues(typeof(Loop)))
        {
            // Twice round, to hear that it loops without a seam.
            var once = SoundSynth.MakeLoop(l);
            var twice = new float[once.Length * 2];
            once.CopyTo(twice, 0);
            once.CopyTo(twice, once.Length);
            Write(Path.Combine(dir, $"Loop_{l}.wav"), twice);
        }
        Console.WriteLine($"wrote the sounds to {Path.GetFullPath(dir)}");
        return 0;
    }

    static void Write(string path, float[] samples)
    {
        using var w = new BinaryWriter(File.Create(path));
        int bytes = samples.Length * 2;
        w.Write("RIFF".ToCharArray()); w.Write(36 + bytes); w.Write("WAVE".ToCharArray());
        w.Write("fmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(SoundSynth.Rate); w.Write(SoundSynth.Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data".ToCharArray()); w.Write(bytes);
        foreach (float a in samples) w.Write((short)Math.Round(Math.Clamp(a, -1f, 1f) * 32767f));
    }
}
