using System;

namespace KingdomGame.Audio
{
    /// <summary>Every sound the game plays once (see <c>Sounds.Play</c>).</summary>
    public enum Sound
    {
        Gun, HeavyGun, Bow, Throw, Cannon, Rocket, Flame, Swing,
        ExplosionBig, ExplosionSmall,
        Groan, ZombieHit, ZombieDie, Claw, Spit, Splash, Scream,
        Hammer, Chop, Pick, Rustle, Door, Hurt, Horn, Loot, Chime,
    }

    /// <summary>The background sounds, played on a loop (see <c>Ambience</c>).</summary>
    public enum Loop { Wind, Crickets, Birds }

    /// <summary>
    /// Makes the game's sounds from scratch (noise, tones, filters and envelopes), the way the art is drawn in
    /// code: there are no sound files. Plain C# with no Unity code, so the tests can check every sound
    /// (tools/tests) and tools/sound-preview can write them out as .wav files to listen to.
    /// </summary>
    public static class SoundSynth
    {
        /// <summary>Samples per second (mono).</summary>
        public const int Rate = 22050;
        /// <summary>How long each background loop is; it joins up with itself seamlessly.</summary>
        public const float LoopSeconds = 8f;
        public static readonly int SoundCount = Enum.GetValues(typeof(Sound)).Length;
        public static readonly int LoopCount = Enum.GetValues(typeof(Loop)).Length;

        /// <summary>How many versions of a sound there are (so a crowd of zombies doesn't all groan the same).</summary>
        public static int Variants(Sound s)
        {
            switch (s)
            {
                case Sound.Groan: return 4;
                case Sound.Gun: case Sound.ZombieHit: case Sound.Hammer: case Sound.Chop: case Sound.Pick: return 2;
                default: return 1;
            }
        }

        /// <summary>The samples (-1..1) of version <paramref name="variant"/> of a sound.</summary>
        public static float[] Make(Sound s, int variant)
        {
            variant = Math.Max(0, Math.Min(Variants(s) - 1, variant));
            var r = new Rng((uint)(1000 + (int)s * 31 + variant * 7));
            float[] x;
            switch (s)
            {
                case Sound.Gun: x = Gun(r, variant, false); break;
                case Sound.HeavyGun: x = Gun(r, variant, true); break;
                case Sound.Bow: x = Bow(r); break;
                case Sound.Throw: x = Throw(r); break;
                case Sound.Cannon: x = Cannon(r); break;
                case Sound.Rocket: x = Rocket(r); break;
                case Sound.Flame: x = Flame(r); break;
                case Sound.Swing: x = Swing(r); break;
                case Sound.ExplosionBig: x = Explosion(r, true); break;
                case Sound.ExplosionSmall: x = Explosion(r, false); break;
                case Sound.Groan: x = Groan(r, variant); break;
                case Sound.ZombieHit: x = ZombieHit(r, variant); break;
                case Sound.ZombieDie: x = ZombieDie(r); break;
                case Sound.Claw: x = Claw(r); break;
                case Sound.Spit: x = Spit(r); break;
                case Sound.Splash: x = Splash(r); break;
                case Sound.Scream: x = Scream(r); break;
                case Sound.Hammer: x = Hammer(r, variant); break;
                case Sound.Chop: x = Chop(r, variant); break;
                case Sound.Pick: x = Pick(r, variant); break;
                case Sound.Rustle: x = Rustle(r); break;
                case Sound.Door: x = Door(r); break;
                case Sound.Hurt: x = Hurt(r); break;
                case Sound.Horn: x = Horn(r); break;
                case Sound.Loot: x = Loot(); break;
                default: x = Chime(); break;
            }
            Finish(x, 0.9f, true);
            return x;
        }

        /// <summary>The samples of a background loop: <see cref="LoopSeconds"/> long, its end running on into its start.</summary>
        public static float[] MakeLoop(Loop l)
        {
            var r = new Rng((uint)(5000 + (int)l * 13));
            float seconds = LoopSeconds + 1f;   // one second more, to fade the end into the start
            float[] x;
            switch (l)
            {
                case Loop.Wind: x = Wind(r, seconds); break;
                case Loop.Crickets: x = Crickets(r, seconds); break;
                default: x = Birds(r, seconds); break;
            }
            var looped = Seamless(x, (int)(LoopSeconds * Rate), Rate);
            Finish(looped, 0.7f, false);
            return looped;
        }

