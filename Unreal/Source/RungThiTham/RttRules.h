// The game's rules inside the engine: the plain C++ rules (Rules/) with their numbers loaded from
// Content/Data/*.json, the same files Tools/RulesCheck tests against.
#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "Rules/RttArmour.h"
#include "Rules/RttJson.h"
#include "Rules/RttTuning.h"

#include "RttRules.generated.h"

// Every data file the rules read.
struct FRttRuleSet
{
	Rtt::Tuning Tuning;
	Rtt::ArmourTable Armour;
	Rtt::JsonValue Classes;
	Rtt::JsonValue Races;
	Rtt::JsonValue Abilities;
	Rtt::JsonValue Items;
};

// Content/Data/, where unity_to_json.py writes the data.
RUNGTHITHAM_API FString RttDataDir();
// Reads the data files in `Dir` into `Out`; false (with the file and the reason in `Error`) when one is missing or broken.
RUNGTHITHAM_API bool RttLoadRules(const FString& Dir, FRttRuleSet& Out, FString& Error);

UCLASS()
class RUNGTHITHAM_API URttRules : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;

	const FRttRuleSet& Get() const { return Rules; }
	bool IsLoaded() const { return bLoaded; }

private:
	FRttRuleSet Rules;
	bool bLoaded = false;
};
