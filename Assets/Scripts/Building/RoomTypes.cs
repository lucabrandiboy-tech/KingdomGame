namespace KingdomGame.Building
{
    /// <summary>What a room is, from what's in it (see <see cref="RoomTypes.Of"/>). Some do something for the kingdom.</summary>
    public enum RoomType
    {
        None,
        Empty,
        Small,
        Throne,
        Infirmary,
        Barracks,
        Cottage,
        Bedroom,
        DiningHall,
        GreatHall,
        Armory,
        Training,
        Library,
        Kitchen,
        Workshop,
        Storeroom,
    }

    /// <summary>
    /// The kinds of room: how each is made (what furniture makes a walled-in room with a door one), what it does,
    /// and its name. The first rule that fits wins, in the order of <see cref="Guide"/>.
    /// </summary>
    public static class RoomTypes
    {
        /// <summary>The kinds of room as the rooms guide (I) lists them, in the order they're worked out.</summary>
        public static readonly RoomType[] Guide =
        {
            RoomType.Throne, RoomType.Infirmary, RoomType.Barracks, RoomType.Cottage, RoomType.Bedroom, RoomType.GreatHall, RoomType.DiningHall,
            RoomType.Armory, RoomType.Training, RoomType.Library, RoomType.Kitchen, RoomType.Workshop, RoomType.Storeroom,
        };

        /// <summary>
        /// What a room is: its size, whether the throne is in it, and how many of each kind of furniture
        /// (<paramref name="count"/> gives the number with a <see cref="RoomTag"/>).
        /// </summary>
        public static RoomType Of(int size, bool throne, System.Func<RoomTag, int> count)
        {
            if (throne) return RoomType.Throne;
            if (count(RoomTag.Medicine) > 0 && count(RoomTag.Bed) > 0) return RoomType.Infirmary;
            if (count(RoomTag.Bed) >= 3) return RoomType.Barracks;
            if (count(RoomTag.Bed) > 0 && (count(RoomTag.Cooking) > 0 || count(RoomTag.Table) > 0) && size <= 30) return RoomType.Cottage;
            if (count(RoomTag.Bed) > 0) return RoomType.Bedroom;
            if (count(RoomTag.Table) > 0 && count(RoomTag.Seat) > 0) return size >= 24 ? RoomType.GreatHall : RoomType.DiningHall;
            if (count(RoomTag.Weapons) > 0) return RoomType.Armory;
            if (count(RoomTag.Training) > 0) return RoomType.Training;
            if (count(RoomTag.Books) > 0) return RoomType.Library;
            if (count(RoomTag.Cooking) > 0) return RoomType.Kitchen;
            if (count(RoomTag.Workshop) > 0) return RoomType.Workshop;
            if (count(RoomTag.Storage) > 0) return RoomType.Storeroom;
            return size <= 4 ? RoomType.Small : RoomType.Empty;
        }

        public static string Name(RoomType t)
        {
            switch (t)
            {
                case RoomType.Throne: return "Throne room";
                case RoomType.Infirmary: return "Infirmary";
                case RoomType.Barracks: return "Barracks";
                case RoomType.Cottage: return "Cottage";
                case RoomType.Bedroom: return "Bedroom";
                case RoomType.DiningHall: return "Dining hall";
                case RoomType.GreatHall: return "Great hall";
                case RoomType.Armory: return "Armory";
                case RoomType.Training: return "Training room";
                case RoomType.Library: return "Library";
                case RoomType.Kitchen: return "Kitchen";
                case RoomType.Workshop: return "Workshop";
                case RoomType.Storeroom: return "Storeroom";
                case RoomType.Small: return "Small room";
                default: return "Empty room";
            }
        }

        /// <summary>What makes one (walls and a door all round, and...).</summary>
        public static string How(RoomType t)
        {
            switch (t)
            {
                case RoomType.Throne: return "the throne";
                case RoomType.Infirmary: return "a medicine table and a bed";
                case RoomType.Barracks: return "3 or more beds";
                case RoomType.Cottage: return "a bed and a table or a cooking pot, in a small room (30 tiles or less)";
                case RoomType.Bedroom: return "a bed";
                case RoomType.DiningHall: return "a table and a chair or bench (the cafeteria)";
                case RoomType.GreatHall: return "a table and a seat, in a big room (24 tiles or more)";
                case RoomType.Armory: return "a weapon rack or an armor stand";
                case RoomType.Training: return "a training dummy (no weapon racks: they make it an armory)";
                case RoomType.Library: return "a bookshelf";
                case RoomType.Kitchen: return "a cauldron, hearth, oven or stove";
                case RoomType.Workshop: return "an anvil or a crafting table";
                case RoomType.Storeroom: return "chests, barrels or crates";
                default: return "";
            }
        }

        /// <summary>What it does for the kingdom (with the numbers from its settings, when given).</summary>
        public static string Does(RoomType t, KingdomConfig k = null, float diningBonus = 0.1f)
        {
            string Percent(float f) => $"{UnityEngine.Mathf.RoundToInt(f * 100f)}%";
            switch (t)
            {
                case RoomType.Throne: return "where you rule from.";
                case RoomType.Infirmary:
                    return $"villagers and soldiers hurt below {Percent(k != null ? k.recoverBelow : 0.6f)} of their health go to its beds " +
                           $"and get back {(k != null ? k.infirmaryHealPerSecond : 4f):0.#} health a second.";
                case RoomType.Barracks:
                    return $"soldiers asleep in its beds heal {(k != null ? k.barracksHealing : 2f):0.#} times as fast. Each bed is a home for more villagers.";
                case RoomType.Cottage: case RoomType.Bedroom: return "each bed is a home for more villagers.";
                case RoomType.DiningHall: case RoomType.GreatHall: return $"well fed, rested villagers work {Percent(diningBonus)} faster.";
                case RoomType.Armory: return "E at its weapon rack or armor stand: buy armor for you and your soldiers, and arm your soldiers.";
                case RoomType.Training: return "soldiers with nothing to fight train at its dummies and go up in rank.";
                case RoomType.Library: return $"research goes {Percent(k != null ? k.libraryResearch : 0.1f)} faster.";
                case RoomType.Kitchen: return "cooked meals: each meal takes less food.";
                case RoomType.Workshop: return $"crafters work {Percent(k != null ? k.workshopCrafting : 0.15f)} faster.";
                case RoomType.Storeroom: return "somewhere tidy to keep things.";
                default: return "";
            }
        }
    }
}
