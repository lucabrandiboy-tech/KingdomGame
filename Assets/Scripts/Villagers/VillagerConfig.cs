using UnityEngine;

namespace KingdomGame.Villagers
{
    /// <summary>How builder villagers move and work.</summary>
    [CreateAssetMenu(menuName = "KingdomGame/Villagers/Villager Config", fileName = "VillagerConfig")]
    public sealed class VillagerConfig : ScriptableObject
    {
        [Tooltip("Walking speed, in tiles per second.")]
        [Min(0.1f)] public float moveSpeed = 2.4f;
        [Tooltip("How much they carry per trip.")]
        [Min(1)] public int carryAmount = 20;
        [Tooltip("Seconds of building work done per second (1 = normal).")]
        [Min(0.1f)] public float workRate = 1f;
        [Tooltip("How often an idle villager looks for work, in seconds.")]
        [Min(0.05f)] public float thinkSeconds = 0.5f;
        [Tooltip("Steps per second of the walking animation.")]
        [Min(0.1f)] public float stepsPerSecond = 7f;

        [Header("Danger")]
        [Min(1)] public float maxHealth = 60f;
        [Tooltip("Villagers drop their work and run when they see a zombie this close (tiles).")]
        [Min(0)] public float fleeDistance = 6f;

        [Header("Needs: food and sleep")]
        [Tooltip("Off: villagers never get hungry or tired.")]
        public bool needs = true;
        [Tooltip("Game hours from well fed to starving (a day is 24).")]
        [Min(1)] public float hungerHours = 24f;
        [Tooltip("Food one meal takes from storage.")]
        [Min(1)] public int mealFood = 3;
        [Tooltip("Food a meal takes when the kingdom has a kitchen (a cauldron, hearth, oven or stove): cooked food goes further.")]
        [Min(1)] public int cookedMealFood = 2;
        [Tooltip("Seconds a meal takes to eat.")]
        [Min(0)] public float eatSeconds = 2.5f;
        [Tooltip("Game hours awake from rested to exhausted.")]
        [Min(1)] public float awakeHours = 30f;
        [Tooltip("Game hours of sleep in a bed from exhausted to rested.")]
        [Min(0.5f)] public float bedSleepHours = 6f;
        [Tooltip("Game hours of sleep on the ground from exhausted to rested (as far as Ground Rest Cap).")]
        [Min(0.5f)] public float groundSleepHours = 12f;
        [Tooltip("Sleeping on the ground rests them only this far (1 = fully): they wake up tired and are slow by the evening.")]
        [Range(0.1f, 1f)] public float groundRestCap = 0.6f;
        [Tooltip("How fast hungry or tired villagers work and walk (1 = normal).")]
        [Range(0.1f, 1f)] public float tiredSpeed = 0.75f;
        [Tooltip("How fast starving or exhausted villagers work and walk.")]
        [Range(0.1f, 1f)] public float exhaustedSpeed = 0.5f;
        [Tooltip("How much faster villagers work when they're fed and rested and the kingdom has a dining hall (0.1 = 10%).")]
        [Range(0f, 1f)] public float contentBonus = 0.1f;
        [Tooltip("Hit points a villager gets back each game hour asleep in a bed.")]
        [Min(0)] public float healPerHourInBed = 20f;

        [Header("Hunting (hunters)")]
        [Tooltip("What hunters shoot with (their bow). None: hunters can't hunt.")]
        public Combat.WeaponConfig huntWeapon;
        [Tooltip("They shoot from this close (tiles); deer take fright at the Wildlife Config's Deer Shy Distance.")]
        [Min(1)] public float huntRange = 8.5f;
        [Tooltip("Seconds between shots.")]
        [Min(0.2f)] public float huntShotSeconds = 1.6f;
        [Tooltip("They look for animals this far away (tiles), within the kingdom's lands.")]
        [Min(5)] public float huntDistance = 80f;
        [Tooltip("They give up on an animal after this many seconds and look for another.")]
        [Min(5)] public float huntSeconds = 60f;
    }
}
