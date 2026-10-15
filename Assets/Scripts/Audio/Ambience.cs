using KingdomGame.World;
using UnityEngine;

namespace KingdomGame.Audio
{
    /// <summary>
    /// The background sounds, on a loop: wind all the time (stronger at night), birds by day, crickets at night.
    /// How loud is the Background sounds slider in Settings (times the sound volume).
    /// </summary>
    public sealed class Ambience : MonoBehaviour
    {
        DayNightCycle dayNight;
        AudioSource wind, birds, crickets;
        AudioClip[] made;

        public void Setup(DayNightCycle cycle)
        {
            dayNight = cycle;
            if (Application.isBatchMode) return;
            made = new AudioClip[SoundSynth.LoopCount];
            wind = Source(Loop.Wind);
            birds = Source(Loop.Birds);
            crickets = Source(Loop.Crickets);
            // Each starts at a different place in its loop.
            foreach (var s in new[] { wind, birds, crickets }) s.timeSamples = Random.Range(0, s.clip.samples);
        }

        AudioSource Source(Loop l)
        {
            var samples = SoundSynth.MakeLoop(l);
            var clip = AudioClip.Create(l.ToString(), samples.Length, 1, SoundSynth.Rate, false);
            clip.SetData(samples, 0);
            made[(int)l] = clip;
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
            return source;
        }

        void Update()
        {
            if (wind == null) return;
            float day = dayNight != null ? dayNight.Daylight : 1f;
            float level = GameSettings.Ambience;
            // Eased toward where they should be, so dawn and dusk come in slowly.
            float step = Time.unscaledDeltaTime * 0.5f;
            wind.volume = Mathf.MoveTowards(wind.volume, level * Mathf.Lerp(0.35f, 0.2f, day), step);
            birds.volume = Mathf.MoveTowards(birds.volume, level * 0.3f * day * day, step);
            crickets.volume = Mathf.MoveTowards(crickets.volume, level * 0.25f * (1f - day) * (1f - day), step);
        }

        void OnDestroy()
        {
            if (made == null) return;
            foreach (var clip in made) if (clip != null) Destroy(clip);
        }
    }
}
