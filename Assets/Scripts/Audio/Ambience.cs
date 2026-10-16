using KingdomGame.World;
using UnityEngine;

namespace KingdomGame.Audio
{
    /// <summary>
    /// The background sounds, on a loop: wind all the time (stronger at night, in storms and snow), birds by day
    /// (not in winter or the rain), crickets on warm nights, rain when it rains (not heard inside a house or a mine).
    /// How loud is the Background sounds slider in Settings (times the sound volume).
    /// </summary>
    public sealed class Ambience : MonoBehaviour
    {
        DayNightCycle dayNight;
        AudioSource wind, birds, crickets, rain;
        AudioClip[] made;

        public void Setup(DayNightCycle cycle)
        {
            dayNight = cycle;
            if (Application.isBatchMode) return;
            made = new AudioClip[SoundSynth.LoopCount];
            wind = Source(Loop.Wind);
            birds = Source(Loop.Birds);
            crickets = Source(Loop.Crickets);
            rain = Source(Loop.Rain);
            // Each starts at a different place in its loop.
            foreach (var s in new[] { wind, birds, crickets, rain }) s.timeSamples = Random.Range(0, s.clip.samples);
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
            // The season and the weather (none of it heard inside a house or down a mine, far off the map).
            var seasons = Seasons.Current;
            var cam = Camera.main;
            bool outdoors = cam == null || Economy.NatureMap.Inside(new Vector2Int(Mathf.FloorToInt(cam.transform.position.x), Mathf.FloorToInt(cam.transform.position.y)));
            float wet = 0f, storm = 0f, cold = 0f;
            if (seasons != null && seasons.On)
            {
                bool raining = seasons.Weather == Weather.Rain || seasons.Weather == Weather.Storm;
                wet = raining ? seasons.Strength : 0f;
                storm = seasons.Weather == Weather.Storm || seasons.Weather == Weather.Snow ? seasons.Strength : 0f;
                cold = seasons.Season == Season.Winter ? 1f : 0f;
            }
            float inside = outdoors ? 1f : 0.25f;
            // Eased toward where they should be, so dawn, dusk and the rain come in slowly.
            float step = Time.unscaledDeltaTime * 0.5f;
            wind.volume = Mathf.MoveTowards(wind.volume, level * inside * Mathf.Min(0.6f, Mathf.Lerp(0.35f, 0.2f, day) + storm * 0.25f), step);
            birds.volume = Mathf.MoveTowards(birds.volume, level * inside * 0.3f * day * day * (1f - cold * 0.85f) * (1f - wet), step);
            crickets.volume = Mathf.MoveTowards(crickets.volume, level * inside * 0.25f * (1f - day) * (1f - day) * (1f - cold) * (1f - wet), step);
            rain.volume = Mathf.MoveTowards(rain.volume, level * inside * 0.5f * wet, step);
        }

        void OnDestroy()
        {
            if (made == null) return;
            foreach (var clip in made) if (clip != null) Destroy(clip);
        }
    }
}
