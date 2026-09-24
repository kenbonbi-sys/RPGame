// Reference stubs for com.unity.inputsystem 1.x: devices, controls, settings, low level, UI module.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
namespace UnityEngine.InputSystem.Utilities
{
    public struct ReadOnlyArray<TValue> : IReadOnlyList<TValue>
    {
        public ReadOnlyArray(TValue[] array) { }
        public int Count => 0;
        public TValue this[int index] => default;
        public TValue[] ToArray() => null;
        public int IndexOf(Predicate<TValue> predicate) => -1;
        public IEnumerator<TValue> GetEnumerator() => null;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null;
    }
    public struct InternedString { public InternedString(string text) { } public override string ToString() => null; public static implicit operator string(InternedString str) => null; }
}
namespace UnityEngine.InputSystem.Controls
{
    public class AxisControl : InputControl<float> { }
    public class ButtonControl : AxisControl
    {
        public float pressPoint = -1;
        public bool isPressed => false;
        public bool wasPressedThisFrame => false;
        public bool wasReleasedThisFrame => false;
    }
    public class KeyControl : ButtonControl { public Key keyCode => default; public int scanCode => 0; }
    public class AnyKeyControl : ButtonControl { }
    public class Vector2Control : InputControl<Vector2> { public AxisControl x => null; public AxisControl y => null; }
    public class DeltaControl : Vector2Control { public AxisControl up => null; public AxisControl down => null; public AxisControl left => null; public AxisControl right => null; }
    public class StickControl : Vector2Control { public ButtonControl up => null; public ButtonControl down => null; public ButtonControl left => null; public ButtonControl right => null; }
    public class DpadControl : Vector2Control { public ButtonControl up => null; public ButtonControl down => null; public ButtonControl left => null; public ButtonControl right => null; }
    public class IntegerControl : InputControl<int> { }
}
namespace UnityEngine.InputSystem
{
    public enum Key { None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket, Minus, Equals, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0, LeftShift, RightShift, LeftAlt, RightAlt, AltGr = RightAlt, LeftCtrl, RightCtrl, LeftMeta, RightMeta, LeftWindows = LeftMeta, RightWindows = RightMeta, LeftApple = LeftMeta, RightApple = RightMeta, LeftCommand = LeftMeta, RightCommand = RightMeta, ContextMenu, Escape, LeftArrow, RightArrow, UpArrow, DownArrow, Backspace, PageDown, PageUp, Home, End, Insert, Delete, CapsLock, NumLock, PrintScreen, ScrollLock, Pause, NumpadEnter, NumpadDivide, NumpadMultiply, NumpadPlus, NumpadMinus, NumpadPeriod, NumpadEquals, Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, OEM1, OEM2, OEM3, OEM4, OEM5, IMESelected }
    public class Keyboard : InputDevice
    {
        public const int KeyCount = 110;
        public static Keyboard current { get; private set; }
        public event Action<char> onTextInput;
        public AnyKeyControl anyKey => null;
        public KeyControl this[Key key] => null;
        public UnityEngine.InputSystem.Utilities.ReadOnlyArray<KeyControl> allKeys => default;
        public KeyControl spaceKey => null;
        public KeyControl enterKey => null;
        public KeyControl numpadEnterKey => null;
        public KeyControl tabKey => null;
        public KeyControl backquoteKey => null;
        public KeyControl escapeKey => null;
        public KeyControl backspaceKey => null;
        public KeyControl leftShiftKey => null;
        public KeyControl rightShiftKey => null;
        public KeyControl leftCtrlKey => null;
        public KeyControl rightCtrlKey => null;
        public KeyControl leftAltKey => null;
        public KeyControl rightAltKey => null;
        public ButtonControl shiftKey => null;
        public ButtonControl ctrlKey => null;
        public ButtonControl altKey => null;
        public KeyControl upArrowKey => null;
        public KeyControl downArrowKey => null;
        public KeyControl leftArrowKey => null;
        public KeyControl rightArrowKey => null;
        public KeyControl aKey => null;
        public KeyControl bKey => null;
        public KeyControl cKey => null;
        public KeyControl dKey => null;
        public KeyControl eKey => null;
        public KeyControl fKey => null;
        public KeyControl gKey => null;
        public KeyControl hKey => null;
        public KeyControl iKey => null;
        public KeyControl jKey => null;
        public KeyControl kKey => null;
        public KeyControl lKey => null;
        public KeyControl mKey => null;
        public KeyControl nKey => null;
        public KeyControl oKey => null;
        public KeyControl pKey => null;
        public KeyControl qKey => null;
        public KeyControl rKey => null;
        public KeyControl sKey => null;
        public KeyControl tKey => null;
        public KeyControl uKey => null;
        public KeyControl vKey => null;
        public KeyControl wKey => null;
        public KeyControl xKey => null;
        public KeyControl yKey => null;
        public KeyControl zKey => null;
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
        public KeyControl f1Key => null;
        public KeyControl f2Key => null;
        public KeyControl f3Key => null;
        public KeyControl f4Key => null;
        public KeyControl f5Key => null;
        public KeyControl f6Key => null;
        public KeyControl f7Key => null;
        public KeyControl f8Key => null;
        public KeyControl f9Key => null;
        public KeyControl f10Key => null;
        public KeyControl f11Key => null;
        public KeyControl f12Key => null;
        public KeyControl pageUpKey => null;
        public KeyControl pageDownKey => null;
        public KeyControl homeKey => null;
        public KeyControl endKey => null;
        public KeyControl deleteKey => null;
        public KeyControl insertKey => null;
    }
    public class Pointer : InputDevice
    {
        public static Pointer current { get; internal set; }
        public Vector2Control position => null;
        public DeltaControl delta => null;
        public ButtonControl press => null;
    }
    public class Mouse : Pointer
    {
        public static new Mouse current { get; private set; }
        public DeltaControl scroll => null;
        public ButtonControl leftButton => null;
        public ButtonControl middleButton => null;
        public ButtonControl rightButton => null;
        public ButtonControl backButton => null;
        public ButtonControl forwardButton => null;
        public IntegerControl clickCount => null;
        public void WarpCursorPosition(Vector2 position) { }
    }
    public class Gamepad : InputDevice
    {
        public static Gamepad current { get; private set; }
        public static UnityEngine.InputSystem.Utilities.ReadOnlyArray<Gamepad> all => default;
        public ButtonControl buttonWest => null;
        public ButtonControl buttonNorth => null;
        public ButtonControl buttonSouth => null;
        public ButtonControl buttonEast => null;
        public ButtonControl leftStickButton => null;
        public ButtonControl rightStickButton => null;
        public ButtonControl startButton => null;
        public ButtonControl selectButton => null;
        public DpadControl dpad => null;
        public ButtonControl leftShoulder => null;
        public ButtonControl rightShoulder => null;
        public ButtonControl leftTrigger => null;
        public ButtonControl rightTrigger => null;
        public StickControl leftStick => null;
        public StickControl rightStick => null;
        public ButtonControl aButton => null;
        public ButtonControl bButton => null;
        public ButtonControl xButton => null;
        public ButtonControl yButton => null;
        public void SetMotorSpeeds(float lowFrequency, float highFrequency) { }
    }
    public class InputSettings : ScriptableObject
    {
        public enum UpdateMode { ProcessEventsInDynamicUpdate = 1, ProcessEventsInFixedUpdate, ProcessEventsManually }
        public enum BackgroundBehavior { ResetAndDisableNonBackgroundDevices = 0, ResetAndDisableAllDevices = 1, IgnoreFocus = 2 }
        public enum EditorInputBehaviorInPlayMode { PointersAndKeyboardsRespectGameViewFocus = 0, AllDevicesRespectGameViewFocus = 1, AllDeviceInputAlwaysGoesToGameView = 2 }
        public UpdateMode updateMode { get; set; }
        public BackgroundBehavior backgroundBehavior { get; set; }
        public EditorInputBehaviorInPlayMode editorInputBehaviorInPlayMode { get; set; }
        public float defaultButtonPressPoint { get; set; }
        public float buttonReleaseThreshold { get; set; }
        public bool compensateForScreenOrientation { get; set; }
    }
    public static class InputSystem
    {
        public static InputSettings settings { get; set; }
        public static string version => null;
        public static UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice> devices => default;
        public static event Action<InputDevice, InputDeviceChange> onDeviceChange;
        public static event Action<object, InputActionChange> onActionChange;
        public static TDevice AddDevice<TDevice>(string name = null) where TDevice : InputDevice => null;
        public static InputDevice AddDevice(string layout, string name = null, string variants = null) => null;
        public static void AddDevice(InputDevice device) { }
        public static void RemoveDevice(InputDevice device) { }
        public static void EnableDevice(InputDevice device) { }
        public static void DisableDevice(InputDevice device, bool keepSendingEvents = false) { }
        public static TDevice GetDevice<TDevice>() where TDevice : InputDevice => null;
        public static InputDevice GetDevice(string nameOrLayout) => null;
        public static void QueueStateEvent<TState>(InputDevice device, TState state, double time = -1) where TState : struct, IInputStateTypeInfo { }
        public static void QueueDeltaStateEvent<TDelta>(InputControl control, TDelta delta, double time = -1) where TDelta : struct { }
        public static void Update() { }
        public static void ResetDevice(InputDevice device, bool alsoResetDontResetControls = false) { }
        public static void FlushDisconnectedDevices() { }
        public static List<InputAction> ListEnabledActions() => null;
    }
    public enum InputDeviceChange { Added, Removed, Disconnected, Reconnected, Enabled, Disabled, UsageChanged, ConfigurationChanged, SoftReset, HardReset }
    public enum InputActionChange { ActionEnabled, ActionDisabled, ActionMapEnabled, ActionMapDisabled, ActionStarted, ActionPerformed, ActionCanceled, BoundControlsAboutToChange, BoundControlsChanged }
    public class PlayerInput : MonoBehaviour { public InputActionAsset actions { get; set; } public string currentControlScheme => null; }
}
namespace UnityEngine.InputSystem.LowLevel
{
    public struct FourCC { public FourCC(int code) { } public FourCC(char a, char b = ' ', char c = ' ', char d = ' ') { } }
    public interface IInputStateTypeInfo { FourCC format { get; } }
    public struct KeyboardState : IInputStateTypeInfo
    {
        public KeyboardState(params Key[] pressedKeys) { }
        public KeyboardState(IMEEventState imeEventState = default, params Key[] pressedKeys) { }
        public void Set(Key key, bool state) { }
        public void Press(Key key) { }
        public void Release(Key key) { }
        public FourCC format => default;
    }
    public struct IMEEventState { }
    public struct MouseState : IInputStateTypeInfo
    {
        public Vector2 position; public Vector2 delta; public Vector2 scroll; public ushort buttons; public ushort clickCount;
        public MouseState WithButton(MouseButton button, bool state = true) => this;
        public FourCC format => default;
    }
    public enum MouseButton { Left, Right, Middle, Forward, Back }
    public struct GamepadState : IInputStateTypeInfo
    {
        public uint buttons; public Vector2 leftStick; public Vector2 rightStick; public float leftTrigger; public float rightTrigger;
        public GamepadState(params GamepadButton[] buttons) { this.buttons = 0; leftStick = default; rightStick = default; leftTrigger = 0; rightTrigger = 0; }
        public GamepadState WithButton(GamepadButton button, bool value = true) => this;
        public FourCC format => default;
    }
    public enum GamepadButton { DpadUp = 0, DpadDown = 1, DpadLeft = 2, DpadRight = 3, North = 4, East = 5, South = 6, West = 7, LeftStick = 8, RightStick = 9, LeftShoulder = 10, RightShoulder = 11, Start = 12, Select = 13, LeftTrigger = 32, RightTrigger = 33, X = West, Y = North, A = South, B = East, Cross = South, Square = West, Triangle = North, Circle = East }
    public static class InputState { public static double currentTime => 0; public static uint updateCount => 0; }
}
namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule
    {
        public InputActionAsset actionsAsset { get; set; }
        public UnityEngine.InputSystem.InputActionReference point { get; set; }
        public UnityEngine.InputSystem.InputActionReference leftClick { get; set; }
        public UnityEngine.InputSystem.InputActionReference rightClick { get; set; }
        public UnityEngine.InputSystem.InputActionReference middleClick { get; set; }
        public UnityEngine.InputSystem.InputActionReference scrollWheel { get; set; }
        public UnityEngine.InputSystem.InputActionReference move { get; set; }
        public UnityEngine.InputSystem.InputActionReference submit { get; set; }
        public UnityEngine.InputSystem.InputActionReference cancel { get; set; }
        public bool deselectOnBackgroundClick { get; set; }
        public void AssignDefaultActions() { }
        public void UnassignActions() { }
        public override void Process() { }
    }
}
namespace UnityEngine.InputSystem
{
    public class InputActionReference : ScriptableObject { public InputAction action => null; public static InputActionReference Create(InputAction action) => null; public void Set(InputAction action) { } }
}
