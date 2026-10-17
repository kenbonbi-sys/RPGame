using UnrealBuildTool;

// The game module. Game rules live in Rules/ as plain C++ (no engine types), so the same code runs
// in the game, in a dedicated server and in Tools/RulesCheck without the engine.
public class RungThiTham : ModuleRules
{
	public RungThiTham(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
		// each file on its own: the ports keep Godot's file-local helpers and constants (S, Flat, ...),
		// which clash once Unreal glues every .cpp of the module into one unity file
		bUseUnity = false;
		PublicIncludePaths.Add(ModuleDirectory);
		PublicDependencyModuleNames.AddRange(new string[]
		{
			"Core", "CoreUObject", "Engine", "InputCore", "EnhancedInput", "AIModule", "UMG", "Slate", "SlateCore", "Json", "JsonUtilities"
		});
	}
}
