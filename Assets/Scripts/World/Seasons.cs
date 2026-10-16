using System;
using KingdomGame.Art;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// The seasons and the weather in the game (<see cref="SeasonMath"/> works out which, from the day, the hour and the
    /// world's seed, so it's the same for everyone online and needs no saving). It moves the plants' clock (nothing
    /// grows in winter, rain helps), lengthens summer days and winter nights, dims the light under clouds, turns the
    /// trees' leaves, lays snow on the ground and draws the rain, snow and fog round the camera. In a storm lightning
    /// flashes and thunder rolls; where it strikes is told to <see cref="LightningStruck"/> (fire can start there).
    /// Gameplay reads it too: snow slows walking (<see cref="MoveFactor"/>), fog shortens towers' sight
    /// (<see cref="TowerRange"/>), winter needs firewood (the kingdom).
    /// </summary>
    public sealed class Seasons : MonoBehaviour
    {
        public static Seasons Current { get; private set; }

        const int Drops = 420, Splashes = 70;
        // Where the weather is drawn (above the leaves, under the markers of the throne view).
        const int PrecipitationOrder = SortOrder.Canopies + 200, FogOrder = SortOrder.Canopies + 300;

        sealed class Drop
        {
            public SpriteRenderer Look;
            public Vector2 At, Velocity;
            public float Age, Life, Sway;
        }

        SeasonConfig config;
        DayNightCycle clock;
        int seed;
        float[][] chances;
        Camera view;

        Sprite rain, flake, splash, fogSprite, snowSprite;
        Drop[] drops;
        Drop[] splashes;
        int nextSplash;
        SpriteRenderer snowGround;
        SpriteRenderer[] fog;
        float snowPeriod = 16f, fogPeriod = 14f;
        Vector2 wind = new Vector2(-1f, -0.35f);
        Vector2 fogDrift;
        float lightningAt, thunderAt;
        Vector2 strikeAt;
        int lastLook = -1;
        readonly System.Collections.Generic.List<UnityEngine.Object> made = new System.Collections.Generic.List<UnityEngine.Object>();

        public SeasonConfig Config => config;
        /// <summary>Seasons are on (the Season Config says so): off, it's always summer and clear.</summary>
        public bool On => config != null && config.seasons;
        public Season Season { get; private set; } = Season.Summer;
        public Weather Weather { get; private set; } = Weather.Clear;
        /// <summary>How heavy the weather is, 0..1.</summary>
        public float Strength { get; private set; }
        /// <summary>Snow lying on the ground, 0..1.</summary>
        public float SnowCover { get; private set; }
        /// <summary>Whole days left of this season after today.</summary>
        public int DaysLeft { get; private set; }
        /// <summary>How fast plants grow right now (1 normal).</summary>
        public float GrowthRate { get; private set; } = 1f;
        /// <summary>For testing (the cheat menu): this weather, at full strength, instead of the sky's.</summary>
        public Weather? Forced { get; set; }
        public bool Winter => On && Season == Season.Winter;
        /// <summary>How fast people and zombies walk out in the snow (1 normally).</summary>
        public float MoveFactor => On ? Mathf.Lerp(1f, config.snowSpeed, SnowCover) : 1f;
        /// <summary>How far towers see (a share of their range): less in fog.</summary>
        public float TowerRange => On && Weather == Weather.Fog ? Mathf.Lerp(1f, config.fogTowerRange, Strength) : 1f;
        /// <summary>Lightning struck here (a storm). Fire can start where it hits.</summary>
        public event Action<Vector2> LightningStruck;

        /// <summary>Like "Summer, 2 days left: heavy rain".</summary>
        public string Describe()
        {
            if (!On) return null;
            string season = Season.ToString();
            string left = DaysLeft == 0 ? "its last day" : DaysLeft == 1 ? "1 day left" : $"{DaysLeft} days left";
            string weather = Weather == Weather.Clear || Strength < 0.05f ? "clear" : $"{(Strength > 0.75f ? "heavy " : Strength < 0.35f ? "light " : "")}{WeatherName(Weather)}";
            return $"{season} ({left}): {weather}";
        }

        static string WeatherName(Weather w) => w == Weather.Storm ? "storm" : w == Weather.Fog ? "fog" : w == Weather.Snow ? "snow" : "rain";

        void Awake()
        {
            if (Current != null && Current != this) Debug.LogWarning("There is more than one Seasons in the scene.", this);
            Current = this;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            ResourceNode.SetSeasonLook(0);
            if (clock != null)
            {
                clock.DayLengthShift = 0f;
                clock.Overcast = 0f;
            }
            foreach (var o in made) if (o != null) Destroy(o);
        }

        /// <summary><paramref name="worldSeed"/>: the world's seed (the weather comes from it, the same for everyone in it).</summary>
        public void Setup(SeasonConfig seasonConfig, DayNightCycle dayNight, int worldSeed)
        {
            config = seasonConfig;
            clock = dayNight;
            seed = worldSeed;
            chances = config != null ? config.Chances() : null;
            if (Application.isBatchMode || !On) return;   // nothing to draw on a server

            rain = Made(PixelTextures.ToSprite(WeatherArt.RainStreak(64), "Rain", false, 64));
            flake = Made(PixelTextures.ToSprite(WeatherArt.Snowflake(64), "Snowflake", false, 64));
            splash = Made(PixelTextures.ToSprite(WeatherArt.Splash(64), "Splash", false, 64));
            // Fog: 192 pixels for 14 tiles (soft and blurry is the point); lying snow: 512 for 16 tiles (smooth, and big
            // enough that its repeat doesn't show).
            fogSprite = Made(PixelTextures.ToSprite(WeatherArt.Fog(192, (uint)worldSeed + 3u), "Fog", true, 192f / fogPeriod));
            snowSprite = Made(PixelTextures.ToSprite(WeatherArt.SnowGround(512, (uint)worldSeed + 5u), "Snow", true, 512f / snowPeriod));

            var precipitation = new GameObject("Rain and snow").transform;
            precipitation.SetParent(transform, false);
            drops = new Drop[Drops];
            for (int i = 0; i < drops.Length; i++) drops[i] = NewDrop(precipitation, rain, PrecipitationOrder);
            splashes = new Drop[Splashes];
            for (int i = 0; i < splashes.Length; i++) splashes[i] = NewDrop(precipitation, splash, PrecipitationOrder - 1);

            snowGround = Tiled("Snow on the ground", snowSprite, SortOrder.GroundTint + 1);
            fog = new[] { Tiled("Fog", fogSprite, FogOrder), Tiled("Fog (far)", fogSprite, FogOrder - 1) };
            fog[1].transform.localScale = new Vector3(1.7f, 1.7f, 1f);
        }

        Sprite Made(Sprite s)
        {
            made.Add(s);
            if (s != null) made.Add(s.texture);
            return s;
        }

        static Drop NewDrop(Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject("Drop");
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.enabled = false;
            return new Drop { Look = r, Life = -1f };
        }

        SpriteRenderer Tiled(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.drawMode = SpriteDrawMode.Tiled;
            r.tileMode = SpriteTileMode.Continuous;
            r.sortingOrder = order;
            r.color = new Color(1f, 1f, 1f, 0f);
            r.enabled = false;
            return r;
        }

        void Update()
        {
            if (clock == null) return;
            if (!On)
            {
                Season = Season.Summer;
                Weather = Weather.Clear;
                Strength = 0f;
                SnowCover = 0f;
                GrowthRate = 1f;
                NatureClock.Advance(Time.deltaTime, 1f);
                return;
            }
            int day = clock.Day, per = Mathf.Max(1, config.daysPerSeason);
            float hour = clock.Hour;
            Season = SeasonMath.SeasonOf(day, per);
            DaysLeft = SeasonMath.DaysLeft(day, per);
            SnowCover = SeasonMath.SnowCover(day, hour, per);
            if (Forced.HasValue)
            {
                Weather = Forced.Value;
                Strength = Weather == Weather.Clear ? 0f : 1f;
            }
            else
            {
                Weather = SeasonMath.WeatherAt(seed, day, hour, per, chances, config.spellHours, out float strength);
                Strength = strength;
            }
            GrowthRate = SeasonMath.GrowthRate(Season, Weather, Strength, config.growth, config.rainGrowth);
            NatureClock.Advance(Time.deltaTime, GrowthRate);

            clock.DayLengthShift = SeasonMath.DayLengthShift(day, hour, per, config.dayLengthShift);
            float cloud = Weather == Weather.Storm ? 1.3f : Weather == Weather.Rain ? 1f : Weather == Weather.Snow ? 0.7f : Weather == Weather.Fog ? 0.6f : 0f;
            clock.Overcast = Mathf.Clamp01(config.overcast * cloud * Strength);

            // The trees: autumn colours from autumn (until the snow settles), bare and snowy while it lies, green otherwise.
            int look = SnowCover > 0.5f ? 2 : Season == Season.Autumn || Season == Season.Winter ? 1 : 0;
            if (look != lastLook)
            {
                lastLook = look;
                ResourceNode.SetSeasonLook(look);
            }

            Storm();
        }

        // In a storm: now and then a flash of lightning somewhere in sight, the thunder a moment later.
        void Storm()
        {
            float now = Time.time;
            if (thunderAt > 0f && now >= thunderAt)
            {
                thunderAt = 0f;
                Audio.Sounds.PlayHere(Audio.Sound.Thunder, 0.6f + 0.4f * Strength);
            }
            if (Weather != Weather.Storm || Strength < 0.4f || Time.timeScale <= 0f) return;
            if (lightningAt <= 0f) lightningAt = now + NextStrike();
            if (now < lightningAt) return;
            lightningAt = now + NextStrike();
            var cam = View();
            Vector2 middle = cam != null ? (Vector2)cam.transform.position : Vector2.zero;
            float reach = cam != null ? cam.orthographicSize * 2.5f : 30f;
            strikeAt = middle + UnityEngine.Random.insideUnitCircle * reach;
            clock.Flash = 0.6f + 0.4f * UnityEngine.Random.value;
            // The further away, the longer the thunder takes (sound is slow).
            float far = (strikeAt - middle).magnitude / Mathf.Max(1f, reach);
            thunderAt = now + 0.15f + far * 1.8f;
            if (NatureMap.Inside(new Vector2Int(Mathf.FloorToInt(strikeAt.x), Mathf.FloorToInt(strikeAt.y))))
                LightningStruck?.Invoke(strikeAt);
        }

        float NextStrike() => 60f / Mathf.Max(0.1f, config.lightningPerMinute * Mathf.Max(0.2f, Strength)) * UnityEngine.Random.Range(0.4f, 1.6f);

        Camera View()
        {
            if (view == null || !view.isActiveAndEnabled) view = Camera.main;
            return view;
        }

        // Drawn after the camera has moved this frame.
        void LateUpdate()
        {
            if (drops == null || !On) return;
            var cam = View();
            if (cam == null) return;
            Vector2 middle = cam.transform.position;
            // Inside a house or down a mine (far off the map) there's no weather.
            bool outdoors = NatureMap.Inside(new Vector2Int(Mathf.FloorToInt(middle.x), Mathf.FloorToInt(middle.y)));
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);

            // Snow lying on the ground.
            Cover(snowGround, outdoors ? SnowCover * 0.9f : 0f, middle, halfW, halfH, snowPeriod, Vector2.zero);
            // Fog drifting past, two layers at different speeds.
            float fogAlpha = outdoors && Weather == Weather.Fog ? Strength : 0f;
            fogDrift += new Vector2(0.35f, 0.12f) * dt;
            Cover(fog[0], fogAlpha * 0.5f, middle, halfW, halfH, fogPeriod, fogDrift);
            Cover(fog[1], fogAlpha * 0.35f, middle, halfW, halfH, fogPeriod * 1.7f, fogDrift * 0.55f);

            // Rain or snow round the camera: as many drops as the weather is heavy.
            bool snowing = Weather == Weather.Snow;
            bool falling = outdoors && (snowing || Weather == Weather.Rain || Weather == Weather.Storm);
            int wanted = falling ? Mathf.RoundToInt(Drops * Strength * (snowing ? 0.7f : Weather == Weather.Storm ? 1f : 0.75f)) : 0;
            var box = new Rect(middle.x - halfW - 2f, middle.y - halfH - 2f, halfW * 2f + 4f, halfH * 2f + 4f);
            var windNow = (Weather == Weather.Storm ? 1.6f : 1f) * wind;
            for (int i = 0; i < drops.Length; i++)
            {
                var d = drops[i];
                if (d.Life >= 0f)
                {
                    d.Age += dt;
                    if (d.Age >= d.Life || !box.Contains(d.At))
                    {
                        if (!snowing && d.Age >= d.Life && box.Contains(d.At)) Splash(d.At);
                        d.Life = -1f;
                    }
                }
                if (d.Life < 0f)
                {
                    if (i >= wanted) { if (d.Look.enabled) d.Look.enabled = false; continue; }
                    // A new one somewhere in view.
                    d.Look.sprite = snowing ? flake : rain;
                    d.At = new Vector2(UnityEngine.Random.Range(box.xMin, box.xMax), UnityEngine.Random.Range(box.yMin, box.yMax));
                    d.Age = 0f;
                    d.Sway = UnityEngine.Random.Range(0f, 10f);
                    if (snowing)
                    {
                        d.Life = UnityEngine.Random.Range(2.5f, 5f);
                        d.Velocity = windNow.normalized * UnityEngine.Random.Range(0.6f, 1.4f);
                    }
                    else
                    {
                        d.Life = UnityEngine.Random.Range(0.25f, 0.45f);
                        d.Velocity = windNow.normalized * UnityEngine.Random.Range(14f, 20f);
                    }
                    d.Look.enabled = true;
                }
                d.At += d.Velocity * dt;
                if (snowing) d.At += new Vector2(Mathf.Sin(Time.time * 1.3f + d.Sway) * 0.35f * dt, 0f);
                var t = d.Look.transform;
                t.position = new Vector3(d.At.x, d.At.y, 0f);
                if (!snowing) t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.Velocity.y, d.Velocity.x) * Mathf.Rad2Deg);
                // Rain is faint and brief; snow fades in and out.
                float k = d.Age / Mathf.Max(0.01f, d.Life);
                float alpha = snowing ? Mathf.Min(1f, Mathf.Min(k * 4f, (1f - k) * 4f)) * 0.9f : Mathf.Min(1f, (1f - k) * 3f) * 0.42f;
                d.Look.color = new Color(1f, 1f, 1f, alpha);
            }

            foreach (var s in splashes)
            {
                if (s.Life < 0f) continue;
                s.Age += dt;
                if (s.Age >= s.Life)
                {
                    s.Life = -1f;
                    s.Look.enabled = false;
                    continue;
                }
                float k = s.Age / s.Life;
                s.Look.transform.localScale = Vector3.one * (0.4f + k * 0.8f);
                s.Look.color = new Color(1f, 1f, 1f, (1f - k) * 0.5f);
            }
        }

        void Splash(Vector2 at)
        {
            if (splashes == null || UnityEngine.Random.value > 0.35f) return;
            var s = splashes[nextSplash];
            nextSplash = (nextSplash + 1) % splashes.Length;
            s.At = at;
            s.Age = 0f;
            s.Life = 0.3f;
            s.Look.transform.position = new Vector3(at.x, at.y, 0f);
            s.Look.enabled = true;
        }

        // A tiled layer covering the view, at the given see-through-ness; the pattern stays put on the ground
        // (moved only in whole repeats), shifted by `drift`.
        static void Cover(SpriteRenderer r, float alpha, Vector2 middle, float halfW, float halfH, float period, Vector2 drift)
        {
            if (r == null) return;
            bool show = alpha > 0.005f;
            if (r.enabled != show) r.enabled = show;
            if (!show) return;
            float scale = r.transform.localScale.x;
            float p = period * scale;
            var offset = new Vector2(Mathf.Repeat(drift.x, p), Mathf.Repeat(drift.y, p));
            var corner = new Vector2(Mathf.Floor((middle.x - halfW) / p) * p - p, Mathf.Floor((middle.y - halfH) / p) * p - p) + offset;
            var size = new Vector2(Mathf.Ceil((halfW * 2f) / p + 3f) * p, Mathf.Ceil((halfH * 2f) / p + 3f) * p);
            r.transform.position = new Vector3(corner.x, corner.y, 0f);
            r.size = size / scale;
            r.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}
