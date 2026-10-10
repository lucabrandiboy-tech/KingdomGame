using System.Collections.Generic;
using KingdomGame.Economy;
using UnityEngine;

namespace KingdomGame.World
{
    /// <summary>
    /// Tree leaves are drawn above everyone. This makes the leaves around the
    /// ruler see-through, so you can still see yourself (and whoever is
    /// sneaking up on you) in a forest. In the throne view all leaves are a
    /// little see-through, so buildings under them stay visible.
    /// </summary>
    public sealed class CanopyFader : MonoBehaviour
    {
        struct Canopy
        {
            public SpriteRenderer Renderer;
            public Vector2 Center;
            public float Alpha;
        }

        readonly List<Canopy> canopies = new List<Canopy>();

        /// <summary>Whose surroundings get see-through (the ruler).</summary>
        public Transform Focus { get; set; }
        /// <summary>How see-through the leaves near the focus get (0 = invisible, 1 = solid).</summary>
        public float NearAlpha { get; set; } = 0.35f;
        /// <summary>How see-through all other leaves are (1 normally, lower in the throne view).</summary>
        public float FarAlpha { get; set; } = 1f;
        /// <summary>Leaves this close to the focus (in tiles) fade.</summary>
        public float Radius { get; set; } = 1.6f;

        public void Add(ResourceNode node)
        {
            if (node == null || node.Leaves == null) return;
            canopies.Add(new Canopy { Renderer = node.Leaves, Center = node.transform.position, Alpha = 1f });
        }

        void LateUpdate()
        {
            Vector2 focus = Focus != null ? (Vector2)Focus.position : new Vector2(float.MaxValue, float.MaxValue);
            float radius2 = Radius * Radius;
            float step = Time.deltaTime * 4f;
            // Leaves that are gone (their tree was taken away) are dropped as it goes, in one pass.
            int kept = 0;
            for (int i = 0; i < canopies.Count; i++)
            {
                var c = canopies[i];
                if (c.Renderer == null) continue;
                float target = (c.Center - focus).sqrMagnitude < radius2 ? Mathf.Min(NearAlpha, FarAlpha) : FarAlpha;
                if (!Mathf.Approximately(c.Alpha, target))
                {
                    c.Alpha = Mathf.MoveTowards(c.Alpha, target, step);
                    var color = c.Renderer.color;
                    color.a = c.Alpha;
                    c.Renderer.color = color;
                }
                canopies[kept++] = c;
            }
            canopies.RemoveRange(kept, canopies.Count - kept);
        }
    }
}
