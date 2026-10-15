using System;
using KingdomGame.Villagers;

// Tests for villagers' needs: hunger, tiredness, when they sleep and how it slows them.
static partial class Tests
{
    static void HungerEmptiesOverADay()
    {
        float fed = 1f;
        for (int hour = 0; hour < 12; hour++) fed = NeedsMath.Hunger(fed, 1f, 24f);
        Check(Math.Abs(fed - 0.5f) < 1e-4f, $"half a day should leave half fed, got {fed}");
        fed = NeedsMath.Hunger(fed, 100f, 24f);
        Check(fed == 0f, "never below starving");
        // A meal from the eating mark fills them up.
        Check(NeedsMath.EatBelow + 1f >= 1f, "a whole meal from the eating mark fills up");
    }

    static float Rest(float rested, float hours, bool asleep, bool inBed) => NeedsMath.Rest(rested, hours, asleep, inBed, 30f, 6f, 12f, 0.6f);

    static void BedsRestFasterThanTheGround()
    {
        float bed = Rest(0f, 3f, true, true);
        float ground = Rest(0f, 3f, true, false);
        Check(Math.Abs(bed - 0.5f) < 1e-4f, $"3 hours in bed is half rested, got {bed}");
        Check(ground < bed, "the ground rests less");
        float awake = Rest(1f, 15f, false, false);
        Check(Math.Abs(awake - 0.5f) < 1e-4f, $"15 hours awake is half tired, got {awake}");
        // A night (10 hours) in bed always ends rested; on the ground only as far as the cap.
        Check(Rest(0.2f, 10f, true, true) >= 1f, "a night in bed rests fully");
        Check(Math.Abs(Rest(0.2f, 10f, true, false) - 0.6f) < 1e-4f, "a night on the ground ends at the cap");
        Check(Math.Abs(Rest(0.8f, 10f, true, false) - 0.8f) < 1e-4f, "sleeping on the ground never makes them less rested");

        // Day after day: on the ground they're tired (slow) by the evening, in a bed never.
        float g = 1f, b = 1f;
        bool groundTired = false, bedTired = false;
        for (int day = 0; day < 10; day++)
        {
            for (int h = 0; h < 14; h++)
            {
                g = Rest(g, 1f, false, false);
                b = Rest(b, 1f, false, false);
                groundTired |= g < NeedsMath.Low;
                bedTired |= b < NeedsMath.Low;
            }
            g = Rest(g, 10f, true, false);
            b = Rest(b, 10f, true, true);
        }
        Check(groundTired, "sleeping on the ground should leave them tired by the evening");
        Check(!bedTired, "sleeping in a bed should never leave them tired");
        Check(g > 0f, "but never exhausted");
    }

    static void HungryAndTiredWorkSlower()
    {
        float normal = NeedsMath.WorkFactor(0.8f, 0.8f, false, 0.75f, 0.5f, 0.1f);
        Check(Math.Abs(normal - 1f) < 1e-5f, "fed and rested: normal speed");
        Check(Math.Abs(NeedsMath.WorkFactor(0.8f, 0.8f, true, 0.75f, 0.5f, 0.1f) - 1.1f) < 1e-5f, "a dining hall: 10% faster");
        Check(Math.Abs(NeedsMath.WorkFactor(0.1f, 0.8f, true, 0.75f, 0.5f, 0.1f) - 0.75f) < 1e-5f, "hungry: slower, no dining hall bonus");
        Check(Math.Abs(NeedsMath.WorkFactor(0f, 0f, true, 0.75f, 0.5f, 0.1f) - 0.25f) < 1e-5f, "starving and exhausted: a quarter");
        // Never faster when worse off.
        float last = 2f;
        for (float f = 1f; f >= 0f; f -= 0.05f)
        {
            float w = NeedsMath.WorkFactor(f, f, true, 0.75f, 0.5f, 0.1f);
            Check(w <= last + 1e-6f, $"pace went up as needs went down at {f}");
            last = w;
        }
    }

    static void VillagersSleepAtNightGuardsByDay()
    {
        Check(NeedsMath.ShouldSleep(22f, true, 0.6f, false), "villagers sleep at night");
        Check(!NeedsMath.ShouldSleep(14f, false, 0.6f, false), "not in the day");
        Check(!NeedsMath.ShouldSleep(22f, true, 0.6f, true), "guards keep watch at night");
        Check(NeedsMath.ShouldSleep(13f, false, 0.6f, true), "guards sleep by day");
        Check(!NeedsMath.ShouldSleep(13f, false, 0.9f, true), "a rested guard stays up");
        Check(NeedsMath.ShouldSleep(14f, false, 0f, false), "the exhausted nap any time");

        // A whole night asleep: they stay asleep, even rested, until it's day.
        Check(NeedsMath.StayAsleep(3f, true, 1f, false, false), "the night through");
        Check(!NeedsMath.StayAsleep(7f, false, 0.7f, false, false), "up in the morning");
        Check(NeedsMath.StayAsleep(14f, false, 0.3f, false, true), "a nap until rested enough");
        Check(!NeedsMath.StayAsleep(14f, false, NeedsMath.NapUntil, false, true), "then up");
        Check(!NeedsMath.StayAsleep(13f, false, 1f, true, false), "a rested guard gets up");
        Check(!NeedsMath.StayAsleep(18f, false, 0.6f, true, false), "guards' sleep ends in the evening");
    }

    static void KitchenMealsTakeLessFood()
    {
        Check(NeedsMath.MealFood(3, 2, false) == 3, "raw meal");
        Check(NeedsMath.MealFood(3, 2, true) == 2, "cooked meal");
        Check(NeedsMath.MealFood(3, 5, true) == 3, "a kitchen never makes meals dearer");
        Check(NeedsMath.MealFood(0, 0, true) == 1, "a meal takes some food");
    }
}
