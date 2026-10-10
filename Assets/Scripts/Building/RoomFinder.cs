using System.Collections.Generic;

namespace KingdomGame.Building
{
    /// <summary>
    /// Finds the rooms on the map: areas closed off all round by walls (doors
    /// count as walls), so they can't be reached from the edge of the map
    /// without going through one. Steps are up, down, left and right only, so
    /// walls that touch at a corner still close a room off; that matches the
    /// path finding, where nobody squeezes between two corners.
    ///
    /// Plain C# with no Unity code, so it's unit-tested (tools/tests).
    /// </summary>
    public static class RoomFinder
    {
        /// <summary>
        /// Returns each room as a list of cell indexes (y * width + x).
        /// <paramref name="walls"/>[i] is true for walls and doors. Enclosed areas
        /// bigger than <paramref name="maxCells"/> count as open ground (a whole
        /// walled town isn't one room).
        /// </summary>
        public static List<List<int>> Find(bool[] walls, int width, int height, int maxCells)
        {
            var rooms = new List<List<int>>();
            var seen = new bool[width * height];
            var queue = new Queue<int>();

            // Everything that can be reached from the edge of the map is outside.
            for (int x = 0; x < width; x++)
            {
                Seed(x, 0);
                Seed(x, height - 1);
            }
            for (int y = 0; y < height; y++)
            {
                Seed(0, y);
                Seed(width - 1, y);
            }
            Spread(null);

            // What's left (and isn't wall) is closed off: each connected part is a room.
            for (int i = 0; i < walls.Length; i++)
            {
                if (seen[i] || walls[i]) continue;
                var room = new List<int>();
                seen[i] = true;
                queue.Enqueue(i);
                Spread(room);
                if (room.Count <= maxCells) rooms.Add(room);
            }
            return rooms;

            void Seed(int x, int y)
            {
                int i = y * width + x;
                if (walls[i] || seen[i]) return;
                seen[i] = true;
                queue.Enqueue(i);
            }

            void Spread(List<int> cells)
            {
                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    cells?.Add(i);
                    int x = i % width, y = i / width;
                    if (x > 0) Visit(i - 1);
                    if (x < width - 1) Visit(i + 1);
                    if (y > 0) Visit(i - width);
                    if (y < height - 1) Visit(i + width);
                }
            }

            void Visit(int i)
            {
                if (walls[i] || seen[i]) return;
                seen[i] = true;
                queue.Enqueue(i);
            }
        }
    }
}
