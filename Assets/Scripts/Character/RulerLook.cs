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
        SpriteRenderer bodyRenderer, gunRenderer, shadow;
        Sprite[,] frames;     // [direction, frame]
        Sprite[] shadows;     // [frame]
        float stepClock;

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
            return gun;
        }

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
