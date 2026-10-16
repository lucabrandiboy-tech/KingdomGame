using System.Collections;
using System.Collections.Generic;
using KingdomGame.Combat;
using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Audio
{
    /// <summary>
    /// Plays the game's sounds (made in code by <see cref="SoundSynth"/>, no sound files). A sound at a place in the
    /// world is louder the nearer it is to the middle of the screen and comes from the left or right; things on the
    /// screen are always heard. Each kind of sound has a loudness, a most-at-once and a shortest gap, so a gatling or a
    /// horde doesn't drown everything. The volume is in Settings (<see cref="GameSettings"/>).
    /// </summary>
    public sealed class Sounds : MonoBehaviour
    {
        // How loud each sound is, the most playing at once, the shortest gap between two (seconds),
        // and how far past the edge of the screen it can be heard (tiles).
        struct Mix
        {
            public float Volume, Gap, Range;
            public int Max;
            public Mix(float volume, int max, float gap, float range) { Volume = volume; Max = max; Gap = gap; Range = range; }
        }

        static Mix MixOf(Sound s)
        {
            switch (s)
            {
                case Sound.Gun: return new Mix(0.45f, 6, 0.03f, 25f);
                case Sound.HeavyGun: return new Mix(0.7f, 4, 0.05f, 35f);
                case Sound.Bow: return new Mix(0.4f, 6, 0.03f, 18f);
                case Sound.Throw: return new Mix(0.45f, 4, 0.05f, 20f);
                case Sound.Cannon: return new Mix(0.75f, 4, 0.08f, 40f);
                case Sound.Rocket: return new Mix(0.6f, 4, 0.06f, 30f);
                case Sound.Flame: return new Mix(0.35f, 3, 0.08f, 15f);
                case Sound.Swing: return new Mix(0.4f, 3, 0.05f, 10f);
                case Sound.ExplosionBig: return new Mix(1f, 6, 0.04f, 55f);
                case Sound.ExplosionSmall: return new Mix(0.75f, 6, 0.03f, 35f);
                case Sound.Groan: return new Mix(0.35f, 4, 0.25f, 15f);
                case Sound.ZombieHit: return new Mix(0.45f, 5, 0.06f, 20f);
                case Sound.ZombieDie: return new Mix(0.45f, 4, 0.05f, 20f);
                case Sound.Claw: return new Mix(0.5f, 4, 0.05f, 15f);
                case Sound.Spit: return new Mix(0.45f, 3, 0.05f, 18f);
                case Sound.Splash: return new Mix(0.45f, 3, 0.05f, 18f);
                case Sound.Scream: return new Mix(0.7f, 2, 0.2f, 35f);
                case Sound.Hammer: return new Mix(0.3f, 4, 0.08f, 12f);
                case Sound.Chop: return new Mix(0.35f, 4, 0.06f, 12f);
                case Sound.Pick: return new Mix(0.3f, 4, 0.06f, 12f);
                case Sound.Rustle: return new Mix(0.25f, 3, 0.08f, 10f);
                case Sound.Door: return new Mix(0.35f, 3, 0.08f, 12f);
                case Sound.Hurt: return new Mix(0.6f, 1, 0.15f, 0f);
                case Sound.Horn: return new Mix(0.65f, 1, 1f, 0f);
                case Sound.Loot: return new Mix(0.5f, 2, 0.05f, 0f);
                case Sound.Thunder: return new Mix(0.9f, 2, 0.4f, 0f);
                case Sound.Crackle: return new Mix(0.4f, 3, 0.2f, 15f);
                case Sound.Howl: return new Mix(0.5f, 2, 1.2f, 70f);
                case Sound.Snarl: return new Mix(0.5f, 3, 0.15f, 18f);
                default: return new Mix(0.55f, 1, 0.3f, 0f);   // the chime
            }
        }

        const int Voices = 24;
        static Sounds instance;

        AudioSource[] voices;
        Sound[] playing;
        float[] started;
        readonly float[] lastPlayed = new float[SoundSynth.SoundCount];
        readonly Dictionary<(Sound, int), AudioClip> clips = new Dictionary<(Sound, int), AudioClip>();
        Camera view;

        /// <summary>Plays a sound at <paramref name="at"/> in the world.</summary>
        public static void Play(Sound s, Vector2 at, float volume = 1f)
        {
            var self = Get();
            if (self != null) self.PlayAt(s, at, volume, false);
        }

        /// <summary>Plays a sound that isn't anywhere in the world (the ruler's own, a horn, a chime).</summary>
        public static void PlayHere(Sound s, float volume = 1f)
        {
            var self = Get();
            if (self != null) self.PlayAt(s, default, volume, true);
        }

        /// <summary>The sound a weapon makes when it fires (null: none).</summary>
        public static Sound? Of(WeaponConfig weapon)
        {
            if (weapon == null) return null;
            switch (weapon.sound)
            {
                case ShotSound.Gun: return Sound.Gun;
                case ShotSound.HeavyGun: return Sound.HeavyGun;
                case ShotSound.Bow: return Sound.Bow;
                case ShotSound.Throw: return Sound.Throw;
                case ShotSound.Cannon: return Sound.Cannon;
                case ShotSound.Rocket: return Sound.Rocket;
                case ShotSound.Flame: return Sound.Flame;
                case ShotSound.Swing: return Sound.Swing;
                case ShotSound.None: return null;
            }
            // Auto: a guess from what it is.
            if (weapon.melee) return Sound.Swing;
            if (weapon.damage != null && weapon.damage.category == DamageCategory.Explosive) return Sound.Cannon;
            if (weapon.damage != null && weapon.damage.category == DamageCategory.Bullet) return Sound.Gun;
            return Sound.Bow;
        }

        /// <summary>Plays the sound a weapon makes, if it has one.</summary>
        public static void Fired(WeaponConfig weapon, Vector2 at, float volume = 1f)
        {
            var s = Of(weapon);
            if (s.HasValue) Play(s.Value, at, volume);
        }

        static Sounds Get()
        {
            if (instance != null) return instance;
            if (Application.isBatchMode) return null;   // a dedicated server has no one to hear it
            instance = new GameObject("Sounds").AddComponent<Sounds>();
            return instance;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        void Awake()
        {
            voices = new AudioSource[Voices];
            playing = new Sound[Voices];
            started = new float[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;   // 2D: panned and made quieter by hand (see PlayAt)
                voices[i] = source;
            }
            for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = -10f;
            StartCoroutine(MakeAll());
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            AudioListener.pause = false;
            foreach (var clip in clips.Values) if (clip != null) Destroy(clip);
        }

        void Update()
        {
            // The game paused (offline): so are the sounds.
            bool pause = Time.timeScale <= 0f;
            if (AudioListener.pause != pause) AudioListener.pause = pause;
        }

        // Makes every sound ahead of time, one a frame, so the first gunshot doesn't stutter.
        IEnumerator MakeAll()
        {
            for (int s = 0; s < SoundSynth.SoundCount; s++)
                for (int v = 0; v < SoundSynth.Variants((Sound)s); v++)
                {
                    Clip((Sound)s, v);
                    yield return null;
                }
        }

        AudioClip Clip(Sound s, int variant)
        {
            if (clips.TryGetValue((s, variant), out var clip)) return clip;
            var samples = SoundSynth.Make(s, variant);
            clip = AudioClip.Create($"{s}{variant}", samples.Length, 1, SoundSynth.Rate, false);
            clip.SetData(samples, 0);
            clips[(s, variant)] = clip;
            return clip;
        }

        Camera View()
        {
            if (view == null || !view.isActiveAndEnabled) view = Camera.main;
            return view;
        }

        void PlayAt(Sound s, Vector2 at, float volume, bool here)
        {
            var mix = MixOf(s);
            float now = Time.unscaledTime;
            if (now - lastPlayed[(int)s] < mix.Gap) return;
            float loudness = mix.Volume * volume, pan = 0f;
            if (!here)
            {
                var cam = View();
                if (cam == null) return;
                Vector2 middle = cam.transform.position;
                float halfHeight = cam.orthographic ? cam.orthographicSize : 10f, halfWidth = halfHeight * cam.aspect;
                // Heard all over the screen, and out past its edge (further for loud things).
                float hearing = Mathf.Max(halfWidth, halfHeight) + mix.Range;
                float d = (at - middle).magnitude;
                if (d > hearing) return;
                float near = 1f - d / hearing;
                loudness *= Mathf.Lerp(0.2f, 1f, near * near);
                pan = Mathf.Clamp((at.x - middle.x) / Mathf.Max(1f, halfWidth), -1f, 1f) * 0.7f;
            }
            if (loudness < 0.01f) return;

            // A free voice; if this sound already has its most at once, the oldest of them; else the oldest of all.
            int same = 0, oldestSame = -1, free = -1, oldest = 0;
            for (int i = 0; i < Voices; i++)
            {
                if (!voices[i].isPlaying)
                {
                    if (free < 0) free = i;
                    continue;
                }
                if (started[i] < started[oldest] || !voices[oldest].isPlaying) oldest = i;
                if (playing[i] == s)
                {
                    same++;
                    if (oldestSame < 0 || started[i] < started[oldestSame]) oldestSame = i;
                }
            }
            int use = same >= mix.Max && oldestSame >= 0 ? oldestSame : free >= 0 ? free : oldest;

            var source = voices[use];
            source.Stop();
            source.clip = Clip(s, Random.Range(0, SoundSynth.Variants(s)));
            source.volume = Mathf.Clamp01(loudness);
            source.panStereo = pan;
            // A little different every time (not the tunes).
            source.pitch = s == Sound.Horn || s == Sound.Chime || s == Sound.Loot ? 1f : s == Sound.Thunder ? Random.Range(0.8f, 1.05f) : Random.Range(0.93f, 1.07f);
            source.Play();
            playing[use] = s;
            started[use] = now;
            lastPlayed[(int)s] = now;
        }
    }
}
