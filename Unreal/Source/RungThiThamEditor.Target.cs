using UnrealBuildTool;

public class RungThiThamEditorTarget : TargetRules
{
	public RungThiThamEditorTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Editor;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		ExtraModuleNames.Add("RungThiTham");
	}
}