        // ------------------------------------------------------------ weapons --

        static float[] Gun(Rng r, int v, bool heavy)
        {
            float len = heavy ? 0.9f : 0.45f;
            var crack = Noise(r, len);
            HighPass(crack, heavy ? 600f : 1000f);
            Shape(crack, t => Env(t, 0.0005f, heavy ? 0.03f : 0.018f));
            var body = Noise(r, len);
            LowPass(body, (heavy ? 700f : 1400f) * (v == 1 ? 1.25f : 1f));
            Shape(body, t => Env(t, 0.001f, heavy ? 0.12f : 0.06f));
            var thump = Tone(len, t => (heavy ? 55f : 85f) * (1f - 0.3f * t), Sine);
            Shape(thump, t => Env(t, 0.002f, heavy ? 0.12f : 0.05f));
            var tail = Noise(r, len);
            LowPass(tail, 400f);
            Shape(tail, t => Env(t, 0.02f, heavy ? 0.3f : 0.14f));
            var x = Buffer(len);
            Add(x, crack, 0.8f);
            Add(x, body, 1f);
            Add(x, thump, heavy ? 0.9f : 0.6f);
            Add(x, tail, heavy ? 0.45f : 0.35f);
            return x;
        }

        // A bowstring (a plucked string) and the arrow's whoosh.
        static float[] Bow(Rng r)
        {
            const float len = 0.45f;
            var str = Pluck(r, len, 120f, 0.994f);
            Shape(str, t => Env(t, 0f, 0.09f));
            var whoosh = Noise(r, len);
            BandPass(whoosh, t => Math.Max(500f, 2200f - 3000f * t), 1.5f);
            Shape(whoosh, t => Env(t, 0.03f, 0.08f));
            var x = Buffer(len);
            Add(x, str, 0.8f);
            Add(x, whoosh, 0.5f);
            return x;
        }

        // A sling, a catapult arm, a spear: a knock and a whoosh.
        static float[] Throw(Rng r)
        {
            const float len = 0.5f;
            var whoosh = Noise(r, len);
            BandPass(whoosh, t => 900f - 1100f * Math.Min(t, 0.5f), 1.2f);
            Shape(whoosh, t => Env(t, 0.08f, 0.12f));
            var knock = Tone(len, t => 70f, Sine);
            Shape(knock, t => Env(t, 0.002f, 0.05f));
            var x = Buffer(len);
            Add(x, whoosh, 0.9f);
            Add(x, knock, 0.6f);
            return x;
        }

        static float[] Cannon(Rng r)
        {
            const float len = 1.4f;
            var thump = Tone(len, t => 52f - 18f * Math.Min(1f, t * 2f), Sine);
            Shape(thump, t => Env(t, 0.003f, 0.22f));
            var body = Noise(r, len);
            LowPass(body, t => 900f * (float)Math.Exp(-t * 3f) + 150f);
            LowPass(body, t => 1400f * (float)Math.Exp(-t * 3f) + 200f);
            Shape(body, t => Env(t, 0.002f, 0.25f));
            var crack = Noise(r, len);
            HighPass(crack, 800f);
            Shape(crack, t => Env(t, 0.0005f, 0.02f));
            var rumble = Noise(r, len);
            LowPass(rumble, 150f);
            LowPass(rumble, 150f);
            Shape(rumble, t => Env(t, 0.05f, 0.5f));
            var x = Buffer(len);
            Add(x, thump, 1f);
            Add(x, body, 1f);
            Add(x, crack, 0.6f);
            Add(x, rumble, 0.6f);
            return x;
        }

