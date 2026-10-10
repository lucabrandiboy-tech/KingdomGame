using System;
using System.Collections.Generic;

namespace KingdomGame.Zombies
{
    /// <summary>
    /// The zombies' map of the way to their prey. For every grid cell it knows
    /// how much effort it takes to reach the nearest target from there, so one
    /// field serves every zombie: each one just steps to its cheapest neighbour
    /// (<see cref="Next"/>). Recomputing it costs the same whether there are 5
    /// zombies or 500.
    ///
    /// <see cref="Cost"/> says what stepping into a cell takes: 0 = impossible
    /// (the map edge, trees, rocks, buildings), 1 = open ground, more = something
    /// to break through first (a door, a wall, furniture); the number is how many
    /// tiles of walking that's worth. So zombies walk round a wall if the way
    /// round is short, and smash through when it's the quicker way in.
    ///
    /// Plain C# with no Unity code, so it's unit-tested (tools/tests).
    /// </summary>
    public sealed class FlowField
    {
        public const float Unreachable = float.PositiveInfinity;
        const float Diagonal = 1.41421356f;
        static readonly int[] StepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] StepY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        public readonly int Width, Height;
        /// <summary>What stepping into each cell takes (see the class summary). Index = y * Width + x.</summary>
        public readonly float[] Cost;
        readonly float[] distance;

        // Binary heap of cells to look at, cheapest first.
        float[] heapKey = new float[1024];
        int[] heapCell = new int[1024];
        int heapCount;

        public FlowField(int width, int height)
        {
            Width = width;
            Height = height;
            Cost = new float[width * height];
            distance = new float[width * height];
            for (int i = 0; i < distance.Length; i++) distance[i] = Unreachable;
        }

        public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>Effort from this cell to the nearest target (<see cref="Unreachable"/> if there's no way).</summary>
        public float Distance(int x, int y) => Inside(x, y) ? distance[y * Width + x] : Unreachable;

        /// <summary>Open ground: walkable without breaking anything.</summary>
        public bool Open(int x, int y) => Inside(x, y) && Cost[y * Width + x] == 1f;

        /// <summary>Can be entered, perhaps after breaking through.</summary>
        public bool Passable(int x, int y) => Inside(x, y) && Cost[y * Width + x] > 0f;

        /// <summary>
        /// Works out every cell's distance to the nearest of <paramref name="targets"/>
        /// (cell indexes). Each target has a head start (<paramref name="startCosts"/>,
        /// same order): a bigger number makes it less attractive, so zombies go for
        /// people before they bother with an empty building.
        /// </summary>
        public void Compute(IReadOnlyList<int> targets, IReadOnlyList<float> startCosts)
        {
            for (int i = 0; i < distance.Length; i++) distance[i] = Unreachable;
            heapCount = 0;
            for (int t = 0; t < targets.Count; t++)
            {
                int cell = targets[t];
                if (cell < 0 || cell >= distance.Length || Cost[cell] <= 0f) continue;
                float start = startCosts != null && t < startCosts.Count ? startCosts[t] : 0f;
                if (start >= distance[cell]) continue;
                distance[cell] = start;
                Push(cell, start);
            }

            while (heapCount > 0)
            {
                Pop(out int u, out float d);
                if (d > distance[u]) continue;           // an old, longer entry
                int ux = u % Width, uy = u / Width;
                // A zombie standing next to u pays u's cost to step into it.
                float enter = Cost[u];
                for (int k = 0; k < 8; k++)
                {
                    int vx = ux + StepX[k], vy = uy + StepY[k];
                    if (!Inside(vx, vy)) continue;
                    int v = vy * Width + vx;
                    if (Cost[v] <= 0f) continue;
                    bool diagonal = k >= 4;
                    if (diagonal && !DiagonalOk(vx, vy, ux, uy)) continue;
                    float nd = d + enter * (diagonal ? Diagonal : 1f);
                    if (nd < distance[v])
                    {
                        distance[v] = nd;
                        Push(v, nd);
                    }
                }
            }
        }

        /// <summary>
        /// The cell a zombie standing in (<paramref name="x"/>, <paramref name="y"/>) should
        /// step into next. False if it's already at a target or there's no way anywhere.
        /// The cell may need breaking through first (its <see cref="Cost"/> is more than 1).
        /// </summary>
        public bool Next(int x, int y, out int nextX, out int nextY)
        {
            nextX = x;
            nextY = y;
            float best = Inside(x, y) ? distance[y * Width + x] : Unreachable;
            best = float.IsPositiveInfinity(best) ? best : best + 1e-3f;
            bool found = false;
            for (int k = 0; k < 8; k++)
            {
                int vx = x + StepX[k], vy = y + StepY[k];
                if (!Passable(vx, vy)) continue;
                bool diagonal = k >= 4;
                if (diagonal && !DiagonalOk(x, y, vx, vy)) continue;
                int v = vy * Width + vx;
                float via = distance[v] + Cost[v] * (diagonal ? Diagonal : 1f);
                if (via < best)
                {
                    best = via;
                    nextX = vx;
                    nextY = vy;
                    found = true;
                }
            }
            return found;
        }

        // A diagonal step only onto open ground, and never squeezing past a corner:
        // both cells beside the step must be open too.
        bool DiagonalOk(int fromX, int fromY, int toX, int toY) =>
            Open(toX, toY) && Open(toX, fromY) && Open(fromX, toY);

        void Push(int cell, float key)
        {
            if (heapCount == heapKey.Length)
            {
                Array.Resize(ref heapKey, heapKey.Length * 2);
                Array.Resize(ref heapCell, heapCell.Length * 2);
            }
            int i = heapCount++;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heapKey[parent] <= key) break;
                heapKey[i] = heapKey[parent];
                heapCell[i] = heapCell[parent];
                i = parent;
            }
            heapKey[i] = key;
            heapCell[i] = cell;
        }

        void Pop(out int cell, out float key)
        {
            cell = heapCell[0];
            key = heapKey[0];
            heapCount--;
            if (heapCount == 0) return;
            float lastKey = heapKey[heapCount];
            int lastCell = heapCell[heapCount];
            int i = 0;
            while (true)
            {
                int child = i * 2 + 1;
                if (child >= heapCount) break;
                if (child + 1 < heapCount && heapKey[child + 1] < heapKey[child]) child++;
                if (heapKey[child] >= lastKey) break;
                heapKey[i] = heapKey[child];
                heapCell[i] = heapCell[child];
                i = child;
            }
            heapKey[i] = lastKey;
            heapCell[i] = lastCell;
        }
    }
}
