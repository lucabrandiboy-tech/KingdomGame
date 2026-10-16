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
        public KeyControl digit1Key => null;
        public KeyControl digit2Key => null;
        public KeyControl digit3Key => null;
        public KeyControl digit4Key => null;
        public KeyControl digit5Key => null;
        public KeyControl digit6Key => null;
        public KeyControl digit7Key => null;
        public KeyControl digit8Key => null;
        public KeyControl digit9Key => null;
        public KeyControl digit0Key => null;
        public KeyControl tabKey => null;
        public KeyControl qKey => null;
        public KeyControl fKey => null;
        public KeyControl pKey => null;
        public KeyControl enterKey => null;
        public KeyControl deleteKey => null;
        public KeyControl nKey => null;
        public KeyControl f8Key => null;
        public KeyControl f9Key => null;
        public KeyControl f10Key => null;
        public KeyControl leftBracketKey => null;
        public KeyControl rightBracketKey => null;
        public KeyControl leftShiftKey => null;
        public KeyControl bKey => null;
        public KeyControl vKey => null;
        public KeyControl uKey => null;
        public KeyControl yKey => null;
        public KeyControl kKey => null;
        public KeyControl jKey => null;
        public KeyControl oKey => null;
        public KeyControl rightShiftKey => null;
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
        public DeltaControl scroll { get; protected set; }
    }
}

namespace UnityEngine.InputSystem.Controls
{
    public class ButtonControl : InputControl<float>
    {
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
        public bool wasReleasedThisFrame => false;
    }

    public class KeyControl : ButtonControl { }

    public class Vector2Control : InputControl<Vector2> { }

    public class DeltaControl : Vector2Control { }
}
