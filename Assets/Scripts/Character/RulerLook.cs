using KingdomGame.Art;
using UnityEngine;

namespace KingdomGame.Character
{
    /// <summary>
    /// How the ruler looks: walking frames while moving, facing the side you aim
    /// at, drawn in the right order with trees around it, and the gun in front
    /// of or behind the body depending on where you aim.
    /// </summary>
    public sealed class RulerLook : MonoBehaviour
    {
        SpriteRenderer body;
        SpriteRenderer gun;
        Sprite[] frames;
        float stepClock;

        public void Setup(SpriteRenderer bodyRenderer, SpriteRenderer gunRenderer)
        {
            body = bodyRenderer;
            gun = gunRenderer;
            var images = CharacterArt.RulerFrames();
            frames = new Sprite[images.Length];
            for (int i = 0; i < images.Length; i++)
                frames[i] = PixelTextures.ToSprite(images[i], $"Ruler {i}");
            body.sprite = frames[0];
        }

        /// <summary>Called every frame by the controller.</summary>
        public void Show(Vector2 velocity, Vector2 aim, float maxSpeed, float stepsPerSecond)
        {
            if (frames == null) return;
            float speed = velocity.magnitude;
            if (speed > 0.15f)
            {
                stepClock += Time.deltaTime * stepsPerSecond * Mathf.Clamp01(speed / Mathf.Max(0.01f, maxSpeed));
                body.sprite = frames[Mathf.FloorToInt(stepClock) % frames.Length];
            }
            else
            {
                stepClock = 0f;
                body.sprite = frames[0];
            }

            body.flipX = aim.x < -0.05f;
            int order = YSort.Order(transform.position.y);
            body.sortingOrder = order;
            if (gun != null) gun.sortingOrder = aim.y > 0.35f ? order - 1 : order + 1;
        }

        void OnDestroy()
        {
            if (frames == null) return;
            foreach (var sprite in frames)
            {
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }
    }
}
