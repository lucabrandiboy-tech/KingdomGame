using System.Collections.Generic;
using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Economy
{
    /// <summary>
    /// One tree, rock or bush in the world. Gathering takes resources out of it;
    /// when it's empty a tree becomes a stump, a rock disappears and a bush loses
    /// its berries (and grows them back if Regrow Seconds is set).
    /// </summary>
    public sealed class ResourceNode : MonoBehaviour
    {
        static readonly List<ResourceNode> all = new List<ResourceNode>();

        /// <summary>Every node in the scene (for finding the closest one).</summary>
        public static IReadOnlyList<ResourceNode> All => all;

        ResourceNodeConfig config;
        SpriteRenderer look;
        Sprite fullSprite, emptySprite;
        CircleCollider2D block;
        Vector3 restPosition;
        float shakeTime;
        float regrowAt;

        public ResourceNodeConfig Config => config;
        public int Left { get; private set; }
        public bool Empty => Left <= 0;

        /// <summary>Sets the node up. <paramref name="empty"/> is shown once it's used up (null = hide it).</summary>
        public void Setup(ResourceNodeConfig nodeConfig, Sprite full, Sprite empty, int sortingOrder)
        {
            config = nodeConfig;
            fullSprite = full;
            emptySprite = empty;
            Left = config.amount;
            restPosition = transform.position;

            look = gameObject.AddComponent<SpriteRenderer>();
            look.sprite = full;
            look.sortingOrder = sortingOrder;

            block = gameObject.AddComponent<CircleCollider2D>();
            block.radius = config.blockRadius;
            enabled = false; // only runs while shaking or regrowing
        }

        void Awake() => all.Add(this);
        void OnDestroy() => all.Remove(this);

        /// <summary>The distance from <paramref name="from"/> to the edge of the solid part.</summary>
        public float DistanceFrom(Vector2 from) =>
            Mathf.Max(0f, Vector2.Distance(from, restPosition) - (config != null ? config.blockRadius : 0f));

        /// <summary>One swing: takes up to <paramref name="space"/> resources out and returns how many.</summary>
        public int Gather(int space)
        {
            if (Empty || space <= 0) return 0;
            int taken = Mathf.Min(space, Mathf.Min(config.perHit, Left));
            Left -= taken;

            shakeTime = 0.18f;
            enabled = true;
            var world = PixelWorld.Current;
            if (world != null) world.Burst(restPosition + new Vector3(0, 0.25f, 0), config.chipColor, 6, 2.5f);

            if (Empty) UseUp();
            return taken;
        }

        void UseUp()
        {
            if (emptySprite != null) look.sprite = emptySprite;
            else look.enabled = false;
            // Stumps and empty bushes stay solid; rocks that disappear don't.
            block.enabled = emptySprite != null;
            if (config.regrowSeconds > 0) regrowAt = Time.time + config.regrowSeconds;
        }

        void Update()
        {
            bool busy = false;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                float wobble = Mathf.Sin(shakeTime * 90f) * shakeTime * 0.25f;
                transform.position = restPosition + new Vector3(Mathf.Round(wobble * PixelArt.PixelsPerUnit) / PixelArt.PixelsPerUnit, 0, 0);
                busy = shakeTime > 0f;
                if (!busy) transform.position = restPosition;
            }
            if (Empty && config.regrowSeconds > 0)
            {
                busy = true;
                if (Time.time >= regrowAt)
                {
                    Left = config.amount;
                    look.sprite = fullSprite;
                    look.enabled = true;
                    block.enabled = true;
                }
            }
            if (!busy) enabled = false;
        }
    }
}
