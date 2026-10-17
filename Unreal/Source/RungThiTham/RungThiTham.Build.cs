using UnrealBuildTool;

// The game module. Game rules live in Rules/ as plain C++ (no engine types), so the same code runs
// in the game, in a dedicated server and in Tools/RulesCheck without the engine.
public class RungThiTham : ModuleRules
{
	public RungThiTham(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
		PublicIncludePaths.Add(ModuleDirectory);
		PublicDependencyModuleNames.AddRange(new string[]
		{
			"Core", "CoreUObject", "Engine", "InputCore", "EnhancedInput", "AIModule", "UMG", "Slate", "SlateCore", "Json", "JsonUtilities"
		});
	}
}