        static float[] Rocket(Rng r)
        {
            const float len = 1.1f;
            var roar = Noise(r, len);
            BandPass(roar, t => 400f + 2500f * t, 0.8f);
            Shape(roar, t => Hold(t, 0.05f, len, 0.5f));
            var low = Noise(r, len);
            LowPass(low, 300f);
            Shape(low, t => Hold(t, 0.03f, len, 0.6f));
            var pop = Noise(r, len);
            HighPass(pop, 1200f);
            Shape(pop, t => Env(t, 0.0005f, 0.015f));
            var x = Buffer(len);
            Add(x, roar, 1f);
            Add(x, low, 0.6f);
            Add(x, pop, 0.6f);
            return x;
        }

        // A gout of flame: a flickering roar with crackles.
        static float[] Flame(Rng r)
        {
            const float len = 0.6f;
            var roar = Noise(r, len);
            LowPass(roar, 1200f);
            LowPass(roar, 1600f);
            float phase = r.Next() * 6f;
            Shape(roar, t => Hold(t, 0.03f, len, 0.35f) * (0.6f + 0.4f * (float)Math.Sin(2 * Math.PI * 13f * t + phase)));
            var crackle = Buffer(len);
            for (int i = 0; i < crackle.Length; i++) if (r.Next() < 0.004f) crackle[i] = r.Signed();
            HighPass(crackle, 1500f);
            var x = Buffer(len);
            Add(x, roar, 1f);
            Add(x, crackle, 0.4f);
            return x;
        }

        // A club through the air.
        static float[] Swing(Rng r)
        {
            const float len = 0.3f;
            var x = Noise(r, len);
            BandPass(x, t => 1500f - 2500f * t, 1.2f);
            Shape(x, t => { float s = (float)Math.Sin(Math.PI * Math.Min(1f, t / len)); return s * s; });
            return x;
        }

        static float[] Explosion(Rng r, bool big)
        {
            float len = big ? 2.6f : 1.2f;
            var sub = Tone(len, t => big ? 48f - 22f * Math.Min(1f, t) : 70f - 25f * Math.Min(1f, t * 2f), Sine);
            Shape(sub, t => Env(t, 0.004f, big ? 0.45f : 0.18f));
            var body = Noise(r, len);
            LowPass(body, t => (big ? 1500f : 2200f) * (float)Math.Exp(-t * (big ? 4f : 6f)) + (big ? 120f : 200f));
            LowPass(body, t => (big ? 2200f : 3000f) * (float)Math.Exp(-t * (big ? 4f : 6f)) + (big ? 180f : 300f));
            Shape(body, t => Env(t, 0.002f, big ? 0.5f : 0.2f));
            var crack = Noise(r, len);
            HighPass(crack, 700f);
            Shape(crack, t => Env(t, 0.0005f, 0.03f));
            var rumble = Noise(r, len);
            LowPass(rumble, big ? 110f : 200f);
            LowPass(rumble, big ? 110f : 200f);
            Shape(rumble, t => Env(t, big ? 0.08f : 0.03f, big ? 0.9f : 0.35f));
            var x = Buffer(len);
            Add(x, sub, 1f);
            Add(x, body, big ? 1.2f : 1f);
            Add(x, crack, 0.5f);
            Add(x, rumble, big ? 0.8f : 0.5f);
            return x;
        }

        // ------------------------------------------------------------ zombies --

        // A voice: a buzzing tone (the throat) through the mouth's resonances (formants).
        static float[] Voice(Rng r, float len, Func<float, float> pitch, float f1, float f2, float f3, float breath)
        {
            var src = Tone(len, pitch, Saw);
            var air = Noise(r, len);
            Normalize(air);
            Add(src, air, breath);
            var a = (float[])src.Clone();
            var b = (float[])src.Clone();
            var c = (float[])src.Clone();
            BandPass(a, t => f1, 5f);
            BandPass(b, t => f2, 6f);
            BandPass(c, t => f3, 8f);
            var x = Buffer(len);
            Add(x, a, 1f);
            Add(x, b, 0.6f);
            Add(x, c, 0.25f);
            return x;
        }

