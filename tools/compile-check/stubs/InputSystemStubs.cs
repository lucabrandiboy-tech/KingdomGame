// Compile-check stubs: ONLY the Input System members the KingdomGame scripts use,
// with signatures copied from com.unity.inputsystem 1.20
// (github.com/Unity-Technologies/InputSystem, Packages/com.unity.inputsystem/InputSystem/Runtime).
namespace UnityEngine.InputSystem
{
    using UnityEngine.InputSystem.Controls;

    public abstract class InputControl { }

    public abstract class InputControl<TValue> : InputControl where TValue : struct
    {
        public TValue ReadValue() => default;
    }

    public class Keyboard
    {
        public static Keyboard current { get; private set; }
        public KeyControl wKey => null;
        public KeyControl aKey => null;
        public KeyControl sKey => null;
        public KeyControl dKey => null;
        public KeyControl upArrowKey => null;
        public KeyControl downArrowKey => null;
        public KeyControl leftArrowKey => null;
        public KeyControl rightArrowKey => null;
        public KeyControl spaceKey => null;
        public KeyControl rKey => null;
        public KeyControl tKey => null;
        public KeyControl xKey => null;
        public KeyControl hKey => null;
        public KeyControl eKey => null;
        public KeyControl gKey => null;
        public KeyControl f3Key => null;
        public KeyControl escapeKey => null;
    }

    public class Pointer
    {
        public Vector2Control position { get; protected set; }
    }

    public class Mouse : Pointer
    {
        public static Mouse current { get; private set; }
        public ButtonControl leftButton { get; protected set; }
        public ButtonControl rightButton { get; protected set; }
    }
}

namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl : InputControl<float>
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
    }

    public class KeyControl : ButtonControl { }

    public class Vector2Control : InputControl<Vector2> { }
}
