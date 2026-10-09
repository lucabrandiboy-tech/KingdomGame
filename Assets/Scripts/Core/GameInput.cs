using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KingdomGame
{
    /// <summary>
    /// Keyboard and mouse, in one place. Uses the Input System
    /// package when the project has it switched on, otherwise Unity's old
    /// Input Manager, so it compiles either way. Key rebinding and gamepads
    /// come later.
    /// </summary>
    public static class GameInput
    {
        /// <summary>WASD or arrow keys, length 0..1.</summary>
        public static Vector2 Move
        {
            get
            {
                var move = new Vector2(
                    (Held(KeyCode.D) || Held(KeyCode.RightArrow) ? 1 : 0) - (Held(KeyCode.A) || Held(KeyCode.LeftArrow) ? 1 : 0),
                    (Held(KeyCode.W) || Held(KeyCode.UpArrow) ? 1 : 0) - (Held(KeyCode.S) || Held(KeyCode.DownArrow) ? 1 : 0));
                return move.sqrMagnitude > 1f ? move.normalized : move;
            }
        }

        /// <summary>Mouse position in screen pixels.</summary>
        public static Vector2 PointerPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                return Vector2.zero;
#endif
            }
        }

        public static bool FireHeld => MouseHeld(0);
        public static bool FirePressed => MousePressed(0);
        public static bool FireReleased => MouseReleased(0);
        public static bool AltFirePressed => MousePressed(1);

        /// <summary>Mouse wheel this frame: +1 per click away from you (zoom in), -1 towards you.</summary>
        public static float Scroll
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                // The Input System reports 120 per wheel click on Windows; only the direction matters here.
                float y = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
                return y > 0.01f ? 1f : y < -0.01f ? -1f : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
                float y = Input.mouseScrollDelta.y;
                return y > 0.01f ? 1f : y < -0.01f ? -1f : 0f;
#else
                return 0f;
#endif
            }
        }

        public static bool Held(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var control = Find(key);
            return control != null && control.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(key);
#else
            return false;
#endif
        }

        public static bool Pressed(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var control = Find(key);
            return control != null && control.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#else
            return false;
#endif
        }

        static bool MouseHeld(int button)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            return (button == 0 ? mouse.leftButton : mouse.rightButton).isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(button);
#else
            return false;
#endif
        }

        static bool MouseReleased(int button)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            return (button == 0 ? mouse.leftButton : mouse.rightButton).wasReleasedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(button);
#else
            return false;
#endif
        }

        static bool MousePressed(int button)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return false;
            return (button == 0 ? mouse.leftButton : mouse.rightButton).wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        // The keys the game uses, translated to the Input System's keys.
        static UnityEngine.InputSystem.Controls.KeyControl Find(KeyCode key)
        {
            var k = Keyboard.current;
            if (k == null) return null;
            switch (key)
            {
                case KeyCode.W: return k.wKey;
                case KeyCode.A: return k.aKey;
                case KeyCode.S: return k.sKey;
                case KeyCode.D: return k.dKey;
                case KeyCode.UpArrow: return k.upArrowKey;
                case KeyCode.DownArrow: return k.downArrowKey;
                case KeyCode.LeftArrow: return k.leftArrowKey;
                case KeyCode.RightArrow: return k.rightArrowKey;
                case KeyCode.Space: return k.spaceKey;
                case KeyCode.R: return k.rKey;
                case KeyCode.T: return k.tKey;
                case KeyCode.X: return k.xKey;
                case KeyCode.H: return k.hKey;
                case KeyCode.E: return k.eKey;
                case KeyCode.G: return k.gKey;
                case KeyCode.F3: return k.f3Key;
                case KeyCode.Alpha1: return k.digit1Key;
                case KeyCode.Alpha2: return k.digit2Key;
                case KeyCode.Alpha3: return k.digit3Key;
                case KeyCode.Alpha4: return k.digit4Key;
                case KeyCode.Alpha5: return k.digit5Key;
                case KeyCode.Delete: return k.deleteKey;
                case KeyCode.Escape: return k.escapeKey;
                default:
                    Debug.LogWarning($"GameInput doesn't know key {key} yet. Add it to GameInput.Find.");
                    return null;
            }
        }
#endif
    }
}
