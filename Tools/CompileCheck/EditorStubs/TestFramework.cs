// Reference stubs for com.unity.test-framework (UnityEngine.TestRunner / UnityEditor.TestRunner).
using System;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
namespace UnityEngine.TestTools
{
    [AttributeUsage(AttributeTargets.Method)] public class UnityTestAttribute : NUnit.Framework.CombiningStrategyAttribute { public UnityTestAttribute() : base(null, null) { } }
    [AttributeUsage(AttributeTargets.Method)] public class UnitySetUpAttribute : NUnit.Framework.NUnitAttribute { }
    [AttributeUsage(AttributeTargets.Method)] public class UnityTearDownAttribute : NUnit.Framework.NUnitAttribute { }
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)] public class UnityPlatformAttribute : NUnit.Framework.NUnitAttribute { public UnityPlatformAttribute() { } public UnityPlatformAttribute(params RuntimePlatform[] include) { } }
    public static class LogAssert
    {
        public static bool ignoreFailingMessages { get; set; }
        public static void Expect(LogType type, string message) { }
        public static void Expect(LogType type, Regex message) { }
        public static void Expect(string message) { }
        public static void Expect(Regex message) { }
        public static void NoUnexpectedReceived() { }
    }
    public interface IEditModeTestYieldInstruction { bool ExpectDomainReload { get; } bool ExpectedPlaymodeState { get; } IEnumerator Perform(); }
    public class EnterPlayMode : IEditModeTestYieldInstruction { public EnterPlayMode() { } public EnterPlayMode(bool expectDomainReload) { } public bool ExpectDomainReload => false; public bool ExpectedPlaymodeState => true; public IEnumerator Perform() => null; }
    public class ExitPlayMode : IEditModeTestYieldInstruction { public bool ExpectDomainReload => false; public bool ExpectedPlaymodeState => false; public IEnumerator Perform() => null; }
    public class RecompileScripts : IEditModeTestYieldInstruction { public RecompileScripts() { } public RecompileScripts(bool expectScriptCompilation, bool expectScriptCompilationSuccess = true) { } public bool ExpectDomainReload => true; public bool ExpectedPlaymodeState => false; public IEnumerator Perform() => null; }
    public class WaitForDomainReload : IEditModeTestYieldInstruction { public bool ExpectDomainReload => true; public bool ExpectedPlaymodeState => false; public IEnumerator Perform() => null; }
}
