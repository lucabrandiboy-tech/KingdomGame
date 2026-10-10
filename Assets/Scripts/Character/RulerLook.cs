using KingdomGame.Art;
using UnityEngine;

namespace KingdomGame.Character
{
    /// <summary>
    /// How the ruler looks from above: the body turns to face where you aim (the
    /// gun turns with it), plays walking frames while moving, and has a soft
    /// shadow on the ground that turns with it. The ruler is drawn facing
    /// <see cref="CharacterArt.RulerDirections"/> directions, each lit from the
    /// top-left; the nearest one is shown and turned the last few degrees.
    /// </summary>
    public sealed class RulerLook : MonoBehaviour
    {
        Transform body;
        SpriteRenderer bodyRenderer, gunRenderer, shadow, fallen;
        Sprite[,] frames;     // [direction, frame]
        Sprite[] shadows;     // [frame]
        Sprite[] dead;        // [direction]
        float stepClock;
        float swing;          // seconds left of a club swing
        const float SwingSeconds = 0.22f;

        /// <summary>The turning part (body and gun). The ruler's own transform never turns.</summary>
        public Transform Body => body;

        /// <summary>Makes the body, gun and shadow under the ruler's transform and returns the gun's transform.</summary>
        public Transform Build()
        {
            frames = new Sprite[CharacterArt.RulerDirections, CharacterArt.WalkFrames];
            shadows = new Sprite[CharacterArt.WalkFrames];
            for (int f = 0; f < CharacterArt.WalkFrames; f++)
            {
                for (int d = 0; d < CharacterArt.RulerDirections; d++) frames[d, f] = ArtSprites.Get(ArtKeys.Ruler(d, f));
                shadows[f] = ArtSprites.Get(ArtKeys.RulerShadow(f));
            }

            shadow = Look.AddShadow(transform, shadows[0], 0.12f);

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            bodyRenderer = body.gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = frames[0, 0];
            bodyRenderer.sortingOrder = SortOrder.Characters;

            var gun = new GameObject("Gun").transform;
            gun.SetParent(body, false);
            gun.localPosition = CharacterArt.RulerHand;
            gunRenderer = gun.gameObject.AddComponent<SpriteRenderer>();
            gunRenderer.sprite = ArtSprites.Get(ArtKeys.Rifle);
            gunRenderer.sortingOrder = SortOrder.Characters + 1;

            dead = new Sprite[CharacterArt.DeadDirections];
            for (int d = 0; d < dead.Length; d++) dead[d] = ArtSprites.Get(ArtKeys.RulerDead(d));
            var lying = new GameObject("Fallen");
            lying.transform.SetParent(transform, false);
            fallen = lying.AddComponent<SpriteRenderer>();
            fallen.sortingOrder = SortOrder.Corpses + 1;
            fallen.enabled = false;
            return gun;
        }

        /// <summary>The weapon in hand (null: the rifle's picture stays).</summary>
        public void SetWeapon(Sprite sprite)
        {
            if (gunRenderer != null && sprite != null) gunRenderer.sprite = sprite;
        }

        /// <summary>A swing of the club: the weapon sweeps across in front.</summary>
        public void Swing() => swing = SwingSeconds;

        /// <summary>The ruler has fallen: lying on their back, head away from <paramref name="facingDegrees"/>.</summary>
        public void ShowDead(float facingDegrees)
        {
            if (fallen == null) return;
            float a = facingDegrees + 180f;
            fallen.sprite = dead[Direction(a, dead.Length)];
            fallen.transform.localRotation = Quaternion.Euler(0f, 0f, a);
            fallen.enabled = true;
            bodyRenderer.enabled = gunRenderer.enabled = shadow.enabled = false;
        }

        /// <summary>Up again (respawned).</summary>
        public void ShowAlive()
        {
            if (fallen == null) return;
            fallen.enabled = false;
            bodyRenderer.enabled = shadow.enabled = true;
        }

        /// <summary>
        /// Up on a wall (true) or on the ground: up there the ruler is a little closer to the
        /// camera (drawn a bit bigger) and their shadow falls further away.
        /// </summary>
        public void SetRaised(bool raised)
        {
            transform.localScale = Vector3.one * (raised ? 1.07f : 1f);
            if (shadow != null) shadow.transform.localPosition = (Vector3)(Look.ShadowDirection * (raised ? 0.36f : 0.12f));
        }

        /// <summary>Which way the body faces, in degrees.</summary>
        public float FacingDegrees => body != null ? body.localEulerAngles.z : 0f;

        /// <summary>Called every frame by the controller. <paramref name="facing"/> is a direction.</summary>
        public void Show(Vector2 velocity, Vector2 facing, float maxSpeed, float stepsPerSecond, bool holdingGun = true)
        {
            if (frames == null) return;
            float speed = velocity.magnitude;
            int frame = 0;
            if (speed > 0.15f)
            {
                stepClock += Time.deltaTime * stepsPerSecond * Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeed));
                frame = Mathf.FloorToInt(stepClock) % CharacterArt.WalkFrames;
            }
            else
            {
                stepClock = 0f;
            }

            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            bodyRenderer.sprite = frames[Direction(angle, CharacterArt.RulerDirections), frame];
            shadow.sprite = shadows[frame];
            var turn = Quaternion.Euler(0f, 0f, angle);
            body.localRotation = turn;
            shadow.transform.localRotation = turn;
            gunRenderer.enabled = holdingGun;
            // A club swing sweeps from one side to the other.
            if (swing > 0f)
            {
                swing -= Time.deltaTime;
                float t = 1f - Mathf.Clamp01(swing / SwingSeconds);
                gunRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(65f, -65f, t));
            }
            else gunRenderer.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Which of <paramref name="directions"/> drawings is nearest to <paramref name="degrees"/>.</summary>
        public static int Direction(float degrees, int directions)
        {
            float step = 360f / directions;
            int d = Mathf.RoundToInt(degrees / step) % directions;
            return d < 0 ? d + directions : d;
        }
    }
}