        static float[] Groan(Rng r, int v)
        {
            float f0 = new[] { 92f, 105f, 80f, 118f }[v];
            float len = new[] { 1.3f, 1.1f, 1.6f, 1.0f }[v];
            float f1 = new[] { 520f, 600f, 450f, 650f }[v], f2 = new[] { 900f, 1000f, 820f, 1150f }[v];
            var x = Voice(r, len, t => f0 * (1f - 0.18f * t / len) * (1f + 0.035f * (float)Math.Sin(2 * Math.PI * 5.5 * t) + 0.02f * (float)Math.Sin(2 * Math.PI * 1.3 * t)),
                f1, f2, 2400f, 0.25f);
            Shape(x, t => Hold(t, 0.18f, len, 0.45f) * (0.85f + 0.15f * (float)Math.Sin(2 * Math.PI * 3.1 * t)));
            return x;
        }

        // A blow on a wall or a door.
        static float[] ZombieHit(Rng r, int v)
        {
            const float len = 0.35f;
            var thud = Noise(r, len);
            LowPass(thud, 500f);
            Shape(thud, t => Env(t, 0.001f, 0.04f));
            var tone = Tone(len, t => v == 1 ? 140f : 110f, Sine);
            Shape(tone, t => Env(t, 0.001f, 0.07f));
            var rattle = Noise(r, len);
            BandPass(rattle, t => 900f, 3f);
            Shape(rattle, t => Env(t, 0.002f, 0.06f));
            var x = Buffer(len);
            Add(x, thud, 1f);
            Add(x, tone, 0.7f);
            Add(x, rattle, 0.4f);
            return x;
        }

        static float[] ZombieDie(Rng r)
        {
            const float len = 0.9f;
            var x = Voice(r, len, t => 100f - 45f * Math.Min(1f, t / 0.7f), 450f, 800f, 2200f, 0.3f);
            Shape(x, t => Hold(t, 0.03f, 0.75f, 0.4f));
            var splat = Noise(r, len);
            LowPass(splat, 1200f);
            Shape(splat, t => Env(t, 0.002f, 0.07f));
            Normalize(x);
            Add(x, splat, 0.8f);
            return x;
        }

        // Claws on someone: a slash and a thud.
        static float[] Claw(Rng r)
        {
            const float len = 0.3f;
            var slash = Noise(r, len);
            HighPass(slash, 2500f);
            Shape(slash, t => Env(t, 0.002f, 0.035f));
            var thud = Noise(r, len);
            LowPass(thud, 300f);
            Shape(thud, t => Env(t, 0.001f, 0.05f));
            var x = Buffer(len);
            Add(x, slash, 0.8f);
            Add(x, thud, 0.8f);
            return x;
        }

        static float[] Spit(Rng r)
        {
            const float len = 0.35f;
            var hiss = Noise(r, len);
            BandPass(hiss, t => Math.Max(400f, 1800f - 3000f * t), 2f);
            Shape(hiss, t => Env(t, 0.005f, 0.06f));
            var pop = Tone(len, t => 300f - 150f * Math.Min(1f, t * 10f), Sine);
            Shape(pop, t => Env(t, 0.001f, 0.03f));
            var x = Buffer(len);
            Add(x, hiss, 1f);
            Add(x, pop, 0.5f);
            return x;
        }

        static float[] Splash(Rng r)
        {
            const float len = 0.5f;
            var splat = Noise(r, len);
            LowPass(splat, 2500f);
            Shape(splat, t => Env(t, 0.001f, 0.07f) * (0.7f + 0.3f * (float)Math.Sin(2 * Math.PI * 35 * t)));
            var drops = Noise(r, len);
            BandPass(drops, t => 1200f, 4f);
            Shape(drops, t => Env(t, 0.03f, 0.15f));
            var x = Buffer(len);
            Add(x, splat, 1f);
            Add(x, drops, 0.4f);
            return x;
        }

        static float[] Scream(Rng r)
        {
            const float len = 1.1f;
            var x = Voice(r, len, t => 640f * (1f + 0.25f * t) * (1f + 0.06f * (float)Math.Sin(2 * Math.PI * 11 * t)), 1000f, 2700f, 3600f, 0.5f);
            Shape(x, t => Hold(t, 0.04f, len, 0.35f));
            return x;
        }

