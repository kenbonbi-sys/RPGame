using UnrealBuildTool;

public class RungThiThamTarget : TargetRules
{
	public RungThiThamTarget(TargetInfo Target) : base(Target)
	{
		Type = TargetType.Game;
		DefaultBuildSettings = BuildSettingsVersion.Latest;
		IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
		ExtraModuleNames.Add("RungThiTham");
	}
}
