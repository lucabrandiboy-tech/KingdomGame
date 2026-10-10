using System.Collections.Generic;
using KingdomGame.Art;
using UnityEngine;

namespace KingdomGame.Zombies
{
    /// <summary>
    /// What fights leave on the ground: bodies and blood. They fade away after a
    /// while, and only so many stay at once (the oldest go first), so a long night
    /// of fighting doesn't slow the game down. One per game scene (<see cref="Current"/>).
    /// </summary>
    public sealed class Remains : MonoBehaviour
    {
        const int MaxBodies = 60, MaxBlood = 160;
        const float BodySeconds = 50f, BloodSeconds = 120f, FadeSeconds = 5f;

        sealed class Mark
        {
            public SpriteRenderer Renderer;
            public float Born, Life, Alpha;
        }

        readonly List<Mark> bodies = new List<Mark>();
        readonly List<Mark> blood = new List<Mark>();
        readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();
        readonly Sprite[] bloodSprites = new Sprite[ArtCatalog.BloodVariants];

        public static Remains Current { get; private set; }

        void Awake()
        {
            Current = this;
            for (int v = 0; v < bloodSprites.Length; v++) bloodSprites[v] = ArtSprites.Get(ArtKeys.Blood(v));
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        /// <summary>A body lying on the ground, turned <paramref name="degrees"/>.</summary>
        public void Body(Sprite sprite, Vector2 at, float degrees, float scale = 1f)
        {
            if (sprite == null) return;
            Add(bodies, MaxBodies, sprite, at, degrees, scale, SortOrder.Corpses, BodySeconds, 1f);
        }

        /// <summary>A splash of blood, <paramref name="size"/> times the usual size.</summary>
        public void Blood(Vector2 at, float size = 1f)
        {
            var sprite = bloodSprites[Random.Range(0, bloodSprites.Length)];
            if (sprite == null) return;
            Add(blood, MaxBlood, sprite, at, Random.Range(0f, 360f), size * Random.Range(0.8f, 1.2f), SortOrder.Blood, BloodSeconds, Random.Range(0.75f, 0.95f));
        }

        void Add(List<Mark> list, int max, Sprite sprite, Vector2 at, float degrees, float scale, int order, float life, float alpha)
        {
            if (list.Count >= max)
            {
                Recycle(list[0]);
                list.RemoveAt(0);
            }
            var r = spare.Count > 0 ? spare.Pop() : NewRenderer();
            r.gameObject.SetActive(true);
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = new Color(1f, 1f, 1f, alpha);
            r.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, degrees));
            r.transform.localScale = Vector3.one * scale;
            list.Add(new Mark { Renderer = r, Born = Time.time, Life = life, Alpha = alpha });
        }

        SpriteRenderer NewRenderer()
        {
            var go = new GameObject("Remains");
            go.transform.SetParent(transform, false);
            return go.AddComponent<SpriteRenderer>();
        }

        void Recycle(Mark mark)
        {
            if (mark.Renderer == null) return;
            mark.Renderer.gameObject.SetActive(false);
            spare.Push(mark.Renderer);
        }

        void Update()
        {
            Fade(bodies);
            Fade(blood);
        }

        // Fades the old ones out; they're all in the order they were made.
        void Fade(List<Mark> list)
        {
            float now = Time.time;
            int gone = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                float left = m.Born + m.Life - now;
                if (left <= 0f)
                {
                    Recycle(m);
                    gone = i + 1;
                    continue;
                }
                if (left < FadeSeconds)
                    m.Renderer.color = new Color(1f, 1f, 1f, m.Alpha * left / FadeSeconds);
            }
            if (gone > 0) list.RemoveRange(0, gone);
        }
    }
}