        // ----------------------------------------------------------- the work --

        static float[] Hammer(Rng r, int v)
        {
            const float len = 0.3f;
            float k = v == 1 ? 1.15f : 1f;
            var click = Noise(r, len);
            HighPass(click, 3000f);
            Shape(click, t => Env(t, 0.0002f, 0.004f));
            var ring = Tone(len, t => 520f * k, Sine);
            Shape(ring, t => Env(t, 0.0005f, 0.05f));
            var high = Tone(len, t => 1340f * k, Sine);
            Shape(high, t => Env(t, 0.0005f, 0.03f));
            var thump = Tone(len, t => 160f, Sine);
            Shape(thump, t => Env(t, 0.0005f, 0.04f));
            var x = Buffer(len);
            Add(x, click, 0.6f);
            Add(x, ring, 0.8f);
            Add(x, high, 0.5f);
            Add(x, thump, 0.6f);
            return x;
        }

        // An axe in wood.
        static float[] Chop(Rng r, int v)
        {
            const float len = 0.35f;
            var thock = Noise(r, len);
            BandPass(thock, t => v == 1 ? 780f : 650f, 4f);
            Shape(thock, t => Env(t, 0.0005f, 0.05f));
            var tone = Tone(len, t => 210f, Sine);
            Shape(tone, t => Env(t, 0.0005f, 0.045f));
            var click = Noise(r, len);
            HighPass(click, 2500f);
            Shape(click, t => Env(t, 0.0002f, 0.005f));
            var x = Buffer(len);
            Add(x, thock, 1f);
            Add(x, tone, 0.6f);
            Add(x, click, 0.4f);
            return x;
        }

        // A pick on stone: a metal ring and a click.
        static float[] Pick(Rng r, int v)
        {
            const float len = 0.45f;
            float k = v == 1 ? 1.12f : 1f;
            var x = Buffer(len);
            foreach (var (f, decay, gain) in new[] { (2200f, 0.09f, 0.8f), (3150f, 0.06f, 0.5f), (4730f, 0.04f, 0.3f) })
            {
                var p = Tone(len, t => f * k, Sine);
                Shape(p, t => Env(t, 0.0002f, decay));
                Add(x, p, gain);
            }
            var click = Noise(r, len);
            HighPass(click, 3000f);
            Shape(click, t => Env(t, 0.0002f, 0.004f));
            var thud = Noise(r, len);
            LowPass(thud, 400f);
            Shape(thud, t => Env(t, 0.001f, 0.03f));
            Add(x, click, 0.5f);
            Add(x, thud, 0.4f);
            return x;
        }

        // Picking berries: leaves rustling.
        static float[] Rustle(Rng r)
        {
            const float len = 0.4f;
            var x = Noise(r, len);
            BandPass(x, t => 3500f, 1f);
            var flutter = Noise(r, len);
            LowPass(flutter, 40f);
            Normalize(flutter);
            Shape(x, t => Env(t, 0.05f, 0.12f));
            for (int i = 0; i < x.Length; i++) x[i] *= 0.5f + 0.5f * Math.Abs(flutter[i]);
            return x;
        }

        // A door or gate: a wooden thud and a short creak.
        static float[] Door(Rng r)
        {
            const float len = 0.45f;
            var thud = Tone(len, t => 85f, Sine);
            Shape(thud, t => Env(t, 0.001f, 0.09f));
            var knock = Noise(r, len);
            BandPass(knock, t => 300f, 2f);
            Shape(knock, t => Env(t, 0.001f, 0.08f));
            var creak = Tone(len, t => 260f - 60f * t / len, Saw);
            BandPass(creak, t => 900f, 6f);
            Shape(creak, t => Hold(t, 0.01f, 0.22f, 0.1f));
            var x = Buffer(len);
            Add(x, thud, 0.8f);
            Add(x, knock, 0.8f);
            Add(x, creak, 0.25f);
            return x;
        }

