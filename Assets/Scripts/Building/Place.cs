using KingdomGame.Villagers;
using UnityEngine;

namespace KingdomGame.Building
{
    /// <summary>
    /// A place you go into through a door on the map: a house's inside or a mine. It lives
    /// far off the map (each has a square of its own), has its own walking paths, and its
    /// own floor to build on. People and villagers go in and out by its door; zombies go
    /// for its building on the map when someone is hiding in it.
    /// </summary>
    public abstract class Place : MonoBehaviour
    {
        /// <summary>Like "Hut" or "Mine".</summary>
        public abstract string Name { get; }
        /// <summary>Every cell of it (floor, walls, rock).</summary>
        public abstract RectInt Bounds { get; }
        /// <summary>Where walking paths are found in it.</summary>
        public abstract NavGrid Nav { get; }
        /// <summary>The door or ladder inside: E next to it goes back out.</summary>
        public abstract Vector2Int ExitCell { get; }
        /// <summary>The cell you stand on when you've just come in (kept clear).</summary>
        public abstract Vector2Int EntranceCell { get; }
        public Vector2 Entrance => new Vector2(EntranceCell.x + 0.5f, EntranceCell.y + 0.45f);
        /// <summary>Where you stand on the map when you've just come out.</summary>
        public abstract Vector2 Outside { get; }
        /// <summary>The map cell in front of its door (it must stay clear).</summary>
        public abstract Vector2Int OutsideCell { get; }
        /// <summary>Where on the map E takes you in.</summary>
        public abstract Vector2 DoorPoint { get; }
        /// <summary>Its building on the map (zombies break it down to get at whoever is inside).</summary>
        public abstract IBuilt Building { get; }
        public abstract Health BuildingHealth { get; }
        public virtual Vector2 Center => Bounds.center;

        public bool Contains(Vector2Int cell) => Bounds.Contains(cell);

        /// <summary>Whether something can be built on this cell (and if not, why: <see cref="CantBuildReason"/>).</summary>
        public abstract bool CanBuildAt(Vector2Int cell);
        public virtual string CantBuildReason(Vector2Int cell) => "That's the wall";
    }
}
