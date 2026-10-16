using System;
using KingdomGame.Audio;

// Tests for the sounds made in code (Assets/Scripts/Audio/SoundSynth.cs).
static partial class Tests
{
    static void EverySoundIsMadeCleanly()
    {
        foreach (Sound s in Enum.GetValues(typeof(Sound)))
            for (int v = 0; v < SoundSynth.Variants(s); v++)
            {
                var x = SoundSynth.Make(s, v);
                string name = $"{s} #{v}";
                float seconds = x.Length / (float)SoundSynth.Rate;
                // (thunder rolls on for longer: only two at once)
                float longest = s == Sound.Thunder ? 6f : 3f;
                Check(seconds >= 0.1f && seconds <= longest, $"{name} is {seconds:0.00} s long");
                float peak = 0f;
                double energy = 0;
                foreach (float a in x)
                {
                    Check(!float.IsNaN(a) && !float.IsInfinity(a), $"{name} has a broken sample");
                    peak = Math.Max(peak, Math.Abs(a));
                    energy += a * a;
                }
                float rms = (float)Math.Sqrt(energy / x.Length);
                Check(peak <= 0.9001f && peak >= 0.5f, $"{name} peaks at {peak:0.000}");
                Check(rms > 0.02f, $"{name} is nearly silent (rms {rms:0.000})");
                // No click at either end.
                Check(Math.Abs(x[0]) < 0.05f, $"{name} starts with a jump ({x[0]:0.000})");
                for (int i = 1; i <= 10; i++) Check(Math.Abs(x[x.Length - i]) < 0.02f, $"{name} stops with a jump");
                // The same every time (so the game sounds the same on every computer).
                var again = SoundSynth.Make(s, v);
                for (int i = 0; i < x.Length; i += 97) Check(again[i] == x[i], $"{name} came out differently the second time");
            }
    }

    static void GroansAreAllDifferent()
    {
        int n = SoundSynth.Variants(Sound.Groan);
        Check(n >= 2, "only one groan");
        for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++)
            {
                var x = SoundSynth.Make(Sound.Groan, a);
                var y = SoundSynth.Make(Sound.Groan, b);
                Check(x.Length != y.Length || x[x.Length / 2] != y[y.Length / 2], $"groans {a} and {b} are the same");
            }
    }

    static void LoopsJoinUpSeamlessly()
    {
        foreach (Loop l in Enum.GetValues(typeof(Loop)))
        {
            var x = SoundSynth.MakeLoop(l);
            Check(x.Length == (int)(SoundSynth.LoopSeconds * SoundSynth.Rate), $"{l} is {x.Length} samples long");
            float peak = 0f;
            double step = 0;
            for (int i = 0; i < x.Length; i++)
            {
                Check(!float.IsNaN(x[i]) && !float.IsInfinity(x[i]), $"{l} has a broken sample");
                peak = Math.Max(peak, Math.Abs(x[i]));
                if (i > 0) step += Math.Abs(x[i] - x[i - 1]);
            }
            Check(peak <= 0.7001f && peak > 0.3f, $"{l} peaks at {peak:0.000}");
            // Going round from the end to the start is no bigger a jump than the sound makes anyway.
            float usual = (float)(step / (x.Length - 1));
            float seam = Math.Abs(x[0] - x[x.Length - 1]);
            Check(seam <= usual * 6f + 0.01f, $"{l} jumps {seam:0.000} where it loops (usually {usual:0.000})");
        }
    }
}