        // The ruler being hurt: a grunt.
        static float[] Hurt(Rng r)
        {
            const float len = 0.35f;
            var x = Voice(r, len, t => 150f - 45f * t / len, 650f, 1100f, 2500f, 0.2f);
            Shape(x, t => Hold(t, 0.015f, 0.3f, 0.12f));
            var thud = Noise(r, len);
            LowPass(thud, 250f);
            Shape(thud, t => Env(t, 0.001f, 0.05f));
            Normalize(x);
            Add(x, thud, 0.6f);
            return x;
        }

        // A war horn for a wave: two low notes a fifth apart, brightening as it swells.
        static float[] Horn(Rng r)
        {
            const float len = 2.6f;
            var x = Buffer(len);
            foreach (var (f, gain) in new[] { (98f, 1f), (147.3f, 0.7f) })
            {
                var note = Tone(len, t => f * (1f + (t > 0.5f ? 0.006f * (float)Math.Sin(2 * Math.PI * 4.5 * t) : 0f)), Saw);
                Add(x, note, gain);
            }
            LowPass(x, t => 300f + 1100f * Math.Min(1f, t / 0.4f));
            LowPass(x, t => 300f + 1100f * Math.Min(1f, t / 0.4f));
            Shape(x, t => Hold(t, 0.3f, len, 0.7f));
            return x;
        }

        static float[] Loot()
        {
            const float len = 0.5f;
            var x = Buffer(len);
            foreach (var (f, start) in new[] { (1320f, 0f), (1760f, 0.07f) })
            {
                var ping = Tone(len - start, t => f, Sine);
                var over = Tone(len - start, t => f * 2f, Sine);
                Add(ping, over, 0.2f);
                Shape(ping, t => Env(t, 0.002f, 0.13f));
                Add(x, ping, 0.6f, (int)(start * Rate));
            }
            return x;
        }

        // Research finished, a new age: four notes going up.
        static float[] Chime()
        {
            const float len = 1.6f;
            var x = Buffer(len);
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int n = 0; n < notes.Length; n++)
            {
                float start = n * 0.11f, f = notes[n];
                var note = Tone(len - start, t => f, Sine);
                var over = Tone(len - start, t => f * 2f, Sine);
                Add(note, over, 0.25f);
                Shape(note, t => Env(t, 0.003f, 0.45f));
                Add(x, note, 0.5f, (int)(start * Rate));
            }
            return x;
        }

        // ---------------------------------------------------------- the loops --

        static float[] Wind(Rng r, float seconds)
        {
            var low = Noise(r, seconds);
            LowPass(low, 400f);
            LowPass(low, 400f);
            Normalize(low);
            var band = Noise(r, seconds);
            BandPass(band, t => 500f + 250f * (float)Math.Sin(2 * Math.PI * 0.11 * t), 0.7f);
            Normalize(band);
            var x = Buffer(seconds);
            Add(x, low, 0.7f);
            Add(x, band, 0.3f);
            LowPass(x, 900f);
            LowPass(x, 1200f);
            Shape(x, t => 0.55f + 0.3f * (float)Math.Sin(2 * Math.PI * 0.13 * t + 1) + 0.15f * (float)Math.Sin(2 * Math.PI * 0.31 * t));
            return x;
        }

        // Three crickets, each chirping its own song: short pulses of a high tone.
        static float[] Crickets(Rng r, float seconds)
        {
            var x = Buffer(seconds);
            foreach (var (f, gain) in new[] { (4300f, 1f), (4800f, 0.7f), (5200f, 0.5f) })
            {
                var song = Buffer(seconds);
                float at = r.Range(0f, 0.6f);
                while (at < seconds)
                {
                    int pulses = 3 + (int)(r.Next() * 2f);
                    for (int p = 0; p < pulses; p++)
                    {
                        int start = (int)((at + p * 0.04f) * Rate), length = (int)(0.012f * Rate);
                        for (int i = 0; i < length && start + i < song.Length; i++)
                        {
                            float s = (float)Math.Sin(Math.PI * i / length);
                            song[start + i] += s * s * (float)Math.Sin(2 * Math.PI * f * (start + i) / Rate);
                        }
                    }
                    at += r.Range(0.5f, 1.1f);
                }
                Add(x, song, gain);
            }
            return x;
        }

