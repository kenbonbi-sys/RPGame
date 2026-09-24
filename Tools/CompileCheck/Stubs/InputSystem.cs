// Reference stubs for com.unity.inputsystem 1.x. Signatures only.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine.InputSystem
{
    public enum InputActionType { Value = 0, Button = 1, PassThrough = 2 }
    public enum InputActionPhase { Disabled, Waiting, Started, Performed, Canceled }
    public struct InputBinding : IEquatable<InputBinding>
    {
        public const char Separator = ';';
        public InputBinding(string path, string action = null, string groups = null, string processors = null, string interactions = null, string name = null) : this() { }
        public string name { get; set; }
        public Guid id { get; set; }
        public string path { get; set; }
        public string overridePath { get; set; }
        public string effectivePath => null;
        public string groups { get; set; }
        public string action { get; set; }
        public string interactions { get; set; }
        public string processors { get; set; }
        public bool isComposite { get; set; }
        public bool isPartOfComposite { get; set; }
        public bool hasOverrides => false;
        public static InputBinding MaskByGroup(string group) => default;
        public static InputBinding MaskByGroups(params string[] groups) => default;
        public string ToDisplayString(DisplayStringOptions options = default, InputControl control = default) => null;
        public bool Matches(InputBinding binding) => false;
        public bool Equals(InputBinding other) => false;
        [Flags] public enum DisplayStringOptions { DontUseShortDisplayNames = 1, DontOmitDevice = 2, DontIncludeInteractions = 4, IgnoreBindingOverrides = 8 }
    }
    public struct InputControlScheme : IEquatable<InputControlScheme>
    {
        public string name => null;
        public string bindingGroup { get; set; }
        public bool Equals(InputControlScheme other) => false;
        public struct DeviceRequirement { public string controlPath { get; set; } public bool isOptional { get; set; } }
    }
    public class InputControl
    {
        public string name => null;
        public string displayName => null;
        public string shortDisplayName => null;
        public string path => null;
        public string layout => null;
        public InputDevice device => null;
        public InputControl parent => null;
        public bool IsPressed(float buttonPressPoint = 0) => false;
        public object ReadValueAsObject() => null;
        public float EvaluateMagnitude() => 0;
    }
    public abstract class InputControl<TValue> : InputControl where TValue : struct
    {
        public TValue value => default;
        public TValue ReadValue() => default;
        public TValue ReadDefaultValue() => default;
        public TValue ReadUnprocessedValue() => default;
    }
    public class InputDevice : InputControl
    {
        public int deviceId => 0;
        public bool added => false;
        public bool enabled => false;
        public bool wasUpdatedThisFrame => false;
        public double lastUpdateTime => 0;
        public System.Collections.ObjectModel.ReadOnlyCollection<InputControl> allControls => null;
        public virtual void MakeCurrent() { }
    }
    public class InputActionAsset : ScriptableObject, IInputActionCollection2
    {
        public const string Extension = "inputactions";
        public bool enabled => false;
        public System.Collections.ObjectModel.ReadOnlyCollection<InputActionMap> actionMaps => null;
        public System.Collections.ObjectModel.ReadOnlyCollection<InputControlScheme> controlSchemes => null;
        public InputBinding? bindingMask { get; set; }
        public UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputDevice>? devices { get; set; }
        public IEnumerable<InputBinding> bindings => null;
        public InputAction this[string actionNameOrId] => null;
        public string ToJson() => null;
        public void LoadFromJson(string json) { }
        public static InputActionAsset FromJson(string json) => null;
        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false) => null;
        public InputAction FindAction(Guid guid) => null;
        public InputActionMap FindActionMap(string nameOrId, bool throwIfNotFound = false) => null;
        public InputActionMap FindActionMap(Guid id) => null;
        public int FindBinding(InputBinding mask, out InputAction action) { action = null; return -1; }
        public int FindControlSchemeIndex(string name) => -1;
        public InputControlScheme? FindControlScheme(string name) => null;
        public void Enable() { }
        public void Disable() { }
        public bool Contains(InputAction action) => false;
        public IEnumerator<InputAction> GetEnumerator() => null;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null;
    }
    public sealed class InputActionMap : IInputActionCollection2, ICloneable, IDisposable
    {
        public InputActionMap(string name = null) { }
        public string name => null;
        public InputActionAsset asset => null;
        public Guid id => default;
        public bool enabled => false;
        public System.Collections.ObjectModel.ReadOnlyCollection<InputAction> actions => null;
        public System.Collections.ObjectModel.ReadOnlyCollection<InputBinding> bindings => null;
        IEnumerable<InputBinding> IInputActionCollection2.bindings => null;
        public InputBinding? bindingMask { get; set; }
        public InputAction this[string actionNameOrId] => null;
        public event Action<InputAction.CallbackContext> actionTriggered;
        public int FindBinding(InputBinding mask, out InputAction action) { action = null; return -1; }
        public bool Contains(InputAction action) => false;
        public InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false) => null;
        public InputAction FindAction(Guid id) => null;
        public void Enable() { }
        public void Disable() { }
        public object Clone() => null;
        public void Dispose() { }
        public IEnumerator<InputAction> GetEnumerator() => null;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null;
        public string ToJson() => null;
    }
    public sealed class InputAction : ICloneable, IDisposable
    {
        public InputAction(string name = null, InputActionType type = default, string binding = null, string interactions = null, string processors = null, string expectedControlType = null) { }
        public string name => null;
        public InputActionType type => default;
        public Guid id => default;
        public string expectedControlType { get; set; }
        public string processors => null;
        public string interactions => null;
        public InputActionMap actionMap => null;
        public InputBinding? bindingMask { get; set; }
        public System.Collections.ObjectModel.ReadOnlyCollection<InputBinding> bindings => null;
        public UnityEngine.InputSystem.Utilities.ReadOnlyArray<InputControl> controls => default;
        public InputActionPhase phase => default;
        public bool inProgress => false;
        public bool enabled => false;
        public bool triggered => false;
        public InputControl activeControl => null;
        public Type activeValueType => null;
        public bool wantsInitialStateCheck { get; set; }
        public event Action<CallbackContext> started;
        public event Action<CallbackContext> canceled;
        public event Action<CallbackContext> performed;
        public void Enable() { }
        public void Disable() { }
        public TValue ReadValue<TValue>() where TValue : struct => default;
        public object ReadValueAsObject() => null;
        public float GetControlMagnitude() => 0;
        public void Reset() { }
        public bool IsPressed() => false;
        public bool IsInProgress() => false;
        public bool WasPressedThisFrame() => false;
        public bool WasReleasedThisFrame() => false;
        public bool WasPerformedThisFrame() => false;
        public bool WasCompletedThisFrame() => false;
        public bool WasPressedThisDynamicUpdate() => false;
        public bool WasReleasedThisDynamicUpdate() => false;
        public float GetTimeoutCompletionPercentage() => 0;
        public object Clone() => null;
        public void Dispose() { }
        public override string ToString() => null;
        public struct CallbackContext
        {
            public InputActionPhase phase => default;
            public bool started => false;
            public bool performed => false;
            public bool canceled => false;
            public InputAction action => null;
            public InputControl control => null;
            public double time => 0;
            public double startTime => 0;
            public double duration => 0;
            public Type valueType => null;
            public int valueSizeInBytes => 0;
            public TValue ReadValue<TValue>() where TValue : struct => default;
            public bool ReadValueAsButton() => false;
            public object ReadValueAsObject() => null;
        }
        public sealed class RebindingOperation : IDisposable
        {
            public InputAction action => null;
            public InputControl selectedControl => null;
            public bool started => false;
            public bool completed => false;
            public bool canceled => false;
            public RebindingOperation WithControlsExcluding(string path) => this;
            public RebindingOperation WithCancelingThrough(string binding) => this;
            public RebindingOperation WithTargetBinding(int bindingIndex) => this;
            public RebindingOperation WithBindingGroup(string group) => this;
            public RebindingOperation OnMatchWaitForAnother(float seconds) => this;
            public RebindingOperation OnComplete(Action<RebindingOperation> callback) => this;
            public RebindingOperation OnCancel(Action<RebindingOperation> callback) => this;
            public RebindingOperation Start() => this;
            public void Cancel() { }
            public void Dispose() { }
        }
    }
    public static class InputActionSetupExtensions
    {
        public static InputActionMap AddActionMap(this InputActionAsset asset, string name) => null;
        public static void AddActionMap(this InputActionAsset asset, InputActionMap map) { }
        public static void RemoveActionMap(this InputActionAsset asset, InputActionMap map) { }
        public static InputAction AddAction(this InputActionMap map, string name, InputActionType type = default, string binding = null, string interactions = null, string processors = null, string groups = null, string expectedControlLayout = null) => null;
        public static void RemoveAction(this InputAction action) { }
        public static BindingSyntax AddBinding(this InputAction action, string path, string interactions = null, string processors = null, string groups = null) => default;
        public static BindingSyntax AddBinding(this InputAction action, InputBinding binding = default) => default;
        public static BindingSyntax AddBinding(this InputActionMap actionMap, string path, InputAction action, string interactions = null, string processors = null, string groups = null) => default;
        public static CompositeSyntax AddCompositeBinding(this InputAction action, string composite, string interactions = null, string processors = null) => default;
        public static BindingSyntax ChangeBinding(this InputAction action, int index) => default;
        public static BindingSyntax ChangeCompositeBinding(this InputAction action, string compositeName) => default;
        public static ControlSchemeSyntax AddControlScheme(this InputActionAsset asset, string name) => default;
        public static void AddControlScheme(this InputActionAsset asset, InputControlScheme controlScheme) { }
        public static void RemoveControlScheme(this InputActionAsset asset, string name) { }
        public struct BindingSyntax
        {
            public bool valid => false;
            public int bindingIndex => -1;
            public InputBinding binding => default;
            public BindingSyntax WithName(string name) => this;
            public BindingSyntax WithPath(string path) => this;
            public BindingSyntax WithGroup(string group) => this;
            public BindingSyntax WithGroups(string groups) => this;
            public BindingSyntax WithInteraction(string interaction) => this;
            public BindingSyntax WithInteractions(string interactions) => this;
            public BindingSyntax WithProcessor(string processor) => this;
            public BindingSyntax WithProcessors(string processors) => this;
            public BindingSyntax Triggering(InputAction action) => this;
            public BindingSyntax To(InputBinding binding) => this;
            public BindingSyntax NextBinding() => this;
            public BindingSyntax PreviousBinding() => this;
            public BindingSyntax NextPartBinding(string partName) => this;
            public BindingSyntax NextCompositeBinding(string compositeName = null) => this;
            public BindingSyntax InsertPartBinding(string partName, string path) => this;
            public void Erase() { }
        }
        public struct CompositeSyntax
        {
            public int bindingIndex => -1;
            public CompositeSyntax With(string name, string binding, string groups = null, string processors = null) => this;
        }
        public struct ControlSchemeSyntax
        {
            public ControlSchemeSyntax WithBindingGroup(string bindingGroup) => this;
            public ControlSchemeSyntax WithRequiredDevice<TDevice>() where TDevice : InputDevice => this;
            public ControlSchemeSyntax WithOptionalDevice<TDevice>() where TDevice : InputDevice => this;
            public ControlSchemeSyntax WithRequiredDevice(string controlPath) => this;
            public ControlSchemeSyntax WithOptionalDevice(string controlPath) => this;
            public ControlSchemeSyntax OrWithRequiredDevice(string controlPath) => this;
            public InputControlScheme Done() => default;
        }
    }
    public static class InputActionRebindingExtensions
    {
        public static string GetBindingDisplayString(this InputAction action, InputBinding.DisplayStringOptions options = default, string group = null) => null;
        public static string GetBindingDisplayString(this InputAction action, InputBinding bindingMask, InputBinding.DisplayStringOptions options = default) => null;
        public static string GetBindingDisplayString(this InputAction action, int bindingIndex, InputBinding.DisplayStringOptions options = default) => null;
        public static string GetBindingDisplayString(this InputAction action, int bindingIndex, out string deviceLayoutName, out string controlPath, InputBinding.DisplayStringOptions options = default) { deviceLayoutName = null; controlPath = null; return null; }
        public static int GetBindingIndex(this InputAction action, InputBinding bindingMask) => -1;
        public static int GetBindingIndex(this InputAction action, string group = null, string path = null) => -1;
        public static int GetBindingIndexForControl(this InputAction action, InputControl control) => -1;
        public static void ApplyBindingOverride(this InputAction action, string newPath, string group = null, string path = null) { }
        public static void ApplyBindingOverride(this InputAction action, InputBinding bindingOverride) { }
        public static void ApplyBindingOverride(this InputAction action, int bindingIndex, InputBinding bindingOverride) { }
        public static void ApplyBindingOverride(this InputAction action, int bindingIndex, string path) { }
        public static void RemoveBindingOverride(this InputAction action, int bindingIndex) { }
        public static void RemoveAllBindingOverrides(this InputAction action) { }
        public static void RemoveAllBindingOverrides(this IInputActionCollection2 actions) { }
        public static string SaveBindingOverridesAsJson(this IInputActionCollection2 actions) => null;
        public static string SaveBindingOverridesAsJson(this InputAction action) => null;
        public static void LoadBindingOverridesFromJson(this IInputActionCollection2 actions, string json, bool removeExisting = true) { }
        public static void LoadBindingOverridesFromJson(this InputAction action, string json, bool removeExisting = true) { }
        public static InputAction.RebindingOperation PerformInteractiveRebinding(this InputAction action, int bindingIndex = -1) => null;
    }
    public interface IInputActionCollection : IEnumerable<InputAction> { InputBinding? bindingMask { get; set; } bool Contains(InputAction action); void Enable(); void Disable(); }
    public interface IInputActionCollection2 : IInputActionCollection { IEnumerable<InputBinding> bindings { get; } InputAction FindAction(string actionNameOrId, bool throwIfNotFound = false); int FindBinding(InputBinding mask, out InputAction action); }
}
