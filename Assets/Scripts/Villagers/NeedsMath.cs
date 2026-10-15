using System;

namespace KingdomGame.Villagers
{
    /// <summary>
    /// How hungry and tired villagers get, when they sleep, and how much it slows their work. Both needs go from 1
    /// (well fed, rested) down to 0 (starving, exhausted). Plain C# with no Unity code, so it's unit-tested (tools/tests).
    /// </summary>
    public static class NeedsMath
    {
        /// <summary>A villager goes to eat when the next job is done, below this.</summary>
        public const float EatBelow = 0.4f;
        /// <summary>Below this they drop what they're doing to eat (if there's food).</summary>
        public const float Urgent = 0.15f;
        /// <summary>Below this they're hungry (or tired): slower.</summary>
        public const float Low = 0.25f;
        /// <summary>At least this fed and rested (with a dining hall): content, a little faster.</summary>
        public const float Content = 0.5f;
        /// <summary>Guards sleep by day, between these hours (they keep watch at night).</summary>
        public const float GuardBedtime = 11f, GuardWakeup = 17f;
        /// <summary>Exhausted in the day: a nap until rested this much.</summary>
        public const float NapUntil = 0.5f;

        /// <summary>How fed a villager is after <paramref name="hours"/> game hours (empty after <paramref name="hungerHours"/>).</summary>
        public static float Hunger(float fed, float hours, float hungerHours) =>
            Clamp01(fed - hours / Math.Max(0.01f, hungerHours));

        /// <summary>
        /// How rested a villager is after <paramref name="hours"/> game hours: awake it wears off (exhausted after
        /// <paramref name="awakeHours"/>); asleep it comes back, fully after <paramref name="bedHours"/> in a bed. On the
        /// ground it comes back at the pace of <paramref name="groundHours"/> and only up to <paramref name="groundCap"/>:
        /// they wake up tired, and are slow by the evening.
        /// </summary>
        public static float Rest(float rested, float hours, bool asleep, bool inBed, float awakeHours, float bedHours, float groundHours, float groundCap)
        {
            if (!asleep) return Clamp01(rested - hours / Math.Max(0.01f, awakeHours));
            if (inBed) return Clamp01(rested + hours / Math.Max(0.01f, bedHours));
            float cap = Clamp01(groundCap);
            if (rested >= cap) return rested;
            return Math.Min(cap, rested + hours / Math.Max(0.01f, groundHours));
        }

        /// <summary>How fast a villager works (and walks): slower hungry or tired, slower still starving or exhausted, a little faster content.</summary>
        public static float WorkFactor(float fed, float rested, bool diningHall, float slow, float verySlow, float contentBonus)
        {
            float f = 1f;
            f *= fed <= 0f ? verySlow : fed < Low ? slow : 1f;
            f *= rested <= 0f ? verySlow : rested < Low ? slow : 1f;
            if (diningHall && fed >= Content && rested >= Content) f *= 1f + contentBonus;
            return f;
        }

        /// <summary>Whether it's a guard's time to sleep (the middle of the day).</summary>
        public static bool GuardSleepTime(float hour) => hour >= GuardBedtime && hour < GuardWakeup;

        /// <summary>
        /// Whether an awake villager should go to sleep now: everyone at night but guards, who sleep in the middle of
        /// the day if they're at all tired; anyone exhausted, any time (a nap).
        /// </summary>
        public static bool ShouldSleep(float hour, bool night, float rested, bool guard)
        {
            if (rested <= 0.02f) return true;
            return guard ? GuardSleepTime(hour) && rested < 0.8f : night;
        }

        /// <summary>
        /// Whether a sleeping villager stays asleep: the whole night (guards: the middle of the day, until rested);
        /// outside that time only a nap, until rested enough (<see cref="NapUntil"/>).
        /// </summary>
        public static bool StayAsleep(float hour, bool night, float rested, bool guard, bool napping)
        {
            bool sleepTime = guard ? GuardSleepTime(hour) : night;
            if (sleepTime) return !guard || rested < 1f;
            return napping && rested < NapUntil;
        }

        /// <summary>Food a meal takes: less with a kitchen (cooked food goes further).</summary>
        public static int MealFood(int raw, int cooked, bool kitchen) => Math.Max(1, kitchen ? Math.Min(raw, cooked) : raw);

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
