using KingdomGame.Art;
using UnityEngine;

namespace KingdomGame.Character
{
    /// <summary>
    /// How the ruler looks from above: the body turns to face where you aim (the
    /// gun turns with it), plays walking frames while moving, and has a soft
    /// shadow on the ground that turns with it.
    /// </summary>
    public sealed class RulerLook : MonoBehaviour
    {
        Transform body;
        SpriteRenderer bodyRenderer, gunRenderer, shadow;
        Sprite[] frames, shadows;
        float stepClock;

        /// <summary>The turning part (body and gun). The ruler's own transform never turns.</summary>
        public Transform Body => body;

        /// <summary>Makes the body, gun and shadow under the ruler's transform and returns the gun's transform.</summary>
        public Transform Build()
        {
            var images = CharacterArt.RulerFrames();
            var atlas = new SpriteAtlasBuilder();
            for (int i = 0; i < images.Length; i++) atlas.Add(images[i], $"Ruler {i}");
            for (int i = 0; i < images.Length; i++) atlas.Add(images[i].Shadow(3, 1f), $"Ruler shadow {i}");
            int gunIndex = atlas.Add(CharacterArt.Rifle(), "Rifle");
            var sprites = atlas.Build("Ruler");
            frames = new Sprite[images.Length];
            shadows = new Sprite[images.Length];
            System.Array.Copy(sprites, 0, frames, 0, images.Length);
            System.Array.Copy(sprites, images.Length, shadows, 0, images.Length);

            shadow = Look.AddShadow(transform, shadows[0], 0.12f);

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            bodyRenderer = body.gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = frames[0];
            bodyRenderer.sortingOrder = SortOrder.Characters;

            var gun = new GameObject("Gun").transform;
            gun.SetParent(body, false);
            gun.localPosition = CharacterArt.RulerHand / PixelArt.PixelsPerUnit;
            gunRenderer = gun.gameObject.AddComponent<SpriteRenderer>();
            gunRenderer.sprite = sprites[gunIndex];
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
                frame = Mathf.FloorToInt(stepClock) % frames.Length;
            }
            else
            {
                stepClock = 0f;
            }
            bodyRenderer.sprite = frames[frame];
            shadow.sprite = shadows[frame];

            var turn = Quaternion.Euler(0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
            body.localRotation = turn;
            shadow.transform.localRotation = turn;
            gunRenderer.enabled = holdingGun;
        }

        void OnDestroy()
        {
            if (frames == null || frames.Length == 0 || frames[0] == null) return;
            Destroy(frames[0].texture);
            foreach (var sprite in frames) Destroy(sprite);
            foreach (var sprite in shadows) Destroy(sprite);
        }
    }
}