        // Two birds: little groups of chirps sliding up or down, now and then.
        static float[] Birds(Rng r, float seconds)
        {
            var x = Buffer(seconds);
            for (int bird = 0; bird < 2; bird++)
            {
                float at = r.Range(0.2f, 1.5f);
                while (at < seconds)
                {
                    int notes = 2 + (int)(r.Next() * 4f);
                    float from = r.Range(2500f, 3800f), to = from * r.Range(0.7f, 1.4f);
                    for (int n = 0; n < notes; n++)
                    {
                        float length = r.Range(0.06f, 0.14f);
                        int start = (int)(at * Rate), count = (int)(length * Rate);
                        double phase = 0;
                        for (int i = 0; i < count && start + i < x.Length; i++)
                        {
                            float k = i / (float)count;
                            float f = from + (to - from) * k;
                            phase += f * (1 + 0.03 * Math.Sin(2 * Math.PI * 30 * i / Rate)) / Rate;
                            float s = (float)Math.Sin(Math.PI * k);
                            x[start + i] += s * s * (float)Math.Sin(2 * Math.PI * phase) * (bird == 0 ? 1f : 0.7f);
                        }
                        at += length + r.Range(0.03f, 0.09f);
                    }
                    at += r.Range(1.2f, 3.5f);
                }
            }
            return x;
        }

        // --------------------------------------------------------------- tools --

