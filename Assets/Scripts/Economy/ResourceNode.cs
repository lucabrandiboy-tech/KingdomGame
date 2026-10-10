using System;
using System.Collections.Generic;
using KingdomGame.Destruction;
using UnityEngine;

namespace KingdomGame.Economy
{
    /// <summary>The pictures one tree, rock or bush uses.</summary>
    public sealed class NodeSprites
    {
        /// <summary>The part on the ground: trunk, rock or bush.</summary>
        public Sprite Full;
        /// <summary>Shown once it's used up (a stump, a bush without berries); null = it disappears.</summary>
        public Sprite Empty;
        /// <summary>Leaves above everything (trees only), or null.</summary>
        public Sprite Canopy;
        public Sprite Shadow;
        public Sprite EmptyShadow;
        /// <summary>How high it is, in tiles (how far its shadow falls).</summary>
        public float ShadowHeight = 0.15f;
        public float EmptyShadowHeight = 0.05f;
    }

    /// <summary>
    /// One tree, rock or bush in the world, as a real object (only the ones near players
    /// and kingdoms are: the rest are just data in the <see cref="NatureMap"/>, which this
    /// writes what happens to it back into). Gathering takes resources out of it; when
    /// it's empty a tree becomes a stump, a rock disappears and a bush loses its berries
    /// (and grows them back if Regrow Seconds is set).
    /// </summary>
    public sealed class ResourceNode : MonoBehaviour
    {
        static readonly List<ResourceNode> all = new List<ResourceNode>();

        /// <summary>Every node that's a real object right now (for finding the closest one).</summary>
        public static IReadOnlyList<ResourceNode> All => all;

        ResourceNodeConfig config;
        NodeSprites sprites;
        SpriteRenderer look, leaves, shadow;
        CircleCollider2D block;
        bool solid;
        Vector3 restPosition;
        float shakeTime;
        float regrowAt;
        bool growing;
        float growStart, growEnd;
        NatureMap map;
        int index = -1;

        public ResourceNodeConfig Config => config;
        public int Left { get; private set; }
        public bool Empty => Left <= 0;
        /// <summary>A young planted tree or bush, still growing (nothing to gather yet).</summary>
        public bool Growing => growing;
        /// <summary>How grown it is, 0..1 (1 when it isn't growing).</summary>
        public float Grown => growing ? Mathf.Clamp01((Time.time - growStart) / Mathf.Max(0.01f, growEnd - growStart)) : 1f;
        /// <summary>True while it's solid (people bump into it and walk round it).</summary>
        public bool Blocks => solid;
        /// <summary>The grid cell it stands in.</summary>
        public Vector2Int Cell => new Vector2Int(Mathf.FloorToInt(restPosition.x), Mathf.FloorToInt(restPosition.y));
        /// <summary>The leaves (trees only), for fading them when someone walks under.</summary>
        public SpriteRenderer Leaves => leaves;
        /// <summary>The villager gathering from it (one at a time), or null.</summary>
        public Component Gatherer { get; set; }
        /// <summary>Planted by a kingdom (not part of the map as it was made).</summary>
        public bool Planted => map != null && map.IsPlanted(index);

        /// <summary>Sets the node up. <paramref name="rotation"/> turns the pictures (seen from above, any angle looks natural).</summary>
        public void Setup(ResourceNodeConfig nodeConfig, NodeSprites nodeSprites, float rotation)
        {
            config = nodeConfig;
            sprites = nodeSprites;
            Left = config.amount;
            restPosition = transform.position;

            look = Part("Look", sprites.Full, SortOrder.Nature, rotation);
            if (sprites.Canopy != null) leaves = Part("Leaves", sprites.Canopy, SortOrder.Canopies, rotation * 1.7f + 40f);
            if (sprites.Shadow != null)
            {
                // A tree's shadow comes from its leaves; everything else from its own picture.
                shadow = Look.AddShadow(transform, sprites.Shadow, sprites.ShadowHeight, leaves != null ? rotation * 1.7f + 40f : rotation);
            }

            block = gameObject.AddComponent<CircleCollider2D>();
            block.radius = config.blockRadius;
            solid = true;
            enabled = false; // only runs while shaking, regrowing or growing
        }

        /// <summary>
        /// Ties it to its cell of the <paramref name="nature"/> map and takes on the state kept there
        /// (how much is left, growing, growing back).
        /// </summary>
        public void Bind(NatureMap nature, int cellIndex)
        {
            map = nature;
            index = cellIndex;
            map.Attach(index, this);
            map.StateOf(index, out int amountLeft, out bool isGrowing, out float start, out float end);
            if (isGrowing)
            {
                Grow(Mathf.Max(1f, end - start));
                growStart = start;
                growEnd = end;
                ShowGrowth();
                return;
            }
            Left = Mathf.Clamp(amountLeft, 0, config.amount);
            if (Left > 0) return;
            ShowEmpty();
            if (config.regrowSeconds > 0)
            {
                regrowAt = end > 0f ? end : Time.time + config.regrowSeconds;
                enabled = true;
            }
        }

