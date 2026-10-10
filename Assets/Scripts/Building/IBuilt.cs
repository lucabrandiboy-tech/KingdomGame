using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>Something finished that the kingdom built: a floor tile, a door, furniture, a storehouse or chest.</summary>
    public interface IBuilt
    {
        BuildingConfig Config { get; }
        /// <summary>The grid cells it covers.</summary>
        RectInt Cells { get; }
        /// <summary>Which way it faces, 0..3 (quarter turns anticlockwise).</summary>
        int Rotation { get; }
        /// <summary>Takes it away at once (torn down or broken).</summary>
        void Remove();
    }

    /// <summary>Turning offsets with a building (quarter turns anticlockwise, like its picture).</summary>
    public static class Facing
    {
        public static Vector2 Turn(Vector2 offset, int rotation)
        {
            switch (((rotation % 4) + 4) % 4)
            {
                case 1: return new Vector2(-offset.y, offset.x);
                case 2: return new Vector2(-offset.x, -offset.y);
                case 3: return new Vector2(offset.y, -offset.x);
                default: return offset;
            }
        }
    }
}
