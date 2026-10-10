namespace KingdomGame
{
    /// <summary>
    /// Drawing order (SpriteRenderer.sortingOrder) of everything, seen from
    /// straight above: higher numbers are drawn on top. Nothing is sorted by
    /// height on screen any more; tree leaves are simply above everyone, and
    /// fade out when the ruler walks under them.
    /// </summary>
    public static class SortOrder
    {
        public const int Ground = -30000;
        public const int GroundTint = -29990;
        /// <summary>Wooden and stone floors you build.</summary>
        public const int Floors = -1000;
        /// <summary>Rugs, on top of floors.</summary>
        public const int Rugs = -900;
        /// <summary>Blood splatters.</summary>
        public const int Blood = -800;
        /// <summary>Fallen zombies and villagers.</summary>
        public const int Corpses = -700;
        public const int Rubble = -200;
        /// <summary>Shadows of walls, trees, people and flying cubes.</summary>
        public const int Shadows = -100;
        public const int Walls = 0;
        public const int Doors = 50;
        /// <summary>Rocks, bushes, tree trunks and stumps.</summary>
        public const int Nature = 100;
        /// <summary>Tables, beds, shelves (their flames are one higher).</summary>
        public const int Furniture = 150;
        public const int Buildings = 200;
        /// <summary>Bags of loot on the ground.</summary>
        public const int Loot = 250;
        public const int Blueprints = 300;
        /// <summary>The ruler and villagers; what they hold is one higher.</summary>
        public const int Characters = 1000;
        public const int Charges = 1500;
        public const int Debris = 2000;
        /// <summary>Roofs: over the people inside, under shots flying past (they fade when the ruler goes in).</summary>
        public const int Roofs = 2400;
        public const int Projectiles = 2500;
        public const int Sparks = 2600;
        public const int Smoke = 4000;
        public const int Canopies = 5000;
        /// <summary>Placement previews and progress bars (throne view).</summary>
        public const int Markers = 6000;
    }
}