        /// <summary>A tiny random number maker, the same numbers for the same seed (so every sound comes out the same).</summary>
        sealed class Rng
        {
            uint state;
            public Rng(uint seed) { state = seed * 2654435761u + 1u; if (state == 0) state = 1; }
            /// <summary>0 up to 1.</summary>
            public float Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state / 4294967296f;
            }
            public float Signed() => Next() * 2f - 1f;
            public float Range(float a, float b) => a + (b - a) * Next();
        }

        static float[] Buffer(float seconds) => new float[Math.Max(1, (int)(seconds * Rate))];
        static float Seconds(int i) => i / (float)Rate;

        static float[] Noise(Rng r, float seconds)
        {
            var x = Buffer(seconds);
            for (int i = 0; i < x.Length; i++) x[i] = r.Signed();
            return x;
        }

        static float Sine(float phase) => (float)Math.Sin(2 * Math.PI * phase);
        static float Saw(float phase) => 2f * phase - 1f;

        // A tone whose frequency can change over time; shape gives the wave for a phase from 0 to 1.
        static float[] Tone(float seconds, Func<float, float> frequency, Func<float, float> shape)
        {
            var x = Buffer(seconds);
            double phase = 0;
            for (int i = 0; i < x.Length; i++)
            {
                phase += frequency(Seconds(i)) / Rate;
                phase -= Math.Floor(phase);
                x[i] = shape((float)phase);
            }
            Normalize(x);
            return x;
        }

        // A plucked string (Karplus-Strong): a burst of noise going round a delay line, softening each time.
        static float[] Pluck(Rng r, float seconds, float frequency, float keep)
        {
            var x = Buffer(seconds);
            int n = Math.Max(2, (int)(Rate / frequency));
            var line = new float[n];
            for (int k = 0; k < n; k++) line[k] = r.Signed();
            int at = 0;
            for (int i = 0; i < x.Length; i++)
            {
                float now = line[at], next = line[(at + 1) % n];
                x[i] = now;
                line[at] = 0.5f * (now + next) * keep;
                at = (at + 1) % n;
            }
            Normalize(x);
            return x;
        }

        // Rises over `attack` seconds, then dies away (to a third every `decay` seconds).
        static float Env(float t, float attack, float decay) =>
            (attack > 0f && t < attack ? t / attack : 1f) * (float)Math.Exp(-Math.Max(0f, t - attack) / decay);

        // Rises over `attack` seconds, holds, and fades out over `release` seconds before `length`.
        static float Hold(float t, float attack, float length, float release) =>
            Math.Min(attack > 0f ? Math.Min(1f, t / attack) : 1f, Math.Max(0f, Math.Min(1f, (length - t) / release)));

        static void Shape(float[] x, Func<float, float> envelope)
        {
            for (int i = 0; i < x.Length; i++) x[i] *= envelope(Seconds(i));
        }

        // One-pole low-pass: lets through what's under the cutoff (which may change over time).
        static void LowPass(float[] x, Func<float, float> cutoff)
        {
            float y = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                float a = (float)Math.Exp(-2 * Math.PI * Math.Max(1f, cutoff(Seconds(i))) / Rate);
                y = (1f - a) * x[i] + a * y;
                x[i] = y;
            }
            Normalize(x);
        }

        static void LowPass(float[] x, float cutoff) => LowPass(x, t => cutoff);

        static void HighPass(float[] x, float cutoff)
        {
            float a = (float)Math.Exp(-2 * Math.PI * cutoff / Rate), low = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                low = (1f - a) * x[i] + a * low;
                x[i] -= low;
            }
            Normalize(x);
        }

        // Band-pass (a resonant filter): lets through what's near the center frequency (which may change over time).
        static void BandPass(float[] x, Func<float, float> center, float q)
        {
            float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
            for (int i = 0; i < x.Length; i++)
            {
                double f0 = Math.Max(20.0, Math.Min(Rate * 0.45, center(Seconds(i))));
                double w0 = 2 * Math.PI * f0 / Rate, alpha = Math.Sin(w0) / (2 * q), cos = Math.Cos(w0), a0 = 1 + alpha;
                float b0 = (float)(alpha / a0), b2 = (float)(-alpha / a0), a1 = (float)(-2 * cos / a0), a2 = (float)((1 - alpha) / a0);
                float y = b0 * x[i] + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = x[i];
                y2 = y1;
                y1 = y;
                x[i] = y;
            }
            Normalize(x);
        }

        // Each part is made as loud as it can be before it's mixed, so the gains in the mix say how loud it is.
        static void Normalize(float[] x)
        {
            float peak = 0f;
            foreach (float v in x) peak = Math.Max(peak, Math.Abs(v));
            if (peak < 1e-9f) return;
            float k = 1f / peak;
            for (int i = 0; i < x.Length; i++) x[i] *= k;
        }

        static void Add(float[] into, float[] x, float gain, int at = 0)
        {
            for (int i = 0; i < x.Length && at + i < into.Length; i++) into[at + i] += x[i] * gain;
        }

        // The end fades into the start, so it loops without a seam: the result is `length` samples.
        static float[] Seamless(float[] x, int length, int fade)
        {
            var y = new float[length];
            Array.Copy(x, y, length);
            for (int i = 0; i < fade && length + i < x.Length; i++)
            {
                float a = i / (float)fade;
                y[i] = x[i] * (float)Math.Sqrt(a) + x[length + i] * (float)Math.Sqrt(1f - a);
            }
            return y;
        }

        // To `peak` at the loudest. One-shots also lose any low hum (DC) and fade in over a few samples and out at the end
        // (no clicks); loops keep their edges as they are, since their end runs on into their start.
        static void Finish(float[] x, float peak, bool oneShot)
        {
            if (oneShot)
            {
                float low = 0f, a = (float)Math.Exp(-2 * Math.PI * 25.0 / Rate);
                for (int i = 0; i < x.Length; i++)
                {
                    low = (1f - a) * x[i] + a * low;
                    x[i] -= low;
                }
            }
            float loudest = 0f;
            foreach (float v in x) loudest = Math.Max(loudest, Math.Abs(v));
            if (loudest > 1e-9f)
            {
                float k = peak / loudest;
                for (int i = 0; i < x.Length; i++) x[i] *= k;
            }
            if (!oneShot) return;
            int fadeIn = Math.Min(x.Length, Rate / 600), fadeOut = Math.Min(x.Length, Rate / 60);
            for (int i = 0; i < fadeIn; i++) x[i] *= i / (float)fadeIn;
            for (int i = 0; i < fadeOut; i++) x[x.Length - 1 - i] *= i / (float)fadeOut;
        }
    }
}