        /// <summary>Unties it from the map (it's about to go; the map has its state).</summary>
        public void Unbind()
        {
            if (map != null) map.Detach(index, this);
            map = null;
            index = -1;
        }

        /// <summary>Writes its state into the map (before saving, or before it goes).</summary>
        public void WriteBack()
        {
            if (map == null) return;
            bool waiting = !growing && Empty && config.regrowSeconds > 0;
            map.Record(index, Left, growing, growStart, growing ? growEnd : regrowAt, waiting);
        }

        SpriteRenderer Part(string partName, Sprite sprite, int order, float rotation)
        {
            var go = new GameObject(partName);
            go.transform.SetParent(transform, false);
            go.transform.localRotation = Quaternion.Euler(0, 0, rotation);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        void Awake() => all.Add(this);

        void OnDestroy()
        {
            all.Remove(this);
            if (map != null) map.Detach(index, this);
        }

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
            if (world != null) world.Burst(restPosition, config.chipColor, 6, 2.5f);

            if (Empty) UseUp();
            WriteBack();
            return taken;
        }

        /// <summary>Takes it out of the world for good (a building is going up on this spot).</summary>
        public void Clear()
        {
            if (map != null) map.Clear(Cell);
            else Destroy(gameObject);
        }

        /// <summary>Starts it as a young one that grows to full size in <paramref name="seconds"/> (planted, or replanted after being used up).</summary>
        public void Grow(float seconds)
        {
            Left = 0;
            growing = true;
            growStart = Time.time;
            growEnd = Time.time + Mathf.Max(1f, seconds);
            look.sprite = sprites.Full;
            look.enabled = true;
            if (leaves != null) leaves.enabled = true;
            if (shadow != null)
            {
                shadow.enabled = sprites.Shadow != null;
                shadow.sprite = sprites.Shadow;
            }
            SetSolid(true);
            ShowGrowth();
            enabled = true;
        }

        // A growing one is drawn smaller: a quarter of its size when planted, full size when grown.
        void ShowGrowth()
        {
            float k = Mathf.Lerp(0.25f, 1f, Grown);
            var scale = new Vector3(k, k, 1f);
            look.transform.localScale = scale;
            if (leaves != null) leaves.transform.localScale = scale;
            if (shadow != null)
            {
                shadow.transform.localScale = scale;
                shadow.transform.localPosition = (Vector3)(Look.ShadowDirection * sprites.ShadowHeight * k);
            }
        }

        void UseUp()
        {
            // A planted one: a young one grows in its place.
            if (config.replant && config.regrowSeconds > 0f)
            {
                Grow(config.regrowSeconds);
                return;
            }
            ShowEmpty();
            if (config.regrowSeconds > 0) regrowAt = Time.time + config.regrowSeconds;
        }

        // A stump, a bare bush, or (a rock) nothing.
        void ShowEmpty()
        {
            if (sprites.Empty != null) look.sprite = sprites.Empty;
            else look.enabled = false;
            if (leaves != null) leaves.enabled = false;
            if (shadow != null)
            {
                shadow.enabled = sprites.EmptyShadow != null;
                if (shadow.enabled)
                {
                    shadow.sprite = sprites.EmptyShadow;
                    shadow.transform.localPosition = (Vector3)(Look.ShadowDirection * sprites.EmptyShadowHeight);
                    shadow.transform.localRotation = look.transform.localRotation;
                }
            }
            // Stumps and empty bushes stay solid; rocks that disappear don't.
            SetSolid(sprites.Empty != null);
        }

        void Regrow()
        {
            Left = config.amount;
            look.sprite = sprites.Full;
            look.enabled = true;
            if (leaves != null) leaves.enabled = true;
            if (shadow != null)
            {
                shadow.enabled = sprites.Shadow != null;
                shadow.sprite = sprites.Shadow;
                shadow.transform.localPosition = (Vector3)(Look.ShadowDirection * sprites.ShadowHeight);
            }
            SetSolid(true);
            WriteBack();
        }

        void SetSolid(bool value)
        {
            if (block != null) block.enabled = value;
            solid = value;
            if (map != null) map.SetBlocks(index, value);
        }

        void Update()
        {
            bool busy = false;
            if (shakeTime > 0f)
            {
                shakeTime -= Time.deltaTime;
                float wobble = Mathf.Sin(shakeTime * 90f) * shakeTime * 0.25f;
                transform.position = restPosition + new Vector3(wobble, 0, 0);
                busy = shakeTime > 0f;
                if (!busy) transform.position = restPosition;
            }
            if (growing)
            {
                busy = true;
                ShowGrowth();
                if (Time.time >= growEnd)
                {
                    growing = false;
                    Left = config.amount;
                    ShowGrowth();
                    WriteBack();
                }
            }
            else if (Empty && config.regrowSeconds > 0)
            {
                busy = true;
                if (Time.time >= regrowAt) Regrow();
            }
            if (!busy) enabled = false;
        }
    }
}
